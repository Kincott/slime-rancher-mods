using SRMultiplayer;

namespace SRMPFixes
{
    // SRMP sets Globals.DisableAchievements when a session starts (host in NetworkServer, client in
    // SRMP.OnGameLoaded), and its patches then skip AchievementsDirector.Update and LateUpdate. Stats keep
    // counting during that time; only the awarding step is blocked. Clearing the flag lets the game award them
    // again, including the ones already reached while it was blocked (their checks are still queued).
    //
    // Note for the client: in multiplayer the client plays in the host's world, so world-based achievements
    // can unlock from the host's progress.
    internal static class AchievementsFix
    {
        private static bool logged;

        internal static void Tick()
        {
            if (!Globals.DisableAchievements)
            {
                return;
            }
            Globals.DisableAchievements = false;
            if (!logged)
            {
                logged = true;
                Main.Log("re-enabled achievements in multiplayer");
            }
        }
    }
}
