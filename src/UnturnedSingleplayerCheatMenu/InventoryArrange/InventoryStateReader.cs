using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryStateReader
{
	private static AmmoCompatibilityCatalog _ammoCatalog = AmmoCompatibilityCatalog.Empty;
	private static bool _ammoCatalogInitialized;

	internal static void RefreshAmmoCompatibility()
	{
		if (_ammoCatalogInitialized) return;
		List<ItemAsset> list = new List<ItemAsset>();
		Assets.find(list);
		_ammoCatalog = UnturnedAmmoCompatibilityReader.BuildCatalog(list);
		_ammoCatalogInitialized = true;
	}

	internal static void InvalidateAmmoCompatibility()
	{
		_ammoCatalog = AmmoCompatibilityCatalog.Empty;
		_ammoCatalogInitialized = false;
	}

	internal static bool TryReadAmmoCount(ItemAsset asset, Item item, out int ammoCount)
	{
		ammoCount = -1;
		if (asset is ItemMagazineAsset || IsMappedAmmoSupply(asset))
		{
			ammoCount = item?.amount ?? 0;
			return item != null;
		}
		if (!(asset is ItemGunAsset) || item?.state == null || item.state.Length <= 10)
		{
			return false;
		}
		ammoCount = item.state[10];
		return true;
	}

	internal static bool TryReadAmmoContainerMetadata(ItemAsset asset, out int capacity, out AmmoRefillAssetRules rules)
	{
		capacity = -1;
		rules = null;
		if (asset == null || !_ammoCatalog.IsAmmoRelated(asset.GUID.ToString("N"))) return false;
		if (!AmmoRefillIdentity.TryGetValidCapacity(asset.MaxAmount, out capacity)) return false;
		rules = _ammoCatalog.GetRules(asset.GUID.ToString("N"), asset is ItemMagazineAsset);
		return true;
	}

	private static bool IsMappedAmmoSupply(ItemAsset asset)
	{
		if (asset is ItemSupplyAsset)
		{
			return _ammoCatalog.IsAmmoRelated(asset.GUID.ToString("N"));
		}
		return false;
	}

}
