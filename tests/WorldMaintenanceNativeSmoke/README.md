# World maintenance native smoke

Run from the repository root:

```powershell
dotnet run --project tests\WorldMaintenanceNativeSmoke\WorldMaintenanceNativeSmoke.csproj
```

This harness links the production `WorldMaintenanceNativeBridge`, `WorldMaintenanceService`, and
`WorldMaintenanceContracts` sources. It exercises their real preview, batch, commit, notification,
and result paths against a minimal native API surface.

The private stub signatures for `respawnItems()` and `spawnItem(...)` match the reflected game
methods. Native outcomes are fixture callbacks rather than copies of the production logic. Data
commits enqueue local drops, and the fixture explicitly advances the native Update before checking
that every committed instance has exactly one model.

Covered behavior includes no-op accounting, native true without data, deferred model construction,
multiple items in one region, mode quota, safe authored points, server authority, replacing an
active spawn list, item-count/quota/safezone drift after start, preserving partial native commits
after event exceptions, restoring native cursors and respawn clocks, mutation notifications, and
zero-result zombie generation.

The harness verifies plugin behavior at the native boundary. It does not run Unity physics, native
loot selection, actual Steam networking, or an in-game pickup interaction.
