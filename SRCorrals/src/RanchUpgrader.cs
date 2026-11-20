using System;
using System.Reflection;
using UnityEngine;

namespace SRCorrals
{
    // Common base of the per-plot upgraders (corral, garden, coop), added by SRML to every plot of that type.
    // LandPlot calls Apply for each upgrade when bought and, on load, for every saved upgrade. Subclasses keep
    // their levels; the base answers "is this actor in my plot" and rebuilds the tier visuals when levels change.
    internal abstract class RanchUpgrader : PlotUpgrader
    {
        private TrackContainedIdentifiables[] trackers;
        protected readonly TierVisuals Visuals = new TierVisuals();
        protected bool VisualsDirty;

        public override void Apply(LandPlot.Upgrade upgrade)
        {
            if (OnUpgrade(upgrade)) VisualsDirty = true;
        }

        // Returns true when the upgrade is one of this plot type's and changed a level.
        protected abstract bool OnUpgrade(LandPlot.Upgrade upgrade);

        // Rebuilds the visuals; returns false to retry next frame (font or objects not ready yet).
        protected abstract bool BuildVisuals();

        protected virtual void LateUpdate()
        {
            if (VisualsDirty) VisualsDirty = !BuildVisuals();
            Visuals.FaceCamera();
        }

        protected virtual void OnDestroy()
        {
            Visuals.Clear();
        }

        // Whether this plot currently holds the actor (the plot's own content trackers, as drones use).
        internal bool Contains(Identifiable actor)
        {
            if (trackers == null) trackers = GetComponentsInChildren<TrackContainedIdentifiables>(true);
            foreach (TrackContainedIdentifiables t in trackers)
            {
                if (t != null && t.Contains(actor)) return true;
            }
            return false;
        }

        // ---- Multiplayer -------------------------------------------------------------------------------------

        private static readonly Type srmpGlobals = Type.GetType("SRMultiplayer.Globals, SRMP");
        private static readonly PropertyInfo isMultiplayer =
            srmpGlobals != null ? srmpGlobals.GetProperty("IsMultiplayer", BindingFlags.Public | BindingFlags.Static) : null;
        private static readonly Type networkLandplot = Type.GetType("SRMultiplayer.Networking.NetworkLandplot, SRMP");
        private static readonly PropertyInfo landplotIsLocal =
            networkLandplot != null ? networkLandplot.GetProperty("IsLocal") : null;

        // Actions that change the world (harvesting) must run on one side only. SRMP lets the owner of the plot's
        // region simulate it (like the plort collector); in solo, or without SRMP, it is always this side.
        protected bool SimulatedHere()
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
