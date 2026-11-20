using UnityEngine;

namespace SRSeasons
{
    // The season of a game day: 7 days each, spring, summer, autumn, winter, then again (a 28-day year).
    // Computed from the game day only, which SRMP keeps identical for both players, so they always share the
    // season, its prices and its growth speed. Visual changes blend over the last day of a season.
    internal enum SeasonId { Spring, Summer, Autumn, Winter }

    internal static class Season
    {
        internal const int DaysPerSeason = 7;

        // Set by the "season" console command to preview a season; null = follow the day.
        internal static SeasonId? Forced;

        internal static SeasonId Of(int day)
        {
            if (Forced.HasValue) return Forced.Value;
            return (SeasonId)(((day / DaysPerSeason) % 4 + 4) % 4);
        }

        internal static int DayInSeason(int day)
        {
            return ((day % DaysPerSeason) + DaysPerSeason) % DaysPerSeason + 1; // 1..7
        }

        internal static string Name(SeasonId s)
        {
            switch (s)
            {
                case SeasonId.Spring: return "Printemps";
                case SeasonId.Summer: return "Été";
                case SeasonId.Autumn: return "Automne";
                default: return "Hiver";
            }
        }

        // ---- Look ----------------------------------------------------------------------------------------

        // Multiplier on the world's light, fog and sky colors: mild, so zones keep their own character.
        internal static Color Tint(SeasonId s)
        {
            switch (s)
            {
                case SeasonId.Spring: return new Color(0.95f, 1.05f, 0.95f, 1f);  // fresh green
                case SeasonId.Summer: return new Color(1.08f, 1.02f, 0.88f, 1f);  // warm gold
                case SeasonId.Autumn: return new Color(1.10f, 0.92f, 0.76f, 1f);  // orange
                default: return new Color(0.86f, 0.94f, 1.12f, 1f);               // cold blue
            }
        }

        // Accent used by the HUD pill.
        internal static Color Accent(SeasonId s)
        {
            switch (s)
            {
                case SeasonId.Spring: return new Color(0.55f, 0.90f, 0.50f, 1f);
                case SeasonId.Summer: return new Color(1f, 0.82f, 0.35f, 1f);
                case SeasonId.Autumn: return new Color(1f, 0.55f, 0.25f, 1f);
                default: return new Color(0.65f, 0.85f, 1f, 1f);
            }
        }

        // Current tint, blending into the next season over the last day of this one.
        internal static Color CurrentTint(float dayWithFraction)
        {
            if (Forced.HasValue) return Tint(Forced.Value);
            int day = Mathf.FloorToInt(dayWithFraction);
            SeasonId now = Of(day);
            if (DayInSeason(day) < DaysPerSeason) return Tint(now);
            float t = dayWithFraction - day; // 0..1 across the last day
            return Color.Lerp(Tint(now), Tint(Of(day + 1)), t);
        }

        // ---- Gardens -------------------------------------------------------------------------------------

        // Multiplier on the time before a crop's next harvest (lower = faster).
        internal static float GrowthTime(SeasonId s)
        {
            switch (s)
            {
                case SeasonId.Spring: return 0.8f;
                case SeasonId.Summer: return 0.9f;
                case SeasonId.Autumn: return 1f;
                default: return 1.4f;
            }
        }

        // ---- Prices --------------------------------------------------------------------------------------

        internal const float InSeasonBonus = 1.25f;

        // Three plorts are "in season" each season and sell 25 % higher.
        internal static Identifiable.Id[] InSeason(SeasonId s)
        {
            switch (s)
            {
                case SeasonId.Spring: return new[] { Identifiable.Id.HONEY_PLORT, Identifiable.Id.PINK_PLORT, Identifiable.Id.PHOSPHOR_PLORT };
                case SeasonId.Summer: return new[] { Identifiable.Id.PUDDLE_PLORT, Identifiable.Id.TANGLE_PLORT, Identifiable.Id.RAD_PLORT };
                case SeasonId.Autumn: return new[] { Identifiable.Id.HUNTER_PLORT, Identifiable.Id.BOOM_PLORT, Identifiable.Id.MOSAIC_PLORT };
                default: return new[] { Identifiable.Id.FIRE_PLORT, Identifiable.Id.CRYSTAL_PLORT, Identifiable.Id.QUANTUM_PLORT };
            }
        }

        // Price multiplier of a plort on a given day (used by the price patch, and by SR Market's forecast).
        public static float PriceFactor(Identifiable.Id id, int day)
        {
            foreach (Identifiable.Id x in InSeason(Of(day))) if (x == id) return InSeasonBonus;
            return 1f;
        }
    }
}
