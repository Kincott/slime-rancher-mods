using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // The "Press E to activate" prompt (ActivateUI) restyled in place, to match the dark glass HUD: a pill behind
    // smaller text without the heavy outline, the key shown as a keycap, sitting a bit below the crosshair instead
    // of across it. The game keeps showing and hiding the prompt (its GameObject), so nothing of its logic changes.
    internal static class ActivatePrompt
    {
        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, "Awake", "AfterAwake");
            Patch(harmony, "OnKeysChanged", "AfterKeysChanged");
        }

        private static void Patch(Harmony harmony, string method, string postfix)
        {
            var target = AccessTools.Method(typeof(ActivateUI), method);
            if (target == null)
            {
                Main.Log("ActivateUI." + method + " not found, activate prompt keeps its look");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(ActivatePrompt), postfix));
        }

        private static void AfterAwake(ActivateUI __instance)
        {
            try
            {
                if (__instance.normalPrompt != null && __instance.normalPrompt.GetComponent<PromptPill>() == null)
                {
                    __instance.normalPrompt.AddComponent<PromptPill>().Init(__instance.normalPromptText);
                }
            }
            catch (Exception e)
            {
                Main.Log("activate prompt styling failed: " + e.Message);
            }
        }

        // Rebuilds the sentence with the key as a keycap. The game has just written it as plain text with the key
        // inline; the key is recovered by building the same sentence around a marker, so this needs no access to
        // the input library.
        private static void AfterKeysChanged(ActivateUI __instance)
        {
            try
            {
                TMP_Text label = __instance.normalPromptText;
                if (label == null) return;
                MessageBundle ui = SRSingleton<GameContext>.Instance.MessageDirector.GetBundle("ui");
                const string Marker = "\u0001";
                string template = ui.Get(__instance.key, Marker);
                int at = template.IndexOf(Marker, StringComparison.Ordinal);
                if (at < 0) return;
                string before = template.Substring(0, at);
                string after = template.Substring(at + Marker.Length);
                string sentence = label.text;
                if (!sentence.StartsWith(before, StringComparison.Ordinal) || !sentence.EndsWith(after, StringComparison.Ordinal)
                    || sentence.Length < before.Length + after.Length) return;
                string key = sentence.Substring(before.Length, sentence.Length - before.Length - after.Length);
                string cap = "<mark=#ffffff2e padding=\"10,10,4,4\"><b><color=#4dc9f0> " + key + " </color></b></mark>";
                label.text = before + cap + after;
            }
            catch (Exception e)
            {
                Main.Log("activate prompt key styling failed: " + e.Message);
            }
        }
    }

    // The pill behind the prompt text, resized whenever the text changes.
    internal class PromptPill : MonoBehaviour
    {
        private const float PadX = 22f;
        private const float PadY = 10f;
        private const float BelowCrosshair = -70f;

        private TMP_Text text;
        private RectTransform pill;
        private string sizedFor;

        internal void Init(TMP_Text promptText)
        {
            text = promptText;
            if (text == null) return;

            // Smaller, without the vanilla outline (on a per-text material copy, so other texts keep theirs).
            text.fontSize = Mathf.Min(text.fontSize, 26f);
            text.enableAutoSizing = false;
            Material m = text.fontMaterial;
            if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            if (m.HasProperty(ShaderUtilities.ID_UnderlayColor)) m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0f));
            text.color = Theme.TextPrimary;

            Image bg = Theme.Box("SRHudPromptPill", text.transform.parent, Theme.Panel, 1f);
            pill = bg.rectTransform;
            pill.SetSiblingIndex(text.transform.GetSiblingIndex()); // drawn behind the text
            Image border = Theme.Box("Border", pill, Theme.PanelBorder, 1f);
            Theme.Fill(border.rectTransform, -1f).SetAsFirstSibling();

            // Move the whole prompt a little below the crosshair.
            var rt = transform as RectTransform;
            if (rt != null) rt.anchoredPosition += new Vector2(0f, BelowCrosshair);
        }

        private void LateUpdate()
        {
            if (text == null || pill == null || ReferenceEquals(text.text, sizedFor)) return;
            sizedFor = text.text;
            // Match the text's own placement, sized to the words (not to the text box, which spans the screen).
            // The text box may stretch across the screen, so the pill is placed at the box's center by local
            // position (anchors are irrelevant then) and sized from the words.
            Vector2 size = text.GetPreferredValues(text.text);
            RectTransform t = text.rectTransform;
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(size.x + 2f * PadX, size.y + 2f * PadY);
            pill.localPosition = t.localPosition + (Vector3)t.rect.center;
        }
    }
}
