using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SRSeasons
{
    // The season's color mood. AmbianceDirector.Update recomputes, every frame, the ambient light, the fog and
    // the sky colors from the zone and the time of day; right after it, they are multiplied by the season tint.
    // Since the game starts again from its own values each frame, the tint never stacks.
    //
    // The sun's color is not reset each frame by the game unless it is one of its "dusk" lights, so for the sun
    // the original color is kept and the tint applied to that.
    internal static class SeasonLook
    {
        private static readonly int SkyDay = Shader.PropertyToID("_SkyColor");
        private static readonly int HorizonDay = Shader.PropertyToID("_HorizonColor");
        private static readonly int SkyNight = Shader.PropertyToID("_SkyColorNight");
        private static readonly int HorizonNight = Shader.PropertyToID("_HorizonNight");

        private static readonly FieldInfo duskedField = AccessTools.Field(typeof(AmbianceDirector), "duskedLightDefaultColors");
        private static readonly Dictionary<Light, Color> sunOriginal = new Dictionary<Light, Color>();

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(AmbianceDirector), "Update");
            if (target == null)
            {
                Main.Log("AmbianceDirector.Update not found: no seasonal colors");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(SeasonLook), "AfterAmbiance"));
        }

        private static void AfterAmbiance(AmbianceDirector __instance)
        {
            try
            {
                TimeDirector time = SRSingleton<SceneContext>.Instance.TimeDirector;
                float dayWithFraction = time.CurrDay() + time.CurrHour() / 24f;
                Color tint = Season.CurrentTint(dayWithFraction);

                RenderSettings.ambientLight = Mul(RenderSettings.ambientLight, tint);
                RenderSettings.fogColor = Mul(RenderSettings.fogColor, tint);

                Material sky = RenderSettings.skybox;
                if (sky != null)
                {
                    foreach (int id in new[] { SkyDay, HorizonDay, SkyNight, HorizonNight })
                    {
                        if (sky.HasProperty(id)) sky.SetColor(id, Mul(sky.GetColor(id), tint));
                    }
                }

                Light sun = RenderSettings.sun;
                if (sun != null)
                {
                    var dusked = duskedField != null ? duskedField.GetValue(__instance) as Dictionary<Light, Color> : null;
                    if (dusked != null && dusked.ContainsKey(sun))
                    {
                        sun.color = Mul(sun.color, tint); // the game has just reset it this frame
                    }
                    else
                    {
                        Color original;
                        if (!sunOriginal.TryGetValue(sun, out original)) sunOriginal[sun] = original = sun.color;
                        sun.color = Mul(original, tint);
                    }
                }
            }
            catch (Exception)
            {
                // a frame without tint is harmless; never break the game's lighting
            }
        }

        private static Color Mul(Color c, Color tint)
        {
            return new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
        }
    }
}
