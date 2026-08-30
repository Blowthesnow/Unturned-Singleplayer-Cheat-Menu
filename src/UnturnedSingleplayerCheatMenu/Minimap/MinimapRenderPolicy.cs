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

internal static class MinimapRenderPolicy
{
	public static bool UsesSceneCameras(MinimapBackgroundState state)
	{
		return state == MinimapBackgroundState.RealtimeSurface ||
			state == MinimapBackgroundState.RealtimeHeightSlice;
	}

	public static bool ShowsLoadedObjects(MinimapBackgroundState state, bool enabled)
	{
		return enabled && UsesSceneCameras(state);
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
}
