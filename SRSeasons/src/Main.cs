using SRML;
using UnityEngine;

namespace SRSeasons
{
    // Seasons (see Season): world colors (SeasonLook), snowfall in winter (Snow), garden growth and plort prices
    // (GameplayPatches), and a HUD pill (SeasonPill). The scene objects are created once per game scene.
    public class Main : ModEntryPoint
    {
        private static Main instance;
        private GameObject sceneObjects;

        public override void PreLoad()
        {
            instance = this;
            GameplayPatches.Apply(HarmonyInstance);
            SeasonLook.Apply(HarmonyInstance);
            SRML.Console.Console.RegisterCommand(new SeasonCommand());
            SRML.Console.Console.RegisterCommand(new SeasonDumpCommand());
        }

        public override void Load()
        {
            Log("loaded: " + Season.DaysPerSeason + " days per season");
        }

        public override void Update()
        {
            HudUI hud = SRSingleton<HudUI>.Instance;
            if (hud == null || sceneObjects != null) return; // main menu, or already built for this scene
            sceneObjects = new GameObject("SRSeasons");
            sceneObjects.AddComponent<SeasonPill>().Init(hud);
            sceneObjects.AddComponent<SeasonWorld>();
            new GameObject("SRSeasonsSnow").AddComponent<Snow>().transform.SetParent(sceneObjects.transform, false);
            Log("season: " + Season.Name(GameplayPatches.CurrentSeason()));
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }
    }
}
