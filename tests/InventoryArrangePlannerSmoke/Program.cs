using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.ItemStacking;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static InventoryArrangeSettings Settings(InventoryArrangeAction action, bool autoRefill = false)
{
    return new InventoryArrangeSettings
    {
        Action = action,
        Preset = InventorySortPreset.Name,
        AutoRotate = false,
        AllowCrossPage = false,
        AutoRefillMagazines = autoRefill
    };
}

static InventoryArrangeItem Item(string key, int x, bool hotkeyBound = false, bool protectedItem = false,
    ItemStackingKind stackingKind = ItemStackingKind.IndividualInstance, int amount = 1, int maxAmount = 2,
    string assetGuid = "11111111-1111-1111-1111-111111111111", string state = "state", int quality = 100,
    IEnumerable<string> directSources = null, bool isMagazine = true, bool isAmmoSource = false)
{
    return new InventoryArrangeItem
    {
        ItemKey = key,
        AssetGuid = assetGuid,
        StateHash = state,
        Name = key,
        Width = 1,
        Height = 1,
        Amount = amount,
        OriginalAmount = amount,
        MaxAmount = maxAmount,
        StackingKind = stackingKind,
        Quality = quality,
        AmmoCount = stackingKind == ItemStackingKind.AmmoContainer ? amount : -1,
        AmmoCapacity = stackingKind == ItemStackingKind.AmmoContainer ? maxAmount : -1,
        AmmoDirectSourceAssetGuids = (directSources ?? Array.Empty<string>()).ToArray(),
        IsMagazine = stackingKind == ItemStackingKind.AmmoContainer && isMagazine,
        IsAmmoSource = isAmmoSource,
        IsAmmoTarget = directSources?.Any() == true,
        SourcePage = 3,
        SourceX = x,
        SourceY = 0,
        OriginalIndex = x,
        IsHotkeyBound = hotkeyBound,
        IsProtected = protectedItem
    };
}

static InventoryArrangePlan Plan(InventoryArrangePlanner planner, IReadOnlyList<InventoryArrangeItem> items,
    InventoryArrangeSettings settings)
{
    return planner.CreatePlan(
    [
        new InventoryArrangeContainer { Page = 3, Width = Math.Max(1, items.Count), Height = 1, Items = items.ToList() }
    ], settings);
}

static string Fingerprint(IEnumerable<InventoryArrangeItem> items) => InventoryStackingSemantics.BuildConservationFingerprint(items);
static int BatchAmount(AmmoRefillBatch value, int capacity) =>
    value.FullCount > 0 ? capacity : value.PartialCount > 0 ? value.PartialAmount : 0;

static List<AmmoRefillLot> MaterializeSingleInstancePlan(IReadOnlyList<AmmoRefillLot> input, AmmoRefillPlan plan)
{
    Dictionary<string, AmmoRefillBatch> allocations = plan.Allocations.ToDictionary(value => value.LotKey, value => value.Allocation);
    Dictionary<string, long> remainders = plan.SourceRemainders.ToDictionary(value => value.LotKey, value => value.RemainingRounds);
    List<AmmoRefillLot> result = new();
    foreach (AmmoRefillLot lot in input)
    {
        if (lot.IsTargetContainer)
        {
            if (lot.InstanceCount != 1 || !allocations.TryGetValue(lot.LotKey, out AmmoRefillBatch allocation))
                throw new InvalidOperationException("Property fixture requires one materialized instance per target lot.");
            result.Add(new AmmoRefillLot(lot.LotKey, lot.ContainerIdentity, lot.Rules, 1,
                BatchAmount(allocation, lot.Capacity), lot.Capacity));
        }
        else if (remainders.TryGetValue(lot.LotKey, out long remaining) && remaining > 0)
        {
            result.Add(AmmoRefillLot.SourceOnly(lot.LotKey, lot.ContainerIdentity, lot.Rules, remaining));
        }
    }
    return result;
}

static string LotFingerprint(IEnumerable<AmmoRefillLot> lots) => string.Join(";", lots
    .OrderBy(value => value.LotKey, StringComparer.Ordinal)
    .Select(value => value.LotKey + "|" + value.Rules.AssetGuid + "|" +
        (value.IsTargetContainer ? "container:" + value.Amount + "/" + value.Capacity : "source:" + value.DonorRounds)));

InventoryArrangePlanner planner = new();

// CN_Foray_20 accepts both CN_Guerrilla_Low_60 and its incoming 200-round box source.
const string forayGuid = "626994d391194520b3c12b49dfb2b536";
const string lightBoxGuid = "87aadc89276643b489adf51091df8c37";
const string heavyBoxGuid = "7a459d8955344caeb13a31d5425c00e5";
List<InventoryArrangeItem> screenshotAmmo =
[
    Item("light-box", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 60, maxAmount: 60,
        assetGuid: lightBoxGuid, state: "box-state", quality: 73, isMagazine: false,
        isAmmoSource: true, directSources: [lightBoxGuid, heavyBoxGuid]),
    Item("foray-mag", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 16, maxAmount: 20,
        assetGuid: forayGuid, state: "mag-state", quality: 61,
        directSources: [lightBoxGuid, heavyBoxGuid, forayGuid])
];
foreach (var input in new[] { screenshotAmmo, screenshotAmmo.AsEnumerable().Reverse().ToList() })
{
    InventoryArrangePlan refill = Plan(planner, input, Settings(InventoryArrangeAction.Merge, autoRefill: true));
    Check(refill.IsSuccess && refill.Placements.Single(x => x.Item.ItemKey == "foray-mag").Item.Amount == 20 &&
          refill.Placements.Single(x => x.Item.ItemKey == "light-box").Item.Amount == 56,
        "Screenshot regression: a directly compatible refillable box must supply four rounds (60+16 -> 56+20).");
    Check(InventoryStackingSemantics.TryValidateTransition(input, refill.Placements.Select(x => x.Item),
        refill.AmmoTransfers, out _), "Refill ledger must accept the safe direct bridge and conserve identities.");
    InventoryArrangePlan repeat = Plan(planner, refill.Placements.Select(x => x.Item).ToList(),
        Settings(InventoryArrangeAction.Merge, autoRefill: true));
    Check(repeat.IsSuccess && repeat.AmmoChangedContainerCount == 0, "Repeated screenshot refill must be stable.");
}

// Safety is per receiving asset: one incompatible receiver must not disable a safe receiver.
var unsafeReceiver = Item("unsafe-mag", 2, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 20,
    assetGuid: "00000000000000000000000000000001", state: "", directSources: [lightBoxGuid]);
var mixedReceivers = screenshotAmmo.Concat(new[] { unsafeReceiver }).ToList();
var mixedPlan = Plan(planner, mixedReceivers, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(mixedPlan.IsSuccess && mixedPlan.Placements.Single(x => x.Item.ItemKey == "foray-mag").Item.Amount == 20 &&
    mixedPlan.Placements.Single(x => x.Item.ItemKey == "unsafe-mag").Item.Amount == 0,
    "An unsafe edge must remain blocked without disabling a separate directly compatible receiver.");
var forgedAfter = mixedReceivers.Select(x => x.CloneWithAmount(x.ItemKey == "light-box" ? 56 :
    x.ItemKey == "unsafe-mag" ? 4 : x.Amount)).ToList();
Check(!InventoryStackingSemantics.TryValidateTransition(mixedReceivers, forgedAfter,
    new[] { new AmmoRefillTransfer(lightBoxGuid, "light-box", unsafeReceiver.AssetGuid, "unsafe-mag", 4) }, out _),
    "Independent ledger validation must reject a forged indirect conversion even when totals are conserved.");

Check(!AmmoRefillIdentity.TryGetValidCapacity(0, out _) &&
      AmmoRefillIdentity.TryGetValidCapacity(40, out int validCapacity) && validCapacity == 40,
    "Production readers must reject zero-capacity Workshop ammo assets instead of coercing them to capacity one.");
Check(ItemStackingClassifier.Classify(new ItemStackingFacts(false, false, false, false, 1, false, false)) ==
      ItemStackingKind.HomogeneousInstances &&
      ItemStackingClassifier.Classify(new ItemStackingFacts(false, false, false, false, 1, false, false)) ==
      ItemStackingKind.HomogeneousInstances,
    "Amount-one stateless items without visible quality must use homogeneous whole-instance semantics.");
Check(ItemStackingClassifier.Classify(new ItemStackingFacts(false, false, false, false, 1, true, false)) ==
      ItemStackingKind.IndividualInstance &&
      ItemStackingClassifier.Classify(new ItemStackingFacts(false, false, false, false, 1, false, true)) ==
      ItemStackingKind.IndividualInstance,
    "State-bearing or quality-visible amount-one items must retain exact individual identity.");
Check((int)ItemStackingKind.IndividualInstance == 0 && (int)ItemStackingKind.FungibleUnits == 1 &&
      (int)ItemStackingKind.AmmoContainer == 2 && (int)ItemStackingKind.StatefulContainer == 3 &&
      (int)ItemStackingKind.HomogeneousInstances == 4,
    "HomogeneousInstances must be appended without renumbering existing planner semantics.");

AmmoCompatibilityGraph compatibilityGraph = new();
compatibilityGraph.AddContainer("ap");
compatibilityGraph.AddContainer("bridge");
compatibilityGraph.AddContainer("frag");
compatibilityGraph.AddDirectTransfer("ap", "bridge");
compatibilityGraph.AddDirectTransfer("bridge", "frag");
AmmoCompatibilityCatalog directCatalog = compatibilityGraph.Build();
Check(directCatalog.CanTransfer("ap", "bridge") && directCatalog.CanTransfer("bridge", "frag") &&
      !directCatalog.CanTransfer("bridge", "ap") && !directCatalog.CanTransfer("ap", "frag"),
    "FillTargetItem compatibility must remain direct and directional without reverse or transitive closure.");
Check(directCatalog.ProjectDirectSources(["bridge", "frag"]).SequenceEqual(["ap", "bridge"]),
    "Hover-compatible ammo projection must use the same direct source edges as execution.");

AmmoRefillAssetRules allocatorSourceRules = new("allocator-source", false, true, Array.Empty<string>());
AmmoRefillAssetRules allocatorTargetRules = new("allocator-target", true, false, ["allocator-source"]);
List<AmmoRefillLot> firstPassLots =
[
    new("allocator-source-entry", AmmoRefillIdentity.BuildContainerIdentity("allocator-source", 100, "", 10),
        allocatorSourceRules, 1, 8, 10),
    new("allocator-target-entry", AmmoRefillIdentity.BuildContainerIdentity("allocator-target", 100, "", 10),
        allocatorTargetRules, 1, 0, 10)
];
Check(AmmoRefillAllocator.TryPlan(firstPassLots, out AmmoRefillPlan firstPassPlan) && firstPassPlan.Changed,
    "The shared direct-edge planner should produce a first-pass refill.");
Dictionary<string, AmmoRefillBatch> firstPassAllocations = firstPassPlan.Allocations.ToDictionary(value => value.LotKey, value => value.Allocation);
List<AmmoRefillLot> secondPassLots =
[
    new("allocator-source-entry", AmmoRefillIdentity.BuildContainerIdentity("allocator-source", 100, "", 10),
        allocatorSourceRules, 1, BatchAmount(firstPassAllocations["allocator-source-entry"], 10), 10),
    new("allocator-target-entry", AmmoRefillIdentity.BuildContainerIdentity("allocator-target", 100, "", 10),
        allocatorTargetRules, 1, BatchAmount(firstPassAllocations["allocator-target-entry"], 10), 10)
];
Check(AmmoRefillAllocator.TryPlan(secondPassLots, out AmmoRefillPlan secondPassPlan) && !secondPassPlan.Changed &&
      secondPassPlan.Transfers.All(value => value.SourceAssetGuid == value.TargetAssetGuid),
    "A second TryPlan pass must be idempotent and must not repeat a cross-asset transfer.");

for (int seed = 0; seed < 20_000; seed++)
{
    Random random = new(seed);
    int assetCount = random.Next(3, 7);
    int[] ranks = Enumerable.Range(1, assetCount).OrderBy(_ => random.Next()).ToArray();
    string[] assets = ranks.Select(value => $"00000000-0000-0000-0000-{value:D12}").ToArray();
    bool[,] edges = new bool[assetCount, assetCount];
    bool hasCrossEdge = false;
    for (int sourceIndex = 0; sourceIndex < assetCount; sourceIndex++)
    {
        for (int targetIndex = 0; targetIndex < assetCount; targetIndex++)
        {
            edges[sourceIndex, targetIndex] = random.NextDouble() < (sourceIndex == targetIndex ? 0.45 : 0.24);
            hasCrossEdge |= sourceIndex != targetIndex && edges[sourceIndex, targetIndex];
        }
    }
    if (!hasCrossEdge) edges[assetCount - 1, 0] = true;

    AmmoRefillAssetRules[] rules = new AmmoRefillAssetRules[assetCount];
    for (int assetIndex = 0; assetIndex < assetCount; assetIndex++)
    {
        string[] incoming = Enumerable.Range(0, assetCount)
            .Where(sourceIndex => edges[sourceIndex, assetIndex])
            .Select(sourceIndex => assets[sourceIndex])
            .ToArray();
        bool isDirectSource = Enumerable.Range(0, assetCount).Any(targetIndex => edges[assetIndex, targetIndex]);
        rules[assetIndex] = new AmmoRefillAssetRules(assets[assetIndex], random.Next(2) == 0,
            isDirectSource, incoming);
    }

    List<AmmoRefillLot> fuzzLots = new();
    for (int assetIndex = 0; assetIndex < assetCount; assetIndex++)
    {
        int capacity = random.Next(1, 21);
        int amount = random.Next(0, capacity + 1);
        fuzzLots.Add(new AmmoRefillLot("container-" + assetIndex,
            AmmoRefillIdentity.BuildContainerIdentity(assets[assetIndex], 100, "state-" + assetIndex, capacity),
            rules[assetIndex], 1, amount, capacity));
        if (rules[assetIndex].IsDirectSource && random.Next(2) == 0)
        {
            fuzzLots.Add(AmmoRefillLot.SourceOnly("source-" + assetIndex,
                "resource|" + assets[assetIndex], rules[assetIndex], random.Next(0, 21)));
        }
    }

    if (!AmmoRefillAllocator.TryPlan(fuzzLots, out AmmoRefillPlan fuzzFirstPlan))
        throw new InvalidOperationException("First property plan failed at seed " + seed + ".");
    List<AmmoRefillLot> fuzzSecondInput = MaterializeSingleInstancePlan(fuzzLots, fuzzFirstPlan);
    if (!AmmoRefillAllocator.TryPlan(fuzzSecondInput, out AmmoRefillPlan fuzzSecondPlan))
        throw new InvalidOperationException("Second property plan failed at seed " + seed + ".");
    List<AmmoRefillLot> fuzzSecondOutput = MaterializeSingleInstancePlan(fuzzSecondInput, fuzzSecondPlan);
    if (fuzzSecondPlan.Changed || LotFingerprint(fuzzSecondInput) != LotFingerprint(fuzzSecondOutput))
        throw new InvalidOperationException("Second property plan was not idempotent at seed " + seed + ".");
}

InventoryArrangeItem fixedHotkeyItem = Item("fixed", 1, hotkeyBound: true, protectedItem: true);
InventoryArrangePlan fixedPlan = Plan(planner,
    [Item("other", 0), fixedHotkeyItem], Settings(InventoryArrangeAction.Arrange));
InventoryArrangePlacement fixedPlacement = fixedPlan.Placements.Single(placement => placement.Item.ItemKey == "fixed");
Check(fixedPlan.IsSuccess && fixedPlacement.Page == 3 && fixedPlacement.X == 1 && fixedPlacement.Y == 0,
    "Fixed hotkey items must retain their original grid coordinates.");

InventoryArrangeItem movingHotkeyItem = Item("hotkey", 1, hotkeyBound: true);
InventoryArrangePlan movingPlan = Plan(planner,
    [Item("matching", 0), movingHotkeyItem], Settings(InventoryArrangeAction.Arrange));
Check(movingPlan.IsSuccess && movingPlan.MergeCount == 0 && movingPlan.Placements.Count == 2,
    "A moving hotkey item must retain a unique planned placement instead of being merged away.");
Check(movingPlan.Placements.Any(placement => placement.Item.ItemKey == "hotkey"),
    "The plan must retain the moving hotkey item so its shortcut coordinates can be updated.");

const string resourceGuid = "22222222-2222-2222-2222-222222222222";
List<InventoryArrangeItem> resourceInput =
[
    Item("resource-a", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 3, maxAmount: 10,
        assetGuid: resourceGuid, state: "", quality: 100),
    Item("resource-b", 1, stackingKind: ItemStackingKind.FungibleUnits, amount: 4, maxAmount: 10,
        assetGuid: resourceGuid, state: "", quality: 100)
];
InventoryArrangePlan resourcePlan = Plan(planner, resourceInput, Settings(InventoryArrangeAction.Merge));
Check(resourcePlan.IsSuccess && resourcePlan.MergeCount == 1 && resourcePlan.Placements.Count == 1 &&
      resourcePlan.Placements[0].Item.Amount == 7,
    "Confirmed fungible supplies must merge by amount up to their asset MaxAmount.");
Check(resourcePlan.MoveCount == 0 && resourcePlan.Placements[0].X == 0,
    "Merge-only must retain the surviving stack position rather than rearranging inventory.");
Check(Fingerprint(resourceInput) == Fingerprint(resourcePlan.Placements.Select(placement => placement.Item)),
    "Fungible-unit consolidation must preserve the semantic inventory fingerprint.");

List<InventoryArrangeItem> differentQualityResources =
[
    Item("quality-low", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 3, maxAmount: 10,
        assetGuid: resourceGuid, state: "", quality: 25),
    Item("quality-high", 1, stackingKind: ItemStackingKind.FungibleUnits, amount: 4, maxAmount: 10,
        assetGuid: resourceGuid, state: "", quality: 100)
];
InventoryArrangePlan differentQualityPlan = Plan(planner, differentQualityResources,
    Settings(InventoryArrangeAction.Merge));
Check(differentQualityPlan.IsSuccess && differentQualityPlan.MergeCount == 0 &&
      differentQualityPlan.Placements.Count == 2 &&
      differentQualityPlan.Placements.Select(value => value.Item.Quality).OrderBy(value => value).SequenceEqual([25, 100]) &&
      Fingerprint(differentQualityResources) ==
          Fingerprint(differentQualityPlan.Placements.Select(placement => placement.Item)),
    "Fungible resources with different hidden quality must remain separate exact buckets during inventory merge.");

List<InventoryArrangeItem> exactIdentityInput =
[
    Item("exact-a", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 2, maxAmount: 10,
        assetGuid: resourceGuid, state: "A", quality: 100),
    Item("exact-b", 1, stackingKind: ItemStackingKind.FungibleUnits, amount: 2, maxAmount: 10,
        assetGuid: resourceGuid, state: "B", quality: 100),
    Item("exact-c", 2, stackingKind: ItemStackingKind.FungibleUnits, amount: 2, maxAmount: 20,
        assetGuid: resourceGuid, state: "A", quality: 90)
];
InventoryArrangePlan exactIdentityPlan = Plan(planner, exactIdentityInput, Settings(InventoryArrangeAction.Merge));
Check(exactIdentityPlan.IsSuccess && exactIdentityPlan.Placements.Count == 3 && exactIdentityPlan.MergeCount == 0,
    "State, quality, or native stack-limit differences must split otherwise fungible stacks.");

const string containerGuid = "33333333-3333-3333-3333-333333333333";
List<InventoryArrangeItem> containerInput =
[
    Item("fuel-a", 0, stackingKind: ItemStackingKind.StatefulContainer, amount: 20, maxAmount: 100,
        assetGuid: containerGuid, state: "1400"),
    Item("fuel-b", 1, stackingKind: ItemStackingKind.StatefulContainer, amount: 40, maxAmount: 100,
        assetGuid: containerGuid, state: "2800")
];
InventoryArrangePlan containerPlan = Plan(planner, containerInput, Settings(InventoryArrangeAction.Merge));
Check(containerPlan.IsSuccess && containerPlan.Placements.Count == 2 && containerPlan.MergeCount == 0,
    "Fuel, water, generator, storage, library, and refill content amounts must remain complete instances.");
Check(Fingerprint(containerInput) == Fingerprint(containerPlan.Placements.Select(placement => placement.Item)),
    "Stateful-container payloads must survive merge-only unchanged.");

List<InventoryArrangeItem> unknownInput =
[
    Item("unknown-a", 0, amount: 20, maxAmount: 100, assetGuid: containerGuid, state: ""),
    Item("unknown-b", 1, amount: 40, maxAmount: 100, assetGuid: containerGuid, state: "")
];
InventoryArrangePlan unknownPlan = Plan(planner, unknownInput, Settings(InventoryArrangeAction.Merge));
Check(unknownPlan.IsSuccess && unknownPlan.Placements.Count == 2,
    "MaxAmount alone must not flatten an unknown item into one stack.");

const string seedGuid = "4c35676e-b54a-4b4f-90c9-49e549f0f1cd";
List<InventoryArrangeItem> homogeneousSeeds =
[
    Item("seed-hidden-quality-a", 0, stackingKind: ItemStackingKind.HomogeneousInstances,
        amount: 1, maxAmount: 1, assetGuid: seedGuid, state: "", quality: 10),
    Item("seed-hidden-quality-b", 1, stackingKind: ItemStackingKind.HomogeneousInstances,
        amount: 1, maxAmount: 1, assetGuid: seedGuid, state: "", quality: 100)
];
InventoryArrangePlan homogeneousSeedPlan = Plan(planner, homogeneousSeeds, Settings(InventoryArrangeAction.Merge));
Check(homogeneousSeedPlan.IsSuccess && homogeneousSeedPlan.Placements.Count == 2 && homogeneousSeedPlan.MergeCount == 0 &&
      Fingerprint(homogeneousSeeds) == Fingerprint(homogeneousSeedPlan.Placements.Select(value => value.Item)),
    "Homogeneous seeds may share storage identity but must remain two whole inventory instances rather than one ResourceUnits stack.");
Check(Fingerprint([homogeneousSeeds[0]]) != Fingerprint([homogeneousSeeds[1]]),
    "Homogeneous-instance conservation must still distinguish hidden native quality values.");

const string sourceGuid = "44444444-4444-4444-4444-444444444444";
const string targetGuid = "55555555-5555-5555-5555-555555555555";
const string thirdGuid = "66666666-6666-6666-6666-666666666666";
const string bridgeGuid = "77777777-7777-7777-7777-777777777777";
const string vanillaSupplyGuid = "ac70d382-ad48-474c-9ad0-57bf1bd8a3d4";
const string vanillaMagazineGuid = "2326f2dc-ced6-4ff0-b61f-452b16c9754f";

List<InventoryArrangeItem> sameAssetAmmo =
[
    Item("same-low", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 2, maxAmount: 10,
        assetGuid: sourceGuid, state: "same"),
    Item("same-high", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 8, maxAmount: 10,
        assetGuid: sourceGuid, state: "same")
];
InventoryArrangePlan sameAssetPlan = Plan(planner, sameAssetAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, int> sameAssetAmounts = sameAssetPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
Check(sameAssetPlan.IsSuccess && sameAssetAmounts["same-high"] == 10 && sameAssetAmounts["same-low"] == 0 &&
      sameAssetPlan.AmmoChangedContainerCount == 2 && sameAssetPlan.AmmoTransfers.Count == 0,
    "Identical container identities should compact full-first without inventing a cross-asset transfer.");

List<InventoryArrangeItem> directAmmo =
[
    Item("source", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 8, maxAmount: 10,
        assetGuid: sourceGuid, state: "source-state", quality: 90, isMagazine: false, isAmmoSource: true),
    Item("target", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: targetGuid, state: "target-state", quality: 100, directSources: [sourceGuid])
];
InventoryArrangePlan directPlan = Plan(planner, directAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, InventoryArrangeItem> directOutput = directPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item);
Check(directPlan.IsSuccess && directOutput["source"].Amount == 0 && directOutput["target"].Amount == 8 &&
      directOutput["source"].Quality == 90 && directOutput["source"].StateHash == "source-state" &&
      directOutput["target"].Quality == 100 && directOutput["target"].StateHash == "target-state" &&
      directPlan.AmmoTransfers.Count == 1 && directPlan.AmmoChangedContainerCount == 2,
    "A direct source-to-target transfer must preserve both container metadata identities and report payload changes.");
Check(Fingerprint(directAmmo) != Fingerprint(directOutput.Values),
    "Per-asset fingerprints must expose a legal cross-asset amount change rather than hiding it in a compatibility group.");

List<InventoryArrangeItem> vanillaSupplyToMagazine =
[
    Item("civilian-40", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 40, maxAmount: 40,
        assetGuid: vanillaSupplyGuid, state: "", directSources: [vanillaSupplyGuid],
        isMagazine: false, isAmmoSource: true),
    Item("ace-empty", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 6,
        assetGuid: vanillaMagazineGuid, state: "", directSources: [vanillaSupplyGuid]),
    Item("ace-half", 2, stackingKind: ItemStackingKind.AmmoContainer, amount: 3, maxAmount: 6,
        assetGuid: vanillaMagazineGuid, state: "", directSources: [vanillaSupplyGuid])
];
InventoryArrangePlan vanillaSupplyPlan = Plan(planner, vanillaSupplyToMagazine,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, int> vanillaSupplyAmounts = vanillaSupplyPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
Check(vanillaSupplyPlan.IsSuccess && vanillaSupplyAmounts["civilian-40"] == 31 &&
      vanillaSupplyAmounts["ace-empty"] == 6 && vanillaSupplyAmounts["ace-half"] == 6 &&
      vanillaSupplyPlan.AmmoTransfers.Where(value => value.SourceLotKey == "civilian-40").Sum(value => value.Rounds) == 9 &&
      vanillaSupplyPlan.AmmoChangedContainerCount == 3,
    "Vanilla fungible Supply rounds must be a source-only donor for directly compatible magazines.");
Check(vanillaSupplyAmounts.Values.Sum() == 43,
    "Vanilla Supply-to-Magazine refill must conserve supply rounds plus rounds already in magazines.");
List<InventoryArrangeItem> vanillaSecondInput = vanillaSupplyPlan.Placements.Select(placement =>
{
    InventoryArrangeItem item = placement.Item.CloneWithAmount(placement.Item.Amount);
    item.OriginalAmount = item.Amount;
    item.SourcePage = placement.Page;
    item.SourceX = placement.X;
    item.SourceY = placement.Y;
    item.SourceRotation = placement.Rotation;
    return item;
}).ToList();
InventoryArrangePlan vanillaSecondPlan = Plan(planner, vanillaSecondInput,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(vanillaSecondPlan.IsSuccess && vanillaSecondPlan.AmmoChangedContainerCount == 0 &&
      vanillaSecondPlan.AmmoTransfers.Count == 0 &&
      vanillaSecondPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount)
          .OrderBy(value => value.Key).SequenceEqual(vanillaSupplyAmounts.OrderBy(value => value.Key)),
    "A second Vanilla Supply-to-Magazine arrange pass must be idempotent.");
foreach (int[] order in new[]
{
    new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
    new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
})
{
    InventoryArrangePlan permutationPlan = Plan(planner, order.Select(index => vanillaSupplyToMagazine[index]).ToList(),
        Settings(InventoryArrangeAction.Merge, autoRefill: true));
    Dictionary<string, int> permutationAmounts = permutationPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
    Check(permutationPlan.IsSuccess && vanillaSupplyAmounts.OrderBy(value => value.Key)
            .SequenceEqual(permutationAmounts.OrderBy(value => value.Key)),
        "Vanilla fungible Supply refill must be identical for every input permutation.");
}

const string persistentSupplyTargetGuid = "88888888-8888-8888-8888-888888888880";
List<InventoryArrangeItem> supplyToSupply =
[
    Item("supply-donor", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 10, maxAmount: 10,
        assetGuid: vanillaSupplyGuid, state: "", isMagazine: false, isAmmoSource: true),
    Item("persistent-supply-target", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: persistentSupplyTargetGuid, state: "", directSources: [vanillaSupplyGuid], isMagazine: false)
];
InventoryArrangePlan supplyToSupplyPlan = Plan(planner, supplyToSupply,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(supplyToSupplyPlan.IsSuccess && supplyToSupplyPlan.Placements.Count == 1 &&
      supplyToSupplyPlan.Placements.Single().Item.ItemKey == "persistent-supply-target" &&
      supplyToSupplyPlan.Placements.Single().Item.Amount == 10,
    "A direct Supply-to-Supply edge may fill a persistent target while deleting the exhausted fungible donor stack.");

const string sameGuidSupply = "99999999-9999-9999-9999-999999999990";
List<InventoryArrangeItem> sameGuidSupplyToPersistent =
[
    Item("same-guid-source", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 10, maxAmount: 10,
        assetGuid: sameGuidSupply, state: "", directSources: [sameGuidSupply],
        isMagazine: false, isAmmoSource: true),
    Item("same-guid-target", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: sameGuidSupply, state: "", directSources: [sameGuidSupply],
        isMagazine: false, isAmmoSource: true)
];
InventoryArrangePlan sameGuidSupplyPlan = Plan(planner, sameGuidSupplyToPersistent,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(sameGuidSupplyPlan.IsSuccess && sameGuidSupplyPlan.Placements.Count == 1 &&
      sameGuidSupplyPlan.Placements.Single().Item.ItemKey == "same-guid-target" &&
      sameGuidSupplyPlan.Placements.Single().Item.Amount == 10 &&
      sameGuidSupplyPlan.AmmoTransfers.Count == 1 &&
      sameGuidSupplyPlan.AmmoTransfers[0].SourceLotKey == "same-guid-source" &&
      sameGuidSupplyPlan.AmmoTransfers[0].SourceAssetGuid == sameGuidSupplyPlan.AmmoTransfers[0].TargetAssetGuid,
    "Same-GUID fungible Supply-to-persistent Supply must retain its SourceOnly ledger while ordinary container reshuffles stay filtered.");

List<InventoryArrangeItem> reverseAmmo =
[
    Item("reverse-source", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: sourceGuid, state: "", isMagazine: false, isAmmoSource: true),
    Item("reverse-target", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 8, maxAmount: 10,
        assetGuid: targetGuid, state: "", directSources: [sourceGuid])
];
InventoryArrangePlan reversePlan = Plan(planner, reverseAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(reversePlan.IsSuccess && reversePlan.Placements.Single(value => value.Item.ItemKey == "reverse-source").Item.Amount == 0 &&
      reversePlan.Placements.Single(value => value.Item.ItemKey == "reverse-target").Item.Amount == 8,
    "A direct A-to-B blueprint must never infer the reverse B-to-A transfer.");

List<InventoryArrangeItem> chainedAmmo =
[
    Item("chain-a", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 8, maxAmount: 10,
        assetGuid: sourceGuid, state: "", isMagazine: false, isAmmoSource: true),
    Item("chain-b", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: targetGuid, state: "", directSources: [sourceGuid], isAmmoSource: true),
    Item("chain-c", 2, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: thirdGuid, state: "", directSources: [targetGuid])
];
InventoryArrangePlan chainedPlan = Plan(planner, chainedAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, int> chainedAmounts = chainedPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
Check(chainedPlan.IsSuccess && chainedAmounts["chain-a"] == 0 && chainedAmounts["chain-b"] == 8 && chainedAmounts["chain-c"] == 0 &&
      chainedPlan.AmmoTransfers.All(value => value.SourceLotKey != "chain-b"),
    "A-to-B-to-C must freeze donor budgets so rounds received by B cannot be donated to C in the same operation.");

const string bridgeC = "00000000-0000-0000-0000-000000000001";
const string bridgeB = "00000000-0000-0000-0000-000000000002";
const string bridgeA = "00000000-0000-0000-0000-000000000003";
List<InventoryArrangeItem> crossClickBridge =
[
    Item("bridge-a-source", 0, stackingKind: ItemStackingKind.FungibleUnits, amount: 10, maxAmount: 10,
        assetGuid: bridgeA, state: "", isMagazine: false, isAmmoSource: true),
    Item("bridge-b", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 10, maxAmount: 20,
        assetGuid: bridgeB, state: "", directSources: [bridgeA, bridgeB], isAmmoSource: true),
    Item("bridge-c", 2, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 20,
        assetGuid: bridgeC, state: "", directSources: [bridgeB, bridgeC])
];
InventoryArrangePlan bridgeFirstPlan = Plan(planner, crossClickBridge,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, int> bridgeFirstAmounts = bridgeFirstPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
Check(bridgeFirstPlan.IsSuccess && !bridgeFirstAmounts.ContainsKey("bridge-a-source") &&
      bridgeFirstAmounts["bridge-b"] == 20 && bridgeFirstAmounts["bridge-c"] == 0 &&
      bridgeFirstPlan.AmmoTransfers.All(value => value.SourceLotKey != "bridge-b"),
    "A persistent asset with cross-asset incoming and a current cross-asset target must not donate through the bridge.");
List<InventoryArrangeItem> bridgeSecondInput = bridgeFirstPlan.Placements.Select(placement =>
{
    InventoryArrangeItem item = placement.Item.CloneWithAmount(placement.Item.Amount);
    item.OriginalAmount = item.Amount;
    item.SourcePage = placement.Page;
    item.SourceX = placement.X;
    item.SourceY = placement.Y;
    item.SourceRotation = placement.Rotation;
    return item;
}).ToList();
InventoryArrangePlan bridgeSecondPlan = Plan(planner, bridgeSecondInput,
    Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(bridgeSecondPlan.IsSuccess && bridgeSecondPlan.AmmoChangedContainerCount == 0 &&
      bridgeSecondPlan.AmmoTransfers.Count == 0 &&
      Fingerprint(bridgeFirstPlan.Placements.Select(value => value.Item)) ==
      Fingerprint(bridgeSecondPlan.Placements.Select(value => value.Item)),
    "The second bridge arrange pass must be a no-op with an identical full fingerprint.");

List<InventoryArrangeItem> bridgeAbsentAmmo =
[
    Item("ap", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 8, maxAmount: 10,
        assetGuid: sourceGuid, state: "", isAmmoSource: true),
    Item("frag", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 10,
        assetGuid: thirdGuid, state: "", directSources: [bridgeGuid])
];
InventoryArrangePlan bridgeAbsentPlan = Plan(planner, bridgeAbsentAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Check(bridgeAbsentPlan.IsSuccess && bridgeAbsentPlan.Placements.Single(value => value.Item.ItemKey == "ap").Item.Amount == 8 &&
      bridgeAbsentPlan.Placements.Single(value => value.Item.ItemKey == "frag").Item.Amount == 0,
    "AP/FM/FRAG-style bridge relationships must not transfer when the direct bridge source is absent.");

List<InventoryArrangeItem> fanOutAmmo =
[
    Item("fan-source", 0, stackingKind: ItemStackingKind.AmmoContainer, amount: 10, maxAmount: 10,
        assetGuid: sourceGuid, state: "", isMagazine: false, isAmmoSource: true),
    Item("fan-b", 1, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 6,
        assetGuid: targetGuid, state: "", directSources: [sourceGuid]),
    Item("fan-c", 2, stackingKind: ItemStackingKind.AmmoContainer, amount: 0, maxAmount: 6,
        assetGuid: thirdGuid, state: "", directSources: [sourceGuid])
];
InventoryArrangePlan fanOutPlan = Plan(planner, fanOutAmmo, Settings(InventoryArrangeAction.Merge, autoRefill: true));
Dictionary<string, int> fanOutAmounts = fanOutPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
Check(fanOutPlan.IsSuccess && fanOutAmounts["fan-source"] == 0 && fanOutAmounts["fan-b"] == 6 && fanOutAmounts["fan-c"] == 4 &&
      fanOutPlan.AmmoTransfers.Where(value => value.SourceLotKey == "fan-source").Sum(value => value.Rounds) == 10,
    "Fan-out targets must share one frozen source budget without overspending it.");
foreach (int[] order in new[]
{
    new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
    new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
})
{
    InventoryArrangePlan permutationPlan = Plan(planner, order.Select(index => fanOutAmmo[index]).ToList(),
        Settings(InventoryArrangeAction.Merge, autoRefill: true));
    Dictionary<string, int> permutationAmounts = permutationPlan.Placements.ToDictionary(value => value.Item.ItemKey, value => value.Item.Amount);
    Check(permutationPlan.IsSuccess && fanOutAmounts.OrderBy(value => value.Key).SequenceEqual(permutationAmounts.OrderBy(value => value.Key)),
        "Ammo transfer results must be identical for every input permutation.");
}

List<InventoryArrangeItem> hotkeyResourceInput =
[
    Item("hotkey-resource", 0, hotkeyBound: true, stackingKind: ItemStackingKind.FungibleUnits, amount: 3,
        maxAmount: 10, assetGuid: resourceGuid, state: ""),
    Item("free-resource", 1, stackingKind: ItemStackingKind.FungibleUnits, amount: 4,
        maxAmount: 10, assetGuid: resourceGuid, state: "")
];
InventoryArrangePlan hotkeyResourcePlan = Plan(planner, hotkeyResourceInput, Settings(InventoryArrangeAction.Merge));
Check(hotkeyResourcePlan.IsSuccess && hotkeyResourcePlan.Placements.Count == 2 && hotkeyResourcePlan.MergeCount == 0,
    "A hotkey-bound resource stack must remain addressable and cannot be merged away.");

foreach ((InventoryArrangeAction action, InventorySortPreset preset) in new[]
{
    (InventoryArrangeAction.Arrange, InventorySortPreset.Name),
    (InventoryArrangeAction.Merge, InventorySortPreset.Name),
    (InventoryArrangeAction.Compact, InventorySortPreset.Name),
    (InventoryArrangeAction.Arrange, InventorySortPreset.LowMovement)
})
{
    List<InventoryArrangeItem> hotkeyAmmo =
    [
        Item("hotkey-ammo", 0, hotkeyBound: true, stackingKind: ItemStackingKind.FungibleUnits,
            amount: 8, maxAmount: 10, assetGuid: sourceGuid, state: "", isMagazine: false, isAmmoSource: true),
        Item("hotkey-target", 1, stackingKind: ItemStackingKind.AmmoContainer,
            amount: 0, maxAmount: 10, assetGuid: targetGuid, state: "", directSources: [sourceGuid])
    ];
    InventoryArrangeSettings hotkeySettings = Settings(action, autoRefill: true);
    hotkeySettings.Preset = preset;
    InventoryArrangePlan hotkeyAmmoPlan = Plan(planner, hotkeyAmmo, hotkeySettings);
    Check(hotkeyAmmoPlan.IsSuccess &&
          hotkeyAmmoPlan.Placements.Single(value => value.Item.ItemKey == "hotkey-ammo").Item.Amount == 8 &&
          hotkeyAmmoPlan.Placements.Single(value => value.Item.ItemKey == "hotkey-target").Item.Amount == 0,
        $"Hotkey ammo payload protection must be identical for {action}/{preset}.");
}

List<InventoryArrangeItem> alreadyOrdered =
[
    Item("a", 0, assetGuid: "88888888-8888-8888-8888-888888888888"),
    Item("b", 1, assetGuid: "99999999-9999-9999-9999-999999999999")
];
InventoryArrangePlan noChangePlan = Plan(planner, alreadyOrdered, Settings(InventoryArrangeAction.Arrange));
Dictionary<string, int> originalAmounts = alreadyOrdered.ToDictionary(item => item.ItemKey, item => item.Amount, StringComparer.Ordinal);
Check(noChangePlan.IsSuccess && !InventoryArrangeExecutionPolicy.RequiresInventoryRebuild(
        noChangePlan, key => originalAmounts.TryGetValue(key, out int amount) ? amount : -1),
    "An already ordered inventory must be a no-op instead of rebuilding native items and hiding hotkey numbers.");
Check(InventoryArrangeExecutionPolicy.RequiresHotkeyDisplayRefresh(inventoryWasRebuilt: true, capturedBindingCount: 1),
    "Any native inventory rebuild containing a bound item must refresh hotkey number display even when coordinates are unchanged.");

InventoryHotkeyBindingState[] dragBindings =
[
    new InventoryHotkeyBindingState(0, 100, new InventoryItemAddress(3, 1, 0)),
    new InventoryHotkeyBindingState(1, 200, new InventoryItemAddress(4, 2, 0))
];
IReadOnlyList<InventoryHotkeyBindingState> dragged = InventoryHotkeyBindingPolicy.Remap(
    dragBindings,
    [new InventoryItemMove(100, new InventoryItemAddress(3, 1, 0), new InventoryItemAddress(3, 5, 1))]);
Check(dragged[0].Address == new InventoryItemAddress(3, 5, 1) && dragged[1].Address == dragBindings[1].Address,
    "Dragging a bound item must move only its hotkey binding to the new item coordinates.");

IReadOnlyList<InventoryHotkeyBindingState> swapped = InventoryHotkeyBindingPolicy.Remap(
    dragBindings,
    [
        new InventoryItemMove(100, new InventoryItemAddress(3, 1, 0), new InventoryItemAddress(4, 2, 0)),
        new InventoryItemMove(200, new InventoryItemAddress(4, 2, 0), new InventoryItemAddress(3, 1, 0))
    ]);
Check(swapped[0].Address == new InventoryItemAddress(4, 2, 0) && swapped[1].Address == new InventoryItemAddress(3, 1, 0),
    "Swapping two bound items must swap both hotkey bindings without clearing either binding.");

string inventoryFeatureSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src",
    "UnturnedSingleplayerCheatMenu", "InventoryArrange", "InventoryArrangeFeature.cs"));
Check(inventoryFeatureSource.Contains("_journalConnectionKey = _connectionKey();", StringComparison.Ordinal) &&
      inventoryFeatureSource.Contains("!string.Equals(_journalConnectionKey, _connectionKey(), StringComparison.Ordinal)", StringComparison.Ordinal),
    "Inventory arrange Undo must fail closed after the connection key changes.");

Console.WriteLine("Inventory arrange planner smoke checks passed.");
