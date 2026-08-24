using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class MovementSpeedProfile
{
	internal const float BaseMetersPerSecond = 7f;

	internal const float MinimumMultiplier = 1f;

	internal const float MaximumMultiplier = 10f;

	internal static float NormalizeMultiplier(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			return 1f;
		}
		return Math.Max(1f, Math.Min(10f, value));
	}

	internal static float GetMetersPerSecond(float multiplier)
	{
		return 7f * NormalizeMultiplier(multiplier);
	}
}
