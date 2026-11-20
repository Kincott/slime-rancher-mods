using SRML;

namespace SRMPFixes
{
    // SRML entry point. SRML calls Load once, then Update every frame.
    public class Main : ModEntryPoint
    {
        private static Main instance;

        public override void Load()
        {
            instance = this;
            GhostLargoFix.Apply(HarmonyInstance);
            OwnershipBroadcastFix.Apply(HarmonyInstance);
            SelfSendFix.Apply(HarmonyInstance);
            TeardownDestroyFix.Apply(HarmonyInstance);
            Log("loaded");
        }

        public override void Update()
        {
            TeardownDestroyFix.Tick();
            OrphanOwnershipFix.Tick();
            AchievementsFix.Tick();
        }

        // Goes to the in-game console and to SRML's log file, tagged with this mod's name.
        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null)
            {
                instance.ConsoleInstance.Log(message);
            }
        }
    }
}
