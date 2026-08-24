namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventorySortKeySpec
{
	internal InventorySortKey Key;

	internal InventorySortDirection Direction;

	internal InventorySortKeySpec(InventorySortKey key, InventorySortDirection direction)
	{
		Key = key;
		Direction = direction;
	}
}
