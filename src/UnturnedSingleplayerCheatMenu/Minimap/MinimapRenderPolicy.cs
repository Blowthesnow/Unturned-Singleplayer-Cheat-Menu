using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal readonly struct MinimapRenderCenter
{
	public float X { get; }
	public float Z { get; }

	public MinimapRenderCenter(float x, float z)
	{
		X = x;
		Z = z;
	}
}

internal readonly struct MinimapNativeCropWindow
{
	internal MinimapNativeCropWindow(float uvX, float uvY, float uvWidth, float uvHeight,
		float viewportX, float viewportY, float viewportWidth, float viewportHeight,
		bool coversVisibleRadius)
	{
		UvX = uvX;
		UvY = uvY;
		UvWidth = uvWidth;
		UvHeight = uvHeight;
		ViewportX = viewportX;
		ViewportY = viewportY;
		ViewportWidth = viewportWidth;
		ViewportHeight = viewportHeight;
		CoversVisibleRadius = coversVisibleRadius;
	}

	internal float UvX { get; }
	internal float UvY { get; }
	internal float UvWidth { get; }
	internal float UvHeight { get; }
	internal float ViewportX { get; }
	internal float ViewportY { get; }
	internal float ViewportWidth { get; }
	internal float ViewportHeight { get; }
	internal bool CoversVisibleRadius { get; }
	internal bool HasVisibleTexture => UvWidth > 0f && UvHeight > 0f &&
		ViewportWidth > 0f && ViewportHeight > 0f;
}

internal enum MinimapSceneCameraPass
{
	Terrain,
	Objects
}

internal readonly struct MinimapCameraDepthProfile
{
	internal MinimapCameraDepthProfile(float positionY, float nearClip, float farClip)
	{
		PositionY = positionY;
		NearClip = nearClip;
		FarClip = farClip;
	}

	internal float PositionY { get; }
	internal float NearClip { get; }
	internal float FarClip { get; }
}

internal static class MinimapRenderPolicy
{
	private const float CutawayClearanceAboveHead = 0.5f;
	private const float CutawayClearanceBelowCeiling = 0.1f;

	internal static float ResolveCutawaySliceAbove(float configuredAbove, float playerY, float headY,
		float detectedCeilingY = float.NaN)
	{
		float configured = IsFinite(configuredAbove)
			? Math.Max(MinimapSettings.MinUndergroundSlice, configuredAbove)
			: MinimapSettings.DefaultUndergroundSliceAbove;
		if (IsFinite(playerY) && IsFinite(detectedCeilingY) &&
			detectedCeilingY > playerY + CutawayClearanceBelowCeiling)
		{
			// Convert the room's absolute roof plane to this frame's camera offset.
			// The final camera Y remains ceilingY - clearance as the player moves vertically.
			return Math.Max(0.05f,
				detectedCeilingY - playerY - CutawayClearanceBelowCeiling);
		}
		if (!IsFinite(playerY) || !IsFinite(headY) || headY <= playerY) return configured;
		float headCutaway = Math.Max(MinimapSettings.MinUndergroundSlice,
			headY - playerY + CutawayClearanceAboveHead);
		return Math.Min(configured, headCutaway);
	}

	internal static MinimapCameraDepthProfile ResolveCameraDepth(
		MinimapSceneCameraPass pass, MinimapBackgroundState state, float playerY,
		float sliceAbove, float sliceBelow)
	{
		if (pass == MinimapSceneCameraPass.Terrain)
			return new MinimapCameraDepthProfile(2000f, 0.1f, 2200f);

		float centerY = IsFinite(playerY) ? playerY : 0f;
		if (state == MinimapBackgroundState.RealtimeHeightSlice)
		{
			float above = IsFinite(sliceAbove) ? Math.Max(0.05f, sliceAbove) :
				MinimapSettings.DefaultUndergroundSliceAbove;
			float below = IsFinite(sliceBelow) ? Math.Max(0.05f, sliceBelow) :
				MinimapSettings.DefaultUndergroundSliceBelow;
			return new MinimapCameraDepthProfile(centerY + above, 0.05f,
				Math.Max(0.1f, above + below));
		}
		return new MinimapCameraDepthProfile(centerY + 800f, 0.1f, 1000f);
	}

	internal static bool CanUseNativeMap(bool textureAvailable, bool coversVisibleRadius)
	{
		_ = coversVisibleRadius;
		return textureAvailable;
	}

	internal static MinimapNativeCropWindow ResolveNativeCrop(float centerX, float centerY,
		float halfWidth, float halfHeight)
	{
		if (!IsFinite(centerX) || !IsFinite(centerY) || !IsFinite(halfWidth) ||
			!IsFinite(halfHeight) || halfWidth <= 0f || halfHeight <= 0f)
			return new MinimapNativeCropWindow(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);

		float desiredXMin = centerX - halfWidth;
		float desiredYMin = centerY - halfHeight;
		float desiredWidth = halfWidth * 2f;
		float desiredHeight = halfHeight * 2f;
		float uvXMin = Clamp01(desiredXMin);
		float uvYMin = Clamp01(desiredYMin);
		float uvXMax = Clamp01(centerX + halfWidth);
		float uvYMax = Clamp01(centerY + halfHeight);
		float uvWidth = Math.Max(0f, uvXMax - uvXMin);
		float uvHeight = Math.Max(0f, uvYMax - uvYMin);
		bool covers = desiredXMin >= 0f && desiredYMin >= 0f &&
			centerX + halfWidth <= 1f && centerY + halfHeight <= 1f;
		if (uvWidth <= 0f || uvHeight <= 0f)
			return new MinimapNativeCropWindow(uvXMin, uvYMin, 0f, 0f,
				0f, 0f, 0f, 0f, covers);

		return new MinimapNativeCropWindow(uvXMin, uvYMin, uvWidth, uvHeight,
			(uvXMin - desiredXMin) / desiredWidth,
			(uvYMin - desiredYMin) / desiredHeight,
			uvWidth / desiredWidth, uvHeight / desiredHeight, covers);
	}

	public static bool UsesSceneCameras(MinimapBackgroundState state)
	{
		return state == MinimapBackgroundState.RealtimeSurface ||
			state == MinimapBackgroundState.RealtimeHeightSlice;
	}

	public static bool ShowsLoadedObjects(MinimapBackgroundState state, bool enabled)
	{
		return enabled && UsesSceneCameras(state);
	}

	internal static bool ShowsNativeUnderlay(MinimapBackgroundState state, bool nativeAvailable)
	{
		return nativeAvailable && UsesSceneCameras(state);
	}

	internal static bool UsesTransparentTerrainClear(MinimapBackgroundState state)
	{
		return UsesSceneCameras(state);
	}

	public static bool ShouldRender(bool needsCamera, bool stateChanged, float now, float nextRenderTime)
	{
		return needsCamera && (stateChanged || now >= nextRenderTime);
	}

	public static MinimapRenderCenter SnapCenter(float worldX, float worldZ, float mapYawDegrees,
		float visibleRadiusMeters, int resolution)
	{
		float worldUnitsPerTexel = Math.Max(0.0001f,
			Math.Max(0.001f, visibleRadiusMeters) * 2f / Math.Max(1, resolution));
		float radians = mapYawDegrees * (float)(Math.PI / 180d);
		float cosine = (float)Math.Cos(radians);
		float sine = (float)Math.Sin(radians);
		float localX = cosine * worldX - sine * worldZ;
		float localZ = sine * worldX + cosine * worldZ;
		localX = (float)Math.Round(localX / worldUnitsPerTexel) * worldUnitsPerTexel;
		localZ = (float)Math.Round(localZ / worldUnitsPerTexel) * worldUnitsPerTexel;
		return new MinimapRenderCenter(
			cosine * localX + sine * localZ,
			-sine * localX + cosine * localZ);
	}

	private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
	private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
