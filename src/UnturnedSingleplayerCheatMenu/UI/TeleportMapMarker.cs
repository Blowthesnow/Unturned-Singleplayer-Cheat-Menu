using UnityEngine;
using UnityEngine.EventSystems;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class TeleportMapMarker : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IDragHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
	private TeleportMapSurface _owner;

	public TeleportPoint Point { get; private set; }

	public void Initialize(TeleportMapSurface owner, TeleportPoint point)
	{
		_owner = owner;
		Point = point;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Right)
		{
			_owner?.HandleMarkerRightClick(Point);
			eventData.Use();
		}
		else if (eventData.button == PointerEventData.InputButton.Left)
		{
			_owner?.BeginDrag(eventData.position);
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			_owner?.EndDrag();
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		_owner?.Drag(eventData);
	}

	public void OnScroll(PointerEventData eventData)
	{
		_owner?.Scroll(eventData);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		_owner?.ShowMarkerTooltip(Point, eventData.position);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		_owner?.HideMarkerTooltip(Point);
	}
}
