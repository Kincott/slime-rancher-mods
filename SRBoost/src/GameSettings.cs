namespace SRBoost
{
    // One-shot application of the best in-game options, through the same calls the options menu uses, so the
    // game stores them in the profile on its next save. Waits until a game is loaded (player present), which is
    // after the profile has been read; applying earlier would be overwritten by the profile load.
    internal static class GameSettings
    {
        internal static void Tick()
        {
            if (!Main.Config.ApplyBestGameSettings)
            {
                return;
            }
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            GameContext game = SRSingleton<GameContext>.Instance;
            if (scene == null || scene.Player == null || game == null || game.OptionsDirector == null)
            {
                return;
            }

            SRQualitySettings.CurrentLevel = SRQualitySettings.Level.VERY_HIGH; // all sub-options to their max
            game.OptionsDirector.SetVsync(false);

            Main.Config.ApplyBestGameSettings = false;
            Main.Config.Save();
            Main.Log("applied in-game graphics Very High with V-Sync off (saved by the game on its next save)");
        }
    }
}
