using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class OverlaySelectableVisual : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
	private static readonly List<OverlaySelectableVisual> ActiveVisuals = new List<OverlaySelectableVisual>();

	private Selectable _selectable;

	private Image _background;

	private Text _label;

	private GameObject _focusIndicator;

	private Outline _outline;

	private Color _baseColor;

	private LegacyFluentSurface _fluentSurface;

	private Func<Color, FluentSelectablePalette> _paletteResolver;

	private bool _hovered;

	private bool _pressed;

	private bool _selected;

	private bool _showOutline = true;

	private bool _showFocusIndicator = true;

	private bool? _lastInteractable;

	private string _hoverExplanation;

	private bool _delayHoverExplanation;

	private float _hoverExplanationStartTime;

	private bool _hoverExplanationShown;

	private Action<string, RectTransform, Vector2> _showHoverExplanation;

	private Action _hideHoverExplanation;

	public void Initialize(
		Selectable selectable,
		Image background,
		Text label,
		GameObject focusIndicator,
		Outline outline,
		Color baseColor,
		LegacyFluentSurface fluentSurface = null,
		Func<Color, FluentSelectablePalette> paletteResolver = null)
	{
		_selectable = selectable;
		_background = background;
		_label = label;
		_focusIndicator = focusIndicator;
		_outline = outline;
		_baseColor = baseColor;
		_fluentSurface = fluentSurface;
		_paletteResolver = paletteResolver;
		_lastInteractable = null;
		Refresh();
	}

	internal void SetPaletteResolver(Func<Color, FluentSelectablePalette> paletteResolver)
	{
		_paletteResolver = paletteResolver;
		Refresh();
	}

	public void SetHoverExplanation(string message, Action<string, RectTransform, Vector2> show, Action hide, bool delay = false)
	{
		_hoverExplanation = message;
		_showHoverExplanation = show;
		_hideHoverExplanation = hide;
		_delayHoverExplanation = delay;
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
		_hoverExplanationStartTime = Time.unscaledTime;
		_hoverExplanationShown = false;
		Refresh();
		if (!_delayHoverExplanation && !string.IsNullOrWhiteSpace(_hoverExplanation))
		{
			_hoverExplanationShown = true;
			_showHoverExplanation?.Invoke(_hoverExplanation, (_background != null) ? _background.rectTransform : null, eventData.position);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		_hovered = false;
		_pressed = false;
		CancelHoverExplanation();
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
		ActiveVisuals.Remove(this);
		_hovered = false;
		_pressed = false;
		_selected = false;
		CancelHoverExplanation();
		if (_focusIndicator != null)
		{
			_focusIndicator.SetActive(value: false);
		}
	}

	private void OnEnable()
	{
		if (!ActiveVisuals.Contains(this))
		{
			ActiveVisuals.Add(this);
		}
		Refresh();
	}

	internal static void PumpAll()
	{
		for (int i = ActiveVisuals.Count - 1; i >= 0; i--)
		{
			OverlaySelectableVisual visual = ActiveVisuals[i];
			if (visual == null)
			{
				ActiveVisuals.RemoveAt(i);
				continue;
			}
			visual.Pump();
		}
	}

	private void Pump()
	{
		if (_selectable != null && _lastInteractable != _selectable.interactable)
		{
			Refresh();
		}
		if (_delayHoverExplanation && _hovered && !_hoverExplanationShown && !string.IsNullOrWhiteSpace(_hoverExplanation) && Time.unscaledTime - _hoverExplanationStartTime >= 2f)
		{
			_hoverExplanationShown = true;
			_showHoverExplanation?.Invoke(_hoverExplanation, (_background != null) ? _background.rectTransform : null, Input.mousePosition);
		}
	}

	private void CancelHoverExplanation()
	{
		_hoverExplanationStartTime = 0f;
		_hoverExplanationShown = false;
		_hideHoverExplanation?.Invoke();
	}

	private void Refresh()
	{
		if (_selectable == null || _background == null)
		{
			return;
		}
		_lastInteractable = _selectable.interactable;
		if (_fluentSurface != null && _paletteResolver != null)
		{
			RefreshFluent();
			return;
		}
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

	private void RefreshFluent()
	{
		FluentSelectablePalette palette = _paletteResolver(_baseColor);
		_background.color = Color.clear;
		if (_outline != null)
		{
			_outline.enabled = false;
		}

		Color fill;
		Color border;
		Color textColor;
		if (!_selectable.interactable)
		{
			fill = palette.DisabledFill.ToUnity();
			border = palette.DisabledBorder.ToUnity();
			textColor = palette.DisabledText.ToUnity();
		}
		else if (_pressed)
		{
			fill = palette.PressedFill.ToUnity();
			border = palette.PressedBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else if (_hovered)
		{
			fill = palette.HoverFill.ToUnity();
			border = palette.HoverBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else if (_selected)
		{
			fill = palette.FocusedFill.ToUnity();
			border = palette.FocusedBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else
		{
			fill = palette.NormalFill.ToUnity();
			border = palette.NormalBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}

		_fluentSurface.SetColors(fill, _showOutline ? border : Color.clear);
		if (_label != null)
		{
			_label.color = textColor;
		}
		if (_focusIndicator != null)
		{
			bool visible = _selectable.interactable && _showFocusIndicator && _selected;
			_focusIndicator.SetActive(visible);
			if (visible)
			{
				_focusIndicator.transform.SetAsLastSibling();
			}
		}
	}
}
