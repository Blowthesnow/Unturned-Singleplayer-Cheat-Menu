using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class OverlaySelectableVisual : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
	private Selectable _selectable;

	private Image _background;

	private Text _label;

	private GameObject _focusIndicator;

	private Outline _outline;

	private Color _baseColor;

	private bool _hovered;

	private bool _pressed;

	private bool _selected;

	private bool _showOutline = true;

	private bool _showFocusIndicator = true;

	private bool? _lastInteractable;

	private string _hoverExplanation;

	private Action<string, RectTransform, Vector2> _showHoverExplanation;

	private Action _hideHoverExplanation;

	public void Initialize(Selectable selectable, Image background, Text label, GameObject focusIndicator, Outline outline, Color baseColor)
	{
		_selectable = selectable;
		_background = background;
		_label = label;
		_focusIndicator = focusIndicator;
		_outline = outline;
		_baseColor = baseColor;
		_lastInteractable = null;
		Refresh();
	}

	public void SetHoverExplanation(string message, Action<string, RectTransform, Vector2> show, Action hide)
	{
		_hoverExplanation = message;
		_showHoverExplanation = show;
		_hideHoverExplanation = hide;
	}

	public void SetBaseColor(Color baseColor)
	{
		_baseColor = baseColor;
		Refresh();
	}

	public void SetOutlineVisible(bool visible)
	{
		_showOutline = visible;
		Refresh();
	}

	public void SetFocusIndicatorVisible(bool visible)
	{
		_showFocusIndicator = visible;
		Refresh();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		_hovered = true;
		Refresh();
		if (!string.IsNullOrWhiteSpace(_hoverExplanation))
		{
			_showHoverExplanation?.Invoke(_hoverExplanation, (_background != null) ? _background.rectTransform : null, eventData.position);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		_hovered = false;
		_pressed = false;
		_hideHoverExplanation?.Invoke();
		Refresh();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		_pressed = true;
		Refresh();
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		_pressed = false;
		Refresh();
	}

	public void OnSelect(BaseEventData eventData)
	{
		_selected = true;
		Refresh();
	}

	public void OnDeselect(BaseEventData eventData)
	{
		_selected = false;
		Refresh();
	}

	private void OnDisable()
	{
		_hovered = false;
		_pressed = false;
		_selected = false;
		_hideHoverExplanation?.Invoke();
		if (_focusIndicator != null)
		{
			_focusIndicator.SetActive(value: false);
		}
	}

	private void OnEnable()
	{
		Refresh();
	}

	private void Update()
	{
		if (_selectable != null && _lastInteractable != _selectable.interactable)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		if (_selectable == null || _background == null)
		{
			return;
		}
		_lastInteractable = _selectable.interactable;
		if (!_selectable.interactable)
		{
			_background.color = CheatMenuStyle.DisabledSurface;
			if (_label != null)
			{
				_label.color = CheatMenuStyle.DisabledText;
			}
			if (_focusIndicator != null)
			{
				_focusIndicator.SetActive(value: false);
			}
			if (_outline != null)
			{
				_outline.effectColor = (_showOutline ? CheatMenuStyle.ButtonBorderDisabled : Color.clear);
			}
			return;
		}
		if (_label != null)
		{
			_label.color = CheatMenuStyle.Text;
		}
		if (_focusIndicator != null)
		{
			bool flag = _showFocusIndicator && _selected;
			_focusIndicator.SetActive(flag);
			if (flag)
			{
				_focusIndicator.transform.SetAsLastSibling();
			}
		}
		if (_pressed)
		{
			_background.color = Color.Lerp(_baseColor, Color.black, 0.2f);
			if (_outline != null)
			{
				_outline.effectColor = (_showOutline ? CheatMenuStyle.ButtonBorderPressed : Color.clear);
			}
		}
		else if (_hovered || _selected)
		{
			_background.color = (_hovered ? new Color(0f, 0f, 0f, 0.3f) : Color.Lerp(_baseColor, Color.white, 0.14f));
			if (_outline != null)
			{
				_outline.effectColor = (_showOutline ? CheatMenuStyle.ButtonBorderHover : Color.clear);
				Canvas componentInParent = _background.GetComponentInParent<Canvas>();
				float num = ((componentInParent != null) ? Mathf.Max(0.01f, componentInParent.scaleFactor) : 1f);
				_outline.effectDistance = Vector2.one * (3f / num);
			}
		}
		else
		{
			_background.color = _baseColor;
			if (_outline != null)
			{
				_outline.effectColor = (_showOutline ? CheatMenuStyle.ButtonBorder : Color.clear);
			}
		}
	}
}
