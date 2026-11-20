using System;
using HarmonyLib;
using UnityEngine;

namespace SRCorrals
{
    // Slime-side effects of the calm and production upgrades, for slimes inside an upgraded corral.
    //
    // Calm: SlimeEmotions.GetRecoveryFactor(AGITATION) is 1, +1 with a music box (vanilla); the diffuser adds its
    // level on top, so agitation drops faster and rises slower.
    // Production: SlimeEat.Produce(count, produces) spawns the plorts of a meal; the stimulator raises count.
    internal static class CorralEffects
    {
        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, typeof(SlimeEmotions), "GetRecoveryFactor", null, "AfterRecoveryFactor");
            Patch(harmony, typeof(SlimeEat), "Produce", "BeforeProduce", null);
        }

        private static void Patch(Harmony harmony, Type type, string method, string prefix, string postfix)
        {
            var target = AccessTools.Method(type, method);
            if (target == null)
            {
                Main.Log(type.Name + "." + method + " not found, that corral effect is disabled");
                return;
            }
            harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(typeof(CorralEffects), prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(typeof(CorralEffects), postfix) : null);
        }

        private static void AfterRecoveryFactor(SlimeEmotions __instance, SlimeEmotions.Emotion emotion, ref float __result)
        {
            if (emotion != SlimeEmotions.Emotion.AGITATION) return;
            CorralUpgrader corral = CorralOf(__instance, true);
            if (corral != null) __result += corral.CalmLevel;
        }

        private static void BeforeProduce(SlimeEat __instance, ref int count, GameObject produces)
        {
            if (produces == null || !Identifiable.IsPlort(Identifiable.GetId(produces))) return;
            CorralUpgrader corral = CorralOf(__instance, false);
            if (corral == null) return;
            if (corral.ProductionLevel >= 2) count += 1;
            else if (corral.ProductionLevel == 1 && UnityEngine.Random.value < 0.5f) count += 1;
        }

        // The upgraded corral holding this slime, if any. Fast exit when no corral has the upgrade (most frames).
        private static CorralUpgrader CorralOf(Component slime, bool calm)
        {
            Identifiable ident = null;
            foreach (CorralUpgrader c in CorralUpgrader.Active)
            {
                if ((calm ? c.CalmLevel : c.ProductionLevel) <= 0) continue;
                if (ident == null) ident = slime.GetComponent<Identifiable>();
                if (ident == null) return null;
                if (c.Contains(ident)) return c;
            }
            return null;
        }
    }
}
