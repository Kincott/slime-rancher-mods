using System;
using System.Collections.Generic;

namespace SRFusion
{
    // The plorts the market buys (minus the cosmic plort): the fusion needs one of each.
    internal static class Plorts
    {
        internal static List<Identifiable.Id> Required()
        {
            var list = new List<Identifiable.Id>();
            EconomyDirector econ = SRSingleton<SceneContext>.Instance != null ? SRSingleton<SceneContext>.Instance.EconomyDirector : null;
            if (econ == null || econ.baseValueMap == null) return list;
            foreach (EconomyDirector.ValueMap v in econ.baseValueMap)
            {
                if (v == null || v.accept == null) continue;
                Identifiable.Id id = v.accept.id;
                if (id != Content.FusionPlort && Identifiable.IsPlort(id) && !list.Contains(id)) list.Add(id);
            }
            return list;
        }

        // Discovered once its slime's Slimepedia entry is unlocked (X_PLORT -> X_SLIME).
        internal static bool Discovered(Identifiable.Id plort)
        {
            try
            {
                PediaDirector pedia = SRSingleton<SceneContext>.Instance.PediaDirector;
                string slimeName = plort.ToString().Replace("_PLORT", "_SLIME");
                if (!Enum.IsDefined(typeof(Identifiable.Id), slimeName)) return true;
                PediaDirector.Id? entry = pedia.GetPediaId((Identifiable.Id)Enum.Parse(typeof(Identifiable.Id), slimeName));
                return !entry.HasValue || pedia.IsUnlocked(entry.Value);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool AllDiscovered()
        {
            List<Identifiable.Id> all = Required();
            if (all.Count == 0) return false;
            foreach (Identifiable.Id id in all) if (!Discovered(id)) return false;
            return true;
        }
    }
}
