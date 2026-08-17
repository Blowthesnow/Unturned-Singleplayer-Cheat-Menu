using System;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Models;

internal enum ItemPrimaryCategory
{
    All,
    Weapons,
    AmmoAndAttachments,
    Clothing,
    FoodAndDrink,
    Medical,
    Building,
    Tools,
    Other
}

internal enum ItemOriginFilter
{
    All,
    Official,
    Workshop,
    MapOrOther
}

[Flags]
internal enum GunFireModeFilter
{
    None = 0,
    Semi = 1,
    Auto = 2,
    Burst = 4
}

internal sealed class ItemFilterState
{
    public ItemPrimaryCategory Category { get; set; } = ItemPrimaryCategory.All;
    public EItemType? ItemType { get; set; }
    public ItemOriginFilter Origin { get; set; } = ItemOriginFilter.All;
    public EItemRarity? Rarity { get; set; }
    public ESlotType? Slot { get; set; }
    public EAction? GunAction { get; set; }
    public GunFireModeFilter FireModes { get; set; }
    public string SearchQuery { get; set; } = string.Empty;

    public int ActiveFilterCount =>
        (ItemType.HasValue ? 1 : 0)
        + (Origin != ItemOriginFilter.All ? 1 : 0)
        + (Rarity.HasValue ? 1 : 0)
        + (Slot.HasValue ? 1 : 0)
        + (GunAction.HasValue ? 1 : 0)
        + (FireModes != GunFireModeFilter.None ? 1 : 0);

    public ItemFilterState Clone()
    {
        ItemFilterState clone = new();
        clone.CopyFrom(this);
        return clone;
    }

    public void CopyFrom(ItemFilterState source)
    {
        Category = source.Category;
        ItemType = source.ItemType;
        Origin = source.Origin;
        Rarity = source.Rarity;
        Slot = source.Slot;
        GunAction = source.GunAction;
        FireModes = source.FireModes;
        SearchQuery = source.SearchQuery;
    }

    public void ResetAdvanced()
    {
        ItemType = null;
        Origin = ItemOriginFilter.All;
        Rarity = null;
        Slot = null;
        GunAction = null;
        FireModes = GunFireModeFilter.None;
    }
}
