using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace SRMarket
{
    // Daily plort prices, one file per save game (the game keeps no history). A day is recorded when the market
    // resets its prices (EconomyDirector.ResetPrices) and, on load, for the current day if it is missing.
    //
    // File: SRML/Mods/SRMarketData/<save name>.prices, one line per day: "day|ID=price;ID=price".
    internal static class PriceHistory
    {
        private const int KeepDays = 120;

        private static readonly SortedDictionary<int, Dictionary<Identifiable.Id, int>> days =
            new SortedDictionary<int, Dictionary<Identifiable.Id, int>>();
        private static string loadedFor;

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(EconomyDirector), "ResetPrices");
            if (target == null)
            {
                Main.Log("EconomyDirector.ResetPrices not found, prices will only be recorded on load");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PriceHistory), "AfterReset"));
        }

        // day is ResetPrices' own parameter: the day the new prices belong to.
        private static void AfterReset(int day)
        {
            try
            {
                // While a save loads, the game resets prices for placeholder days (0, 1) before the real world is
                // in place, and also in the main menu's background world. Only the current day of a loaded game
                // is a real price day.
                SceneContext scene = SRSingleton<SceneContext>.Instance;
                if (!PriceModel.Ready || scene.PlayerState == null || day != PriceModel.Today) return;
                EnsureLoaded();
                Record(day);
                PriceModel.Check(day);
            }
            catch (Exception e)
            {
                Main.Log("recording prices failed: " + e.Message);
            }
        }

        // Called every frame by Main: loads the right file when a save is entered, records today if missing.
        internal static void Tick()
        {
            if (!PriceModel.Ready) return;
            if (EnsureLoaded() || !days.ContainsKey(PriceModel.Today))
            {
                Record(PriceModel.Today);
            }
        }

        internal static int Count { get { return days.Count; } }

        // The last `n` recorded prices for a plort, oldest first (fewer if the history is shorter).
        internal static List<int> Last(Identifiable.Id id, int n)
        {
            var result = new List<int>();
            int today = PriceModel.Ready ? PriceModel.Today : int.MaxValue;
            foreach (KeyValuePair<int, Dictionary<Identifiable.Id, int>> day in days)
            {
                int price;
                if (day.Key <= today && day.Value.TryGetValue(id, out price)) result.Add(price);
            }
            if (result.Count > n) result.RemoveRange(0, result.Count - n);
            return result;
        }

        private static void Record(int day)
        {
            var prices = new Dictionary<Identifiable.Id, int>();
            foreach (PriceModel.Plort p in PriceModel.Plorts())
            {
                int? price = PriceModel.Current(p.Id);
                if (price.HasValue) prices[p.Id] = price.Value;
            }
            if (prices.Count == 0) return;
            days[day] = prices;
            while (days.Count > KeepDays)
            {
                foreach (int first in days.Keys) { days.Remove(first); break; }
            }
            Save();
        }

        // Returns true when a (new) save's history was just loaded.
        private static bool EnsureLoaded()
        {
            string name = SaveName();
            if (name == loadedFor) return false;
            loadedFor = name;
            days.Clear();
            string path = FilePath(name);
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path)) ParseLine(line);
            }
            Main.Log("price history for '" + name + "': " + days.Count + " day(s)");
            return true;
        }

        private static void ParseLine(string line)
        {
            int bar = line.IndexOf('|');
            int day;
            if (bar <= 0 || !int.TryParse(line.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out day)) return;
            var prices = new Dictionary<Identifiable.Id, int>();
            foreach (string pair in line.Substring(bar + 1).Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                int price;
                if (!int.TryParse(pair.Substring(eq + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out price)) continue;
                try
                {
                    prices[(Identifiable.Id)Enum.Parse(typeof(Identifiable.Id), pair.Substring(0, eq))] = price;
                }
                catch (ArgumentException)
                {
                    // an id from a mod that is no longer installed: skip it
                }
            }
            if (prices.Count > 0) days[day] = prices;
        }

        private static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (KeyValuePair<int, Dictionary<Identifiable.Id, int>> day in days)
                {
                    sb.Append(day.Key.ToString(CultureInfo.InvariantCulture)).Append('|');
                    bool first = true;
                    foreach (KeyValuePair<Identifiable.Id, int> p in day.Value)
                    {
                        if (!first) sb.Append(';');
                        first = false;
                        sb.Append(p.Key).Append('=').Append(p.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    sb.Append('\n');
                }
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                Main.Log("saving price history failed: " + e.Message);
            }
        }

        private static string SaveName()
        {
            try
            {
                string name = SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName();
                return string.IsNullOrEmpty(name) ? "default" : name;
            }
            catch (Exception)
            {
                return "default";
            }
        }

        private static string FilePath(string saveName)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) saveName = saveName.Replace(c, '_');
            string dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "SRMarketData");
            return Path.Combine(dir, saveName + ".prices");
        }
    }
}
