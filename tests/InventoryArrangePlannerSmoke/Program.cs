using UnturnedSingleplayerCheatMenu.InventoryArrange;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static InventoryArrangeItem Item(string id, int index, int width, int height, int amount = 1, int maxAmount = 1)
{
    return new InventoryArrangeItem
    {
        ItemKey = id + "-" + index,
        AssetGuid = id,
        StateHash = "state",
        Name = id,
        TypeRank = 1,
        LegacyId = index,
        Width = width,
        Height = height,
        Amount = amount,
        MaxAmount = maxAmount,
        Quality = 100,
        SourcePage = 3,
        SourceX = index,
        SourceY = 0,
        OriginalIndex = index
    };
}

InventoryArrangePlanner planner = new();
InventoryArrangeSettings settings = new();
InventoryArrangeContainer page = new() { Page = 3, Width = 4, Height = 2 };
page.Items.Add(Item("A", 0, 2, 1));
page.Items.Add(Item("B", 1, 1, 1));
page.Items.Add(Item("C", 2, 1, 2));
InventoryArrangePlan deterministicA = planner.CreatePlan(new[] { page }, settings);
InventoryArrangePlan deterministicB = planner.CreatePlan(new[] { page }, settings);
Check(deterministicA.IsSuccess && deterministicB.IsSuccess, "A fit must create a plan.");
Check(deterministicA.Placements.Count == 3, "All three items must be planned.");
Check(deterministicA.Placements.Zip(deterministicB.Placements).All(pair => pair.First.Page == pair.Second.Page && pair.First.X == pair.Second.X && pair.First.Y == pair.Second.Y && pair.First.Rotation == pair.Second.Rotation), "Repeated planning must be stable.");

InventoryArrangeContainer full = new() { Page = 3, Width = 1, Height = 1 };
full.Items.Add(Item("TooLarge", 0, 2, 1));
InventoryArrangePlan impossible = planner.CreatePlan(new[] { full }, settings);
Check(!impossible.IsSuccess && impossible.Placements.Count == 0, "An impossible arrangement must fail before producing placements.");

InventoryArrangeContainer mergePage = new() { Page = 3, Width = 3, Height = 1 };
mergePage.Items.Add(Item("Stack", 0, 1, 1, 2, 5));
mergePage.Items.Add(Item("Stack", 1, 1, 1, 3, 5));
mergePage.Items[0].LegacyId = 77;
mergePage.Items[1].LegacyId = 77;
InventoryArrangePlan merge = planner.CreatePlan(new[] { mergePage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Merge });
Check(merge.IsSuccess && merge.Placements.Count == 1 && merge.Placements[0].Item.Amount == 5, "Merge-only must combine compatible partial stacks without reordering unrelated items.");

InventoryArrangeContainer splitMergePage = new() { Page = 3, Width = 3, Height = 1 };
splitMergePage.Items.Add(Item("Split", 0, 1, 1, 5, 5));
splitMergePage.Items.Add(Item("Split", 1, 1, 1, 4, 5));
splitMergePage.Items[0].LegacyId = 88;
splitMergePage.Items[1].LegacyId = 88;
InventoryArrangePlan splitMerge = planner.CreatePlan(new[] { splitMergePage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Merge });
Check(splitMerge.IsSuccess && splitMerge.Placements.Count == 2 && splitMerge.Placements[0].X != splitMerge.Placements[1].X, "Multiple merged output stacks must retain distinct source positions.");
InventoryArrangeContainer statePage = new() { Page = 3, Width = 2, Height = 1 };
InventoryArrangeItem stateA = Item("Stateful", 0, 1, 1, 2, 5);
InventoryArrangeItem stateB = Item("Stateful", 1, 1, 1, 3, 5);
stateA.LegacyId = 99;
stateB.LegacyId = 99;
stateB.StateHash = "different";
statePage.Items.Add(stateA);
statePage.Items.Add(stateB);
InventoryArrangePlan stateMerge = planner.CreatePlan(new[] { statePage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Merge });
Check(stateMerge.IsSuccess && stateMerge.Placements.Count == 2, "Different item state must prevent merging.");

InventoryArrangePlan compact = planner.CreatePlan(new[] { mergePage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Compact });
Check(compact.IsSuccess && compact.MergeCount == 0 && compact.Placements.Count == 2, "Compact-only must not merge compatible stacks.");

foreach (InventorySortPreset preset in new[] { InventorySortPreset.Compatibility, InventorySortPreset.Compact, InventorySortPreset.Type, InventorySortPreset.Name, InventorySortPreset.LargeFirst, InventorySortPreset.SmallFirst })
{
    InventoryArrangePlan presetPlan = planner.CreatePlan(new[] { page }, new InventoryArrangeSettings { Preset = preset });
    Check(presetPlan.IsSuccess, "Every first-round preset must produce a plan: " + preset);
}


InventoryArrangeItem lowAmmo = Item("LowAmmo", 0, 1, 1);
InventoryArrangeItem highAmmo = Item("HighAmmo", 1, 1, 1);
lowAmmo.AmmoCount = 2;
highAmmo.AmmoCount = 8;
InventoryArrangeContainer ammoPage = new() { Page = 3, Width = 2, Height = 1 };
ammoPage.Items.Add(lowAmmo);
ammoPage.Items.Add(highAmmo);
InventoryArrangePlan ammoPlan = planner.CreatePlan(new[] { ammoPage }, new InventoryArrangeSettings { Preset = InventorySortPreset.Ammo });
Check(ammoPlan.IsSuccess && ammoPlan.Placements[0].Item.Name == "HighAmmo", "Ammo preset must put a readable higher-ammo item first.");

InventoryArrangeContainer emptyPage = new() { Page = 3, Width = 2, Height = 1 };
emptyPage.Items.Add(Item("Empty", 0, 1, 1, 0, 5));
InventoryArrangePlan emptyPlan = planner.CreatePlan(new[] { emptyPage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Merge });
Check(emptyPlan.IsSuccess && emptyPlan.Placements.Count == 1 && emptyPlan.Placements[0].Item.Amount == 0, "Zero-amount items must not disappear during merge.");

Check(InventorySortRegistry.TryParseCustom("Name:Descending,Area:Ascending", out List<InventorySortKeySpec> customKeys) && customKeys.Count == 2, "Custom sort chains must parse two valid keys.");
InventoryArrangePlan customPlan = planner.CreatePlan(new[] { page }, new InventoryArrangeSettings { Preset = InventorySortPreset.Custom, CustomKeys = customKeys });
Check(customPlan.IsSuccess, "A parsed custom sort chain must produce a plan.");

InventoryArrangePlan invalidCustomPlan = planner.CreatePlan(new[] { page }, new InventoryArrangeSettings { Preset = InventorySortPreset.Custom });
InventoryArrangePlan compatibilityPlan = planner.CreatePlan(new[] { page }, new InventoryArrangeSettings { Preset = InventorySortPreset.Compatibility });
Check(invalidCustomPlan.IsSuccess && invalidCustomPlan.Placements.Zip(compatibilityPlan.Placements).All(pair => pair.First.X == pair.Second.X && pair.First.Y == pair.Second.Y), "An invalid custom chain must fall back to compatibility planning.");

InventoryArrangeContainer lowPage = new() { Page = 3, Width = 4, Height = 2 };
lowPage.Items.Add(Item("LowA", 0, 1, 1));
lowPage.Items.Add(Item("LowB", 1, 1, 1));
lowPage.Items.Add(Item("LowC", 2, 1, 1));
InventoryArrangeItem common = Item("Common", 0, 1, 1);
InventoryArrangeItem rare = Item("Rare", 1, 1, 1);
common.RarityRank = 0;
rare.RarityRank = 5;
InventoryArrangeContainer rarityPage = new() { Page = 3, Width = 2, Height = 1 };
rarityPage.Items.Add(common);
rarityPage.Items.Add(rare);
InventoryArrangePlan rarityPlan = planner.CreatePlan(new[] { rarityPage }, new InventoryArrangeSettings { Preset = InventorySortPreset.Rarity });
Check(rarityPlan.IsSuccess && rarityPlan.Placements[0].Item.Name == "Rare", "High-rarity preset must put the higher-rarity item first.");

InventoryArrangePlan lowMovementPlan = planner.CreatePlan(new[] { lowPage }, new InventoryArrangeSettings { Preset = InventorySortPreset.LowMovement });
Check(lowMovementPlan.IsSuccess && lowMovementPlan.MoveCount == 0, "Low-movement planning must preserve the original positions.");

InventoryArrangeItem halfMagazine = Item("Magazine", 0, 1, 1, 15, 30);
InventoryArrangeItem secondMagazine = Item("Magazine", 1, 1, 1, 15, 30);
halfMagazine.IsMagazine = true;
secondMagazine.IsMagazine = true;
halfMagazine.AmmoGroup = "5.56";
secondMagazine.AmmoGroup = "5.56";
halfMagazine.AmmoCount = 15;
secondMagazine.AmmoCount = 15;
halfMagazine.AmmoCapacity = 30;
secondMagazine.AmmoCapacity = 30;
InventoryArrangeContainer refillPage = new() { Page = 3, Width = 2, Height = 1 };
refillPage.Items.Add(halfMagazine);
refillPage.Items.Add(secondMagazine);
InventoryArrangePlan refillPlan = planner.CreatePlan(new[] { refillPage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Arrange });
Check(refillPlan.IsSuccess && refillPlan.Placements.Count == 2 && refillPlan.Placements[0].Item.Amount == 30 && refillPlan.Placements[1].Item.Amount == 0, $"Two half magazines must become one full and one empty magazine. success={refillPlan.IsSuccess}; count={refillPlan.Placements.Count}; amounts={string.Join(",", refillPlan.Placements.Select(placement => placement.Item.Amount))}; failure={refillPlan.FailureReason}");
InventoryArrangeItem ammoBox = Item("AmmoBox", 0, 1, 1, 20, 40);
InventoryArrangeItem emptyMagazine = Item("Magazine", 1, 1, 1, 0, 30);
ammoBox.IsAmmoSource = true;
emptyMagazine.IsMagazine = true;
ammoBox.AmmoCount = 20;
emptyMagazine.AmmoCount = 0;
ammoBox.AmmoGroup = "7.62";
emptyMagazine.AmmoGroup = "7.62";
ammoBox.AmmoCapacity = 40;
emptyMagazine.AmmoCapacity = 30;
ammoBox.AssetGuid = "ammo-box-guid";
emptyMagazine.AssetGuid = "magazine-guid";
InventoryArrangeContainer ammoBoxPage = new() { Page = 3, Width = 2, Height = 1 };
ammoBoxPage.Items.Add(ammoBox);
ammoBoxPage.Items.Add(emptyMagazine);
InventoryArrangePlan ammoBoxPlan = planner.CreatePlan(new[] { ammoBoxPage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Arrange });
Check(ammoBoxPlan.IsSuccess && ammoBoxPlan.Placements[0].Item.Amount == 20 && ammoBoxPlan.Placements[1].Item.Amount == 0, "An ammo box and empty compatible magazine must retain both containers and preserve the total ammo.");
InventoryArrangeItem halfAmmoBox = Item("AmmoBox", 0, 1, 1, 20, 40);
InventoryArrangeItem secondHalfAmmoBox = Item("AmmoBox", 1, 1, 1, 20, 40);
halfAmmoBox.IsAmmoSource = true;
secondHalfAmmoBox.IsAmmoSource = true;
halfAmmoBox.AmmoCount = 20;
secondHalfAmmoBox.AmmoCount = 20;
halfAmmoBox.AmmoGroup = "ammo-box-group";
secondHalfAmmoBox.AmmoGroup = "ammo-box-group";
halfAmmoBox.AmmoCapacity = 40;
secondHalfAmmoBox.AmmoCapacity = 40;
InventoryArrangeContainer ammoBoxRefillPage = new() { Page = 3, Width = 2, Height = 1 };
ammoBoxRefillPage.Items.Add(halfAmmoBox);
ammoBoxRefillPage.Items.Add(secondHalfAmmoBox);
InventoryArrangePlan ammoBoxRefillPlan = planner.CreatePlan(new[] { ammoBoxRefillPage }, new InventoryArrangeSettings { Action = InventoryArrangeAction.Arrange });
Check(ammoBoxRefillPlan.IsSuccess && ammoBoxRefillPlan.Placements[0].Item.Amount == 40 && ammoBoxRefillPlan.Placements[1].Item.Amount == 0, "Two half ammo boxes must become one full and one empty box.");

InventoryArrangeItem protectedItem = Item("Protected", 0, 1, 1);
protectedItem.IsProtected = true;
InventoryArrangeContainer protectedPage = new() { Page = 3, Width = 2, Height = 1 };
protectedPage.Items.Add(protectedItem);
protectedPage.Items.Add(Item("Move", 1, 1, 1));
InventoryArrangePlan protectedPlan = planner.CreatePlan(new[] { protectedPage }, settings);
InventoryArrangePlacement protectedPlacement = protectedPlan.Placements.Single(placement => placement.Item.IsProtected);
Check(protectedPlan.IsSuccess && protectedPlacement.X == 0 && protectedPlacement.Y == 0, "Protected items must retain their original position.");

Check(InventoryQuickTransferIdentity.IsSameAsset(42, "guid-a", 42, "guid-a"), "The clicked item and candidate with the same id and asset GUID must match.");
Check(!InventoryQuickTransferIdentity.IsSameAsset(42, "guid-a", 43, "guid-a"), "A different legacy id must not match even when the GUID is equal.");
Check(!InventoryQuickTransferIdentity.IsSameAsset(42, "guid-a", 42, "guid-b"), "A different asset GUID must not match even when the id is equal.");
Check(!InventoryQuickTransferIdentity.IsSameAsset(42, "guid-a", 42, "GUID-A"), "Asset GUID matching must be ordinal and case-sensitive.");
Console.WriteLine("Inventory arrange planner smoke checks passed.");
