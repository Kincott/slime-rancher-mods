using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Top-left "143 FPS" in a small dark glass pill, averaged over half a second; text only rebuilt when the
    // number changes.
    internal class FpsLabel
    {
        private readonly TextMeshProUGUI text;
        private float elapsed;
        private int frames;
        private int shown = -1;

        internal FpsLabel(Transform canvas)
        {
            Image pill = Theme.Box("Fps", canvas, Theme.Panel, 1.5f);
            // Top-left: the top-right corner is taken by SRMP's multiplayer window.
            Theme.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(92f, 30f));
            Image border = Theme.Box("Border", pill.rectTransform, Theme.PanelBorder, 1.5f);
            Theme.Fill(border.rectTransform, -1f).SetAsFirstSibling();

            text = Theme.Text("Value", pill.rectTransform, 15f, Theme.TextPrimary, TextAlignmentOptions.Center);
            Theme.Fill(text.rectTransform, 0f);
            text.fontStyle = FontStyles.Bold;
        }

        internal void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < 0.5f) return;
            int fps = Mathf.RoundToInt(frames / elapsed);
            elapsed = 0f;
            frames = 0;
            if (fps == shown) return;
            shown = fps;
            text.text = fps + " <alpha=#99><size=12>FPS";
        }
    }
}
