# Changelog

All notable changes to this project are documented here.

## [Unreleased]

## [2.0.0] - 2026-09-05

### Added

- Added a live session-status badge and an explicit, in-memory risk acknowledgement for unsupported multiplayer/listen-server gameplay. The acknowledgement applies only to the current connection and is invalidated by disconnects, server/map changes, or local-player recreation.
- Added `RuntimeBoundarySmoke` for frontend/loading/singleplayer/multiplayer/listen-server/dedicated-server classification and acknowledgement invalidation, plus a source-only Snapshot performance probe for clone, point-lookup, staging, serialization, and foreground/background persistence measurements.

### Changed

- Split client-menu readiness from gameplay authorization. A connected client can open the menu after its local player spawns; singleplayer remains the only supported gameplay environment, while acknowledged multiplayer execution is unsupported best-effort.
- Kept Workshop localization outside gameplay authorization. Session changes no longer cancel active translation, and asset refreshes during a batch coalesce into one deferred rescan.
- Moved AutoPickup's Snapshot-priority persistence to one background operation at a time. The world drop is suspended while the candidate is written, finalized only after persistence succeeds, and otherwise resumes or enters fail-closed reconciliation according to the commit result.
- Batched all Snapshot material/output mutations from one native craft request into one thread-local transaction and one outer commit, and replaced whole-warehouse scans with exact ItemGroup lookup.

### Fixed

- Fixed directly compatible Workshop ammo boxes being rejected as unsafe conversion bridges during backpack arrangement and Snapshot ammo refill, while retaining exact round conservation and blocking unsupported indirect conversions.
- Repaired realtime minimap orientation, native-map edge clipping, native Terrain material compatibility, complete cliff/tree-canopy rendering, and low-quality Water4 visibility; temporary rendering state is restored after capture.
- Added native-teleport-driven background recovery and cache refresh for destinations without a native map image. Ordinary walking beyond map edges does not silently switch the selected background.
- Refined responsive character and inventory forms, translation progress/actions fixed above scrolling settings, collapsible provider/cache sections, and the two-column time/weather layout.
- Corrected tooltip handoff, selectable-state transitions, and toast departure/reflow so interrupted animations do not leave stale callbacks or jump between positions.
- Disabled diagnostic minimap GPU readback and PNG/JSON writes by default; developers can explicitly opt in before launch with `UNTURNED_MINIMAP_CAPTURE=1`.
- Repaired view-direction flight for the current Unturned runtime: configured directional bindings drive movement, W/S follows camera pitch and yaw, Space/Ctrl has fixed world-Y priority, diagonals are normalized, and zero/non-finite directions fail over safely.
- Prevented the crafting `SearchContents` Postfix from peeking a null thread-local context during unrelated native inventory searches, which could interrupt attachment-slot and rarity-color refresh.
- Made null, missing, or unresolved asset origins classify as unknown instead of Workshop.
- Bound Inventory Arrange undo journals to the connection that created them, so an undo cannot cross a disconnect or server/map transition.
- Preserved requested automation and UI settings when gameplay authorization is temporarily unavailable, while mutation paths still stop immediately.
- Bound an in-flight AutoPickup Snapshot write to its originating connection and exact `ItemDrop` object. A completion arriving after a scene/session change can no longer apply to the active kernel, finalize or resume a current-world drop, or trigger native fallback; committed or uncertain old-session writes are latched only at their original persistence path.
- Moved the Workshop localization Pump to the first isolated runtime step after frame de-duplication, so failures in session observation, icon work, or UI maintenance cannot starve translation completion, cache application, or deferred rescans.

### Removed

- Removed the legacy all-or-nothing `SingleplayerGuard`; session classification and gameplay authorization now use `RuntimeSessionPolicy`, `RuntimeSessionBoundary`, and `GameplayFeatureAccessPolicy`.
- Removed the global `PlayerInventory.forceAddItem` crafting Prefix. Only the two verified crafted-output calls inside `HandleCraftRequestInternal` are rewritten, leaving attachment, reward, condition, and unrelated native grants untouched.

### Security

- Kept Snapshot Infinite Storage and zombie-region retention hard-disabled outside a true `Singleplayer_<slot>` session, with capability rechecks at runtime, module, transaction, and persistence entry points rather than waiting for the next frame pump.
- Made unknown sessions fail closed, rolled back transactions when authorization disappears before commit, persisted reconciliation on uncertain background outcomes, and kept standalone dedicated-server startup inert before client UI, runtime services, or Harmony patches are initialized.

### Documentation

- Updated the bilingual usage, contribution, security, runtime-boundary, Snapshot living-reference, and external-API inventory documentation for the 2.0 behavior and evidence boundaries.

### Verification

- Re-audited the current Unturned runtime ABI, including `PlayerLook.aim`, configured movement bindings, the two crafted-output grant sites, and attachment searches through `PlayerInventory.SearchContents`.
- Release solution build completed with zero warnings and zero errors; all 30 recursively discovered `*Smoke.csproj` projects were individually rebuilt and run with `--no-build`: 30/30 PASS.
- The external API inventory generated twice with 1,680 rows and identical SHA-256 `0416CF68CDEC2A53A043DDA206A9E407D2C3DF660B829FE66EB7C562F7EBEEF9`.
- The final `2.0.0.0` DLL is 1,187,328 bytes with SHA-256 `79AC75876C0B3B829188BB02EEB2DF5C32F04BF1AC7C025D695B44389B33F192`. The validated three-file Plugin-Only ZIP is 448,465 bytes with SHA-256 `92CFA47EB93A68A98EC122EEDB67EDC6D59F7E0A290E0207F72500009D7049D9`.
- The final 2.0 DLL/package was not deployed or restarted in the real game during release preparation. Earlier diagnostic/deployment evidence does not constitute final-package gameplay acceptance.

## [1.995.0] - 2026-09-04 (source checkpoint; unreleased)

### Added

- Added one stable, frame-driven Legacy uGUI motion driver for selectable colors, switches, modal and popup transitions, tooltips, and toast enter/exit/reflow while preserving synchronous high-frequency and native-handoff paths.

### Changed

- Replaced the previous unreleased Infinite Storage runtime and persistence design with a Snapshot Kernel that stores exact native `{id, amount, quality, state[]}` buckets and checked `long` instance counts.
- Moved Infinite Storage to strict Snapshot schema 1 under `InfiniteStorageSnapshots/v1`, with versioned identity keys, collision checks, exact world/player/character-slot validation, atomic primary/backup handling, and reconciliation recovery records.
- Reconnected the existing Crafting, AmmoRefill, and Decomposition capabilities as independently configurable same-DLL adapters over a small runtime port, keeping their interpretation rules outside the Snapshot Core.
- Reworked the existing Infinite Storage view around ItemGroup headers and expandable exact SnapshotBucket rows, with runtime asset metadata, default-template priority, asset-conflict locks, and a one-time permanent-destruction warning.
- Consolidated the existing inventory-arrange and storage ammo paths onto runtime-asset-driven item identity, stacking, compatibility-graph, and allocation rules without per-item ID/name or Workshop exclusions.
- Replaced the retired Infinite Storage implementation and monolithic Smoke project in the solution with focused Snapshot Kernel, adapter, registry, and single-DLL integration coverage.

### Fixed

- Prevented `SteamPlayerID == null` from invoking Unturned's null-unsafe overloaded equality inside the storage runtime; Snapshot Pump is isolated so a storage fault cannot abort later HUD updates.
- Prevented hidden-quality loss and amount/state conflation by keeping native amount, quality, state, and capacity exact; only the evidence-backed four-byte Farm timestamp is canonicalized.
- Rejected malformed, duplicate, trailing, wrong-version, comment-bearing, unknown-field, identity-conflicting, or externally changed Snapshot documents instead of activating or overwriting them.
- Fixed Store All, Withdraw, crafting, ammo refill, and decomposition failure paths so Kernel mutations, persistence, native inventory changes, and compensation either agree or enter a persistent fail-closed reconciliation state.
- Closed replacement-time and final-verification races across primary, backup, transaction backup, candidate history, reconciliation marker, and process-lifetime path latches while retaining independently recoverable evidence.
- Made candidate-history cleanup verify Windows file identity, SHA-256, path, handle ownership, sharing, and deletion state before removing a sidecar.
- Expanded native rollback checkpoints to equipment selection, clothing, quick slots, and carried pages; fixed crafting rollback ordering, page-255 target matching, exact ignored-item matching, and over-capacity ammo rejection.
- Fixed AutoPickup's native/storage handoff so legal `amount=0` items, sent-request uncertainty, stale/timeout confirmation, double failures, fallback, quarantine, scheduler recreation, and reconciliation races do not duplicate or lose items.
- Preserved native AutoPickup diagnostic text byte-for-byte while appending and translating exactly one structured confirmation suffix, and retained specific false/throw/default callback outcomes through final accounting.
- Fixed the existing Inventory Arrange feature so true no-op plans do not rebuild inventory and numeric item bindings survive rebuild, native drag, and native swap paths.
- Fixed the existing flight feature so local flight suppresses the native airborne viewmodel tilt without changing normal jumps or remote-player animation.
- Fixed malformed bare `<color>` closing tags in legacy item descriptions without altering valid rich-text color markup.
- Fixed UI-motion retargeting and teardown so destroyed owners, modal/popup layers, toggle transitions, tooltip handoff, tab/scene/menu closure, and toast reflow do not leave stale handles, focus, raycasts, or visual residue.
- Removed the retired migration, provenance, ambiguity, and Claim paths; old never-released storage files are ignored rather than read, moved, deleted, or represented as migrated.

### Documentation

- Replaced the retired storage-migration reference with the Snapshot Kernel living specification, schema-1 examples, recovery bundle, deployment checklist, and source/Smoke versus deployment/game-acceptance boundary.
- Added a deterministic Roslyn external-API inventory generator and checked-in inventory for auditing the implementation's dependency surface.
- Added the source-only multiplayer/BattlEye boundary plan and updated the UI-motion plan, Chinese/English README version text, Snapshot paths, and reconciliation guidance.

### Verification

- Release solution build completed with zero warnings and zero errors.
- All 26 recursively discovered `*Smoke.csproj` projects were explicitly rebuilt and then run with `--no-build`: 26/26 PASS, including Snapshot Kernel, all three internal adapters, registry, single-DLL/ABI integration, AutoPickup, inventory arrangement, UI motion, localization, and UI architecture.
- The external API inventory generated twice with 1,586 rows and identical SHA-256 `09DEFAB1A2640AC68A1E47050E14936B878C46FBFB23BDFAAD9A116F08A3D020`.
- The locally built `1.995.0.0` source artifact is `1,115,648` bytes with SHA-256 `0B5D302469CFA8779CF1F43D4D58723CD73EF7F6C77E5998D481834201393FA7`.
- This is a source-and-documentation checkpoint only: no package, deployment, tag, game launch, or GitHub Release is created, and the combined v1.995 behavior is not represented as fresh in-game acceptance.

## [1.98.0] - 2026-09-01 (source checkpoint; unreleased)

### Added

- Added a Fluent-inspired Legacy uGUI control layer with shared theme tokens, rounded surfaces, selectable-state palettes, tooltip and lifetime bridges, HUD primitives, and a dedicated control-library Smoke project.
- Added persistent interface accent-color customization and applied it across the shell, controls, HUD surfaces, teleport markers, and minimap visuals.
- Added transactional Infinite Storage decomposition with single-item, matching-asset, and full-stack scopes; native salvage recipe resolution; transferred item-state handling; overflow and persistence rollback; eligibility diagnostics; and an in-page destructive-action mode.
- Added a persistent six-level automatic-pickup rarity mask with a modal Common-through-Mythical picker. Rarity filtering now runs before quality, allow/block-list, scheduling, and native pickup execution.
- Added optional view-relative minimap rotation with north-up compatibility, rotation-aware projection extent, upright compass labels, and separate player/map heading treatment.
- Added minimap layout-input isolation for native key and chat paths, preventing `End`, `Escape`, and Enter releases used by layout editing from leaking back into gameplay UI.
- Added bounded cross-region zombie retention for the minimap: existing non-networked regions can be reused across native bound updates, while an eight-region LRU cap and protected-radius checks reclaim only plugin-tracked, unoccupied far regions.

### Changed

- Reworked the overlay into responsive wide and narrow shells with separate navigation/layout groups, scroll handling, larger working geometry, consistent toolbar metrics, and explicit page/view cleanup.
- Replaced the world-time slider with a Chinese/English 12-hour time ring mapped to the game's dawn/noon/dusk/midnight cycle, including wrapped drag accumulation, wheel/keyboard adjustment, and parent-scroll ownership.
- Expanded Infinite Storage to a five-column, 15-entry page layout with clearer search, withdrawal, scope, status, and destructive-action affordances.
- Standardized Fluent styling and four-edge HUD frames across the main window, cards, tooltips, toasts, Point Tool, creature health bars, minimap, and modal surfaces; dynamic accent/font revisions refresh long-lived controls in place.
- Refined catalog, character, inventory, movement, teleport, translation, world, and minimap views for consistent field widths, localization, responsive columns, focus state, and compact control density.
- Minimap item markers now use rarity-colored high-contrast fills, black outlines, star geometry, and dynamic Mythical coloring without per-frame Unity object or texture allocation. Cross-floor alpha differences remain while floor-direction arrows are removed.
- Minimap scanning now reads `ZombieManager.AllZombies`, and projection/background/compass text refreshes correctly when orientation or language changes.

### Fixed

- Prevented mixed horizontal/vertical layout groups from being attached to the same shell object during responsive transitions.
- Prevented minimap layout editing from reopening chat or toggling game actions on the same or immediately following frame.
- Prevented retained zombie regions from growing without bound or recycling networked, occupied, protected, or non-plugin-tracked regions.
- Kept Infinite Storage decomposition inside the existing transaction and persistence boundary, including exact rollback on invalid recipes, missing output assets, arithmetic overflow, or save failure.
- Filled localization gaps for formatted catalog counts, minimap values and suffixes, tooltips, transient summaries, and long-lived compass/background labels.

### Documentation

- Added implementation plans for the Legacy uGUI control-library redesign, minimap rarity visuals, native-framework localization, the future Other-page world-maintenance workflow, and UI motion optimization.
- Updated Chinese/English README version and verification language while retaining `v1.7.0` as the latest formal release asset.

### Verification

- Release solution build completed with zero warnings and zero errors.
- All 20 recursively discovered `*Smoke.csproj` projects were rebuilt and passed serially, including the new `UiControlLibrarySmoke` project and expanded automatic-pickup, Infinite Storage, minimap, localization, and UI-architecture gates.
- The locally built `1.98.0.0` source artifact is `941,568` bytes with SHA-256 `555A99CB46FB55BB6BDBC8F0F993BD20B7C212319342A85995F980CECFE4B288`.
- This is a source-and-documentation push only: no package, deployment, tag, or GitHub Release is created, and the combined v1.98 behavior is not represented as fresh in-game acceptance.

## [1.95.0] - 2026-08-30 (source checkpoint; unreleased)

### Added

- Added an editable minimap HUD with circular/square layouts; persisted anchors, offsets, size, radius, and zoom shortcuts; player, group, teleport, zombie, animal, player, and item markers; six item-rarity filters; other-floor indicators; and native-map, realtime-surface, height-slice, or fallback-grid backgrounds.
- Added shared nearby-item snapshots so the minimap and automatic pickup can reuse one max-radius/max-frequency scan without coupling display data to pickup-only filters or actions.
- Added transactional BepInEx configuration initialization with exact rollback for existing, empty, and first-run configuration files.
- Added dedicated `ConfigInitializationSmoke`, `MinimapSmoke`, `UiArchitectureSmoke`, and `WorkshopLocalizationSmoke` projects and expanded existing regression coverage.

### Changed

- Replaced transient runtime host/driver ownership with `PluginRuntimeContext`, `PluginRuntimeComposition`, a static per-frame `RuntimeUpdatePatch` bridge, and one persistent `PluginUiRoot` for menu, HUD, modal, input, and teardown ownership.
- Split the former monolithic overlay into feature views, presenters, view hosts, state stores, search/edit controllers, toast/window helpers, and deferred destruction outside active Canvas rebuilds.
- Replaced per-widget font creation with a shared validated Windows system-font provider and unified HUD text under the root canvas.
- Synchronized the project, assembly, BepInEx plugin metadata, startup log, and About dialog to current source version `1.95.0`.
- Workshop localization now normalizes up to two API keys, creates one worker per key, dispatches batches concurrently, supports batch sizes from `1–100`, and treats unsupported Mono `MaxConnectionsPerServer` setters as a compatibility fallback rather than a startup failure.
- Item and vehicle cards now share a fitted thumbnail viewport that updates from each texture's actual aspect ratio, preventing square item icons and wide vehicle renders from being stretched.
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
- The locally built `1.95.0.0` source artifact is `852,992` bytes with SHA-256 `4C643DAECC1A0CB4421F47ACA58383275FE659C4D2C989B16CE3B0008E5EFA03`; it was not packaged, deployed, tagged, or published as a release.

## [1.9.0] - 2026-08-27 (`tooltip-feature` source push; unreleased)

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

## [1.8.0] - 2026-08-24 (source recovery checkpoint; unreleased)

### Added

- Added Workshop/Vanilla asset-text scanning, translation queues, batching, retry/cache/progress handling, native-UI application, and preservation of existing Chinese localization.
- Added inventory arrangement planning and rollback, page/all-carried scope, equipment and hotbar protection, automatic rotation, presets, and Ctrl/Shift quick transfer.
- Added a configurable global-shortcut framework around the existing menu toggle and the new inventory-arrangement actions.
- Added the UI state, focus, selectable, icon-binding, and progress connections required by these v1.8-stage features.

### Recovery

- Reconstructed source lost during an intermediate reset from the last runnable plugin DLL, then checked the recovered API usage against the local Unturned assemblies.
- Restored v1.7 and older PointTool, movement, teleport, filtering, favorites, thumbnail, and world-control code only as baseline recovery; those existing behaviors are not counted as v1.8 additions.
- Kept project and plugin metadata at `1.7.0`; this checkpoint did not create a v1.8 tag, package, or GitHub Release.
- Detailed scope and exclusions are recorded in [the v1.8 recovery log](docs/V1.8_RECOVERY_CHANGELOG.md).

### Verification

- Historical recovery evidence records a zero-warning/zero-error Release build, eight passing Smoke projects, local `Assembly-CSharp.dll` API checks, and 83 top-level recovered type/signature matches.
- The historical game check did not claim byte identity with the recovered DLL; this changelog cleanup did not rerun the old commit or relabel current-source automation as v1.8 runtime acceptance.

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
