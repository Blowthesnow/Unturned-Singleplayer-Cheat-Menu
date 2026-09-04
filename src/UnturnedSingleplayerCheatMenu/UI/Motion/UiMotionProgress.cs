using System;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal struct UiMotionProgress
{
	private float _duration;
	private float _elapsed;

	internal bool IsComplete { get; private set; }

	internal void Begin(float duration)
	{
		_duration = float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f ? 0f : duration;
		_elapsed = 0f;
		IsComplete = _duration <= 0f;
	}

	internal float Advance(float deltaTime)
	{
		if (IsComplete) return 1f;
		float delta = float.IsNaN(deltaTime) || deltaTime < 0f ? 0f : deltaTime;
		if (float.IsInfinity(delta)) delta = _duration;
		_elapsed += delta;
		float progress = Math.Min(1f, _elapsed / _duration);
		IsComplete = progress >= 1f;
		return progress;
	}
}
