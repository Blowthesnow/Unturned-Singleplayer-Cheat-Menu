# Unturned Singleplayer Cheat Menu

[中文说明](README.md) · [Releases](https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu/releases) · [Changelog](CHANGELOG.md) · [Security](SECURITY.md) · [Sponsor](https://afdian.com/a/Blowthesnow)

![Version](https://img.shields.io/badge/version-2.0.0-0078D4) ![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23.5-2E8B57) ![Smoke](https://img.shields.io/badge/Smoke-30%2F30%20PASS-2E8B57) ![License](https://img.shields.io/badge/license-MIT-blue)

**A bilingual BepInEx 5 toolkit built for Unturned singleplayer worlds.** Instead of relying on a hard-coded item or vehicle catalog, it reads the assets already loaded by the running game. Vanilla content, map-provided assets, and Workshop mods appear according to the current installation and world.

Beyond asset discovery, version 2.0 combines runtime localization, player controls, favorites, inventory arrangement, automatic pickup, Snapshot Infinite Storage, crosshair interaction, creature health bars, flight and noclip, map teleportation, a configurable minimap, and time/weather controls.

**Default shortcut: press `End` after the local player has spawned.**

> Gameplay features officially target singleplayer worlds. Asset localization is an independent local-content feature. See “Runtime boundaries” below for the complete session matrix.

## Unique core capability: runtime localization for vanilla and Workshop assets

**Across the publicly discoverable Unturned plugin ecosystem, we found no second plugin dedicated to this complete path: scan the loaded vanilla and Workshop assets at runtime → translate in batches → cache by asset and field → apply text to the plugin catalog and Unturned display paths.** This is not a prebuilt static language pack or an offline map-file converter; it follows the content actually loaded by the current game process.

| Common approach | What it handles | Key difference from this project |
| --- | --- | --- |
| [Native Unturned localization](https://docs.smartlydressedgames.com/en/latest/assets/asset-definitions.html#localization) / Workshop language packs | Pre-authored per-language `.dat` files beside known assets | Requires maintained static files and does not automatically fill missing text for arbitrary assets loaded in the current run |
| [Unturned Map Translator](https://steamcommunity.com/sharedfiles/filedetails/?id=3365019362) | Selects a map outside the game and generates or overwrites language files | An offline file converter rather than an in-game runtime plugin |
| [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) | Generic text/resource translation across many Unity games | Does not specialize in enumerating Unturned `Assets`, classifying vanilla/Workshop origin, or maintaining a field-level asset catalog |
| [Server chat translation plugins](https://imperialplugins.com/Unturned/Products/Translation) | Player chat messages and channels | Does not translate Item, Vehicle, Quest, NPC, Object, or Vendor assets |

- Scans item names and descriptions, vehicle names, action text and hints, NPC names/dialogue/responses, quest names and descriptions, loaded `ObjectAsset` names and interaction-dialogue display names, and vendor text.
- Processes only supported types and fields that the current process successfully loaded into the `Assets` mapping; it does not promise every unloaded asset, map file, or piece of map text.
- Isolates cache entries by asset type, GUID, field, and cache schema, so fields on the same asset remain independent.
- Preserves existing Chinese localization and protects rich-text tags, formatting placeholders, and dialogue controls.
- Provides scanning, translation queues, batching, retries, cache hits, forced retranslation, progress reporting, and local text application.
- Supports up to two API keys working in parallel, with batch sizes from `1–100`.
- Includes 18 provider/protocol presets: Tencent TMT, Baidu Translate, Google Cloud, Microsoft Translator, DeepL, Youdao, OpenAI, OpenAI Compatible, Azure OpenAI, Anthropic, Gemini, Ollama, OpenRouter, DeepSeek, SiliconFlow, LM Studio, Tencent Hunyuan, and Baidu Qianfan.
- Never modifies original `.dat` or AssetBundle files. Entering multiplayer, closing the menu, or changing connections does not automatically cancel an active translation batch.

<details>
<summary><strong>View screenshots: asset scanning and localization settings (2)</strong></summary>

**Translation status and cache**

![Runtime vanilla and Workshop asset localization with cache, parallel keys, and progress](docs/images/v2/workshop-localization.png)

**Provider and keys**

![Translation provider, endpoint, parallel keys, and batch settings](docs/images/v2/workshop-localization-api.png)

</details>

> Search boundary: the “no second plugin found” statement is based on public GitHub, Thunderstore, Nexus, Steam Workshop, and Unturned plugin-market searches through 2026-09-05. Private or unindexed implementations are outside that claim.

## Complete v2.0.0 feature set

### Player controls

- God mode and infinite survival stats.
- One-click healing, injury treatment, and survival-stat refill.
- Exact health, food, water, immunity, stamina, and oxygen values.
- Custom experience/reputation changes and one-click maximum skills.

<details>
<summary><strong>View screenshot: player controls and skills</strong></summary>

![Player survival stats, experience, reputation, and skills](docs/images/v2/player-status.png)

</details>

### Item catalog

- Reads the currently loaded `ItemAsset` set, including vanilla, map-provided, and Workshop content.
- Filters by primary category, exact item type, origin, rarity, equipment slot, gun action, and multiple fire modes.
- Searches by name, ID, GUID, and source, with pagination and independent state for the normal/favorites catalogs.
- Uses game-generated thumbnails and localized names, fitted to each texture's real aspect ratio.
- Gives configurable quantities from `1–255`.

<details>
<summary><strong>View screenshot: loaded item catalog</strong></summary>

![Loaded vanilla, map-provided, and Workshop item catalog](docs/images/v2/item-browser.png)

</details>

### Vehicle catalog

- Reads the currently loaded `VehicleAsset` set and groups land vehicles, fixed-wing aircraft, helicopters, airships, and boats.
- Supports name/ID/GUID/source search, pagination, and batches from `1–20`.
- Generates every thumbnail from the real model with one auto-framing camera and the highest available LOD rather than asset-authored `Icon2/Icon` framing.
- Supports `128 × 96`, `192 × 144`, and `256 × 192` output, framing control, and GUID/render-configuration disk caching.
- Places vehicle batches in a ground array in front of the player instead of overlapping them.

<details>
<summary><strong>View screenshot: loaded vehicle catalog</strong></summary>

![Loaded vehicle categories, search, thumbnails, and batch spawning](docs/images/v2/vehicle-browser.png)

</details>

### Favorites

- Adds/removes item and vehicle favorites directly from catalog cards.
- Persists favorites by asset GUID without deleting temporarily unloaded Workshop assets.
- Keeps separate item/vehicle category, search, quantity, and pagination state, with direct give/spawn actions.

<details>
<summary><strong>View screenshot: item and vehicle favorites</strong></summary>

![Favorites categories and saved item cards](docs/images/v2/favorites-items.png)

</details>

### Snapshot Infinite Storage

- Preserves exact native `{id, amount, quality, state[]}` data rather than conflating quality, state, or instance count.
- Separates storage by world, Steam ID, and character slot with versioned paths, strict identity validation, and collision checks.
- Supports Store All, exact-bucket withdrawal, search, pagination, default templates, and permanent-destruction confirmation.
- Provides independently configurable same-DLL Crafting, Ammo Refill, and Decomposition adapters.
- Can receive automatic pickup through the Snapshot-priority path with conservative background persistence and world-drop handoff.
- Protects primary, backup, candidate-history, and reconciliation-marker paths with atomic writes, TOCTOU checks, and fail-closed recovery.

### Inventory arrangement

- Previews and arranges the current container or all carried pages with presets such as Compact.
- Supports rotation, cross-page placement, equipment/hotkey protection, compression-only, merge-only, and undo.
- Ctrl/Shift click can consolidate same-asset items across an open storage container.
- Cancels when preflight cannot place everything and preserves rollback boundaries for apply, native drag, and failure paths.

<details>
<summary><strong>View screenshot: inventory arrangement</strong></summary>

![Inventory presets, scope, protection, compression, merging, and undo](docs/images/v2/inventory-arrange.png)

</details>

### Automatic pickup

- Configurable range, scan frequency, per-cycle request limit, menu-open pause, and death pause.
- Post-drop suppression duration, quality threshold, six rarity levels, allow/block lists, and diagnostic counters.
- Optional minimap-range and minimap-rarity linkage, both disabled by default.
- Selectable native-inventory or Snapshot-priority delivery with handling for legal `amount=0`, timeouts, uncertain native outcomes, and recovery.

<details>
<summary><strong>View screenshot: automatic-pickup rules</strong></summary>

![Automatic-pickup range, frequency, quality, rarity, and list settings](docs/images/v2/auto-pickup.png)

</details>

### Crosshair interaction

- Smart, Inspect, Repair, Teleport, Utility, and Delete modes activated with middle mouse.
- Smart mode selects one semantic action for the current target; no-target paths can still inspect world surfaces or use safe coordinate fallback.
- Delete requires `Shift`; name, ID/GUID, and health/durability HUD fields are individually configurable.

<details>
<summary><strong>View screenshot: crosshair interaction tool</strong></summary>

![Crosshair interaction modes, target fields, and range settings](docs/images/v2/crosshair-tools.png)

</details>

### Creature health bars

- Shows current and maximum health for crosshair-targeted or actually damaged zombies and animals.
- Shares the plugin font, accent, canvas lifecycle, and scene cleanup.

<details>
<summary><strong>View screenshot: in-game creature health bars</strong></summary>

![In-game zombie health bars](docs/images/v2/creature-health-bars.png)

</details>

### Movement tools

- Flight and noclip stay inside Unturned's native movement simulation with separate horizontal/vertical multipliers.
- View-direction flight reads configured movement bindings; W/S follows camera pitch/yaw, while Space/Ctrl keeps fixed world-Y priority.
- Diagonal movement is normalized and zero/non-finite camera directions fall back safely.
- Disabling noclip searches for a safe position; leaving authorization, dying, or entering a vehicle restores native movement state.

<details>
<summary><strong>View screenshot: flight and noclip</strong></summary>

![Flight mode, noclip, speed multipliers, and safe-exit settings](docs/images/v2/movement-tools.png)

</details>

### Map and teleport points

- Saves multiple named positions with map-scoped coordinates, orientation, and creation time.
- Switches between waypoint list and GPS map; the map supports zoom, pan, player position, and saved markers.
- Map clicks resolve terrain, available rooftops, and nearby safe fallback positions.
- Waypoints support star, square, circle, and diamond shapes plus preset/custom colors.

<details>
<summary><strong>View screenshots: map and teleport points (2)</strong></summary>

**Named waypoint list**

![Map-scoped named teleport points](docs/images/v2/teleport-points.png)

**GPS map**

![Zoomable map GPS with player and saved markers](docs/images/v2/teleport-map.png)

</details>

### Minimap

- Circular/square HUD, anchor, offset, size, visible radius, and direct drag/scale layout editing.
- North-up or view-relative rotation with compass and world coordinates.
- Player, group-native, waypoint, zombie, animal, other-player, and ground-item markers with six rarity levels and floor handling.
- Native-map crop, realtime surface, realtime height slice, and fallback grid backgrounds with configured building/cave detection.
- Bounded zombie-region retention prevents marker loss at scan boundaries without unbounded memory growth.

<details>
<summary><strong>View screenshots: minimap settings and in-game layouts (3)</strong></summary>

**Settings**

![Minimap shape, anchor, background, scan, and marker settings](docs/images/v2/minimap-settings.png)

**Circular realtime-surface HUD**

![Circular realtime-surface minimap](docs/images/v2/minimap-hud-circle.png)

**Square native-map-crop HUD**

![Square native-map-crop minimap](docs/images/v2/minimap-hud-square.png)

</details>

### Time, weather, and world events

- Bilingual 12-hour time ring with drag, wheel, keyboard, day, and night controls.
- Time freeze, forced full moon, and airdrop requests.
- Rain, blizzard, current-weather clearing, and weather-scheduler shutdown.
- Manual rescan of currently loaded assets.

<details>
<summary><strong>View screenshot: time, weather, and world events</strong></summary>

![12-hour time ring, weather, full moon, and world events](docs/images/v2/world-controls.png)

</details>

### Interface, shortcuts, and persistence

- Instant Chinese/English switching, Windows system-font selection, and persistent accent color.
- Fluent-inspired Legacy uGUI controls, responsive wide/narrow layouts, unified HUD/modal/tooltip/toast styling, and frame-driven motion.
- One persistent `PluginUiRoot` owns menus, HUDs, input, modals, scene transitions, and deterministic cleanup.
- Persists main page, tool subpage, teleport view, window position, and feature settings; global shortcuts include conflict detection and native-input isolation.

## What changed since v1.7.0

Versions `1.8.0`, `1.9.0`, `1.95.0`, `1.98.0`, and `1.995.0` were unreleased source checkpoints. Version `2.0.0` is the first formal release to deliver their combined changes to users upgrading directly from `1.7.0`.

### Newly delivered in a formal release

- Runtime scanning, translation queues, batching, retries, caching, progress, and local UI application for loaded vanilla and Workshop assets.
- Field-level item, vehicle, action, NPC-dialogue, quest, loaded-`ObjectAsset` name/interaction-dialogue, and vendor localization with rich-text/placeholder protection.
- Inventory preview, arrangement, compression, merging, cross-page placement, protected equipment/hotkeys, quick transfer, rollback, and undo.
- Guarded automatic pickup with range, scheduling, request limits, pause gates, quality/rarity filters, allow/block lists, diagnostics, minimap integration, and Snapshot priority.
- A global-shortcut framework with conflict detection, native-input isolation, and persistent menu/inventory-arrangement bindings.
- Transactional BepInEx configuration initialization with exact rollback for first-run, empty, and existing files when binding or saving fails.
- One max-radius/max-frequency nearby-item snapshot shared by the minimap and AutoPickup, avoiding duplicate scans while keeping display and pickup rules separate.
- Snapshot Infinite Storage with same-DLL Crafting, Ammo Refill, and Decomposition adapters.
- Creature health bars, item/vehicle hover details, and ammunition compatibility information.
- Editable circular/square minimap with native/realtime backgrounds, world markers, rarity/floor handling, rotation, and bounded zombie-region retention.
- Fluent-inspired Legacy uGUI controls, accent color, system fonts, responsive layout, and frame-driven motion.

### Major changes

- The unreleased early Infinite Storage design was replaced by the Snapshot Kernel with exact native item state, strict session identity, atomic primary/backup files, and persistent reconciliation.
- Localization expanded to 18 provider/protocol presets, two parallel API keys, and `1–100` batch sizes; its lifecycle is independent of gameplay authorization.
- Item/vehicle thumbnails now preserve real aspect ratio; every vehicle render uses a consistent auto-framing camera, highest available LOD, and configuration-scoped cache.
- The time slider became a bilingual 12-hour time ring.
- UI ownership moved to a persistent root with responsive shells, shared fonts/theme, and deterministic view cleanup.
- Menu readiness and gameplay authorization are separate; live session status and per-connection multiplayer acknowledgement were added while singleplayer remains the supported gameplay target.
- Snapshot-priority AutoPickup persistence became one background operation, and one native craft request now shares one outer Snapshot transaction.

### Fixes and data-safety hardening

- Fixed Snapshot quality/state/count conflation, malformed documents, external writes, replacement races, rollback, and uncertain-commit paths.
- Fixed AutoPickup duplication/loss risks around legal `amount=0`, timeouts, uncertain native outcomes, storage fallback, and reconciliation.
- Fixed unnecessary inventory rebuilds, lost hotkey bindings, and undo journals surviving connection changes.
- Repaired view-direction flight, vertical-control priority, diagonal speed, and local flight-viewmodel tilt for the current Unturned runtime.
- Removed global Crafting `SearchContents`/`forceAddItem` interference with attachment slots, rarity display, rewards, and unrelated native grants.
- Fixed unresolved asset origins being mislabeled as Workshop, malformed bare `<color>` tags, localization gaps, and scene/Canvas/input/modal/tooltip/toast lifetime residue.
- Bound stale Snapshot background callbacks to their original connection and exact `ItemDrop`, so they cannot act on the current world after a session change.
- Moved the Workshop localization Pump to the first isolated runtime step so other UI/session failures cannot starve completion or deferred rescans.

- Fixed compatible Workshop ammunition donors in inventory arrangement and Snapshot refill while preserving round conservation and rejecting indirect incompatible conversions.
- Fixed minimap orientation, native-map edge clipping, Terrain materials, cliffs, tree canopies, low-quality water, and background recovery after native teleportation.
- Refined responsive forms, fixed translation progress/actions above scrolling settings, collapsible provider/cache controls, and the two-column time/weather layout.
- Fixed interrupted tooltip, selectable, and toast transitions; diagnostic minimap captures are disabled by default.

## Runtime boundaries

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

`v2.0.0` is the first formal release since `v1.7.0`. Intermediate source checkpoints and the cumulative 2.0 changes, build, and Smoke evidence are recorded in the [main changelog](CHANGELOG.md); the v1.8 reverse-recovery boundary remains separate in the [v1.8 recovery log](docs/V1.8_RECOVERY_CHANGELOG.md). The 2.0 Release build completed with zero warnings and zero errors, and all 30 recursively discovered Smoke projects were individually rebuilt and run successfully. Source/API, package, deployment, and real-game evidence remain separate layers.

v2.0.0 release artifacts:

```text
DLL SHA-256: 79AC75876C0B3B829188BB02EEB2DF5C32F04BF1AC7C025D695B44389B33F192
Plugin-Only ZIP SHA-256: 92CFA47EB93A68A98EC122EEDB67EDC6D59F7E0A290E0207F72500009D7049D9
```

The final 2.0 DLL/package was not deployed or tested in a restarted game during release preparation; automated gates do not substitute for real singleplayer acceptance.

## Disclaimer and license

This is an unofficial community singleplayer-gameplay mod and is not affiliated with or endorsed by Smartly Dressed Games, Unturned, BattlEye, or BepInEx. Multiplayer/server gameplay and BattlEye loading are unsupported and not guaranteed; back up saves and understand the risks of kicks, rollbacks, disconnects, or account penalties.

Plugin source code is available under the [MIT License](LICENSE). Unturned, Unity, BepInEx, and any third-party files retain their respective licenses and ownership.
## Current architecture verification

The source now uses one `PluginUiRoot`; `RuntimeUpdatePatch` hooks `Provider/MenuUI/PlayerUI.Update` and dispatches one de-duplicated Update/LateUpdate per frame to `PluginRuntimeContext`. Like the working legacy branch, this static bridge does not depend on transient BepInEx or driver components that Unturned destroys during its startup scene transition. `PluginUiRoot` recreates itself if the bootstrap scene removes it. It also includes input-capture release, draggable/clamped window position persistence, and teleport-point views isolated to the current map. The production source no longer contains the legacy IMGUI menu or `UnityEngine.IMGUIModule` reference.

After a Release build, recursively discover and run every `tests/**/*Smoke.csproj` project. Report source/API, build, Smoke, deployment hash, BattlEye launch, BepInEx/plugin discovery, multiplayer connection, translation continuity, and real-game interaction separately. None of these layers substitutes for real singleplayer gameplay acceptance.

## Support development

If this plugin helps you play or localize Unturned, you can support continued development on [Afdian](https://afdian.com/a/Blowthesnow). Sponsorship is entirely optional.

<p><a href="https://afdian.com/a/Blowthesnow"><img src="docs/images/afdian-Blowthesnow.jpg" alt="Blowthesnow Afdian sponsorship QR code" width="320" /></a></p>
