using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class UiDebouncer : IDisposable
{
    private readonly float _delaySeconds;
    private Action _pending;
    private float _dueTime;

    internal UiDebouncer(float delaySeconds)
    {
        _delaySeconds = Mathf.Clamp(delaySeconds, 0.1f, 0.2f);
    }

    internal void Schedule(Action action)
    {
        _pending = action;
        _dueTime = Time.unscaledTime + _delaySeconds;
    }

    internal void Tick()
    {
        if (_pending == null || Time.unscaledTime < _dueTime)
            return;

        Action action = _pending;
        _pending = null;
        action();
    }

    internal void Cancel()
    {
        _pending = null;
    }

    public void Dispose()
    {
        Cancel();
    }
}
