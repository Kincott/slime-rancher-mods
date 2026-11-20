using SRML;

namespace SRCorrals
{
    // Extra plot upgrades for corrals, gardens and coops (see Upgrades for the list). New upgrades must be
    // registered in PreLoad.
    public class Main : ModEntryPoint
    {
        private static Main instance;

        public override void PreLoad()
        {
            instance = this;
            Upgrades.Register();
            CorralEffects.Apply(HarmonyInstance);
            CoopUpgrader.Apply(HarmonyInstance);
        }

        public override void Load()
        {
            Log("loaded: 17 upgrades (corral 8, garden 5, coop 4)");
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }
    }
}
