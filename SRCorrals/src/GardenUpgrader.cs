using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MonomiPark.SlimeRancher.DataModel;
using UnityEngine;

namespace SRCorrals
{
    // Gardens.
    //   Fertilizer I/II: the crop grows 25 % / 50 % faster (spawn interval scaled).
    //   Bounty I/II: +1 / +2 produce per cycle (limited by the plant's free attach points, which the harvester
    //   keeps freeing).
    //   Harvester: every few seconds, ripe or fallen produce goes into the nearest silo that has room. If none
    //   has room, it stays on the plant as usual, so nothing is lost. Silos are saved by the game and synced by
    //   SRMP; harvesting runs only on the side that simulates the plot.
    // The crop is a child object created when planting (and replaced when replanting), so the scaling is
    // re-applied to whatever plant is there at each tick, always from the plant's own original values.
    internal class GardenUpgrader : RanchUpgrader
    {
        private const float TickSeconds = 5f;
        private const float SiloRange = 120f;

        private int fertilizerLevel, bountyLevel;
        private bool harvester;
        private float nextTick;

        private struct PlantBase
        {
            public float MinInterval, MaxInterval, MinCount, MaxCount, MinNutrientCount;
        }

        private readonly Dictionary<SpawnResource, PlantBase> originals = new Dictionary<SpawnResource, PlantBase>();
        private static readonly FieldInfo spawnedField = AccessTools.Field(typeof(SpawnResource), "spawned");
        private static readonly FieldInfo cycleModelField = AccessTools.Field(typeof(ResourceCycle), "model");
        private static readonly FieldInfo plotObjectField = AccessTools.Field(typeof(LandPlotModel), "gameObj");
        private static bool loggedFirstHarvest;

        protected override bool OnUpgrade(LandPlot.Upgrade upgrade)
        {
            if (upgrade == Upgrades.Fertilizer1) fertilizerLevel = Mathf.Max(fertilizerLevel, 1);
            else if (upgrade == Upgrades.Fertilizer2) fertilizerLevel = Mathf.Max(fertilizerLevel, 2);
            else if (upgrade == Upgrades.Bounty1) bountyLevel = Mathf.Max(bountyLevel, 1);
            else if (upgrade == Upgrades.Bounty2) bountyLevel = Mathf.Max(bountyLevel, 2);
            else if (upgrade == Upgrades.Harvester) harvester = true;
            else return false;
            nextTick = 0f; // apply right away
            return true;
        }

        protected override bool BuildVisuals()
        {
            Visuals.TintPlot(gameObject, Mathf.Max(fertilizerLevel, bountyLevel));
            return true;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + TickSeconds;
            if (fertilizerLevel == 0 && bountyLevel == 0 && !harvester) return;

            foreach (SpawnResource plant in GetComponentsInChildren<SpawnResource>())
            {
                ScalePlant(plant);
                if (harvester && SimulatedHere()) Harvest(plant);
            }
        }

        private void ScalePlant(SpawnResource plant)
        {
            PlantBase b;
            if (!originals.TryGetValue(plant, out b))
            {
                b = new PlantBase
                {
                    MinInterval = plant.MinSpawnIntervalGameHours, MaxInterval = plant.MaxSpawnIntervalGameHours,
                    MinCount = plant.MinObjectsSpawned, MaxCount = plant.MaxObjectsSpawned,
                    MinNutrientCount = plant.MinNutrientObjectsSpawned
                };
                originals[plant] = b;
            }
            float speed = 1f - 0.25f * fertilizerLevel;
            plant.MinSpawnIntervalGameHours = b.MinInterval * speed;
            plant.MaxSpawnIntervalGameHours = b.MaxInterval * speed;
            // The game draws (int)Random.Range(min, max): +1 on both ends adds one produce per cycle.
            plant.MinObjectsSpawned = b.MinCount + bountyLevel;
            plant.MaxObjectsSpawned = b.MaxCount + bountyLevel;
            plant.MinNutrientObjectsSpawned = b.MinNutrientCount + bountyLevel;
        }

        private void Harvest(SpawnResource plant)
        {
            var spawned = spawnedField != null ? spawnedField.GetValue(plant) as List<GameObject> : null;
            if (spawned == null || spawned.Count == 0) return;

            List<SiloStorage> silos = null;
            int harvested = 0;
            foreach (GameObject produce in spawned.ToArray())
            {
                if (produce == null || !Ready(produce)) continue;
                Identifiable.Id id = Identifiable.GetId(produce);
                if (id == Identifiable.Id.NONE) continue;
                if (silos == null) silos = NearbySilos();
                foreach (SiloStorage silo in silos)
                {
                    if (!silo.CanAccept(id) || !silo.MaybeAddIdentifiable(id)) continue;
                    Destroyer.DestroyActor(produce, "SRCorrals.Harvester");
                    harvested++;
                    break;
                }
            }
            if (harvested > 0 && !loggedFirstHarvest)
            {
                loggedFirstHarvest = true;
                Main.Log("harvester: first harvest, " + harvested + " item(s) sent to a silo");
            }
        }

        // Ripe on the plant, or fallen and still edible. Unripe and rotten produce are left alone.
        private static bool Ready(GameObject produce)
        {
            ResourceCycle cycle = produce.GetComponent<ResourceCycle>();
            var model = cycle != null && cycleModelField != null ? cycleModelField.GetValue(cycle) as ProduceModel : null;
            return model != null && (model.state == ResourceCycle.State.RIPE || model.state == ResourceCycle.State.EDIBLE);
        }

        // Every silo storage on the ranch within range, nearest first.
        private List<SiloStorage> NearbySilos()
        {
            var result = new List<SiloStorage>();
            Vector3 here = transform.position;
            foreach (LandPlotModel plot in SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().Values)
            {
                if (plot == null || plot.typeId != LandPlot.Id.SILO) continue;
                var go = plotObjectField != null ? plotObjectField.GetValue(plot) as GameObject : null;
                if (go == null || Vector3.Distance(go.transform.position, here) > SiloRange) continue;
                result.AddRange(go.GetComponentsInChildren<SiloStorage>());
            }
            result.Sort((a, b) => Vector3.Distance(a.transform.position, here).CompareTo(Vector3.Distance(b.transform.position, here)));
            return result;
        }
    }
}
