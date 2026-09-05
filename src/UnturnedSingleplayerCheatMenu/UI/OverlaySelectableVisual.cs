using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using UnturnedSingleplayerCheatMenu.UI.Motion;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class OverlaySelectableVisual : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
	private static readonly List<OverlaySelectableVisual> ActiveVisuals = new List<OverlaySelectableVisual>();
	private const float DetailedTooltipDelaySeconds = 0.45f;
	private const float TooltipWarmWindowSeconds = 0.70f;
	private static OverlaySelectableVisual _tooltipOwner;
	private static float _tooltipWarmUntil;

	private Selectable _selectable;

	private Image _background;

	private Text _label;

	private GameObject _focusIndicator;

	private Outline _outline;

	private Color _baseColor;

	private LegacyFluentSurface _fluentSurface;

	private Func<Color, FluentSelectablePalette> _paletteResolver;
	private UiMotionDriver _motion;
	private VisualState _visualState;
	private bool _hasVisualState;
	private bool _hasTargetPalette;
	private Color _targetFill;
	private Color _targetBorder;
	private Color _targetText;
	private Action<bool> _interactableChanged;

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

	private Action<string, RectTransform, Vector2, bool> _showHoverExplanation;

	private Action _hideHoverExplanation;

	private enum VisualState
	{
		Normal,
		Hovered,
		Pressed,
		Focused,
		Disabled
	}

	public void Initialize(
		Selectable selectable,
		Image background,
		Text label,
		GameObject focusIndicator,
		Outline outline,
		Color baseColor,
		UiMotionDriver motion,
		LegacyFluentSurface fluentSurface = null,
		Func<Color, FluentSelectablePalette> paletteResolver = null)
	{
		_selectable = selectable;
		_background = background;
		_label = label;
		_focusIndicator = focusIndicator;
		_outline = outline;
		_baseColor = baseColor;
		_motion = motion;
		_fluentSurface = fluentSurface;
		_paletteResolver = paletteResolver;
		_lastInteractable = null;
		Refresh(animate: false);
	}

	internal void SetPaletteResolver(Func<Color, FluentSelectablePalette> paletteResolver)
	{
		_paletteResolver = paletteResolver;
		Refresh(animate: false);
	}

	public void SetHoverExplanation(string message, Action<string, RectTransform, Vector2, bool> show, Action hide, bool delay = false)
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

	internal void SetInteractableChanged(Action<bool> changed)
	{
		_interactableChanged = changed;
		if (_selectable != null) _interactableChanged?.Invoke(_selectable.interactable);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		_hovered = true;
		_hoverExplanationStartTime = Time.unscaledTime;
		_hoverExplanationShown = false;
		Refresh();
		bool warm = Time.unscaledTime < _tooltipWarmUntil;
		if ((!_delayHoverExplanation || warm) && !string.IsNullOrWhiteSpace(_hoverExplanation))
		{
			ShowHoverExplanationNow(eventData.position);
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
		_motion?.Cancel(gameObject, includeChildren: true);
		_hovered = false;
		_pressed = false;
		_selected = false;
		CancelHoverExplanation();
		if (_focusIndicator != null)
		{
			_focusIndicator.SetActive(value: false);
		}
		_hasVisualState = false;
		_hasTargetPalette = false;
	}

	private void OnEnable()
	{
		if (!ActiveVisuals.Contains(this))
		{
			ActiveVisuals.Add(this);
		}
		Refresh(animate: false);
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
		if (_delayHoverExplanation && _hovered && !_hoverExplanationShown && !string.IsNullOrWhiteSpace(_hoverExplanation) &&
			Time.unscaledTime - _hoverExplanationStartTime >= DetailedTooltipDelaySeconds)
		{
			ShowHoverExplanationNow(Input.mousePosition);
		}
	}

	private void ShowHoverExplanationNow(Vector2 screenPosition)
	{
		bool warm = Time.unscaledTime < _tooltipWarmUntil;
		_hoverExplanationShown = true;
		_tooltipOwner = this;
		_tooltipWarmUntil = Time.unscaledTime + TooltipWarmWindowSeconds;
		_showHoverExplanation?.Invoke(_hoverExplanation, (_background != null) ? _background.rectTransform : null, screenPosition, warm);
	}

	private void CancelHoverExplanation()
	{
		bool ownsTooltip = ReferenceEquals(_tooltipOwner, this);
		if (_hoverExplanationShown) _tooltipWarmUntil = Time.unscaledTime + TooltipWarmWindowSeconds;
		_hoverExplanationStartTime = 0f;
		_hoverExplanationShown = false;
		if (ownsTooltip)
		{
			_tooltipOwner = null;
			_hideHoverExplanation?.Invoke();
		}
	}

	private void Refresh(bool animate = true)
	{
		if (_selectable == null || _background == null)
		{
			return;
		}
		bool interactableChanged = _lastInteractable != _selectable.interactable;
		_lastInteractable = _selectable.interactable;
		if (interactableChanged) _interactableChanged?.Invoke(_selectable.interactable);
		if (_fluentSurface != null && _paletteResolver != null)
		{
			RefreshFluent(animate);
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

	private void RefreshFluent(bool animate)
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
		VisualState state;
		if (!_selectable.interactable)
		{
			state = VisualState.Disabled;
			fill = palette.DisabledFill.ToUnity();
			border = palette.DisabledBorder.ToUnity();
			textColor = palette.DisabledText.ToUnity();
		}
		else if (_pressed)
		{
			state = VisualState.Pressed;
			fill = palette.PressedFill.ToUnity();
			border = palette.PressedBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else if (_hovered)
		{
			state = VisualState.Hovered;
			fill = palette.HoverFill.ToUnity();
			border = palette.HoverBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else if (_selected)
		{
			state = VisualState.Focused;
			fill = palette.FocusedFill.ToUnity();
			border = palette.FocusedBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}
		else
		{
			state = VisualState.Normal;
			fill = palette.NormalFill.ToUnity();
			border = palette.NormalBorder.ToUnity();
			textColor = palette.NormalText.ToUnity();
		}

		border = _showOutline ? border : Color.clear;
		VisualState previousState = _visualState;
		bool firstState = !_hasVisualState;
		_visualState = state;
		_hasVisualState = true;
		bool immediate = !animate || firstState || state == VisualState.Disabled || _motion == null || !isActiveAndEnabled;
		float duration = state == VisualState.Pressed
			? UiMotionTokens.PressIn
			: previousState == VisualState.Pressed ? UiMotionTokens.PressOut : UiMotionTokens.Hover;
		bool targetsChanged = !_hasTargetPalette || !Approximately(_targetFill, fill) ||
			!Approximately(_targetBorder, border) || !Approximately(_targetText, textColor);
		_targetFill = fill;
		_targetBorder = border;
		_targetText = textColor;
		_hasTargetPalette = true;
		if (!firstState && previousState == state && !targetsChanged)
		{
			RefreshFocusIndicator();
			return;
		}

		if (immediate)
		{
			_motion?.Cancel(gameObject);
			_fluentSurface.SetColors(fill, border);
			if (_label != null) _label.color = textColor;
		}
		else
		{
			Color startFill = _fluentSurface.CurrentFill;
			Color startBorder = _fluentSurface.CurrentBorder;
			_motion.Animate(gameObject, UiMotionChannel.FillColor, duration, UiMotionEasingKind.EaseOutCubic,
				progress => _fluentSurface.SetColors(
					Color.LerpUnclamped(startFill, fill, progress),
					Color.LerpUnclamped(startBorder, border, progress)));
			if (_label != null)
			{
				Color startText = _label.color;
				_motion.Animate(gameObject, UiMotionChannel.TextColor, duration, UiMotionEasingKind.EaseOutCubic,
					progress => _label.color = Color.LerpUnclamped(startText, textColor, progress));
			}
		}
		RefreshFocusIndicator();
	}

	private void RefreshFocusIndicator()
	{
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

	private static bool Approximately(Color left, Color right)
	{
		return Mathf.Abs(left.r - right.r) < 0.001f && Mathf.Abs(left.g - right.g) < 0.001f &&
			Mathf.Abs(left.b - right.b) < 0.001f && Mathf.Abs(left.a - right.a) < 0.001f;
	}
}
