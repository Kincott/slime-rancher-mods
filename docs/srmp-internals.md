# SRMP internals (what we learned by decompiling)

Notes from decompiling SRMP 0.0.1584 (author SatyPardus) and the game's `Assembly-CSharp.dll`, 2026-11-20.
The decompiled code is not in this repo; regenerate it with the recipe at the end.

## Install layout

- Game: `C:\Program Files (x86)\Steam\steamapps\common\Slime Rancher`
- SRML: `SlimeRancher_Data\Managed\SRML.dll`, its libs (0Harmony 2.2.1, ...) in `SRML\Libs`
- SRMP: `SRML\Mods\SRMP.dll`, data and saves in `SRMP\`, logs in `SRMP\Logs\log-*.txt`
- Game log: `%USERPROFILE%\AppData\LocalLow\Monomi Park\Slime Rancher\Player.log`

## SRML version

SRMP 0.0.1584 is compiled against **SRML 0.2.1.0**. With the older 0.1.8.2 it throws
`TypeLoadException ... ObjectExtensions, SRML, Version=0.2.1.0` inside `NetworkRegion.AddPlayer`, so a joining
player never receives a region's actors (missing slimes and structures) and the host cannot see the client.
Installer: https://github.com/SlimeRancherModding/SRML/releases/tag/v0.2.1 (`SRMLInstaller_0.2.1c.zip`).
Every player needs it.

## Ownership model

- Every `NetworkRegion` and `NetworkActor` has a `byte Owner` (0 = nobody). `IsLocal` means `Owner == Globals.LocalID`.
- `NetworkLandplot.IsLocal` is its region's `IsLocal`: corrals belong to whoever owns the region.
- A non-local actor is frozen on that side: `NetworkActor.Update` zeroes its velocity and overwrites its position.
- Regions: the host always takes ownership on `Region.Unproxy`; a client only if `Owner == 0`. `Region.Proxy` drops it.
- Actors: `OnEnable` takes ownership if `Owner == 0`; `OnDisable` (hibernation deactivates actors) drops it.
  `Vacuumable.capture` takes ownership. On a client `TakeOwnership` is only a request, applied when the host
  answers; the host grants every request.
- Player ids: lowest free id (`NetworkServer`), so a reconnecting player often gets the same id back.

## Known SRMP bugs

| Bug | Where | Status |
|---|---|---|
| **Data loss**: world teardown on quit sends `PacketActorDestroy` for every actor, the host destroys them and saves. Happened 2026-11-20 17:35 (save shrank 352 KB to 117 KB). | `Patches/Identifiable_OnDestroy` (sender), `NetworkHandlerServer.OnActorDestroy` (host) | Fixed by `TeardownDestroyFix` (sender guard + host 3 s delay, dropped on disconnect) |
| Disconnect never releases the player's regions and actors | `NetworkServer.OnDisconnected` | Worked around by `OrphanOwnershipFix` |
| `PlortCollector.DoCollection` skipped unless the corral's region is local | `Patches/PlortCollector_DoCollection` | By design; consequence of the bug above |
| Releasing a region or actor: host re-takes it, then broadcasts `Owner = 0` after (Unreliable), so clients see 0 | `NetworkHandlerServer.OnRegionOwner` / `OnActorOwner` | Fixed by `OwnershipBroadcastFix` (handlers replaced) |
| Host sends packets to itself (`NoConnection` spam), e.g. the full actor list on every region load | `Region_Unproxy` → `NetworkRegion.AddPlayer(Globals.LocalPlayer)` → `NetworkServer.SendPacket` | Fixed by `SelfSendFix` |
| Achievements disabled in multiplayer (on purpose): `Globals.DisableAchievements` makes SRMP skip `AchievementsDirector.Update` / `LateUpdate`; stats still count | `NetworkServer` (host), `SRMP.OnGameLoaded` (client) | Re-enabled by `AchievementsFix` at the user's request |
| Logs `component.IsLocal` before its null check (NRE if an object has no `NetworkActor`) | `Patches/Vacuumable_capture` | Not fixed, never seen in logs |
| `OnPlayerFX` / `SetVacAudio` / `SetJetpackAudio` NRE on the host | `NetworkHandlerServer`, `NetworkPlayer` | Seen with SRML 0.1.8; recheck on 0.2.1 |

## Open questions

- Why the CellDirector largo index keeps destroyed largos (the Tabby `AweTowardsLargos` NRE). `GhostLargoFix`
  removes the symptom; the root cause in SRMP's actor removal is not found.
- If a collector still does nothing with both players connected and no disconnect, the cause is something else.

## Saves

- Saves are `%USERPROFILE%\AppData\LocalLow\Monomi Park\Slime Rancher\<timestamp>_<Name>_<n>.sav` (+ `.mod` from SRML).
- The game keeps 5 per game (`AutoSaveDirector.MAX_AUTOSAVES`) and deletes the oldest on each save, including on quit.
- It loads the save with the highest **save number stored inside the file**, not by filename or file date. To roll
  back, move the newer `.sav`/`.mod` pairs out of the folder; renaming or copying the old one does not work.
- Steam Cloud is on by default and re-downloads moved-out `.sav` files at the next launch (not the `.mod`). Turn it
  off first (Properties > General, stored as `cloudenabled "0"` for app 433340 in
  `Steam\userdata\<id>\7\remote\sharedconfig.vdf`), and keep it off until the restored game has saved more times
  than the damaged saves it replaced.
- Backup of the 2026-11-20 incident: `%USERPROFILE%\Documents\SlimeRancher-save-backup-2026-11-20`.

## Decompile recipe

No .NET SDK on this machine (only the .NET 9 runtime), so use the self-contained `ilspycmd.win-x64` NuGet package:

```bash
V=11.2.0.10050
curl -sSLo ilspy.nupkg "https://api.nuget.org/v3-flatcontainer/ilspycmd.win-x64/$V/ilspycmd.win-x64.$V.nupkg"
unzip -q ilspy.nupkg -d ilspy   # exe in ilspy/tools/any/win-x64/ilspycmd.exe
G="C:/Program Files (x86)/Steam/steamapps/common/Slime Rancher"; M="$G/SlimeRancher_Data/Managed"
ilspycmd.exe -r "$M" -r "$G/SRML/Libs" -p -o out/srmp "$G/SRML/Mods/SRMP.dll"   # whole project
ilspycmd.exe -r "$M" -t PlortCollector "$M/Assembly-CSharp.dll"                 # one game type
```

Keep the output outside the repo.
