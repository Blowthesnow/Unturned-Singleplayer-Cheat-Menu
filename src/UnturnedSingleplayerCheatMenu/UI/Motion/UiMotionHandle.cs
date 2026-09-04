using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal sealed class UiMotionHandle : IDisposable
{
	private UiMotionProgress _progress;
	private UiMotionEasingKind _easing;
	private Action<float> _apply;
	private readonly UiMotionCompletion _completion = new UiMotionCompletion();

	internal UiMotionHandle(GameObject owner, UiMotionChannel channel)
	{
		Owner = owner;
		Channel = channel;
	}

	internal GameObject Owner { get; private set; }
	internal UiMotionChannel Channel { get; }
	internal bool IsActive { get; private set; }

	internal void Retarget(float duration, UiMotionEasingKind easing, Action<float> apply, Action completed)
	{
		_progress.Begin(duration);
		_easing = easing;
		_apply = apply ?? throw new ArgumentNullException(nameof(apply));
		_completion.Reset(completed);
		IsActive = true;
		_apply(0f);
		if (_progress.IsComplete)
		{
			SnapToEnd();
		}
	}

	internal bool Advance(float unscaledDeltaTime)
	{
		if (!IsActive)
		{
			return true;
		}
		if (Owner == null || !Owner.activeInHierarchy)
		{
			Cancel();
			return true;
		}

		float raw = _progress.Advance(unscaledDeltaTime);
		_apply?.Invoke(UiMotionEasing.Evaluate(_easing, raw));
		if (raw < 1f)
		{
			return false;
		}

		IsActive = false;
		_completion.Invoke();
		return true;
	}

	internal void SnapToEnd()
	{
		if (!IsActive)
		{
			return;
		}
		if (Owner == null)
		{
			Cancel();
			return;
		}
		_apply?.Invoke(1f);
		IsActive = false;
		_completion.Invoke();
	}

	internal void Cancel()
	{
		IsActive = false;
		_apply = null;
		_completion.Cancel();
	}

	public void Dispose()
	{
		Cancel();
		Owner = null;
	}

}
