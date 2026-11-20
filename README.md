# Slime Rancher mods

Independent SRML mods for Slime Rancher, made for playing co-op with SRMP (Slime Rancher Multiplayer 0.0.1584).
Each mod lives on its own branch; `main` holds what they share (build script, docs).

| Branch | Mod | What it does |
|---|---|---|
| `srmp-fixes` | SRMP Fixes (`SRMPFixes.dll`) | Fixes SRMP sync bugs, a save-wiping bug and re-enables achievements, without modifying SRMP |
| `sr-boost` | SR Boost (`SRBoost.dll`) | Removes the 120 FPS cap, boosts graphics, smart feeders, FPS measurements |
| `sr-hud` | SR HUD (`SRHud.dll`) | Redesigned HUD, dark theme for every menu, shop status |
| `sr-market` | SR Market (`SRMarket.dll`) | Plort price history and forecast, on the market screen and with a key |
| `sr-corrals` | SR Corrals (`SRCorrals.dll`) | Extra corral upgrades: collector, feeder, calm, production (planned) |
| `sr-fusion` | SR Fusion (`SRFusion.dll`) | One-per-server fused slime, unlocked once every plort is discovered (planned) |

SRMP refuses to connect when the two players' mod lists differ, so both players install the same mods.

## SRMP Fixes

Fixes bugs of SRMP without modifying SRMP itself.

| Bug | Cause | Fix |
|---|---|---|
| **A player quitting wipes the host's slimes and garden produce from the save** | When a game unloads its world, SRMP sends "actor destroyed" for every object, and the host really destroys them, then saves. | Quitting or unloading games no longer send destroys; the host also applies a client's destroys 3 s late and drops them if that client disconnected meanwhile. |
| Plort collectors sometimes do nothing (button and timer), until you vacuum and throw some slimes | SRMP only runs a collector on the player who owns the corral's region, and only lets the owner move an object. When a player disconnects, SRMP never releases what they owned, so the corral and their slimes/plorts stay owned by someone who is gone. | Every 2 s, the host hands everything owned by a disconnected player back to itself (if it has it loaded) or to nobody (the next player who loads it takes it). |
| Console spam `NullReferenceException ... Identifiable.IsEdible ... AweTowardsLargos` | Tabby slimes search nearby largos in an index that can still hold a destroyed largo. | Destroyed entries are skipped before the search. |
| A region or slime the host owns looks unowned to the client, who then grabs it (brief double simulation) | When a client gives something up and the host re-takes it, SRMP still broadcasts the client's "nobody owns it" afterwards, and sends ownership changes unreliably. | The "nobody" broadcast is only sent when still true, and ownership packets are sent reliably. |
| `Failed to send packet: Error - NoConnection` spam on the host | Each time the host loads a region, SRMP serializes the region's whole actor list and sends it to the host itself. | Sends addressed to the host are skipped. |
| No achievements in multiplayer | SRMP disables the game's achievement awarding for host and client on purpose. Stats keep counting. | Awarding is re-enabled on both sides. On the client, world achievements can unlock from the host's world. |

In the SRML console the mod logs `loaded`, `reclaimed ...` whenever it repairs an ownership, and `re-enabled achievements` once per session.

## SR Boost (graphics and frame rate)

A second, independent mod, `SRBoost.dll`. SRMP refuses to connect when the two players' mod lists differ, so both
players install it; each one tunes `SRML\Mods\SRBoost.ini` (created on first launch) for their own PC.

- Removes the game's hard-coded 120 FPS cap (`GameContext.Awake`). `TargetFps = 0` leaves the cap to the GPU driver;
  on a 280 Hz G-Sync screen use `TargetFps = 270` (no NVIDIA profile tool needed: the game is CPU-bound, so the
  driver's low-latency mode would not help).
- `ApplyBestGameSettings = true` sets the in-game options to Very High with V-Sync off on the next game load, through
  the options menu's own calls, then turns itself back to `false`.
- Pushes the highest in-game levels further: shadow distance 150 to 250, shadow resolution Very High, LOD bias 2 to
  4, pixel lights 4 to 8. Lower in-game levels are left as the game sets them.
- Smart feeder (`SmartFeeder = true`): on top of the vanilla cycle (6 items every 3/6/9 h, whatever the corral
  holds), every few seconds a feeder queues one item per slime in its corral that is hungry enough to eat, minus the
  food already on the ground. It goes through the normal feed queue, so in multiplayer only the corral owner ejects.
- Measured on 2026-11-20 (Ryzen 7 5800X, RTX 5060 Ti): about 140 FPS average, 1% lows near 40 FPS, and the hitch count
  matching the GC run count (about one collection per second). Incremental GC is off in the shipped game; the
  experiment is adding `gc-max-time-slice=3` to `SlimeRancher_Data\boot.config` (original backed up in
  `Documents\SlimeRancher-save-backup-2026-11-20\boot.config.original`); the perf log reports whether it took effect.
- Logs `perf ...: avg FPS, 1% low, worst frame, hitches, GC runs` every 30 s, and the physics step once, to
  `%USERPROFILE%\AppData\LocalLow\Monomi Park\Slime Rancher\SRML\srml.log`.

## SR HUD (redesigned HUD, work in progress)

A third mod, `SRHud.dll`, replacing the vanilla HUD with a compact "dark glass" one. Both players install it
(SRMP mod-list check); `SRML\Mods\SRHud.ini` sets `Enabled`, `Scale`, `Opacity`, `ShowFps`.

- Bottom-left panel: HP / EN bars (RAD only while irradiated), money, keys, day and time.
- Bottom-center slots: item icon, count, fill gauge in the item's color, no big X on empty slots; the selected slot
  is ringed and its item name and count shown above. Magic water timer on the liquid slot.
- Aim card at the top center of the screen (replaces the top-right target text): icon, name and info mirrored from the vanilla
  TargetingUI, plus live Hunger and Agitation bars for slimes and largos. Fades in and out.
- Top-left FPS counter in a dark glass pill (top-right is SRMP's window).
- Money toast: the vanilla "+24 [coin]" popups (which piled up on the left when selling fast) are hidden with the old
  HUD (they still spawn, for the sale sound); one toast just above the money, driven by the balance itself, sums
  changes made within 1.2 s: gold for gains, red for spending.
- The vanilla elements it replaces are only made invisible (CanvasGroup), never disabled, so the game logic behind
  them keeps running; mail, autosave, popups and tutorials are untouched. On any error it restores the vanilla HUD.
- Menu theme (`ThemeMenus = true`): the game's UI style sheet (`UIStyleDirector`, used by every `*Styler`) is
  rewritten in memory once: 9-sliced backgrounds become the rounded dark surface keeping a hint of their hue, buttons
  use color tints instead of sprite swaps, dark text turns light. A per-menu pass (`MenuThemer`, attached to every
  `BaseUI`, the main menu and the pause menu) applies the same rules to elements without a Styler. Icons and
  illustrations are never touched.
- Shop status (every `PurchaseUI`: vacpack upgrades, gadgets, ranch): each entry gets a left bar and a corner tag,
  green bar and gold price when it can be bought now, orange bar and red price when newbucks or refinery materials
  are short, dimmed with the game's own "sold out" word when owned. The buy button turns green only when the purchase
  is possible.
- Planned next: co-op partner status, ranch alerts, then a Slimepedia layout rework.

## Requirements

- SRML **0.2.1** (SRMP is built against it; older SRML breaks SRMP's sync).
- SRMP 0.0.1584 in `SRML\Mods`.

## Install

Both players install it (the host does the ownership repair, everyone gets the Tabby fix):

1. Close the game.
2. Copy `SRMPFixes.dll` into `Slime Rancher\SRML\Mods\` next to `SRMP.dll`.

Uninstall: delete `SRML\Mods\SRMPFixes.dll`.

## Build

Check out the mod's branch, then:

```powershell
git switch sr-hud                 # or srmp-fixes, sr-boost
.\build.ps1                       # default Steam path
.\build.ps1 -GameDir "D:\...\Slime Rancher"
```

The script builds whichever mods are present in the checkout.

Uses the C# compiler shipped with Windows (.NET Framework 4), so no SDK is needed. It references the game, SRML,
Harmony and SRMP DLLs from the game install; none of them are stored in this repo. Output: `bin\<Mod>.dll`.
