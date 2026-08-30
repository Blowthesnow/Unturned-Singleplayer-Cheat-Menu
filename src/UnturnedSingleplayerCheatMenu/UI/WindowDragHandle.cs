using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _target;
    private RectTransform _bounds;
    private Action<Vector2> _onPositionChanged;
    private Vector2 _pointerOffset;

    internal void Initialize(RectTransform target, RectTransform bounds, Action<Vector2> onPositionChanged)
    {
        _target = target;
        _bounds = bounds;
        _onPositionChanged = onPositionChanged;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_target == null || _bounds == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_bounds, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            return;
        }
        _pointerOffset = localPoint - _target.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_target == null || _bounds == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_bounds, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            return;
        }
        Vector2 next = localPoint - _pointerOffset;
        Vector2 boundsSize = _bounds.rect.size;
        Vector2 targetSize = Vector2.Scale(_target.rect.size, _target.localScale);
        float maxX = Mathf.Max(0f, (boundsSize.x - targetSize.x) * 0.5f);
        float maxY = Mathf.Max(0f, (boundsSize.y - targetSize.y) * 0.5f);
        next.x = Mathf.Clamp(next.x, -maxX, maxX);
        next.y = Mathf.Clamp(next.y, -maxY, maxY);
        _target.anchoredPosition = next;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _onPositionChanged?.Invoke(_target != null ? _target.anchoredPosition : Vector2.zero);
    }
}
