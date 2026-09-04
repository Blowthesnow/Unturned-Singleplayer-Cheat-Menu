using System;

namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal sealed class AmmoFillBlueprintFacts
{
	internal bool IsFillTargetItem;
	internal int SupplyCount;
	internal bool HasPrimarySupply;
	internal bool PrimaryShouldConsume;
	internal bool PrimaryCountsTotalAmount;
	internal bool PrimaryCountEmptyAsOne;
	internal bool PrimaryIncludesMaxQuality;
	internal bool PrimaryExcludesFullAmount;
	internal int PrimaryAmount;
	internal int OutputCount;
	internal string OwnerAssetGuid;
	internal bool HasExplicitTarget;
	internal string ExplicitTargetAssetGuid;
	internal string SourceAssetGuid;
	internal bool SourceIsSupportedContainer;
	internal bool TargetIsSupportedContainer;
	internal int SourceCapacity;
	internal int TargetCapacity;
}

internal static class AmmoFillBlueprintPolicy
{
	internal static bool TryGetDirectEdge(AmmoFillBlueprintFacts facts,
		out string sourceAssetGuid, out string targetAssetGuid)
	{
		sourceAssetGuid = string.Empty;
		targetAssetGuid = string.Empty;
		if (facts == null || !facts.IsFillTargetItem || facts.SupplyCount != 1 ||
			!facts.HasPrimarySupply || !facts.PrimaryShouldConsume || !facts.PrimaryCountsTotalAmount ||
			facts.PrimaryCountEmptyAsOne || !facts.PrimaryIncludesMaxQuality || facts.PrimaryExcludesFullAmount ||
			facts.PrimaryAmount <= 0 || facts.OutputCount > 0 || !facts.SourceIsSupportedContainer ||
			!facts.TargetIsSupportedContainer ||
			!AmmoRefillIdentity.TryGetValidCapacity(facts.SourceCapacity, out _) ||
			!AmmoRefillIdentity.TryGetValidCapacity(facts.TargetCapacity, out _))
		{
			return false;
		}

		sourceAssetGuid = AmmoRefillIdentity.NormalizeAssetGuid(facts.SourceAssetGuid);
		targetAssetGuid = AmmoRefillIdentity.NormalizeAssetGuid(
			facts.HasExplicitTarget ? facts.ExplicitTargetAssetGuid : facts.OwnerAssetGuid);
		if (string.IsNullOrEmpty(sourceAssetGuid) || string.IsNullOrEmpty(targetAssetGuid))
		{
			sourceAssetGuid = string.Empty;
			targetAssetGuid = string.Empty;
			return false;
		}
		return true;
	}
}
