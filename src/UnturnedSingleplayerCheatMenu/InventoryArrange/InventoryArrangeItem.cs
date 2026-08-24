using System;

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

	internal int MaxAmount;

	internal int Quality;

	internal int RarityRank;

	internal int AmmoCount = -1;

	internal int SourcePage;

	internal int SourceX;

	internal int SourceY;

	internal bool IsMagazine;

	internal bool IsAmmoSource;

	internal int AmmoCapacity = -1;

	internal string AmmoGroup;

	internal int SourceRotation;

	internal int OriginalIndex;

	internal bool IsRotatable = true;

	internal bool IsProtected;

	internal bool IsHotkeyBound;

	internal bool IsAmmoContainer
	{
		get
		{
			if (!IsMagazine)
			{
				return IsAmmoSource;
			}
			return true;
		}
	}

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
			MaxAmount = MaxAmount,
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
			AmmoCapacity = AmmoCapacity,
			AmmoGroup = AmmoGroup,
			IsProtected = IsProtected,
			IsHotkeyBound = IsHotkeyBound
		};
	}

	internal bool CanMergeWith(InventoryArrangeItem other)
	{
		if (other != null && !IsProtected && !other.IsProtected && MaxAmount > 1 && other.MaxAmount == MaxAmount && Quality == other.Quality && string.Equals(AssetGuid, other.AssetGuid, StringComparison.Ordinal))
		{
			return string.Equals(StateHash, other.StateHash, StringComparison.Ordinal);
		}
		return false;
	}
}
