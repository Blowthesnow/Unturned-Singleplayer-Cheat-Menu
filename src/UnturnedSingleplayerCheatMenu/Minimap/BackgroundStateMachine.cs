using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class BackgroundStateMachine
{
	private MinimapBackgroundState _state = MinimapBackgroundState.NativeMapCrop;
	private float _enterCandidateSince = -1f;
	private float _exitCandidateSince = -1f;

	public MinimapBackgroundState State => _state;

	public MinimapBackgroundState Update(float now, MinimapBackgroundMode mode, bool nativeMapAvailable,
		bool realtimeAvailable, OcclusionSummary occlusion, float enterDelaySeconds, float exitDelaySeconds)
	{
		switch (mode)
		{
			case MinimapBackgroundMode.Disabled:
				return SetImmediate(MinimapBackgroundState.Disabled);
			case MinimapBackgroundMode.NativeMapOnly:
				return SetImmediate(nativeMapAvailable ? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.FallbackGrid);
			case MinimapBackgroundMode.RealtimeSurface:
				return SetImmediate(realtimeAvailable ? MinimapBackgroundState.RealtimeSurface :
					(nativeMapAvailable ? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.FallbackGrid));
			case MinimapBackgroundMode.RealtimeHeightSlice:
				return SetImmediate(realtimeAvailable ? MinimapBackgroundState.RealtimeHeightSlice :
					(nativeMapAvailable ? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.FallbackGrid));
		}

		if (!realtimeAvailable)
		{
			return SetImmediate(nativeMapAvailable ? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.FallbackGrid);
		}
		MinimapBackgroundState surfaceState = nativeMapAvailable
			? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.RealtimeSurface;

		if (_state == MinimapBackgroundState.RealtimeHeightSlice)
		{
			_enterCandidateSince = -1f;
			if (!occlusion.MeetsExitThreshold)
			{
				_exitCandidateSince = -1f;
				return _state;
			}
			if (_exitCandidateSince < 0f) _exitCandidateSince = now;
			if (Elapsed(now, _exitCandidateSince) >= Math.Max(0f, exitDelaySeconds))
			{
				return SetImmediate(surfaceState);
			}
			return _state;
		}

		_exitCandidateSince = -1f;
		if (!occlusion.MeetsEnterThreshold)
		{
			_enterCandidateSince = -1f;
			_state = surfaceState;
			return _state;
		}
		if (_enterCandidateSince < 0f) _enterCandidateSince = now;
		if (Elapsed(now, _enterCandidateSince) >= Math.Max(0f, enterDelaySeconds))
		{
			return SetImmediate(MinimapBackgroundState.RealtimeHeightSlice);
		}
		_state = surfaceState;
		return _state;
	}

	public void Reset()
	{
		_state = MinimapBackgroundState.NativeMapCrop;
		_enterCandidateSince = -1f;
		_exitCandidateSince = -1f;
	}

	private MinimapBackgroundState SetImmediate(MinimapBackgroundState state)
	{
		_state = state;
		_enterCandidateSince = -1f;
		_exitCandidateSince = -1f;
		return state;
	}

	private static float Elapsed(float now, float since)
	{
		return Math.Max(0f, now - since);
	}
}
