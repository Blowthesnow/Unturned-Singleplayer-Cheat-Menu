using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Owns one rendered page subtree and its destruction contract.</summary>
internal sealed class UiPageView<TPage> : IDisposable where TPage : struct, Enum
{
	private readonly Action<GameObject> _destroy;
	private bool _disposed;

	internal UiPageView(TPage page, GameObject root, Action<GameObject> destroy)
	{
		Page = page;
		Root = root ?? throw new ArgumentNullException(nameof(root));
		_destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
	}

	internal TPage Page { get; }
	internal GameObject Root { get; private set; }

	internal void DetachForAncestorDestroy()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		Root = null;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		GameObject root = Root;
		Root = null;
		if (root != null)
		{
			_destroy(root);
		}
	}
}
