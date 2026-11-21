using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRPing
{
    // Reads the ping key and draws the pings. One ping per player at a time (a new one replaces it). Each ping:
    //   - a light beam rising from the spot (LineRenderer, in the player's color);
    //   - a pulsing diamond with a dark glass label: the object's name if one was pinged, then
    //     "<player> · <distance> m";
    //   - when the spot is off screen or behind, the diamond sticks to the screen edge with an arrow toward it;
    //   - a short two-note chime when it appears. Fades in, then out over its last second.
    internal partial class PingView : MonoBehaviour
    {
        private const float MaxRange = 400f;
        private const float NoHitDistance = 150f;
        private const float EdgeMargin = 70f;
        private const float BeamHeight = 30f;

        private static PingView current;

        private class Ping
        {
            internal string sender;
            internal Vector3 pos;
            internal Transform target; // followed while it exists (a pinged slime, plort, object)
            internal string objectName;
            internal Color color;
            internal float born;
            internal RectTransform root;
            internal RectTransform diamond;
            internal RectTransform arrow;
            internal RectTransform labelBox;
            internal TextMeshProUGUI label;
            internal CanvasGroup group;
            internal LineRenderer beam;
            internal RectTransform mapRoot; // the pin on the map screen (PingView.Map.cs)
            internal TextMeshProUGUI mapLabel;
            internal RectTransform mapLabelBox;
            internal bool desert;           // which map the spot belongs to
        }

        private readonly List<Ping> pings = new List<Ping>();
        private HudUI hud;
        private TMP_FontAsset font;
        private Canvas canvas;
        private AudioSource audioSource;
        private AudioClip chime;
        private Material beamMaterial;

        internal void Init(HudUI vanilla)
        {
            hud = vanilla;
            current = this;
            if (vanilla.dayText != null) font = vanilla.dayText.font as TMP_FontAsset;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            chime = Chime();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) beamMaterial = new Material(shader) { mainTexture = Sprites.BeamTexture() };

            // Markers are placed right before the UI is drawn, after the camera has moved this frame (placing
            // them in Update used last frame's camera, so they lagged and jittered while moving).
            Canvas.willRenderCanvases += PlaceMarkers;
            Canvas.willRenderCanvases += PlaceMapMarkers;
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= PlaceMarkers;
            Canvas.willRenderCanvases -= PlaceMapMarkers;
            foreach (Ping p in pings) DestroyPing(p);
            pings.Clear();
            if (current == this) current = null;
        }

        // ---- input ------------------------------------------------------------------------------------

        private void Update()
        {
            if (hud == null) { Destroy(gameObject); return; }
            bool hudVisible = hud.uiContainer != null && hud.uiContainer.activeInHierarchy;
            canvas.enabled = hudVisible && !MapOpen(); // the map shows its own pins

            if (hudVisible && Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked && !ChatOpen() && Input.GetKeyDown(Main.Key))
            {
                try { PingAim(); }
                catch (Exception e) { Main.Log("ping failed: " + e.Message); }
            }
            UpdatePings();
        }

        // SRMP's chat box is typed into while open: a letter ping key must not fire then.
        private static readonly System.Reflection.FieldInfo openChatField = HarmonyLib.AccessTools.Field(typeof(ChatUI), "openChat");

        private static bool ChatOpen()
        {
            try
            {
                ChatUI chat = SRSingleton<ChatUI>.Instance;
                return chat != null && openChatField != null && (bool)openChatField.GetValue(chat);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void PingAim()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            GameObject player = SRSingleton<SceneContext>.Instance != null ? SRSingleton<SceneContext>.Instance.Player : null;

            RaycastHit[] hits = Physics.RaycastAll(ray, MaxRange, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (player != null && hit.collider.transform.IsChildOf(player.transform)) continue;
                Identifiable ident = hit.collider.GetComponentInParent<Identifiable>();
                Identifiable.Id id = ident != null ? ident.id : Identifiable.Id.NONE;
                PingNet.Send(ident != null ? ident.transform.position : hit.point, id, ident != null ? ident.transform : null);
                return;
            }
            PingNet.Send(ray.GetPoint(NoHitDistance), Identifiable.Id.NONE, null);
        }

        // ---- showing ----------------------------------------------------------------------------------

        internal static void Show(string sender, bool own, Vector3 pos, Identifiable.Id id, Transform target)
        {
            if (current == null) return;
            if (target == null && id != Identifiable.Id.NONE) target = FindNear(pos, id);
            current.Add(sender, own, pos, id, target);
        }

        // The other player's copy of the pinged object: the nearest object of that type close to the spot.
        private static Transform FindNear(Vector3 pos, Identifiable.Id id)
        {
            Transform best = null;
            float bestDist = float.MaxValue;
            foreach (Collider c in Physics.OverlapSphere(pos, 4f, ~0, QueryTriggerInteraction.Collide))
            {
                Identifiable ident = c.GetComponentInParent<Identifiable>();
                if (ident == null || ident.id != id) continue;
                float d = (ident.transform.position - pos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = ident.transform; }
            }
            return best;
        }

        private void Add(string sender, bool own, Vector3 pos, Identifiable.Id id, Transform target)
        {
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                if (pings[i].sender != sender) continue;
                DestroyPing(pings[i]);
                pings.RemoveAt(i);
            }

            var p = new Ping { sender = sender, pos = pos, target = target, born = Time.time, color = ColorFor(sender, own) };
            p.objectName = id != Identifiable.Id.NONE ? NameOf(id) : null;
            p.desert = InDesert(pos);
            Build(p);
            pings.Add(p);
            if (chime != null) audioSource.PlayOneShot(chime, Main.Volume);
        }

        private void Build(Ping p)
        {
            p.root = NewRect("Ping " + p.sender, transform);
            p.group = p.root.gameObject.AddComponent<CanvasGroup>();
            p.group.blocksRaycasts = false;

            p.arrow = NewImage("Arrow", p.root, Sprites.Arrow(), p.color, new Vector2(26f, 26f));
            p.diamond = NewImage("Diamond", p.root, Sprites.Diamond(), p.color, new Vector2(24f, 24f));
            NewImage("Core", p.diamond, Sprites.Diamond(), new Color(1f, 1f, 1f, 0.9f), new Vector2(9f, 9f));

            p.labelBox = NewImage("LabelBox", p.root, Sprites.Rounded(), new Color(0.055f, 0.063f, 0.082f, 0.72f), new Vector2(120f, 40f));
            p.labelBox.pivot = new Vector2(0.5f, 0f);
            p.labelBox.anchoredPosition = new Vector2(0f, 22f);
            var image = p.labelBox.GetComponent<Image>();
            image.type = Image.Type.Sliced;

            p.label = NewRect("Label", p.labelBox).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) p.label.font = font;
            p.label.fontSize = 15f;
            p.label.alignment = TextAlignmentOptions.Center;
            p.label.enableWordWrapping = false;
            p.label.raycastTarget = false;
            p.label.color = Color.white;
            var lrt = p.label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 4f);
            lrt.offsetMax = new Vector2(-10f, -4f);

            if (beamMaterial != null)
            {
                var go = new GameObject("SRPing beam");
                p.beam = go.AddComponent<LineRenderer>();
                p.beam.material = beamMaterial;
                p.beam.useWorldSpace = true;
                p.beam.positionCount = 2;
                p.beam.SetPosition(0, p.pos);
                p.beam.SetPosition(1, p.pos + Vector3.up * BeamHeight);
                p.beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 0.08f));
                p.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                p.beam.receiveShadows = false;
            }
        }

        private void UpdatePings()
        {
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                if (Time.time - pings[i].born <= Main.PingSeconds) continue;
                DestroyPing(pings[i]);
                pings.RemoveAt(i);
            }
        }

        private void PlaceMarkers()
        {
            if (pings.Count == 0 || canvas == null) return;
            Camera cam = Camera.main;
            float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            for (int i = pings.Count - 1; i >= 0; i--)
            {
                Ping p = pings[i];
                float age = Time.time - p.born;
                if (cam == null) continue;
                if (p.target != null) p.pos = p.target.position; // follows the object until it is gone
                float alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01(Main.PingSeconds - age);
                p.group.alpha = alpha;

                if (p.beam != null)
                {
                    Color top = p.color;
                    top.a = 0f;
                    Color bottom = p.color;
                    bottom.a = 0.75f * alpha;
                    p.beam.startColor = bottom;
                    p.beam.endColor = top;
                    p.beam.SetPosition(0, p.pos);
                    p.beam.SetPosition(1, p.pos + Vector3.up * BeamHeight);
                }

                int meters = Mathf.RoundToInt(Vector3.Distance(cam.transform.position, p.pos));
                string who = "<color=#" + ColorUtility.ToHtmlStringRGB(p.color) + ">" + p.sender + "</color>  <alpha=#AA>" + meters + " m";
                string text = p.objectName != null ? "<b>" + p.objectName + "</b>\n<size=13>" + who + "</size>" : "<size=14>" + who + "</size>";
                if (p.label.text != text)
                {
                    p.label.text = text;
                    Vector2 size = p.label.GetPreferredValues(text);
                    p.labelBox.sizeDelta = new Vector2(size.x + 22f, size.y + 10f);
                }

                // Screen position (overlay canvas: positions are in screen pixels).
                Vector3 sp = cam.WorldToScreenPoint(p.pos + Vector3.up * 0.8f);
                bool behind = sp.z < 0f;
                var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var flat = new Vector2(sp.x, sp.y);
                if (behind) flat = center - (flat - center); // mirrored: points the right way when behind
                float margin = EdgeMargin * scale;
                bool onScreen = !behind && flat.x > margin && flat.x < Screen.width - margin && flat.y > margin && flat.y < Screen.height - margin;

                float pulse = 1f + 0.12f * Mathf.Sin(age * 6f);
                p.diamond.localScale = new Vector3(pulse, pulse, 1f);

                if (onScreen)
                {
                    p.root.position = Snap(flat); // whole pixels: no text shimmer
                    p.arrow.gameObject.SetActive(false);
                }
                else
                {
                    Vector2 dir = flat - center;
                    if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                    dir.Normalize();
                    // Push from the center along dir until the margin rectangle's edge.
                    float hx = center.x - margin, hy = center.y - margin;
                    float t = Mathf.Min(Mathf.Abs(dir.x) > 1e-4f ? hx / Mathf.Abs(dir.x) : float.MaxValue,
                                        Mathf.Abs(dir.y) > 1e-4f ? hy / Mathf.Abs(dir.y) : float.MaxValue);
                    p.root.position = Snap(center + dir * t);
                    p.arrow.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    p.arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                    p.arrow.anchoredPosition = dir * 30f;
                }
            }
        }

        private static Vector3 Snap(Vector2 v)
        {
            return new Vector3(Mathf.Round(v.x), Mathf.Round(v.y), 0f);
        }

        private static void DestroyPing(Ping p)
        {
            if (p.root != null) Destroy(p.root.gameObject);
            if (p.beam != null) Destroy(p.beam.gameObject);
            if (p.mapRoot != null) Destroy(p.mapRoot.gameObject);
        }

        // ---- helpers ----------------------------------------------------------------------------------

        private static readonly Color Own = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color[] Others =
        {
            new Color(1f, 0.62f, 0.25f, 1f), new Color(1f, 0.45f, 0.75f, 1f),
            new Color(0.55f, 0.95f, 0.45f, 1f), new Color(1f, 0.88f, 0.35f, 1f),
        };

        private static Color ColorFor(string sender, bool own)
        {
            if (own) return Own;
            int h = 0;
            foreach (char c in sender) h = h * 31 + c;
            return Others[(h & 0x7fffffff) % Others.Length];
        }

        private static string NameOf(Identifiable.Id id)
        {
            try
            {
                string name = Identifiable.GetName(id, false);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch (Exception)
            {
                // fall through to the enum name
            }
            return id.ToString().Replace('_', ' ').ToLowerInvariant();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static RectTransform NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            rt.sizeDelta = size;
            return rt;
        }

        // A soft, short "bloop" (a water-drop pitch rise with a quick fade), quiet and slime-like.
        private static AudioClip Chime()
        {
            const int rate = 44100;
            const float length = 0.22f;
            int n = (int)(rate * length);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float freq = 420f + 520f * (1f - Mathf.Exp(-t * 30f)); // glides up 420 -> ~940 Hz
                phase += 2.0 * Math.PI * freq / rate;
                float env = Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t * 18f);
                data[i] = 0.22f * env * (float)Math.Sin(phase);
            }
            AudioClip clip = AudioClip.Create("SRPing bloop", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
