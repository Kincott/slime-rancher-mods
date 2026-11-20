using System.Collections.Generic;
using UnityEngine;

namespace SRCorrals
{
    // Corrals. Collector and feeder tiers change the components directly (frequency, amount per cycle, storage per
    // slot); calm and production levels are kept here and read by the slime patches (CorralEffects).
    internal class CorralUpgrader : RanchUpgrader
    {
        internal static readonly List<CorralUpgrader> Active = new List<CorralUpgrader>();

        internal int CalmLevel;
        internal int ProductionLevel;

        private int collectorLevel = 1, feederLevel = 1;
        // Vanilla values, read before the first change so tiers multiply the original, never a modified value.
        private float baseCollectPeriod = -1f;
        private int baseCollectorStorage = -1, baseFeederItems = -1, baseFeederStorage = -1;

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        protected override bool OnUpgrade(LandPlot.Upgrade upgrade)
        {
            if (upgrade == Upgrades.Collector2) SetCollector(2);
            else if (upgrade == Upgrades.Collector3) SetCollector(3);
            else if (upgrade == Upgrades.Feeder2) SetFeeder(2);
            else if (upgrade == Upgrades.Feeder3) SetFeeder(3);
            else if (upgrade == Upgrades.Calm1) CalmLevel = Mathf.Max(CalmLevel, 1);
            else if (upgrade == Upgrades.Calm2) CalmLevel = Mathf.Max(CalmLevel, 2);
            else if (upgrade == Upgrades.Production1) ProductionLevel = Mathf.Max(ProductionLevel, 1);
            else if (upgrade == Upgrades.Production2) ProductionLevel = Mathf.Max(ProductionLevel, 2);
            else return false;
            return true;
        }

        protected override bool BuildVisuals()
        {
            PlortCollectorUpgrader c = GetComponent<PlortCollectorUpgrader>();
            FeederUpgrader f = GetComponent<FeederUpgrader>();
            return Visuals.Build(transform,
                c != null ? c.collector : null, collectorLevel,
                f != null ? f.feeder : null, feederLevel,
                CalmLevel, ProductionLevel);
        }

        // Mk II: twice as often, 2.5x storage. Mk III: four times as often, 5x storage. Upgrades can be applied in
        // any order on load, so the highest tier wins.
        private void SetCollector(int level)
        {
            collectorLevel = Mathf.Max(collectorLevel, level);
            PlortCollector collector = GetComponentInChildren<PlortCollector>(true);
            if (collector == null) return;
            if (baseCollectPeriod < 0f) baseCollectPeriod = collector.collectPeriod;
            collector.collectPeriod = baseCollectPeriod / (collectorLevel == 3 ? 4f : 2f);
            SetStorage(collector.GetComponentInParent<SiloStorage>(), ref baseCollectorStorage, collectorLevel);
        }

        // Mk II: twice the food per cycle, 2.5x storage. Mk III: three times, 5x storage.
        private void SetFeeder(int level)
        {
            feederLevel = Mathf.Max(feederLevel, level);
            SlimeFeeder feeder = GetComponentInChildren<SlimeFeeder>(true);
            if (feeder == null) return;
            if (baseFeederItems < 0) baseFeederItems = feeder.itemsPerFeeding;
            feeder.itemsPerFeeding = baseFeederItems * feederLevel;
            SetStorage(feeder.GetComponentInParent<SiloStorage>(), ref baseFeederStorage, feederLevel);
        }

        // SiloStorage reads maxAmmo each time it checks a slot's capacity, so raising it takes effect at once.
        private static void SetStorage(SiloStorage storage, ref int baseMax, int level)
        {
            if (storage == null) return;
            if (baseMax < 0) baseMax = storage.maxAmmo;
            storage.maxAmmo = level == 3 ? baseMax * 5 : (baseMax * 5) / 2;
        }
    }
}
