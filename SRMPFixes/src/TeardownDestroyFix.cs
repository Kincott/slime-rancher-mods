using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SRMultiplayer;
using SRMultiplayer.Networking;
using SRMultiplayer.Packets;
using UnityEngine;

namespace SRMPFixes
{
    // SRMP's Identifiable_OnDestroy postfix sends "actor destroyed" for EVERY actor whose GameObject is destroyed,
    // including when a player quits and Unity tears the whole world down. The host's OnActorDestroy then really
    // destroys each one, and the next save no longer has them: on 2026-11-20 a client quitting wiped the host's
    // slimes and garden produce (land plots and upgrades are not actors, so they survived).
    //
    // Two layers:
    // 1. Sender side (everyone): no destroy broadcast while this game is quitting or unloading the scene.
    // 2. Host side: a client's destroy is applied after a short delay, and dropped if that client disconnected
    //    in the meantime. This protects the host's save even if the client runs an older version of this mod.
    internal static class TeardownDestroyFix
    {
        private const float HostDelaySeconds = 3f;

        private static bool quitting;
        private static MethodInfo serverOnActorDestroy;
        private static bool replaying;
        private static int droppedSinceLastLog;

        private struct PendingDestroy
        {
            public PacketActorDestroy Packet;
            public NetworkPlayer Player;
            public byte PlayerId;
            public float DueAt;
        }

        private static readonly List<PendingDestroy> pending = new List<PendingDestroy>();

        internal static void Apply(Harmony harmony)
        {
            Application.quitting += OnQuitting;

            var onDestroy = AccessTools.Method(typeof(Identifiable), "OnDestroy");
            if (onDestroy == null)
            {
                Main.Log("Identifiable.OnDestroy not found, sender-side teardown guard disabled");
            }
            else
            {
                // The postfix runs after SRMP's (Priority.Last) to restore the flag.
                harmony.Patch(onDestroy,
                    prefix: new HarmonyMethod(typeof(TeardownDestroyFix), "SenderPrefix") { priority = Priority.First },
                    postfix: new HarmonyMethod(typeof(TeardownDestroyFix), "SenderPostfix") { priority = Priority.Last });
            }

            serverOnActorDestroy = AccessTools.Method(typeof(NetworkHandlerServer), "OnActorDestroy");
            if (serverOnActorDestroy == null)
            {
                Main.Log("NetworkHandlerServer.OnActorDestroy not found, host-side destroy guard disabled");
            }
            else
            {
                harmony.Patch(serverOnActorDestroy, prefix: new HarmonyMethod(typeof(TeardownDestroyFix), "HostPrefix"));
            }
        }

        private static void OnQuitting()
        {
            quitting = true;
        }

        // ---- Sender side -------------------------------------------------------------------------------------

        // SRMP's postfix skips its broadcast when Globals.HandlePacket is true (that is how it avoids echoing
        // destroys it received). Setting it for the duration of OnDestroy during teardown reuses that guard.
        private static void SenderPrefix(Identifiable __instance, out bool __state)
        {
            __state = Globals.HandlePacket;
            if (IsTearingDown(__instance))
            {
                Globals.HandlePacket = true;
            }
        }

        private static void SenderPostfix(bool __state)
        {
            Globals.HandlePacket = __state;
        }

        private static bool IsTearingDown(Identifiable identifiable)
        {
            if (quitting)
            {
                return true;
            }
            // While a scene is being unloaded its objects no longer belong to a loaded scene.
            return identifiable != null && !identifiable.gameObject.scene.isLoaded;
        }

        // ---- Host side ---------------------------------------------------------------------------------------

        // Returning false defers SRMP's handler; Tick replays it later.
        private static bool HostPrefix(PacketActorDestroy packet, NetworkPlayer player)
        {
            if (replaying || player == null)
            {
                return true;
            }
            pending.Add(new PendingDestroy
            {
                Packet = packet,
                Player = player,
                PlayerId = player.ID,
                DueAt = Time.unscaledTime + HostDelaySeconds
            });
            return false;
        }

        internal static void Tick()
        {
            if (pending.Count == 0)
            {
                return;
            }
            if (!Globals.IsServer)
            {
                pending.Clear(); // session ended
                return;
            }

            float now = Time.unscaledTime;
            int i = 0;
            while (i < pending.Count && pending[i].DueAt <= now)
            {
                PendingDestroy item = pending[i];
                i++;

                NetworkPlayer current;
                bool stillConnected = Globals.Players.TryGetValue(item.PlayerId, out current) && current == item.Player;
                if (!stillConnected)
                {
                    droppedSinceLastLog++;
                    continue;
                }
                Replay(item);
            }
            pending.RemoveRange(0, i);

            if (droppedSinceLastLog > 0)
            {
                Main.Log("ignored " + droppedSinceLastLog + " destroy message(s) from a player who disconnected right after (world unload, not gameplay)");
                droppedSinceLastLog = 0;
            }
        }

        private static void Replay(PendingDestroy item)
        {
            bool previousHandlePacket = Globals.HandlePacket;
            replaying = true;
            Globals.HandlePacket = true; // same context SRMP runs its handlers in
            try
            {
                serverOnActorDestroy.Invoke(null, new object[] { item.Packet, item.Player });
            }
            catch (System.Exception e)
            {
                Main.Log("replaying a destroy failed: " + e.GetBaseException().Message);
            }
            finally
            {
                replaying = false;
                Globals.HandlePacket = previousHandlePacket;
            }
        }
    }
}
