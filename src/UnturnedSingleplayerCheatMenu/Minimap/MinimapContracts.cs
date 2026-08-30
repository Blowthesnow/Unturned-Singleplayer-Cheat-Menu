using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal enum MinimapShape
{
	Circle,
	Square
}

internal enum MinimapAnchor
{
	TopLeft,
	TopRight,
	BottomLeft,
	BottomRight
}

internal enum MinimapBackgroundMode
{
	AutoNativeFirst,
	NativeMapOnly,
	RealtimeSurface,
	RealtimeHeightSlice,
	Disabled
}

internal enum MinimapBackgroundState
{
	NativeMapCrop,
	RealtimeSurface,
	RealtimeHeightSlice,
	FallbackGrid,
	Disabled
}

[Flags]
internal enum ItemRarityMask
{
	None = 0,
	Common = 1 << 0,
	Uncommon = 1 << 1,
	Rare = 1 << 2,
	Epic = 1 << 3,
	Legendary = 1 << 4,
	Mythical = 1 << 5,
	All = Common | Uncommon | Rare | Epic | Legendary | Mythical
}
