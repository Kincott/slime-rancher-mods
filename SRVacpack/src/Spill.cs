using System;
using System.Reflection;
using HarmonyLib;

namespace SRVacpack
{
    // Ammo.MaybeAddToSlot stops at the first slot already holding the item: if that slot is full the item is
    // refused, even with empty slots left (and once two slots hold the same item, only the first ever fills).
    //
    // This runs after it (after SRMP's replacement too, which keeps the same logic) and only steps in when the
    // item was refused, for the player's vacpack only (silos keep their vanilla rules and SRMP sync). It then
    // adds the item to another non-full slot holding it, or to an empty slot that accepts it.
    internal static class Spill
    {
        private static readonly PropertyInfo slotsProperty = AccessTools.Property(typeof(Ammo), "Slots");
        private static readonly MethodInfo fillPerVac = AccessTools.Method(typeof(Ammo), "GetAmountFilledPerVac");

        internal static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Ammo), "MaybeAddToSlot", new[] { typeof(Identifiable.Id), typeof(Identifiable) });
            if (target == null || slotsProperty == null || fillPerVac == null)
            {
                Main.Log("Ammo internals not found: no spilling into empty slots");
                return;
            }
            // VeryLow: after the game's and SRMP's logic, but before the largo limit (Priority.Last).
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(Spill), "AfterAdd") { priority = Priority.VeryLow });
        }

        private static void AfterAdd(Ammo __instance, ref bool __result, Identifiable.Id id, Identifiable identifiable)
        {
            if (__result || id == Identifiable.Id.MAGIC_WATER_LIQUID || !IsPlayerVacpack(__instance)) return;
            try
            {
                var slots = (Ammo.Slot[])slotsProperty.GetValue(__instance, null);
                if (slots == null) return;
                int usable = Math.Min(__instance.GetUsableSlotCount(), slots.Length);

                // Only when a slot already holds this item (otherwise the refusal has another reason).
                bool held = false;
                for (int i = 0; i < usable; i++) if (slots[i] != null && slots[i].id == id) held = true;
                if (!held) return;

                int fill = (int)fillPerVac.Invoke(__instance, new object[] { id, 0 });
                SlimeEmotions emotions = identifiable != null ? identifiable.GetComponent<SlimeEmotions>() : null;

                // Another slot with the same item that still has room.
                for (int i = 0; i < usable; i++)
                {
                    Ammo.Slot s = slots[i];
                    if (s == null || s.id != id) continue;
                    int max = __instance.GetSlotMaxCount(id, i);
                    if (s.count >= max) continue;
                    s.count = Math.Min(max, s.count + fill);
                    if (emotions != null) s.AverageIn(emotions);
                    __result = true;
                    return;
                }

                // An empty slot that accepts it (slot rules and the vacpack's allowed items are checked here).
                for (int j = 0; j < usable; j++)
                {
                    if (slots[j] != null || !__instance.CouldAddToSlot(id, j, false)) continue;
                    slots[j] = new Ammo.Slot(id, fill);
                    if (emotions != null) slots[j].AverageIn(emotions);
                    __result = true;
                    return;
                }
            }
            catch (Exception e)
            {
                Main.Log("spilling into an empty slot failed: " + e.Message);
            }
        }

        internal static bool IsPlayerVacpack(Ammo ammo)
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            return scene != null && scene.PlayerState != null && ReferenceEquals(scene.PlayerState.Ammo, ammo);
        }
    }
}
