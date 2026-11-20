using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SRHud
{
    // SRHud.ini next to SRHud.dll, created with defaults on first launch. "key = value", '#' for comments.
    internal class HudConfig
    {
        public bool Enabled = true;      // false = original HUD
        public float Scale = 1f;         // overall size, 1 = designed for 1080p
        public float Opacity = 1f;       // whole HUD opacity, 0.2 to 1
        public bool ShowFps = true;
        public bool ThemeMenus = true;   // dark glass theme on every menu

        private const string FileName = "SRHud.ini";

        private const string Template =
@"# SR HUD settings. Restart the game after editing.

# false = keep the original HUD.
Enabled = {0}
# Overall size (1 = designed for 1080p). Try 1.25 on 1440p, 1.5 on 4K if it feels small.
Scale = {1}
# Whole HUD opacity, 0.2 to 1.
Opacity = {2}
# Small FPS counter in the top-right corner.
ShowFps = {3}
# Dark glass theme on every menu (pause, options, shops, Slimepedia...).
ThemeMenus = {4}
";

        internal static HudConfig LoadOrCreate()
        {
            var config = new HudConfig();
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, string.Format(CultureInfo.InvariantCulture, Template,
                        "true", config.Scale, config.Opacity, "true", "true"));
                    return config;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#") || eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    bool b;
                    float f;
                    switch (key)
                    {
                        case "Enabled": if (bool.TryParse(value, out b)) config.Enabled = b; break;
                        case "ShowFps": if (bool.TryParse(value, out b)) config.ShowFps = b; break;
                        case "ThemeMenus": if (bool.TryParse(value, out b)) config.ThemeMenus = b; break;
                        case "Scale":
                            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) config.Scale = Clamp(f, 0.5f, 3f);
                            break;
                        case "Opacity":
                            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) config.Opacity = Clamp(f, 0.2f, 1f);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Main.Log("could not read " + FileName + ", using defaults: " + e.Message);
            }
            return config;
        }

        internal string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture, "Enabled={0} Scale={1} Opacity={2} ShowFps={3} ThemeMenus={4}",
                Enabled, Scale, Opacity, ShowFps, ThemeMenus);
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : (v > max ? max : v);
        }
    }
}
