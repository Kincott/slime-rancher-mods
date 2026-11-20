using HarmonyLib;
using Lidgren.Network;
using SRMultiplayer;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;

namespace SRMPFixes
{
    // Host-side replacement for NetworkHandlerServer.OnRegionOwner / OnActorOwner, same logic with two fixes.
    //
    // 1. Stale "nobody" broadcast. When a client gives up a region or actor, SRMP calls SetOwnership(0); if the
    //    host has it loaded, SetOwnership re-takes it and broadcasts "owner = host". SRMP then broadcasts the
    //    client's original "owner = 0" packet on top, so clients end up believing nobody owns something the
    //    host owns, and the next client to load it grabs it: brief double simulation and ownership ping-pong.
    //    Here the "0" is only broadcast when it is still true.
    // 2. Ownership packets were re-broadcast with the default Unreliable delivery and could be lost; they now use
    //    ReliableOrdered, like SRMP's own TakeOwnership.
    internal static class OwnershipBroadcastFix
    {
        private const byte NoOwner = 0;
        private const NetDeliveryMethod Reliable = NetDeliveryMethod.ReliableOrdered;

        internal static void Apply(Harmony harmony)
        {
            Patch(harmony, "OnRegionOwner", "RegionOwnerPrefix");
            Patch(harmony, "OnActorOwner", "ActorOwnerPrefix");
        }

        private static void Patch(Harmony harmony, string original, string prefix)
        {
            var target = AccessTools.Method(typeof(NetworkHandlerServer), original);
            if (target == null)
            {
                Main.Log("NetworkHandlerServer." + original + " not found, ownership broadcast fix disabled for it");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(OwnershipBroadcastFix), prefix));
        }

        // Returning false skips SRMP's original handler.
        private static bool RegionOwnerPrefix(PacketRegionOwner packet, NetworkPlayer player)
        {
            NetworkRegion region;
            if (!Globals.Regions.TryGetValue(packet.ID, out region))
            {
                return false;
            }

            if (packet.Owner == NoOwner && region.Owner == player.ID)
            {
                region.SetOwnership(NoOwner); // may re-take it for the host, which broadcasts that itself
                if (region.Owner == NoOwner)
                {
                    packet.SendToAll(Reliable);
                }
            }
            else if (packet.Owner != NoOwner && region.Owner == NoOwner)
            {
                region.SetOwnership(packet.Owner);
                packet.SendToAll(Reliable);
            }
            else
            {
                // Request refused: tell the sender who really owns it.
                packet.Owner = region.Owner;
                packet.Send(player, Reliable);
            }
            return false;
        }

        private static bool ActorOwnerPrefix(PacketActorOwner packet, NetworkPlayer player)
        {
            NetworkActor actor;
            if (!Globals.Actors.TryGetValue(packet.ID, out actor) || actor == null)
            {
                return false;
            }

            if (packet.Owner == NoOwner && actor.Owner == player.ID)
            {
                actor.SetOwnership(NoOwner); // may re-take it for the host, which broadcasts that itself
                if (actor.Owner == NoOwner)
                {
                    packet.SendToAll(Reliable);
                }
            }
            else if (packet.Owner != NoOwner)
            {
                // SRMP grants every actor ownership request; unchanged.
                actor.SetOwnership(packet.Owner);
                packet.SendToAll(Reliable);
            }
            else
            {
                packet.Owner = actor.Owner;
                packet.Send(player, Reliable);
            }
            return false;
        }
    }
}
