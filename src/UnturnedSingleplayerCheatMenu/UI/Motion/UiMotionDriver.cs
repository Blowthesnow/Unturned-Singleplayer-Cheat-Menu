using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal sealed class UiMotionDriver : IDisposable
{
	private readonly List<UiMotionHandle> _handles = new List<UiMotionHandle>();

	internal int ActiveCount
	{
		get
		{
			int count = 0;
			for (int index = 0; index < _handles.Count; index++)
			{
				if (_handles[index].IsActive) count++;
			}
			return count;
		}
	}

	internal UiMotionHandle Animate(
		GameObject owner,
		UiMotionChannel channel,
		float duration,
		UiMotionEasingKind easing,
		Action<float> apply,
		Action completed = null)
	{
		if (owner == null)
		{
			completed?.Invoke();
			return null;
		}

		UiMotionHandle handle = Find(owner, channel);
		if (handle == null)
		{
			handle = new UiMotionHandle(owner, channel);
			_handles.Add(handle);
		}
		handle.Retarget(duration, easing, apply, completed);
		return handle;
	}

	internal void Pump(float unscaledDeltaTime)
	{
		for (int index = _handles.Count - 1; index >= 0; index--)
		{
			UiMotionHandle handle = _handles[index];
			if (handle == null)
			{
				_handles.RemoveAt(index);
				continue;
			}
			bool finished = handle.Advance(unscaledDeltaTime);
			// Completion callbacks may retarget this handle or mutate the registry.
			if (finished && !handle.IsActive && _handles.Remove(handle))
			{
				handle.Dispose();
			}
		}
	}

	internal void Cancel(GameObject owner, bool includeChildren = false)
	{
		if (owner == null)
		{
			return;
		}
		Transform root = owner.transform;
		for (int index = 0; index < _handles.Count; index++)
		{
			UiMotionHandle handle = _handles[index];
			GameObject candidate = handle?.Owner;
			if (candidate == null)
			{
				handle?.Cancel();
				continue;
			}
			if (ReferenceEquals(candidate, owner) || includeChildren && candidate.transform.IsChildOf(root))
			{
				handle.Cancel();
			}
		}
	}

	internal void CancelAll()
	{
		for (int index = 0; index < _handles.Count; index++)
		{
			_handles[index]?.Dispose();
		}
		_handles.Clear();
	}

	internal void SnapAll()
	{
		int safety = 4096;
		while (_handles.Count > 0 && safety-- > 0)
		{
			int index = _handles.Count - 1;
			UiMotionHandle handle = _handles[index];
			_handles.RemoveAt(index);
			handle?.SnapToEnd();
			handle?.Dispose();
		}
		if (_handles.Count > 0) CancelAll();
	}

	public void Dispose() => CancelAll();

	private UiMotionHandle Find(GameObject owner, UiMotionChannel channel)
	{
		for (int index = 0; index < _handles.Count; index++)
		{
			UiMotionHandle handle = _handles[index];
			if (handle != null && ReferenceEquals(handle.Owner, owner) && handle.Channel == channel)
			{
				return handle;
			}
		}
		return null;
	}
}
