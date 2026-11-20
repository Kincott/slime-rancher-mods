using System;
using System.IO;
using UnityEngine;

namespace SRSeasons
{
    // Texture helpers. The game's textures are not CPU-readable, so a copy is read back from the GPU: blit into a
    // temporary RenderTexture, then ReadPixels into a new readable Texture2D (downscaled to maxSize).
    internal static class TextureTools
    {
        internal static Texture2D Readable(Texture src, int maxSize)
        {
            int w = src.width, h = src.height;
            while (w > maxSize || h > maxSize)
            {
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(true);
                return tex;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // Returns a recolored copy of src (same wrap and filter settings), or null if it could not be read.
        internal static Texture2D Recolor(Texture src, int maxSize, Func<Color, int, int, Color> map)
        {
            if (src == null) return null;
            Texture2D tex = Readable(src, maxSize);
            Color[] px = tex.GetPixels();
            int w = tex.width;
            for (int i = 0; i < px.Length; i++) px[i] = map(px[i], i % w, i / w);
            tex.SetPixels(px);
            tex.Apply(true);
            tex.wrapMode = src.wrapMode;
            tex.filterMode = src.filterMode;
            tex.anisoLevel = src.anisoLevel;
            tex.name = src.name + " (season)";
            return tex;
        }

        // Tileable snow: off-white with soft blue-gray value noise and a few brighter grains.
        internal static Texture2D Snow()
        {
            const int size = 256, cells = 16;
            var rnd = new System.Random(1234);
            var grid = new float[cells, cells];
            for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) grid[x, y] = (float)rnd.NextDouble();

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[size * size];
            Color light = new Color(0.96f, 0.97f, 1f, 1f), shade = new Color(0.80f, 0.85f, 0.93f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0.65f * ValueNoise(grid, cells, x * cells / (float)size, y * cells / (float)size)
                            + 0.35f * ValueNoise(grid, cells, x * cells * 4f / size, y * cells * 4f / size);
                    Color c = Color.Lerp(shade, light, 0.35f + 0.65f * n);
                    if (rnd.NextDouble() < 0.004) c = Color.white;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            tex.name = "SRSeasons snow";
            return tex;
        }

        private static float ValueNoise(float[,] grid, int cells, float fx, float fy)
        {
            int x0 = (int)Mathf.Floor(fx), y0 = (int)Mathf.Floor(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            Func<int, int> wrap = v => ((v % cells) + cells) % cells;
            float a = grid[wrap(x0), wrap(y0)], b = grid[wrap(x0 + 1), wrap(y0)];
            float c = grid[wrap(x0), wrap(y0 + 1)], d = grid[wrap(x0 + 1), wrap(y0 + 1)];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        // 32-bit BMP (no image module needed); used by the dump.
        internal static void WriteBmp(Texture2D tex, string path)
        {
            int w = tex.width, h = tex.height;
            Color32[] px = tex.GetPixels32();
            using (var bw = new BinaryWriter(File.Create(path)))
            {
                int dataSize = w * h * 4;
                bw.Write((byte)'B'); bw.Write((byte)'M');
                bw.Write(54 + dataSize); bw.Write(0); bw.Write(54);
                bw.Write(40); bw.Write(w); bw.Write(h); bw.Write((short)1); bw.Write((short)32);
                bw.Write(0); bw.Write(dataSize); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
                foreach (Color32 c in px) { bw.Write(c.b); bw.Write(c.g); bw.Write(c.r); bw.Write(c.a); }
            }
        }
    }
}
