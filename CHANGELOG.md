# Changelog

All notable changes to this project are documented here.

## [Unreleased]

### Added

- Added an editable minimap HUD with circular/square layouts; persisted anchors, offsets, size, radius, and zoom shortcuts; player, group, teleport, zombie, animal, player, and item markers; six item-rarity filters; other-floor indicators; and native-map, realtime-surface, height-slice, or fallback-grid backgrounds.
- Added shared nearby-item snapshots so the minimap and automatic pickup can reuse one max-radius/max-frequency scan without coupling display data to pickup-only filters or actions.
- Added transactional BepInEx configuration initialization with exact rollback for existing, empty, and first-run configuration files.
- Added dedicated `ConfigInitializationSmoke`, `MinimapSmoke`, `UiArchitectureSmoke`, and `WorkshopLocalizationSmoke` projects and expanded existing regression coverage.

### Changed

- Replaced transient runtime host/driver ownership with `PluginRuntimeContext`, `PluginRuntimeComposition`, a static per-frame `RuntimeUpdatePatch` bridge, and one persistent `PluginUiRoot` for menu, HUD, modal, input, and teardown ownership.
- Split the former monolithic overlay into feature views, presenters, view hosts, state stores, search/edit controllers, toast/window helpers, and deferred destruction outside active Canvas rebuilds.
- Replaced per-widget font creation with a shared validated Windows system-font provider and unified HUD text under the root canvas.
- Workshop localization now normalizes up to two API keys, creates one worker per key, dispatches batches concurrently, supports batch sizes from `1–100`, and treats unsupported Mono `MaxConnectionsPerServer` setters as a compatibility fallback rather than a startup failure.
- Vehicle thumbnails now always use the generated auto-framing camera and highest available LOD; authored `Icon2/Icon` transforms are ignored and cache format version `2` invalidates incompatible PNG keys without deleting existing files.
- Automatic pickup can explicitly follow minimap range and rarity settings, both disabled by default; its rule picker covers the complete carried inventory including the Hands page.
- Reference preparation now removes the production IMGUI dependency, includes TextMeshPro/TextCore assemblies, and can explicitly remove retired references after path validation.
- Consolidated public documentation into the Chinese/English README, this changelog, contribution/security policies, and the detailed source-update log; implementation plans, audits, reviews, research notes, and stale acceptance ledgers were removed.

### Fixed

- Prevented failed configuration binding or save operations from leaving partially migrated files or in-memory values.
- Prevented scene changes, stale page objects, Canvas rebuilds, and duplicate per-component `Update` loops from leaving broken UI references or destroying replacement views too early.
- Preserved explicit input ownership, IME/search edit state, window geometry, modal stacking, and deterministic teardown across menu and scene transitions.
- Kept source/build/Smoke evidence separate from real singleplayer visual and interaction acceptance.

### Verification

- Final merged-main Release build completed with zero warnings and zero errors.
- All 19 recursively discovered Smoke projects passed serially: assembly metadata, automatic pickup, configuration transactions, creature health bars, favorites serialization, flight, infinite storage, inventory arrangement, item filtering, hover tooltips, localization, minimap, movement speed, point-tool actions, shortcut toggles, teleport serialization, uGUI architecture, vehicle thumbnails, and Workshop localization.
- The locally built source artifact is `852,992` bytes with SHA-256 `6588462CCC704ABC0F941DBFE49AD88C083C0AF3AD6942DBB9980D8F4A33997B`; it was not packaged, deployed, tagged, or published as a release.

## [1.9.0] - 2026-08-27 (source push; unreleased)

### Added

- Added guarded automatic pickup with configurable range, scheduling, request limits, quality filtering, pause gates, diagnostics, and persisted whitelist/blacklist rules.
- Added persistent infinite storage with an independent UI, quantity/state-aware item semantics, bulk transfer, and crafting material/output overflow support.
- Added creature health-bar HUD, item/vehicle hover details, ammo compatibility details, view-direction flight, and expanded global shortcut actions.
- Added Smoke coverage for metadata, automatic pickup, infinite storage, creature health bars, flight mode, inventory planning, and hover details.

### Improved

- Inventory arrange now supports per-container page scope and keeps hotkey-bound item coordinates synchronized through apply, rollback, and exception recovery.
- Workshop and vanilla localization now tracks fields independently and uses asset-scoped cache identity (asset type, GUID, localization key, and schema version).
- Expanded NPC dialogue, quest, object interaction, and vendor text handling while preserving rich-text and dialogue placeholders.

### Fixed

- Prevented localized fields from being skipped or overwritten when another field on the same asset already contains Chinese text.
- Added conservative handling for ambiguous legacy infinite-storage entries so stateful items are not silently merged or split.

### Verification

- Release build passes with zero warnings and zero errors across the plugin and 15 Smoke projects.
- This entry documents a source-only push. No tag, release package, or GitHub Release was created; complete live-game acceptance remains separate and was not rerun in this source update.

## [1.7.0] - 2026-08-21

### Added

- Added a crosshair interaction tool with Smart, Inspect, Repair, Teleport, Utility, and Delete modes. Middle-mouse activation, configurable range, target HUD fields, semantic target inspection, and safe coordinate fallback are guarded by the singleplayer runtime boundary.
- Added native-simulation flight and noclip controls with separate horizontal and vertical speed multipliers, keyboard movement, and configurable safe-exit recovery when noclip is disabled.
- Added point-tool and movement smoke tests, and restored localization and teleport serialization regression coverage in the solution.

### Improved

- Hardened map-click teleport landing: surface candidates are validated for clip-volume and stance clearance, with nearby fallback positions when the requested point is blocked.
- Kept movement changes inside Unturned's simulation path and automatically restores native movement state when leaving singleplayer, dying, entering a vehicle, or closing the runtime boundary.
- Added live time-slider preview behavior and retained the overlay's native cursor/input isolation after PlayerUI updates.
- The installer now backs up and disables duplicate active plugin DLLs, then verifies that exactly one canonical DLL remains under `BepInEx\\plugins\\UnturnedSingleplayerCheatMenu`.


### Verified

- Release build and nine solution projects pass with zero warnings and zero errors; live point-tool and movement gameplay acceptance remains reported separately from source/build evidence.
- The formal release asset is Plugin-Only only. No complete BepInEx package is uploaded.

## [1.6.0] - 2026-08-17

### Added

- Advanced item filters for primary category, exact item type, origin, rarity, equipment slot, gun action, and multi-select fire modes.
- Configurable vehicle thumbnail output at `128 × 96`, `192 × 144`, or `256 × 192`, with an adjustable automatic-framing multiplier.
- Persistent vehicle-thumbnail PNG caching keyed by vehicle GUID, cache format, resolution, and framing.
- Smoke-test projects for item filtering, shortcut de-duplication, and vehicle-thumbnail setting normalization.

### Improved

- Item and favorite-item pages now keep independent filter state and show the number of active advanced filters.
- Vehicle icon loading remains lazy, processes at most one disk decode or render request per update budget, and falls back from corrupt cache entries to a fresh render.
- Predefined vehicle icon paths avoid unnecessary LOD forcing and full-texture transparency scans; those costs are limited to generated framing.

### Fixed

- Prevented one physical shortcut press from opening and immediately closing the menu when Unity and native input paths report it in adjacent frames.
- Preserved explicit user vehicle-thumbnail settings while normalizing invalid resolutions and framing values safely.

### Verified

- Release build, six smoke-test projects, version-metadata checks, package validation, and final hashes were recorded during the v1.6.0 release preparation.
- Existing v1.5.0 singleplayer runtime evidence is retained; the new item-filter and shortcut behavior is not mislabeled as fresh in-game acceptance.

## [1.5.0] - 2026-08-16

### Added

- Map-based teleport view with current-player positioning, zoom/pan, map-scoped points, right-click deletion, and safe landing resolution on terrain, roofs, and structures.
- Teleport marker shapes (star, square, circle, and diamond), custom colors, preset swatches, and persisted marker metadata with legacy-data normalization.
- Persisted interface state for the last main tab and teleport subview.
- Typed status feedback, empty states, confirmation dialogs, generated slider/glyph sprites, and localized UI strings for the expanded overlay.

### Improved

- Reworked the overlay layout and visual tokens for cards, inputs, buttons, toggles, sliders, summaries, status bars, and focus/pressed states.
- Vehicle thumbnails now use a right-front framing for the plugin-generated camera path, and the vehicle page is labeled “载具” in Chinese.
- Search and time controls use debounced updates; generated UI textures and transient objects are explicitly released.
- Teleport persistence and runtime actions now validate marker metadata and resolve obstructed destinations more safely.

### Verified

- Release build: zero warnings and zero errors.
- Localization, favorites serialization, and teleport serialization smoke tests.
- Existing real singleplayer screenshot evidence for the six-page overlay, vehicle right-front framing, and the 1.1 screenshot baseline is retained; full-scale interaction evidence remains separate from automated verification.

## [1.2.0] - 2026-08-15

### Added

- English localization for the active in-game overlay, including tabs, controls, categories, status messages, empty states, and default teleport names.
- `Interface.Language` configuration with `Auto`, `English`, and `Chinese` modes. `Auto` follows Unturned's current language and falls back to English for non-Chinese languages.
- An in-window `EN` / `中文` button that switches language immediately and persists the selection without restarting the game.
- Localization smoke tests covering language resolution, static strings, dynamic status messages, user-authored text preservation, and default names.

### Verified

- Release build: zero warnings and zero errors.
- Localization, favorites serialization, and teleport serialization smoke tests.
- Real singleplayer runtime confirmation that the language button is visible and switches the active overlay between Chinese and English.

## [1.1.0] - 2026-08-15

### Added

- Persistent item and vehicle favorites keyed by asset GUID.
- Dedicated favorites tab with item/vehicle modes, categories, search, quantities, and pagination.
- Direct item giving and vehicle spawning from favorite cards.
- Vehicle model thumbnail rendering when official icons are missing or unusable.
- Persistent, named, map-scoped teleport points.
- Configurable item quantities (`1–255`) and vehicle batch counts (`1–20`).
- Full-moon controls and expanded weather actions.

### Improved

- Replaced the original IMGUI-only presentation with an Unturned-compatible overlay and native cursor/input isolation.
- Hardened shortcut detection for Unity event and polling paths.
- Preserved favorites for temporarily unavailable Workshop assets.
- Added rollback behavior when favorites or teleport JSON cannot be saved.
- Added portable packaging and installation-layout verification.

### Verified

- Release build: zero warnings and zero errors.
- Favorites and teleport serialization smoke tests.
- Real singleplayer runtime checks for all six tabs, loaded Workshop assets, icons, quantities, vehicle spawning, persistence, teleports, time, weather, full moon, and airdrops.

## [1.0.0] - 2026-08-15

- Initial singleplayer-only menu with player, item, vehicle, teleport, time, weather, and airdrop controls.
