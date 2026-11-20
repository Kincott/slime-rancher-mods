using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // Every shop (vacpack upgrades, gadgets, ranch expansions...) is a PurchaseUI. Vanilla only greys out sold-out
    // entries and never shows whether the player can afford an item until the buy button is pressed. Each list
    // entry gets a status, and the buy button follows it:
    //
    //   ▌ Ouvre-capsule      [4 500]   orange bar, red price   -> available, not enough newbucks/materials
    //   ▌ Jetpack            [  350]   green bar, gold price   -> can buy now          (button: green)
    //     Réservoir d'eau  [Épuisé]    dimmed row              -> owned / sold out     (button: clearly disabled)
    internal static class ShopStatus
    {
        private const string StatusName = "SRHudStatus";

        private static readonly Color Buyable = new Color(0.36f, 0.85f, 0.50f, 1f);
        private static readonly Color TooExpensive = new Color(1f, 0.62f, 0.25f, 1f);
        private static readonly Color BuyIdle = new Color(0.20f, 0.60f, 0.37f, 1f);
        private static readonly Color BuyHover = new Color(0.26f, 0.72f, 0.45f, 1f);
        private static readonly Color BuyPressed = new Color(0.15f, 0.48f, 0.29f, 1f);
        private static readonly Color Inactive = new Color(0.20f, 0.22f, 0.27f, 0.55f);

        private enum State { Owned, Buyable, TooExpensive }

        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, "UpdateButton", "AfterUpdateButton");
            Patch(harmony, "SetupActivePurchaseButton", "AfterSetupPurchaseButton");
        }

        private static void Patch(Harmony harmony, string method, string postfix)
        {
            var target = AccessTools.Method(typeof(PurchaseUI), method);
            if (target == null)
            {
                Main.Log("PurchaseUI." + method + " not found, shop status disabled for it");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ShopStatus), postfix));
        }

        private static State StateOf(PurchaseUI.Purchasable p)
        {
            if (!p.avail()) return State.Owned;
            return CanAfford(p) ? State.Buyable : State.TooExpensive;
        }

        private static bool CanAfford(PurchaseUI.Purchasable p)
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.PlayerState == null) return true;
            if (p.cost > 0 && scene.PlayerState.GetCurrency() < p.cost) return false;
            if (p.craftCosts != null && scene.GadgetDirector != null)
            {
                foreach (GadgetDefinition.CraftCost c in p.craftCosts)
                {
                    if (scene.GadgetDirector.GetRefineryCount(c.id) < c.amount) return false;
                }
            }
            return true;
        }

        private static string soldOut;

        // The game's own translated "sold out" word (the one on the buy button), rather than a symbol the game
        // font may not contain.
        private static string SoldOutLabel()
        {
            if (soldOut != null) return soldOut;
            try
            {
                MessageBundle ui = SRSingleton<GameContext>.Instance.MessageDirector.GetBundle("ui");
                soldOut = ui != null ? ui.Xlate("b.sold_out") : null;
            }
            catch (Exception)
            {
                soldOut = null;
            }
            if (string.IsNullOrEmpty(soldOut) || soldOut.StartsWith("b.", StringComparison.Ordinal)) soldOut = "OK";
            return soldOut;
        }

        // ---- List entries ------------------------------------------------------------------------------------

        private static void AfterUpdateButton(PurchaseUI.Purchasable purchasable, GameObject buttonObj)
        {
            try
            {
                Decorate(purchasable, buttonObj);
            }
            catch (Exception e)
            {
                Main.Log("shop status failed for an entry: " + e.Message);
            }
        }

        private static void Decorate(PurchaseUI.Purchasable p, GameObject row)
        {
            State state = StateOf(p);
            Transform existing = Host(row).Find(StatusName);
            RectTransform status = existing != null ? (RectTransform)existing : Build(row);

            Image bar = status.Find("Bar").GetComponent<Image>();
            Image chip = status.Find("Chip").GetComponent<Image>();
            TextMeshProUGUI label = status.Find("Chip/Text").GetComponent<TextMeshProUGUI>();

            bar.enabled = state != State.Owned;
            bar.color = state == State.Buyable ? Buyable : TooExpensive;

            string price = p.cost > 0 ? p.cost.ToString("N0") : "";
            switch (state)
            {
                case State.Owned:
                    label.text = SoldOutLabel();
                    label.color = Theme.TextSecondary;
                    break;
                case State.Buyable:
                    label.text = price;
                    label.color = Theme.Gold;
                    break;
                default:
                    label.text = price;
                    label.color = TooExpensive;
                    break;
            }
            chip.enabled = label.text.Length > 0;
            label.enabled = chip.enabled;
            float w = label.text.Length > 0 ? label.GetPreferredValues(label.text).x + 14f : 0f;
            chip.rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, w), 22f);

            // Owned entries step back so the ones that matter stand out.
            CanvasGroup group = row.GetComponent<CanvasGroup>() ?? row.AddComponent<CanvasGroup>();
            group.alpha = state == State.Owned ? 0.55f : 1f;
        }

        // The visible body of a list entry: the button's background graphic. The entry's root object does not
        // always have the row's size (in the empty-plot menu it does not), which displaced or squashed the tags.
        private static Transform Host(GameObject row)
        {
            Selectable s = row.GetComponent<Selectable>();
            if (s != null && s.targetGraphic != null && s.targetGraphic.rectTransform.rect.height > 1f)
            {
                return s.targetGraphic.transform;
            }
            Transform content = row.transform.Find("Content");
            return content != null ? content : row.transform;
        }

        private static RectTransform Build(GameObject row)
        {
            RectTransform status = Theme.Rect(StatusName, Host(row));
            Theme.Fill(status, 0f);
            status.SetAsLastSibling();

            Image bar = Theme.Box("Bar", status, Buyable, 6f);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.offsetMin = new Vector2(3f, 12f);
            bar.rectTransform.offsetMax = new Vector2(7f, -12f);

            Image chip = Theme.Box("Chip", status, Theme.Panel, 2f);
            Theme.Place(chip.rectTransform, Vector2.one, new Vector2(-6f, -6f), new Vector2(60f, 22f));

            // Same font as the row's own name, so the chip matches the menu (the HUD font may not exist yet).
            TMP_Text name = row.GetComponentInChildren<TMP_Text>(true);
            if (name != null && Theme.Font == null) Theme.Font = name.font as TMP_FontAsset;
            TextMeshProUGUI text = Theme.Text("Text", chip.rectTransform, 14f, Theme.Gold, TextAlignmentOptions.Center);
            Theme.Fill(text.rectTransform, 0f);
            text.fontStyle = FontStyles.Bold;
            return status;
        }

        // ---- Buy button on the details panel -----------------------------------------------------------------

        private static void AfterSetupPurchaseButton(PurchaseUI __instance, PurchaseUI.Purchasable purchasable)
        {
            try
            {
                State state = StateOf(purchasable);
                Style(__instance.purchaseBtn, __instance.purchaseBtnText, state);
                Style(__instance.holdToPurchaseBtn, __instance.holdToPurchaseBtnText, state);
                if (__instance.selectedCost != null)
                {
                    __instance.selectedCost.color = state == State.TooExpensive ? TooExpensive : Theme.TextOnDark;
                }
            }
            catch (Exception e)
            {
                Main.Log("shop buy button styling failed: " + e.Message);
            }
        }

        private static void Style(Button button, TMP_Text text, State state)
        {
            if (button == null) return;
            ColorBlock cb = button.colors;
            if (state == State.Buyable)
            {
                cb.normalColor = BuyIdle;
                cb.highlightedColor = BuyHover;
                cb.selectedColor = BuyHover;
                cb.pressedColor = BuyPressed;
            }
            else
            {
                // Sold out: not interactable. Too expensive: still clickable (the game then explains why), but
                // it must not look like a purchase is possible.
                cb.normalColor = Inactive;
                cb.highlightedColor = Inactive;
                cb.selectedColor = Inactive;
                cb.pressedColor = Inactive;
            }
            cb.disabledColor = Inactive;
            cb.colorMultiplier = 1f;
            button.colors = cb;
            if (text != null)
            {
                text.color = state == State.Buyable ? Color.white : Theme.TextSecondary;
            }
        }
    }
}
