using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Models;

internal readonly struct ItemFilterSnapshot
{
    public ItemFilterSnapshot(
        ItemPrimaryCategory category,
        EItemType itemType,
        ItemOriginFilter origin,
        EItemRarity rarity,
        ESlotType slot,
        bool isGun,
        EAction gunAction,
        bool semi,
        bool auto,
        int bursts,
        string displayName,
        string assetName,
        string id,
        string guid,
        string originName)
    {
        Category = category;
        ItemType = itemType;
        Origin = origin;
        Rarity = rarity;
        Slot = slot;
        IsGun = isGun;
        GunAction = gunAction;
        Semi = semi;
        Auto = auto;
        Bursts = bursts;
        DisplayName = displayName;
        AssetName = assetName;
        Id = id;
        Guid = guid;
        OriginName = originName;
    }

    public ItemPrimaryCategory Category { get; }
    public EItemType ItemType { get; }
    public ItemOriginFilter Origin { get; }
    public EItemRarity Rarity { get; }
    public ESlotType Slot { get; }
    public bool IsGun { get; }
    public EAction GunAction { get; }
    public bool Semi { get; }
    public bool Auto { get; }
    public int Bursts { get; }
    public string DisplayName { get; }
    public string AssetName { get; }
    public string Id { get; }
    public string Guid { get; }
    public string OriginName { get; }
}
