using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRCorrals
{
    // Shows a corral's upgrade tiers in the world:
    //   - collector / feeder Mk II and III: the machine itself turns silver (Mk II) or gold (Mk III);
    //   - calm / production (no machine of their own): one sign floating above the middle of the corral.
    // Signs always face the camera. Built lazily: the text font comes from the HUD, which may not exist yet when
    // upgrades are applied on load.
    internal class TierVisuals
    {
        internal static readonly Color Tier2 = new Color(0.82f, 0.86f, 0.92f, 1f); // silver (Mk II)
        internal static readonly Color Tier3 = new Color(1f, 0.78f, 0.30f, 1f);    // gold (Mk III)
        private static readonly Color Plate = new Color(0.055f, 0.063f, 0.082f, 0.85f);

        private readonly List<GameObject> built = new List<GameObject>();
        private readonly List<Transform> signs = new List<Transform>();
        private static Sprite rounded;

        internal void Clear()
        {
            Untint();
            foreach (GameObject go in built) if (go != null) Object.Destroy(go);
            built.Clear();
            signs.Clear();
        }

        // Rebuilds everything for the given levels. machine objects may be null (upgrade not bought).
        internal bool Build(Transform corral, GameObject collector, int collectorLevel, GameObject feeder, int feederLevel,
            int calmLevel, int productionLevel)
        {
            TMP_FontAsset font = Font();
            if (font == null) return false; // try again next frame
            Clear();

            if (collector != null && collectorLevel >= 2) Machine(collector, collectorLevel, font);
            if (feeder != null && feederLevel >= 2) Machine(feeder, feederLevel, font);

            string corralText = "";
            if (calmLevel > 0) corralText += "Apaisant " + Roman(calmLevel);
            if (productionLevel > 0) corralText += (corralText.Length > 0 ? "  ·  " : "") + "Stimulateur " + Roman(productionLevel);
            if (corralText.Length > 0)
            {
                Color c = Mathf.Max(calmLevel, productionLevel) >= 2 ? Tier3 : Tier2;
                Sign(corral, corral.position + Vector3.up * 5f, corralText, c, font, 1.6f);
            }
            return true;
        }

        // Gardens and coops: tints the plot's structure for its highest tier (1 = silver, 2 = gold). Only the
        // game's recolorable materials ("SR/Actor/Recolor", the machines and tech parts) change, so the ground
        // keeps its look; plants (SpawnResource) and animals or items (Identifiable) are skipped.
        internal void TintPlot(GameObject plot, int tier)
        {
            Untint();
            if (tier <= 0) return;
            Color light = tier >= 2 ? GoldLight : SilverLight;
            Color dark = tier >= 2 ? GoldDark : SilverDark;
            foreach (Renderer r in plot.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r.GetComponentInParent<SpawnResource>() != null || r.GetComponentInParent<Identifiable>() != null) continue;
                if (!UsesRecolorOnly(r)) continue;
                foreach (Material m in r.materials) TintRecolorPairs(m, light, dark);
            }
        }

        // Checked on the shared materials, so renderers that are not tinted are never given material copies.
        private static bool UsesRecolorOnly(Renderer r)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_Color00")) return false;
            }
            return true;
        }

        // Turns every sign toward the camera (yaw only, so text stays upright).
        internal void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            foreach (Transform s in signs)
            {
                if (s == null) continue;
                Vector3 away = s.position - cam.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f) s.rotation = Quaternion.LookRotation(away);
            }
        }

        // The machine itself takes the tier color: every color property of its materials is pulled toward it, so
        // the textures stay readable but the whole structure reads as silver (Mk II) or gold (Mk III). In game a
        // light and a floating sign mostly lit the ground and were hard to see.
        // Metal palettes: a highlight and a shadow tone. The contrast between the two is what reads as metal; a
        // single mid tone (first version) made Mk II look flat gray instead of silver.
        private static readonly Color SilverLight = new Color(0.96f, 0.97f, 1f, 1f);
        private static readonly Color SilverDark = new Color(0.40f, 0.46f, 0.55f, 1f);
        private static readonly Color GoldLight = new Color(1f, 0.87f, 0.42f, 1f);
        private static readonly Color GoldDark = new Color(0.60f, 0.38f, 0.08f, 1f);

        private void Machine(GameObject machine, int level, TMP_FontAsset font)
        {
            Color flat = level >= 3 ? Tier3 : Tier2;
            Color light = level >= 3 ? GoldLight : SilverLight;
            Color dark = level >= 3 ? GoldDark : SilverDark;
            foreach (Renderer r in machine.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue; // not particles or trails
                foreach (Material m in r.materials) // per-renderer instances: the prefab's shared materials stay untouched
                {
                    if (!TintRecolorPairs(m, light, dark)) Tint(m, flat);
                }
            }
        }

        // The game's "SR/Actor/Recolor" shaders color up to 8 zones, each as a pair _ColorN0 / _ColorN1 (two shades
        // of the zone). The lighter shade of each pair becomes the metal's highlight and the darker its shadow,
        // each keeping a little of its original brightness so details stay visible. False if not such a shader.
        private bool TintRecolorPairs(Material m, Color light, Color dark)
        {
            if (!m.HasProperty("_Color00") || !m.HasProperty("_Color01")) return false;
            for (int zone = 0; zone < 8; zone++)
            {
                int a = Shader.PropertyToID("_Color" + zone + "0");
                int b = Shader.PropertyToID("_Color" + zone + "1");
                if (!m.HasProperty(a) || !m.HasProperty(b)) continue;
                Color ca = m.GetColor(a), cb = m.GetColor(b);
                tinted.Add(new Saved { Material = m, Property = a, Original = ca });
                tinted.Add(new Saved { Material = m, Property = b, Original = cb });

                bool aIsLight = Luma(ca) >= Luma(cb);
                m.SetColor(a, Shade(aIsLight ? light : dark, ca));
                m.SetColor(b, Shade(aIsLight ? dark : light, cb));
            }
            return true;
        }

        private static Color Shade(Color tone, Color original)
        {
            Color c = tone * Mathf.Lerp(0.85f, 1.08f, Luma(original));
            c.a = original.a;
            return c;
        }

        private static float Luma(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        private struct Saved
        {
            public Material Material;
            public int Property;
            public Color Original;
        }

        private readonly List<Saved> tinted = new List<Saved>();
        private static readonly HashSet<string> dumpedShaders = new HashSet<string>();

        private void Tint(Material m, Color tier)
        {
            Shader shader = m.shader;
            if (shader == null) return;
            DumpOnce(shader);
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                string name = shader.GetPropertyName(i);
                int id = shader.GetPropertyNameId(i);
                Color original = m.GetColor(id);
                tinted.Add(new Saved { Material = m, Property = id, Original = original });

                Color c;
                if (name.IndexOf("emiss", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("glow", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("rim", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Glowing parts (the green stripes) shine in the tier color, at their original brightness.
                    float brightness = Mathf.Max(original.r, Mathf.Max(original.g, original.b));
                    c = tier * Mathf.Max(brightness, 0.6f);
                }
                else
                {
                    // Surfaces: mostly the tier color, keeping some of the original shading.
                    float l = 0.2126f * original.r + 0.7152f * original.g + 0.0722f * original.b;
                    c = Color.Lerp(original, tier * Mathf.Lerp(0.55f, 1.1f, l), 0.75f);
                }
                c.a = original.a;
                m.SetColor(id, c);
            }
        }

        // Puts back the original colors (before re-tinting for a new tier).
        private void Untint()
        {
            foreach (Saved s in tinted)
            {
                if (s.Material != null) s.Material.SetColor(s.Property, s.Original);
            }
            tinted.Clear();
        }

        // One log line per shader, listing its color properties, to tune the tint if a part does not change.
        private static void DumpOnce(Shader shader)
        {
            if (!dumpedShaders.Add(shader.name)) return;
            var names = new System.Text.StringBuilder();
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                if (names.Length > 0) names.Append(", ");
                names.Append(shader.GetPropertyName(i));
            }
            Main.Log("tier tint: shader '" + shader.name + "' color properties: " + (names.Length > 0 ? names.ToString() : "none"));
        }

        // A small world-space plate with text, `scale` 1 = about 0.9 m wide for a short label.
        private void Sign(Transform parent, Vector3 position, string text, Color color, TMP_FontAsset font, float scale)
        {
            var go = new GameObject("SRCorralsTierSign", typeof(RectTransform));
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.01f * scale / Mathf.Max(0.0001f, parent.lossyScale.x);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            label.font = font;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.text = text;
            float w = label.GetPreferredValues(text).x + 36f;
            rt.sizeDelta = new Vector2(w, 56f);
            Fill(label.rectTransform);

            var plate = new GameObject("Plate", typeof(RectTransform)).AddComponent<Image>();
            plate.transform.SetParent(go.transform, false);
            plate.transform.SetAsFirstSibling();
            plate.sprite = Rounded();
            plate.type = Image.Type.Sliced;
            plate.color = Plate;
            Fill(plate.rectTransform);

            built.Add(go);
            signs.Add(go.transform);
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static string Roman(int level)
        {
            return level >= 3 ? "III" : (level == 2 ? "II" : "I");
        }

        private static TMP_FontAsset Font()
        {
            HudUI hud = SRSingleton<HudUI>.Instance;
            return hud != null && hud.dayText != null ? hud.dayText.font as TMP_FontAsset : null;
        }

        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            const int size = 64, radius = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }
    }
}
