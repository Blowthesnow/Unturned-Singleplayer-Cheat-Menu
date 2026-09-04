namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal enum ItemStackingKind
{
	IndividualInstance,
	FungibleUnits,
	AmmoContainer,
	StatefulContainer,
	HomogeneousInstances
}

internal readonly struct ItemStackingFacts
{
	internal ItemStackingFacts(bool hasProvenDivisibleSupplyAmount, bool isDirectAmmoSource,
		bool isAmmoContainerTarget,
		bool isKnownStatefulContainer, int maxAmount, bool hasState, bool showsQuality)
	{
		HasProvenDivisibleSupplyAmount = hasProvenDivisibleSupplyAmount;
		IsDirectAmmoSource = isDirectAmmoSource;
		IsAmmoContainerTarget = isAmmoContainerTarget;
		IsKnownStatefulContainer = isKnownStatefulContainer;
		MaxAmount = maxAmount;
		HasState = hasState;
		ShowsQuality = showsQuality;
	}

	internal bool HasProvenDivisibleSupplyAmount { get; }
	internal bool IsDirectAmmoSource { get; }
	internal bool IsAmmoContainerTarget { get; }
	internal bool IsKnownStatefulContainer { get; }
	internal int MaxAmount { get; }
	internal bool HasState { get; }
	internal bool ShowsQuality { get; }
}

internal static class ItemStackingClassifier
{
	internal static ItemStackingKind Classify(ItemStackingFacts facts)
	{
		if (facts.IsKnownStatefulContainer)
		{
			return ItemStackingKind.StatefulContainer;
		}
		if (facts.IsAmmoContainerTarget)
		{
			return ItemStackingKind.AmmoContainer;
		}
		if (facts.HasProvenDivisibleSupplyAmount)
		{
			return ItemStackingKind.FungibleUnits;
		}
		if (facts.IsDirectAmmoSource)
		{
			return ItemStackingKind.AmmoContainer;
		}
		if (facts.MaxAmount == 1 && !facts.HasState && !facts.ShowsQuality)
		{
			return ItemStackingKind.HomogeneousInstances;
		}
		return ItemStackingKind.IndividualInstance;
	}
}
