using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

ItemFilterSnapshot gun = new(
    ItemPrimaryCategory.Weapons,
    EItemType.GUN,
    ItemOriginFilter.Workshop,
    EItemRarity.RARE,
    ESlotType.PRIMARY,
    isGun: true,
    EAction.Trigger,
    semi: true,
    auto: true,
    bursts: 3,
    "Workshop Rifle",
    "rifle_asset",
    "123",
    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Workshop");

ItemFilterState filter = new();
Check(ItemFilterService.Matches(gun, filter), "Default filter should match the snapshot.");

filter.Category = ItemPrimaryCategory.Weapons;
filter.ItemType = EItemType.MELEE;
Check(!ItemFilterService.Matches(gun, filter), "Exact item type should exclude a gun.");
filter.ItemType = EItemType.GUN;
filter.Origin = ItemOriginFilter.Workshop;
filter.Rarity = EItemRarity.RARE;
filter.Slot = ESlotType.PRIMARY;
Check(ItemFilterService.Matches(gun, filter), "Origin, rarity, slot, and type should combine with AND.");

filter.FireModes = GunFireModeFilter.Auto;
Check(ItemFilterService.Matches(gun, filter), "Auto mode should match hasAuto.");
filter.FireModes = GunFireModeFilter.Burst;
Check(ItemFilterService.Matches(gun, filter), "Burst mode should match a positive burst count.");
filter.FireModes = GunFireModeFilter.Semi | GunFireModeFilter.Auto;
Check(ItemFilterService.Matches(gun, filter), "Multiple fire modes should use OR within the mode dimension.");

filter.FireModes = GunFireModeFilter.None;
filter.GunAction = EAction.Bolt;
Check(!ItemFilterService.Matches(gun, filter), "Gun action should be exact.");
filter.GunAction = EAction.Trigger;
filter.SearchQuery = "workshop";
Check(ItemFilterService.Matches(gun, filter), "Search should include origin text.");
filter.SearchQuery = "missing";
Check(!ItemFilterService.Matches(gun, filter), "Unmatched search should exclude the snapshot.");

filter.SearchQuery = string.Empty;
Check(filter.ActiveFilterCount == 5, "Active filter count should count dimensions, not each fire mode.");
filter.ResetAdvanced();
Check(filter.ActiveFilterCount == 0 && filter.Category == ItemPrimaryCategory.Weapons,
    "Reset should keep the primary category while clearing advanced filters.");

filter.ItemType = EItemType.GUN;
filter.GunAction = EAction.Trigger;
filter.FireModes = GunFireModeFilter.Auto;
filter.Category = ItemPrimaryCategory.AmmoAndAttachments;
ItemFilterService.NormalizeForCategory(filter);
Check(
    filter.ItemType == null
        && filter.GunAction == null
        && filter.FireModes == GunFireModeFilter.None,
    "Changing away from weapons should clear incompatible gun filters.");

Check(ItemFilterService.GetPrimaryCategory(EItemType.GUN) == ItemPrimaryCategory.Weapons,
    "Gun should map to the Weapons primary category.");
Check(ItemFilterService.GetPrimaryCategory(EItemType.MAGAZINE) == ItemPrimaryCategory.AmmoAndAttachments,
    "Magazine should map to AmmoAndAttachments.");
Check(ItemFilterService.GetPrimaryCategory(EItemType.MEDICAL) == ItemPrimaryCategory.Medical,
    "Medical should map to Medical.");

Console.WriteLine("Item filtering smoke checks passed.");
