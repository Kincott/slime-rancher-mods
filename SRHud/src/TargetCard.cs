using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Card at the top center of the screen for what the player is aiming at, replacing the vanilla top-right text:
    //
    //        ┌──────────────────────────────────────┐
    //        │ [icon]  Largo Rocheux Boum            │
    //        │         Alimentation: Viande y        │   <- wraps to 2 lines rather than being cut
    //        │         Légumes                       │
    //        │  Hunger    ███████░░░░░               │   <- slimes and largos only
    //        │  Agitation ██░░░░░░░░░░               │
    //        └──────────────────────────────────────┘
    //
    // The card sizes itself to its text (between MinWidth and MaxWidth). Name and info are mirrored from the
    // vanilla TargetingUI texts, so every target type and translation stays correct; visibility follows the
    // vanilla one (including its 1 s hold) with a short fade.
    internal class TargetCard
    {
        private const float MinWidth = 300f;
        private const float MaxWidth = 560f;
        private const float Pad = 12f;
        private const float IconSize = 40f;
        private const float TextLeft = Pad + IconSize + 10f;
        private const float MeterHeight = 18f;
        private const float FadeSpeed = 10f;
        private const int MaxInfoLines = 2;

        private readonly TargetingUI vanilla;
        private readonly PlayerState player;
        private readonly VanillaLook look;

        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly IconOrBadge icon;
        private readonly TextMeshProUGUI title, info;
        private readonly RectTransform meters;
        private readonly Bar hunger, agitation;

        private string shownName, shownInfo;
        private GameObject shownTarget;
        private SlimeEmotions emotions;
        private float alpha;
        private readonly HashSet<string> logged = new HashSet<string>();

        internal TargetCard(Transform canvas, TargetingUI vanilla, PlayerState player, VanillaLook look)
        {
            this.vanilla = vanilla;
            this.player = player;
            this.look = look;

            Image panel = Theme.Box("Target", canvas, Theme.Panel, 1f);
            // Top center, above the crosshair; pivot at the top so the card grows downwards.
            root = Theme.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(MinWidth, 64f));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Image border = Theme.Box("Border", root, Theme.PanelBorder, 1f);
            Theme.Fill(border.rectTransform, -1f).SetAsFirstSibling();

            icon = new IconOrBadge("Icon", root);
            Theme.Place(icon.Root, new Vector2(0f, 1f), new Vector2(Pad, -Pad), new Vector2(IconSize, IconSize));

            title = Theme.Text("Name", root, 20f, Theme.TextPrimary, TextAlignmentOptions.TopLeft);
            TopLeft(title.rectTransform);
            title.fontStyle = FontStyles.Bold;
            title.overflowMode = TextOverflowModes.Ellipsis;

            info = Theme.Text("Info", root, 15f, Theme.TextSecondary, TextAlignmentOptions.TopLeft);
            TopLeft(info.rectTransform);
            info.enableWordWrapping = true;
            info.overflowMode = TextOverflowModes.Ellipsis;
            info.maxVisibleLines = MaxInfoLines;

            meters = Theme.Rect("Meters", root);
            meters.anchorMin = new Vector2(0f, 1f);
            meters.anchorMax = new Vector2(1f, 1f);
            meters.pivot = new Vector2(0.5f, 1f);
            meters.sizeDelta = new Vector2(-2f * Pad, 2f * MeterHeight);
            hunger = MakeMeter("Hunger", 0, Theme.Gold);
            agitation = MakeMeter("Agitation", 1, Theme.Health);
        }

        private static void TopLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        private Bar MakeMeter(string label, int index, Color color)
        {
            RectTransform row = Theme.Rect(label, meters);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, MeterHeight);
            row.anchoredPosition = new Vector2(0f, -index * MeterHeight);

            TextMeshProUGUI tag = Theme.Text("Label", row, 13f, Theme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            Theme.Place(tag.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80f, MeterHeight));
            tag.text = label;

            var bar = new Bar(row, color);
            bar.Root.anchorMin = new Vector2(0f, 0.5f);
            bar.Root.anchorMax = new Vector2(1f, 0.5f);
            bar.Root.offsetMin = new Vector2(84f, -3f);
            bar.Root.offsetMax = new Vector2(0f, 3f);
            return bar;
        }

        internal void Update()
        {
            bool visible = vanilla != null && vanilla.nameText != null && vanilla.nameText.enabled
                && (!string.IsNullOrEmpty(vanilla.nameText.text) || !string.IsNullOrEmpty(vanilla.infoText.text));

            if (visible)
            {
                bool changed = false;
                // The vanilla UI only rewrites its texts when the target changes: compare references, no allocation.
                if (!ReferenceEquals(vanilla.nameText.text, shownName) || !ReferenceEquals(vanilla.infoText.text, shownInfo))
                {
                    shownName = vanilla.nameText.text;
                    shownInfo = vanilla.infoText.text;
                    title.text = shownName ?? "";
                    info.text = shownInfo ?? "";
                    changed = true;
                }
                // Follow the target the vanilla UI recognized, not whatever is under the crosshair: during the
                // 1 s hold after looking away, the crosshair is on the ground and the card must keep the last
                // slime's icon and meters (following the crosshair showed a letter badge and dropped the meters).
                GameObject target = RecognizedTarget();
                if (target != null && target != shownTarget)
                {
                    SetTarget(target);
                    changed = true;
                }
                if (changed)
                {
                    Layout();
                }
                if (emotions != null)
                {
                    hunger.Set(emotions.GetCurr(SlimeEmotions.Emotion.HUNGER), 1f);
                    agitation.Set(emotions.GetCurr(SlimeEmotions.Emotion.AGITATION), 1f);
                }
            }

            float goal = visible ? 1f : 0f;
            if (!Mathf.Approximately(alpha, goal))
            {
                alpha = Mathf.MoveTowards(alpha, goal, FadeSpeed * Time.unscaledDeltaTime);
                group.alpha = alpha;
            }
        }

        // TargetingUI.currentTarget: set only for targets it shows info for, null while it merely holds the text.
        private static readonly FieldInfo currentTargetField = AccessTools.Field(typeof(TargetingUI), "currentTarget");

        private GameObject RecognizedTarget()
        {
            if (currentTargetField == null)
            {
                return player.Targeting; // field renamed in a game update: best effort
            }
            return currentTargetField.GetValue(vanilla) as GameObject;
        }

        // Width from the texts' natural size, info wrapped to at most two lines, everything stacked from the top.
        private void Layout()
        {
            float maxText = MaxWidth - TextLeft - Pad;

            bool hasTitle = title.text.Length > 0;
            Vector2 titleSize = hasTitle ? title.GetPreferredValues(title.text) : Vector2.zero;
            float titleW = Mathf.Min(titleSize.x, maxText);

            bool hasInfo = info.text.Length > 0;
            float infoW = hasInfo ? Mathf.Min(info.GetPreferredValues(info.text).x, maxText) : 0f;
            float infoH = hasInfo ? info.GetPreferredValues(info.text, infoW + 1f, 0f).y : 0f;
            infoH = Mathf.Min(infoH, MaxInfoLines * info.fontSize * 1.3f);

            float width = Mathf.Clamp(TextLeft + Mathf.Max(titleW, infoW) + Pad, MinWidth, MaxWidth);
            float textW = width - TextLeft - Pad;

            title.gameObject.SetActive(hasTitle);
            title.rectTransform.sizeDelta = new Vector2(textW, titleSize.y);
            title.rectTransform.anchoredPosition = new Vector2(TextLeft, -Pad + 1f);

            float infoTop = -Pad + 1f - (hasTitle ? titleSize.y + 1f : 0f);
            info.gameObject.SetActive(hasInfo);
            info.rectTransform.sizeDelta = new Vector2(textW, infoH);
            info.rectTransform.anchoredPosition = new Vector2(TextLeft, infoTop);

            float header = Mathf.Max(IconSize, -infoTop + infoH - Pad + 1f);
            bool showMeters = emotions != null;
            meters.gameObject.SetActive(showMeters);
            meters.anchoredPosition = new Vector2(0f, -Pad - header - 6f);

            float height = Pad + header + Pad + (showMeters ? 2f * MeterHeight + 6f : 0f);
            root.sizeDelta = new Vector2(width, height);
        }

        private void SetTarget(GameObject target)
        {
            shownTarget = target;
            Identifiable.Id id;
            Sprite sprite = look.TargetIcon(target, out id);
            Sprite original = sprite;
            Sprite first, second;
            if (look.LargoPair(id, out first, out second))
            {
                icon.SetPair(first, second);
            }
            else
            {
                // Placeholder art or no image at all: a letter badge from the shown name.
                if (VanillaLook.IsPlaceholder(sprite)) sprite = null;
                icon.Set(sprite, shownName, look.Color(id));
            }

            Identifiable ident = target.GetComponentInParent<Identifiable>();
            emotions = ident != null ? ident.GetComponent<SlimeEmotions>() : null;

            LogOnce(id, original);
        }

        // One line per target type, to check names, info and icons against what the game sends.
        private void LogOnce(Identifiable.Id id, Sprite sprite)
        {
            string key = id.ToString();
            if (logged.Count >= 60 || !logged.Add(key)) return;
            Main.Log("target " + key + ": name='" + shownName + "' info='" + shownInfo + "' icon='"
                + (sprite != null ? sprite.name : "none") + "'");
        }
    }
}
