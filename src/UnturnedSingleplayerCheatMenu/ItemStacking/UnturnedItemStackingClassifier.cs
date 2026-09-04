using System;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal static class UnturnedItemStackingClassifier
{
	internal static ItemStackingKind Classify(ItemAsset asset, Item item, bool isDirectAmmoSource,
		bool isAmmoContainerTarget)
	{
		if (asset == null) throw new ArgumentNullException(nameof(asset));
		return ItemStackingClassifier.Classify(new ItemStackingFacts(
			hasProvenDivisibleSupplyAmount: HasProvenDivisibleSupplyAmount(asset, item),
			isDirectAmmoSource: isDirectAmmoSource,
			isAmmoContainerTarget: isAmmoContainerTarget || asset is ItemMagazineAsset,
			isKnownStatefulContainer: IsKnownStatefulContainer(asset),
			maxAmount: asset.MaxAmount,
			hasState: item?.state != null && item.state.Length > 0,
			showsQuality: asset.showQuality));
	}

	// ItemSupplyAsset is Unturned's crafting-ingredient marker. Combined with capacity, delete-at-zero,
	// and an actually empty instance state it proves that amount can be conserved as divisible units.
	internal static bool HasProvenDivisibleSupplyAmount(ItemAsset asset, Item item)
	{
		return asset is ItemSupplyAsset && asset.MaxAmount > 1 && asset.ShouldDeleteAtZeroAmount &&
			item?.amount > 0 && item.amount <= asset.MaxAmount && item.state != null && item.state.Length == 0;
	}

	internal static bool IsKnownStatefulContainer(ItemAsset asset)
	{
		return asset is ItemFuelAsset || asset is ItemTankAsset || asset is ItemGeneratorAsset ||
			asset is ItemStorageAsset || asset is ItemLibraryAsset || asset is ItemRefillAsset;
	}
}
