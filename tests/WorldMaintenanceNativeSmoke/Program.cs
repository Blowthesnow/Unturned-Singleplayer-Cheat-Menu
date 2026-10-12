using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

int checks = 0;
List<string> failures = new();
void Check(bool condition, string message)
{
    checks++;
    if (condition) Console.WriteLine("PASS " + message);
    else { failures.Add(message); Console.WriteLine("FAIL " + message); }
}

ItemData NewData(uint id, float x = 10f) => new()
{
    item = new Item { id = 12 }, instanceID = id, point = new Vector3(x, 0f, 10f)
};

WorldMaintenanceNativeBridge NewBridge()
{
    WorldMaintenanceNativeBridge bridge = new(new ManualLogSource());
    Check(bridge.ItemRespawnCapabilityResolved, "Exact private native signatures bind successfully.");
    return bridge;
}

WorldMaintenancePreview RestockPreview(WorldMaintenanceService service)
{
    bool previewed = service.TryCreatePreview(WorldMaintenanceActionKind.RestockGroundItems,
        WorldMaintenanceScopeKind.WholeMap, 250f, false, out WorldMaintenancePreview preview, out _);
    Check(previewed, "Restock preview is created from the actual service.");
    return preview;
}

WorldMaintenanceResult RunRestock(WorldMaintenanceService service)
{
    WorldMaintenancePreview preview = RestockPreview(service);
    bool started = service.TryStart(preview, out _, out _);
    Check(started, "Restock preview starts the actual service batch.");
    if (started) service.Pump();
    return service.CaptureDashboard(250f).Result;
}

// A native no-op must preserve state and must not become a successful maintenance result.
ItemRegion region = ItemManager.Reset();
WorldMaintenanceNativeBridge bridge = NewBridge();
int added = bridge.TryRespawnItems(0, 0);
Check(added == 0 && region.items.Count == 0 && region.drops.Count == 0 && ItemManager.PendingDrops.Count == 0,
    "Native false with no item data remains a bridge no-op.");
Check(ItemManager.Cursor == (19, 23) && region.lastRespawn == 123f,
    "No-op restores native region cursor and original respawn clock.");
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    int notified = 0;
    service.GroundItemsChanged += () => notified++;
    WorldMaintenanceResult result = RunRestock(service);
    Check(result?.SucceededCount == 0 && WorldMaintenancePolicy.IsResultConserved(result),
        "Service reports zero success when native restock adds no item data.");
    Check(notified == 0, "A restock no-op never publishes a ground-item mutation notification.");
}

// The native bool can be true for a failed asset lookup without committing any item.
region = ItemManager.Reset();
ItemManager.RespawnBehavior = (x, y) => true;
bridge = NewBridge();
added = bridge.TryRespawnItems(0, 0);
Check(added == 0 && region.items.Count == 0 && ItemManager.PendingDrops.Count == 0,
    "A native true return without committed data still reports zero restocked items.");

// Installed native SendItem loops back into a pending queue; Update constructs models later.
region = ItemManager.Reset();
ItemManager.RespawnBehavior = (x, y) => { ItemManager.NativeCommitData(x, y, NewData(51)); return true; };
bridge = NewBridge();
added = bridge.TryRespawnItems(0, 0);
Check(added == 1 && region.items.Count == 1 && region.drops.Count == 0 && ItemManager.PendingDrops.Count == 1,
    "Native commit counts one item while model construction remains pending until Update.");
Check(ItemManager.LocalSpawnCalls == 0, "Bridge leaves local model construction to the native pending queue.");
ItemManager.NativeUpdate();
Check(region.drops.Count(drop => drop.instanceID == 51) == 1 && ItemManager.PendingDrops.Count == 0,
    "Native Update yields exactly one local model per committed instance ID.");
ItemManager.NativeUpdate();
Check(region.drops.Count == 1, "A later native Update does not duplicate a drained pending model.");

// Existing occupied points remain intact; a batch reports items rather than just regions.
region = ItemManager.Reset();
region.items.Add(NewData(61, 0f));
region.drops.Add(new ItemDrop { instanceID = 61 });
LevelItems.spawns[0, 0] = new List<ItemSpawnpoint>
{
    new() { point = new Vector3(0f, 0f, 10f) },
    new() { point = new Vector3(10f, 0f, 10f) },
    new() { point = new Vector3(20f, 0f, 10f) }
};
ItemManager.RespawnBehavior = (x, y) =>
{
    ItemManager.NativeCommitData(x, y, NewData(62));
    ItemManager.NativeCommitData(x, y, NewData(63, 20f));
    return true;
};
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    int notified = 0;
    service.GroundItemsChanged += () => notified++;
    WorldMaintenancePreview preview = RestockPreview(service);
    Check(preview.CandidateCount == 2, "Preview counts two vacant item slots in one native region.");
    WorldMaintenanceResult result = RunRestock(service);
    Check(result?.SucceededCount == 2 && result.OriginalCandidateCount == 2 && WorldMaintenancePolicy.IsResultConserved(result),
        "The real service counts both native items rather than one successful region.");
    Check(notified == 1, "A native region commit publishes exactly one ground-item change notification.");
    Check(region.items.Count == 3 && region.drops.Count == 1 && ItemManager.PendingDrops.Count == 2 &&
        ItemManager.RespawnCalls == 1 && ItemManager.LocalSpawnCalls == 0,
        "One native region pass preserves existing models and queues both new models once.");
    ItemManager.NativeUpdate();
    Check(region.drops.Count == 3 && region.drops.Select(drop => drop.instanceID).Distinct().Count() == 3,
        "Native Update leaves three unique instance models including the existing occupied point.");
}

// A client without world authority does not invoke the server's native write entry point.
region = ItemManager.Reset();
Provider.isServer = false;
ItemManager.RespawnBehavior = (x, y) => { ItemManager.NativeCommitData(x, y, NewData(81)); return true; };
bridge = NewBridge();
added = bridge.TryRespawnItems(0, 0);
Check(added == 0 && ItemManager.RespawnCalls == 0 && region.items.Count == 0 && ItemManager.PendingDrops.Count == 0,
    "A client without server authority creates no native data or queued models.");

// Replacing a spawn collection after batch creation must invalidate the queued candidate.
region = ItemManager.Reset();
ItemManager.RespawnBehavior = (x, y) => { ItemManager.NativeCommitData(x, y, NewData(82)); return true; };
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    bool started = service.TryStart(preview, out _, out _);
    Check(started, "Spawn-list replacement fixture starts with a current preview.");
    LevelItems.spawns[0, 0] = new List<ItemSpawnpoint>(LevelItems.spawns[0, 0]);
    if (started) service.Pump();
    WorldMaintenanceResult result = service.CaptureDashboard(250f).Result;
    Check(result?.SucceededCount == 0 && result.InvalidCount == 1 &&
        WorldMaintenancePolicy.IsResultConserved(result) && ItemManager.RespawnCalls == 0,
        "Equal-count spawn-list replacement invalidates the queued region before native mutation.");
}

// Original mode quota is authoritative even when authored points remain vacant.
region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
Provider.modeConfigData.Items.Spawn_Chance = 0.5f;
region.items.Add(NewData(71));
region.drops.Add(new ItemDrop { instanceID = 71 });
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    Check(preview.CandidateCount == 0, "A full original spawn quota has no restock candidates despite a vacant authored point.");
    Check(!service.TryStart(preview, out _, out _) && ItemManager.RespawnCalls == 0,
        "Quota-full regions do not enter the native restock batch.");
}

// Authored points are not eligible merely because quota is available.
region = ItemManager.Reset();
SafezoneManager.PointValid = _ => false;
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    Check(preview.CandidateCount == 0 && ItemManager.RespawnCalls == 0,
        "Unsafe authored points never become previewed restock slots.");
}

// Exceptions must restore the temporary native scan cursor without making a false commit.
region = ItemManager.Reset();
ItemManager.RespawnBehavior = (x, y) => throw new InvalidOperationException("injected native failure");
bridge = NewBridge();
bool threw = false;
try { bridge.TryRespawnItems(0, 0); }
catch (WorldMaintenancePartialMutationException exception)
{
    threw = exception.ChangedCount == 0 && exception.InnerException is InvalidOperationException;
}
Check(threw && ItemManager.Cursor == (19, 23) && region.items.Count == 0 && region.lastRespawn == 123f,
    "Native failure without a commit restores cursor and original respawn clock.");

// Candidate weight must stay tied to the world state that was confirmed, not a later larger gap.
region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
region.items.Add(NewData(91));
region.drops.Add(new ItemDrop { instanceID = 91 });
ItemManager.RespawnBehavior = (x, y) =>
{
    ItemManager.NativeCommitData(x, y, NewData(92));
    ItemManager.NativeCommitData(x, y, NewData(93, 20f));
    return true;
};
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    bool started = service.TryStart(preview, out _, out _);
    Check(started && preview.CandidateCount == 1, "One-slot preview starts before existing item removal.");
    region.items.Clear();
    region.drops.Clear();
    if (started) service.Pump();
    WorldMaintenanceResult result = service.CaptureDashboard(250f).Result;
    Check(result?.SucceededCount == 0 && result.InvalidCount == 1 && result.OriginalCandidateCount == 1 &&
        WorldMaintenancePolicy.IsResultConserved(result) && ItemManager.RespawnCalls == 0 && region.items.Count == 0,
        "Removing an existing item after start invalidates old weight before a larger native commit.");
}

region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
Provider.modeConfigData.Items.Spawn_Chance = 0.5f;
ItemManager.RespawnBehavior = (x, y) =>
{
    ItemManager.NativeCommitData(x, y, NewData(94));
    ItemManager.NativeCommitData(x, y, NewData(95, 20f));
    return true;
};
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    bool started = service.TryStart(preview, out _, out _);
    Check(started && preview.CandidateCount == 1, "One-slot preview starts under the original spawn density.");
    Provider.modeConfigData.Items.Spawn_Chance = 1f;
    if (started) service.Pump();
    WorldMaintenanceResult result = service.CaptureDashboard(250f).Result;
    Check(result?.SucceededCount == 0 && result.InvalidCount == 1 && result.OriginalCandidateCount == 1 &&
        WorldMaintenancePolicy.IsResultConserved(result) && ItemManager.RespawnCalls == 0,
        "Increasing spawn density after start invalidates the old quota before native mutation.");
}

region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
SafezoneManager.PointValid = point => point.x < 15f;
ItemManager.RespawnBehavior = (x, y) =>
{
    ItemManager.NativeCommitData(x, y, NewData(96));
    ItemManager.NativeCommitData(x, y, NewData(97, 20f));
    return true;
};
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    WorldMaintenancePreview preview = RestockPreview(service);
    bool started = service.TryStart(preview, out _, out _);
    Check(started && preview.CandidateCount == 1, "One-safe-slot preview starts under the original safezone state.");
    SafezoneManager.PointValid = _ => true;
    if (started) service.Pump();
    WorldMaintenanceResult result = service.CaptureDashboard(250f).Result;
    Check(result?.SucceededCount == 0 && result.InvalidCount == 1 && WorldMaintenancePolicy.IsResultConserved(result) &&
        ItemManager.RespawnCalls == 0,
        "Opening another safe spawnpoint after start invalidates the old available-slot count.");
}

// A native listener can throw after the first item has already committed and entered its queue.
region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
InvalidOperationException partialNativeError = new("injected native event failure after commit");
ItemManager.RespawnBehavior = (x, y) =>
{
    ItemManager.NativeCommitData(x, y, NewData(101));
    throw partialNativeError;
};
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    int notified = 0;
    service.GroundItemsChanged += () => notified++;
    WorldMaintenanceResult result = RunRestock(service);
    Check(result?.SucceededCount == 1 && result.FailedCount == 1 && result.OriginalCandidateCount == 2 &&
        WorldMaintenancePolicy.IsResultConserved(result),
        "A native partial commit preserves one success and accounts the remaining slot as failed.");
    Check(notified == 1 && region.items.Count == 1 && ItemManager.PendingDrops.Count == 1 && region.drops.Count == 0 &&
        ItemManager.LocalSpawnCalls == 0,
        "A native partial commit publishes once and retains exactly one pending native model.");
    Check(ItemManager.Cursor == (19, 23) && region.lastRespawn == Time.realtimeSinceStartup,
        "A native partial commit restores cursor and records the real commit time.");
    ItemManager.NativeUpdate();
    Check(region.drops.Count == 1 && region.drops[0].instanceID == 101 && ItemManager.PendingDrops.Count == 0,
        "Native Update instantiates the partially committed item exactly once.");
}

// The same native exception before any commit remains a complete failure and emits no write event.
region = ItemManager.Reset();
LevelItems.spawns[0, 0].Add(new ItemSpawnpoint { point = new Vector3(20f, 0f, 10f) });
ItemManager.RespawnBehavior = (x, y) => throw new InvalidOperationException("injected event failure before commit");
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    int notified = 0;
    service.GroundItemsChanged += () => notified++;
    WorldMaintenanceResult result = RunRestock(service);
    Check(result?.SucceededCount == 0 && result.FailedCount == 2 && result.OriginalCandidateCount == 2 &&
        WorldMaintenancePolicy.IsResultConserved(result),
        "A native failure before commit preserves zero successes and accounts both slots as failed.");
    Check(notified == 0 && region.items.Count == 0 && ItemManager.PendingDrops.Count == 0 &&
        ItemManager.Cursor == (19, 23) && region.lastRespawn == 123f,
        "A native failure before commit emits no notification and restores cursor and clock.");
}

// Generation shares the result-returning runner overload, including its zero-result path.
region = ItemManager.Reset();
Zombie zombie = new() { isDead = true, bound = 0 };
ZombieManager.AllZombies.Add(zombie);
ZombieManager.regions = new[] { new ZombieRegion { zombies = new List<Zombie> { zombie } } };
LevelZombies.zombies = new[] { new List<ZombieSpawnpoint>() };
using (WorldMaintenanceService service = new(new ManualLogSource(), () => true))
{
    bool previewed = service.TryCreatePreview(WorldMaintenanceActionKind.GenerateZombies,
        WorldMaintenanceScopeKind.LoadedWorld, 250f, false, out WorldMaintenancePreview preview, out _);
    bool started = previewed && service.TryStart(preview, out _, out _);
    if (started) service.Pump();
    WorldMaintenanceResult result = service.CaptureDashboard(250f).Result;
    Check(started && result?.SucceededCount == 0 && WorldMaintenancePolicy.IsResultConserved(result),
        "Zombie generation with no native spawnpoint reports zero actual successes.");
}

// The native clear-weather API does not reopen the gate closed by DisableWeather.
// Restoration must use the map's retained schedule, including maps without weather.
foreach (LevelAsset.SchedulableWeather[] schedule in new LevelAsset.SchedulableWeather[][]
    { new LevelAsset.SchedulableWeather[1], Array.Empty<LevelAsset.SchedulableWeather>(), null })
{
    LightingManager.Reset(schedule);
    LightingManager.DisableWeather();
    WorldWeatherNativeBridge.RestoreNaturalWeather();
    Check(LightingManager.ShouldTickScheduledWeather == (schedule != null && schedule.Length != 0) &&
        !LightingManager.ActiveWeather && LightingManager.ResetCalls == 1 &&
        ReferenceEquals(LightingManager.ScheduledWeathers, schedule),
        "Restore Natural reopens disabled scheduling only when the retained native map schedule exists.");
}

Console.WriteLine($"WORLD_MAINTENANCE_NATIVE_SMOKE checks={checks} failed={failures.Count}");
return failures.Count == 0 ? 0 : 1;
