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
    private readonly HashSet<string> _itemKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _vehicleKeys = new(StringComparer.OrdinalIgnoreCase);

    public FavoriteStore(ManualLogSource log)
    {
        _log = log;
        _filePath = Path.Combine(Paths.ConfigPath, "UnturnedSingleplayerCheatMenu.favorites.json");
    }

    public int ItemCount => _itemKeys.Count;
    public int VehicleCount => _vehicleKeys.Count;
    public string FilePath => _filePath;

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
            string json = File.ReadAllText(_filePath);
            FavoriteAssetCollection collection = JsonConvert.DeserializeObject<FavoriteAssetCollection>(json);
            AddValidKeys(_itemKeys, collection?.ItemKeys);
            AddValidKeys(_vehicleKeys, collection?.VehicleKeys);
            _log.LogInfo($"已读取收藏：物品 {_itemKeys.Count}，车辆 {_vehicleKeys.Count}。");
        }
        catch (Exception ex)
        {
            _log.LogError($"读取收藏失败，已保留原文件：{_filePath}\n{ex}");
        }
    }

    public bool IsItemFavorite(ItemAsset asset) => asset != null && _itemKeys.Contains(GetAssetKey(asset));
    public bool IsVehicleFavorite(VehicleAsset asset) => asset != null && _vehicleKeys.Contains(GetAssetKey(asset));

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
            return false;

        string key = GetAssetKey(asset);
        bool wasFavorite = target.Contains(key);
        if (wasFavorite == favorite)
            return true;

        if (favorite)
            target.Add(key);
        else
            target.Remove(key);

        if (Save())
        {
            _log.LogInfo(
                $"{(favorite ? "已收藏" : "已取消收藏")}{kind}：{asset.FriendlyName}；键={key}。");
            return true;
        }

        if (wasFavorite)
            target.Add(key);
        else
            target.Remove(key);
        return false;
    }

    private bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? Paths.ConfigPath);
            FavoriteAssetCollection collection = new()
            {
                ItemKeys = _itemKeys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                VehicleKeys = _vehicleKeys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            };
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(_filePath, json);
            _log.LogInfo($"已保存收藏：物品 {_itemKeys.Count}，车辆 {_vehicleKeys.Count}。");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"保存收藏失败：{_filePath}\n{ex}");
            return false;
        }
    }

    private static void AddValidKeys(HashSet<string> target, IEnumerable<string> values)
    {
        if (values == null)
            return;

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target.Add(value.Trim());
        }
    }

    private static string GetAssetKey(Asset asset)
    {
        if (asset.GUID != Guid.Empty)
            return $"guid:{asset.GUID:D}";

        string origin = asset.GetOriginName() ?? string.Empty;
        return $"legacy:{asset.GetType().FullName}:{asset.id}:{origin}:{asset.name}";
    }
}
