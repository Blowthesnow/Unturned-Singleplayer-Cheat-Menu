using System;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal sealed class UiMotionCompletion
{
	private Action _callback;
	private bool _settled;

	internal void Reset(Action callback)
	{
		_callback = callback;
		_settled = false;
	}

	internal void Invoke()
	{
		if (_settled) return;
		_settled = true;
		Action callback = _callback;
		_callback = null;
		callback?.Invoke();
	}

	internal void Cancel()
	{
		_settled = true;
		_callback = null;
	}
}
