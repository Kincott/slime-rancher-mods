using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MonomiPark.SlimeRancher.DataModel;
using MonomiPark.SlimeRancher.Regions;
using SRML.SR;
using UnityEngine;

namespace SRFusion
{
    // The fusion chamber: a corral upgrade (a real LandPlot.Upgrade, so saved by SRML and synced by SRMP).
    //   - Offered only once every plort is discovered, and only if no corral on the server has one.
    //   - In that corral, one plort of each type plus a pink slime are consumed to create the fused slime.
    //   - One fused slime per server: while one exists anywhere in the world, no new fusion happens.
    // A sign above the corral shows the progress. The fusion itself runs only on the side that simulates the
    // corral (the SRMP owner of its region), so it never happens twice; the spawn and the consumed items are
    // ordinary actors, synced by SRMP.
    internal class FusionChamber : PlotUpgrader
    {
        private const int Cost = 15000;
        private const float TickSeconds = 2f;

        internal static LandPlot.Upgrade Chamber;
        private static readonly FieldInfo activatorField = AccessTools.Field(typeof(LandPlotUI), "activator");
        private static readonly MethodInfo upgradeMethod =
            AccessTools.Method(typeof(LandPlotUI), "Upgrade", new[] { typeof(LandPlot.Upgrade), typeof(int) });

        private bool owned;
        private float nextTick;
        private TrackContainedIdentifiables[] trackers;
        private readonly WorldSign sign = new WorldSign();
        private readonly List<Identifiable> found = new List<Identifiable>();

        // ---- Registration (PreLoad) --------------------------------------------------------------------------

        internal static void Register()
        {
            Chamber = LandPlotUpgradeRegistry.CreateLandPlotUpgrade(217, "SRFUSION_CHAMBER");
            const string nameKey = "m.upgrade.name.corral.srfusion_chamber";
            const string descKey = "m.upgrade.desc.corral.srfusion_chamber";
            TranslationPatcher.AddPediaTranslation(nameKey, "Chambre de fusion");
            TranslationPatcher.AddPediaTranslation(descKey,
                "Dépose dans l'enclos une plorte de chaque type et un slime rose : ils fusionnent en un slime géant et unique " +
                "qui produit des plortes cosmiques, extrêmement rares et d'une valeur énorme. Un seul par serveur.");

            PurchasableUIRegistry.RegisterPurchasable<CorralUI>(ui => new PurchaseUI.Purchasable(nameKey,
                ShopIcon(), ShopIcon(), descKey, Cost, PediaDirector.Id.CORRAL,
                () => upgradeMethod.Invoke(ui, new object[] { Chamber, Cost }),
                // Visible once every plort is discovered (or on the corral that already has it).
                () => Plot(ui).HasUpgrade(Chamber) || Plorts.AllDiscovered(),
                // Buyable if no corral on the server has a chamber yet.
                () => !AnyCorralHasChamber()));

            LandPlotUpgradeRegistry.RegisterPlotUpgrader<FusionChamber>(LandPlot.Id.CORRAL);
        }

        private static Sprite shopIcon;

        private static Sprite ShopIcon()
        {
            if (shopIcon == null) shopIcon = Icons.Circle(Content.Top, Content.Middle, Content.Bottom);
            return shopIcon;
        }

        private static LandPlot Plot(LandPlotUI ui)
        {
            return (LandPlot)activatorField.GetValue(ui);
        }

        private static bool AnyCorralHasChamber()
        {
            foreach (LandPlotModel plot in SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().Values)
            {
                if (plot != null && plot.HasUpgrade(Chamber)) return true;
            }
            return false;
        }

        // ---- In the corral -----------------------------------------------------------------------------------

        public override void Apply(LandPlot.Upgrade upgrade)
        {
            if (upgrade == Chamber)
            {
                owned = true;
                nextTick = 0f;
            }
        }

        private void OnDestroy()
        {
            sign.Clear();
        }

        private void LateUpdate()
        {
            sign.FaceCamera();
            if (!owned || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + TickSeconds;
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Main.Log("fusion chamber failed: " + e.Message);
            }
        }

        private void Tick()
        {
            if (FusedSlimeExists())
            {
                sign.Show(transform, "Chambre de fusion · slime fusionné actif", Content.Bottom);
                return;
            }

            List<Identifiable.Id> required = Plorts.Required();
            int have = 0;
            foreach (Identifiable.Id id in required) if (CountInCorral(id) > 0) have++;
            bool pink = CountInCorral(Identifiable.Id.PINK_SLIME) > 0;

            // Plain words: the game font may not have check-mark glyphs.
            sign.Show(transform, "Fusion : " + have + "/" + required.Count + " plortes  ·  slime rose : " + (pink ? "oui" : "non"),
                have == required.Count && pink ? Content.Bottom : Content.Top);

            if (have == required.Count && pink && required.Count > 0 && SimulatedHere()) Fuse(required);
        }

        private void Fuse(List<Identifiable.Id> required)
        {
            foreach (Identifiable.Id id in required) ConsumeOne(id);
            ConsumeOne(Identifiable.Id.PINK_SLIME);

            Region region = GetComponentInParent<Region>();
            Vector3 at = transform.position + Vector3.up * 2f;
            SRBehaviour.InstantiateActor(Content.SlimePrefab, region != null ? region.setId : RegionRegistry.RegionSetId.HOME,
                at, Quaternion.identity);
            Main.Log("fusion complete: the fused slime is born");
        }

        private int CountInCorral(Identifiable.Id id)
        {
            int n = 0;
            foreach (TrackContainedIdentifiables t in Trackers()) n += t.Count(id);
            return n;
        }

        private void ConsumeOne(Identifiable.Id id)
        {
            foreach (TrackContainedIdentifiables t in Trackers())
            {
                found.Clear();
                t.GetTrackedItemsOfType(id, found);
                foreach (Identifiable item in found)
                {
                    if (item == null) continue;
                    Destroyer.DestroyActor(item.gameObject, "SRFusion.Fuse");
                    return;
                }
            }
        }

        private TrackContainedIdentifiables[] Trackers()
        {
            if (trackers == null) trackers = GetComponentsInChildren<TrackContainedIdentifiables>(true);
            return trackers;
        }

        // Anywhere in the world, loaded or not (the game model holds every actor of the save).
        private static bool FusedSlimeExists()
        {
            foreach (ActorModel actor in SRSingleton<SceneContext>.Instance.GameModel.AllActors().Values)
            {
                if (actor != null && actor.ident == Content.FusionSlime) return true;
            }
            return false;
        }

        // ---- Multiplayer: which side runs the fusion ---------------------------------------------------------

        private static readonly Type srmpGlobals = Type.GetType("SRMultiplayer.Globals, SRMP");
        private static readonly PropertyInfo isMultiplayer =
            srmpGlobals != null ? srmpGlobals.GetProperty("IsMultiplayer", BindingFlags.Public | BindingFlags.Static) : null;
        private static readonly Type networkLandplot = Type.GetType("SRMultiplayer.Networking.NetworkLandplot, SRMP");
        private static readonly PropertyInfo landplotIsLocal = networkLandplot != null ? networkLandplot.GetProperty("IsLocal") : null;

        private bool SimulatedHere()
        {
            try
            {
                if (isMultiplayer == null || !(bool)isMultiplayer.GetValue(null, null)) return true;
                Component net = networkLandplot != null ? GetComponentInParent(networkLandplot) : null;
                return net == null || landplotIsLocal == null || (bool)landplotIsLocal.GetValue(net, null);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
