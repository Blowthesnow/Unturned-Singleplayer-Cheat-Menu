using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Owns the active GameObject subtree for one enum-keyed page; it contains no feature state or commands.</summary>
internal abstract class ActivePageViewHost<TPage> : IDisposable where TPage : struct, Enum
{
	private readonly Func<TPage, UiPageView<TPage>> _createView;
	private UiPageView<TPage> _activeView;
	private TPage? _activePage;

	protected ActivePageViewHost(Func<TPage, UiPageView<TPage>> createView)
	{
		_createView = createView ?? throw new ArgumentNullException(nameof(createView));
	}

	internal GameObject Activate(TPage page, bool invalidate)
	{
		if (!invalidate && _activeView?.Root != null && _activePage.HasValue &&
			EqualityComparer<TPage>.Default.Equals(_activePage.Value, page))
		{
			_activeView.Root.SetActive(true);
			return _activeView.Root;
		}
		DestroyActiveView();
		_activeView = _createView(page);
		_activeView.Root.SetActive(true);
		_activePage = page;
		return _activeView.Root;
	}

	private void DestroyActiveView()
	{
		if (_activeView != null)
		{
			_activeView.Dispose();
			_activeView = null;
		}
		_activePage = null;
	}

	public void Dispose() => DestroyActiveView();
}
