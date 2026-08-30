using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class WindowGeometry
{
	internal static float ResolveScale(
		float requestedScale,
		Vector2 boundsSize,
		Vector2 minimumSize,
		float margin)
	{
		return UiLifecyclePolicy.ResolveResponsiveScale(
			requestedScale,
			boundsSize.x,
			boundsSize.y,
			minimumSize.x,
			minimumSize.y,
			margin);
	}

	internal static Vector2 ResolveSize(
		Vector2 boundsSize,
		float scale,
		Vector2 minimumSize,
		Vector2 preferredSize,
		float margin)
	{
		return new Vector2(
			UiLifecyclePolicy.ResolveResponsiveAxis(boundsSize.x, scale, minimumSize.x, preferredSize.x, margin),
			UiLifecyclePolicy.ResolveResponsiveAxis(boundsSize.y, scale, minimumSize.y, preferredSize.y, margin));
	}

	internal static Vector2 RestoreNormalized(Vector2 normalized, Vector2 boundsSize, Vector2 windowSize, float scale)
	{
		return new Vector2(
			UiLifecyclePolicy.RestoreAxis(normalized.x, boundsSize.x, windowSize.x, scale),
			UiLifecyclePolicy.RestoreAxis(normalized.y, boundsSize.y, windowSize.y, scale));
	}

	internal static Vector2 Normalize(Vector2 position, Vector2 boundsSize, Vector2 windowSize, float scale)
	{
		return new Vector2(
			UiLifecyclePolicy.NormalizeAxis(position.x, boundsSize.x, windowSize.x, scale),
			UiLifecyclePolicy.NormalizeAxis(position.y, boundsSize.y, windowSize.y, scale));
	}

	internal static Vector2 Clamp(Vector2 position, Vector2 boundsSize, Vector2 windowSize, float scale)
	{
		return new Vector2(
			UiLifecyclePolicy.ClampAxis(position.x, boundsSize.x, windowSize.x, scale),
			UiLifecyclePolicy.ClampAxis(position.y, boundsSize.y, windowSize.y, scale));
	}
}
