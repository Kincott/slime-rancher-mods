using System;
using System.Collections.Generic;
using MonomiPark.SlimeRancher.DataModel;
using UnityEngine;

namespace SRMarket
{
    // The game's own price formula (EconomyDirector.GetTargetValue), evaluated for any day:
    //
    //   price(day) = base * (1 + clamp01((fullSat - sat) / fullSat))         <- market saturation, from sales
    //                      * (Perlin(day, seed, -10000) + 0.7)               <- whole-market trend
    //                      * (Perlin(day, seed, id * 10000) + 0.7)           <- this plort's own trend
    //
    // Both noise terms are deterministic (the seed is saved in the world), and saturation only decays by
    // saturationRecovery at each daily reset when nobody sells. So the price of the coming days is known exactly
    // as long as nobody sells: that is the forecast. Check() compares the formula with the real price at each reset.
    internal static class PriceModel
    {
        internal struct Plort
        {
            public Identifiable.Id Id;
            public float BaseValue;
            public float FullSaturation;
        }

        private static EconomyDirector Econ
        {
            get { return SRSingleton<SceneContext>.Instance != null ? SRSingleton<SceneContext>.Instance.EconomyDirector : null; }
        }

        private static WorldModel World
        {
            get
            {
                SceneContext scene = SRSingleton<SceneContext>.Instance;
                return scene != null && scene.GameModel != null ? scene.GameModel.GetWorldModel() : null;
            }
        }

        internal static bool Ready
        {
            get { return Econ != null && World != null && SRSingleton<SceneContext>.Instance.TimeDirector != null; }
        }

        internal static int Today
        {
            get { return SRSingleton<SceneContext>.Instance.TimeDirector.CurrDay(); }
        }

        // Every item the market buys, in the market's own order.
        internal static List<Plort> Plorts()
        {
            var list = new List<Plort>();
            EconomyDirector econ = Econ;
            if (econ == null || econ.baseValueMap == null) return list;
            foreach (EconomyDirector.ValueMap v in econ.baseValueMap)
            {
                if (v == null || v.accept == null) continue;
                list.Add(new Plort { Id = v.accept.id, BaseValue = v.value, FullSaturation = v.fullSaturation });
            }
            return list;
        }

        internal static int? Current(Identifiable.Id id)
        {
            EconomyDirector econ = Econ;
            return econ != null ? econ.GetCurrValue(id) : null;
        }

        private static bool Dynamic
        {
            get { return SRSingleton<SceneContext>.Instance.GameModeConfig.GetModeSettings().plortMarketDynamic; }
        }

        // Price for `day` given a saturation value, exactly as GetTargetValue computes it.
        internal static float PriceFor(Plort p, float saturation, int day)
        {
            if (!Dynamic) return p.BaseValue * 1.5f;
            WorldModel world = World;
            float satFactor = 1f + Mathf.Clamp01((p.FullSaturation - saturation) / p.FullSaturation);
            float market = Noise.Noise.PerlinNoise(day, world.econSeed, -10000f, 10f, 0.6f, 1f) + 0.7f;
            float own = Noise.Noise.PerlinNoise(day, world.econSeed, p.Id.GetHashCode() * 10000, 10f, 0.6f, 1f) + 0.7f;
            return p.BaseValue * satFactor * market * own;
        }

        // Prices of the next `days` days if nobody sells from now on (today's sales already count).
        internal static int[] Forecast(Plort p, int days)
        {
            var result = new int[days];
            WorldModel world = World;
            float saturation;
            if (world == null || !world.marketSaturation.TryGetValue(p.Id, out saturation)) return result;
            float recovery = Econ.saturationRecovery;
            int today = Today;
            for (int k = 1; k <= days; k++)
            {
                saturation *= 1f - recovery; // one daily reset
                result[k - 1] = Mathf.RoundToInt(PriceFor(p, saturation, today + k));
            }
            return result;
        }

        // Changes whenever a sale (saturation) or a reset (day) happens: cheap change detection for the UI.
        internal static float Stamp()
        {
            WorldModel world = World;
            if (world == null) return 0f;
            float sum = Today * 1000f;
            foreach (float s in world.marketSaturation.Values) sum += s;
            return sum;
        }

        // Right after a reset the saturation is the one the game just used: the formula must give the same prices.
        internal static void Check(int day)
        {
            WorldModel world = World;
            if (world == null) return;
            int match = 0, total = 0;
            foreach (Plort p in Plorts())
            {
                float sat;
                int? real = Current(p.Id);
                if (!real.HasValue || !world.marketSaturation.TryGetValue(p.Id, out sat)) continue;
                total++;
                if (Math.Abs(Mathf.RoundToInt(PriceFor(p, sat, day)) - real.Value) <= 1) match++;
            }
            Main.Log("forecast model check for day " + day + ": " + match + "/" + total + " prices match the game");
        }
    }
}
