using System;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal enum UiMotionEasingKind
{
	Linear,
	EaseOutCubic,
	EaseInOutCubic
}

internal static class UiMotionEasing
{
	internal static float Evaluate(UiMotionEasingKind easing, float value)
	{
		float t = Clamp01(value);
		return easing switch
		{
			UiMotionEasingKind.Linear => t,
			UiMotionEasingKind.EaseInOutCubic => t < 0.5f
				? 4f * t * t * t
				: 1f - (float)Math.Pow(-2f * t + 2f, 3d) * 0.5f,
			_ => 1f - (1f - t) * (1f - t) * (1f - t)
		};
	}

	private static float Clamp01(float value)
	{
		if (float.IsNaN(value) || value <= 0f) return 0f;
		if (float.IsInfinity(value) || value >= 1f) return 1f;
		return value;
	}
}
