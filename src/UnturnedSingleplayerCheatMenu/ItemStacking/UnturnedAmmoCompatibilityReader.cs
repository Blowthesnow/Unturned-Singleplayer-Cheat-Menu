using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal static class UnturnedAmmoCompatibilityReader
{
	internal static AmmoCompatibilityCatalog BuildCatalog(IEnumerable<ItemAsset> assets)
	{
		List<ItemAsset> items = (assets ?? Enumerable.Empty<ItemAsset>()).Where(asset => asset != null).ToList();
		AmmoCompatibilityGraph graph = new AmmoCompatibilityGraph();
		foreach (ItemMagazineAsset magazine in items.OfType<ItemMagazineAsset>())
		{
			if (AmmoRefillIdentity.TryGetValidCapacity(magazine.MaxAmount, out _))
				graph.AddContainer(magazine.GUID.ToString("N"));
		}
		foreach (ItemAsset owner in items)
		{
			foreach (Blueprint blueprint in owner.blueprints ?? new List<Blueprint>())
			{
				if (!TryReadDirectOneToOneTransfer(owner, blueprint, out string sourceGuid, out string targetGuid)) continue;
				graph.AddDirectTransfer(sourceGuid, targetGuid);
			}
		}
		return graph.Build();
	}

	private static bool TryReadDirectOneToOneTransfer(ItemAsset owner, Blueprint blueprint,
		out string sourceAssetGuid, out string targetAssetGuid)
	{
		BlueprintSupply[] supplies = blueprint?.supplies;
		BlueprintSupply primary = supplies != null && supplies.Length > 0 ? supplies[0] : null;
		bool hasExplicitTarget = blueprint?.TargetItem != null;
		ItemAsset source = primary?.FindItemAsset();
		ItemAsset target = hasExplicitTarget ? blueprint.TargetItem.FindItemAsset() : owner;
		AmmoFillBlueprintFacts facts = new AmmoFillBlueprintFacts
		{
			IsFillTargetItem = blueprint?.Operation == EBlueprintOperation.FillTargetItem,
			SupplyCount = supplies?.Length ?? 0,
			HasPrimarySupply = primary != null,
			PrimaryShouldConsume = primary?.ShouldConsume ?? false,
			PrimaryCountsTotalAmount = primary?.CountingMethod == ECraftingInputCountingMethod.TotalAmount,
			PrimaryCountEmptyAsOne = primary?.ShouldCountEmptyAsOne ?? false,
			PrimaryIncludesMaxQuality = primary?.ShouldIncludeMaxQuality ?? false,
			PrimaryExcludesFullAmount = primary?.ShouldExcludeFullAmount ?? false,
			PrimaryAmount = primary?.amount ?? 0,
			OutputCount = blueprint?.outputs?.Length ?? 0,
			OwnerAssetGuid = owner?.GUID.ToString("N"),
			HasExplicitTarget = hasExplicitTarget,
			ExplicitTargetAssetGuid = hasExplicitTarget ? target?.GUID.ToString("N") : null,
			SourceAssetGuid = source?.GUID.ToString("N"),
			SourceIsSupportedContainer = IsSupportedAmmoContainer(source),
			TargetIsSupportedContainer = IsSupportedAmmoContainer(target),
			SourceCapacity = source?.MaxAmount ?? 0,
			TargetCapacity = target?.MaxAmount ?? 0
		};
		return AmmoFillBlueprintPolicy.TryGetDirectEdge(facts, out sourceAssetGuid, out targetAssetGuid);
	}

	private static bool IsSupportedAmmoContainer(ItemAsset asset) => asset is ItemMagazineAsset || asset is ItemSupplyAsset;
}
