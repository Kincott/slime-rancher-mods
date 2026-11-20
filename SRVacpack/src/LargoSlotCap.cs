using System;
using System.Reflection;
using HarmonyLib;
using MonomiPark.SlimeRancher.DataModel;
using UnityEngine;

namespace SRVacpack
{
    // A vacpack slot holds at most LargosPerSlot largos. A slot's capacity comes from a rule stored in the
    // vacpack's model (AmmoModel.slotMaxCountFunction), which every capacity check goes through: adding, the
    // "could I vacuum this" test, the HUD gauge. Wrapping that rule (rather than patching the one-line getter,
    // which the JIT may inline) makes all of them agree: a largo slot is full at LargosPerSlot, so the next largo
    // is refused there and the empty-slot spill puts it in another slot, exactly like slimes.
    //
    // The game installs a new rule when the vacpack is upgraded, so the wrapper is checked every second and put
    // back if it was replaced.
    internal static class LargoSlotCap
    {
        private const float CheckSeconds = 1f;

        private static readonly FieldInfo ammoModelField = AccessTools.Field(typeof(Ammo), "ammoModel");
        private static readonly FieldInfo ruleField = AccessTools.Field(typeof(AmmoModel), "slotMaxCountFunction");

        private static Func<Identifiable.Id, int, int> installed;
        private static float nextCheck;
        private static bool warned;

        internal static void Tick()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckSeconds;

            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.PlayerState == null || ammoModelField == null || ruleField == null) return;
            try
            {
                var model = ammoModelField.GetValue(scene.PlayerState.Ammo) as AmmoModel;
                if (model == null) return;
                var current = ruleField.GetValue(model) as Func<Identifiable.Id, int, int>;
                if (current == null || ReferenceEquals(current, installed)) return;

                Func<Identifiable.Id, int, int> original = current;
                installed = (id, index) =>
                {
                    int max = original(id, index);
                    return Identifiable.IsLargo(id) ? Math.Min(max, Main.LargosPerSlot) : max;
                };
                model.SetSlotMaxCountFunction(installed);
            }
            catch (Exception e)
            {
                if (warned) return;
                warned = true;
                Main.Log("largo slot cap failed: " + e.Message);
            }
        }
    }
}
