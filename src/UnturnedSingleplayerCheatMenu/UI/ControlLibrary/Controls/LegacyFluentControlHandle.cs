using System;
using System.Collections.Generic;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;

/// <summary>Subscription/surface lifetime; the page host remains the sole GameObject owner.</summary>
internal class LegacyFluentControlHandle : IDisposable
{
	private readonly List<Action> _cleanup = new List<Action>();
	private bool _disposed;

	internal LegacyFluentControlHandle(GameObject root, LegacyFluentSurface surface)
	{
		Root = root ?? throw new ArgumentNullException(nameof(root));
		Surface = surface;
	}

	internal GameObject Root { get; private set; }
	internal LegacyFluentSurface Surface { get; }
	internal bool IsDisposed => _disposed;
	internal Action<bool> InteractableChanged { get; set; }

	internal void AddCleanup(Action cleanup)
	{
		if (cleanup == null)
		{
			return;
		}
		if (_disposed)
		{
			cleanup();
			return;
		}
		_cleanup.Add(cleanup);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		for (int index = _cleanup.Count - 1; index >= 0; index--)
		{
			_cleanup[index]();
		}
		_cleanup.Clear();
		Surface?.Dispose();
		InteractableChanged = null;
		Root = null;
	}
}
