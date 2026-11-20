using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SRMarket
{
    // SRMarket.ini next to SRMarket.dll, created with defaults on first launch. "key = value", '#' for comments.
    internal class Config
    {
        public KeyCode PanelKey = KeyCode.F6;   // SRMP uses F3/F4
        public int HistoryDays = 14;
        public int ForecastDays = 3;
        public bool TileCharts = true;

        private const string FileName = "SRMarket.ini";

        private const string Template =
@"# SR Market settings. Restart the game after editing.

# Key that opens and closes the price panel (Unity key name: F6, F7, P, ...).
PanelKey = {0}
# Days of recorded history shown on the charts.
HistoryDays = {1}
# Days of forecast shown after today (prices if nobody sells).
ForecastDays = {2}
# Small chart on each tile of the Plort Market screen.
TileCharts = {3}
";

        internal static Config LoadOrCreate()
        {
            var c = new Config();
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, string.Format(CultureInfo.InvariantCulture, Template,
                        c.PanelKey, c.HistoryDays, c.ForecastDays, "true"));
                    return c;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#") || eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    int i;
                    bool b;
                    switch (key)
                    {
                        case "PanelKey":
                            try { c.PanelKey = (KeyCode)Enum.Parse(typeof(KeyCode), value, true); }
                            catch (ArgumentException) { }
                            break;
                        case "HistoryDays":
                            if (int.TryParse(value, out i)) c.HistoryDays = Mathf.Clamp(i, 2, 120);
                            break;
                        case "ForecastDays":
                            if (int.TryParse(value, out i)) c.ForecastDays = Mathf.Clamp(i, 0, 7);
                            break;
                        case "TileCharts":
                            if (bool.TryParse(value, out b)) c.TileCharts = b;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Main.Log("could not read " + FileName + ", using defaults: " + e.Message);
            }
            return c;
        }

        internal string Describe()
        {
            return "PanelKey=" + PanelKey + " HistoryDays=" + HistoryDays + " ForecastDays=" + ForecastDays + " TileCharts=" + TileCharts;
        }
    }
}
