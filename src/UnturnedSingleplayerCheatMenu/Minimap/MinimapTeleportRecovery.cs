using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapTeleportRecovery
{
	private bool _hasPosition;
	private MinimapWorldPoint _previous;
	internal bool NeedsRealtimeDestination { get; private set; }

	// Movement discontinuity is only a cache-refresh signal, never a map-mode trigger.
	internal bool ObservePosition(MinimapWorldPoint position)
	{
		if (!Finite(position.X) || !Finite(position.Y) || !Finite(position.Z))
		{
			_hasPosition = false;
			return false;
		}
		bool jumped = false;
		if (_hasPosition)
		{
			double x = position.X - (double)_previous.X;
			double y = position.Y - (double)_previous.Y;
			double z = position.Z - (double)_previous.Z;
			jumped = x * x + y * y + z * z >= 64d * 64d;
		}
		_previous = position;
		_hasPosition = true;
		return jumped;
	}

	internal void ObserveDestination(bool nativeTeleport, bool hasVisibleNativeCrop, bool playerInsideNativeBounds)
	{
		if (nativeTeleport) NeedsRealtimeDestination = !hasVisibleNativeCrop;
		else if (playerInsideNativeBounds) NeedsRealtimeDestination = false;
	}

	internal void Reset()
	{
		_hasPosition = false;
		NeedsRealtimeDestination = false;
	}

	private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
