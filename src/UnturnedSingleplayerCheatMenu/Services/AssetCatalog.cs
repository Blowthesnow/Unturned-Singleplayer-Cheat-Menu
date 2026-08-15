using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class AssetCatalog
{
    private readonly List<ItemAsset> _items = new();
    private readonly List<VehicleAsset> _vehicles = new();

    public IReadOnlyList<ItemAsset> Items => _items;
    public IReadOnlyList<VehicleAsset> Vehicles => _vehicles;
    public bool IsDirty { get; private set; } = true;
    public int WorkshopItemCount { get; private set; }
    public int WorkshopVehicleCount { get; private set; }

    public void MarkDirty() => IsDirty = true;

    public void Refresh()
    {
        _items.Clear();
        _vehicles.Clear();

        Assets.find(_items);
        Assets.find(_vehicles);

        // Assets.find reads the current mapping, so replaced assets are already absent.
        _items.RemoveAll(asset => asset == null || asset.id == 0 || asset.isPro);
        _vehicles.RemoveAll(asset => asset == null);

        _items.Sort(CompareItems);
        _vehicles.Sort(CompareVehicles);
        WorkshopItemCount = _items.Count(IsWorkshopAsset);
        WorkshopVehicleCount = _vehicles.Count(IsWorkshopAsset);
        IsDirty = false;
    }

    public static string GetItemCategory(ItemAsset asset)
    {
        switch (asset.type)
        {
            case EItemType.GUN:
            case EItemType.MELEE:
            case EItemType.THROWABLE:
                return "武器";

            case EItemType.MAGAZINE:
            case EItemType.SIGHT:
            case EItemType.TACTICAL:
            case EItemType.GRIP:
            case EItemType.BARREL:
            case EItemType.OPTIC:
                return "弹药与配件";

            case EItemType.HAT:
            case EItemType.PANTS:
            case EItemType.SHIRT:
            case EItemType.MASK:
            case EItemType.BACKPACK:
            case EItemType.VEST:
            case EItemType.GLASSES:
                return "衣物";

            case EItemType.FOOD:
            case EItemType.WATER:
                return "食物与饮料";

            case EItemType.MEDICAL:
            case EItemType.FILTER:
                return "医疗与防护";

            case EItemType.BARRICADE:
            case EItemType.STORAGE:
            case EItemType.BEACON:
            case EItemType.FARM:
            case EItemType.TRAP:
            case EItemType.STRUCTURE:
            case EItemType.GROWER:
            case EItemType.GENERATOR:
            case EItemType.OIL_PUMP:
            case EItemType.SENTRY:
                return "建筑与放置物";

            case EItemType.FUEL:
            case EItemType.TOOL:
            case EItemType.REFILL:
            case EItemType.FISHER:
            case EItemType.VEHICLE_REPAIR_TOOL:
            case EItemType.VEHICLE_PAINT_TOOL:
            case EItemType.VEHICLE_LOCKPICK_TOOL:
            case EItemType.TIRE:
                return "工具";

            default:
                return "其他";
        }
    }

    public static string GetVehicleCategory(VehicleAsset asset)
    {
        switch (asset.engine)
        {
            case EEngine.CAR: return "陆地车辆";
            case EEngine.PLANE: return "固定翼飞机";
            case EEngine.HELICOPTER: return "直升机";
            case EEngine.BLIMP: return "飞艇";
            case EEngine.BOAT: return "船只";
            case EEngine.TRAIN: return "火车";
            default: return "其他";
        }
    }

    public static bool Matches(Asset asset, string displayName, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        query = query.Trim();
        return Contains(displayName, query)
            || Contains(asset.name, query)
            || Contains(asset.id.ToString(), query)
            || Contains(asset.GUID.ToString("N"), query)
            || Contains(asset.GetOriginName(), query);
    }

    public static string GetOriginLabel(Asset asset)
    {
        if (IsWorkshopAsset(asset))
            return PluginLocalization.Translate("模组");

        string origin = asset.GetOriginName();
        if (Contains(origin, "core") || Contains(origin, "official"))
            return PluginLocalization.Translate("原版");
        return string.IsNullOrWhiteSpace(origin) ? PluginLocalization.Translate("未知来源") : origin;
    }

#pragma warning disable CS0618
    public static bool IsWorkshopAsset(Asset asset) => asset.assetOrigin == EAssetOrigin.WORKSHOP;
#pragma warning restore CS0618

    private static int CompareItems(ItemAsset left, ItemAsset right)
    {
        int category = string.Compare(GetItemCategory(left), GetItemCategory(right), StringComparison.CurrentCulture);
        return category != 0 ? category : string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.CurrentCultureIgnoreCase);
    }

    private static int CompareVehicles(VehicleAsset left, VehicleAsset right)
    {
        int category = string.Compare(GetVehicleCategory(left), GetVehicleCategory(right), StringComparison.CurrentCulture);
        return category != 0 ? category : string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool Contains(string text, string value)
    {
        return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
