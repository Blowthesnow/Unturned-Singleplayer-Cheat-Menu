# Unturned Singleplayer Cheat Menu

[中文说明](README.md) · [Changelog](CHANGELOG.md) · [Acceptance matrix](ACCEPTANCE.md) · [Security](SECURITY.md)

A bilingual Chinese/English BepInEx 5 in-game menu built specifically for **Unturned singleplayer worlds**. It scans the asset registry already loaded by the running game, so vanilla, map-provided, and Workshop items and vehicles can appear automatically. It brings player controls, item and vehicle discovery, favorites, map-scoped teleports, world-time and weather controls, crosshair interaction, and native-simulation movement into one focused overlay.

> **Singleplayer without BattlEye only.** This project does not disable, modify, or bypass BattlEye and does not support multiplayer servers.

**Default shortcut: press `End` after entering a singleplayer world to open or close the menu.**

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

### Player controls

- God mode and unlimited survival state.
- One-click healing, injury handling, and survival-stat refill.
- Independent health, food, water, immunity, stamina, and oxygen values.
- Custom experience and reputation changes, plus one-click max skills.

### Items

- Scans every loaded `ItemAsset` exposed by Unturned's current asset map, including vanilla, Workshop, and map-provided content.
- Filters by primary category, exact item type, origin, rarity, equipment slot, gun action, and multi-select fire mode.
- Searches by name, ID, GUID, or source, with pagination, game-generated icons, and quantities from `1–255`.

### Vehicles

- Scans loaded `VehicleAsset` entries and groups them by land vehicle, fixed-wing aircraft, helicopter, blimp, and boat.
- Uses official icons when available and renders model thumbnails when an icon is missing or unusable.
- Supports `128 × 96`, `192 × 144`, and `256 × 192` output, adjustable framing, per-configuration disk caching, and batches from `1–20` placed in front of the player.

### Favorites

- Persists item and vehicle favorites by asset GUID and keeps independent filters for the regular and favorites pages.
- Gives items and spawns vehicles directly from favorite cards; unavailable Workshop assets remain saved until they are loaded again.

### Teleports

- Provides map and list views with zoom, pan, player and saved-point markers, customizable marker shapes/colors, and map-scoped JSON persistence.
- Map-click teleport validates terrain or available building-top surfaces, checks standing clearance, and tries nearby safe candidates when the requested point is blocked.

### World controls

- Controls time, day/night, time freeze, full moon, airdrops, rain, snow, weather clearing, and loaded-asset rescanning.
- Dragging the time slider updates a live percentage preview immediately; the time change is applied through a short debounce so the control remains responsive.

### Crosshair interaction

- The Tools page supports Smart, Inspect, Repair, Teleport, Utility, and Delete modes, activated with the middle mouse button.
- Smart mode separates semantic target recognition from teleport coordinates, so a surface can still be inspected and a safe coordinate fallback can be used when no semantic target is available.
- The target HUD can show name, ID/GUID, and health/durability independently; range and deletion protection are configurable. Delete requires holding `Shift` while pressing the middle mouse button.

### Flight and noclip

- Uses Unturned's native movement-simulation path rather than repeatedly teleporting the player.
- Provides separate horizontal and vertical speed multipliers; default controls are `WASD` to move, `Space` to rise, and `Ctrl` to descend.
- Optional safe-exit recovery searches for a standable position when noclip is disabled inside blocked geometry.

### Overlay input and shortcut behavior

- While the menu is open, the plugin captures the native cursor and gameplay input; it reasserts that isolation after `PlayerUI` updates and restores the previous state when the menu closes or the singleplayer boundary ends.
- The menu shortcut accepts Unity polling, native input, and GUI-event paths with cross-frame de-duplication, preventing one physical press from toggling twice.
- Shortcut capture accepts keyboard keys, middle mouse, and side mouse buttons. Left and right mouse buttons are rejected, while `Ctrl`, `Alt`, `Shift`, and `Win` can be used as modifiers.
- The last main tab and teleport subview are persisted and restored the next time the menu opens.

## Runtime boundary

The menu only opens after confirming a loaded local player in a true `Singleplayer_` world where the current process is both client and server. It closes automatically if that condition stops being true.

The map setup checkbox named **singleplayer cheats** is not required. It controls Unturned's built-in command system; this plugin does not depend on `Provider.hasCheats`.

## Tested environment

| Component | Version |
| --- | --- |
| Unturned | `3.26.3.8` |
| Unity | `2022.3.62f3` |
| Runtime | Windows x64 / Unity Mono |
| BepInEx | `5.4.23.5` x64 |
| Plugin | `1.7.0` |

## Installation

The `Unturned-Singleplayer-Cheat-Menu-v1.7.0-Plugin-Only.zip` release asset contains the plugin only.

1. Exit Unturned.
2. Install BepInEx `5.4.23.5` x64 into the Unturned game root.
3. Extract the release asset into the directory containing `Unturned.exe`.
4. Verify that the DLL is located at:

   ```text
   BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll
   ```

   Deployment rule: keep exactly one active `UnturnedSingleplayerCheatMenu.dll` under `BepInEx\plugins`, at this canonical subdirectory path. Do not leave another same-named DLL in the plugins root or another subdirectory. Backups must use a non-`.dll` suffix so BepInEx cannot load duplicates and prevent future upgrades from replacing the active plugin.

5. Use Steam's launch-option dialog and explicitly choose the **no BattlEye** entry.
6. Enter a singleplayer world and press `End` after the player has loaded.

Do not use this plugin with BattlEye or on multiplayer servers.

## Configuration and saved data

Files are created under `BepInEx/config` after first use:

- `com.codex.unturned.singleplayer-cheat-menu.cfg`
- `UnturnedSingleplayerCheatMenu.favorites.json`
- `UnturnedSingleplayerCheatMenu.teleports.json`

The default shortcut is `End` and can be changed through `ToggleShortcut` or recaptured from the in-menu shortcut settings. The capture dialog accepts keyboard keys, middle mouse, and side mouse buttons; left and right mouse buttons are unavailable. Unsupported values are normalized back to `End`.

The top-right `EN` / `中文` button switches the language immediately and writes the choice back to `Interface.Language`; no restart is required.

`Interface.Language` initially defaults to `Auto`: Chinese Unturned languages use Chinese, while all other game languages use English. It can also be set explicitly to `English` or `Chinese`. Unknown values fall back to `Auto`.

Only assets successfully loaded by the current game process are discoverable. Missing subscriptions, failed downloads, unresolved dependencies, or map-specific assets that were not loaded cannot be synthesized by the plugin.

## Building

Game, Unity, and BepInEx assemblies are intentionally not committed.

```powershell
.\scripts\Prepare-References.ps1 -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Unturned'
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
dotnet run --project .\tests\FavoritesSerializationSmoke\FavoritesSerializationSmoke.csproj -c Release
dotnet run --project .\tests\ItemFilteringSmoke\ItemFilteringSmoke.csproj -c Release
dotnet run --project .\tests\LocalizationSmoke\LocalizationSmoke.csproj -c Release
dotnet run --project .\tests\MovementSpeedSmoke\MovementSpeedSmoke.csproj -c Release
dotnet run --project .\tests\PointToolActionSmoke\PointToolActionSmoke.csproj -c Release
dotnet run --project .\tests\ShortcutToggleSmoke\ShortcutToggleSmoke.csproj -c Release
dotnet run --project .\tests\TeleportSerializationSmoke\TeleportSerializationSmoke.csproj -c Release
dotnet run --project .\tests\VehicleThumbnailSettingsSmoke\VehicleThumbnailSettingsSmoke.csproj -c Release
```

The helper copies required references into the git-ignored `lib/` directory. The plugin is emitted to `artifacts/UnturnedSingleplayerCheatMenu.dll`.

## Verification

Release build, nine solution projects including eight Smoke projects, Plugin-Only package validation, retained runtime evidence, and remaining runtime boundaries are recorded separately in [ACCEPTANCE.md](ACCEPTANCE.md). Source/build/Smoke/package evidence is kept separate from fresh in-game interaction acceptance. No complete BepInEx package is published.

Published plugin DLL:

```text
SHA-256: FA500D0EC9E4E927797462CB72D713C1B6E409FE698F2F945C498F64BC31B3CF
```

## Disclaimer and license

This is an unofficial community project and is not affiliated with or endorsed by Smartly Dressed Games, Unturned, BattlEye, or BepInEx.

Plugin source code is available under the [MIT License](LICENSE). Unturned, Unity, BepInEx, and any third-party files retain their respective licenses and ownership.
