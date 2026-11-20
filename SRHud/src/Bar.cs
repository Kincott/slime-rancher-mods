using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Thin rounded progress bar: a faint track and a colored fill whose width follows the value.
    internal class Bar
    {
        private readonly RectTransform fill;
        private readonly Image fillImage;
        private float shown = -1f;

        internal readonly RectTransform Root;

        internal Bar(Transform parent, Color color)
        {
            Image track = Theme.Box("Track", parent, Theme.Track, 6f);
            Root = track.rectTransform;
            fillImage = Theme.Box("Fill", Root, color, 6f);
            fill = fillImage.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        internal void Set(float value, float max)
        {
            float f = max > 0f ? Mathf.Clamp01(value / max) : 0f;
            if (Mathf.Abs(f - shown) < 0.001f) return;
            shown = f;
            fill.anchorMax = new Vector2(f, 1f);
            fillImage.enabled = f > 0f;
        }

        internal Color Color
        {
            set { fillImage.color = value; }
        }
    }
}
