using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SRCorrals
{
    // Coops.
    //   Incubator I/II: hens lay 25 % / 50 % more often (Reproduce.ReproducePeriod, 6 to 12 h in vanilla, shortened).
    //   Extension I/II: x1.5 / x2 chickens before the coop counts as crowded (Reproduce.MaxDensity, 10 in vanilla).
    // Both are read by patches on Reproduce, for hens standing in an upgraded coop.
    internal class CoopUpgrader : RanchUpgrader
    {
        internal static readonly List<CoopUpgrader> Active = new List<CoopUpgrader>();

        internal int IncubatorLevel, ExtensionLevel;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        protected override bool OnUpgrade(LandPlot.Upgrade upgrade)
        {
            if (upgrade == Upgrades.Incubator1) IncubatorLevel = Mathf.Max(IncubatorLevel, 1);
            else if (upgrade == Upgrades.Incubator2) IncubatorLevel = Mathf.Max(IncubatorLevel, 2);
            else if (upgrade == Upgrades.Extension1) ExtensionLevel = Mathf.Max(ExtensionLevel, 1);
            else if (upgrade == Upgrades.Extension2) ExtensionLevel = Mathf.Max(ExtensionLevel, 2);
            else return false;
            return true;
        }

        protected override bool BuildVisuals()
        {
            Visuals.TintPlot(gameObject, Mathf.Max(IncubatorLevel, ExtensionLevel));
            return true;
        }

        // ---- Patches on chicken reproduction -----------------------------------------------------------------

        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, "ReproducePeriod", "AfterReproducePeriod");
            Patch(harmony, "MaxDensity", "AfterMaxDensity");
        }

        private static void Patch(Harmony harmony, string method, string postfix)
        {
            var target = AccessTools.Method(typeof(Reproduce), method);
            if (target == null)
            {
                Main.Log("Reproduce." + method + " not found, that coop upgrade has no effect");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(CoopUpgrader), postfix));
        }

        private static void AfterReproducePeriod(Reproduce __instance, ref float __result)
        {
            CoopUpgrader coop = CoopOf(__instance, true);
            if (coop != null) __result *= 1f - 0.25f * coop.IncubatorLevel;
        }

        private static void AfterMaxDensity(Reproduce __instance, ref int __result)
        {
            CoopUpgrader coop = CoopOf(__instance, false);
            if (coop != null) __result = Mathf.RoundToInt(__result * (1f + 0.5f * coop.ExtensionLevel));
        }

        // The upgraded coop holding this chicken, if any. Fast exit when no coop has the upgrade.
        private static CoopUpgrader CoopOf(Component chicken, bool incubator)
        {
            Identifiable ident = null;
            foreach (CoopUpgrader c in Active)
            {
                if ((incubator ? c.IncubatorLevel : c.ExtensionLevel) <= 0) continue;
                if (ident == null) ident = chicken.GetComponent<Identifiable>();
                if (ident == null) return null;
                if (c.Contains(ident)) return c;
            }
            return null;
        }
    }
}
