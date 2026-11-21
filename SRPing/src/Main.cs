using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using SRML;
using UnityEngine;

namespace SRPing
{
    // Ping a spot or an object for the other player: press the ping key (middle mouse by default) while aiming.
    // Both players see a marker (beam, name, distance, an arrow at the screen edge when off screen) and hear a
    // chime, for PingSeconds. Pings travel over SRMP's chat channel as hidden messages (PingNet), so nothing in
    // SRMP's own networking changes. Settings in SRPing.ini.
    public class Main : ModEntryPoint
    {
        private static Main instance;
        internal static KeyCode Key = KeyCode.Mouse2;
        internal static float PingSeconds = 8f;
        internal static float Volume = 0.4f;

        private GameObject sceneObject;

        public override void PreLoad()
        {
            instance = this;
            LoadConfig();
            PingNet.Apply(HarmonyInstance);
        }

        public override void Load()
        {
            Log("loaded: Key=" + Key + " PingSeconds=" + PingSeconds + " Volume=" + Volume);
        }

        public override void Update()
        {
            HudUI hud = SRSingleton<HudUI>.Instance;
            if (hud == null || sceneObject != null) return;
            sceneObject = new GameObject("SRPing");
            sceneObject.AddComponent<PingView>().Init(hud);
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }

        private const string FileName = "SRPing.ini";

        private static void LoadConfig()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path,
                        "# SR Ping settings. Restart the game after editing.\n\n" +
                        "# Ping key, a Unity KeyCode name: Mouse2 = middle mouse (also the game's Pulse Wave), Mouse3/Mouse4 =\n" +
                        "# side buttons, or a free key like V, B, Z, X, C. Avoid G, T, F, Q, R, H, E, M: the game uses them.\nKey = Mouse2\n" +
                        "# How long a ping stays, in seconds.\nPingSeconds = 8\n" +
                        "# Ping sound volume, 0 to 1.\nVolume = 0.4\n");
                    return;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#") || eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string text = line.Substring(eq + 1).Trim();
                    float number;
                    if (key == "Key")
                    {
                        try { Key = (KeyCode)Enum.Parse(typeof(KeyCode), text, true); }
                        catch (Exception) { Log("unknown key '" + text + "', using Mouse2"); }
                    }
                    else if (key == "PingSeconds" && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                        PingSeconds = Mathf.Clamp(number, 1f, 120f);
                    else if (key == "Volume" && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                        Volume = Mathf.Clamp01(number);
                }
            }
            catch (Exception e)
            {
                Log("could not read " + FileName + ", using defaults: " + e.Message);
            }
        }
    }
}
