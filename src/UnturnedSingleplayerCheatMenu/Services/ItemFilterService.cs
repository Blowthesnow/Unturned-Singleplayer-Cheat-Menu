using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class ItemFilterService
{
	private static readonly ItemPrimaryCategory[] PrimaryCategories = new ItemPrimaryCategory[9]
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

	private static readonly EItemType[] WeaponTypes = new EItemType[3]
	{
		EItemType.GUN,
		EItemType.MELEE,
		EItemType.THROWABLE
	};

	private static readonly EItemType[] AttachmentTypes = new EItemType[6]
	{
		EItemType.MAGAZINE,
		EItemType.SIGHT,
		EItemType.TACTICAL,
		EItemType.GRIP,
		EItemType.BARREL,
		EItemType.OPTIC
	};

	private static readonly EItemType[] ClothingTypes = new EItemType[7]
	{
		EItemType.HAT,
		EItemType.SHIRT,
		EItemType.PANTS,
		EItemType.BACKPACK,
		EItemType.VEST,
		EItemType.MASK,
		EItemType.GLASSES
	};

	private static readonly EItemType[] FoodTypes = new EItemType[2]
	{
		EItemType.FOOD,
		EItemType.WATER
	};

	private static readonly EItemType[] MedicalTypes = new EItemType[2]
	{
		EItemType.MEDICAL,
		EItemType.FILTER
	};

	private static readonly EItemType[] BuildingTypes = new EItemType[10]
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

	private static readonly EItemType[] ToolTypes = new EItemType[8]
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
			_ => Array.Empty<EItemType>(),
		};
	}

	public static ItemPrimaryCategory GetPrimaryCategory(EItemType type)
	{
		switch (type)
		{
		case EItemType.GUN:
		case EItemType.MELEE:
		case EItemType.THROWABLE:
			return ItemPrimaryCategory.Weapons;
		case EItemType.SIGHT:
		case EItemType.TACTICAL:
		case EItemType.GRIP:
		case EItemType.BARREL:
		case EItemType.MAGAZINE:
		case EItemType.OPTIC:
			return ItemPrimaryCategory.AmmoAndAttachments;
		case EItemType.HAT:
		case EItemType.PANTS:
		case EItemType.SHIRT:
		case EItemType.MASK:
		case EItemType.BACKPACK:
		case EItemType.VEST:
		case EItemType.GLASSES:
			return ItemPrimaryCategory.Clothing;
		case EItemType.FOOD:
		case EItemType.WATER:
			return ItemPrimaryCategory.FoodAndDrink;
		case EItemType.MEDICAL:
		case EItemType.FILTER:
			return ItemPrimaryCategory.Medical;
		case EItemType.BARRICADE:
		case EItemType.STORAGE:
		case EItemType.BEACON:
		case EItemType.FARM:
		case EItemType.TRAP:
		case EItemType.STRUCTURE:
		case EItemType.GROWER:
		case EItemType.GENERATOR:
		case EItemType.SENTRY:
		case EItemType.OIL_PUMP:
			return ItemPrimaryCategory.Building;
		case EItemType.FUEL:
		case EItemType.TOOL:
		case EItemType.REFILL:
		case EItemType.FISHER:
		case EItemType.VEHICLE_REPAIR_TOOL:
		case EItemType.TIRE:
		case EItemType.VEHICLE_PAINT_TOOL:
		case EItemType.VEHICLE_LOCKPICK_TOOL:
			return ItemPrimaryCategory.Tools;
		default:
			return ItemPrimaryCategory.Other;
		}
	}

	public static bool Matches(ItemAsset asset, ItemFilterState filter)
	{
		return Matches(asset, filter, null);
	}

	public static bool Matches(ItemAsset asset, ItemFilterState filter, string originalName)
	{
		if (asset == null || filter == null)
		{
			return false;
		}
		ItemGunAsset itemGunAsset = asset as ItemGunAsset;
		return Matches(new ItemFilterSnapshot(GetPrimaryCategory(asset.type), asset.type, GetOrigin(asset), asset.rarity, asset.slot, itemGunAsset != null, itemGunAsset?.action ?? EAction.Trigger, itemGunAsset?.hasSemi ?? false, itemGunAsset?.hasAuto ?? false, itemGunAsset?.bursts ?? 0, asset.FriendlyName, asset.name, asset.id.ToString(), asset.GUID.ToString("N"), asset.GetOriginName(), originalName), filter);
	}

	internal static bool Matches(ItemFilterSnapshot item, ItemFilterState filter)
	{
		if (filter.Category != ItemPrimaryCategory.All && item.Category != filter.Category)
		{
			return false;
		}
		if (filter.ItemType.HasValue && item.ItemType != filter.ItemType.Value)
		{
			return false;
		}
		if (filter.Origin != ItemOriginFilter.All && item.Origin != filter.Origin)
		{
			return false;
		}
		if (filter.Rarity.HasValue && item.Rarity != filter.Rarity.Value)
		{
			return false;
		}
		if (filter.Slot.HasValue && item.Slot != filter.Slot.Value)
		{
			return false;
		}
		if (filter.GunAction.HasValue && (!item.IsGun || item.GunAction != filter.GunAction.Value))
		{
			return false;
		}
		if (!MatchesFireModes(item, filter.FireModes))
		{
			return false;
		}
		return MatchesSearch(item, filter.SearchQuery);
	}

	public static void NormalizeForCategory(ItemFilterState filter)
	{
		if (filter != null)
		{
			IReadOnlyList<EItemType> itemTypes = GetItemTypes(filter.Category);
			if (filter.ItemType.HasValue && !Contains(itemTypes, filter.ItemType.Value))
			{
				filter.ItemType = null;
			}
			if (filter.ItemType != EItemType.GUN)
			{
				filter.GunAction = null;
				filter.FireModes = GunFireModeFilter.None;
			}
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
			_ => "全部",
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
			_ => type.ToString(),
		};
	}

	public static string GetOriginLabel(ItemOriginFilter origin)
	{
		return origin switch
		{
			ItemOriginFilter.Official => "原版",
			ItemOriginFilter.Workshop => "Workshop",
			ItemOriginFilter.MapOrOther => "地图 / 其他",
			_ => "全部来源",
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
			_ => rarity.ToString(),
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
			_ => slot.ToString(),
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
			_ => "常规枪械",
		};
	}

	public static string GetFireModeLabel(GunFireModeFilter fireModes)
	{
		List<string> list = new List<string>();
		if ((fireModes & GunFireModeFilter.Semi) != GunFireModeFilter.None)
		{
			list.Add("半自动");
		}
		if ((fireModes & GunFireModeFilter.Auto) != GunFireModeFilter.None)
		{
			list.Add("全自动");
		}
		if ((fireModes & GunFireModeFilter.Burst) != GunFireModeFilter.None)
		{
			list.Add("点射");
		}
		return string.Join(" / ", list);
	}

	private static ItemOriginFilter GetOrigin(ItemAsset asset)
	{
		if (AssetOriginClassifier.IsOfficial(asset))
		{
			return ItemOriginFilter.Official;
		}
		if (AssetOriginClassifier.IsWorkshop(asset))
		{
			return ItemOriginFilter.Workshop;
		}
		return ItemOriginFilter.MapOrOther;
	}

	private static bool MatchesFireModes(ItemFilterSnapshot item, GunFireModeFilter selected)
	{
		if (selected == GunFireModeFilter.None)
		{
			return true;
		}
		if (!item.IsGun)
		{
			return false;
		}
		if (((selected & GunFireModeFilter.Semi) == 0 || !item.Semi) && ((selected & GunFireModeFilter.Auto) == 0 || !item.Auto))
		{
			if ((selected & GunFireModeFilter.Burst) != GunFireModeFilter.None)
			{
				return item.Bursts > 0;
			}
			return false;
		}
		return true;
	}

	private static bool MatchesSearch(ItemFilterSnapshot item, string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}
		query = query.Trim();
		if (!Contains(item.DisplayName, query) && !Contains(item.OriginalName, query) && !Contains(item.AssetName, query) && !Contains(item.Id, query) && !Contains(item.Guid, query))
		{
			return Contains(item.OriginName, query);
		}
		return true;
	}

	private static bool Contains(IReadOnlyList<EItemType> types, EItemType value)
	{
		for (int i = 0; i < types.Count; i++)
		{
			if (types[i] == value)
			{
				return true;
			}
		}
		return false;
	}

	private static bool Contains(string text, string value)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
		}
		return false;
	}
}
