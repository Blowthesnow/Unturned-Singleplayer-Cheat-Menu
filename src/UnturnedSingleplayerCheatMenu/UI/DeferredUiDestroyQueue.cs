using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class DeferredUiDestroyQueue<T> where T : class
{
	private sealed class Entry
	{
		internal Entry(T target, int earliestFrame)
		{
			Target = target;
			EarliestFrame = earliestFrame;
		}

		internal T Target { get; }
		internal int EarliestFrame { get; }
	}

	private readonly List<Entry> _entries = new List<Entry>();

	internal int Count => _entries.Count;

	internal void Enqueue(T target, int currentFrame, Action<T> retire)
	{
		if (target == null)
		{
			throw new ArgumentNullException(nameof(target));
		}
		if (retire == null)
		{
			throw new ArgumentNullException(nameof(retire));
		}
		retire(target);
		foreach (Entry entry in _entries)
		{
			if (ReferenceEquals(entry.Target, target))
			{
				return;
			}
		}
		_entries.Add(new Entry(target, currentFrame + 1));
	}

	internal bool TryDequeueReady(int currentFrame, bool canvasRebuilding, out T target)
	{
		target = null;
		if (canvasRebuilding)
		{
			return false;
		}
		for (int i = 0; i < _entries.Count; i++)
		{
			Entry entry = _entries[i];
			if (currentFrame < entry.EarliestFrame)
			{
				continue;
			}
			_entries.RemoveAt(i);
			target = entry.Target;
			return true;
		}
		return false;
	}
}
