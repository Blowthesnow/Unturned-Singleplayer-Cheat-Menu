using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal enum MinimapZoomDirection
{
	In,
	Out
}

internal static class MinimapZoomPolicy
{
	private const float ZoomInFactorPerDetent = 0.9f;
	private const float RadiusStepMeters = 5f;

	internal static float Apply(float visibleRadiusMeters, MinimapZoomDirection direction, float detents)
	{
		float radius = IsFinite(visibleRadiusMeters) ? visibleRadiusMeters : 100f;
		float steps = IsFinite(detents) ? Math.Max(0f, Math.Min(20f, Math.Abs(detents))) : 0f;
		if (steps > 0f)
		{
			double factor = direction == MinimapZoomDirection.In
				? Math.Pow(ZoomInFactorPerDetent, steps)
				: Math.Pow(1d / ZoomInFactorPerDetent, steps);
			radius = (float)(radius * factor);
		}
		float snapped = (float)(Math.Round(radius / RadiusStepMeters, MidpointRounding.AwayFromZero) * RadiusStepMeters);
		return Math.Max(MinimapSettings.MinVisibleRadiusMeters,
			Math.Min(MinimapSettings.MaxVisibleRadiusMeters, snapped));
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}
