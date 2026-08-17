using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class IconCache
{
    private readonly VehicleIconRenderer _vehicleIconRenderer;
    private readonly VehicleIconDiskCache _vehicleIconDiskCache;
    private readonly Func<VehicleThumbnailRenderSettings> _vehicleSettingsProvider;
    private readonly Dictionary<Guid, Texture2D> _itemIcons = new();
    private readonly Dictionary<string, Texture2D> _vehicleIcons = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _pendingItems = new();
    private readonly HashSet<Guid> _pendingVehicles = new();
    private readonly Queue<PendingDiskLoad> _pendingDiskLoads = new();

    internal IconCache(
        VehicleIconRenderer vehicleIconRenderer,
        VehicleIconDiskCache vehicleIconDiskCache,
        Func<VehicleThumbnailRenderSettings> vehicleSettingsProvider)
    {
        _vehicleIconRenderer = vehicleIconRenderer;
        _vehicleIconDiskCache = vehicleIconDiskCache;
        _vehicleSettingsProvider = vehicleSettingsProvider;
    }

    public Texture2D GetItemIcon(ItemAsset asset)
    {
        if (asset == null)
            return null;
        if (_itemIcons.TryGetValue(asset.GUID, out Texture2D texture))
            return texture;
        if (!_pendingItems.Add(asset.GUID))
            return null;

        Item preview = new(asset, EItemOrigin.ADMIN);
        ItemTool.getIcon(
            asset.id,
            preview.quality,
            preview.state,
            asset,
            96,
            96,
            (_, icon) =>
            {
                _pendingItems.Remove(asset.GUID);
                if (icon != null)
                    _itemIcons[asset.GUID] = icon;
            });
        return null;
    }

    public Texture2D GetVehicleIcon(VehicleAsset asset)
    {
        if (asset == null)
            return null;

        VehicleThumbnailRenderSettings settings = _vehicleSettingsProvider?.Invoke()
            ?? VehicleThumbnailRenderSettings.Normalize(
                VehicleThumbnailRenderSettings.DefaultWidth,
                VehicleThumbnailRenderSettings.DefaultFraming);
        string cacheKey = _vehicleIconDiskCache.GetPath(asset.GUID, settings);
        if (_vehicleIcons.TryGetValue(cacheKey, out Texture2D texture))
            return texture;
        if (!_pendingVehicles.Add(asset.GUID))
            return null;

        if (_vehicleIconDiskCache.HasEntry(asset.GUID, settings))
        {
            _pendingDiskLoads.Enqueue(new PendingDiskLoad(asset, settings, cacheKey));
            return null;
        }

        EnqueueVehicleRender(asset, settings, cacheKey);
        return null;
    }

    // Returns true when one cache/render item was consumed. The plugin uses this
    // to keep disk decoding and model capture inside the same one-item frame budget.
    internal bool PumpOne()
    {
        if (_pendingDiskLoads.Count == 0)
            return false;

        PendingDiskLoad request = _pendingDiskLoads.Dequeue();
        Texture2D texture = _vehicleIconDiskCache.TryLoad(request.Asset.GUID, request.Settings);
        if (texture != null)
        {
            _vehicleIcons[request.CacheKey] = texture;
            _pendingVehicles.Remove(request.Asset.GUID);
        }
        else
        {
            EnqueueVehicleRender(request.Asset, request.Settings, request.CacheKey);
        }

        return true;
    }

    internal void ClearVehicleMemory()
    {
        _vehicleIconRenderer.CancelPending();
        _pendingDiskLoads.Clear();

        foreach (Texture2D texture in _vehicleIcons.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _vehicleIcons.Clear();
        _pendingVehicles.Clear();
    }

    public void Clear()
    {
        ClearVehicleMemory();

        // ItemTool.captureIcon creates textures for its callbacks. Release them
        // when rescanning to avoid leaking every preview.
        foreach (Texture2D texture in _itemIcons.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _itemIcons.Clear();
        _pendingItems.Clear();
    }

    private void EnqueueVehicleRender(
        VehicleAsset asset,
        VehicleThumbnailRenderSettings settings,
        string cacheKey)
    {
        if (!_vehicleIconRenderer.TryEnqueue(asset, settings, icon =>
            {
                _pendingVehicles.Remove(asset.GUID);
                if (icon == null)
                    return;

                _vehicleIcons[cacheKey] = icon;
                if (_vehicleIconDiskCache.TrySave(asset.GUID, settings, icon, out string failure))
                {
                    CheatMenuPlugin.Instance?.LogVehicleIconCached(
                        asset,
                        settings,
                        _vehicleIconDiskCache.GetPath(asset.GUID, settings));
                }
                else
                {
                    CheatMenuPlugin.Instance?.LogVehicleIconCacheWriteFailure(
                        asset,
                        settings,
                        failure);
                }
            }))
        {
            _pendingVehicles.Remove(asset.GUID);
        }
    }

    private sealed class PendingDiskLoad
    {
        internal PendingDiskLoad(
            VehicleAsset asset,
            VehicleThumbnailRenderSettings settings,
            string cacheKey)
        {
            Asset = asset;
            Settings = settings;
            CacheKey = cacheKey;
        }

        internal VehicleAsset Asset { get; }
        internal VehicleThumbnailRenderSettings Settings { get; }
        internal string CacheKey { get; }
    }
}
