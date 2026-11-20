using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SRHud
{
    // Icons, colors and names exactly as the vanilla HUD shows them, through AmmoSlotUI's own private helpers
    // (they handle slime appearances and translations). Falls back to empty values if unavailable.
    internal class VanillaLook
    {
        private readonly AmmoSlotUI ui;
        private readonly MethodInfo getIcon, getColor, getName;

        internal VanillaLook(HudUI hud)
        {
            ui = hud.GetComponentInChildren<AmmoSlotUI>(true);
            getIcon = AccessTools.Method(typeof(AmmoSlotUI), "GetCurrentIcon");
            getColor = AccessTools.Method(typeof(AmmoSlotUI), "GetCurrentColor");
            getName = AccessTools.Method(typeof(AmmoSlotUI), "GetName");
        }

        internal Sprite Icon(Identifiable.Id id)
        {
            return id == Identifiable.Id.NONE ? null : Call<Sprite>(getIcon, id);
        }

        // Best image for whatever the player aims at: items and slimes, then gordos (their own icon, else the
        // matching slime's: PINK_GORDO -> PINK_SLIME), then drones (their gadget's icon). Null if none exists.
        internal Sprite TargetIcon(GameObject target, out Identifiable.Id id)
        {
            id = Identifiable.Id.NONE;
            if (target == null) return null;

            Identifiable ident = target.GetComponentInParent<Identifiable>();
            if (ident != null && ident.id != Identifiable.Id.NONE)
            {
                id = ident.id;
                return Icon(id);
            }

            GordoIdentifiable gordo = target.GetComponentInParent<GordoIdentifiable>();
            if (gordo != null && gordo.id != Identifiable.Id.NONE)
            {
                id = gordo.id;
                Sprite s = LookupIcon(gordo.id);
                if (s != null) return s;
                string slimeName = gordo.id.ToString().Replace("_GORDO", "_SLIME");
                if (Enum.IsDefined(typeof(Identifiable.Id), slimeName))
                {
                    Identifiable.Id slime = (Identifiable.Id)Enum.Parse(typeof(Identifiable.Id), slimeName);
                    return Icon(slime);
                }
                return null;
            }

            Drone drone = target.GetComponentInParent<Drone>();
            if (drone != null && drone.gadget != null)
            {
                try
                {
                    return SRSingleton<GameContext>.Instance.LookupDirector.GetGadgetDefinition(drone.gadget.id).icon;
                }
                catch (Exception)
                {
                    return null;
                }
            }
            return null;
        }

        // LookupDirector.GetIcon throws or returns null for ids without a vacpack entry (like gordos).
        private static Sprite LookupIcon(Identifiable.Id id)
        {
            try
            {
                return SRSingleton<GameContext>.Instance.LookupDirector.GetIcon(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // A largo's two base slime icons (vanilla has placeholder art for some largos). False if not a largo.
        internal bool LargoPair(Identifiable.Id id, out Sprite first, out Sprite second)
        {
            first = null;
            second = null;
            if (id == Identifiable.Id.NONE || !Identifiable.IsLargo(id)) return false;
            try
            {
                SlimeDefinition def = SRSingleton<GameContext>.Instance.SlimeDefinitions.GetSlimeByIdentifiableId(id);
                if (def == null || def.BaseSlimes == null || def.BaseSlimes.Length < 2) return false;
                first = Icon(def.BaseSlimes[0].IdentifiableId);
                second = Icon(def.BaseSlimes[1].IdentifiableId);
                return first != null && second != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The game ships some "IMAGE NEEDED" placeholder icons; treat them as missing. Matched by asset name,
        // and every icon name seen for the first time on a target is logged so the list can be refined.
        internal static bool IsPlaceholder(Sprite sprite)
        {
            if (sprite == null) return false;
            string n = (sprite.name + " " + (sprite.texture != null ? sprite.texture.name : "")).ToLowerInvariant();
            return n.Contains("need") || n.Contains("placeholder") || n.Contains("missing") || n.Contains("todo");
        }

        internal Color Color(Identifiable.Id id)
        {
            return id == Identifiable.Id.NONE ? default(Color) : Call<Color>(getColor, id);
        }

        internal string Name(Identifiable.Id id)
        {
            if (id == Identifiable.Id.NONE) return "";
            string name = Call<string>(getName, id, false);
            return string.IsNullOrEmpty(name) ? id.ToString() : name;
        }

        private T Call<T>(MethodInfo method, params object[] args)
        {
            if (method == null || ui == null) return default(T);
            try
            {
                object result = method.Invoke(ui, args);
                return result is T ? (T)result : default(T);
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}
