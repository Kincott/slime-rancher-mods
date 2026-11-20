using System;
using System.Collections.Generic;
using SRML;
using SRML.SR;
using SRML.Utils;
using UnityEngine;

namespace SRFusion
{
    // The two new items: the fused slime and the plort it makes, the cosmic plort.
    //
    // Their ids are fixed numbers, identical for both players: SRMP sends an actor's type as that number, so a
    // fused slime spawned by one player appears as a fused slime for the other. The "_SLIME" / "_PLORT" suffixes
    // make SRML classify them like any slime or plort. Prefabs are copies of the pink slime and pink plort with
    // new colors, built once the game's own prefabs exist (Load).
    //
    // The slime always produces the same plort (a single item stacks and stores cleanly), worth a fortune; its
    // low saturation makes the price drop fast when many are sold at once.
    internal static class Content
    {
        internal static Identifiable.Id FusionSlime, FusionPlort;
        internal static GameObject SlimePrefab, PlortPrefab;
        internal static SlimeDefinition Definition;

        private const float PlortValue = 1500f;     // a gold plort is around 200-300
        private const float PlortSaturation = 20f;  // low: selling many at once drops the price quickly
        private const float SizeFactor = 1.6f;      // compared to a pink slime

        // Iridescent palette of the slime: top, middle, bottom.
        internal static readonly Color Top = new Color(0.55f, 0.92f, 1f, 1f);
        internal static readonly Color Middle = new Color(0.95f, 0.50f, 1f, 1f);
        internal static readonly Color Bottom = new Color(1f, 0.85f, 0.40f, 1f);
        // Cosmic palette of its plort (the colors then drift over time, see CosmicPlortFX).
        internal static readonly Color CosmicTop = new Color(0.62f, 0.35f, 1f, 1f);
        internal static readonly Color CosmicMiddle = new Color(0.07f, 0.03f, 0.16f, 1f);
        internal static readonly Color CosmicBottom = new Color(0.15f, 0.85f, 1f, 1f);

        // PreLoad: ids and names.
        internal static void RegisterIds()
        {
            FusionSlime = IdentifiableRegistry.CreateIdentifiableId(32000, "FUSION_SLIME");
            FusionPlort = IdentifiableRegistry.CreateIdentifiableId(32001, "FUSION_PLORT");
            TranslationPatcher.AddActorTranslation("l.fusion_slime", "Slime fusionné");
            TranslationPatcher.AddActorTranslation("l.fusion_plort", "Plorte cosmique");
        }

        // Load: prefabs, slime definition, vacpack and silo entries.
        internal static void Build()
        {
            LookupDirector lookup = SRSingleton<GameContext>.Instance.LookupDirector;

            // ---- Cosmic plort ----
            PlortPrefab = PrefabUtils.CopyPrefab(lookup.GetPrefab(Identifiable.Id.PINK_PLORT));
            PlortPrefab.name = "plortCosmic";
            PlortPrefab.GetComponent<Identifiable>().id = FusionPlort;
            Recolor(PlortPrefab, "plort", CosmicTop, CosmicMiddle, CosmicBottom);
            PlortPrefab.AddComponent<CosmicPlortFX>(); // drifting colors, sparkles and glow on each plort
            LookupRegistry.RegisterIdentifiablePrefab(PlortPrefab);
            Sprite plortIcon = Icons.CosmicDiamond(CosmicTop, CosmicMiddle, CosmicBottom);
            LookupRegistry.RegisterVacEntry(FusionPlort, CosmicTop, plortIcon);
            AmmoRegistry.RegisterPlayerAmmo(PlayerState.AmmoMode.DEFAULT, FusionPlort);
            AmmoRegistry.RegisterSiloAmmo(t => t == SiloStorage.StorageType.PLORT || t == SiloStorage.StorageType.NON_SLIMES, FusionPlort);
            PlortRegistry.RegisterPlort(FusionPlort, PlortValue, PlortSaturation);

            // ---- Fused slime ----
            SlimeDefinitions defs = SRSingleton<GameContext>.Instance.SlimeDefinitions;
            SlimeDefinition pink = defs.GetSlimeByIdentifiableId(Identifiable.Id.PINK_SLIME);

            Definition = (SlimeDefinition)PrefabUtils.DeepCopyObject(pink);
            Definition.name = "FusionSlime";
            Definition.Name = "Fusion";
            Definition.IdentifiableId = FusionSlime;
            Definition.CanLargofy = false; // eating plorts must not turn it into a largo
            Definition.PrefabScale = pink.PrefabScale * SizeFactor;
            Definition.BaseSlimes = new SlimeDefinition[0];
            Definition.Diet = new SlimeDiet
            {
                MajorFoodGroups = new[] { SlimeEat.FoodGroup.FRUIT, SlimeEat.FoodGroup.VEGGIES, SlimeEat.FoodGroup.MEAT },
                Favorites = new Identifiable.Id[0],
                AdditionalFoods = new Identifiable.Id[0],
                Produces = new[] { FusionPlort },
                FavoriteProductionCount = 2,
                EatMap = new List<SlimeDiet.EatMapEntry>()
            };

            Sprite slimeIcon = Icons.Circle(Top, Middle, Bottom);
            SlimeAppearance appearance = CopyAppearance(pink.AppearancesDefault[0], slimeIcon);
            Definition.AppearancesDefault = new[] { appearance };
            Definition.AppearancesDynamic = new List<SlimeAppearance>();

            SlimePrefab = PrefabUtils.CopyPrefab(lookup.GetPrefab(Identifiable.Id.PINK_SLIME));
            SlimePrefab.name = "slimeFusion";
            // Bigger by default (the definition's PrefabScale is also raised, for systems that read it).
            SlimePrefab.transform.localScale *= SizeFactor;
            SlimePrefab.GetComponent<Identifiable>().id = FusionSlime;
            SlimePrefab.GetComponent<SlimeEat>().slimeDefinition = Definition;
            SlimeAppearanceApplicator applicator = SlimePrefab.GetComponent<SlimeAppearanceApplicator>();
            if (applicator != null)
            {
                applicator.SlimeDefinition = Definition;
                applicator.Appearance = appearance;
            }

            SlimeRegistry.RegisterSlimeDefinition(Definition);
            LookupRegistry.RegisterIdentifiablePrefab(SlimePrefab);
            LookupRegistry.RegisterVacEntry(FusionSlime, Middle, slimeIcon);
            AmmoRegistry.RegisterPlayerAmmo(PlayerState.AmmoMode.DEFAULT, FusionSlime);

            Main.Log("content built: fused slime and cosmic plort (" + PlortValue + " newbucks)");
        }

        // The pink slime's look with every material recolored to the iridescent palette.
        private static SlimeAppearance CopyAppearance(SlimeAppearance source, Sprite icon)
        {
            var copy = (SlimeAppearance)PrefabUtils.DeepCopyObject(source);
            copy.name = "FusionAppearance";
            copy.Icon = icon;
            var structures = new SlimeAppearanceStructure[source.Structures.Length];
            for (int i = 0; i < structures.Length; i++)
            {
                structures[i] = new SlimeAppearanceStructure(source.Structures[i]);
                Material[] mats = structures[i].DefaultMaterials;
                var newMats = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    newMats[j] = mats[j] != null ? new Material(mats[j]) : null;
                    if (newMats[j] != null) RecolorMaterial(newMats[j], "slime", Top, Middle, Bottom);
                }
                structures[i].DefaultMaterials = newMats;
            }
            copy.Structures = structures;
            return copy;
        }

        private static void Recolor(GameObject prefab, string what, Color top, Color middle, Color bottom)
        {
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = new Material(mats[i]);
                    RecolorMaterial(mats[i], what, top, middle, bottom);
                }
                r.sharedMaterials = mats;
            }
        }

        private static readonly HashSet<string> dumped = new HashSet<string>();

        // Slime and plort shaders use top / middle / bottom colors; any other color goes to the middle tone.
        // Each shader's color properties are logged once, to tune the palette if a part keeps its old color.
        private static void RecolorMaterial(Material m, string what, Color top, Color middle, Color bottom)
        {
            Shader s = m.shader;
            if (s == null) return;
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < s.GetPropertyCount(); i++)
            {
                if (s.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                string name = s.GetPropertyName(i);
                names.Append(name).Append(' ');
                string lower = name.ToLowerInvariant();
                if (lower.Contains("spec") || lower.Contains("shadow") || lower.Contains("outline")) continue;
                Color c = lower.Contains("top") ? top : (lower.Contains("bottom") ? bottom : middle);
                c.a = m.GetColor(name).a;
                m.SetColor(name, c);
            }
            if (dumped.Add(what + ":" + s.name)) Main.Log("recolor " + what + ": shader '" + s.name + "' colors: " + names);
        }
    }
}
