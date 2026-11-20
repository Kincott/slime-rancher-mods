using UnityEngine;

namespace SRBoost
{
    // GameContext.Awake hard-codes Application.targetFrameRate = 120, with no option to change it. Re-applying
    // the configured value every frame is cheap and also wins if anything resets it later.
    // (Unity ignores targetFrameRate while in-game V-Sync is on, so V-Sync should be off.)
    internal static class FrameRate
    {
        internal static void Tick()
        {
            int wanted = Main.Config.TargetFps <= 0 ? -1 : Main.Config.TargetFps;
            if (Application.targetFrameRate != wanted)
            {
                Application.targetFrameRate = wanted;
            }
        }
    }
}
