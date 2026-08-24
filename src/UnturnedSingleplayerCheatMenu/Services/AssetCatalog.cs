using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class AssetCatalog
{
	private readonly List<ItemAsset> _items = new List<ItemAsset>();

	private readonly List<VehicleAsset> _vehicles = new List<VehicleAsset>();

	public IReadOnlyList<ItemAsset> Items => _items;

	public IReadOnlyList<VehicleAsset> Vehicles => _vehicles;

	public bool IsDirty { get; private set; } = true;

	public int WorkshopItemCount { get; private set; }

	public int WorkshopVehicleCount { get; private set; }

	public void MarkDirty()
	{
		IsDirty = true;
	}

	public void Refresh()
	{
		_items.Clear();
		_vehicles.Clear();
		Assets.find(_items);
		Assets.find(_vehicles);
		_items.RemoveAll((ItemAsset asset) => asset == null || asset.id == 0 || asset.isPro);
		_vehicles.RemoveAll((VehicleAsset asset) => asset == null);
		_items.Sort(CompareItems);
		_vehicles.Sort(CompareVehicles);
		WorkshopItemCount = _items.Count(IsWorkshopAsset);
		WorkshopVehicleCount = _vehicles.Count(IsWorkshopAsset);
		IsDirty = false;
	}

	public static ItemPrimaryCategory GetItemCategory(ItemAsset asset)
	{
		return ItemFilterService.GetPrimaryCategory(asset.type);
	}

	public static string GetVehicleCategory(VehicleAsset asset)
	{
		return asset.engine switch
		{
			EEngine.CAR => "陆地车辆",
			EEngine.PLANE => "固定翼飞机",
			EEngine.HELICOPTER => "直升机",
			EEngine.BLIMP => "飞艇",
			EEngine.BOAT => "船只",
			EEngine.TRAIN => "火车",
			_ => "其他",
		};
	}

	public static bool Matches(Asset asset, string displayName, string query)
	{
		return Matches(asset, displayName, null, query);
	}

	public static bool Matches(Asset asset, string displayName, string originalName, string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}
		query = query.Trim();
		if (!Contains(displayName, query) && !Contains(originalName, query) && !Contains(asset.name, query) && !Contains(asset.id.ToString(), query) && !Contains(asset.GUID.ToString("N"), query))
		{
			return Contains(asset.GetOriginName(), query);
		}
		return true;
	}

	public static string GetOriginLabel(Asset asset)
	{
		if (IsWorkshopAsset(asset))
		{
			return PluginLocalization.Translate("模组");
		}
		string originName = asset.GetOriginName();
		if (Contains(originName, "core") || Contains(originName, "official") || Contains(originName, "vanilla"))
		{
			return PluginLocalization.Translate("原版");
		}
		if (!string.IsNullOrWhiteSpace(originName))
		{
			return originName;
		}
		return PluginLocalization.Translate("未知来源");
	}

	public static bool IsWorkshopAsset(Asset asset)
	{
		return asset.assetOrigin == EAssetOrigin.WORKSHOP;
	}

	private static int CompareItems(ItemAsset left, ItemAsset right)
	{
		int num = GetItemCategory(left).CompareTo(GetItemCategory(right));
		if (num == 0)
		{
			return string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.CurrentCultureIgnoreCase);
		}
		return num;
	}

	private static int CompareVehicles(VehicleAsset left, VehicleAsset right)
	{
		int num = string.Compare(GetVehicleCategory(left), GetVehicleCategory(right), StringComparison.CurrentCulture);
		if (num == 0)
		{
			return string.Compare(left.FriendlyName, right.FriendlyName, StringComparison.CurrentCultureIgnoreCase);
		}
		return num;
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
