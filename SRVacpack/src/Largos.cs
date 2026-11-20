using System;
using SRML.SR;
using UnityEngine;

namespace SRVacpack
{
    // WeaponVacuum consumes (stores) a captured object only when its Vacuumable.size is NORMAL; LARGE objects,
    // like largos, are held in the beam instead. And the player's vacpack only accepts ids in its list of potential
    // ammo. Largos get both: normal size on their prefab, and a place in the player's ammo list.
    internal static class Largos
    {
        // PreLoad: the ammo list is built from these registrations.
        internal static void AllowInVacpack()
        {
            foreach (Identifiable.Id id in Identifiable.LARGO_CLASS)
            {
                AmmoRegistry.RegisterPlayerAmmo(PlayerState.AmmoMode.DEFAULT, id);
            }
        }

        // Load: the prefabs exist now, before any largo is spawned in the world.
        internal static void MakeNormalSize()
        {
            LookupDirector lookup = SRSingleton<GameContext>.Instance.LookupDirector;
            int changed = 0;
            foreach (Identifiable.Id id in Identifiable.LARGO_CLASS)
            {
                try
                {
                    GameObject prefab = lookup.GetPrefab(id);
                    Vacuumable vac = prefab != null ? prefab.GetComponent<Vacuumable>() : null;
                    if (vac == null || vac.size == Vacuumable.Size.NORMAL) continue;
                    vac.size = Vacuumable.Size.NORMAL;
                    changed++;
                }
                catch (Exception)
                {
                    // a largo id without a prefab (unused id): skip
                }
            }
            Main.Log("largos storable in the vacpack: " + changed + " largo types");
        }
    }
}
