using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRFusion
{
    // One floating sign above a corral, facing the camera, in the dark glass style of the other mods.
    // Show() only rebuilds when the text or color changes.
    internal class WorldSign
    {
        private const float Height = 5.5f;
        private static readonly Color Plate = new Color(0.055f, 0.063f, 0.082f, 0.85f);
        private static Sprite rounded;

        private GameObject go;
        private TextMeshProUGUI label;
        private string shownText;
        private Color shownColor;

        internal void Show(Transform corral, string text, Color color)
        {
            if (go != null && text == shownText && color == shownColor) return;
            TMP_FontAsset font = Font();
            if (font == null) return; // the HUD (and its font) is not there yet
            if (go == null) Build(corral, font);
            shownText = text;
            shownColor = color;
            label.text = text;
            label.color = color;
            ((RectTransform)go.transform).sizeDelta = new Vector2(label.GetPreferredValues(text).x + 40f, 60f);
        }

        internal void Clear()
        {
            if (go != null) Object.Destroy(go);
            go = null;
        }

        internal void FaceCamera()
        {
            Camera cam = Camera.main;
            if (go == null || cam == null) return;
            Vector3 away = go.transform.position - cam.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(away);
        }

        private void Build(Transform corral, TMP_FontAsset font)
        {
            go = new GameObject("SRFusionSign", typeof(RectTransform));
            go.transform.SetParent(corral, true);
            go.transform.position = corral.position + Vector3.up * Height;
            go.transform.localScale = Vector3.one * 0.016f / Mathf.Max(0.0001f, corral.lossyScale.x);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var plate = new GameObject("Plate", typeof(RectTransform)).AddComponent<Image>();
            plate.transform.SetParent(go.transform, false);
            plate.sprite = Rounded();
            plate.type = Image.Type.Sliced;
            plate.color = Plate;
            Fill(plate.rectTransform);

            label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            label.font = font;
            label.fontSize = 34f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            Fill(label.rectTransform);
        }

        private static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
