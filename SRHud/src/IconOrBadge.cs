using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Shows, by priority:
    //   - a pair of overlapping icons (a largo: its two base slimes),
    //   - a single sprite,
    //   - a rounded badge with the name's first letter, tinted with the item's color,
    // so a missing (or placeholder) image never leaves an empty hole.
    internal class IconOrBadge
    {
        private readonly Image icon, pairBack, pairFront;
        private readonly Image badge;
        private readonly TextMeshProUGUI letter;

        internal readonly RectTransform Root;

        internal IconOrBadge(string name, Transform parent)
        {
            Root = Theme.Rect(name, parent);

            badge = Theme.Box("Badge", Root, Theme.SlotSelected, 1.5f);
            Theme.Fill(badge.rectTransform, 2f);
            letter = Theme.Text("Letter", badge.rectTransform, 20f, Theme.TextPrimary, TextAlignmentOptions.Center);
            Theme.Fill(letter.rectTransform, 0f);
            letter.fontStyle = FontStyles.Bold;

            icon = Theme.Icon("Icon", Root);
            Theme.Fill(icon.rectTransform, 0f);

            // Pair: each at 70% of the box, back one top-left, front one bottom-right.
            pairBack = Theme.Icon("PairBack", Root);
            Corner(pairBack.rectTransform, new Vector2(0f, 1f));
            pairFront = Theme.Icon("PairFront", Root);
            Corner(pairFront.rectTransform, new Vector2(1f, 0f));
            Show(false, false, false);
        }

        private static void Corner(RectTransform rt, Vector2 corner)
        {
            rt.anchorMin = corner;
            rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = Vector2.zero;
        }

        // sprite may be null; name and tint are only used for the badge.
        internal void Set(Sprite sprite, string name, Color tint)
        {
            if (sprite != null)
            {
                icon.sprite = sprite;
                Show(true, false, false);
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                Show(false, false, false);
                return;
            }
            letter.text = FirstLetter(name);
            // The item's color, darkened and translucent so the letter stays readable.
            Color c = tint.r + tint.g + tint.b < 0.15f ? Theme.Energy : tint;
            badge.color = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.85f);
            Show(false, false, true);
        }

        internal void SetPair(Sprite back, Sprite front)
        {
            float size = Root.rect.width > 0f ? Root.rect.width * 0.7f : 28f;
            pairBack.rectTransform.sizeDelta = new Vector2(size, size);
            pairFront.rectTransform.sizeDelta = new Vector2(size, size);
            pairBack.sprite = back;
            pairFront.sprite = front;
            Show(false, true, false);
        }

        internal void Clear()
        {
            Show(false, false, false);
        }

        private void Show(bool single, bool pair, bool letterBadge)
        {
            icon.enabled = single;
            pairBack.enabled = pair;
            pairFront.enabled = pair;
            badge.enabled = letterBadge;
            letter.enabled = letterBadge;
        }

        // First visible letter, skipping rich-text tags the game may put in names.
        private static string FirstLetter(string name)
        {
            bool inTag = false;
            foreach (char ch in name)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag && char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString();
            }
            return "?";
        }
    }
}
