using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Visual language of the HUD: one palette, one rounded shape, one font, and small factory helpers so every
    // element is built the same way. Sizes are in 1080p reference pixels (the canvas scales them).
    internal static class Theme
    {
        // Dark glass panels
        internal static readonly Color Panel = new Color(0.055f, 0.063f, 0.082f, 0.62f);
        internal static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.08f);
        internal static readonly Color SlotIdle = new Color(0.055f, 0.063f, 0.082f, 0.55f);
        internal static readonly Color SlotSelected = new Color(0.16f, 0.18f, 0.22f, 0.78f);
        internal static readonly Color SelectedRing = new Color(1f, 1f, 1f, 0.85f);
        internal static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);

        // Text
        internal static readonly Color TextPrimary = new Color(1f, 1f, 1f, 0.95f);
        internal static readonly Color TextSecondary = new Color(1f, 1f, 1f, 0.58f);

        // Vitals (softer than the vanilla saturated red/blue)
        internal static readonly Color Health = new Color(1f, 0.38f, 0.45f, 1f);
        internal static readonly Color Energy = new Color(0.30f, 0.79f, 0.94f, 1f);
        internal static readonly Color Rad = new Color(0.72f, 0.94f, 0.30f, 1f);
        internal static readonly Color Gold = new Color(1f, 0.80f, 0.36f, 1f);

        // Menus: more opaque than the HUD, since they hold dense content.
        internal static readonly Color MenuBase = new Color(0.075f, 0.082f, 0.102f, 1f);
        // Controls sit clearly above the panels (~0.10-0.13) they are drawn on: in game the first, darker values
        // made buttons, dropdowns and checkboxes disappear into their panel.
        internal static readonly Color ButtonIdle = new Color(0.22f, 0.25f, 0.31f, 1f);
        internal static readonly Color ButtonHover = new Color(0.31f, 0.35f, 0.43f, 1f);
        internal static readonly Color ButtonPressed = new Color(0.15f, 0.17f, 0.21f, 1f);
        internal static readonly Color ButtonDisabled = new Color(0.22f, 0.25f, 0.31f, 0.4f);
        // Selected tab / list row and checkmarks: the accent, so the current choice is obvious.
        internal static readonly Color Selected = new Color(0.30f, 0.79f, 0.94f, 0.45f);
        internal static readonly Color Mark = new Color(0.30f, 0.79f, 0.94f, 1f);
        internal static readonly Color TextOnDark = new Color(0.93f, 0.94f, 0.96f, 1f);

        internal const float Radius = 12f;

        internal static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        // Any background color to the dark palette, keeping a hint of its hue so menus stay distinguishable.
        internal static Color DarkSurface(Color c)
        {
            return new Color(MenuBase.r + c.r * 0.12f, MenuBase.g + c.g * 0.12f, MenuBase.b + c.b * 0.12f,
                Mathf.Max(c.a, 0.9f));
        }

        // Text meant for a light background, made readable on a dark one; light text is left alone.
        internal static Color LightText(Color c)
        {
            if (Luminance(c) >= 0.55f) return c;
            float saturation = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            Color light = saturation < 0.15f ? TextOnDark : Color.Lerp(c, Color.white, 0.55f);
            light.a = c.a;
            return light;
        }

        internal static TMP_FontAsset Font;
        private static Sprite rounded;

        // A white rounded rectangle, 9-sliced, generated once: tint it with Image.color.
        internal static Sprite Rounded
        {
            get
            {
                if (rounded == null)
                {
                    rounded = BuildRounded(64, 16);
                }
                return rounded;
            }
        }

        private static Sprite BuildRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the rounded corner, anti-aliased over one pixel.
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - radius;
                    byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // Anchors and pivot at the same normalized point, positioned by offset, fixed size.
        internal static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        internal static RectTransform Fill(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        internal static Image Box(string name, Transform parent, Color color, float pixelsPerUnitMultiplier)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier; // higher = tighter corners
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        internal static Image Icon(string name, Transform parent)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        internal static TextMeshProUGUI Text(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
            {
                t.font = Font;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
