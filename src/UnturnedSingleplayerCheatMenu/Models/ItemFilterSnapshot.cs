using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Models;

internal readonly struct ItemFilterSnapshot(ItemPrimaryCategory category, EItemType itemType, ItemOriginFilter origin, EItemRarity rarity, ESlotType slot, bool isGun, EAction gunAction, bool semi, bool auto, int bursts, string displayName, string assetName, string id, string guid, string originName, string originalName = null)
{
	public ItemPrimaryCategory Category { get; } = category;

	public EItemType ItemType { get; } = itemType;

	public ItemOriginFilter Origin { get; } = origin;

	public EItemRarity Rarity { get; } = rarity;

	public ESlotType Slot { get; } = slot;

	public bool IsGun { get; } = isGun;

	public EAction GunAction { get; } = gunAction;

	public bool Semi { get; } = semi;

	public bool Auto { get; } = auto;

	public int Bursts { get; } = bursts;

	public string DisplayName { get; } = displayName;

	public string OriginalName { get; } = originalName ?? displayName;

	public string AssetName { get; } = assetName;

	public string Id { get; } = id;

	public string Guid { get; } = guid;

	public string OriginName { get; } = originName;
}
