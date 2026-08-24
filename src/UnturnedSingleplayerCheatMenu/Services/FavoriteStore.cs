using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class FavoriteStore
{
	private readonly ManualLogSource _log;

	private readonly string _filePath;

	private readonly HashSet<string> _itemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _vehicleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public int ItemCount => _itemKeys.Count;

	public int VehicleCount => _vehicleKeys.Count;

	public string FilePath => _filePath;

	public FavoriteStore(ManualLogSource log)
	{
		_log = log;
		_filePath = Path.Combine(Paths.ConfigPath, "UnturnedSingleplayerCheatMenu.favorites.json");
	}

	public void Load()
	{
		_itemKeys.Clear();
		_vehicleKeys.Clear();
		if (!File.Exists(_filePath))
		{
			_log.LogInfo("已读取收藏：物品 0，车辆 0。");
			return;
		}
		try
		{
			FavoriteAssetCollection favoriteAssetCollection = JsonConvert.DeserializeObject<FavoriteAssetCollection>(File.ReadAllText(_filePath));
			AddValidKeys(_itemKeys, favoriteAssetCollection?.ItemKeys);
			AddValidKeys(_vehicleKeys, favoriteAssetCollection?.VehicleKeys);
			_log.LogInfo($"已读取收藏：物品 {_itemKeys.Count}，车辆 {_vehicleKeys.Count}。");
		}
		catch (Exception arg)
		{
			_log.LogError($"读取收藏失败，已保留原文件：{_filePath}\n{arg}");
		}
	}

	public bool IsItemFavorite(ItemAsset asset)
	{
		if (asset != null)
		{
			return _itemKeys.Contains(GetAssetKey(asset));
		}
		return false;
	}

	public bool IsVehicleFavorite(VehicleAsset asset)
	{
		if (asset != null)
		{
			return _vehicleKeys.Contains(GetAssetKey(asset));
		}
		return false;
	}

	public bool TrySetItemFavorite(ItemAsset asset, bool favorite)
	{
		return TrySetFavorite(_itemKeys, asset, favorite, "物品");
	}

	public bool TrySetVehicleFavorite(VehicleAsset asset, bool favorite)
	{
		return TrySetFavorite(_vehicleKeys, asset, favorite, "车辆");
	}

	private bool TrySetFavorite(HashSet<string> target, Asset asset, bool favorite, string kind)
	{
		if (asset == null)
		{
			return false;
		}
		string assetKey = GetAssetKey(asset);
		bool flag = target.Contains(assetKey);
		if (flag == favorite)
		{
			return true;
		}
		if (favorite)
		{
			target.Add(assetKey);
		}
		else
		{
			target.Remove(assetKey);
		}
		if (Save())
		{
			_log.LogInfo((favorite ? "已收藏" : "已取消收藏") + kind + "：" + asset.FriendlyName + "；键=" + assetKey + "。");
			return true;
		}
		if (flag)
		{
			target.Add(assetKey);
		}
		else
		{
			target.Remove(assetKey);
		}
		return false;
	}

	private bool Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? Paths.ConfigPath);
			string contents = JsonConvert.SerializeObject(new FavoriteAssetCollection
			{
				ItemKeys = _itemKeys.OrderBy<string, string>((string value) => value, StringComparer.OrdinalIgnoreCase).ToArray(),
				VehicleKeys = _vehicleKeys.OrderBy<string, string>((string value) => value, StringComparer.OrdinalIgnoreCase).ToArray()
			}, Formatting.Indented);
			File.WriteAllText(_filePath, contents);
			_log.LogInfo($"已保存收藏：物品 {_itemKeys.Count}，车辆 {_vehicleKeys.Count}。");
			return true;
		}
		catch (Exception arg)
		{
			_log.LogError($"保存收藏失败：{_filePath}\n{arg}");
			return false;
		}
	}

	private static void AddValidKeys(HashSet<string> target, IEnumerable<string> values)
	{
		if (values == null)
		{
			return;
		}
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				target.Add(value.Trim());
			}
		}
	}

	private static string GetAssetKey(Asset asset)
	{
		if (asset.GUID != Guid.Empty)
		{
			return $"guid:{asset.GUID:D}";
		}
		string text = asset.GetOriginName() ?? string.Empty;
		return $"legacy:{asset.GetType().FullName}:{asset.id}:{text}:{asset.name}";
	}
}
