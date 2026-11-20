using Epic.OnlineServices;
using HarmonyLib;
using SRMultiplayer;
using SRMultiplayer.Networking;

namespace SRMPFixes
{
    // The host is also a player in SRMP, and some code sends it packets: every time the host loads a region,
    // Region_Unproxy calls NetworkRegion.AddPlayer(Globals.LocalPlayer), which serializes the whole actor list of
    // that region and sends it to the host itself. Epic refuses a send to yourself ("NoConnection"), so this is
    // pure wasted work plus two log lines per packet. Skipping sends addressed to the host changes nothing else.
    internal static class SelfSendFix
    {
        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(NetworkServer), "SendPacket");
            if (target == null)
            {
                Main.Log("NetworkServer.SendPacket not found, self-send fix disabled");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SelfSendFix), "Prefix"));
        }

        // Returning false skips the send.
        private static bool Prefix(ProductUserId targetUserId)
        {
            ProductUserId self;
            if (targetUserId == null || !Globals.PlayerToEpic.TryGetValue(Globals.LocalID, out self) || self == null)
            {
                return true;
            }
            return !(targetUserId == self);
        }
    }
}
