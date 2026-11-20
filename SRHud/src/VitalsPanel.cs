using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Bottom-left dark glass panel:
    //   HP   ████████████░░  180      (rows: health, energy, radiation only when irradiated)
    //   EN   ██████████████  200
    //   ─────────────────────────
    //   ◎ 869        ☀ Jour 22 09:44   🔑 1   7Z 1
    // The right side of the info line flows right to left from the texts' real widths, so nothing overlaps
    // whatever the language or the numbers.
    internal class VitalsPanel
    {
        private const float Width = 380f;
        private const float RowHeight = 30f;
        private const float Pad = 14f;
        private const float InfoHeight = 34f;
        private const float IconSize = 18f;
        private const float IconGap = 5f;
        private const float ChipGap = 14f;

        private readonly RectTransform root;
        internal RectTransform Root { get { return root; } }
        private readonly Row health, energy, rad;
        private readonly TextMeshProUGUI money;
        private readonly Chip clock, keys, partner;

        private readonly HudUI vanilla;
        private readonly PlayerState player;
        private readonly TimeDirector time;
        private readonly StatusBar radBar;
        private bool radShown = true;
        private int lastMoney = -1, lastKeys = -1, lastTime = -1;
        private string lastPartner;
        private bool layoutDirty = true;

        private class Row
        {
            internal RectTransform Root;
            internal Bar Bar;
            internal TextMeshProUGUI Value;
            internal int Last = -1, LastMax = -1;
        }

        // An icon followed by a text, placed as one unit.
        private class Chip
        {
            internal Image Icon;
            internal TextMeshProUGUI Text;
            internal bool Visible = true;
        }

        internal VitalsPanel(Transform canvas, HudUI vanilla, PlayerState player, TimeDirector time)
        {
            this.vanilla = vanilla;
            this.player = player;
            this.time = time;
            RadMeter radMeter = vanilla.GetComponentInChildren<RadMeter>(true);
            radBar = radMeter != null ? radMeter.GetComponent<StatusBar>() : null;

            Image panel = Theme.Box("Vitals", canvas, Theme.Panel, 1f);
            root = Theme.Place(panel.rectTransform, Vector2.zero, new Vector2(28f, 28f), new Vector2(Width, 10f));
            Image border = Theme.Box("Border", root, Theme.PanelBorder, 1f);
            Theme.Fill(border.rectTransform, -1f).SetAsFirstSibling();

            health = MakeRow("HP", Theme.Health);
            energy = MakeRow("EN", Theme.Energy);
            rad = MakeRow("RAD", Theme.Rad);

            RectTransform info = Theme.Rect("Info", root);
            info.anchorMin = new Vector2(0f, 0f);
            info.anchorMax = new Vector2(1f, 0f);
            info.pivot = new Vector2(0.5f, 0f);
            info.sizeDelta = new Vector2(-2f * Pad, InfoHeight);
            info.anchoredPosition = new Vector2(0f, 8f);

            Image divider = Theme.Box("Divider", info, Theme.PanelBorder, 20f);
            divider.rectTransform.anchorMin = new Vector2(0f, 1f);
            divider.rectTransform.anchorMax = new Vector2(1f, 1f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 1f);
            divider.rectTransform.anchoredPosition = new Vector2(0f, 2f);

            // Left: money, with the game's own coin icon.
            Image coin = Theme.Icon("MoneyIcon", info);
            Theme.Place(coin.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            coin.sprite = VanillaSprite("CurrencyIcon");
            coin.enabled = coin.sprite != null;
            money = Theme.Text("Money", info, 19f, Theme.Gold, TextAlignmentOptions.MidlineLeft);
            Theme.Place(money.rectTransform, new Vector2(0f, 0.5f), new Vector2(coin.enabled ? 28f : 0f, 0f), new Vector2(120f, InfoHeight));
            money.fontStyle = FontStyles.Bold;

            // Right, flowing leftwards: 7Zee partner level, keys, clock.
            partner = MakeChip(info, "Partner", VanillaSprite("PartnerArea/PartnerIcon"), Theme.TextPrimary);
            keys = MakeChip(info, "Keys", vanilla.keysIcon != null ? vanilla.keysIcon.sprite : null, Theme.TextPrimary);
            clock = MakeChip(info, "Clock", null, Theme.TextPrimary);

            LayoutRows();
        }

        private Sprite VanillaSprite(string path)
        {
            Transform t = vanilla.uiContainer.transform.Find(path);
            Image img = t != null ? t.GetComponent<Image>() : null;
            return img != null ? img.sprite : null;
        }

        private Chip MakeChip(Transform parent, string name, Sprite sprite, Color color)
        {
            var c = new Chip();
            c.Icon = Theme.Icon(name + "Icon", parent);
            Theme.Place(c.Icon.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
            c.Icon.sprite = sprite;
            c.Text = Theme.Text(name, parent, 17f, color, TextAlignmentOptions.MidlineRight);
            Theme.Place(c.Text.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(10f, InfoHeight));
            return c;
        }

        private Row MakeRow(string label, Color color)
        {
            var row = new Row();
            row.Root = Theme.Rect(label + "Row", root);
            row.Root.anchorMin = new Vector2(0f, 1f);
            row.Root.anchorMax = new Vector2(1f, 1f);
            row.Root.pivot = new Vector2(0.5f, 1f);
            row.Root.sizeDelta = new Vector2(-2f * Pad, RowHeight);

            TextMeshProUGUI tag = Theme.Text("Label", row.Root, 13f, color, TextAlignmentOptions.MidlineLeft);
            Theme.Place(tag.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, RowHeight));
            tag.fontStyle = FontStyles.Bold;
            tag.characterSpacing = 4f;
            tag.text = label;

            row.Bar = new Bar(row.Root, color);
            row.Bar.Root.anchorMin = new Vector2(0f, 0.5f);
            row.Bar.Root.anchorMax = new Vector2(1f, 0.5f);
            row.Bar.Root.offsetMin = new Vector2(44f, -4f);
            row.Bar.Root.offsetMax = new Vector2(-58f, 4f);

            row.Value = Theme.Text("Value", row.Root, 18f, Theme.TextPrimary, TextAlignmentOptions.MidlineRight);
            Theme.Place(row.Value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(54f, RowHeight));
            row.Value.fontStyle = FontStyles.Bold;
            return row;
        }

        // Rows stack from the top; the panel grows or shrinks when the radiation row appears or goes.
        private void LayoutRows()
        {
            float y = -Pad + 2f;
            foreach (Row row in new[] { health, energy, rad })
            {
                bool visible = row != rad || radShown;
                row.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                row.Root.anchoredPosition = new Vector2(0f, y);
                y -= RowHeight;
            }
            root.sizeDelta = new Vector2(Width, -y + InfoHeight + 16f);
        }

        // Places the visible chips from the right edge leftwards, each sized to its text.
        private void LayoutChips()
        {
            float x = 0f;
            foreach (Chip c in new[] { partner, keys, clock })
            {
                c.Icon.gameObject.SetActive(c.Visible && c.Icon.sprite != null);
                c.Text.gameObject.SetActive(c.Visible);
                if (!c.Visible) continue;
                float w = c.Text.preferredWidth;
                c.Text.rectTransform.sizeDelta = new Vector2(w, InfoHeight);
                c.Text.rectTransform.anchoredPosition = new Vector2(x, 0f);
                x -= w;
                if (c.Icon.sprite != null)
                {
                    x -= IconGap;
                    c.Icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
                    x -= IconSize;
                }
                x -= ChipGap;
            }
            layoutDirty = false;
        }

        internal void Update()
        {
            SetRow(health, player.GetCurrHealth(), player.GetMaxHealth());
            SetRow(energy, player.GetCurrEnergy(), player.GetMaxEnergy());

            int currRad = player.GetCurrRad();
            bool showRad = currRad > 0;
            if (showRad != radShown)
            {
                radShown = showRad;
                LayoutRows();
            }
            if (showRad)
            {
                SetRow(rad, currRad, radBar != null ? Mathf.RoundToInt(radBar.maxValue) : 100);
            }

            int m = player.GetDisplayedCurrency();
            if (m != lastMoney)
            {
                lastMoney = m;
                money.text = m.ToString("N0");
            }

            int k = player.GetKeys();
            if (k != lastKeys)
            {
                lastKeys = k;
                keys.Text.text = k.ToString();
                keys.Visible = k > 0;
                layoutDirty = true;
            }

            // The game shows the partner level only once the player joined the 7Zee club.
            bool partnerOn = vanilla.partnerArea != null && vanilla.partnerArea.activeSelf && vanilla.partnerLevelText != null;
            string p = partnerOn ? vanilla.partnerLevelText.text : null;
            if (p != lastPartner)
            {
                lastPartner = p;
                partner.Text.text = p ?? "";
                partner.Visible = partnerOn;
                layoutDirty = true;
            }

            int t = time.CurrTime();
            if (t != lastTime)
            {
                lastTime = t;
                clock.Text.text = time.CurrDayString() + "  <alpha=#99>" + time.CurrTimeString();
                clock.Icon.sprite = time.CurrTimeIcon();
                layoutDirty = true;
            }

            if (layoutDirty)
            {
                LayoutChips();
            }
        }

        private static void SetRow(Row row, int value, int max)
        {
            if (value == row.Last && max == row.LastMax) return;
            row.Last = value;
            row.LastMax = max;
            row.Bar.Set(value, max);
            row.Value.text = value.ToString();
        }
    }
}
