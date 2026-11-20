using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Entry = GameObjectActorModelIdentifiableIndex.Entry;

namespace SRMPFixes
{
    // Tabby slimes look for nearby largos (AweTowardsLargos) through the CellDirector index. In multiplayer
    // that index can keep an entry whose GameObject was already destroyed, and
    // FindConsumable.FindNearestConsumable then throws a NullReferenceException in Identifiable.IsEdible on
    // every physics tick, for every slime near the ghost. Dropping dead entries before the search makes the
    // slime simply ignore an object that no longer exists, which is what the game intends.
    internal static class GhostLargoFix
    {
        private static bool loggedOnce;

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(
                typeof(FindConsumable),
                "FindNearestConsumable",
                new Type[] { typeof(IList<Entry>), typeof(float).MakeByRefType() });
            if (target == null)
            {
                Main.Log("FindConsumable.FindNearestConsumable(IList, out float) not found, ghost largo fix disabled");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(GhostLargoFix), "Prefix"));
        }

        private static void Prefix(IList<Entry> gameObjects)
        {
            // The game passes a reusable List; anything else (e.g. an array) cannot shrink, leave it alone.
            List<Entry> list = gameObjects as List<Entry>;
            if (list == null)
            {
                return;
            }
            // Unity's == null is also true for a destroyed object, which is exactly the ghost case.
            int removed = list.RemoveAll(e => e.GameObject == null);
            if (removed > 0 && !loggedOnce)
            {
                loggedOnce = true;
                Main.Log("skipped destroyed objects in a slime search (logged once per session)");
            }
        }
    }
}
