using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRMarket
{
    // Overlay opened with a key (F6 by default), one row per discovered plort, most valuable first:
    //
    //   Prix des plortes                         Jour 23 · 5 jours d'historique
    //   [icon] Plorte boum     ╱╲_╱‾‾ ┄┄    124   +12    Vendre aujourd'hui
    //   [icon] Plorte rocheuse ‾‾╲__  ┄╱    48    -3     Attendre : J+2 +15 %
    //
    // The chart is the recorded history then the forecast (fainter). Advice compares today with the forecast.
    internal class PricePanel
    {
        private const float Width = 760f;
        private const float RowH = 34f;
        private const float Pad = 18f;
        private const float HeaderH = 86f;
        private const float RefreshSeconds = 2f;

        private readonly GameObject canvasGo;
        private readonly RectTransform panel, rows;
        private readonly TextMeshProUGUI title, subtitle;
        private readonly List<GameObject> rowObjects = new List<GameObject>();
        private float nextRefresh;

        internal bool Visible { get { return canvasGo.activeSelf; } }

        internal PricePanel()
        {
            canvasGo = new GameObject("SRMarketPanel");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            Image bg = Ui.Box("Panel", canvasGo.transform, Ui.Panel, 1f);
            panel = Ui.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 200f));
            Image border = Ui.Box("Border", panel, Ui.Border, 1f);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = new Vector2(-1f, -1f);
            border.rectTransform.offsetMax = new Vector2(1f, 1f);
            border.rectTransform.SetAsFirstSibling();

            // Title, then the subtitle on its own full-width line. Heights leave room for the font's line height:
            // with overflow "Ellipsis", TextMeshPro hides a line entirely when it does not fit vertically (the
            // title was invisible at 34 px for a 26 pt font).
            title = Ui.Label("Title", panel, 26f, Ui.Text, TextAlignmentOptions.TopLeft);
            Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -Pad + 4f), new Vector2(Width - 2f * Pad, 40f));
            title.fontStyle = FontStyles.Bold;
            title.overflowMode = TextOverflowModes.Overflow;
            title.text = "Prix des plortes";

            subtitle = Ui.Label("Subtitle", panel, 15f, Ui.Dim, TextAlignmentOptions.TopLeft);
            Ui.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(Pad, -Pad - 34f), new Vector2(Width - 2f * Pad, 26f));
            subtitle.overflowMode = TextOverflowModes.Overflow;

            rows = Ui.Rect("Rows", panel);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot = new Vector2(0.5f, 1f);
            rows.offsetMin = new Vector2(Pad, 0f);
            rows.offsetMax = new Vector2(-Pad, 0f);
            rows.anchoredPosition = new Vector2(0f, -HeaderH);

            canvasGo.SetActive(false);
        }

        internal void Toggle()
        {
            canvasGo.SetActive(!canvasGo.activeSelf);
            if (canvasGo.activeSelf)
            {
                nextRefresh = 0f;
                lastStamp = float.NaN; // always rebuild on open (the day may have changed)
            }
        }

        internal void Destroy()
        {
            // Usually already gone with its scene; Unity's == null covers that.
            if (canvasGo != null) UnityEngine.Object.Destroy(canvasGo);
        }

        internal void Update()
        {
            if (!canvasGo.activeSelf || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            // Each sale moves the saturation (so the forecast) and each reset moves the prices: rebuild only then,
            // not every tick (rebuilding allocates, and garbage collection is what makes this game stutter).
            float stamp = PriceModel.Stamp();
            if (stamp == lastStamp && rowObjects.Count > 0) return;
            lastStamp = stamp;
            Rebuild();
        }

        private float lastStamp = float.NaN;

        private void Rebuild()
        {
            foreach (GameObject go in rowObjects) UnityEngine.Object.Destroy(go);
            rowObjects.Clear();

            var plorts = new List<PriceModel.Plort>();
            foreach (PriceModel.Plort p in PriceModel.Plorts())
            {
                if (Discovered(p.Id) && PriceModel.Current(p.Id).HasValue) plorts.Add(p);
            }
            plorts.Sort((a, b) => PriceModel.Current(b.Id).Value.CompareTo(PriceModel.Current(a.Id).Value));

            for (int i = 0; i < plorts.Count; i++) BuildRow(plorts[i], i);

            panel.sizeDelta = new Vector2(Width, HeaderH + plorts.Count * RowH + Pad);
            subtitle.text = "Jour " + PriceModel.Today + "  ·  " + PriceHistory.Count + " jour(s) d'historique  ·  "
                + "<color=#ffffff59>prévision si personne ne vend</color>";
        }

        private void BuildRow(PriceModel.Plort p, int index)
        {
            Config cfg = Main.Config;
            int today = PriceModel.Current(p.Id).Value;
            List<int> history = PriceHistory.Last(p.Id, cfg.HistoryDays);
            if (history.Count == 0 || history[history.Count - 1] != today) history.Add(today);
            int[] forecast = PriceModel.Forecast(p, cfg.ForecastDays);
            int yesterday = history.Count >= 2 ? history[history.Count - 2] : today;

            RectTransform row = Ui.Rect("Row", rows);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowH - 2f);
            row.anchoredPosition = new Vector2(0f, -index * RowH);
            rowObjects.Add(row.gameObject);
            if (index % 2 == 0)
            {
                Image zebra = Ui.Box("Zebra", row, Ui.Row, 2f);
                zebra.rectTransform.anchorMin = Vector2.zero;
                zebra.rectTransform.anchorMax = Vector2.one;
                zebra.rectTransform.offsetMin = Vector2.zero;
                zebra.rectTransform.offsetMax = Vector2.zero;
            }

            float x = 6f;
            Image icon = Ui.Icon("Icon", row);
            Ui.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(26f, 26f));
            icon.sprite = IconOf(p.Id);
            icon.enabled = icon.sprite != null;
            x += 34f;

            TextMeshProUGUI name = Ui.Label("Name", row, 17f, Ui.Text, TextAlignmentOptions.MidlineLeft);
            Ui.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(190f, RowH));
            name.text = NameOf(p.Id);
            x += 198f;

            Sparkline chart = Ui.Chart("Chart", row, Ui.Line, 2f);
            Ui.Place(chart.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(150f, RowH - 10f));
            chart.SetData(history, forecast);
            x += 160f;

            TextMeshProUGUI price = Ui.Label("Price", row, 18f, Ui.Gold, TextAlignmentOptions.MidlineRight);
            Ui.Place(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(60f, RowH));
            price.fontStyle = FontStyles.Bold;
            price.text = today.ToString();
            x += 68f;

            int delta = today - yesterday;
            TextMeshProUGUI change = Ui.Label("Change", row, 15f, delta > 0 ? Ui.Up : (delta < 0 ? Ui.Down : Ui.Dim), TextAlignmentOptions.MidlineRight);
            Ui.Place(change.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(50f, RowH));
            change.text = delta > 0 ? "+" + delta : (delta < 0 ? delta.ToString() : "=");
            x += 62f;

            Color adviceColor;
            string advice = Advice(today, history, forecast, out adviceColor);
            TextMeshProUGUI hint = Ui.Label("Advice", row, 15f, adviceColor, TextAlignmentOptions.MidlineLeft);
            Ui.Place(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(Width - 2f * Pad - x - 6f, RowH));
            hint.text = advice;
        }

        // Compares today with the coming days (if nobody sells) and with the recorded history.
        private static string Advice(int today, List<int> history, int[] forecast, out Color color)
        {
            int best = today, bestDay = 0;
            for (int i = 0; i < forecast.Length; i++)
            {
                if (forecast[i] > best) { best = forecast[i]; bestDay = i + 1; }
            }
            if (bestDay > 0 && best >= today * 1.08f)
            {
                color = Ui.Wait;
                int pct = Mathf.RoundToInt((best - today) * 100f / Math.Max(1, today));
                return "Attendre : J+" + bestDay + " +" + pct + " %";
            }
            int max = today;
            foreach (int h in history) if (h > max) max = h;
            if (history.Count >= 3 && today >= max)
            {
                color = Ui.Up;
                return "Prix au plus haut";
            }
            if (forecast.Length > 0 && forecast[0] < today * 0.95f)
            {
                color = Ui.Up;
                return "Vendre aujourd'hui";
            }
            color = Ui.Dim;
            return "";
        }

        // A plort counts as discovered once its slime has a Slimepedia entry unlocked (X_PLORT -> X_SLIME).
        private static bool Discovered(Identifiable.Id plort)
        {
            try
            {
                PediaDirector pedia = SRSingleton<SceneContext>.Instance.PediaDirector;
                string slimeName = plort.ToString().Replace("_PLORT", "_SLIME");
                if (!Enum.IsDefined(typeof(Identifiable.Id), slimeName)) return true;
                PediaDirector.Id? entry = pedia.GetPediaId((Identifiable.Id)Enum.Parse(typeof(Identifiable.Id), slimeName));
                return !entry.HasValue || pedia.IsUnlocked(entry.Value);
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal static Sprite IconOf(Identifiable.Id id)
        {
            try
            {
                return SRSingleton<GameContext>.Instance.LookupDirector.GetIcon(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NameOf(Identifiable.Id id)
        {
            try
            {
                return Identifiable.GetName(id, false);
            }
            catch (Exception)
            {
                return id.ToString();
            }
        }
    }
}
