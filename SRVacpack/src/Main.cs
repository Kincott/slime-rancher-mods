using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using SRML;

namespace SRVacpack
{
    // Vacpack changes, each with a setting in SRVacpack.ini:
    //   - Largos: the vacpack only stores objects of "normal" size; largos are "large", so they could only be held
    //     in the beam. They are made normal-size and allowed in the player's vacpack, at most LargosPerSlot per
    //     slot (LargoSlotCap).
    //   - Spill: when an item's slot is full, it goes into another slot already holding it (if not full) or into
    //     an empty slot, instead of being refused. Largos too, once their slot reaches LargosPerSlot.
    public class Main : ModEntryPoint
    {
        private static Main instance;
        internal static bool LargosInVacpack = true;
        internal static int LargosPerSlot = 3;
        internal static bool SpillToEmptySlot = true;

        public override void PreLoad()
        {
            instance = this;
            LoadConfig();
            if (LargosInVacpack) Largos.AllowInVacpack();
            if (SpillToEmptySlot) Spill.Apply(HarmonyInstance);
        }

        public override void Load()
        {
            if (LargosInVacpack) Largos.MakeNormalSize();
            Log("loaded: LargosInVacpack=" + LargosInVacpack + " LargosPerSlot=" + LargosPerSlot + " SpillToEmptySlot=" + SpillToEmptySlot);
        }

        public override void Update()
        {
            if (LargosInVacpack) LargoSlotCap.Tick();
        }

        internal static void Log(string message)
        {
            if (instance != null && instance.ConsoleInstance != null) instance.ConsoleInstance.Log(message);
        }

        private const string FileName = "SRVacpack.ini";

        private static void LoadConfig()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path,
                        "# SR Vacpack settings. Restart the game after editing.\n\n" +
                        "# Largos can be vacuumed and stored in the vacpack like any slime.\nLargosInVacpack = true\n" +
                        "# Most largos in one vacpack slot (more go to another slot).\nLargosPerSlot = 3\n" +
                        "# When an item's slot is full, keep vacuuming it into an empty slot.\nSpillToEmptySlot = true\n");
                    return;
                }
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line.StartsWith("#") || eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string text = line.Substring(eq + 1).Trim();
                    bool flag;
                    int number;
                    if (key == "LargosInVacpack" && bool.TryParse(text, out flag)) LargosInVacpack = flag;
                    else if (key == "SpillToEmptySlot" && bool.TryParse(text, out flag)) SpillToEmptySlot = flag;
                    else if (key == "LargosPerSlot" && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) LargosPerSlot = Math.Max(1, number);
                }
            }
            catch (Exception e)
            {
                Log("could not read " + FileName + ", using defaults: " + e.Message);
            }
        }
    }
}
