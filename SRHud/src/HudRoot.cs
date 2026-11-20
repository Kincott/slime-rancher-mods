using System;
using UnityEngine;
using UnityEngine.UI;

namespace SRHud
{
    // The new HUD's canvas. Created in the game scene (so it goes away with it), it follows the vanilla HUD's
    // visibility: hidden whenever the game hides its own HUD (menus, map, Slimepedia, cutscenes).
    internal class HudRoot : MonoBehaviour
    {
        private HudUI vanilla;
        private CanvasGroup group;
        private VitalsPanel vitals;
        private SlotsPanel slots;
        private TargetCard target;
        private MoneyToast toast;
        private FpsLabel fps;
        private bool failed;

        internal void Init(HudUI vanillaHud)
        {
            vanilla = vanillaHud;
            try
            {
                Theme.Font = vanilla.dayText != null ? vanilla.dayText.font : null;

                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 0;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // A smaller reference resolution makes everything bigger: that is how Scale works.
                scaler.referenceResolution = new Vector2(1920f, 1080f) / Main.Config.Scale;
                scaler.matchWidthOrHeight = 1f;
                group = gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                PlayerState player = SRSingleton<SceneContext>.Instance.PlayerState;
                TimeDirector time = SRSingleton<SceneContext>.Instance.TimeDirector;
                var look = new VanillaLook(vanilla);
                vitals = new VitalsPanel(transform, vanilla, player, time);
                toast = new MoneyToast(vitals.Root, player);
                slots = new SlotsPanel(transform, look, player, time);
                TargetingUI targeting = vanilla.GetComponentInChildren<TargetingUI>(true);
                if (targeting != null)
                {
                    target = new TargetCard(transform, targeting, player, look);
                }
                if (Main.Config.ShowFps)
                {
                    fps = new FpsLabel(transform);
                }

                OriginalHud.Hide(vanilla);
                OriginalHud.Dump(vanilla);
                Main.Log("new HUD built");
            }
            catch (Exception e)
            {
                Fail("building the HUD failed, keeping the original one: " + e);
            }
        }

        private void Update()
        {
            if (failed) return;
            if (vanilla == null)
            {
                Destroy(gameObject);
                return;
            }

            bool visible = vanilla.uiContainer != null && vanilla.uiContainer.activeInHierarchy;
            group.alpha = visible ? Main.Config.Opacity : 0f;
            if (!visible) return;

            try
            {
                vitals.Update();
                slots.Update();
                toast.Update();
                if (target != null) target.Update();
                if (fps != null) fps.Update();
            }
            catch (Exception e)
            {
                Fail("updating the HUD failed, restoring the original one: " + e);
            }
        }

        // Never leave the player without a HUD: drop ours and bring the vanilla one back.
        private void Fail(string message)
        {
            failed = true;
            Main.Log(message);
            OriginalHud.Restore();
            if (group != null) group.alpha = 0f;
        }
    }
}
