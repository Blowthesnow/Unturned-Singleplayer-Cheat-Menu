using UnturnedSingleplayerCheatMenu.InventoryArrange;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static InventoryArrangeItem Item(string key, int x, bool hotkeyBound, bool protectedItem)
{
    return new InventoryArrangeItem
    {
        ItemKey = key,
        AssetGuid = "11111111-1111-1111-1111-111111111111",
        StateHash = "state",
        Name = key,
        Width = 1,
        Height = 1,
        Amount = 1,
        MaxAmount = 2,
        SourcePage = 3,
        SourceX = x,
        SourceY = 0,
        OriginalIndex = x,
        IsHotkeyBound = hotkeyBound,
        IsProtected = protectedItem
    };
}

InventoryArrangePlanner planner = new();
InventoryArrangeSettings settings = new()
{
    Action = InventoryArrangeAction.Arrange,
    Preset = InventorySortPreset.Name,
    AutoRotate = false,
    AllowCrossPage = false,
    AutoRefillMagazines = false
};

InventoryArrangeItem fixedHotkeyItem = Item("fixed", 1, hotkeyBound: true, protectedItem: true);
InventoryArrangePlan fixedPlan = planner.CreatePlan(
[
    new InventoryArrangeContainer { Page = 3, Width = 2, Height = 1, Items = [Item("other", 0, hotkeyBound: false, protectedItem: false), fixedHotkeyItem] }
],
settings);
InventoryArrangePlacement fixedPlacement = fixedPlan.Placements.Single(placement => placement.Item.ItemKey == "fixed");
Check(fixedPlan.IsSuccess && fixedPlacement.Page == 3 && fixedPlacement.X == 1 && fixedPlacement.Y == 0,
    "Fixed hotkey items must retain their original grid coordinates.");

InventoryArrangeItem movingHotkeyItem = Item("hotkey", 1, hotkeyBound: true, protectedItem: false);
InventoryArrangePlan movingPlan = planner.CreatePlan(
[
    new InventoryArrangeContainer { Page = 3, Width = 2, Height = 1, Items = [Item("matching", 0, hotkeyBound: false, protectedItem: false), movingHotkeyItem] }
],
settings);
Check(movingPlan.IsSuccess && movingPlan.MergeCount == 0 && movingPlan.Placements.Count == 2,
    "A moving hotkey item must retain a unique planned placement instead of being merged away.");
Check(movingPlan.Placements.Any(placement => placement.Item.ItemKey == "hotkey"),
    "The plan must retain the moving hotkey item so its shortcut coordinates can be updated.");

Console.WriteLine("Inventory arrange planner smoke checks passed.");
