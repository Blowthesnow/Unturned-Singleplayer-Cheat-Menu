using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class IconCache
{
	private sealed class PendingDiskLoad
	{
		internal VehicleAsset Asset { get; }

		internal VehicleThumbnailRenderSettings Settings { get; }

		internal string CacheKey { get; }

		internal PendingDiskLoad(VehicleAsset asset, VehicleThumbnailRenderSettings settings, string cacheKey)
		{
			Asset = asset;
			Settings = settings;
			CacheKey = cacheKey;
		}
	}

	private readonly VehicleIconRenderer _vehicleIconRenderer;

	private readonly VehicleIconDiskCache _vehicleIconDiskCache;

	private readonly Func<VehicleThumbnailRenderSettings> _vehicleSettingsProvider;

	private readonly Dictionary<Guid, Texture2D> _itemIcons = new Dictionary<Guid, Texture2D>();

	private readonly Dictionary<string, Texture2D> _vehicleIcons = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

	private readonly HashSet<Guid> _pendingItems = new HashSet<Guid>();

	private readonly HashSet<Guid> _pendingVehicles = new HashSet<Guid>();

	private readonly Queue<PendingDiskLoad> _pendingDiskLoads = new Queue<PendingDiskLoad>();

	internal IconCache(VehicleIconRenderer vehicleIconRenderer, VehicleIconDiskCache vehicleIconDiskCache, Func<VehicleThumbnailRenderSettings> vehicleSettingsProvider)
	{
		_vehicleIconRenderer = vehicleIconRenderer;
		_vehicleIconDiskCache = vehicleIconDiskCache;
		_vehicleSettingsProvider = vehicleSettingsProvider;
	}

	public Texture2D GetItemIcon(ItemAsset asset)
	{
		if (asset == null)
		{
			return null;
		}
		if (_itemIcons.TryGetValue(asset.GUID, out var value))
		{
			return value;
		}
		if (!_pendingItems.Add(asset.GUID))
		{
			return null;
		}
		Item item = new Item(asset, EItemOrigin.ADMIN);
		ItemTool.getIcon(asset.id, item.quality, item.state, asset, 96, 96, delegate(int _, Texture2D icon)
		{
			_pendingItems.Remove(asset.GUID);
			if (icon != null)
			{
				_itemIcons[asset.GUID] = icon;
			}
		});
		return null;
	}

	public Texture2D GetVehicleIcon(VehicleAsset asset)
	{
		if (asset == null)
		{
			return null;
		}
		VehicleThumbnailRenderSettings settings = _vehicleSettingsProvider?.Invoke() ?? VehicleThumbnailRenderSettings.Normalize(128, 1f);
		string path = _vehicleIconDiskCache.GetPath(asset.GUID, settings);
		if (_vehicleIcons.TryGetValue(path, out var value))
		{
			return value;
		}
		if (!_pendingVehicles.Add(asset.GUID))
		{
			return null;
		}
		if (_vehicleIconDiskCache.HasEntry(asset.GUID, settings))
		{
			_pendingDiskLoads.Enqueue(new PendingDiskLoad(asset, settings, path));
			return null;
		}
		EnqueueVehicleRender(asset, settings, path);
		return null;
	}

	internal bool PumpOne()
	{
		if (_pendingDiskLoads.Count == 0)
		{
			return false;
		}
		PendingDiskLoad pendingDiskLoad = _pendingDiskLoads.Dequeue();
		Texture2D texture2D = _vehicleIconDiskCache.TryLoad(pendingDiskLoad.Asset.GUID, pendingDiskLoad.Settings);
		if (texture2D != null)
		{
			_vehicleIcons[pendingDiskLoad.CacheKey] = texture2D;
			_pendingVehicles.Remove(pendingDiskLoad.Asset.GUID);
		}
		else
		{
			EnqueueVehicleRender(pendingDiskLoad.Asset, pendingDiskLoad.Settings, pendingDiskLoad.CacheKey);
		}
		return true;
	}

	internal void ClearVehicleMemory()
	{
		_vehicleIconRenderer.CancelPending();
		_pendingDiskLoads.Clear();
		foreach (Texture2D value in _vehicleIcons.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		_vehicleIcons.Clear();
		_pendingVehicles.Clear();
	}

	public void Clear()
	{
		ClearVehicleMemory();
		foreach (Texture2D value in _itemIcons.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		_itemIcons.Clear();
		_pendingItems.Clear();
	}

	private void EnqueueVehicleRender(VehicleAsset asset, VehicleThumbnailRenderSettings settings, string cacheKey)
	{
		if (!_vehicleIconRenderer.TryEnqueue(asset, settings, delegate(Texture2D icon)
		{
			_pendingVehicles.Remove(asset.GUID);
			if (!(icon == null))
			{
				_vehicleIcons[cacheKey] = icon;
				if (_vehicleIconDiskCache.TrySave(asset.GUID, settings, icon, out var failure))
				{
					CheatMenuPlugin.Instance?.LogVehicleIconCached(asset, settings, _vehicleIconDiskCache.GetPath(asset.GUID, settings));
				}
				else
				{
					CheatMenuPlugin.Instance?.LogVehicleIconCacheWriteFailure(asset, settings, failure);
				}
			}
		}))
		{
			_pendingVehicles.Remove(asset.GUID);
		}
	}
}
