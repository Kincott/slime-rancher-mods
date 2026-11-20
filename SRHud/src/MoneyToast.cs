using TMPro;
using UnityEngine;

namespace SRHud
{
    // Replaces the vanilla "+24 [coin]" popups, which pile up on the left of the screen when selling quickly:
    //
    //    +96                <- one toast just above the money; changes within a short window are summed
    //   ┌────────────────
    //   │ HP ...
    //   │ ◎ 1 312 ...
    //
    // Driven by the player's currency itself (every gain or spend shows, whatever caused it), not by the vanilla
    // popups: hooking CoinsPopupUI.Init showed nothing in game. The vanilla popups still spawn (they play the
    // sale sound) but their container is hidden with the rest of the old HUD.
    internal class MoneyToast
    {
        private const float MergeWindow = 1.2f;   // changes closer than this add up in the same toast
        private const float FadeIn = 0.1f;
        private const float FadeOut = 0.35f;
        private const float Rise = 10f;

        private readonly PlayerState player;
        private readonly TextMeshProUGUI text;
        private readonly CanvasGroup group;
        private readonly RectTransform rt;
        private readonly Vector2 basePos;

        private int lastCurrency = int.MinValue;
        private int total;
        private float startedAt = -10f, lastAddAt = -10f;
        private bool shown, loggedFirst;

        internal MoneyToast(RectTransform vitalsPanel, PlayerState player)
        {
            this.player = player;
            text = Theme.Text("MoneyToast", vitalsPanel, 22f, Theme.Gold, TextAlignmentOptions.BottomLeft);
            rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 30f);
            basePos = new Vector2(16f, 6f);
            rt.anchoredPosition = basePos;
            text.fontStyle = FontStyles.Bold;
            group = text.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
        }

        private void Add(int amount)
        {
            float now = Time.unscaledTime;
            bool merge = shown && now - lastAddAt < MergeWindow && (total >= 0) == (amount >= 0);
            if (!merge)
            {
                total = 0;
                startedAt = now;
            }
            total += amount;
            lastAddAt = now;
            text.color = total >= 0 ? Theme.Gold : Theme.Health;
            text.text = (total >= 0 ? "+" : "") + total.ToString("N0");
            shown = true;
            if (!loggedFirst)
            {
                loggedFirst = true;
                Main.Log("money toast shown: " + text.text);
            }
        }

        internal void Update()
        {
            // The real balance (not the animated displayed one), so each sale is counted once, immediately.
            int currency = player.GetCurrency();
            if (lastCurrency == int.MinValue)
            {
                lastCurrency = currency; // first frame: the starting balance is not a gain
            }
            else if (currency != lastCurrency)
            {
                Add(currency - lastCurrency);
                lastCurrency = currency;
            }

            if (!shown) return;
            float now = Time.unscaledTime;
            float sinceStart = now - startedAt;
            float sinceLast = now - lastAddAt;

            // Done only once the fade-out has run its course. (Testing alpha <= 0 instead also matched the first
            // frame of the fade-in, where alpha is 0, and killed every toast as soon as it started.)
            if (sinceLast >= MergeWindow + FadeOut)
            {
                shown = false;
                group.alpha = 0f;
                return;
            }

            float alpha;
            if (sinceStart < FadeIn) alpha = sinceStart / FadeIn;
            else if (sinceLast < MergeWindow) alpha = 1f;
            else alpha = 1f - (sinceLast - MergeWindow) / FadeOut;
            group.alpha = Mathf.Clamp01(alpha);
            // Drifts up slightly while fading out.
            float lift = sinceLast > MergeWindow ? Rise * (sinceLast - MergeWindow) / FadeOut : 0f;
            rt.anchoredPosition = basePos + new Vector2(0f, lift);
        }
    }
}
