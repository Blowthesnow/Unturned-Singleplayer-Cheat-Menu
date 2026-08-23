using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal enum InventoryArrangeAction
{
    Arrange,
    Compact,
    Merge
}

internal enum InventoryArrangeScope
{
    CurrentContext,
    AllCarried
}

internal enum InventorySortPreset
{
    Compatibility,
    Compact,
    Type,
    Name,
    LargeFirst,
    SmallFirst,
    Rarity,
    Quality,
    StackFill,
    Ammo,
    Hotkey,
    LowMovement,
    Custom
}

internal enum InventorySortKey
{
    Type,
    Name,
    Area,
    Width,
    Height,
    LegacyId,
    Guid,
    Quality,
    StackFill,
    Hotkey,
    OriginalPosition,
    Rarity,
    Ammo
}

internal enum InventorySortDirection
{
    Ascending,
    Descending
}

internal sealed class InventorySortKeySpec
{
    internal InventorySortKey Key;
    internal InventorySortDirection Direction;

    internal InventorySortKeySpec(InventorySortKey key, InventorySortDirection direction)
    {
        Key = key;
        Direction = direction;
    }
}

internal sealed class InventorySortProfile
{
    internal InventorySortPreset Preset;
    internal string DisplayName;
    internal List<InventorySortKeySpec> Keys = new();

    internal InventorySortProfile(InventorySortPreset preset, string displayName, params InventorySortKeySpec[] keys)
    {
        Preset = preset;
        DisplayName = displayName;
        Keys.AddRange(keys);
    }
}

internal sealed class InventoryArrangeSettings
{
    internal InventoryArrangeAction Action = InventoryArrangeAction.Arrange;
    internal InventorySortPreset Preset = InventorySortPreset.Compatibility;
    internal bool AutoRotate = true;
    internal bool AllowCrossPage = true;
    internal bool AutoRefillMagazines = true;
    internal List<int> PreferredPages = new();
    internal bool ProtectEquipped = true;
    internal List<InventorySortKeySpec> CustomKeys = new();
    internal bool PreserveHotkeys = true;
}

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
    internal bool IsAmmoContainer => IsMagazine || IsAmmoSource;
    internal int AmmoCapacity = -1;
    internal string AmmoGroup;
    internal int SourceRotation;
    internal int OriginalIndex;
    internal bool IsRotatable = true;
    internal bool IsProtected;
    internal bool IsHotkeyBound;

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
            AmmoCount = IsAmmoContainer ? amount : AmmoCount,
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
        return other != null
            && !IsProtected
            && !other.IsProtected
            && MaxAmount > 1
            && other.MaxAmount == MaxAmount
            && Quality == other.Quality
            && string.Equals(AssetGuid, other.AssetGuid, StringComparison.Ordinal)
            && string.Equals(StateHash, other.StateHash, StringComparison.Ordinal);
    }
}

internal sealed class InventoryArrangeContainer
{
    internal int Page;
    internal int Width;
    internal int Height;
    internal List<InventoryArrangeItem> Items = new();
}

internal sealed class InventoryArrangePlacement
{
    internal InventoryArrangeItem Item;
    internal int Page;
    internal int X;
    internal int Y;
    internal int Rotation;
}

internal sealed class InventoryArrangePlan
{
    internal bool IsSuccess;
    internal string FailureReason;
    internal List<InventoryArrangePlacement> Placements = new();
    internal int MoveCount;
    internal int MergeCount;
    internal int CrossPageCount;
    internal int InputItemCount;
    internal int OutputItemCount;
}
