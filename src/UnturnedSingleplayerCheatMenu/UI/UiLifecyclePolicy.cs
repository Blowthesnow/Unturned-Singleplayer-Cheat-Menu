using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.UI;

internal enum UiInputOwnershipState
{
	Hidden,
	HudOnly,
	ModalPluginUi,
	NativeUiCoexistence
}

internal static class UiLifecyclePolicy
{
	internal static UiInputOwnershipState ResolveInputOwnership(bool pluginWindowOpen, bool nativeUiActive)
	{
		if (!pluginWindowOpen)
		{
			return UiInputOwnershipState.Hidden;
		}
		return nativeUiActive
			? UiInputOwnershipState.NativeUiCoexistence
			: UiInputOwnershipState.ModalPluginUi;
	}

	internal static T SelectBorrowed<T>(
		T current,
		T owned,
		IEnumerable<T> candidates,
		Func<T, bool> isUsable,
		Func<T, bool> isPreferred) where T : class
	{
		if (current != null && !ReferenceEquals(current, owned) && isUsable(current))
		{
			return current;
		}
		foreach (T candidate in candidates)
		{
			if (candidate != null && !ReferenceEquals(candidate, owned) && isUsable(candidate) && isPreferred(candidate))
			{
				return candidate;
			}
		}
		foreach (T candidate in candidates)
		{
			if (candidate != null && !ReferenceEquals(candidate, owned) && isUsable(candidate))
			{
				return candidate;
			}
		}
		return null;
	}

	internal static void ReplaceBorrowed<T>(
		ref T current,
		T next,
		Action<T> release,
		Action<T> capture) where T : class
	{
		if (ReferenceEquals(current, next))
		{
			return;
		}
		if (current != null)
		{
			release(current);
		}
		current = next;
		if (next != null)
		{
			capture(next);
		}
	}

	internal static float ClampAxis(float position, float boundsSize, float windowSize, float scale)
	{
		if (!IsFinite(position))
		{
			position = 0f;
		}
		float maximum = MaximumAxis(boundsSize, windowSize, scale);
		return Math.Max(-maximum, Math.Min(maximum, position));
	}

	internal static float NormalizeAxis(float position, float boundsSize, float windowSize, float scale)
	{
		float maximum = MaximumAxis(boundsSize, windowSize, scale);
		if (maximum <= 0f || !IsFinite(position))
		{
			return 0f;
		}
		return Math.Max(-1f, Math.Min(1f, position / maximum));
	}

	internal static float RestoreAxis(float normalized, float boundsSize, float windowSize, float scale)
	{
		if (!IsFinite(normalized))
		{
			normalized = 0f;
		}
		normalized = Math.Max(-1f, Math.Min(1f, normalized));
		return normalized * MaximumAxis(boundsSize, windowSize, scale);
	}

	internal static float ResolveResponsiveScale(
		float requestedScale,
		float boundsWidth,
		float boundsHeight,
		float minimumWidth,
		float minimumHeight,
		float margin)
	{
		requestedScale = IsFinite(requestedScale) ? Math.Max(0.75f, Math.Min(1.5f, requestedScale)) : 1f;
		if (!IsFinite(boundsWidth) || !IsFinite(boundsHeight) || boundsWidth <= 0f || boundsHeight <= 0f)
		{
			return requestedScale;
		}
		float availableWidth = Math.Max(0f, boundsWidth - margin * 2f);
		float availableHeight = Math.Max(0f, boundsHeight - margin * 2f);
		float fitScale = Math.Min(
			availableWidth / Math.Max(1f, minimumWidth),
			availableHeight / Math.Max(1f, minimumHeight));
		return Math.Max(0.5f, Math.Min(requestedScale, fitScale));
	}

	internal static float ResolveResponsiveAxis(
		float boundsSize,
		float scale,
		float minimumSize,
		float preferredSize,
		float margin)
	{
		if (!IsFinite(boundsSize) || boundsSize <= 0f)
		{
			return preferredSize;
		}
		scale = IsFinite(scale) && scale > 0f ? scale : 1f;
		float available = Math.Max(0f, boundsSize - margin * 2f) / scale;
		return Math.Max(minimumSize, Math.Min(preferredSize, available));
	}

	private static float MaximumAxis(float boundsSize, float windowSize, float scale)
	{
		boundsSize = IsFinite(boundsSize) && boundsSize > 0f ? boundsSize : 0f;
		windowSize = IsFinite(windowSize) && windowSize > 0f ? windowSize : 0f;
		scale = IsFinite(scale) && scale > 0f ? scale : 1f;
		return Math.Max(0f, (boundsSize - windowSize * scale) * 0.5f);
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}
}

internal sealed class IdempotentTeardownGate
{
	private bool _started;

	internal bool TryBegin()
	{
		if (_started)
		{
			return false;
		}
		_started = true;
		return true;
	}
}
