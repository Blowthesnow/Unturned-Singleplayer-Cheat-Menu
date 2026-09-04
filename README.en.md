# Unturned Singleplayer Cheat Menu

[中文说明](README.md) · [Changelog](CHANGELOG.md) · [Security](SECURITY.md) · [Sponsor](https://afdian.com/a/Blowthesnow)

A bilingual Chinese/English BepInEx 5 in-game menu whose gameplay support target is **Unturned singleplayer worlds**. It scans the asset registry already loaded by the running game, so vanilla, map-provided, and Workshop items and vehicles can appear automatically. The current source version is `2.0.0`.

> Gameplay features are designed, tested, and supported only for Unturned singleplayer worlds. The plugin may remain installed while you choose a BattlEye launch, and the menu may open in a multiplayer client session if BepInEx successfully loads and the local player exists. Gameplay-feature use in multiplayer/server sessions is unsupported and at your own risk. Workshop translation is a local content feature: it is not controlled by the singleplayer/multiplayer gameplay gate or multiplayer-risk acknowledgement, and entering multiplayer does not cancel it. The project does not stop, disable, modify, or bypass BattlEye, and does not guarantee that BattlEye will allow BepInEx, this plugin, or the connection to continue.

**Default shortcut: press `End` after the local player has spawned to open or close the menu.**

> **Screenshot note: the project screenshots currently show the 1.1 interface; screenshots will be updated together for 2.0.**

![Items tab scanning vanilla, map-provided, and Workshop assets](docs/images/items-tab.png)

<details>
<summary><strong>View more interface screenshots</strong></summary>

### Player stats and skills

![Character tab with survival stats, experience, reputation, and skills](docs/images/character-tab.png)

### Vehicle browser and spawning

![Vehicles tab with automatic discovery and rendered thumbnails](docs/images/vehicles-tab.png)

### Favorites

![Favorites tab for saved items and vehicles](docs/images/favorites-tab.png)

### Teleport points

![Teleports tab with named map-scoped positions](docs/images/teleports-tab.png)

### Time, weather, and world events

![World controls for time, weather, full moon, airdrops, and rescanning](docs/images/world-tab.png)

</details>

## Features

- Player controls: god mode, infinite survival stats, healing, configurable health/food/water/immunity/stamina/oxygen, experience, reputation, and max skills.
- Items: automatic loaded-asset scan, primary and exact-type filters, origin, rarity, equipment slot, gun-action and multi-select fire-mode filters, name/ID/GUID/source search, pagination, game-generated thumbnails fitted to their real texture aspect ratio, and quantities from `1–255`.
- Vehicles: automatic loaded-asset scan, engine categories, search, pagination, aspect-correct model thumbnails from a uniform generated camera (asset-authored `Icon2/Icon` framing is ignored), configurable `128 × 96`, `192 × 144`, or `256 × 192` output with framing control and per-configuration disk caching, and batches from `1–20` placed in front of the player.
- Favorites: persistent item and vehicle favorites keyed by asset GUID, with direct actions from the favorites tab.
- Teleports: map and list views, zoom/pan, player and saved-point markers, safe map-click landing on terrain or available building tops, customizable marker shapes/colors, and persistent map-scoped positions.
- World controls: time slider, day/night, time freeze, full moon, airdrop, rain, snow, weather clearing, and asset rescan.
- Tools: crosshair interaction with Smart, Inspect, Repair, Teleport, Utility, and Delete modes; middle-mouse activation; configurable target HUD fields; and Shift-guarded deletion.
- Movement: native-simulation flight and noclip with separate horizontal/vertical speed multipliers and safe-exit recovery.
- Minimap: circular or square HUD, editable anchor/offset/size/radius, player/group/teleport/world/item markers, six rarity filters, floor indicators, and native-map/realtime/fallback background modes.
- Automatic-pickup integration: optional minimap-range and minimap-rarity linkage, both disabled by default so existing pickup behavior does not change implicitly.

Menu readiness and gameplay authorization are separate boundaries. The main menu can open in a connected singleplayer or multiplayer client after the local player exists. Singleplayer gameplay is immediately available. Multiplayer and non-singleplayer listen hosts retain a persistent unsupported warning, and the first gameplay action or gameplay-automation enablement requests acknowledgement for that connection only. Disconnecting, changing servers, reconnecting to a map, or recreating the local player invalidates the acknowledgement.

| Session | Main menu | Workshop translation | Gameplay features | Snapshot Infinite Storage |
| --- | --- | --- | --- | --- |
| Frontend/loading | Closed | An existing task keeps pumping | Paused | Off |
| Singleplayer | Open | Available | Supported | Available |
| Multiplayer, unacknowledged | Open with warning | Available and uninterrupted | Blocked | Off |
| Multiplayer, acknowledged | Open with persistent warning | Available and uninterrupted | Unsupported best-effort | Off |
| U3DS dedicated server | No client UI | Client translation is not initialized | Inert/no-op | Off |

Workshop scanning, cache use, requests, batch completion, and local text application do not pass through gameplay authorization. Entering multiplayer, closing the menu, changing connections, or changing acknowledgement state does not cancel the active batch. Asset refreshes during a batch coalesce into one deferred rescan after it completes.

The last main tab and teleport subview are persisted and restored the next time the menu opens.

The map setup checkbox named **singleplayer cheats** is not required. It controls Unturned's built-in command system; this plugin does not depend on `Provider.hasCheats`.

## Tested environment

| Component | Version |
| --- | --- |
| Unturned | `3.26.3.8` |
| Unity | `2022.3.62f3` |
| Runtime | Windows x64 / Unity Mono |
| BepInEx | `5.4.23.5` x64 |
| Plugin | `2.0.0` |

## Installation

The `Unturned-Singleplayer-Cheat-Menu-v2.0.0-Plugin-Only.zip` release asset contains the plugin only.

1. Exit Unturned.
2. Install BepInEx `5.4.23.5` x64 into the Unturned game root.
3. Extract the release asset into the directory containing `Unturned.exe`.
4. Verify that the DLL is located at:

   ```text
   BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll
   ```

   Deployment rule: keep exactly one active `UnturnedSingleplayerCheatMenu.dll` under `BepInEx\plugins`, at this canonical subdirectory path. Do not leave another same-named DLL in the plugins root or another subdirectory. Backups must use a non-`.dll` suffix so BepInEx cannot load duplicates and prevent future upgrades from replacing the active plugin.

5. Continue using the existing Steam/Unturned launch flow. This runtime-boundary change does not add or redesign a launcher.
6. Press `End` after the local player has loaded. Singleplayer is the only supported gameplay environment; multiplayer exposes an unsupported, at-your-own-risk per-connection acknowledgement and never enables Snapshot storage.

Launch behavior remains unchanged. Successfully starting the game through an existing route does not guarantee that BattlEye will permit BepInEx, the plugin, or the server connection to continue.

## Configuration and saved data

Files are created under `BepInEx/config` after first use:

- `com.codex.unturned.singleplayer-cheat-menu.cfg`
- `UnturnedSingleplayerCheatMenu.favorites.json`
- `UnturnedSingleplayerCheatMenu.teleports.json`
- `AutoPickup.rules.json`
- `UnturnedSingleplayerCheatMenu/InfiniteStorageSnapshots/v1/<first-24-hex-world-hash>/<first-24-hex-player-hash>/snapshot.json` (96-bit directory prefixes keep Unity Mono sidecars below its path limit; the document still validates the full world/player identity)

The default shortcut is `End` and can be changed through `ToggleShortcut`.

The never-released `UnturnedSingleplayerCheatMenu/InfiniteStorage/` format is retired. The current runtime does not read, claim, migrate, move, or delete files in that tree. If the new path is absent, Infinite Storage starts empty.

If `snapshot.json.reconciliation-required` exists beside a Snapshot document, Infinite Storage and AutoPickup remain locked to contain an unproven native/storage state. Inspect the primary, `.bak`, retained temporary candidates, and world-drop incident using the living reference before manually clearing the marker and restarting; do not overwrite the save blindly.

The top-right `EN` / `中文` button switches the language immediately and writes the choice back to `Interface.Language`; no restart is required.

`Interface.Language` initially defaults to `Auto`: Chinese Unturned languages use Chinese, while all other game languages use English. It can also be set explicitly to `English` or `Chinese`. Unknown values fall back to `Auto`.

The Workshop localization page accepts up to two API keys. The first row keeps the `+` control, additional rows can be removed with `×`, and one worker is created per key to consume translation batches in parallel. Batch size is configurable from `1–100`; the page remains vertically scrollable as rows are added.

Only assets successfully loaded by the current game process are discoverable. Missing subscriptions, failed downloads, unresolved dependencies, or map-specific assets that were not loaded cannot be synthesized by the plugin.

## Building

Game, Unity, and BepInEx assemblies are intentionally not committed.

```powershell
.\scripts\Prepare-References.ps1 -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Unturned'
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
Get-ChildItem .\tests -Recurse -Filter '*Smoke.csproj' | Sort-Object FullName | ForEach-Object {
    dotnet run --project $_.FullName -c Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke failed: $($_.FullName)" }
}
```

The helper copies required references into the git-ignored `lib/` directory. The plugin is emitted to `artifacts/UnturnedSingleplayerCheatMenu.dll`.

## Verification

`v2.0.0` is the first formal release since `v1.7.0`. Intermediate source checkpoints and the cumulative 2.0 changes, build, and Smoke evidence are recorded in the [main changelog](CHANGELOG.md); the v1.8 reverse-recovery boundary remains separate in the [v1.8 recovery log](docs/V1.8_RECOVERY_CHANGELOG.md). The 2.0 Release build completed with zero warnings and zero errors, and all 27 recursively discovered Smoke projects were individually rebuilt and run successfully. Source/API, package, deployment, and real-game evidence remain separate layers.

v2.0.0 release artifacts:

```text
DLL SHA-256: 93FED6974CBF793B8619AE66D2846DFE60F1532AF5E030C2C7DA62EED059FECD
Plugin-Only ZIP SHA-256: 338D3570FD6582C5F4CC638C2CA14B72BF4E9DC3108BFD88953417621CA70C85
```

The final 2.0 DLL/package was not deployed or tested in a restarted game during release preparation; automated gates do not substitute for real singleplayer acceptance.

## Disclaimer and license

This is an unofficial community singleplayer-gameplay mod and is not affiliated with or endorsed by Smartly Dressed Games, Unturned, BattlEye, or BepInEx. Multiplayer/server gameplay and BattlEye loading are unsupported and not guaranteed; back up saves and understand the risks of kicks, rollbacks, disconnects, or account penalties.

Plugin source code is available under the [MIT License](LICENSE). Unturned, Unity, BepInEx, and any third-party files retain their respective licenses and ownership.
## Current architecture verification

The source now uses one `PluginUiRoot`; `RuntimeUpdatePatch` hooks `Provider/MenuUI/PlayerUI.Update` and dispatches one de-duplicated Update/LateUpdate per frame to `PluginRuntimeContext`. Like the working legacy branch, this static bridge does not depend on transient BepInEx or driver components that Unturned destroys during its startup scene transition. `PluginUiRoot` recreates itself if the bootstrap scene removes it. It also includes input-capture release, draggable/clamped window position persistence, and teleport-point views isolated to the current map. The production source no longer contains the legacy IMGUI menu or `UnityEngine.IMGUIModule` reference.

After a Release build, recursively discover and run every `tests/**/*Smoke.csproj` project. Report source/API, build, Smoke, deployment hash, BattlEye launch, BepInEx/plugin discovery, multiplayer connection, translation continuity, and real-game interaction separately. None of these layers substitutes for real singleplayer gameplay acceptance.
