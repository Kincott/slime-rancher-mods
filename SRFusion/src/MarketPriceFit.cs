using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SRFusion
{
    // The Plort Market tiles were made for prices of at most 3 digits. The cosmic plort sells for 4 digits,
    // and the first digit ended up hidden (1 203 read as "203"). After each price refresh, any tile whose price
    // has more than 3 digits switches its text to best fit, so the number shrinks to fit its frame.
    internal static class MarketPriceFit
    {
        private static readonly FieldInfo amountMapField = AccessTools.Field(typeof(MarketUI), "amountMap");

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(MarketUI), "EconUpdate");
            if (target == null || amountMapField == null)
            {
                Main.Log("MarketUI.EconUpdate not found: 4-digit prices may be cut on the market screen");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(MarketPriceFit), "AfterEconUpdate"));
        }

        private static void AfterEconUpdate(MarketUI __instance)
        {
            try
            {
                var map = amountMapField.GetValue(__instance) as IDictionary;
                if (map == null) return;
                foreach (DictionaryEntry e in map)
                {
                    var tile = e.Value as GameObject;
                    PriceEntry entry = tile != null ? tile.GetComponent<PriceEntry>() : null;
                    if (entry == null || entry.amountText == null) continue;
                    Fit(entry.amountText);
                }
            }
            catch (Exception ex)
            {
                Main.Log("market price fit failed: " + ex.Message);
            }
        }

        private static void Fit(Text text)
        {
            bool isLong = text.text != null && text.text.Length > 3;
            if (isLong && !text.resizeTextForBestFit)
            {
                text.resizeTextMaxSize = text.fontSize;
                text.resizeTextMinSize = Mathf.Max(8, text.fontSize / 2);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.resizeTextForBestFit = true;
            }
        }
    }
}
