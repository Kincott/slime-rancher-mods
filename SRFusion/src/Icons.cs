using UnityEngine;

namespace SRFusion
{
    // Vacpack and shop icons drawn in code (the game has none for the new items): a vertical three-color
    // gradient inside a shape, with a soft white outline and anti-aliased edges.
    internal static class Icons
    {
        private const int Size = 128;

        internal static Sprite Circle(Color top, Color middle, Color bottom)
        {
            return Draw(top, middle, bottom, (x, y) =>
            {
                // A slime-like blob: a circle slightly flattened at the bottom.
                float dx = x - 0.5f, dy = (y - 0.46f) * (y < 0.46f ? 1.25f : 1f);
                return 0.40f - Mathf.Sqrt(dx * dx + dy * dy);
            });
        }

        internal static Sprite Diamond(Color top, Color middle, Color bottom)
        {
            return Draw(top, middle, bottom, (x, y) => 0.40f - (Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f)));
        }

        // A diamond with a dark core and a few twinkling stars inside: the cosmic plort.
        internal static Sprite CosmicDiamond(Color top, Color middle, Color bottom)
        {
            return Draw(top, middle, bottom, (x, y) => 0.40f - (Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f)), true);
        }

        // inside(x, y) > 0 inside the shape, < 0 outside, in units of the icon size.
        private static Sprite Draw(Color top, Color middle, Color bottom, System.Func<float, float, float> inside, bool stars = false)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[Size * Size];
            float px1 = 1f / Size;
            for (int j = 0; j < Size; j++)
            {
                float v = (j + 0.5f) / Size; // 0 bottom, 1 top
                Color fill = v > 0.5f ? Color.Lerp(middle, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, middle, v * 2f);
                for (int i = 0; i < Size; i++)
                {
                    float d = inside((i + 0.5f) / Size, v);
                    float alpha = Mathf.Clamp01(d / px1 + 0.5f);
                    // Outline band just inside the edge.
                    float outline = Mathf.Clamp01(1f - Mathf.Abs(d - 0.025f) / 0.02f);
                    Color c = Color.Lerp(fill, Color.white, outline * 0.85f);
                    // Highlight toward the upper left.
                    float hx = (i + 0.5f) / Size - 0.38f, hy = v - 0.68f;
                    float shine = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.12f) * 0.6f;
                    c = Color.Lerp(c, Color.white, shine);
                    if (stars && d > 0.04f) c = Color.Lerp(c, Color.white, Star((i + 0.5f) / Size, v));
                    c.a = alpha;
                    px[j * Size + i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        // A few fixed 4-point stars: brightness 0..1 at (x, y).
        private static readonly Vector3[] StarSpots =
        {
            new Vector3(0.44f, 0.60f, 0.06f), new Vector3(0.58f, 0.44f, 0.045f),
            new Vector3(0.47f, 0.36f, 0.03f), new Vector3(0.62f, 0.60f, 0.03f), new Vector3(0.36f, 0.48f, 0.025f)
        };

        private static float Star(float x, float y)
        {
            float best = 0f;
            foreach (Vector3 s in StarSpots)
            {
                float dx = Mathf.Abs(x - s.x), dy = Mathf.Abs(y - s.y);
                float ray = Mathf.Clamp01(1f - dx / s.z) * Mathf.Clamp01(1f - dy / (s.z * 0.18f))
                          + Mathf.Clamp01(1f - dy / s.z) * Mathf.Clamp01(1f - dx / (s.z * 0.18f));
                best = Mathf.Max(best, Mathf.Clamp01(ray));
            }
            return best;
        }
    }
}
