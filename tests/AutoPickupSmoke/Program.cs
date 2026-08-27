using System.Diagnostics;
using UnturnedSingleplayerCheatMenu.AutoPickup;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

AutoPickupSettings minimum = new AutoPickupSettings
{
    Range = -10f,
    FrequencyHz = 100f,
    RequestsPerTick = 99
}.Normalize();
Assert(minimum.Range == AutoPickupSettings.MinRange, "Pickup range must clamp to the user-visible 1m minimum.");
Assert(minimum.FrequencyHz == 20f, "Frequency must retain the 20Hz hard ceiling.");
Assert(minimum.RequestsPerTick == 16, "Request budget must retain the 16-request hard ceiling.");

AutoPickupSettings maximum = new AutoPickupSettings { Range = 5000f }.Normalize();
Assert(maximum.Range == AutoPickupSettings.MaxRange, "Pickup range must clamp to the user-visible 1000m maximum.");
AutoPickupSettings dropBlock = new AutoPickupSettings { DropBlockMinutes = 0 }.Normalize();
Assert(dropBlock.DropBlockMinutes == AutoPickupSettings.MinDropBlockMinutes, "Drop block duration must clamp to the 1-minute minimum.");
dropBlock.DropBlockMinutes = 9999;
dropBlock.Normalize();
Assert(dropBlock.DropBlockMinutes == AutoPickupSettings.MaxDropBlockMinutes, "Drop block duration must clamp to the 1440-minute maximum.");
AutoPickupSettings qualityFilter = new AutoPickupSettings { QualityFilterEnabled = true, MinimumQuality = -1 }.Normalize();
Assert(qualityFilter.QualityFilterEnabled && qualityFilter.MinimumQuality == AutoPickupSettings.MinQuality,
    "Quality filtering must retain its enabled state and clamp the threshold to zero.");
qualityFilter.MinimumQuality = 101;
qualityFilter.Normalize();
Assert(qualityFilter.MinimumQuality == AutoPickupSettings.MaxQuality,
    "Quality filtering must clamp its threshold to 100.");
qualityFilter.MinimumQuality = 50;
Assert(!qualityFilter.AllowsQuality(49) && qualityFilter.AllowsQuality(50),
    "Enabled quality filtering must reject only items below the configured threshold.");
qualityFilter.QualityFilterEnabled = false;
Assert(qualityFilter.AllowsQuality(0), "Disabled quality filtering must allow low-quality items.");
AutoPickupDropBlockRegistry dropBlocks = new();
dropBlocks.Block(81, 10f, 60f);
Assert(dropBlocks.IsBlocked(81, 69.99f) && !dropBlocks.IsBlocked(81, 70f),
    "A dropped item ID must remain blocked for the configured duration and unblock exactly at expiry.");
dropBlocks.Block(81, 100f, 120f);
dropBlocks.Block(81, 110f, 120f);
Assert(dropBlocks.IsBlocked(81, 229.99f) && !dropBlocks.IsBlocked(81, 230f),
    "Dropping the same ID again must restart the block window from the latest drop time.");

AutoPickupItemDescriptor rifle = new()
{
    AssetId = 363,
    Guid = "b7d76a8f-e9e8-47aa-a8a5-d0f2381ec657",
    AssetName = "Maplestrike",
    DisplayName = "Maplestrike Rifle",
    ItemType = "Gun",
    Category = "Weapons"
};

ItemFilterMatcher matcher = new();
AutoPickupRuleLoadResult emptyLoad = matcher.Replace(true, AutoPickupFilterMode.WhitelistOnly, [], out _);
Assert(emptyLoad.Accepted == 0 && matcher.Mode == AutoPickupFilterMode.WhitelistThenBlacklist && !matcher.Matches(rifle),
    "An enabled empty whitelist must reject items until an allow card is added.");
matcher.Replace(false, AutoPickupFilterMode.BlacklistOnly, [], out _);
Assert(matcher.Mode == AutoPickupFilterMode.BlacklistOnly && matcher.Matches(rifle),
    "Disabling whitelist mode must restore unrestricted pickup when the blacklist is empty.");

List<ItemFilterRule> blacklistRules =
[
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "363" },
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "363" },
    new() { Enabled = false, Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "200" },
    new() { Action = ItemFilterRuleAction.Allow, Field = ItemFilterRuleField.AssetId, Value = "100" },
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.Keyword, Value = "maple" },
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "not-an-id" }
];
AutoPickupRuleLoadResult blacklistLoad = matcher.Replace(true, AutoPickupFilterMode.AllowAll, blacklistRules, out IReadOnlyList<ItemFilterRule> normalized);
Assert(blacklistLoad.Accepted == 2 && blacklistLoad.Rejected == 4 && normalized.Count == 2,
    "Unique allow and deny item-ID cards must survive while unsupported legacy rules are rejected.");
Assert(!matcher.Matches(rifle), "A blacklisted item ID must be rejected.");

matcher.Replace(false, AutoPickupFilterMode.BlacklistOnly, normalized, out _);
Assert(!matcher.Matches(rifle), "Legacy global filtering flags must not silently disable the card-managed blacklist.");

AutoPickupItemDescriptor civilianItem = new() { AssetId = 81, DisplayName = "MRE" };
AutoPickupRuleLoadResult whitelistLoad = matcher.Replace(true, AutoPickupFilterMode.WhitelistThenBlacklist,
[
    new() { Action = ItemFilterRuleAction.Allow, Field = ItemFilterRuleField.AssetId, Value = "363" },
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "100" }
], out IReadOnlyList<ItemFilterRule> whitelistRules);
Assert(whitelistLoad.Accepted == 2 && whitelistRules.Count == 2, "Whitelist mode must retain both allow and deny item-ID cards.");
Assert(matcher.Matches(rifle) && !matcher.Matches(civilianItem), "Enabled whitelist mode must pick only explicitly allowed item IDs.");
matcher.Replace(true, AutoPickupFilterMode.WhitelistThenBlacklist,
[
    new() { Action = ItemFilterRuleAction.Allow, Field = ItemFilterRuleField.AssetId, Value = "363" },
    new() { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = "363" }
], out _);
Assert(!matcher.Matches(rifle), "Blacklist must override a matching whitelist card.");

List<ItemFilterRule> scaleRules = new(10_000);
for (int i = 1; i <= 10_000; i++)
    scaleRules.Add(new ItemFilterRule { Action = ItemFilterRuleAction.Deny, Field = ItemFilterRuleField.AssetId, Value = i.ToString() });
Stopwatch stopwatch = Stopwatch.StartNew();
AutoPickupRuleLoadResult scaleLoad = matcher.Replace(true, AutoPickupFilterMode.BlacklistOnly, scaleRules, out _);
for (int i = 1; i <= 10_000; i++)
    Assert(!matcher.Matches(new AutoPickupItemDescriptor { AssetId = (ushort)i }), "Every indexed item ID should remain blacklisted.");
stopwatch.Stop();
Assert(scaleLoad.Accepted == 10_000 && scaleLoad.Rejected == 0, "The 10,000-ID blacklist must retain every unique valid rule.");
Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"10,000 indexed matches took too long: {stopwatch.Elapsed}.");

AutoPickupScheduler scheduler = new();
Assert(scheduler.ShouldScan(1f, 10f), "The first due scan should run.");
Assert(!scheduler.ShouldScan(1.05f, 10f), "The scheduler should not scan before the 100ms interval.");
Assert(scheduler.ShouldScan(1.1f, 10f), "The scheduler should scan when the interval elapses.");
float retryAt = scheduler.MarkFailure(42, 2f);
Assert(retryAt == 2.25f && !scheduler.CanRequest(42, 2.2f) && scheduler.CanRequest(42, 2.25f), "First failure backoff should be exactly 250ms.");
Assert(scheduler.MarkFailure(42, 2.25f) == 2.75f, "Second failure backoff should be exactly 500ms.");
scheduler.MarkSuccess(42);
Assert(scheduler.CanRequest(42, 2.25f), "Success should clear per-instance backoff.");
Assert(scheduler.MarkCapacityBlocked(42, 3f) == 8f && !scheduler.CanRequest(42, 7.99f),
    "A full inventory must pause retries for the rejected item without sending native requests.");
scheduler.MarkSuccess(42);
scheduler.Reset(10f);
Assert(scheduler.TakeRequestBudget(10f, 16) == 1, "Native request throttling should allow one immediate request after reset.");
Assert(scheduler.TakeRequestBudget(10.05f, 16) == 0, "Native request throttling should not exceed 10 requests per second.");
Assert(scheduler.TakeRequestBudget(10.11f, 16) == 1, "One native request token should refill after at least 100ms.");

string tempDirectory = Path.Combine(Path.GetTempPath(), "AutoPickupSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDirectory);
try
{
    string rulesPath = Path.Combine(tempDirectory, "rules.json");
    AutoPickupRuleStore store = new(rulesPath);
    AutoPickupRuleLoadResult save = store.ReplaceAndSave(true, AutoPickupFilterMode.WhitelistThenBlacklist, whitelistRules, matcher);
    Assert(save.Success && save.Accepted == 2 && File.Exists(rulesPath) && !File.Exists(rulesPath + ".tmp"),
        $"Atomic save should publish both whitelist and blacklist cards. Error={save.Error}");
    AutoPickupRuleStore reloaded = new(rulesPath);
    AutoPickupRuleLoadResult load = reloaded.Load(new ItemFilterMatcher());
    Assert(load.Success && load.Accepted == 2 && reloaded.Document.Enabled && reloaded.Document.Mode == AutoPickupFilterMode.WhitelistThenBlacklist,
        "Enabled whitelist state and both card types must round-trip.");

    File.WriteAllText(rulesPath, "{\"Version\":1,\"Enabled\":true,\"Mode\":\"BlacklistOnly\",\"Rules\":[]}");
    AutoPickupRuleStore migrated = new(rulesPath);
    migrated.Load(new ItemFilterMatcher());
    Assert(!migrated.Document.Enabled && migrated.Document.Mode == AutoPickupFilterMode.BlacklistOnly,
        "Legacy blacklist-only documents must not accidentally enable the repurposed whitelist flag.");
}
finally
{
    Directory.Delete(tempDirectory, recursive: true);
}

string repositoryRoot = Environment.CurrentDirectory;
string runtimeSourcePath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "AutoPickup", "AutoPickupRuntime.cs");
string pluginSourcePath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "CheatMenuPlugin.cs");
string uiSourcePath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.cs");
Assert(File.Exists(runtimeSourcePath) && File.Exists(pluginSourcePath) && File.Exists(uiSourcePath), "Run AutoPickupSmoke from the repository root so runtime contracts can be inspected.");
string runtimeSource = File.ReadAllText(runtimeSourcePath);
string pluginSource = File.ReadAllText(pluginSourcePath);
string uiSource = File.ReadAllText(uiSourcePath);
Assert(runtimeSource.Contains("SingleplayerGuard.IsReady", StringComparison.Ordinal), "Every runtime pump must retain the singleplayer hard gate.");
Assert(runtimeSource.Contains("ItemManager.takeItem", StringComparison.Ordinal), "Pickup must use the game's native ItemManager.takeItem path.");
Assert(!runtimeSource.Contains("Physics.Raycast", StringComparison.Ordinal) && !runtimeSource.Contains("Physics.Linecast", StringComparison.Ordinal), "Drop scanning must not add a visibility ray gate.");
Assert(!runtimeSource.Contains("player.transform.position =", StringComparison.Ordinal), "Auto pickup must never move the player to reach a drop.");
Assert(runtimeSource.Contains("HasInventorySpace", StringComparison.Ordinal) && runtimeSource.Contains("tryFindSpace", StringComparison.Ordinal),
    "Auto pickup must check carried-inventory space before sending a native pickup request.");
Assert(runtimeSource.IndexOf("HasInventorySpace(player.inventory", StringComparison.Ordinal) < runtimeSource.IndexOf("ItemManager.takeItem", StringComparison.Ordinal),
    "Inventory capacity must be checked before the native pickup request is sent.");
Assert(runtimeSource.Contains("PreferInfiniteStorage", StringComparison.Ordinal) && runtimeSource.Contains("TryStoreCandidate", StringComparison.Ordinal),
    "Auto pickup must support an explicit infinite-storage priority path.");
Assert(runtimeSource.IndexOf("prefersInfiniteStorage && TryStoreCandidate", StringComparison.Ordinal) < runtimeSource.IndexOf("HasInventorySpace(player.inventory", StringComparison.Ordinal),
    "When priority is enabled, infinite storage must be tried before the carried-inventory capacity check.");
Assert(runtimeSource.Contains("plugin?.IsInfiniteStoragePickupPriorityEnabled == true", StringComparison.Ordinal) && runtimeSource.Contains("plugin.InfiniteStorage?.IsOperational == true", StringComparison.Ordinal),
    "Pickup priority must require both its setting and an operational infinite-storage runtime.");
Assert(runtimeSource.Contains("!_settings.AllowsQuality(candidate.Quality)", StringComparison.Ordinal),
    "Enabled quality filtering must reject candidates below the persisted minimum before pickup execution.");
Assert(pluginSource.Contains("Config.Bind(\"AutoPickup\", \"QualityFilterEnabled\"", StringComparison.Ordinal)
    && pluginSource.Contains("Config.Bind(\"AutoPickup\", \"MinimumQuality\"", StringComparison.Ordinal)
    && pluginSource.Contains("PersistInterfaceState(_autoPickupQualityFilterEnabled", StringComparison.Ordinal)
    && pluginSource.Contains("PersistInterfaceState(_autoPickupMinimumQuality", StringComparison.Ordinal),
    "The quality-filter toggle and minimum threshold must both persist through BepInEx configuration.");
Assert(runtimeSource.Contains("onDropItemRequested", StringComparison.Ordinal) && runtimeSource.Contains("onItemDropAdded", StringComparison.Ordinal)
    && runtimeSource.Contains("onItemDropRemoved", StringComparison.Ordinal) && runtimeSource.Contains("IsSuppressed", StringComparison.Ordinal),
    "Player-dropped item instances must be suppressed until their world drop is removed.");
Assert(runtimeSource.Contains("SuppressedItemIds", StringComparison.Ordinal) && runtimeSource.Contains("DropBlockMinutes", StringComparison.Ordinal)
	&& runtimeSource.Contains("SuppressedItemIds.Block(item.id, now, _dropBlockDurationSeconds)", StringComparison.Ordinal),
    "Player drops must temporarily suppress every ground instance sharing the dropped item ID.");
Assert(runtimeSource.Contains("model.parent == null ? model : model.parent", StringComparison.Ordinal),
    "Drop suppression must normalize Unturned's item-child callback model to the ItemDrop root model.");
Assert(uiSource.Contains("CreateAutoPickupSlider(page, \"拾取距离\", settings.Range, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange", StringComparison.Ordinal),
    "The UI must expose one unified 1-1000m pickup-distance slider.");
Assert(!uiSource.Contains("\"扫描半径\"", StringComparison.Ordinal) && !uiSource.Contains("\"请求距离\"", StringComparison.Ordinal),
    "The active UI must not expose separate scan-radius or request-distance controls.");
Assert(uiSource.Contains("CreateTimeSlider(row.transform", StringComparison.Ordinal) && uiSource.Contains("Mathf.Round(Mathf.Lerp(min, max", StringComparison.Ordinal),
    "Pickup distance must reuse the time slider visual and quantize input.");
Assert(uiSource.Contains("GetCurrentBackpackAssets", StringComparison.Ordinal) && uiSource.Contains("HashSet<ushort> seen", StringComparison.Ordinal),
    "The picker must enumerate the current backpack and deduplicate item IDs.");
Assert(uiSource.Contains("AssetHoverDetailsBuilder.BuildItem", StringComparison.Ordinal) && uiSource.Contains("delayTooltip: true", StringComparison.Ordinal),
    "Blacklist cards must retain delayed full item hover details.");
Assert(uiSource.Contains("丢弃后禁拾时长", StringComparison.Ordinal) && uiSource.Contains("SetAutoPickupDropBlockMinutes", StringComparison.Ordinal),
	"The auto-pickup settings page must expose the configurable post-drop item-ID block duration.");
Assert(uiSource.Contains("启用品质过滤", StringComparison.Ordinal) && uiSource.Contains("SetAutoPickupQualityFilterEnabled", StringComparison.Ordinal)
    && uiSource.Contains("SetAutoPickupMinimumQuality", StringComparison.Ordinal),
    "The auto-pickup settings page must expose a persistent quality-filter toggle and 0-100 threshold input.");
Assert(!uiSource.Contains("自动拾取（实验性）", StringComparison.Ordinal), "The auto-pickup UI must not retain the experimental label.");
int autoPickupUiStart = uiSource.IndexOf("private void BuildAutoPickupPanel", StringComparison.Ordinal);
int autoPickupUiEnd = uiSource.IndexOf("private static string AutoPickupDiagnosticsLabel", autoPickupUiStart, StringComparison.Ordinal);
string autoPickupUi = uiSource.Substring(autoPickupUiStart, autoPickupUiEnd - autoPickupUiStart);
Assert(autoPickupUi.Contains("CreateAutoPickupPageDots(pager.transform, _autoPickupPage, pageCount)", StringComparison.Ordinal)
    && autoPickupUi.Contains("AddPageScrollHandler(pageBody, _autoPickupPage, pageCount", StringComparison.Ordinal)
    && autoPickupUi.Contains("AddPageScrollHandler(grid, _autoPickupBlacklistPickerPage", StringComparison.Ordinal),
    "The main auto-pickup pages and backpack picker must support mouse-wheel paging.");
Assert(!autoPickupUi.Contains("BuildPagination", StringComparison.Ordinal) && !autoPickupUi.Contains("上一页", StringComparison.Ordinal)
    && !autoPickupUi.Contains("下一页", StringComparison.Ordinal),
    "Auto-pickup paging must not render previous/next buttons.");
int addRuleStart = autoPickupUi.IndexOf("private void AddAutoPickupRule", StringComparison.Ordinal);
int removeRuleStart = autoPickupUi.IndexOf("private void RemoveAutoPickupRule", addRuleStart, StringComparison.Ordinal);
string addRuleUi = autoPickupUi.Substring(addRuleStart, removeRuleStart - addRuleStart);
Assert(addRuleUi.Contains("RefreshAutoPickupPanel", StringComparison.Ordinal),
    "Adding a blacklist card must immediately rebuild the underlying management page.");
Assert(autoPickupUi.Contains("启用白名单", StringComparison.Ordinal) && autoPickupUi.Contains("白名单物品", StringComparison.Ordinal)
    && autoPickupUi.Contains("BuildAutoPickupRulePage", StringComparison.Ordinal),
    "Whitelist settings and the shared card-management page must remain present.");

Console.WriteLine($"Auto-pickup smoke checks passed; 10,000 indexed matches completed in {stopwatch.Elapsed.TotalMilliseconds:0.0} ms.");
