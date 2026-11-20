using SRML;
using UnityEngine;

namespace SRMarket
{
    // Plort price history and forecast. Records prices daily (PriceHistory), predicts the next days with the
    // game's own formula (PriceModel), and shows them on the market tiles and in a panel opened with a key.
    public class Main : ModEntryPoint
    {
        private static Main instance;
        internal static Config Config;

        private PricePanel panel;
        private SceneContext panelScene;

        public override void Load()
        {
            instance = this;
            Config = Config.LoadOrCreate();
            PriceHistory.Apply(HarmonyInstance);
            MarketTiles.Apply(HarmonyInstance);
            Log("loaded: " + Config.Describe());
        }

        public override void Update()
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.PlayerState == null) return; // main menu
            PriceHistory.Tick();

            // One panel per game scene (rebuilt after returning from the main menu).
            if (panel == null || panelScene != scene)
            {
                if (panel != null) panel.Destroy();
                if (Ui.Font == null && SRSingleton<HudUI>.Instance != null && SRSingleton<HudUI>.Instance.dayText != null)
                {
                    Ui.Font = SRSingleton<HudUI>.Instance.dayText.font as TMPro.TMP_FontAsset;
                }
                panel = new PricePanel();
                panelScene = scene;
            }
            if (Input.GetKeyDown(Config.PanelKey)) panel.Toggle();
            panel.Update();
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }
    }
}
