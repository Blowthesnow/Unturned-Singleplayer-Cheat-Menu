using UnturnedSingleplayerCheatMenu.InfiniteStorage;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static ItemStorageDescriptor Descriptor(ushort id, string guid, StackUnitKind kind, byte amount, byte quality, byte[] state,
    byte maxStack, AmountRole role = AmountRole.NotApplicable, DisplayPolicy display = DisplayPolicy.ConservativeInstance)
{
    return new ItemStorageDescriptor
    {
        AssetId = id,
        AssetGuid = guid,
        UnitKind = kind,
        AmountRole = role,
        QualityPolicy = kind == StackUnitKind.ResourceUnits ? QualityPolicy.Ignore : QualityPolicy.Exact,
        StatePolicy = kind == StackUnitKind.ResourceUnits ? StatePolicy.Ignore : StatePolicy.ExactBytes,
        WithdrawPolicy = kind == StackUnitKind.ResourceUnits ? WithdrawPolicy.SplitResourceUnits : WithdrawPolicy.WholeInstancesOnly,
        DisplayPolicy = display,
        NativeAmount = amount,
        NativeQuality = quality,
        NativeState = state ?? Array.Empty<byte>(),
        NativeMaxStack = maxStack
    };
}

const string resourceGuid = "00000000-0000-0000-0000-000000000001";
const string ammoGuid = "00000000-0000-0000-0000-000000000002";
const string gunGuid = "00000000-0000-0000-0000-000000000003";

VirtualStorageService storage = new();
ItemStorageDescriptor resourceA = Descriptor(1, resourceGuid, StackUnitKind.ResourceUnits, 240, 100, new byte[] { 9 }, 255,
    AmountRole.StackCount, DisplayPolicy.ResourceTotal);
ItemStorageDescriptor resourceB = Descriptor(1, resourceGuid, StackUnitKind.ResourceUnits, 60, 0, Array.Empty<byte>(), 255,
    AmountRole.StackCount, DisplayPolicy.ResourceTotal);
Assert(storage.Add(resourceA).Success && storage.Add(resourceB).Success, "Stateless resources should store.");
Assert(storage.Entries.Count == 1 && storage.Entries[0].StoredUnitCount == 300, "Resource quality and state must not split a confirmed stateless resource.");
IReadOnlyList<NativeItemPayload> resourceWithdraw = ItemInstanceSemantics.CreateWithdrawPayloads(storage.Entries[0], 300);
Assert(resourceWithdraw.Count == 2 && resourceWithdraw[0].Amount == 255 && resourceWithdraw[1].Amount == 45,
    "Resource units must withdraw using native stack limits.");
Assert(InfiniteStorageCardMetadata.Build(storage.Entries[0]).Text == "库存 300", "Resource cards must not invent a quality or state summary.");

ItemStorageDescriptor ammo40 = Descriptor(2, ammoGuid, StackUnitKind.ContainerInstances, 40, 100, new byte[] { 40, 40 }, 40,
    AmountRole.AmmoRounds, DisplayPolicy.AmmoContainer);
ItemStorageDescriptor ammo80 = Descriptor(2, ammoGuid, StackUnitKind.ContainerInstances, 80, 100, new byte[] { 80, 80 }, 80,
    AmountRole.AmmoRounds, DisplayPolicy.AmmoContainer);
Assert(storage.Add(ammo40).Success && storage.Add(ammo40).Success && storage.Add(ammo80).Success, "Ammo containers should store as instances.");
IReadOnlyList<VirtualStorageEntry> ammoEntries = storage.GetEntries(2);
Assert(ammoEntries.Count == 2 && ammoEntries.Single(entry => entry.NativeAmount == 40).StoredUnitCount == 2,
    "40/40 + 40/40 must be two complete instances while 80/80 stays separate.");
VirtualStorageEntry forty = ammoEntries.Single(entry => entry.NativeAmount == 40);
IReadOnlyList<NativeItemPayload> oneAmmo = ItemInstanceSemantics.CreateWithdrawPayloads(forty, 1);
Assert(oneAmmo.Count == 1 && oneAmmo[0].Amount == 40 && oneAmmo[0].State.SequenceEqual(new byte[] { 40, 40 }),
    "Withdrawing one ammo container must restore 40/40 rather than a split 1/40 item.");
Assert(InfiniteStorageCardMetadata.Build(forty).Text == "库存 2 个 | 弹药 40/40", "Ammo card must distinguish instance count from ammo rounds.");
Assert(InfiniteStorageCardMetadata.Build(forty, assetResolved: true, showQuality: true).Text == "库存 2 个 | 弹药 40/40 | 品质 100%",
    "Ammo containers must show quality only when the resolved asset declares that quality is visible.");

ItemStorageDescriptor gun80 = Descriptor(3, gunGuid, StackUnitKind.SingletonInstances, 1, 80, new byte[] { 1, 2, 3 }, 1);
ItemStorageDescriptor gun70 = Descriptor(3, gunGuid, StackUnitKind.SingletonInstances, 1, 70, new byte[] { 1, 2, 3 }, 1);
ItemStorageDescriptor gunStateChanged = Descriptor(3, gunGuid, StackUnitKind.SingletonInstances, 1, 80, new byte[] { 1, 2, 4 }, 1);
Assert(storage.Add(gun80).Success && storage.Add(gun70).Success && storage.Add(gunStateChanged).Success,
    "Singleton items should store.");
Assert(storage.GetEntries(3).Count == 3, "Gun quality and any state-byte difference must split complete instances.");
VirtualStorageEntry qualityInstance = storage.GetEntries(3).Single(entry => entry.NativeQuality == 70);
Assert(InfiniteStorageCardMetadata.Build(qualityInstance, assetResolved: true, showQuality: true).Text == "库存 1 个 | 品质 70% | 状态独立保存",
    "Resolved food, medical, equipment, and other quality-bearing instances must show their exact native quality.");
Assert(InfiniteStorageCardMetadata.Build(qualityInstance, assetResolved: false, showQuality: false).Text == "库存 1 个 | 保守状态隔离",
    "Unresolved assets must not invent a quality display and must remain visibly isolated.");

ItemStorageDescriptor fuelA = Descriptor(4, "00000000-0000-0000-0000-000000000004", StackUnitKind.ContainerInstances, 20, 100, new byte[] { 20 }, 100,
    AmountRole.FuelUnits, DisplayPolicy.FuelOrWaterContainer);
ItemStorageDescriptor fuelB = Descriptor(4, "00000000-0000-0000-0000-000000000004", StackUnitKind.ContainerInstances, 40, 100, new byte[] { 40 }, 100,
    AmountRole.FuelUnits, DisplayPolicy.FuelOrWaterContainer);
ItemStorageDescriptor storageContainer = Descriptor(5, "00000000-0000-0000-0000-000000000005", StackUnitKind.ContainerInstances, 1, 100, new byte[] { 17, 21, 99 }, 1,
    AmountRole.ContainerContents, DisplayPolicy.ConservativeInstance);
Assert(storage.Add(fuelA).Success && storage.Add(fuelB).Success && storage.Add(storageContainer).Success, "Fuel and stateful containers should store.");
Assert(storage.GetEntries(4).Count == 2 && storage.GetEntries(5).Single().NativeState.SequenceEqual(new byte[] { 17, 21, 99 }),
    "Fuel differences and complete storage-container state must remain isolated and intact.");

IReadOnlyList<InfiniteStorageDecomposeTarget> singleTarget = InfiniteStorageDecomposeRangePolicy.Resolve(
    storage.Entries, forty.EntryId, InfiniteStorageDecomposeScope.SingleItem);
Assert(singleTarget.Count == 1 && singleTarget[0].EntryId == forty.EntryId && singleTarget[0].Units == 1,
    "Single-item decomposition must consume exactly one unit from the pointed card.");
IReadOnlyList<InfiniteStorageDecomposeTarget> stackTargets = InfiniteStorageDecomposeRangePolicy.Resolve(
    storage.Entries, forty.EntryId, InfiniteStorageDecomposeScope.Stack);
Assert(stackTargets.Count == 1 && stackTargets[0].EntryId == forty.EntryId && stackTargets[0].Units == 2,
    "Stack decomposition must consume the full StoredUnitCount of the pointed card.");
IReadOnlyList<InfiniteStorageDecomposeTarget> idTargets = InfiniteStorageDecomposeRangePolicy.Resolve(
    storage.Entries, forty.EntryId, InfiniteStorageDecomposeScope.AssetId);
Assert(idTargets.Count == 2 && idTargets.Sum(target => target.Units) == 3,
    "ID decomposition must consume every quality/state card sharing the pointed AssetId.");
Assert(InfiniteStorageDecomposeRangePolicy.Resolve(storage.Entries, "missing", InfiniteStorageDecomposeScope.AssetId).Count == 0,
    "A stale card entry must not select any decomposition target.");
Assert(!InfiniteStorageDecomposeEligibility.IsStorageCandidate(storage.GetEntries(1).Single()) &&
       InfiniteStorageDecomposeEligibility.IsStorageCandidate(forty),
    "Material/resource entries must never become decomposition candidates, while complete item cards may qualify.");
VirtualStorageEntry strawHat = new()
{
    AssetId = 244,
    NativeAmount = 1,
    NativeQuality = 11,
    NativeState = new byte[] { 4, 2, 1 }
};
NativeItemPayload transferredWheat = InfiniteStorageDecomposer.CreateTransferredPayload(344, strawHat);
Assert(transferredWheat.AssetId == 344 && transferredWheat.Amount == 1 && transferredWheat.Quality == 11 &&
       transferredWheat.State.SequenceEqual(strawHat.NativeState),
    "StateTransfer salvage must allow Farmer Hat-style FOOD outputs and preserve source amount, quality, and state.");

VirtualStorageTransaction failedTransaction = new(storage, "world", "player");
Assert(failedTransaction.Add(Descriptor(6, "00000000-0000-0000-0000-000000000006", StackUnitKind.ResourceUnits, 4, 0, Array.Empty<byte>(), 10,
    AmountRole.StackCount, DisplayPolicy.ResourceTotal)), "Transaction add should stage in memory.");
Assert(!failedTransaction.Commit(() => false) && storage.GetEntries(6).Count == 0, "Failed persistence must restore storage snapshot.");
VirtualStorageTransaction committedTransaction = new(storage, "world", "player");
Assert(committedTransaction.Add(Descriptor(6, "00000000-0000-0000-0000-000000000006", StackUnitKind.ResourceUnits, 4, 0, Array.Empty<byte>(), 10,
    AmountRole.StackCount, DisplayPolicy.ResourceTotal)) && committedTransaction.Commit(() => true), "Successful transaction should commit.");
Assert(storage.GetAvailableUnits(6) == 4, "Committed transaction should retain resource units.");

VirtualStorageTransaction repeatedTransaction = new(storage, "world", "player");
Assert(repeatedTransaction.Add(Descriptor(9, "00000000-0000-0000-0000-000000000009", StackUnitKind.ResourceUnits, 4, 0,
        Array.Empty<byte>(), 10, AmountRole.StackCount, DisplayPolicy.ResourceTotal), occurrences: 3) &&
    repeatedTransaction.Commit(() => true),
    "Repeated transaction add should batch identical crafting outputs.");
Assert(storage.GetAvailableUnits(9) == 12,
    "Three crafted resource stacks containing four units each must store twelve resource units.");

VirtualStorageDocument legacy = new()
{
    SchemaVersion = 1,
    Entries = new List<VirtualStorageEntry>
    {
        new() { AssetId = 7, LegacyCount = 300, LegacyMaxNativeAmount = 255, LegacyQuality = 100, LegacyState = Array.Empty<byte>() },
        new() { AssetId = 8, LegacyCount = 2, LegacyMaxNativeAmount = 40, LegacyQuality = 100, LegacyState = new byte[] { 40, 40 } }
    }
};
VirtualStorageService migrated = new();
migrated.LoadForMigration(legacy, id => id == 7
    ? Descriptor(7, "00000000-0000-0000-0000-000000000007", StackUnitKind.ResourceUnits, 1, 0, Array.Empty<byte>(), 255, AmountRole.StackCount, DisplayPolicy.ResourceTotal)
    : Descriptor(8, "00000000-0000-0000-0000-000000000008", StackUnitKind.ContainerInstances, 40, 100, new byte[] { 40, 40 }, 40, AmountRole.AmmoRounds, DisplayPolicy.AmmoContainer));
Assert(migrated.GetAvailableUnits(7) == 300, "Confirmed legacy resources must migrate as resource units.");
Assert(migrated.GetEntries(8).Count == 0 && migrated.LegacyAmbiguousEntries.Count == 1,
    "Legacy container Count > 1 must be isolated instead of guessed as either instances or ammo rounds.");

string directory = Path.Combine(Path.GetTempPath(), "InfiniteStorageSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    string path = Path.Combine(directory, "storage.json");
    VirtualStoragePersistence persistence = new(path);
    Assert(persistence.TrySave(storage.Snapshot("world", "player"), out string saveError), "Persistence save should succeed: " + saveError);
    Assert(persistence.TryLoad(out VirtualStorageDocument loaded, out _), "Schema 2 persistence should roundtrip.");
    Assert(loaded.SchemaVersion == VirtualStorageDocument.CurrentSchemaVersion && loaded.Entries.All(entry => entry.StoredUnitCount > 0),
        "Persisted entries must use the explicit schema-2 unit contract.");
    string json = File.ReadAllText(path);
    Assert(!json.Contains("\"Count\"", StringComparison.Ordinal) && !json.Contains("\"MaxNativeAmount\"", StringComparison.Ordinal),
        "Schema 2 persistence must not write the old Count or MaxNativeAmount contract fields.");
    Assert(persistence.TryExportLegacyAmbiguousEntries(migrated.LegacyAmbiguousEntries, out string legacyExportPath, out string exportError),
        "Legacy ambiguous-entry export should succeed: " + exportError);
    Assert(File.Exists(legacyExportPath) && File.ReadAllText(legacyExportPath).Contains("legacy Count is ambiguous", StringComparison.Ordinal),
        "Legacy export must retain the isolation reason and original-entry payload.");
}
finally
{
    Directory.Delete(directory, true);
}

Console.WriteLine("InfiniteStorageSmoke passed");
