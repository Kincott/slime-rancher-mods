using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SRMarket
{
    // Adds a small chart (history then forecast) across the top of each tile of the Plort Market screen.
    // The tiles are built in MarketUI.Start and their prices refreshed in MarketUI.EconUpdate; both are followed.
    internal static class MarketTiles
    {
        private const string ChartName = "SRMarketChart";
        private static readonly FieldInfo amountMapField = AccessTools.Field(typeof(MarketUI), "amountMap");

        internal static void Apply(HarmonyLib.Harmony harmony)
        {
            Patch(harmony, "Start");
            Patch(harmony, "EconUpdate");
        }

        private static void Patch(HarmonyLib.Harmony harmony, string method)
        {
            var target = AccessTools.Method(typeof(MarketUI), method);
            if (target == null || amountMapField == null)
            {
                Main.Log("MarketUI." + method + " not found, no charts on the market screen");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MarketTiles), "Refresh"));
        }

        private static void Refresh(MarketUI __instance)
        {
            if (!Main.Config.TileCharts || !PriceModel.Ready) return;
            try
            {
                // Dictionary<MarketUI.PlortEntry, GameObject>, read as a plain IDictionary.
                var map = amountMapField.GetValue(__instance) as IDictionary;
                if (map == null) return;
                var plorts = PriceModel.Plorts();
                foreach (DictionaryEntry e in map)
                {
                    var entry = e.Key as MarketUI.PlortEntry;
                    var tile = e.Value as GameObject;
                    if (entry == null || tile == null) continue;
                    PriceModel.Plort plort = plorts.Find(p => p.Id == entry.id);
                    if (plort.Id != entry.id) continue;
                    ChartOf(tile).SetData(History(entry.id), PriceModel.Forecast(plort, Main.Config.ForecastDays));
                }
            }
            catch (Exception e)
            {
                Main.Log("market tile charts failed: " + e.Message);
            }
        }

        private static System.Collections.Generic.List<int> History(Identifiable.Id id)
        {
            var history = PriceHistory.Last(id, Main.Config.HistoryDays);
            int? today = PriceModel.Current(id);
            if (today.HasValue && (history.Count == 0 || history[history.Count - 1] != today.Value)) history.Add(today.Value);
            return history;
        }

        // A dark strip near the top of the tile, inside its frame, holding the chart. In game a bare line along the
        // tile's top edge merged with the frame and the faint forecast vanished on light tiles.
        private static Sparkline ChartOf(GameObject tile)
        {
            Transform existing = tile.transform.Find(ChartName + "/Line");
            if (existing != null) return existing.GetComponent<Sparkline>();

            var strip = Ui.Rect(ChartName, tile.transform);
            strip.anchorMin = new Vector2(0.16f, 0.74f);
            strip.anchorMax = new Vector2(0.84f, 0.92f);
            strip.offsetMin = Vector2.zero;
            strip.offsetMax = Vector2.zero;
            var backdrop = strip.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color(0.04f, 0.05f, 0.07f, 0.6f);
            backdrop.raycastTarget = false;

            Sparkline chart = Ui.Chart("Line", strip, Ui.Line, 2f);
            chart.RelativeThickness = 0.16f;
            chart.ForecastColor = new Color(1f, 1f, 1f, 0.7f);
            RectTransform rt = chart.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return chart;
        }
    }
}
