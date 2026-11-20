using System;
using System.Reflection;
using HarmonyLib;
using MonomiPark.SlimeRancher.DataModel;

namespace SRSeasons
{
    // Seasonal gameplay, applied where the game computes the values, so both players (who share the day) get
    // the same results:
    //   - prices: EconomyDirector.GetTargetValue(..., day) is multiplied for the plorts in season that day;
    //   - gardens: the time before a crop's next harvest is scaled when the game schedules it, both through
    //     StepNextSpawnTime(ripeness, stepHours) and UpdateNextSpawnTime (which draws its own interval).
    internal static class GameplayPatches
    {
        private static readonly FieldInfo spawnModelField = AccessTools.Field(typeof(SpawnResource), "model");

        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, typeof(EconomyDirector), "GetTargetValue", null, "AfterTargetValue");
            Patch(harmony, typeof(SpawnResource), "StepNextSpawnTime", "BeforeStep", null);
            Patch(harmony, typeof(SpawnResource), "UpdateNextSpawnTime", null, "AfterUpdateNext");
        }

        private static void Patch(Harmony harmony, Type type, string method, string prefix, string postfix)
        {
            MethodInfo target = AccessTools.Method(type, method);
            if (target == null)
            {
                Main.Log(type.Name + "." + method + " not found, that seasonal effect is off");
                return;
            }
            harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(typeof(GameplayPatches), prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(typeof(GameplayPatches), postfix) : null);
        }

        private static void AfterTargetValue(Identifiable.Id id, float day, ref float __result)
        {
            __result *= Season.PriceFactor(id, (int)day);
        }

        private static void BeforeStep(ref float stepHours)
        {
            stepHours *= Season.GrowthTime(CurrentSeason());
        }

        // UpdateNextSpawnTime set nextSpawnTime = now + interval: scale that interval.
        private static void AfterUpdateNext(SpawnResource __instance)
        {
            var model = spawnModelField != null ? spawnModelField.GetValue(__instance) as SpawnResourceModel : null;
            TimeDirector time = SRSingleton<SceneContext>.Instance != null ? SRSingleton<SceneContext>.Instance.TimeDirector : null;
            if (model == null || time == null) return;
            double now = time.WorldTime();
            double interval = model.nextSpawnTime - now;
            if (interval > 0) model.nextSpawnTime = now + interval * Season.GrowthTime(CurrentSeason());
        }

        internal static SeasonId CurrentSeason()
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            return scene != null && scene.TimeDirector != null ? Season.Of(scene.TimeDirector.CurrDay()) : SeasonId.Spring;
        }
    }
}
