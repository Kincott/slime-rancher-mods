using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SRHud
{
    // Makes the vanilla elements that the new HUD replaces invisible with a CanvasGroup (alpha 0, no raycasts).
    // They stay active, so their scripts keep running: pickup effects, magic water timer, anything else hooked
    // on them. Everything else of the vanilla HUD (mail, autosave icon, popups, tutorials, countdowns) is untouched.
    internal static class OriginalHud
    {
        internal static readonly string[] LooseElements =
            { "CurrencyIcon", "HealthIcon", "EnergyIcon", "RadIcon", "PartnerArea", "Targeting" };

        // Only the groups this mod hid, so Restore never touches elements the game hides on purpose.
        private static readonly List<CanvasGroup> hidden = new List<CanvasGroup>();

        internal static void Restore()
        {
            foreach (CanvasGroup g in hidden)
            {
                if (g == null) continue;
                g.alpha = 1f;
                g.blocksRaycasts = true;
                g.interactable = true;
            }
            hidden.Clear();
        }

        internal static void Hide(HudUI hud)
        {
            hidden.Clear();
            var targets = new List<GameObject>();

            AmmoSlotUI slots = hud.GetComponentInChildren<AmmoSlotUI>(true);
            if (slots != null) targets.Add(slots.gameObject);

            AddMeter(targets, hud.GetComponentInChildren<HealthMeter>(true), hud);
            AddMeter(targets, hud.energyMeter, hud);
            AddMeter(targets, hud.GetComponentInChildren<RadMeter>(true), hud);

            AddTextGroup(targets, hud.currencyText != null ? hud.currencyText.transform : null, hud);
            AddTextGroup(targets, hud.dayText != null ? hud.dayText.transform : null, hud);
            AddTextGroup(targets, hud.timeText != null ? hud.timeText.transform : null, hud);
            if (hud.timeIcon != null) targets.Add(hud.timeIcon.gameObject);
            AddTextGroup(targets, hud.keysText != null ? hud.keysText.transform : null, hud);
            if (hud.keysIcon != null) targets.Add(hud.keysIcon.gameObject);

            // Icons that sit loose in UIContainer next to their meter or text (seen in the dumped tree),
            // and the 7Zee partner level, which the new panel shows instead.
            foreach (string name in LooseElements)
            {
                Transform t = hud.uiContainer.transform.Find(name);
                if (t != null && !targets.Contains(t.gameObject)) targets.Add(t.gameObject);
            }

            // The coin popups ("+24" and a coin) spawn into their own container, outside UIContainer.
            PopupElementsUI popups = SRSingleton<PopupElementsUI>.Instance;
            if (popups != null && popups.container != null) targets.Add(popups.container.gameObject);

            foreach (GameObject go in targets)
            {
                CanvasGroup group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                hidden.Add(group);
            }
        }

        // A meter is usually grouped with its icon: hide the small group around it, never the HUD container.
        private static void AddMeter(List<GameObject> targets, Component meter, HudUI hud)
        {
            if (meter == null) return;
            Transform parent = meter.transform.parent;
            bool smallGroup = parent != null && parent != hud.uiContainer.transform && parent.childCount <= 3;
            targets.Add(smallGroup ? parent.gameObject : meter.gameObject);
        }

        // A HUD text and its icon usually share a parent; hide that parent when it is a small group.
        private static void AddTextGroup(List<GameObject> targets, Transform text, HudUI hud)
        {
            if (text == null) return;
            Transform parent = text.parent;
            bool smallGroup = parent != null && parent != hud.uiContainer.transform && parent.childCount <= 3;
            targets.Add(smallGroup ? parent.gameObject : text.gameObject);
        }

        // One-time dump of the vanilla HUD tree to the SRML log, to check what was hidden against what exists.
        internal static void Dump(HudUI hud)
        {
            var sb = new StringBuilder("vanilla HUD tree:\n");
            Walk(hud.transform, 0, sb);
            Main.Log(sb.ToString());
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 6) return;
            CanvasGroup g = t.GetComponent<CanvasGroup>();
            sb.Append(' ', depth * 2).Append(t.name)
              .Append(t.gameObject.activeSelf ? "" : " [inactive]")
              .Append(g != null && g.alpha == 0f ? " [hidden by SRHud]" : "")
              .Append('\n');
            for (int i = 0; i < t.childCount; i++)
            {
                Walk(t.GetChild(i), depth + 1, sb);
            }
        }
    }
}
