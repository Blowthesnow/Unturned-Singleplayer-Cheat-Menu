using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class ItemFilterService
{
    private static readonly ItemPrimaryCategory[] PrimaryCategories =
    {
        ItemPrimaryCategory.All,
        ItemPrimaryCategory.Weapons,
        ItemPrimaryCategory.AmmoAndAttachments,
        ItemPrimaryCategory.Clothing,
        ItemPrimaryCategory.FoodAndDrink,
        ItemPrimaryCategory.Medical,
        ItemPrimaryCategory.Building,
        ItemPrimaryCategory.Tools,
        ItemPrimaryCategory.Other
    };

    private static readonly EItemType[] WeaponTypes =
    {
        EItemType.GUN,
        EItemType.MELEE,
        EItemType.THROWABLE
    };

    private static readonly EItemType[] AttachmentTypes =
    {
        EItemType.MAGAZINE,
        EItemType.SIGHT,
        EItemType.TACTICAL,
        EItemType.GRIP,
        EItemType.BARREL,
        EItemType.OPTIC
    };

    private static readonly EItemType[] ClothingTypes =
    {
        EItemType.HAT,
        EItemType.SHIRT,
        EItemType.PANTS,
        EItemType.BACKPACK,
        EItemType.VEST,
        EItemType.MASK,
        EItemType.GLASSES
    };

    private static readonly EItemType[] FoodTypes =
    {
        EItemType.FOOD,
        EItemType.WATER
    };

    private static readonly EItemType[] MedicalTypes =
    {
        EItemType.MEDICAL,
        EItemType.FILTER
    };

    private static readonly EItemType[] BuildingTypes =
    {
        EItemType.STRUCTURE,
        EItemType.BARRICADE,
        EItemType.STORAGE,
        EItemType.TRAP,
        EItemType.FARM,
        EItemType.GROWER,
        EItemType.GENERATOR,
        EItemType.OIL_PUMP,
        EItemType.BEACON,
        EItemType.SENTRY
    };

    private static readonly EItemType[] ToolTypes =
    {
        EItemType.TOOL,
        EItemType.FUEL,
        EItemType.REFILL,
        EItemType.FISHER,
        EItemType.TIRE,
        EItemType.VEHICLE_REPAIR_TOOL,
        EItemType.VEHICLE_PAINT_TOOL,
        EItemType.VEHICLE_LOCKPICK_TOOL
    };

    public static IReadOnlyList<ItemPrimaryCategory> Categories => PrimaryCategories;

    public static IReadOnlyList<EItemType> GetItemTypes(ItemPrimaryCategory category)
    {
        return category switch
        {
            ItemPrimaryCategory.Weapons => WeaponTypes,
            ItemPrimaryCategory.AmmoAndAttachments => AttachmentTypes,
            ItemPrimaryCategory.Clothing => ClothingTypes,
            ItemPrimaryCategory.FoodAndDrink => FoodTypes,
            ItemPrimaryCategory.Medical => MedicalTypes,
            ItemPrimaryCategory.Building => BuildingTypes,
            ItemPrimaryCategory.Tools => ToolTypes,
            _ => Array.Empty<EItemType>()
        };
    }

    public static ItemPrimaryCategory GetPrimaryCategory(EItemType type)
    {
        return type switch
        {
            EItemType.GUN or EItemType.MELEE or EItemType.THROWABLE =>
                ItemPrimaryCategory.Weapons,

            EItemType.MAGAZINE or EItemType.SIGHT or EItemType.TACTICAL
                or EItemType.GRIP or EItemType.BARREL or EItemType.OPTIC =>
                ItemPrimaryCategory.AmmoAndAttachments,

            EItemType.HAT or EItemType.PANTS or EItemType.SHIRT
                or EItemType.MASK or EItemType.BACKPACK or EItemType.VEST
                or EItemType.GLASSES =>
                ItemPrimaryCategory.Clothing,

            EItemType.FOOD or EItemType.WATER =>
                ItemPrimaryCategory.FoodAndDrink,

            EItemType.MEDICAL or EItemType.FILTER =>
                ItemPrimaryCategory.Medical,

            EItemType.BARRICADE or EItemType.STORAGE or EItemType.BEACON
                or EItemType.FARM or EItemType.TRAP or EItemType.STRUCTURE
                or EItemType.GROWER or EItemType.GENERATOR or EItemType.OIL_PUMP
                or EItemType.SENTRY =>
                ItemPrimaryCategory.Building,

            EItemType.FUEL or EItemType.TOOL or EItemType.REFILL
                or EItemType.FISHER or EItemType.VEHICLE_REPAIR_TOOL
                or EItemType.VEHICLE_PAINT_TOOL or EItemType.VEHICLE_LOCKPICK_TOOL
                or EItemType.TIRE =>
                ItemPrimaryCategory.Tools,

            _ => ItemPrimaryCategory.Other
        };
    }

    public static bool Matches(ItemAsset asset, ItemFilterState filter)
    {
        if (asset == null || filter == null)
            return false;

        ItemGunAsset gun = asset as ItemGunAsset;
        ItemFilterSnapshot snapshot = new(
            GetPrimaryCategory(asset.type),
            asset.type,
            GetOrigin(asset),
            asset.rarity,
            asset.slot,
            gun != null,
            gun?.action ?? default,
            gun?.hasSemi ?? false,
            gun?.hasAuto ?? false,
            gun?.bursts ?? 0,
            asset.FriendlyName,
            asset.name,
            asset.id.ToString(),
            asset.GUID.ToString("N"),
            asset.GetOriginName());
        return Matches(snapshot, filter);
    }

    internal static bool Matches(ItemFilterSnapshot item, ItemFilterState filter)
    {
        if (filter.Category != ItemPrimaryCategory.All && item.Category != filter.Category)
            return false;
        if (filter.ItemType.HasValue && item.ItemType != filter.ItemType.Value)
            return false;
        if (filter.Origin != ItemOriginFilter.All && item.Origin != filter.Origin)
            return false;
        if (filter.Rarity.HasValue && item.Rarity != filter.Rarity.Value)
            return false;
        if (filter.Slot.HasValue && item.Slot != filter.Slot.Value)
            return false;
        if (filter.GunAction.HasValue && (!item.IsGun || item.GunAction != filter.GunAction.Value))
            return false;
        if (!MatchesFireModes(item, filter.FireModes))
            return false;
        return MatchesSearch(item, filter.SearchQuery);
    }

    public static void NormalizeForCategory(ItemFilterState filter)
    {
        if (filter == null)
            return;

        IReadOnlyList<EItemType> itemTypes = GetItemTypes(filter.Category);
        if (filter.ItemType.HasValue && !Contains(itemTypes, filter.ItemType.Value))
            filter.ItemType = null;

        if (filter.ItemType != EItemType.GUN)
        {
            filter.GunAction = null;
            filter.FireModes = GunFireModeFilter.None;
        }
    }

    public static string GetPrimaryCategoryLabel(ItemPrimaryCategory category)
    {
        return category switch
        {
            ItemPrimaryCategory.Weapons => "武器",
            ItemPrimaryCategory.AmmoAndAttachments => "弹药与配件",
            ItemPrimaryCategory.Clothing => "衣物",
            ItemPrimaryCategory.FoodAndDrink => "食物与饮料",
            ItemPrimaryCategory.Medical => "医疗与防护",
            ItemPrimaryCategory.Building => "建筑与放置物",
            ItemPrimaryCategory.Tools => "工具",
            ItemPrimaryCategory.Other => "其他",
            _ => "全部"
        };
    }

    public static string GetItemTypeLabel(EItemType type)
    {
        return type switch
        {
            EItemType.GUN => "枪械",
            EItemType.MELEE => "近战",
            EItemType.THROWABLE => "投掷物",
            EItemType.MAGAZINE => "弹匣",
            EItemType.SIGHT => "瞄具",
            EItemType.TACTICAL => "战术配件",
            EItemType.GRIP => "握把",
            EItemType.BARREL => "枪管",
            EItemType.OPTIC => "光学配件",
            EItemType.HAT => "帽子",
            EItemType.SHIRT => "上衣",
            EItemType.PANTS => "裤子",
            EItemType.BACKPACK => "背包",
            EItemType.VEST => "背心",
            EItemType.MASK => "面罩",
            EItemType.GLASSES => "眼镜",
            EItemType.FOOD => "食物",
            EItemType.WATER => "饮料",
            EItemType.MEDICAL => "医疗",
            EItemType.FILTER => "滤芯",
            EItemType.STRUCTURE => "建筑",
            EItemType.BARRICADE => "路障",
            EItemType.STORAGE => "储物",
            EItemType.TRAP => "陷阱",
            EItemType.FARM => "农作物",
            EItemType.GROWER => "种植器",
            EItemType.GENERATOR => "发电机",
            EItemType.OIL_PUMP => "油泵",
            EItemType.BEACON => "信标",
            EItemType.SENTRY => "哨戒炮",
            EItemType.TOOL => "通用工具",
            EItemType.FUEL => "燃料",
            EItemType.REFILL => "容器",
            EItemType.FISHER => "钓具",
            EItemType.TIRE => "轮胎",
            EItemType.VEHICLE_REPAIR_TOOL => "维修工具",
            EItemType.VEHICLE_PAINT_TOOL => "喷漆工具",
            EItemType.VEHICLE_LOCKPICK_TOOL => "开锁工具",
            _ => type.ToString()
        };
    }

    public static string GetOriginLabel(ItemOriginFilter origin)
    {
        return origin switch
        {
            ItemOriginFilter.Official => "原版",
            ItemOriginFilter.Workshop => "Workshop",
            ItemOriginFilter.MapOrOther => "地图 / 其他",
            _ => "全部来源"
        };
    }

    public static string GetRarityLabel(EItemRarity rarity)
    {
        return rarity switch
        {
            EItemRarity.COMMON => "普通",
            EItemRarity.UNCOMMON => "罕见",
            EItemRarity.RARE => "稀有",
            EItemRarity.EPIC => "史诗",
            EItemRarity.LEGENDARY => "传奇",
            EItemRarity.MYTHICAL => "神话",
            _ => rarity.ToString()
        };
    }

    public static string GetSlotLabel(ESlotType slot)
    {
        return slot switch
        {
            ESlotType.NONE => "快捷栏",
            ESlotType.PRIMARY => "主武器",
            ESlotType.SECONDARY => "副武器",
            ESlotType.TERTIARY => "第三槽位",
            ESlotType.ANY => "任意槽位",
            _ => slot.ToString()
        };
    }

    public static string GetGunActionLabel(EAction action)
    {
        return action switch
        {
            EAction.Bolt => "栓动",
            EAction.Pump => "泵动",
            EAction.Rail => "轨道武器",
            EAction.String => "弓 / 弩",
            EAction.Break => "折管",
            EAction.Rocket => "火箭 / 发射器",
            EAction.Minigun => "旋转机枪",
            _ => "常规枪械"
        };
    }

    public static string GetFireModeLabel(GunFireModeFilter fireModes)
    {
        List<string> labels = new();
        if ((fireModes & GunFireModeFilter.Semi) != 0)
            labels.Add("半自动");
        if ((fireModes & GunFireModeFilter.Auto) != 0)
            labels.Add("全自动");
        if ((fireModes & GunFireModeFilter.Burst) != 0)
            labels.Add("点射");
        return string.Join(" / ", labels);
    }

#pragma warning disable CS0618
    private static ItemOriginFilter GetOrigin(ItemAsset asset)
    {
        return asset.assetOrigin switch
        {
            EAssetOrigin.OFFICIAL => ItemOriginFilter.Official,
            EAssetOrigin.WORKSHOP => ItemOriginFilter.Workshop,
            _ => ItemOriginFilter.MapOrOther
        };
    }
#pragma warning restore CS0618

    private static bool MatchesFireModes(ItemFilterSnapshot item, GunFireModeFilter selected)
    {
        if (selected == GunFireModeFilter.None)
            return true;
        if (!item.IsGun)
            return false;

        return ((selected & GunFireModeFilter.Semi) != 0 && item.Semi)
            || ((selected & GunFireModeFilter.Auto) != 0 && item.Auto)
            || ((selected & GunFireModeFilter.Burst) != 0 && item.Bursts > 0);
    }

    private static bool MatchesSearch(ItemFilterSnapshot item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        query = query.Trim();
        return Contains(item.DisplayName, query)
            || Contains(item.AssetName, query)
            || Contains(item.Id, query)
            || Contains(item.Guid, query)
            || Contains(item.OriginName, query);
    }

    private static bool Contains(IReadOnlyList<EItemType> types, EItemType value)
    {
        for (int index = 0; index < types.Count; index++)
        {
            if (types[index] == value)
                return true;
        }
        return false;
    }

    private static bool Contains(string text, string value)
    {
        return !string.IsNullOrEmpty(text)
            && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }
}
