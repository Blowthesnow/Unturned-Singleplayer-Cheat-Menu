using System.Diagnostics;
using System.Text;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.Services;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

AutoPickupRuleDocument destructionRules = new AutoPickupRuleDocument
{
    Enabled = true,
    Mode = AutoPickupFilterMode.WhitelistThenBlacklist,
    Rules = new List<ItemFilterRule>
    {
        new ItemFilterRule
        {
            Action = ItemFilterRuleAction.Allow,
            Field = ItemFilterRuleField.AssetId,
            Value = "41"
        }
    }
};
List<AutoPickupRuleDocument> destructionRuleWrites = new List<AutoPickupRuleDocument>();
int destructionCalls = 0;
InfiniteStorageDestructionResult coordinatedDestruction = InfiniteStorageDestructionCoordinator.Execute(
    42,
    destructionRules,
    (enabled, mode, rules) =>
    {
        destructionRuleWrites.Add(new AutoPickupRuleDocument
        {
            Enabled = enabled,
            Mode = mode,
            Rules = rules.Select(rule => rule.Clone()).ToList()
        });
        return new AutoPickupRuleLoadResult(destructionRuleWrites.Last().Rules.Count, 0, string.Empty);
    },
    () =>
    {
        destructionCalls++;
        return SnapshotDestructionResult.Succeeded(5);
    });
Assert(coordinatedDestruction.Success && coordinatedDestruction.DestroyedCount == 5 &&
       coordinatedDestruction.BlacklistAdded && destructionCalls == 1 &&
       destructionRuleWrites.Count == 1 && destructionRuleWrites[0].Enabled &&
       destructionRuleWrites[0].Mode == AutoPickupFilterMode.WhitelistThenBlacklist &&
       destructionRuleWrites[0].Rules.Any(rule => rule.Enabled &&
           rule.Action == ItemFilterRuleAction.Deny && rule.Field == ItemFilterRuleField.AssetId &&
           rule.Value == "42"),
    "Destruction must persist an enabled item-ID deny rule before deleting Snapshot instances while preserving requested whitelist state.");

List<AutoPickupRuleDocument> rollbackRuleWrites = new List<AutoPickupRuleDocument>();
bool storageFailureReached = false;
InfiniteStorageDestructionResult rolledBackDestruction = InfiniteStorageDestructionCoordinator.Execute(
    43,
    destructionRules,
    (enabled, mode, rules) =>
    {
        rollbackRuleWrites.Add(new AutoPickupRuleDocument
        {
            Enabled = enabled,
            Mode = mode,
            Rules = rules.Select(rule => rule.Clone()).ToList()
        });
        return new AutoPickupRuleLoadResult(rollbackRuleWrites.Last().Rules.Count, 0, string.Empty);
    },
    () =>
    {
        storageFailureReached = true;
        return SnapshotDestructionResult.Failed("injected-storage-failure", rollbackSucceeded: true);
    });
Assert(!rolledBackDestruction.Success && storageFailureReached &&
       rolledBackDestruction.BlacklistRollbackSucceeded && rollbackRuleWrites.Count == 2 &&
       rollbackRuleWrites[0].Rules.Any(rule => rule.Action == ItemFilterRuleAction.Deny && rule.Value == "43") &&
       !rollbackRuleWrites[1].Rules.Any(rule => rule.Action == ItemFilterRuleAction.Deny && rule.Value == "43"),
    "A safely rolled-back storage destruction must also roll back the newly-added blacklist rule.");

AutoPickupRuleDocument alreadyBlockedRules = new AutoPickupRuleDocument
{
    Rules = new List<ItemFilterRule>
    {
        new ItemFilterRule
        {
            Enabled = true,
            Action = ItemFilterRuleAction.Deny,
            Field = ItemFilterRuleField.AssetId,
            Value = "44"
        }
    }
};
int alreadyBlockedWrites = 0;
InfiniteStorageDestructionResult alreadyBlockedDestruction = InfiniteStorageDestructionCoordinator.Execute(
    44,
    alreadyBlockedRules,
    (_, _, _) =>
    {
        alreadyBlockedWrites++;
        return new AutoPickupRuleLoadResult(0, 0, string.Empty);
    },
    () => SnapshotDestructionResult.Succeeded(1));
Assert(alreadyBlockedDestruction.Success && !alreadyBlockedDestruction.BlacklistAdded &&
       alreadyBlockedWrites == 0,
    "An existing enabled item-ID blacklist rule must be reused without duplicating or rewriting it.");

static AutoPickupPendingConfirmationOutcome InvokePendingConfirmation(
    bool worldDropIsCurrent,
    bool timeoutElapsed,
    AutoPickupInventoryEvidence before,
    AutoPickupInventoryEvidence after,
    bool storageResult,
    uint instanceId,
    HashSet<uint> pending,
    AutoPickupScheduler scheduler,
    out int storageCalls,
    out int nativeDeliveryCalls,
    out int suppressCalls,
    out int completeCalls)
{
    int localStorageCalls = 0;
    int localNativeDeliveryCalls = 0;
    int localSuppressCalls = 0;
    int localCompleteCalls = 0;
    AutoPickupPendingConfirmationOutcome result = AutoPickupPendingConfirmationCoordinator.Execute(
        worldDropIsCurrent,
        timeoutElapsed,
        before,
        after,
        (Func<bool>)(() =>
        {
            localStorageCalls++;
            return storageResult;
        }),
        (Action)(() =>
        {
            localNativeDeliveryCalls++;
            scheduler.MarkSuccess(instanceId);
        }),
        (Action)(() =>
        {
            localSuppressCalls++;
            scheduler.SuppressUntilReset(instanceId);
        }),
        (Action)(() =>
        {
            localCompleteCalls++;
            pending.Remove(instanceId);
        }));
    storageCalls = localStorageCalls;
    nativeDeliveryCalls = localNativeDeliveryCalls;
    suppressCalls = localSuppressCalls;
    completeCalls = localCompleteCalls;
    return result;
}

static NativePickupAttempt NormalizeNativeAttemptForSmoke(NativePickupAttempt attempt,
    AutoPickupNativePostAction action)
{
    return AutoPickupFinalAttemptPolicy.NormalizeNativeAttempt(attempt, action);
}

static NativePickupAttempt CaptureNativeAttemptForSmoke(Func<NativePickupAttempt> callback,
    AutoPickupInventoryEvidence baseline)
{
    return AutoPickupFinalAttemptPolicy.CaptureNativeCallback(callback, baseline);
}

static NativePickupAttempt BuildFailedAttemptForSmoke(AutoPickupInventoryEvidence baseline,
    bool nativeAttempted, NativePickupAttempt nativeAttempt,
    bool storageAttempted, NativePickupAttempt storageAttempt, bool nativePickupAvailable)
{
    return AutoPickupFinalAttemptPolicy.BuildFailedAttempt(baseline, nativeAttempted,
        nativeAttempt, storageAttempted, storageAttempt, nativePickupAvailable);
}

static void RecordAttemptForSmoke(NativePickupAttempt attempt, uint instanceId, float now,
    AutoPickupScheduler scheduler, out int requested, out int pickedUp, out int failures,
    out int pending)
{
    int localRequested = 0;
    int localPickedUp = 0;
    int localFailures = 0;
    int localPending = 0;
    AutoPickupAttemptRecordCoordinator.Execute(attempt, instanceId, now, scheduler,
        requestSent: () => localRequested++,
        pickedUp: () => localPickedUp++,
        pending: () => localPending++,
        capacityBlocked: () => localFailures++,
        reconciliationRequired: () => localFailures++,
        suppressed: () => localFailures++,
        failure: () => localFailures++);
    requested = localRequested;
    pickedUp = localPickedUp;
    failures = localFailures;
    pending = localPending;
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
AutoPickupSettings linkDefaults = new AutoPickupSettings();
Assert(!linkDefaults.UseMinimapRange && !linkDefaults.UseMinimapRarityFilter,
    "Minimap range/rarity links must remain explicit opt-ins.");
Assert(linkDefaults.AllowedRarityMask == AutoPickupSettings.AllRaritiesMask,
    "Every item rarity must be enabled by default for auto pickup.");
for (int rarity = 0; rarity < AutoPickupSettings.RarityCount; rarity++)
    Assert(linkDefaults.AllowsRarity(rarity), $"Default auto-pickup rarity mask must allow rarity index {rarity}.");
linkDefaults.AllowedRarityMask = AutoPickupSettings.SetRarityEnabled(linkDefaults.AllowedRarityMask, rarity: 2, enabled: false);
Assert(!linkDefaults.AllowsRarity(2) && linkDefaults.AllowsRarity(1) && linkDefaults.AllowsRarity(3),
    "Disabling one rarity must reject only that rarity.");
linkDefaults.AllowedRarityMask = AutoPickupSettings.AllRaritiesMask | (1 << 12);
linkDefaults.Normalize();
Assert(linkDefaults.AllowedRarityMask == AutoPickupSettings.AllRaritiesMask && linkDefaults.AllowsRarity(99),
    "Rarity-mask normalization must discard unsupported bits while unknown future rarities fail open.");
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
Assert(emptyLoad.Accepted == 0 && matcher.Mode == AutoPickupFilterMode.BlacklistOnly && matcher.Matches(rifle),
    "An enabled empty whitelist must fail open to blacklist-only mode instead of filtering every pickup candidate.");
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

int storageCalls = 0;
int nativeCalls = 0;
AutoPickupExecutionPath storedPath = AutoPickupExecutionCoordinator.Execute(true, true, true,
    () => { storageCalls++; return true; }, () => { nativeCalls++; return true; });
Assert(storedPath == AutoPickupExecutionPath.InfiniteStorage && storageCalls == 1 && nativeCalls == 0,
    "PreferInfiniteStorage success must store exactly once without also issuing a native pickup.");
storageCalls = nativeCalls = 0;
AutoPickupExecutionPath fallbackPath = AutoPickupExecutionCoordinator.Execute(true, true, true,
    () => { storageCalls++; return false; }, () => { nativeCalls++; return true; });
Assert(fallbackPath == AutoPickupExecutionPath.NativePickup && storageCalls == 1 && nativeCalls == 1,
    "Infinite-storage rejection must fall back to one native pickup attempt.");
storageCalls = nativeCalls = 0;
AutoPickupExecutionPath filteredPath = AutoPickupExecutionCoordinator.Execute(false, true, true,
    () => { storageCalls++; return true; }, () => { nativeCalls++; return true; });
Assert(filteredPath == AutoPickupExecutionPath.Filtered && storageCalls == 0 && nativeCalls == 0,
    "A filtered candidate must not invoke either storage or native pickup executors.");
storageCalls = nativeCalls = 0;
AutoPickupExecutionPath failedPath = AutoPickupExecutionCoordinator.Execute(true, true, false,
    () => { storageCalls++; return false; }, () => { nativeCalls++; return true; });
Assert(failedPath == AutoPickupExecutionPath.Failed && storageCalls == 1 && nativeCalls == 0,
    "When storage rejects and native pickup is unavailable, the item must remain unhandled rather than report success.");

foreach (AutoPickupResultKind acceptedResult in new[]
         {
             AutoPickupResultKind.PickedUp,
             AutoPickupResultKind.Requested
         })
{
    int acceptedNativeCalls = 0;
    int forbiddenStorageCalls = 0;
    AutoPickupInventoryEvidence acceptedBefore = default;
    AutoPickupInventoryEvidence acceptedAfter = acceptedResult == AutoPickupResultKind.PickedUp
        ? new AutoPickupInventoryEvidence(1, 1, 1)
        : default;
    AutoPickupNativePostAction acceptedAction = AutoPickupExecutionCoordinator.ResolveNativePostAction(
        acceptedResult,
        worldDropIsCurrent: acceptedResult == AutoPickupResultKind.Requested,
        inventoryBefore: acceptedBefore,
        inventoryAfter: acceptedAfter,
        requestSent: true);
    AutoPickupExecutionPath acceptedPath = AutoPickupExecutionCoordinator.Execute(
        allowed: true,
        preferInfiniteStorage: false,
        nativePickupAvailable: true,
        tryInfiniteStorage: () => { forbiddenStorageCalls++; return true; },
        tryNativePickup: () =>
        {
            acceptedNativeCalls++;
            return AutoPickupExecutionCoordinator.StopsNativeFallback(acceptedAction);
        });
    string expectedAcceptedAction = acceptedResult == AutoPickupResultKind.PickedUp
        ? "AcceptNative"
        : "AwaitNativeConfirmation";
    Assert(acceptedAction.ToString() == expectedAcceptedAction &&
           acceptedPath == AutoPickupExecutionPath.NativePickup &&
           acceptedNativeCalls == 1 && forbiddenStorageCalls == 0,
        acceptedResult + " must use its explicit native outcome without triggering a second Snapshot storage write.");
}
int zeroAmountNativeCalls = 0;
int zeroAmountStorageCalls = 0;
AutoPickupInventoryEvidence zeroAmountBefore = new AutoPickupInventoryEvidence(
    matchingAssetJarCount: 0, totalAmount: 0, exactSnapshotJarCount: 0);
AutoPickupInventoryEvidence zeroAmountAfter = new AutoPickupInventoryEvidence(
    matchingAssetJarCount: 1, totalAmount: 0, exactSnapshotJarCount: 1);
AutoPickupNativePostAction zeroAmountAction = AutoPickupExecutionCoordinator.ResolveNativePostAction(
    AutoPickupResultKind.UnknownFailure, worldDropIsCurrent: false,
    inventoryBefore: zeroAmountBefore, inventoryAfter: zeroAmountAfter,
    requestSent: true);
AutoPickupExecutionPath zeroAmountPath = AutoPickupExecutionCoordinator.Execute(
    allowed: true,
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { zeroAmountStorageCalls++; return true; },
    tryNativePickup: () =>
    {
        zeroAmountNativeCalls++;
        return AutoPickupExecutionCoordinator.StopsNativeFallback(zeroAmountAction);
    });
Assert(zeroAmountAction == AutoPickupNativePostAction.AcceptNative &&
       zeroAmountPath == AutoPickupExecutionPath.NativePickup &&
       zeroAmountNativeCalls == 1 && zeroAmountStorageCalls == 0,
    "A stale world drop delivered as one new amount=0 ItemJar must count as native delivery and must never be copied into Snapshot storage.");

(string Name, AutoPickupInventoryEvidence Before, AutoPickupInventoryEvidence After)[] ordinaryDeliveries =
{
    ("amount merged into an existing jar",
        new AutoPickupInventoryEvidence(1, 5, 0),
        new AutoPickupInventoryEvidence(1, 10, 0)),
    ("positive-amount item delivered as a new jar",
        new AutoPickupInventoryEvidence(1, 5, 0),
        new AutoPickupInventoryEvidence(2, 10, 1)),
    ("candidate-exact snapshot became observable while coarse totals stayed equal",
        new AutoPickupInventoryEvidence(1, 10, 0),
        new AutoPickupInventoryEvidence(1, 10, 1))
};
foreach ((string name, AutoPickupInventoryEvidence before, AutoPickupInventoryEvidence after) in ordinaryDeliveries)
{
    int ordinaryStorageCalls = 0;
    AutoPickupNativePostAction action = AutoPickupExecutionCoordinator.ResolveNativePostAction(
        AutoPickupResultKind.UnknownFailure, worldDropIsCurrent: false,
        inventoryBefore: before, inventoryAfter: after, requestSent: true);
    AutoPickupExecutionPath path = AutoPickupExecutionCoordinator.Execute(
        allowed: true, preferInfiniteStorage: false, nativePickupAvailable: true,
        tryInfiniteStorage: () => { ordinaryStorageCalls++; return true; },
        tryNativePickup: () => AutoPickupExecutionCoordinator.StopsNativeFallback(action));
    Assert(action == AutoPickupNativePostAction.AcceptNative &&
           path == AutoPickupExecutionPath.NativePickup && ordinaryStorageCalls == 0,
        name + " must count as native delivery without a Snapshot duplicate.");
}
AutoPickupInventoryEvidence provenDeliveryBefore = new AutoPickupInventoryEvidence(1, 5, 0);
AutoPickupInventoryEvidence provenDeliveryAfter = new AutoPickupInventoryEvidence(2, 10, 1);
NativePickupAttempt unknownButDelivered = new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
    provenDeliveryBefore, provenDeliveryAfter,
    "原生异常/Native detail: code=E42\r\nstate=unchanged", requestSent: true);
AutoPickupNativePostAction provenDeliveryAction = AutoPickupExecutionCoordinator.ResolveNativePostAction(
    unknownButDelivered.Result, worldDropIsCurrent: false,
    unknownButDelivered.InventoryBefore, unknownButDelivered.InventoryAfter,
    unknownButDelivered.RequestSent);
NativePickupAttempt normalizedDelivered = NormalizeNativeAttemptForSmoke(
    unknownButDelivered, provenDeliveryAction);
int normalizedStorageCalls = 0;
AutoPickupExecutionPath normalizedDeliveryPath = AutoPickupExecutionCoordinator.Execute(
    allowed: true, preferInfiniteStorage: false, nativePickupAvailable: true,
    tryInfiniteStorage: () => { normalizedStorageCalls++; return true; },
    tryNativePickup: () => AutoPickupExecutionCoordinator.StopsNativeFallback(provenDeliveryAction));
const uint normalizedDeliveryId = 1199;
AutoPickupScheduler normalizedDeliveryScheduler = new AutoPickupScheduler();
normalizedDeliveryScheduler.Reset(0f);
normalizedDeliveryScheduler.MarkFailure(normalizedDeliveryId, 0f);
RecordAttemptForSmoke(normalizedDelivered, normalizedDeliveryId, 0f,
    normalizedDeliveryScheduler, out int normalizedRequested, out int normalizedPickedUp,
    out int normalizedFailures, out int normalizedPending);
Assert(provenDeliveryAction == AutoPickupNativePostAction.AcceptNative &&
       normalizedDelivered.Result == AutoPickupResultKind.PickedUp &&
       normalizedDelivered.InventoryBefore.Equals(provenDeliveryBefore) &&
       normalizedDelivered.InventoryAfter.Equals(provenDeliveryAfter) &&
       normalizedDelivered.Detail.StartsWith(unknownButDelivered.Detail, StringComparison.Ordinal) &&
       Encoding.UTF8.GetBytes(normalizedDelivered.Detail)
           .Take(Encoding.UTF8.GetByteCount(unknownButDelivered.Detail))
           .SequenceEqual(Encoding.UTF8.GetBytes(unknownButDelivered.Detail)) &&
       normalizedDelivered.Detail.Contains("背包复合证据已确认原生拾取交付。",
           StringComparison.Ordinal) &&
       normalizedDelivered.RequestSent && normalizedDeliveryPath == AutoPickupExecutionPath.NativePickup &&
       normalizedStorageCalls == 0 && normalizedRequested == 1 && normalizedPickedUp == 1 &&
       normalizedFailures == 0 && normalizedPending == 0 &&
       normalizedDeliveryScheduler.CanRequest(normalizedDeliveryId, 0f),
    "A stale Unknown native result with proven delivery must become the final PickedUp attempt before RecordAttempt accounting.");

NativePickupAttempt emptyDeliveredDetail = NormalizeNativeAttemptForSmoke(
    new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
        provenDeliveryBefore, provenDeliveryAfter, string.Empty, requestSent: true),
    AutoPickupNativePostAction.AcceptNative);
Assert(emptyDeliveredDetail.Detail == "背包复合证据已确认原生拾取交付。",
    "Evidence-proven delivery with an empty native detail must receive one structured localizable explanation.");
NativePickupAttempt normalizedDeliveredTwice = NormalizeNativeAttemptForSmoke(
    normalizedDelivered, AutoPickupNativePostAction.AcceptNative);
Assert(normalizedDeliveredTwice.Detail == normalizedDelivered.Detail,
    "Normalizing an already proven PickedUp attempt must not append the evidence confirmation twice.");

AutoPickupInventoryEvidence doubleFailureBaseline = new AutoPickupInventoryEvidence(1, 5, 0);
int falseNativeCallbackCalls = 0;
NativePickupAttempt falseNativeAttempt = CaptureNativeAttemptForSmoke(
    () =>
    {
        falseNativeCallbackCalls++;
        return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
            doubleFailureBaseline, doubleFailureBaseline,
            "原生掉落已失效但背包没有交付证据。", requestSent: true);
    }, doubleFailureBaseline);
int falseStorageCallbackCalls = 0;
AutoPickupStorageCallbackResult falseStorageCallback =
    AutoPickupFinalAttemptPolicy.CaptureStorageCallback(
    () =>
    {
        falseStorageCallbackCalls++;
        return new AutoPickupStorageCallbackResult(accepted: false, attempt: default);
    }, doubleFailureBaseline);
NativePickupAttempt falseStorageAttempt = falseStorageCallback.Attempt;
NativePickupAttempt falseRecoveryFinal = BuildFailedAttemptForSmoke(doubleFailureBaseline,
    nativeAttempted: true, falseNativeAttempt,
    storageAttempted: true, falseStorageAttempt, nativePickupAvailable: true);
const uint falseRecoveryId = 1198;
AutoPickupScheduler falseRecoveryScheduler = new AutoPickupScheduler();
falseRecoveryScheduler.Reset(0f);
RecordAttemptForSmoke(falseRecoveryFinal, falseRecoveryId, 0f, falseRecoveryScheduler,
    out int falseRecoveryRequested, out int falseRecoveryPickedUp,
    out int falseRecoveryFailures, out int falseRecoveryPending);
Assert(falseNativeCallbackCalls == 1 && falseStorageCallbackCalls == 1 &&
       !falseStorageCallback.Accepted &&
       falseRecoveryFinal.IsInitialized &&
       falseRecoveryFinal.Result == AutoPickupResultKind.UnknownFailure &&
       falseRecoveryFinal.RequestSent &&
       falseRecoveryFinal.Detail.Contains("原生拾取未确认交付", StringComparison.Ordinal) &&
       falseRecoveryFinal.Detail.Contains("无限储存恢复失败", StringComparison.Ordinal) &&
       falseRecoveryRequested == 1 && falseRecoveryPickedUp == 0 &&
       falseRecoveryFailures == 1 && falseRecoveryPending == 0 &&
       !falseRecoveryScheduler.CanRequest(falseRecoveryId, 0f),
    "A false Snapshot recovery after an unconfirmed native attempt must retain the real double failure through RecordAttempt.");

int throwingStorageCallbackCalls = 0;
AutoPickupStorageCallbackResult throwingStorageCallback =
    AutoPickupFinalAttemptPolicy.CaptureStorageCallback(
    () =>
    {
        throwingStorageCallbackCalls++;
        throw new InvalidOperationException("storage callback injected failure");
    }, doubleFailureBaseline);
NativePickupAttempt throwingStorageAttempt = throwingStorageCallback.Attempt;
NativePickupAttempt throwingStorageFinal = BuildFailedAttemptForSmoke(doubleFailureBaseline,
    nativeAttempted: true, falseNativeAttempt,
    storageAttempted: true, throwingStorageAttempt, nativePickupAvailable: true);
Assert(throwingStorageCallbackCalls == 1 && !throwingStorageCallback.Accepted &&
       throwingStorageAttempt.IsInitialized &&
       throwingStorageAttempt.Result == AutoPickupResultKind.UnknownFailure &&
       throwingStorageFinal.IsInitialized &&
       throwingStorageFinal.Result == AutoPickupResultKind.UnknownFailure &&
       throwingStorageFinal.RequestSent,
    "A throwing Snapshot recovery callback must produce an explicit double-failure attempt rather than default Requested.");

int throwingNativeCallbackCalls = 0;
int forbiddenStorageAfterNativeThrow = 0;
NativePickupAttempt throwingNativeAttempt = CaptureNativeAttemptForSmoke(
    () =>
    {
        throwingNativeCallbackCalls++;
        throw new InvalidOperationException("native callback injected failure");
    }, doubleFailureBaseline);
AutoPickupNativePostAction throwingNativeAction = throwingNativeAttempt.SuppressRetry
    ? AutoPickupNativePostAction.SuppressUncertainNative
    : AutoPickupExecutionCoordinator.ResolveNativePostAction(throwingNativeAttempt.Result,
        worldDropIsCurrent: false, throwingNativeAttempt.InventoryBefore,
        throwingNativeAttempt.InventoryAfter, throwingNativeAttempt.RequestSent);
AutoPickupExecutionPath throwingNativePath = AutoPickupExecutionCoordinator.Execute(
    allowed: true, preferInfiniteStorage: false, nativePickupAvailable: true,
    tryInfiniteStorage: () => { forbiddenStorageAfterNativeThrow++; return true; },
    tryNativePickup: () => AutoPickupExecutionCoordinator.StopsNativeFallback(throwingNativeAction));
const uint throwingNativeId = 1197;
AutoPickupScheduler throwingNativeScheduler = new AutoPickupScheduler();
throwingNativeScheduler.Reset(0f);
RecordAttemptForSmoke(throwingNativeAttempt, throwingNativeId, 0f, throwingNativeScheduler,
    out int throwingNativeRequested, out int throwingNativePickedUp,
    out int throwingNativeFailures, out int throwingNativePending);
Assert(throwingNativeCallbackCalls == 1 && forbiddenStorageAfterNativeThrow == 0 &&
       throwingNativePath == AutoPickupExecutionPath.NativePickup &&
       throwingNativeAttempt.IsInitialized &&
       throwingNativeAttempt.Result == AutoPickupResultKind.UnknownFailure &&
       throwingNativeAttempt.RequestSent && throwingNativeAttempt.SuppressRetry &&
       throwingNativeRequested == 1 && throwingNativePickedUp == 0 &&
       throwingNativeFailures == 1 && throwingNativePending == 0 &&
       !throwingNativeScheduler.CanRequest(throwingNativeId, float.MaxValue),
    "A throwing native callback must become an explicit suppressed failure and must never fall through as default Requested.");

NativePickupAttempt defaultNativeAttempt = CaptureNativeAttemptForSmoke(
    () => default, doubleFailureBaseline);
const uint defaultAttemptId = 1196;
AutoPickupScheduler defaultAttemptScheduler = new AutoPickupScheduler();
defaultAttemptScheduler.Reset(0f);
RecordAttemptForSmoke(defaultNativeAttempt, defaultAttemptId, 0f, defaultAttemptScheduler,
    out _, out int defaultAttemptPickedUp, out int defaultAttemptFailures,
    out int defaultAttemptPending);
Assert(defaultNativeAttempt.IsInitialized &&
       defaultNativeAttempt.Result == AutoPickupResultKind.UnknownFailure &&
       defaultAttemptPickedUp == 0 && defaultAttemptFailures == 1 &&
       defaultAttemptPending == 0 &&
       !defaultAttemptScheduler.CanRequest(defaultAttemptId, float.MaxValue),
    "An unassigned callback result must fail explicitly and can never masquerade as default enum value Requested.");

const uint rawDefaultAttemptId = 1195;
AutoPickupScheduler rawDefaultAttemptScheduler = new AutoPickupScheduler();
rawDefaultAttemptScheduler.Reset(0f);
RecordAttemptForSmoke(default, rawDefaultAttemptId, 0f, rawDefaultAttemptScheduler,
    out int rawDefaultRequested, out int rawDefaultPickedUp,
    out int rawDefaultFailures, out int rawDefaultPending);
Assert(rawDefaultRequested == 0 && rawDefaultPickedUp == 0 &&
       rawDefaultFailures == 1 && rawDefaultPending == 0 &&
       !rawDefaultAttemptScheduler.CanRequest(rawDefaultAttemptId, 0f),
    "The RecordAttempt boundary itself must reject a raw default struct instead of treating enum zero as Requested.");

NativePickupAttempt trueCapacityFailure = BuildFailedAttemptForSmoke(doubleFailureBaseline,
    nativeAttempted: false, nativeAttempt: default,
    storageAttempted: false, storageAttempt: default, nativePickupAvailable: false);
NativePickupAttempt unexplainedExecutionFailure = BuildFailedAttemptForSmoke(doubleFailureBaseline,
    nativeAttempted: false, nativeAttempt: default,
    storageAttempted: false, storageAttempt: default, nativePickupAvailable: true);
Assert(trueCapacityFailure.Result == AutoPickupResultKind.RejectedByCapacity &&
       !trueCapacityFailure.RequestSent &&
       unexplainedExecutionFailure.Result == AutoPickupResultKind.UnknownFailure,
    "RejectedByCapacity is valid only when native capacity is unavailable and no more specific callback failure exists.");
int failedNativeCalls = 0;
int recoveryStorageCalls = 0;
AutoPickupNativePostAction staleWithoutInventoryGain = AutoPickupExecutionCoordinator.ResolveNativePostAction(
    AutoPickupResultKind.UnknownFailure, worldDropIsCurrent: false,
    inventoryBefore: default, inventoryAfter: default, requestSent: true);
AutoPickupExecutionPath recoveredAfterNativeFailure = AutoPickupExecutionCoordinator.Execute(
    allowed: true,
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { recoveryStorageCalls++; return true; },
    tryNativePickup: () =>
    {
        failedNativeCalls++;
        return AutoPickupExecutionCoordinator.StopsNativeFallback(staleWithoutInventoryGain);
    });
Assert(recoveredAfterNativeFailure == AutoPickupExecutionPath.InfiniteStorage &&
       failedNativeCalls == 1 && recoveryStorageCalls == 1,
    "A failed native attempt must get at most one Snapshot recovery attempt rather than swallow the candidate.");
failedNativeCalls = recoveryStorageCalls = 0;
AutoPickupExecutionPath bothExecutorsFailed = AutoPickupExecutionCoordinator.Execute(
    allowed: true,
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { recoveryStorageCalls++; return false; },
    tryNativePickup: () =>
    {
        failedNativeCalls++;
        return AutoPickupExecutionCoordinator.StopsNativeFallback(staleWithoutInventoryGain);
    });
Assert(bothExecutorsFailed == AutoPickupExecutionPath.Failed &&
       failedNativeCalls == 1 && recoveryStorageCalls == 1,
    "Native and Snapshot rejection must remain a single failed attempt with no duplicate callback.");

int uncertainNativeCalls = 0;
int unsafeRecoveryStorageCalls = 0;
AutoPickupExecutionPath currentDropAfterUncertainRequest = AutoPickupExecutionCoordinator.Execute(
    allowed: true,
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { unsafeRecoveryStorageCalls++; return true; },
    tryNativePickup: () =>
    {
        uncertainNativeCalls++;
        AutoPickupNativePostAction action = AutoPickupExecutionCoordinator.ResolveNativePostAction(
            AutoPickupResultKind.UnknownFailure, worldDropIsCurrent: true,
            inventoryBefore: default, inventoryAfter: default, requestSent: true);
        Assert(action == AutoPickupNativePostAction.SuppressUncertainNative,
            "A current world drop plus a sent uncertain request must be explicitly classified as unsafe to recover.");
        return AutoPickupExecutionCoordinator.StopsNativeFallback(action);
    });
Assert(currentDropAfterUncertainRequest == AutoPickupExecutionPath.NativePickup &&
       uncertainNativeCalls == 1 && unsafeRecoveryStorageCalls == 0,
    "A still-current drop after an uncertain sent native request must suppress Snapshot fallback to prevent a late-native duplicate.");

AutoPickupInventoryEvidence currentEvidenceBefore = new AutoPickupInventoryEvidence(1, 5, 0);
AutoPickupInventoryEvidence currentEvidenceAfter = new AutoPickupInventoryEvidence(2, 10, 1);
AutoPickupNativePostAction currentWithGainAction = AutoPickupExecutionCoordinator.ResolveNativePostAction(
    AutoPickupResultKind.UnknownFailure, worldDropIsCurrent: true,
    inventoryBefore: currentEvidenceBefore, inventoryAfter: currentEvidenceAfter, requestSent: true);
int currentWithGainStorageCalls = 0;
int currentWithGainNativeCalls = 0;
AutoPickupScheduler currentWithGainScheduler = new AutoPickupScheduler();
currentWithGainScheduler.Reset(0f);
AutoPickupExecutionPath currentWithGainPath = AutoPickupExecutionCoordinator.Execute(
    allowed: currentWithGainScheduler.CanRequest(991, 0f),
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { currentWithGainStorageCalls++; return true; },
    tryNativePickup: () =>
    {
        currentWithGainNativeCalls++;
        if (currentWithGainAction == AutoPickupNativePostAction.SuppressUncertainNative)
            currentWithGainScheduler.SuppressUntilReset(991);
        return AutoPickupExecutionCoordinator.StopsNativeFallback(currentWithGainAction);
    });
Assert(currentWithGainAction == AutoPickupNativePostAction.SuppressUncertainNative &&
       currentWithGainPath == AutoPickupExecutionPath.NativePickup &&
       currentWithGainNativeCalls == 1 && currentWithGainStorageCalls == 0 &&
       !currentWithGainScheduler.CanRequest(991, float.MaxValue),
    "A current drop after a sent non-final native request must suppress retry and Storage even when inventory evidence increased.");

const uint pendingStaleNoGainId = 1201;
const uint pendingWaitId = 1200;
HashSet<uint> pendingWait = new HashSet<uint> { pendingWaitId };
AutoPickupScheduler pendingWaitScheduler = new AutoPickupScheduler();
pendingWaitScheduler.Reset(0f);
AutoPickupPendingConfirmationOutcome pendingWaitOutcome = InvokePendingConfirmation(
    worldDropIsCurrent: true, timeoutElapsed: false,
    before: new AutoPickupInventoryEvidence(1, 5, 0),
    after: new AutoPickupInventoryEvidence(1, 5, 0),
    storageResult: true, pendingWaitId, pendingWait, pendingWaitScheduler,
    out int pendingWaitStorageCalls, out int pendingWaitNativeCalls,
    out int pendingWaitSuppressCalls, out int pendingWaitCompleteCalls);
Assert(pendingWaitOutcome == AutoPickupPendingConfirmationOutcome.Waiting &&
       pendingWaitStorageCalls == 0 && pendingWaitNativeCalls == 0 &&
       pendingWaitSuppressCalls == 0 && pendingWaitCompleteCalls == 0 &&
       pendingWait.Count == 1 && pendingWaitScheduler.CanRequest(pendingWaitId, float.MaxValue),
    "A current non-expired Requested pickup must remain pending without storage, completion, or suppression.");

HashSet<uint> pendingStaleNoGain = new HashSet<uint> { pendingStaleNoGainId };
AutoPickupScheduler pendingStaleNoGainScheduler = new AutoPickupScheduler();
pendingStaleNoGainScheduler.Reset(0f);
AutoPickupPendingConfirmationOutcome pendingStaleNoGainOutcome = InvokePendingConfirmation(
    worldDropIsCurrent: false, timeoutElapsed: false,
    before: new AutoPickupInventoryEvidence(0, 0, 0),
    after: new AutoPickupInventoryEvidence(0, 0, 0),
    storageResult: true, pendingStaleNoGainId, pendingStaleNoGain, pendingStaleNoGainScheduler,
    out int pendingStaleStorageCalls, out int pendingStaleNativeCalls,
    out int pendingStaleSuppressCalls, out int pendingStaleCompleteCalls);
Assert(pendingStaleNoGainOutcome == AutoPickupPendingConfirmationOutcome.RecoveredWithInfiniteStorage &&
       pendingStaleStorageCalls == 1 && pendingStaleNativeCalls == 0 &&
       pendingStaleSuppressCalls == 0 && pendingStaleCompleteCalls == 1 &&
       pendingStaleNoGain.Count == 0,
    "A pending stale drop with no inventory evidence must attempt Snapshot recovery exactly once and clear pending state.");

const uint pendingZeroAmountId = 1202;
HashSet<uint> pendingZeroAmount = new HashSet<uint> { pendingZeroAmountId };
AutoPickupScheduler pendingZeroAmountScheduler = new AutoPickupScheduler();
pendingZeroAmountScheduler.Reset(0f);
AutoPickupPendingConfirmationOutcome pendingZeroAmountOutcome = InvokePendingConfirmation(
    worldDropIsCurrent: false, timeoutElapsed: false,
    before: new AutoPickupInventoryEvidence(0, 0, 0),
    after: new AutoPickupInventoryEvidence(1, 0, 1),
    storageResult: false, pendingZeroAmountId, pendingZeroAmount, pendingZeroAmountScheduler,
    out int pendingZeroStorageCalls, out int pendingZeroNativeCalls,
    out int pendingZeroSuppressCalls, out int pendingZeroCompleteCalls);
Assert(pendingZeroAmountOutcome == AutoPickupPendingConfirmationOutcome.NativeDelivered &&
       pendingZeroStorageCalls == 0 &&
       pendingZeroNativeCalls == 1 && pendingZeroSuppressCalls == 0 &&
       pendingZeroCompleteCalls == 1 && pendingZeroAmount.Count == 0 &&
       pendingZeroAmountScheduler.CanRequest(pendingZeroAmountId, float.MaxValue),
    "A pending amount=0 pickup with one new exact ItemJar must confirm native delivery without Storage and clear pending state.");

foreach ((string name, AutoPickupInventoryEvidence after) in new[]
         {
             ("without inventory growth", new AutoPickupInventoryEvidence(1, 5, 0)),
             ("with inventory growth", new AutoPickupInventoryEvidence(2, 10, 1))
         })
{
    const uint currentTimeoutId = 1203;
    HashSet<uint> pendingCurrentTimeout = new HashSet<uint> { currentTimeoutId };
    AutoPickupScheduler currentTimeoutScheduler = new AutoPickupScheduler();
    currentTimeoutScheduler.Reset(0f);
    AutoPickupPendingConfirmationOutcome currentTimeoutOutcome = InvokePendingConfirmation(
        worldDropIsCurrent: true, timeoutElapsed: true,
        before: new AutoPickupInventoryEvidence(1, 5, 0), after,
        storageResult: true, currentTimeoutId, pendingCurrentTimeout, currentTimeoutScheduler,
        out int timeoutStorageCalls, out int timeoutNativeCalls,
        out int timeoutSuppressCalls, out int timeoutCompleteCalls);
    Assert(currentTimeoutOutcome == AutoPickupPendingConfirmationOutcome.SuppressedUncertainNative &&
           timeoutStorageCalls == 0 &&
           timeoutNativeCalls == 0 && timeoutSuppressCalls == 1 && timeoutCompleteCalls == 1 &&
           pendingCurrentTimeout.Count == 0 &&
           !currentTimeoutScheduler.CanRequest(currentTimeoutId, float.MaxValue),
        "A pending current drop at timeout " + name +
        " must clear pending, suppress the scheduler, and never write Storage.");
}

List<string> reconciliationOrder = new();
AutoPickupStorageCommitResult reconciliation = AutoPickupStorageCommitCoordinator.Execute(
    storeSnapshot: () => { reconciliationOrder.Add("store"); return true; },
    removeWorldDrop: () => { reconciliationOrder.Add("remove-world"); return false; },
    rollbackSnapshot: () => { reconciliationOrder.Add("rollback-storage"); return false; },
    quarantineWorldDrop: () => { reconciliationOrder.Add("quarantine-world"); return true; },
    lockStorage: () => { reconciliationOrder.Add("lock-storage"); return true; });
Assert(!reconciliation.Success && reconciliation.StoredSnapshot &&
       reconciliation.RequiresReconciliation && reconciliation.StorageIsAuthoritative &&
       reconciliation.WorldDropQuarantined && reconciliation.SuppressNativeFallback &&
       reconciliation.SuppressRetry && reconciliation.StorageLocked && reconciliation.MarkerPersisted &&
       reconciliationOrder.SequenceEqual(new[]
       {
           "store", "remove-world", "rollback-storage", "lock-storage", "quarantine-world"
       }),
    "Store/remove/rollback double failure must lock storage, quarantine the world copy, and forbid fallback/retry.");
AutoPickupStorageCommitResult safeRollback = AutoPickupStorageCommitCoordinator.Execute(
    () => true, () => false, () => true, () => throw new InvalidOperationException("must-not-quarantine"),
    () => throw new InvalidOperationException("must-not-lock"));
Assert(!safeRollback.Success && safeRollback.SnapshotRollbackSucceeded &&
       !safeRollback.RequiresReconciliation && safeRollback.SuppressNativeFallback &&
       !safeRollback.SuppressRetry,
    "A successful compensation leaves one visible world copy and may retry later, but never native-fallbacks in the same attempt.");
AutoPickupStorageCommitResult markerFailureReconciliation = AutoPickupStorageCommitCoordinator.Execute(
    () => true, () => false, () => false, () => true, () => false);
Assert(markerFailureReconciliation.RequiresReconciliation &&
       markerFailureReconciliation.StorageLocked && !markerFailureReconciliation.MarkerPersisted &&
       markerFailureReconciliation.SuppressNativeFallback && markerFailureReconciliation.SuppressRetry,
    "AutoPickup must expose marker persistence failure while keeping the current process locked and suppressing fallback/retry.");
scheduler.SuppressUntilReset(777);
Assert(!scheduler.CanRequest(777, float.MaxValue),
    "A reconciliation-required world instance must never be retried during the active scheduler lifecycle.");
scheduler.Reset(20f);
Assert(scheduler.CanRequest(777, 20f),
    "A lifecycle reset may clear only the per-instance scheduler suppression; the persistent reconciliation gate remains authoritative.");
AutoPickupScheduler restartedScheduler = new();
restartedScheduler.Reset(0f);
int restartedStorageCalls = 0;
int restartedNativeCalls = 0;
AutoPickupExecutionPath reconciliationBlockedPath = AutoPickupExecutionCoordinator.Execute(
    allowed: restartedScheduler.CanRequest(777, 0f),
    reconciliationRequired: true,
    preferInfiniteStorage: false,
    nativePickupAvailable: true,
    tryInfiniteStorage: () => { restartedStorageCalls++; return true; },
    tryNativePickup: () => { restartedNativeCalls++; return true; });
Assert(reconciliationBlockedPath == AutoPickupExecutionPath.ReconciliationBlocked &&
       restartedStorageCalls == 0 && restartedNativeCalls == 0,
    "A persistent reconciliation latch must globally block both storage and native pickup after scheduler/feature recreation.");
bool latchCreatedDuringStore = false;
int sameAttemptStorageCalls = 0;
int sameAttemptNativeCalls = 0;
AutoPickupExecutionPath sameAttemptBlocked = AutoPickupExecutionCoordinator.Execute(
    allowed: true,
    isReconciliationRequired: () => latchCreatedDuringStore,
    preferInfiniteStorage: true,
    nativePickupAvailable: true,
    tryInfiniteStorage: () =>
    {
        sameAttemptStorageCalls++;
        latchCreatedDuringStore = true;
        return false;
    },
    tryNativePickup: () => { sameAttemptNativeCalls++; return true; });
Assert(sameAttemptBlocked == AutoPickupExecutionPath.ReconciliationBlocked &&
       sameAttemptStorageCalls == 1 && sameAttemptNativeCalls == 0,
    "AutoPickup must recheck reconciliation after a failed storage attempt before native fallback in the same call.");

string tempDirectory = Path.Combine(Path.GetTempPath(), "AutoPickupSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDirectory);
try
{
    SnapshotStorageIdentity liveLikeIdentity = new SnapshotStorageIdentity(
        "snapshot-storage-identity-v1|root=21:Worlds/Singleplayer_0|level=3:PEI",
        "snapshot-storage-identity-v1|steam=76561198000000001|character=0");
    const string liveConfigRoot = @"I:\Steam\steamapps\common\Unturned\BepInEx\config";
    string liveLikeSnapshotPath = SnapshotStoragePath.Resolve(liveConfigRoot, liveLikeIdentity);
    string liveLikeLongestSidecar = liveLikeSnapshotPath +
        ".candidate-history-previous-recovery-" + new string('0', 32) + ".bak";
    string safeSnapshotPath = SnapshotStoragePath.Resolve(tempDirectory, liveLikeIdentity);
    SnapshotDocument firstAutoPickupDocument = SnapshotDocument.Empty(liveLikeIdentity);
    firstAutoPickupDocument.Groups.Add(new SnapshotDocumentGroup
    {
        AssetId = 1033,
        AssetGuid = "10330000-0000-0000-0000-000000000001",
        Buckets = new List<SnapshotDocumentBucket>
        {
            new SnapshotDocumentBucket
            {
                Amount = 1,
                Quality = 100,
                State = Array.Empty<byte>(),
                Count = 1
            }
        }
    });
    SnapshotPersistence firstAutoPickupPersistence = new SnapshotPersistence(safeSnapshotPath);
    bool worldDropPresent = true;
    int firstStorePersistenceCalls = 0;
    int firstStoreWorldRemoveCalls = 0;
    int firstStoreNativeCalls = 0;
    AutoPickupExecutionPath firstSnapshotStorePath = AutoPickupExecutionCoordinator.Execute(
        allowed: true,
        preferInfiniteStorage: true,
        nativePickupAvailable: true,
        tryInfiniteStorage: () =>
        {
            AutoPickupStorageCommitResult commit = AutoPickupStorageCommitCoordinator.Execute(
                storeSnapshot: () =>
                {
                    if (liveLikeLongestSidecar.Length >= 260) return false;
                    firstStorePersistenceCalls++;
                    return firstAutoPickupPersistence.Save(firstAutoPickupDocument).Success;
                },
                removeWorldDrop: () =>
                {
                    firstStoreWorldRemoveCalls++;
                    worldDropPresent = false;
                    return true;
                },
                rollbackSnapshot: () => throw new InvalidOperationException("must-not-rollback"),
                quarantineWorldDrop: () => throw new InvalidOperationException("must-not-quarantine"),
                lockStorage: () => throw new InvalidOperationException("must-not-lock"));
            return commit.Success;
        },
        tryNativePickup: () =>
        {
            firstStoreNativeCalls++;
            return true;
        });
    Assert(firstSnapshotStorePath == AutoPickupExecutionPath.InfiniteStorage &&
           liveLikeLongestSidecar.Length < 260 && firstStorePersistenceCalls == 1 &&
           firstStoreWorldRemoveCalls == 1 && firstStoreNativeCalls == 0 &&
           !worldDropPresent && File.Exists(safeSnapshotPath),
        "PreferInfiniteStorage with empty carried capacity must complete first Snapshot persistence and world removal without native fallback. LongestPath=" +
        liveLikeLongestSidecar.Length);

    string rulesPath = Path.Combine(tempDirectory, "rules.json");
    AutoPickupRuleStore store = new(rulesPath);
    AutoPickupRuleLoadResult save = store.ReplaceAndSave(true, AutoPickupFilterMode.WhitelistThenBlacklist, whitelistRules, matcher);
    Assert(save.Success && save.Accepted == 2 && File.Exists(rulesPath) && !File.Exists(rulesPath + ".tmp"),
        $"Atomic save should publish both whitelist and blacklist cards. Error={save.Error}");
    AutoPickupRuleStore reloaded = new(rulesPath);
    AutoPickupRuleLoadResult load = reloaded.Load(new ItemFilterMatcher());
    Assert(load.Success && load.Accepted == 2 && reloaded.Document.Enabled && reloaded.Document.Mode == AutoPickupFilterMode.WhitelistThenBlacklist,
        "Enabled whitelist state and both card types must round-trip.");

    File.WriteAllText(rulesPath, "{\"Version\":2,\"Enabled\":true,\"Mode\":3,\"Rules\":[]}");
    ItemFilterMatcher emptyWhitelistMatcher = new();
    AutoPickupRuleStore emptyWhitelistStore = new(rulesPath);
    AutoPickupRuleLoadResult emptyWhitelistResult = emptyWhitelistStore.Load(emptyWhitelistMatcher);
    Assert(emptyWhitelistResult.Success && emptyWhitelistStore.Document.Enabled &&
           emptyWhitelistStore.Document.Mode == AutoPickupFilterMode.WhitelistThenBlacklist &&
           emptyWhitelistMatcher.Mode == AutoPickupFilterMode.BlacklistOnly && emptyWhitelistMatcher.Matches(rifle),
        "An enabled but empty whitelist must preserve the requested toggle while its effective matcher fails open.");

    List<ItemFilterRule> oneAllowedItem =
    [
        new() { Action = ItemFilterRuleAction.Allow, Field = ItemFilterRuleField.AssetId, Value = "363" }
    ];
    AutoPickupRuleLoadResult activated = emptyWhitelistStore.ReplaceAndSave(
        emptyWhitelistStore.Document.Enabled, emptyWhitelistStore.Document.Mode, oneAllowedItem, emptyWhitelistMatcher);
    Assert(activated.Success && emptyWhitelistStore.Document.Enabled &&
           emptyWhitelistMatcher.Mode == AutoPickupFilterMode.WhitelistThenBlacklist &&
           emptyWhitelistMatcher.Matches(rifle) && !emptyWhitelistMatcher.Matches(civilianItem),
        "Whitelist matching must activate automatically only when the preserved toggle and an allow item are both present.");

    AutoPickupRuleLoadResult disabledWithItems = emptyWhitelistStore.ReplaceAndSave(
        false, AutoPickupFilterMode.WhitelistThenBlacklist, oneAllowedItem, emptyWhitelistMatcher);
    Assert(disabledWithItems.Success && !emptyWhitelistStore.Document.Enabled &&
           emptyWhitelistStore.Document.Mode == AutoPickupFilterMode.BlacklistOnly &&
           emptyWhitelistMatcher.Mode == AutoPickupFilterMode.BlacklistOnly &&
           emptyWhitelistMatcher.Matches(rifle) && emptyWhitelistMatcher.Matches(civilianItem),
        "Whitelist items alone must not activate matching while the whitelist toggle is off.");

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
string rawItemIndexPath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "Minimap", "NearbyItemIndex.cs");
string pluginSourcePath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "CheatMenuPlugin.cs");
string runtimeContextPath = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "PluginRuntimeContext.cs");
string minimapSourceDirectory = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "Minimap");
string uiSourceDirectory = Path.Combine(repositoryRoot, "src", "UnturnedSingleplayerCheatMenu", "UI");
Assert(File.Exists(runtimeSourcePath) && File.Exists(rawItemIndexPath) && File.Exists(pluginSourcePath) &&
       File.Exists(runtimeContextPath) && Directory.Exists(minimapSourceDirectory) && Directory.Exists(uiSourceDirectory),
    "Run AutoPickupSmoke from the repository root so runtime and raw-index contracts can be inspected.");
string runtimeSource = File.ReadAllText(runtimeSourcePath);
string rawItemIndexSource = File.ReadAllText(rawItemIndexPath);
string pluginSource = File.ReadAllText(pluginSourcePath);
string runtimeContextSource = File.ReadAllText(runtimeContextPath);
string minimapSource = string.Join("\n", Directory.EnumerateFiles(minimapSourceDirectory, "*.cs").OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText));
string uiSource = string.Join("\n", Directory.EnumerateFiles(uiSourceDirectory, "*.cs").OrderBy(path => path, StringComparer.Ordinal).Select(File.ReadAllText));
Assert(uiSource.Contains("for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE", StringComparison.Ordinal),
    "The whitelist/blacklist picker must enumerate the complete carried inventory, including the Hands page.");
Assert(runtimeSource.Contains("_canExecuteGameplay()", StringComparison.Ordinal) &&
      runtimeSource.Contains("ResetRuntime(\"玩法授权未就绪\")", StringComparison.Ordinal),
    "Every automatic-pickup path must stop and clear pending work while gameplay authorization is unavailable.");
Assert(pluginSource.Contains("PersistInterfaceState(_autoPickupEnabled, value);", StringComparison.Ordinal) &&
      !pluginSource.Contains("PersistInterfaceState(_autoPickupEnabled, value &&", StringComparison.Ordinal),
    "Temporary gameplay denial must not overwrite the user's requested AutoPickup setting.");
Assert(!rawItemIndexSource.Contains("AutoPickupDropGate", StringComparison.Ordinal) &&
      !rawItemIndexSource.Contains("AllowsQuality", StringComparison.Ordinal) &&
      !rawItemIndexSource.Contains("ItemFilterMatcher", StringComparison.Ordinal) &&
      !rawItemIndexSource.Contains("takeItem", StringComparison.Ordinal),
    "The shared ground-item index must enumerate raw client-known drops without pickup-specific gates or actions.");
Assert(runtimeSource.Contains("AutoPickupCandidateAdapter", StringComparison.Ordinal) &&
      !runtimeSource.Contains("internal sealed class DropScanner", StringComparison.Ordinal),
    "AutoPickup must consume the raw index through its own adapter instead of owning Region enumeration.");
Assert(runtimeSource.Contains("ReferenceEquals(drop, candidate.Drop)", StringComparison.Ordinal),
    "A reused region/instance ID must not make a different world's ItemDrop current for an older AutoPickup candidate.");
Assert(!runtimeSource.Contains("DropScanner.", StringComparison.Ordinal),
    "AutoPickupRuntime still references the removed DropScanner type.");
Assert(runtimeSource.Contains("ResolveEffectiveRange()", StringComparison.Ordinal) &&
      runtimeSource.Contains("if (!_settings.UseMinimapRange)", StringComparison.Ordinal) &&
      runtimeSource.Contains("if (_settings.UseMinimapRarityFilter && !AllowsMinimapRarity(candidate.Asset))", StringComparison.Ordinal),
    "Auto-pickup does not apply its two explicit minimap link gates only on the pickup-consumer path.");
Assert(pluginSource.Contains("Config.Bind(\"AutoPickup\", \"UseMinimapRange\", defaultValue: false", StringComparison.Ordinal) &&
      pluginSource.Contains("Config.Bind(\"AutoPickup\", \"UseMinimapRarityFilter\", defaultValue: false", StringComparison.Ordinal),
    "Minimap linkage is not persisted as two disabled-by-default AutoPickup options.");
Assert(runtimeContextSource.Contains("Mathf.Max(radius, autoRadius)", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("Mathf.Max(frequency, autoFrequency)", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("_nearbyItemIndex.Refresh", StringComparison.Ordinal),
    "Auto-pickup and minimap do not share one max-radius/max-frequency raw item scan.");
int infiniteStoragePumpIndex = runtimeContextSource.IndexOf("_infiniteStorage?.Pump();", StringComparison.Ordinal);
int autoPickupPumpIndex = runtimeContextSource.IndexOf("_autoPickup?.Pump(", StringComparison.Ordinal);
Assert(infiniteStoragePumpIndex >= 0 && autoPickupPumpIndex > infiniteStoragePumpIndex,
    "Infinite Storage must become operational before AutoPickup evaluates PreferInfiniteStorage in the same frame.");
Assert(!minimapSource.Contains("AutoPickupDropGate", StringComparison.Ordinal) &&
      !minimapSource.Contains("AllowsQuality", StringComparison.Ordinal) &&
      !minimapSource.Contains("ItemFilterMatcher", StringComparison.Ordinal) &&
      !minimapSource.Contains("ItemManager.takeItem", StringComparison.Ordinal),
    "Minimap item visibility is still coupled to pickup-only gates or actions.");
Assert(runtimeSource.Contains("ItemManager.takeItem", StringComparison.Ordinal), "Pickup must use the game's native ItemManager.takeItem path.");
Assert(!runtimeSource.Contains("Physics.Raycast", StringComparison.Ordinal) && !runtimeSource.Contains("Physics.Linecast", StringComparison.Ordinal), "Drop scanning must not add a visibility ray gate.");
Assert(!runtimeSource.Contains("player.transform.position =", StringComparison.Ordinal), "Auto pickup must never move the player to reach a drop.");
Assert(runtimeSource.Contains("HasInventorySpace", StringComparison.Ordinal) && runtimeSource.Contains("tryFindSpace", StringComparison.Ordinal),
    "Auto pickup must check carried-inventory space before sending a native pickup request.");
Assert(runtimeSource.IndexOf("HasInventorySpace(player.inventory", StringComparison.Ordinal) < runtimeSource.IndexOf("ItemManager.takeItem", StringComparison.Ordinal),
    "Inventory capacity must be checked before the native pickup request is sent.");
Assert(runtimeSource.Contains("PreferInfiniteStorage", StringComparison.Ordinal) &&
       runtimeSource.Contains("TryStoreCandidate", StringComparison.Ordinal) &&
       runtimeSource.Contains("AutoPickupExecutionCoordinator.Execute", StringComparison.Ordinal),
    "Auto pickup must support an explicit infinite-storage priority path.");
Assert(runtimeSource.Contains("AutoPickupStorageCommitCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("Func<AutoPickupCandidate, bool> _rollbackStoredCandidate", StringComparison.Ordinal) &&
       runtimeSource.Contains("Func<AutoPickupCandidate, bool> _lockStorage", StringComparison.Ordinal) &&
	   runtimeSource.Contains("AutoPickupResultKind.ReconciliationRequired", StringComparison.Ordinal) &&
	   runtimeSource.Contains("result.MarkerPersisted", StringComparison.Ordinal) &&
	   runtimeSource.Contains("AutoPickupAttemptRecordCoordinator.Execute", StringComparison.Ordinal) &&
       runtimeSource.Contains("QuarantineWorldDrop", StringComparison.Ordinal) &&
       runtimeSource.Contains("LockInfiniteStorage", StringComparison.Ordinal) &&
       runtimeSource.Contains("LatchReconciliationFailure", StringComparison.Ordinal) &&
       !runtimeSource.Contains("Action<AutoPickupCandidate> _rollbackStoredCandidate", StringComparison.Ordinal) &&
       !runtimeSource.Contains("Action<AutoPickupCandidate> _lockStorage", StringComparison.Ordinal),
    "NativePickupExecutor does not observe rollback failure or enforce storage-authoritative world quarantine.");
Assert(runtimeSource.Contains("SnapshotBackgroundStoreCompletionRouter.Route(", StringComparison.Ordinal) &&
       runtimeSource.Contains("BuildDetachedBackgroundStoreAttempt", StringComparison.Ordinal) &&
       runtimeSource.Contains("未触碰当前世界的掉落、背包或储存内存", StringComparison.Ordinal),
    "A detached background store must be recorded without finalizing, resuming, quarantining, or natively picking up a current-world drop.");
Assert(runtimeSource.Contains("plugin?.IsInfiniteStoragePickupPriorityEnabled == true", StringComparison.Ordinal) && runtimeSource.Contains("plugin.InfiniteStorage?.IsOperational == true", StringComparison.Ordinal),
    "Pickup priority must require both its setting and an operational infinite-storage runtime.");
Assert(runtimeSource.Contains("!_settings.AllowsQuality(candidate.Quality)", StringComparison.Ordinal),
    "Enabled quality filtering must reject candidates below the persisted minimum before pickup execution.");
Assert(runtimeSource.Contains("!_settings.AllowsRarity(candidate.Rarity)", StringComparison.Ordinal),
    "Disabled item rarities must be rejected before native pickup execution.");
Assert(pluginSource.Contains("Config.Bind(\"AutoPickup\", \"QualityFilterEnabled\"", StringComparison.Ordinal)
    && pluginSource.Contains("Config.Bind(\"AutoPickup\", \"MinimumQuality\"", StringComparison.Ordinal)
    && pluginSource.Contains("PersistInterfaceState(_autoPickupQualityFilterEnabled", StringComparison.Ordinal)
    && pluginSource.Contains("PersistInterfaceState(_autoPickupMinimumQuality", StringComparison.Ordinal),
    "The quality-filter toggle and minimum threshold must both persist through BepInEx configuration.");
Assert(pluginSource.Contains("Config.Bind(\"AutoPickup\", \"AllowedRarityMask\", AutoPickupSettings.AllRaritiesMask", StringComparison.Ordinal)
    && pluginSource.Contains("PersistInterfaceState(_autoPickupAllowedRarityMask", StringComparison.Ordinal),
    "The auto-pickup rarity mask must persist with every rarity enabled by default.");
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
Assert(uiSource.Contains("ShowAutoPickupRarityPicker", StringComparison.Ordinal)
    && uiSource.Contains("SetAllowedRarityMask", StringComparison.Ordinal)
    && uiSource.Contains("普通", StringComparison.Ordinal)
    && uiSource.Contains("罕见", StringComparison.Ordinal)
    && uiSource.Contains("稀有", StringComparison.Ordinal)
    && uiSource.Contains("史诗", StringComparison.Ordinal)
    && uiSource.Contains("传奇", StringComparison.Ordinal)
    && uiSource.Contains("神话", StringComparison.Ordinal),
    "The auto-pickup settings page must open a six-rarity toggle popup.");
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
