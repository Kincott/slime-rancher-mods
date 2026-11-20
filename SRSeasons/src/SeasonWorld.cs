using System;
using System.Collections.Generic;
using UnityEngine;

namespace SRSeasons
{
    // Seasonal look of the world's materials, applied when the season changes (checked every second):
    //   - ground ("SR/Paintlight/Triplanar Topper" shaders): their top-surface layer (_Topper_MainTex) becomes
    //     snow in winter, and its top color (_RampTop) goes white-blue; autumn warms that color slightly;
    //   - grass ("GrassUV"): tinted golden in summer, brown-orange in autumn, frosted in winter;
    //   - leaves ("SR/Paintlight/Leaves", no color property): the texture is swapped for a recolored copy,
    //     red/orange in autumn, frosted in winter (bushes); tree crowns are hidden in winter (bare trees).
    // Shared materials are edited in place (no per-renderer copies), from cached originals, so switching
    // season, or leaving the game scene, restores everything exactly.
    internal class SeasonWorld : MonoBehaviour
    {
        private const string TopperPrefix = "SR/Paintlight/Triplanar Topper";
        private const string GrassShader = "SR/Paintlight/GrassUV";
        private const string LeavesShader = "SR/Paintlight/Leaves";
        private const int LeafTexSize = 512;

        private static readonly Color SnowTop = new Color(0.92f, 0.95f, 1f, 1f);
        private static readonly Color Frost = new Color(0.88f, 0.92f, 1f, 1f);

        private class Saved
        {
            internal readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
            internal readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        }

        private readonly Dictionary<Material, Saved> saved = new Dictionary<Material, Saved>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly Dictionary<string, Texture2D> recolored = new Dictionary<string, Texture2D>();
        private Texture2D snow;
        private SeasonId? applied;
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.TimeDirector == null) return;
            SeasonId now = Season.Of(scene.TimeDirector.CurrDay());
            if (applied.HasValue && applied.Value == now) return;
            float t0 = Time.realtimeSinceStartup;
            try
            {
                Restore();
                Apply(now);
                applied = now;
                Main.Log("world look: " + Season.Name(now) + " (" + saved.Count + " materials, " + hidden.Count
                    + " tree crowns hidden, " + Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f) + " ms)");
            }
            catch (Exception e)
            {
                applied = now; // do not retry every second
                Main.Log("world look failed: " + e);
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Apply(SeasonId s)
        {
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null) continue;
                string shader = m.shader.name;
                if (shader.StartsWith(TopperPrefix)) Ground(m, s);
                else if (shader == GrassShader) Grass(m, s);
                else if (shader == LeavesShader) Leaves(m, s);
            }
            if (s == SeasonId.Winter) HideTreeCrowns();
        }

        private void Ground(Material m, SeasonId s)
        {
            if (s == SeasonId.Winter)
            {
                if (snow == null) snow = TextureTools.Snow();
                SetTex(m, "_Topper_MainTex", snow);
                Tint(m, "_RampTop", SnowTop, 0.85f);
            }
            else if (s == SeasonId.Autumn)
            {
                Tint(m, "_RampTop", new Color(1f, 0.72f, 0.42f, 1f), 0.25f);
            }
        }

        private void Grass(Material m, SeasonId s)
        {
            Color target;
            float amount;
            switch (s)
            {
                case SeasonId.Summer: target = new Color(1f, 0.9f, 0.45f, 1f); amount = 0.15f; break;
                case SeasonId.Autumn: target = new Color(0.85f, 0.55f, 0.28f, 1f); amount = 0.45f; break;
                case SeasonId.Winter: target = Frost; amount = 0.6f; break;
                default: return;
            }
            foreach (string p in new[] { "_Color", "_RampTop", "_RampUpper" }) Tint(m, p, target, amount);
        }

        private void Leaves(Material m, SeasonId s)
        {
            if (s != SeasonId.Autumn && s != SeasonId.Winter) return;
            foreach (string p in new[] { "_PrimaryTex", "_CoreTex" })
            {
                if (!m.HasProperty(p)) continue;
                Texture src = m.GetTexture(p);
                if (src == null) continue;
                string key = s + "|" + src.GetInstanceID();
                Texture2D tex;
                if (!recolored.TryGetValue(key, out tex))
                {
                    tex = s == SeasonId.Autumn ? TextureTools.Recolor(src, LeafTexSize, AutumnLeaf) : TextureTools.Recolor(src, LeafTexSize, FrostLeaf);
                    recolored[key] = tex;
                }
                SetTex(m, p, tex);
            }
        }

        // Green leaves turn red to orange to gold, in large patches so each tree gets a mix.
        private static Color AutumnLeaf(Color c, int x, int y)
        {
            float h, sat, v;
            Color.RGBToHSV(c, out h, out sat, out v);
            float patch = Mathf.PerlinNoise(x / 48f, y / 48f);
            float hue = Mathf.Lerp(0.01f, 0.12f, patch);
            Color o = Color.HSVToRGB(hue, Mathf.Clamp01(sat * 1.1f + 0.1f), Mathf.Clamp01(v * 1.08f));
            o.a = c.a;
            return o;
        }

        // Leaves under frost (bushes stay in winter): pale, slightly blue.
        private static Color FrostLeaf(Color c, int x, int y)
        {
            float v = (c.r + c.g + c.b) / 3f;
            Color o = Color.Lerp(new Color(v, v, v, 1f), Frost, 0.6f);
            o.a = c.a;
            return o;
        }

        // Tree crowns ("treeLeaves..." under a "tree..." object) are hidden; bushes keep their (frosted) leaves.
        // Includes inactive cells, which the game turns on as the player moves; prefab assets are skipped.
        private void HideTreeCrowns()
        {
            foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.scene.IsValid()) continue;
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != LeavesShader) continue;
                if (!IsTreeCrown(r.transform)) continue;
                r.enabled = false;
                hidden.Add(r);
            }
        }

        private static bool IsTreeCrown(Transform t)
        {
            if (t.name.IndexOf("treeLeaves", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            Transform p = t.parent;
            return p != null && p.name.StartsWith("tree", StringComparison.OrdinalIgnoreCase);
        }

        // ---- edits from cached originals ----------------------------------------------------------------

        private Saved Save(Material m)
        {
            Saved s;
            if (!saved.TryGetValue(m, out s)) saved[m] = s = new Saved();
            return s;
        }

        private void Tint(Material m, string prop, Color target, float amount)
        {
            if (!m.HasProperty(prop)) return;
            Saved s = Save(m);
            Color original;
            if (!s.colors.TryGetValue(prop, out original)) s.colors[prop] = original = m.GetColor(prop);
            Color c = Color.Lerp(original, target, amount);
            c.a = original.a;
            m.SetColor(prop, c);
        }

        private void SetTex(Material m, string prop, Texture tex)
        {
            if (tex == null || !m.HasProperty(prop)) return;
            Saved s = Save(m);
            if (!s.textures.ContainsKey(prop)) s.textures[prop] = m.GetTexture(prop);
            m.SetTexture(prop, tex);
        }

        private void Restore()
        {
            foreach (KeyValuePair<Material, Saved> e in saved)
            {
                if (e.Key == null) continue;
                foreach (KeyValuePair<string, Color> c in e.Value.colors) e.Key.SetColor(c.Key, c.Value);
                foreach (KeyValuePair<string, Texture> t in e.Value.textures) e.Key.SetTexture(t.Key, t.Value);
            }
            saved.Clear();
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            applied = null;
        }
    }
}
