using System;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal sealed class InventoryArrangeItem
{
	internal string ItemKey;

	internal string AssetGuid;

	internal string StateHash;

	internal string Name;

	internal int TypeRank;

	internal int LegacyId;

	internal int Width;

	internal int Height;

	internal int Amount;

	internal int OriginalAmount = -1;

	internal int MaxAmount;

	internal ItemStackingKind StackingKind;

	internal int Quality;

	internal int RarityRank;

	internal int AmmoCount = -1;

	internal int SourcePage;

	internal int SourceX;

	internal int SourceY;

	internal bool IsMagazine;

	internal bool IsAmmoSource;

	internal bool IsAmmoTarget;

	internal int AmmoCapacity = -1;

	internal string[] AmmoDirectSourceAssetGuids = Array.Empty<string>();

	internal int SourceRotation;

	internal int OriginalIndex;

	internal bool IsRotatable = true;

	internal bool IsProtected;

	internal bool IsHotkeyBound;

	internal bool IsAmmoContainer => StackingKind == ItemStackingKind.AmmoContainer;

	internal bool IsFungible => StackingKind == ItemStackingKind.FungibleUnits;

	internal int Area => Width * Height;

	internal InventoryArrangeItem CloneWithAmount(int amount)
	{
		return new InventoryArrangeItem
		{
			ItemKey = ItemKey,
			AssetGuid = AssetGuid,
			StateHash = StateHash,
			Name = Name,
			TypeRank = TypeRank,
			LegacyId = LegacyId,
			Width = Width,
			Height = Height,
			Amount = amount,
			OriginalAmount = OriginalAmount,
			MaxAmount = MaxAmount,
			StackingKind = StackingKind,
			Quality = Quality,
			AmmoCount = (IsAmmoContainer ? amount : AmmoCount),
			SourcePage = SourcePage,
			SourceX = SourceX,
			RarityRank = RarityRank,
			SourceY = SourceY,
			SourceRotation = SourceRotation,
			OriginalIndex = OriginalIndex,
			IsRotatable = IsRotatable,
			IsMagazine = IsMagazine,
			IsAmmoSource = IsAmmoSource,
			IsAmmoTarget = IsAmmoTarget,
			AmmoCapacity = AmmoCapacity,
			AmmoDirectSourceAssetGuids = (string[])(AmmoDirectSourceAssetGuids?.Clone() ?? Array.Empty<string>()),
			IsProtected = IsProtected,
			IsHotkeyBound = IsHotkeyBound
		};
	}

	internal bool CanMergeWith(InventoryArrangeItem other)
	{
		if (other != null && IsFungible && other.IsFungible && !IsProtected && !other.IsProtected && !IsHotkeyBound &&
			!other.IsHotkeyBound && MaxAmount > 1 && other.MaxAmount == MaxAmount &&
			string.Equals(AssetGuid, other.AssetGuid, StringComparison.Ordinal))
		{
			return string.Equals(StateHash, other.StateHash, StringComparison.Ordinal);
		}
		return false;
	}
}
