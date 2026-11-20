using System;
using System.Collections.Generic;
using MonomiPark.SlimeRancher.Regions;
using SRML.Console;
using UnityEngine;

namespace SRFusion
{
    // Console command (SRML console: Ctrl+Tab) to try the fusion quickly, meant for a separate test game:
    //   fusiontest   gives 20 000 newbucks, unlocks the Slimepedia entry of every plort's slime (so the fusion
    //                chamber is offered), and drops one plort of each type plus a pink slime in front of you.
    internal class FusionTestCommand : ConsoleCommand
    {
        private const int Money = 20000;

        public override string ID { get { return "fusiontest"; } }
        public override string Usage { get { return "fusiontest"; } }
        public override string Description
        {
            get { return "Test the fusion: 20 000 newbucks, all plort slimes discovered, one plort of each + a pink slime in front of you"; }
        }

        public override bool Execute(string[] args)
        {
            SceneContext scene = SRSingleton<SceneContext>.Instance;
            if (scene == null || scene.PlayerState == null || scene.Player == null)
            {
                Main.Log("fusiontest: load a game first");
                return false;
            }

            scene.PlayerState.AddCurrency(Money);

            int unlocked = 0;
            List<Identifiable.Id> plorts = Plorts.Required();
            foreach (Identifiable.Id plort in plorts)
            {
                string slimeName = plort.ToString().Replace("_PLORT", "_SLIME");
                if (!Enum.IsDefined(typeof(Identifiable.Id), slimeName)) continue;
                PediaDirector.Id? entry = scene.PediaDirector.GetPediaId((Identifiable.Id)Enum.Parse(typeof(Identifiable.Id), slimeName));
                if (!entry.HasValue || scene.PediaDirector.IsUnlocked(entry.Value)) continue;
                scene.PediaDirector.UnlockWithoutPopup(entry.Value);
                unlocked++;
            }

            // 4 m in front of the player, a little above the ground, spread in a ring so they do not stack.
            Vector3 forward = scene.Player.transform.forward;
            forward.y = 0f;
            Vector3 at = scene.Player.transform.position + forward.normalized * 4f + Vector3.up;
            RegionRegistry.RegionSetId set = scene.Player.GetComponent<RegionMember>().setId;
            LookupDirector lookup = SRSingleton<GameContext>.Instance.LookupDirector;

            var items = new List<Identifiable.Id>(plorts) { Identifiable.Id.PINK_SLIME };
            for (int i = 0; i < items.Count; i++)
            {
                GameObject prefab = lookup.GetPrefab(items[i]);
                if (prefab == null) continue;
                float angle = i * Mathf.PI * 2f / items.Count;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;
                SRBehaviour.InstantiateActor(prefab, set, at + offset, Quaternion.identity);
            }

            Main.Log("fusiontest: +" + Money + " newbucks, " + unlocked + " Slimepedia entries unlocked, "
                + plorts.Count + " plorts and a pink slime dropped. Buy the fusion chamber on a corral and put them in.");
            return true;
        }
    }
}
