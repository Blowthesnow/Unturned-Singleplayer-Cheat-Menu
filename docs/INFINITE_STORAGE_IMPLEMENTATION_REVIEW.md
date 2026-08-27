# Infinite Storage Implementation Review

This review is against `INFINITE_STORAGE_IMPLEMENTATION_PLAN.md` after the current implementation pass.

## Implemented And Verified

- Persistent enable switch: `InfiniteStorage.Enabled`, default `false`, BepInEx config persistence, and Tools UI toggle.
- Per-world/per-player JSON storage under `BepInEx/config/UnturnedSingleplayerCheatMenu/InfiniteStorage/<world-hash>/<player-hash>.json`.
- Atomic `.tmp` publish, `.bak` recovery, schema validation, null/empty state normalization, and GUID-backed semantic keys.
- Exact state/quality isolation and explicit resource/container/instance aggregation with native stack-size preservation on withdrawal.
- `ItemMagazineAsset` is always a complete container instance; `ItemSupplyAsset` is promoted to an ammo container only when a magazine fill blueprint maps it as such. Unmapped supply assets remain conservative complete instances.
- Manual carried-inventory merge removes the captured native items only after staging storage, then persists; any removal or persistence failure restores the original page/coordinate/rotation/item snapshot.
- Auto-pickup overflow transfer with drop deletion through `ItemManager.ReceiveDestroyItem` and rollback on deletion failure.
- Crafting integration at the native search/deletion boundary: virtual search results are exposed only while blueprint status is running, virtual `Delete`/`DeleteAmount` failures now throw to abort the native craft instead of silently continuing, and full native inventory output falls back to virtual storage.
- Storage UI statistics, carried-item merge, per-entry stack withdrawal, and full-entry withdrawal.
- Release build: 0 warnings, 0 errors.
- Current executable Smoke run: `InfiniteStorageSmoke`, `AutoPickupSmoke`, `ItemFilteringSmoke`, `LocalizationSmoke`, and the other available Release Smoke projects passed. Three historical names (`VehicleToolboxSmoke`, `WorkshopLocalizationSmoke`, `LoadoutStoreSmoke`) have no source project in the current worktree and were not counted; `InventoryLayoutSmoke` likewise has no source project and its stale binary is not acceptance evidence.
- Previous semantic-model deployment: source and game DLL SHA-256 matched (`FD08D81AD8260B7F3757A6D461831E198BF5F79E75B7F7025DCC2F3A5496B732`); assembly version was `1.7.0.0` in both locations. This hash predates the quality-display correction below.

## Implementation Pass Update (2026-08-27)

- Added `VirtualStorageTransaction` for single-commit add/remove/consume operations with snapshot rollback when persistence fails.
- Updated carried-item storage, rollback candidates, produced output, virtual consumption, and withdrawal to restore in-memory state after failed writes.
- Withdrawal now restores the complete pre-withdraw inventory snapshot (page, coordinates, rotation, amount, and state) when the final persistence commit fails, then restores the storage snapshot; it no longer removes arbitrary matching items.
- Added `VirtualStorageCraftingAdapter` and the `ItemInstanceSemantics` classifier as the shared read/consume boundary for native inventory plus virtual storage.
- Removed the superseded `CombinedInventoryView` and `VirtualStorageStateFingerprint` model files; schema-2 persistence no longer writes the old `Count`/`MaxNativeAmount` contract.
- Scoped the crafting Harmony bridge to `PlayerCrafting.UpdateBlueprintInputItemStatus` and `HandleCraftRequestInternal`, preventing virtual results or overflow storage from affecting non-crafting `SearchContents`/`forceAddItem` callers such as attachment, reward, and clothing paths.
- Crafting entry now captures native inventory plus virtual-storage snapshots; if an explicit storage failure aborts the native request, both snapshots are restored before the exception is returned.
- Infinite Storage UI now reuses `ItemFilterState`/`ItemFilterService` matching, search text, logical-entry pagination, and empty-state behavior.
- Added transaction failure assertions to `InfiniteStorageSmoke`; the targeted storage, pickup, filtering, and supporting Smoke executables pass after the update.
- Release DLL was rebuilt and deployed after the semantic-classification and rollback hardening; source and target SHA-256 are both `FD08D81AD8260B7F3757A6D461831E198BF5F79E75B7F7025DCC2F3A5496B732`.

## Quality Display Correction (2026-08-27)

- `QualityPolicy.Exact` already preserved native quality and kept different-quality instances in separate entries; the missing food/medical/filter quality was a card-rendering omission, not data loss.
- Card metadata now displays the exact native quality when the resolved `ItemAsset.showQuality` contract allows it. Confirmed resources still suppress meaningless default quality, ammo/fuel/water containers retain their payload summaries, and unresolved assets retain the `保守状态隔离` warning.
- Resolved entries with non-empty exact state use the clearer `状态独立保存` label. English dynamic-template translations cover every new card form.
- `InfiniteStorageSmoke` and `LocalizationSmoke` pass with explicit quality-display, unresolved-asset, ammo-quality, and bilingual-template assertions.
- Final Release rebuild and redeployment of this correction are currently pending: unrelated in-progress shortcut UI edits in `CheatMenuOverlayUi.cs` reference a removed `_shortcutContentHost` field (34 compile errors), and `Unturned`/`Unturned_BE` are running. No game DLL was overwritten while the game was active.

## Requires In-Game Verification

The following acceptance items require a real Unturned singleplayer session with BattlEye disabled:

- Toggle persistence across game restart.
- Crafting using storage-only and mixed native/storage materials, including batch crafting.
- Output fallback when every native inventory page is full.
- Craft failure and interrupted-write recovery without material loss.
- Auto-pickup gating for player-discarded and pending-confirmation drops.
- State-sensitive weapon/tool separation across restart and world switching.
- Withdrawal behavior with equipment slots, stack merging, and insufficient native space.
- Visual parity with the item-page filters, search, and pagination described by the plan.

Build/Smoke/deployment evidence does not replace these manual checks.
