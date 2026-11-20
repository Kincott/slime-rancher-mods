using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using SRML.Console;
using UnityEngine;
using UnityEngine.Rendering;

namespace SRSeasons
{
    // Diagnostic: "seasondump" lists every material drawn within DumpRadius of the camera, grouped by shader and
    // material, with how many renderers use it, a few object names, and the shader's color and texture
    // properties. Written to SRSeasons-dump.txt next to the mod DLL. Used to find which materials are leaves,
    // grass and ground before making them change with the seasons.
    internal class SeasonDumpCommand : ConsoleCommand
    {
        private const float DumpRadius = 60f;
        private const int SampleNames = 4;

        public override string ID { get { return "seasondump"; } }
        public override string Usage { get { return "seasondump"; } }
        public override string Description { get { return "Write the materials around you to SRSeasons-dump.txt (for the seasons mod)"; } }

        private class Entry
        {
            internal Shader shader;
            internal string rendererType;
            internal int count;
            internal readonly List<string> names = new List<string>();
            internal string material;
            internal Material sample;
        }

        public override bool Execute(string[] args)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Main.Log("seasondump: load a game first");
                return false;
            }
            Vector3 at = cam.transform.position;
            var entries = new Dictionary<string, Entry>();
            int renderers = 0;

            foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if ((r.bounds.ClosestPoint(at) - at).sqrMagnitude > DumpRadius * DumpRadius) continue;
                renderers++;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string shaderName = m.shader != null ? m.shader.name : "(none)";
                    string key = shaderName + "|" + m.name;
                    Entry e;
                    if (!entries.TryGetValue(key, out e))
                    {
                        e = new Entry { shader = m.shader, material = m.name, rendererType = r.GetType().Name, sample = m };
                        entries[key] = e;
                    }
                    e.count++;
                    string name = PathOf(r.transform);
                    if (e.names.Count < SampleNames && !e.names.Contains(name)) e.names.Add(name);
                }
            }

            var sorted = new List<Entry>(entries.Values);
            sorted.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.shader != null ? a.shader.name : "", b.shader != null ? b.shader.name : "");
                return c != 0 ? c : b.count.CompareTo(a.count);
            });

            var sb = new StringBuilder();
            sb.AppendLine("SR Seasons material dump, radius " + DumpRadius + " m, " + renderers + " renderers, " + sorted.Count + " materials");
            sb.AppendLine();
            string dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "SRSeasons-dump");
            Directory.CreateDirectory(dir);
            var exportedPerShader = new Dictionary<string, int>();
            int images = 0;
            string lastShader = null;
            foreach (Entry e in sorted)
            {
                string shaderName = e.shader != null ? e.shader.name : "(none)";
                if (shaderName != lastShader)
                {
                    lastShader = shaderName;
                    sb.AppendLine("== SHADER " + shaderName + "   props: " + Props(e.shader));
                }
                sb.AppendLine("  " + e.material + "  x" + e.count + "  (" + e.rendererType + ")  e.g. " + string.Join(" ; ", e.names.ToArray()));
                if (e.shader == null || e.sample == null) continue;
                sb.AppendLine("      colors: " + Colors(e.sample));

                // Pictures of the textures of the 2 most used materials of each world shader.
                if (!shaderName.StartsWith("SR/Paintlight")) continue;
                int done;
                exportedPerShader.TryGetValue(shaderName, out done);
                if (done >= 2) continue;
                exportedPerShader[shaderName] = done + 1;
                for (int i = 0; i < e.shader.GetPropertyCount(); i++)
                {
                    if (e.shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                    string prop = e.shader.GetPropertyName(i);
                    Texture tex = e.sample.GetTexture(prop);
                    if (tex == null) continue;
                    try
                    {
                        Texture2D copy = TextureTools.Readable(tex, 256);
                        string imageFile = Safe(e.material) + "_" + prop + ".bmp";
                        TextureTools.WriteBmp(copy, Path.Combine(dir, imageFile));
                        UnityEngine.Object.Destroy(copy);
                        Vector2 scale = e.sample.GetTextureScale(prop);
                        sb.AppendLine("      image " + imageFile + "  (" + tex.width + "x" + tex.height + ", tiling " + scale.x + "," + scale.y + ")");
                        images++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("      image " + prop + " failed: " + ex.Message);
                    }
                }
            }

            string file = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "SRSeasons-dump.txt");
            File.WriteAllText(file, sb.ToString());
            Main.Log("seasondump: " + sorted.Count + " materials and " + images + " images written to " + file);
            return true;
        }

        private static string Props(Shader shader)
        {
            if (shader == null) return "";
            var parts = new List<string>();
            int n = shader.GetPropertyCount();
            for (int i = 0; i < n; i++)
            {
                ShaderPropertyType t = shader.GetPropertyType(i);
                if (t == ShaderPropertyType.Color || t == ShaderPropertyType.Texture) parts.Add(shader.GetPropertyName(i) + ":" + t);
            }
            return string.Join(", ", parts.ToArray());
        }

        private static string Colors(Material m)
        {
            var parts = new List<string>();
            Shader shader = m.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Color) continue;
                string p = shader.GetPropertyName(i);
                Color c = m.GetColor(p);
                parts.Add(p + "=(" + c.r.ToString("0.00") + "," + c.g.ToString("0.00") + "," + c.b.ToString("0.00") + "," + c.a.ToString("0.00") + ")");
            }
            return string.Join(" ", parts.ToArray());
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++)
            {
                t = t.parent;
                p = t.name + "/" + p;
            }
            return p;
        }
    }
}
