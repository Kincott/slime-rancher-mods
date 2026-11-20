using SRML.Console;

namespace SRSeasons
{
    // Console command (Ctrl+Tab) to preview a season without waiting for its day:
    //   season hiver|printemps|ete|automne   forces that season (colors, snow, garden speed, prices, HUD pill)
    //   season auto                          back to the season of the current day
    // Local to the player who types it and not saved: restarting the game also returns to "auto".
    internal class SeasonCommand : ConsoleCommand
    {
        public override string ID { get { return "season"; } }
        public override string Usage { get { return "season <printemps|ete|automne|hiver|auto>"; } }
        public override string Description { get { return "Force a season to preview it (local, not saved); auto = follow the day"; } }

        public override bool Execute(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                Main.Log("usage: " + Usage);
                return false;
            }
            switch (args[0].ToLowerInvariant())
            {
                case "printemps": case "spring": Season.Forced = SeasonId.Spring; break;
                case "ete": case "été": case "summer": Season.Forced = SeasonId.Summer; break;
                case "automne": case "autumn": case "fall": Season.Forced = SeasonId.Autumn; break;
                case "hiver": case "winter": Season.Forced = SeasonId.Winter; break;
                case "auto": Season.Forced = null; break;
                default:
                    Main.Log("unknown season, usage: " + Usage);
                    return false;
            }
            Main.Log(Season.Forced.HasValue ? "season forced: " + Season.Name(Season.Forced.Value) : "season follows the day again");
            return true;
        }
    }
}
