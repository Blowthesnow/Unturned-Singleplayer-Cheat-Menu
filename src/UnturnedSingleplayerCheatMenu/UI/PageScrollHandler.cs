using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class PageScrollHandler : UIBehaviour, IScrollHandler
{
	private Action _previous;
	private Action _next;
	private int _page;
	private int _pageCount;

	public void Initialize(int page, int pageCount, Action previous, Action next)
	{
		_page = page;
		_pageCount = pageCount;
		_previous = previous;
		_next = next;
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (eventData == null || !PageNavigation.TryMove(_page, _pageCount, eventData.scrollDelta.y, IsOverInput(eventData), out int nextPage))
		{
			return;
		}
		if (nextPage < _page) _previous?.Invoke();
		else _next?.Invoke();
		eventData.Use();
	}

	private static bool IsOverInput(PointerEventData eventData)
	{
		return eventData.pointerEnter != null && eventData.pointerEnter.GetComponentInParent<InputField>() != null;
	}
}
