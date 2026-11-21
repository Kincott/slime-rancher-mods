using System;
using UnityEngine;

namespace SRPing
{
    // Procedural sprites (anti-aliased, white, tinted by the Image color), built once.
    internal static class Sprites
    {
        private static Sprite diamond, arrow, rounded;
        private static Texture2D beam;

        internal static Sprite Diamond()
        {
            if (diamond == null) diamond = Make(64, (x, y) => 0.5f - (Mathf.Abs(x) + Mathf.Abs(y) - 0.46f) * 64f, Vector4.zero);
            return diamond;
        }

        // Triangle pointing up (rotated toward the target).
        internal static Sprite Arrow()
        {
            if (arrow == null)
            {
                arrow = Make(64, (x, y) =>
                {
                    // apex at y = 0.42, base at y = -0.32 with half-width 0.46: inside when below both slanted
                    // edges and above the base (signed distances, roughly, in texture units)
                    float sides = (0.42f - Mathf.Abs(x) * 1.6f - y) * 0.53f;
                    float bottom = y + 0.32f;
                    return 0.5f + Mathf.Min(sides, bottom) * 64f;
                }, Vector4.zero);
            }
            return arrow;
        }

        internal static Sprite Rounded()
        {
            if (rounded == null)
            {
                const float r = 0.25f; // corner radius as a fraction of the size
                rounded = Make(64, (x, y) =>
                {
                    float cx = Mathf.Clamp(x, -0.5f + r, 0.5f - r), cy = Mathf.Clamp(y, -0.5f + r, 0.5f - r);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
                    return 0.5f - d * 64f;
                }, new Vector4(16, 16, 16, 16));
            }
            return rounded;
        }

        // Soft vertical light: bright in the middle across the width.
        internal static Texture2D BeamTexture()
        {
            if (beam != null) return beam;
            const int w = 32, h = 4;
            beam = new Texture2D(w, h, TextureFormat.RGBA32, false);
            beam.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(1f - u * u) * 255f);
                for (int y = 0; y < h; y++) px[y * w + x] = new Color32(255, 255, 255, a);
            }
            beam.SetPixels32(px);
            beam.Apply(false, true);
            return beam;
        }

        // coverage(x, y) with x, y in [-0.5, 0.5]: > 1 inside, < 0 outside, between = anti-aliased edge.
        private static Sprite Make(int size, Func<float, float, float> coverage, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(coverage((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}
