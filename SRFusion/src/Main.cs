using System;
using SRML;

namespace SRFusion
{
    // The fused slime: new ids and the fusion chamber upgrade in PreLoad (SRML only allows them there), the
    // prefabs in Load once the game's own prefabs exist.
    public class Main : ModEntryPoint
    {
        private static Main instance;

        public override void PreLoad()
        {
            instance = this;
            Content.RegisterIds();
            FusionChamber.Register();
            MarketPriceFit.Apply(HarmonyInstance);
            SRML.Console.Console.RegisterCommand(new FusionTestCommand());
        }

        public override void Load()
        {
            try
            {
                Content.Build();
            }
            catch (Exception e)
            {
                Log("building the fused slime and cosmic plort failed: " + e);
            }
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }
    }
}
