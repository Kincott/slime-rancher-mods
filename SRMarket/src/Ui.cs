using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRMarket
{
    // Same dark glass look as SR HUD (this mod does not depend on it): palette, rounded shape, small factories.
    internal static class Ui
    {
        internal static readonly Color Panel = new Color(0.055f, 0.063f, 0.082f, 0.88f);
        internal static readonly Color Border = new Color(1f, 1f, 1f, 0.08f);
        internal static readonly Color Row = new Color(1f, 1f, 1f, 0.035f);
        internal static readonly Color Text = new Color(1f, 1f, 1f, 0.95f);
        internal static readonly Color Dim = new Color(1f, 1f, 1f, 0.55f);
        internal static readonly Color Gold = new Color(1f, 0.80f, 0.36f, 1f);
        internal static readonly Color Up = new Color(0.36f, 0.85f, 0.50f, 1f);
        internal static readonly Color Down = new Color(1f, 0.45f, 0.45f, 1f);
        internal static readonly Color Line = new Color(0.30f, 0.79f, 0.94f, 1f);
        internal static readonly Color Wait = new Color(1f, 0.70f, 0.30f, 1f);

        internal static TMP_FontAsset Font;
        private static Sprite rounded;

        internal static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = BuildRounded(64, 16);
                return rounded;
            }
        }

        private static Sprite BuildRounded(int size, int radius)
        {
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // Anchor + pivot at the same point; offset from it; fixed size.
        internal static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        internal static Image Box(string name, Transform parent, Color color, float tightness)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = tightness;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        internal static Image Icon(string name, Transform parent)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        internal static TextMeshProUGUI Label(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        internal static Sparkline Chart(string name, Transform parent, Color color, float thickness)
        {
            var s = Rect(name, parent).gameObject.AddComponent<Sparkline>();
            s.color = color;
            s.Thickness = thickness;
            s.raycastTarget = false;
            return s;
        }
    }
}
