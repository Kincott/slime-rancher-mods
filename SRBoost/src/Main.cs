using SRML;

namespace SRBoost
{
    // Graphics and frame-rate tweaks for Slime Rancher. Every value is read from SRBoost.ini next to the DLL,
    // so each player can tune it for their own PC (SRMP only requires both players to have the mod installed).
    public class Main : ModEntryPoint
    {
        private static Main instance;
        internal static BoostConfig Config;

        public override void Load()
        {
            instance = this;
            Config = BoostConfig.LoadOrCreate();
            QualityBoost.Apply(HarmonyInstance);
            SmartFeeder.Apply(HarmonyInstance);
            Log("loaded: " + Config.Describe());
        }

        public override void Update()
        {
            GameSettings.Tick();
            FrameRate.Tick();
            PerfMonitor.Tick();
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
