using System;
using System.Reflection;
using HarmonyLib;
using SRML.SR;
using UnityEngine;

namespace SRCorrals
{
    // The new plot upgrades (corrals, gardens, coops). They are real LandPlot.Upgrade values (registered through
    // SRML), so everything the game does with upgrades applies to them: bought through LandPlotUI.Upgrade (money
    // check, purchase effects), stored in the land plot model and saved by SRML in the .mod file, re-applied on
    // load by the plot's upgrader, and synced in multiplayer by SRMP, which sends any upgrade as its numeric value
    // (one byte).
    //
    // The numeric values are fixed (and below 256 for SRMP): both players must get the same values.
    internal static class Upgrades
    {
        internal static LandPlot.Upgrade Collector2, Collector3, Feeder2, Feeder3, Calm1, Calm2, Production1, Production2;
        internal static LandPlot.Upgrade Fertilizer1, Fertilizer2, Bounty1, Bounty2, Harvester;
        internal static LandPlot.Upgrade Incubator1, Incubator2, Extension1, Extension2;

        private struct Def
        {
            public string Plot;  // "corral", "garden" or "coop": also the translation key segment
            public string Name, Title, Desc;
            public int Cost;
            public Func<LandPlot.Upgrade> Self;
            public Func<LandPlot.Upgrade> Requires; // null: none
            public Func<LandPlotUI, Sprite> Icon;
        }

        private static readonly FieldInfo activatorField = AccessTools.Field(typeof(LandPlotUI), "activator");
        private static readonly MethodInfo upgradeMethod =
            AccessTools.Method(typeof(LandPlotUI), "Upgrade", new[] { typeof(LandPlot.Upgrade), typeof(int) });

        // Called in PreLoad (SRML only allows new upgrades there).
        internal static void Register()
        {
            Collector2 = Create(200, "SRCORRALS_COLLECTOR_2");
            Collector3 = Create(201, "SRCORRALS_COLLECTOR_3");
            Feeder2 = Create(202, "SRCORRALS_FEEDER_2");
            Feeder3 = Create(203, "SRCORRALS_FEEDER_3");
            Calm1 = Create(204, "SRCORRALS_CALM_1");
            Calm2 = Create(205, "SRCORRALS_CALM_2");
            Production1 = Create(206, "SRCORRALS_PRODUCTION_1");
            Production2 = Create(207, "SRCORRALS_PRODUCTION_2");
            Fertilizer1 = Create(208, "SRCORRALS_FERTILIZER_1");
            Fertilizer2 = Create(209, "SRCORRALS_FERTILIZER_2");
            Bounty1 = Create(210, "SRCORRALS_BOUNTY_1");
            Bounty2 = Create(211, "SRCORRALS_BOUNTY_2");
            Harvester = Create(212, "SRCORRALS_HARVESTER");
            Incubator1 = Create(213, "SRCORRALS_INCUBATOR_1");
            Incubator2 = Create(214, "SRCORRALS_INCUBATOR_2");
            Extension1 = Create(215, "SRCORRALS_EXTENSION_1");
            Extension2 = Create(216, "SRCORRALS_EXTENSION_2");

            RegisterCorral();
            RegisterGarden();
            RegisterCoop();

            LandPlotUpgradeRegistry.RegisterPlotUpgrader<CorralUpgrader>(LandPlot.Id.CORRAL);
            LandPlotUpgradeRegistry.RegisterPlotUpgrader<GardenUpgrader>(LandPlot.Id.GARDEN);
            LandPlotUpgradeRegistry.RegisterPlotUpgrader<CoopUpgrader>(LandPlot.Id.COOP);
        }

        private static void RegisterCorral()
        {
            Func<LandPlotUI, Sprite> collector = ui => ((CorralUI)ui).plortCollector.icon;
            Func<LandPlotUI, Sprite> feeder = ui => ((CorralUI)ui).feeder.icon;
            Func<LandPlotUI, Sprite> calm = ui => ((CorralUI)ui).musicBox.icon;
            Func<LandPlotUI, Sprite> plort = ui => ItemIcon(Identifiable.Id.PINK_PLORT);

            Add<CorralUI>(new Def { Plot = "corral", Name = "collector_2", Cost = 2000, Self = () => Collector2, Requires = () => LandPlot.Upgrade.PLORT_COLLECTOR, Icon = collector,
                Title = "Collecteur Mk II",
                Desc = "Collecte deux fois plus souvent et stocke 2,5 fois plus de plortes. Il ramasse tout ce qui est dans l'enclos, même surpeuplé." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "collector_3", Cost = 5000, Self = () => Collector3, Requires = () => Collector2, Icon = collector,
                Title = "Collecteur Mk III",
                Desc = "Collecte quatre fois plus souvent et stocke 5 fois plus de plortes." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "feeder_2", Cost = 1500, Self = () => Feeder2, Requires = () => LandPlot.Upgrade.FEEDER, Icon = feeder,
                Title = "Mangeoire Mk II",
                Desc = "Distribue deux fois plus de nourriture par cycle et en stocke 2,5 fois plus." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "feeder_3", Cost = 4000, Self = () => Feeder3, Requires = () => Feeder2, Icon = feeder,
                Title = "Mangeoire Mk III",
                Desc = "Distribue trois fois plus de nourriture par cycle et en stocke 5 fois plus." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "calm_1", Cost = 1200, Self = () => Calm1, Requires = null, Icon = calm,
                Title = "Diffuseur apaisant",
                Desc = "Les slimes de l'enclos se calment beaucoup plus vite (comme une deuxième boîte à musique) : on peut en garder plus sans qu'ils s'échappent." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "calm_2", Cost = 3000, Self = () => Calm2, Requires = () => Calm1, Icon = calm,
                Title = "Diffuseur apaisant Mk II",
                Desc = "Les slimes de l'enclos se calment encore plus vite." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "production_1", Cost = 3000, Self = () => Production1, Requires = null, Icon = plort,
                Title = "Stimulateur de plortes",
                Desc = "À chaque repas, un slime de l'enclos a une chance sur deux de produire une plorte de plus." });
            Add<CorralUI>(new Def { Plot = "corral", Name = "production_2", Cost = 8000, Self = () => Production2, Requires = () => Production1, Icon = plort,
                Title = "Stimulateur de plortes Mk II",
                Desc = "Chaque repas d'un slime de l'enclos produit une plorte de plus." });
        }

        private static void RegisterGarden()
        {
            Func<LandPlotUI, Sprite> soil = ui => ((GardenUI)ui).soil.icon;
            Func<LandPlotUI, Sprite> mix = ui => ((GardenUI)ui).miracleMix.icon;
            Func<LandPlotUI, Sprite> sprinkler = ui => ((GardenUI)ui).sprinkler.icon;

            Add<GardenUI>(new Def { Plot = "garden", Name = "fertilizer_1", Cost = 1500, Self = () => Fertilizer1, Requires = null, Icon = soil,
                Title = "Engrais",
                Desc = "La plantation pousse 25 % plus vite." });
            Add<GardenUI>(new Def { Plot = "garden", Name = "fertilizer_2", Cost = 4000, Self = () => Fertilizer2, Requires = () => Fertilizer1, Icon = soil,
                Title = "Engrais Mk II",
                Desc = "La plantation pousse 50 % plus vite." });
            Add<GardenUI>(new Def { Plot = "garden", Name = "bounty_1", Cost = 2000, Self = () => Bounty1, Requires = null, Icon = mix,
                Title = "Récolte abondante",
                Desc = "Un fruit ou légume de plus à chaque récolte (dans la limite des emplacements de la plante)." });
            Add<GardenUI>(new Def { Plot = "garden", Name = "bounty_2", Cost = 5000, Self = () => Bounty2, Requires = () => Bounty1, Icon = mix,
                Title = "Récolte abondante Mk II",
                Desc = "Deux fruits ou légumes de plus à chaque récolte." });
            Add<GardenUI>(new Def { Plot = "garden", Name = "harvester", Cost = 3000, Self = () => Harvester, Requires = null, Icon = sprinkler,
                Title = "Récolteur automatique",
                Desc = "Envoie chaque fruit ou légume mûr dans le silo le plus proche qui a de la place. Si aucun silo n'a de place, la récolte reste sur la plante." });
        }

        private static void RegisterCoop()
        {
            Func<LandPlotUI, Sprite> vitamizer = ui => ((CoopUI)ui).vitamizer.icon;
            Func<LandPlotUI, Sprite> walls = ui => ((CoopUI)ui).walls.icon;

            Add<CoopUI>(new Def { Plot = "coop", Name = "incubator_1", Cost = 1200, Self = () => Incubator1, Requires = null, Icon = vitamizer,
                Title = "Couveuse",
                Desc = "Les poules du poulailler pondent 25 % plus souvent." });
            Add<CoopUI>(new Def { Plot = "coop", Name = "incubator_2", Cost = 3000, Self = () => Incubator2, Requires = () => Incubator1, Icon = vitamizer,
                Title = "Couveuse Mk II",
                Desc = "Les poules du poulailler pondent 50 % plus souvent." });
            Add<CoopUI>(new Def { Plot = "coop", Name = "extension_1", Cost = 1500, Self = () => Extension1, Requires = null, Icon = walls,
                Title = "Poulailler agrandi",
                Desc = "Une fois et demie plus de poules avant que le poulailler soit trop plein pour pondre." });
            Add<CoopUI>(new Def { Plot = "coop", Name = "extension_2", Cost = 4000, Self = () => Extension2, Requires = () => Extension1, Icon = walls,
                Title = "Poulailler agrandi Mk II",
                Desc = "Deux fois plus de poules avant que le poulailler soit trop plein pour pondre." });
        }

        private static LandPlot.Upgrade Create(int value, string name)
        {
            return LandPlotUpgradeRegistry.CreateLandPlotUpgrade(value, name);
        }

        // One shop entry in every menu of that plot type. A tier is only offered once the previous one is owned
        // (the shop hides entries while unlocked() is false and re-checks after each purchase).
        private static void Add<T>(Def def) where T : LandPlotUI
        {
            string nameKey = "m.upgrade.name." + def.Plot + ".srcorrals_" + def.Name;
            string descKey = "m.upgrade.desc." + def.Plot + ".srcorrals_" + def.Name;
            TranslationPatcher.AddPediaTranslation(nameKey, def.Title);
            TranslationPatcher.AddPediaTranslation(descKey, def.Desc);
            PediaDirector.Id pedia = def.Plot == "garden" ? PediaDirector.Id.GARDEN
                : (def.Plot == "coop" ? PediaDirector.Id.COOP : PediaDirector.Id.CORRAL);

            PurchasableUIRegistry.RegisterPurchasable<T>(ui =>
            {
                Sprite icon = def.Icon(ui);
                return new PurchaseUI.Purchasable(nameKey, icon, icon, descKey, def.Cost, pedia,
                    () => Buy(ui, def.Self(), def.Cost),
                    () => def.Requires == null || Plot(ui).HasUpgrade(def.Requires()) || Plot(ui).HasUpgrade(def.Self()),
                    () => !Plot(ui).HasUpgrade(def.Self()));
            });
        }

        private static LandPlot Plot(LandPlotUI ui)
        {
            return (LandPlot)activatorField.GetValue(ui);
        }

        // The vanilla purchase path: money check, AddUpgrade (saved, SRMP-synced), sound, UI refresh.
        private static void Buy(LandPlotUI ui, LandPlot.Upgrade upgrade, int cost)
        {
            upgradeMethod.Invoke(ui, new object[] { upgrade, cost });
        }

        private static Sprite ItemIcon(Identifiable.Id id)
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
    }
}
