using System.Collections.Generic;
using Lidgren.Network;
using SRMultiplayer;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using UnityEngine;

namespace SRMPFixes
{
    // SRMP gives every region (and so every corral in it) and every actor (slime, plort...) an owner,
    // and only the owner simulates it: PlortCollector.DoCollection is skipped unless the corral's region
    // is owned locally, and a non-owned actor is frozen in place.
    //
    // When a player disconnects, NetworkServer.OnDisconnected never releases what they owned, so those
    // regions and actors keep pointing at a player who is gone: the collector runs nowhere and their
    // slimes stay frozen until someone vacuums them. This runs on the host only and hands every orphan
    // back: to the host when the host has it loaded, otherwise to "nobody" (0), which SRMP already
    // resolves by giving it to the next player who loads it.
    internal static class OrphanOwnershipFix
    {
        private const float IntervalSeconds = 2f;
        private const byte NoOwner = 0;

        private static float nextCheckAt;

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheckAt)
            {
                return;
            }
            nextCheckAt = Time.unscaledTime + IntervalSeconds;

            if (!Globals.IsServer)
            {
                return;
            }

            int regions = ReclaimRegions();
            int actors = ReclaimActors();
            if (regions > 0 || actors > 0)
            {
                Main.Log("reclaimed " + regions + " region(s) and " + actors + " actor(s) owned by disconnected players");
            }
        }

        private static int ReclaimRegions()
        {
            int count = 0;
            foreach (NetworkRegion region in new List<NetworkRegion>(Globals.Regions.Values))
            {
                if (region == null || region.Region == null)
                {
                    continue;
                }
                bool loadedHere = region.Region.root != null && region.Region.root.activeInHierarchy;
                bool orphaned = IsOrphanOwner(region.Owner);
                // Owner 0 while the host has the region loaded is also a dead state: it happens when
                // SRMP's OnRegionOwner broadcasts "0" right after the host re-took the region.
                bool unownedButLoaded = region.Owner == NoOwner && loadedHere;
                if (!orphaned && !unownedButLoaded)
                {
                    continue;
                }

                if (loadedHere)
                {
                    region.TakeOwnership();
                }
                else
                {
                    region.SetOwnership(NoOwner);
                    new PacketRegionOwner { ID = region.ID, Owner = NoOwner }.Send(NetDeliveryMethod.ReliableOrdered);
                }
                count++;
            }
            return count;
        }

        private static int ReclaimActors()
        {
            int count = 0;
            foreach (NetworkActor actor in new List<NetworkActor>(Globals.Actors.Values))
            {
                if (actor == null || !IsOrphanOwner(actor.Owner))
                {
                    continue;
                }

                if (actor.gameObject.activeInHierarchy)
                {
                    actor.TakeOwnership();
                }
                else
                {
                    actor.SetOwnership(NoOwner);
                    new PacketActorOwner { ID = actor.ID, Owner = NoOwner }.Send(NetDeliveryMethod.ReliableOrdered);
                }
                count++;
            }
            return count;
        }

        // An owner is orphaned when it is a real player id that is neither the host nor connected.
        private static bool IsOrphanOwner(byte owner)
        {
            return owner != NoOwner && owner != Globals.LocalID && !Globals.Players.ContainsKey(owner);
        }
    }
}
