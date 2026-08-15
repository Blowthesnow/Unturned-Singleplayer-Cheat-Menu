# Changelog

All notable changes to this project are documented here.

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
