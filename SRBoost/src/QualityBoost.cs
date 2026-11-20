using System;
using HarmonyLib;
using UnityEngine;

namespace SRBoost
{
    // SRQualitySettings applies each in-game graphics option through a private static Set* method. A postfix on
    // each one pushes the top level further than the game allows, and leaves lower levels untouched so the
    // in-game menu still works as a way to trade quality for speed.
    internal static class QualityBoost
    {
        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, "SetShadows");
            Patch(harmony, "SetModelDetail");
            Patch(harmony, "SetLighting");

            // The game may have applied its settings before this mod loaded.
            try
            {
                Boost();
            }
            catch (Exception e)
            {
                Main.Log("initial quality boost skipped: " + e.Message);
            }
        }

        private static void Patch(Harmony harmony, string method)
        {
            var target = AccessTools.Method(typeof(SRQualitySettings), method);
            if (target == null)
            {
                Main.Log("SRQualitySettings." + method + " not found, not boosted");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(QualityBoost), "Boost"));
        }

        private static void Boost()
        {
            BoostConfig c = Main.Config;

            if (SRQualitySettings.Shadows == SRQualitySettings.ShadowsLevel.HIGH)
            {
                QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, c.ShadowDistance);
                QualitySettings.shadowResolution = c.ShadowResolution;
            }
            if (SRQualitySettings.ModelDetail == SRQualitySettings.ModelDetailLevel.HIGH)
            {
                QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, c.LodBias);
            }
            if (SRQualitySettings.Lighting == SRQualitySettings.LightingLevel.HIGHEST)
            {
                QualitySettings.pixelLightCount = Math.Max(QualitySettings.pixelLightCount, c.PixelLights);
            }
        }
    }
}
