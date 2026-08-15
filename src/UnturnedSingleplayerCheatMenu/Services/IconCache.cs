using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class IconCache
{
    private readonly VehicleIconRenderer _vehicleIconRenderer;
    private readonly Dictionary<Guid, Texture2D> _itemIcons = new();
    private readonly Dictionary<Guid, Texture2D> _vehicleIcons = new();
    private readonly HashSet<Guid> _pendingItems = new();
    private readonly HashSet<Guid> _pendingVehicles = new();

    internal IconCache(VehicleIconRenderer vehicleIconRenderer)
    {
        _vehicleIconRenderer = vehicleIconRenderer;
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
        if (_vehicleIcons.TryGetValue(asset.GUID, out Texture2D texture))
            return texture;
        if (!_pendingVehicles.Add(asset.GUID))
            return null;

        if (!_vehicleIconRenderer.TryEnqueue(asset, icon =>
            {
                _pendingVehicles.Remove(asset.GUID);
                if (icon != null)
                {
                    _vehicleIcons[asset.GUID] = icon;
                    CheatMenuPlugin.Instance?.LogVehicleIconCached(asset, icon);
                }
            }))
        {
            _pendingVehicles.Remove(asset.GUID);
        }
        return null;
    }

    public void Clear()
    {
        _vehicleIconRenderer.CancelPending();

        // ItemTool.captureIcon and VehicleIconRenderer create textures for their
        // callbacks. Release them when rescanning to avoid leaking every preview.
        foreach (Texture2D texture in _itemIcons.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
        foreach (Texture2D texture in _vehicleIcons.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
        _itemIcons.Clear();
        _vehicleIcons.Clear();
        _pendingItems.Clear();
        _pendingVehicles.Clear();
    }
}
