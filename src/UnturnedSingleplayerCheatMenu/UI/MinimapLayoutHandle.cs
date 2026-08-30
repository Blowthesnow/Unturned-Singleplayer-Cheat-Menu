using UnityEngine;
using UnityEngine.EventSystems;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class MinimapLayoutHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	private MinimapHud _owner;
	private bool _resize;

	internal void Initialize(MinimapHud owner, bool resize)
	{
		_owner = owner;
		_resize = resize;
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		_owner?.BeginLayoutDrag();
	}

	public void OnDrag(PointerEventData eventData)
	{
		_owner?.ApplyLayoutDrag(eventData.delta, _resize);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		_owner?.EndLayoutDrag();
	}
}
