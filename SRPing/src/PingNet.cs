using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Lidgren.Network;
using SRMultiplayer;
using SRMultiplayer.Packets;
using UnityEngine;

namespace SRPing
{
    // Pings ride on SRMP's chat channel, which already reaches every player: a client's chat goes to the host,
    // which prefixes the sender's name, shows it, and relays it to all clients (the sender included). A ping is a
    // chat message carrying a marker; ChatUI.AddChatMessage is the single place every received chat line goes
    // through, so a prefix there turns marked lines into pings and keeps them out of the chat box.
    //
    // Message: "<Name>: \u0001SRPING|<pingId>|<x>|<y>|<z>|<identifiable id or 0>". The id lets the sender skip
    // its own ping coming back from the host. The object's name is looked up by each player in their language.
    internal static class PingNet
    {
        private const string Marker = "\u0001SRPING|";

        private static readonly HashSet<int> ownIds = new HashSet<int>();
        private static readonly System.Random random = new System.Random();

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(ChatUI), "AddChatMessage");
            if (target == null)
            {
                Main.Log("SRMP chat not found: pings stay local");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(PingNet), "BeforeChat"));
        }

        // Shows the ping locally and sends it to the other players.
        internal static void Send(Vector3 at, Identifiable.Id id, Transform target)
        {
            int pingId = random.Next(1, int.MaxValue);
            ownIds.Add(pingId);
            PingView.Show(LocalName(), true, at, id, target);

            if (!Globals.IsMultiplayer) return;
            string payload = Marker + pingId + "|" + F(at.x) + "|" + F(at.y) + "|" + F(at.z) + "|" + (int)id;
            try
            {
                // Same as SRMP's own chat send: the host adds its name, a client lets the host add it.
                var packet = new PacketPlayerChat { message = Globals.IsServer ? Globals.Username + ": " + payload : payload };
                packet.Send(NetDeliveryMethod.ReliableOrdered);
            }
            catch (Exception e)
            {
                Main.Log("ping send failed: " + e.Message);
            }
        }

        private static bool BeforeChat(string message)
        {
            if (message == null) return true;
            int at = message.IndexOf(Marker, StringComparison.Ordinal);
            if (at < 0) return true;
            try
            {
                string sender = at >= 2 ? message.Substring(0, at).TrimEnd(' ', ':') : "?";
                string[] f = message.Substring(at + Marker.Length).Split('|');
                int pingId = int.Parse(f[0], CultureInfo.InvariantCulture);
                if (ownIds.Contains(pingId)) return false; // our own ping, relayed back
                var pos = new Vector3(P(f[1]), P(f[2]), P(f[3]));
                var id = (Identifiable.Id)int.Parse(f[4], CultureInfo.InvariantCulture);
                PingView.Show(sender, false, pos, id, null); // the view finds the object near pos to follow it
            }
            catch (Exception e)
            {
                Main.Log("bad ping message: " + e.Message);
            }
            return false; // never shown in the chat box
        }

        private static string LocalName()
        {
            try
            {
                return Globals.IsMultiplayer && !string.IsNullOrEmpty(Globals.Username) ? Globals.Username : "Toi";
            }
            catch (Exception)
            {
                return "Toi";
            }
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static float P(string s) { return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture); }
    }
}
