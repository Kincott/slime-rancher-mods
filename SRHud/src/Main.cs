using SRML;
using UnityEngine;

namespace SRHud
{
    // Replaces the vanilla HUD (vitals, money, time, vacpack slots) with a redesigned one. The vanilla elements are
    // only made invisible, never disabled, so the game's own logic behind them keeps running.
    public class Main : ModEntryPoint
    {
        private static Main instance;
        internal static HudConfig Config;

        private HudRoot hud;

        public override void Load()
        {
            instance = this;
            Config = HudConfig.LoadOrCreate();
            if (Config.ThemeMenus)
            {
                MenuTheme.Apply(HarmonyInstance);
                ShopStatus.Apply(HarmonyInstance);
            }
            Log("loaded: " + Config.Describe());
        }

        public override void Update()
        {
            if (Config.ThemeMenus)
            {
                MenuTheme.TickStyleSheet();
            }
            if (!Config.Enabled)
            {
                return;
            }
            // The HUD lives in the game scene: build it once HudUI exists, rebuilt after each scene load.
            HudUI vanilla = SRSingleton<HudUI>.Instance;
            if (vanilla == null || SRSingleton<SceneContext>.Instance == null || SRSingleton<SceneContext>.Instance.PlayerState == null)
            {
                return;
            }
            if (hud == null)
            {
                GameObject go = new GameObject("SRHud");
                hud = go.AddComponent<HudRoot>();
                hud.Init(vanilla);
            }
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null)
            {
                instance.ConsoleInstance.Log(message);
            }
        }
    }
}
