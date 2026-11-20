using System;
using System.Collections.Generic;
using HarmonyLib;
using MonomiPark.SlimeRancher.DataModel;
using UnityEngine;

namespace SRBoost
{
    // Vanilla feeders drop a fixed 6 items per cycle (every 3, 6 or 9 in-game hours) whatever the corral holds,
    // so a full corral stays hungry. On top of that cycle, the smart feeder checks the corral every few seconds
    // and queues one item per slime that is hungry enough to eat (hunger >= its SlimeEat.minDriveToEat), minus
    // the food already lying in the corral, so it does not flood it.
    //
    // It only adds to the feeder's own queue (LandPlotModel.remainingFeedOperations), so the normal ejection
    // path still runs: in multiplayer SRMP lets only the corral's owner actually eject and remove the food from
    // storage; the other side just counts the operation down, so nothing is used twice.
    internal static class SmartFeeder
    {
        private const int MaxPerCheck = 6;

        private static readonly Dictionary<int, float> nextCheckAt = new Dictionary<int, float>();
        private static readonly Dictionary<int, TrackContainedIdentifiables[]> trackersByFeeder =
            new Dictionary<int, TrackContainedIdentifiables[]>();

        private static readonly List<GameObject> slimes = new List<GameObject>();
        private static readonly List<Identifiable> food = new List<Identifiable>();
        private static bool loggedError;

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(SlimeFeeder), "Update");
            if (target == null)
            {
                Main.Log("SlimeFeeder.Update not found, smart feeder disabled");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(SmartFeeder), "Postfix"));
        }

        // ___model / ___region: Harmony injects the feeder's private fields of those names.
        private static void Postfix(SlimeFeeder __instance, LandPlotModel ___model, MonomiPark.SlimeRancher.Regions.Region ___region)
        {
            BoostConfig c = Main.Config;
            if (!c.SmartFeeder || ___model == null || ___region == null || ___region.Hibernated)
            {
                return;
            }

            int key = __instance.GetInstanceID();
            float now = Time.time;
            float due;
            if (nextCheckAt.TryGetValue(key, out due) && now < due)
            {
                return;
            }
            nextCheckAt[key] = now + Math.Max(1, c.FeederCheckSeconds);

            // Still ejecting the previous batch, or nothing to give.
            if (___model.remainingFeedOperations > 0 || __instance.GetFoodCount() <= 0)
            {
                return;
            }

            try
            {
                int need = CountHungry(__instance) - CountFoodOnGround(__instance);
                if (need > 0)
                {
                    ___model.remainingFeedOperations += Math.Min(need, MaxPerCheck);
                }
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    Main.Log("smart feeder check failed (logged once): " + e.Message);
                }
            }
        }

        private static int CountHungry(SlimeFeeder feeder)
        {
            int hungry = 0;
            foreach (TrackContainedIdentifiables tracker in Trackers(feeder))
            {
                slimes.Clear();
                tracker.GetTrackedItemsOfClass(Identifiable.SLIME_CLASS, slimes);
                tracker.GetTrackedItemsOfClass(Identifiable.LARGO_CLASS, slimes);
                foreach (GameObject slime in slimes)
                {
                    if (slime == null)
                    {
                        continue;
                    }
                    SlimeEat eat = slime.GetComponent<SlimeEat>();
                    SlimeEmotions emotions = slime.GetComponent<SlimeEmotions>();
                    if (eat != null && emotions != null &&
                        emotions.GetCurr(SlimeEmotions.Emotion.HUNGER) >= eat.minDriveToEat)
                    {
                        hungry++;
                    }
                }
            }
            slimes.Clear();
            return hungry;
        }

        private static int CountFoodOnGround(SlimeFeeder feeder)
        {
            int count = 0;
            foreach (TrackContainedIdentifiables tracker in Trackers(feeder))
            {
                food.Clear();
                tracker.GetTrackedItemsOfClass(Identifiable.FOOD_CLASS, food);
                foreach (Identifiable item in food)
                {
                    if (item != null)
                    {
                        count++;
                    }
                }
            }
            food.Clear();
            return count;
        }

        // The corral's content trackers, found once per feeder (the same ones the game's drones use).
        private static TrackContainedIdentifiables[] Trackers(SlimeFeeder feeder)
        {
            int key = feeder.GetInstanceID();
            TrackContainedIdentifiables[] trackers;
            if (!trackersByFeeder.TryGetValue(key, out trackers) || trackers == null || Array.Exists(trackers, t => t == null))
            {
                LandPlot plot = feeder.GetComponentInParent<LandPlot>();
                trackers = plot != null
                    ? plot.GetComponentsInChildren<TrackContainedIdentifiables>()
                    : new TrackContainedIdentifiables[0];
                trackersByFeeder[key] = trackers;
            }
            return trackers;
        }
    }
}
