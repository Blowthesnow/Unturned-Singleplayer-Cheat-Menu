using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class ItemFilterState
{
	public ItemPrimaryCategory Category { get; set; }

	public EItemType? ItemType { get; set; }

	public ItemOriginFilter Origin { get; set; }

	public EItemRarity? Rarity { get; set; }

	public ESlotType? Slot { get; set; }

	public EAction? GunAction { get; set; }

	public GunFireModeFilter FireModes { get; set; }

	public string SearchQuery { get; set; } = string.Empty;

	public int ActiveFilterCount => (ItemType.HasValue ? 1 : 0) + ((Origin != ItemOriginFilter.All) ? 1 : 0) + (Rarity.HasValue ? 1 : 0) + (Slot.HasValue ? 1 : 0) + (GunAction.HasValue ? 1 : 0) + ((FireModes != GunFireModeFilter.None) ? 1 : 0);

	public ItemFilterState Clone()
	{
		ItemFilterState itemFilterState = new ItemFilterState();
		itemFilterState.CopyFrom(this);
		return itemFilterState;
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
