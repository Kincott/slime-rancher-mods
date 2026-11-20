using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SRBoost
{
    // SRBoost.ini, created with defaults next to SRBoost.dll on first launch. Simple "key = value" lines,
    // '#' starts a comment. A missing or invalid value falls back to its default.
    internal class BoostConfig
    {
        // 0 = no in-game cap (cap with the GPU driver instead, which gives lower input lag). The game caps at 120.
        public int TargetFps = 0;

        // Only applied on top of the game's highest level of each setting, never when a lower level is chosen.
        public float ShadowDistance = 250f;            // game "High" shadows: 150
        public ShadowResolution ShadowResolution = ShadowResolution.VeryHigh;
        public float LodBias = 4f;                     // game "High" model detail: 2
        public int PixelLights = 8;                    // game "Highest" lighting: 4

        // Logs average FPS, 1% low and the worst frame every PerfLogSeconds (0 = off).
        public int PerfLogSeconds = 30;

        // One-shot: on the next game load, set the in-game options to Very High with V-Sync off, then turn
        // itself back to false. The game saves the options in the profile itself.
        public bool ApplyBestGameSettings = false;

        // Feeders also drop food whenever slimes in their corral are hungry, checked every N seconds.
        public bool SmartFeeder = true;
        public int FeederCheckSeconds = 3;

        private const string FileName = "SRBoost.ini";
        private string path;

        private const string Template =
@"# SRBoost settings. Restart the game after editing.

# Frame cap. 0 = no in-game cap: set the cap in the NVIDIA Control Panel instead (lower input lag).
# The base game caps at 120.
TargetFps = {0}

# Applied only when the matching in-game option is at its highest level.
ShadowDistance = {1}
# Low, Medium, High or VeryHigh
ShadowResolution = {2}
LodBias = {3}
PixelLights = {4}

# Write average FPS, 1% low and worst frame to the SRML log every N seconds (0 = off).
PerfLogSeconds = {5}

# true = on the next game load, set the in-game options to Very High with V-Sync off (once, then back to false).
ApplyBestGameSettings = {6}

# Feeders also drop food whenever slimes in their corral are hungry (on top of their normal cycle).
# Only the corral owner's setting matters in multiplayer: keep it the same on both PCs.
SmartFeeder = {7}
FeederCheckSeconds = {8}
";

        internal static BoostConfig LoadOrCreate()
        {
            var config = new BoostConfig();
            config.path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileName);
            try
            {
                if (!File.Exists(config.path))
                {
                    File.WriteAllText(config.path, config.ToIni());
                    return config;
                }
                config.Parse(File.ReadAllLines(config.path));
            }
            catch (Exception e)
            {
                Main.Log("could not read " + FileName + ", using defaults: " + e.Message);
            }
            return config;
        }

        // Rewrites the file from the current values (comments are regenerated from the template).
        internal void Save()
        {
            try
            {
                File.WriteAllText(path, ToIni());
            }
            catch (Exception e)
            {
                Main.Log("could not write " + FileName + ": " + e.Message);
            }
        }

        private void Parse(IEnumerable<string> lines)
        {
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "TargetFps": TargetFps = ParseInt(value, TargetFps); break;
                    case "ShadowDistance": ShadowDistance = ParseFloat(value, ShadowDistance); break;
                    case "ShadowResolution": ShadowResolution = ParseShadowResolution(value, ShadowResolution); break;
                    case "LodBias": LodBias = ParseFloat(value, LodBias); break;
                    case "PixelLights": PixelLights = ParseInt(value, PixelLights); break;
                    case "PerfLogSeconds": PerfLogSeconds = ParseInt(value, PerfLogSeconds); break;
                    case "ApplyBestGameSettings": ApplyBestGameSettings = ParseBool(value, ApplyBestGameSettings); break;
                    case "SmartFeeder": SmartFeeder = ParseBool(value, SmartFeeder); break;
                    case "FeederCheckSeconds": FeederCheckSeconds = ParseInt(value, FeederCheckSeconds); break;
                }
            }
        }

        private string ToIni()
        {
            return string.Format(CultureInfo.InvariantCulture, Template,
                TargetFps, ShadowDistance, ShadowResolution, LodBias, PixelLights, PerfLogSeconds,
                ApplyBestGameSettings ? "true" : "false", SmartFeeder ? "true" : "false", FeederCheckSeconds);
        }

        internal string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "TargetFps={0} ShadowDistance={1} ShadowResolution={2} LodBias={3} PixelLights={4} PerfLogSeconds={5} ApplyBestGameSettings={6} SmartFeeder={7} FeederCheckSeconds={8}",
                TargetFps, ShadowDistance, ShadowResolution, LodBias, PixelLights, PerfLogSeconds, ApplyBestGameSettings,
                SmartFeeder, FeederCheckSeconds);
        }

        private static bool ParseBool(string s, bool fallback)
        {
            bool v;
            return bool.TryParse(s, out v) ? v : fallback;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static float ParseFloat(string s, float fallback)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static ShadowResolution ParseShadowResolution(string s, ShadowResolution fallback)
        {
            try
            {
                return (ShadowResolution)Enum.Parse(typeof(ShadowResolution), s, true);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }
    }
}
