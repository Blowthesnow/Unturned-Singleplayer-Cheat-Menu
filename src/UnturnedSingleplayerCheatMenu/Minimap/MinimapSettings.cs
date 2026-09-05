using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapSettings
{
	internal const float MinSize = 160f;
	internal const float MaxSize = 520f;
	internal const float MinVisibleRadiusMeters = 20f;
	internal const float MaxVisibleRadiusMeters = 500f;
	internal const float MinRenderHz = 2f;
	internal const float MaxRenderHz = 10f;
	internal const float MinScanHz = 2f;
	internal const float MaxScanHz = 10f;
	internal const float MinProbeHz = 0.5f;
	internal const float MaxProbeHz = 10f;
	internal const float MinItemMergePixels = 2f;
	internal const float MaxItemMergePixels = 32f;
	internal const float MinUndergroundSlice = 0.5f;
	internal const float MaxUndergroundSliceAbove = 50f;
	internal const float MaxUndergroundSliceBelow = 2048f;
	internal const float MinSameFloorTolerance = 0.5f;
	internal const float MaxSameFloorTolerance = 50f;
	internal const float DefaultOcclusionEnterThickness = 2.5f;
	internal const float DefaultOcclusionExitThickness = 1.5f;
	internal const float DefaultFloorExclusionMaxThickness = 1.25f;
	internal const float DefaultUndergroundSliceAbove = 4f;
	internal const float DefaultUndergroundSliceBelow = 2048f;

	public bool Enabled { get; set; }
	public MinimapShape Shape { get; set; } = MinimapShape.Circle;
	public MinimapAnchor Anchor { get; set; } = MinimapAnchor.TopLeft;
	public float OffsetX { get; set; } = 20f;
	public float OffsetY { get; set; } = 20f;
	public float Size { get; set; } = 260f;
	public float VisibleRadiusMeters { get; set; } = 100f;
	public MinimapBackgroundMode BackgroundMode { get; set; } = MinimapBackgroundMode.AutoNativeFirst;
	public bool RotateWithView { get; set; }
	public float BackgroundRenderHz { get; set; } = 5f;
	public float WorldScanHz { get; set; } = 5f;
	public bool ShowLoadedBuildings { get; set; } = true;
	public float OcclusionProbeHz { get; set; } = 5f;
	public float OcclusionEnterThickness { get; set; } = DefaultOcclusionEnterThickness;
	public float OcclusionExitThickness { get; set; } = DefaultOcclusionExitThickness;
	public float OcclusionEnterDelaySeconds { get; set; } = 0.75f;
	public float OcclusionExitDelaySeconds { get; set; } = 0.25f;
	public float OcclusionMergeGap { get; set; } = 0.25f;
	public float FloorExclusionMaxThickness { get; set; } = DefaultFloorExclusionMaxThickness;
	public bool ShowCompass { get; set; } = true;
	public bool ShowCoordinates { get; set; } = true;
	public bool ShowNativeMapMarkers { get; set; } = true;
	public bool ShowTeleportMarkers { get; set; } = true;
	public bool ShowZombies { get; set; } = true;
	public bool ShowAnimals { get; set; } = true;
	public bool ShowPlayers { get; set; } = true;
	public bool ShowItems { get; set; }
	public ItemRarityMask ItemRarityMask { get; set; } = ItemRarityMask.All;
	public float ItemMergePixels { get; set; } = 8f;
	public float UndergroundSliceAbove { get; set; } = DefaultUndergroundSliceAbove;
	public float UndergroundSliceBelow { get; set; } = DefaultUndergroundSliceBelow;
	public float SameFloorTolerance { get; set; } = 6f;
	public bool ShowOtherFloors { get; set; } = true;
	public bool LayoutEditMode { get; set; }

	public MinimapSettings Normalize()
	{
		OffsetX = IsFinite(OffsetX) ? Math.Max(0f, OffsetX) : 0f;
		OffsetY = IsFinite(OffsetY) ? Math.Max(0f, OffsetY) : 0f;
		Size = Clamp(Size, MinSize, MaxSize);
		VisibleRadiusMeters = Clamp(VisibleRadiusMeters, MinVisibleRadiusMeters, MaxVisibleRadiusMeters);
		BackgroundRenderHz = Clamp(BackgroundRenderHz, MinRenderHz, MaxRenderHz);
		WorldScanHz = Clamp(WorldScanHz, MinScanHz, MaxScanHz);
		OcclusionProbeHz = Clamp(OcclusionProbeHz, MinProbeHz, MaxProbeHz);
		OcclusionEnterThickness = Clamp(OcclusionEnterThickness, 0.25f, 50f);
		OcclusionExitThickness = Clamp(OcclusionExitThickness, 0.1f, 50f);
		FloorExclusionMaxThickness = Clamp(FloorExclusionMaxThickness, 0f, 25f);
		if (FloorExclusionMaxThickness > OcclusionExitThickness || OcclusionExitThickness >= OcclusionEnterThickness)
		{
			FloorExclusionMaxThickness = DefaultFloorExclusionMaxThickness;
			OcclusionExitThickness = DefaultOcclusionExitThickness;
			OcclusionEnterThickness = DefaultOcclusionEnterThickness;
		}
		OcclusionEnterDelaySeconds = Clamp(OcclusionEnterDelaySeconds, 0f, 10f);
		OcclusionExitDelaySeconds = Clamp(OcclusionExitDelaySeconds, 0f, 10f);
		OcclusionMergeGap = Clamp(OcclusionMergeGap, 0f, 5f);
		ItemMergePixels = Clamp(ItemMergePixels, MinItemMergePixels, MaxItemMergePixels);
		UndergroundSliceAbove = ClampOrDefault(UndergroundSliceAbove, MinUndergroundSlice, MaxUndergroundSliceAbove, DefaultUndergroundSliceAbove);
		UndergroundSliceBelow = ClampOrDefault(UndergroundSliceBelow, MinUndergroundSlice, MaxUndergroundSliceBelow, DefaultUndergroundSliceBelow);
		SameFloorTolerance = Clamp(SameFloorTolerance, MinSameFloorTolerance, MaxSameFloorTolerance);
		ItemRarityMask &= ItemRarityMask.All;
		return this;
	}

	public MinimapSettings Clone()
	{
		return (MinimapSettings)MemberwiseClone();
	}

	private static float Clamp(float value, float min, float max)
	{
		if (!IsFinite(value)) return min;
		return Math.Max(min, Math.Min(max, value));
	}

	private static float ClampOrDefault(float value, float min, float max, float fallback)
	{
		if (!IsFinite(value)) return fallback;
		return Math.Max(min, Math.Min(max, value));
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
