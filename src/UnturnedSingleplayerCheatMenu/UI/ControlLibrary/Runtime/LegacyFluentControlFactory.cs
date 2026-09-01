using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

/// <summary>Legacy Text/InputField adaptation subset; it creates no Canvas or EventSystem.</summary>
internal sealed class LegacyFluentControlFactory
{
	private readonly FluentTheme _theme;
	private readonly Func<Font> _font;

	internal LegacyFluentControlFactory(FluentTheme theme, Func<Font> font)
	{
		_theme = theme ?? throw new ArgumentNullException(nameof(theme));
		_font = font ?? throw new ArgumentNullException(nameof(font));
	}

	internal Font ResolveFont() => _font();

	internal LegacyFluentControlHandle AdaptButton(GameObject root, Button button, Image hitTarget, FluentButtonVariant variant)
	{
		LegacyFluentSurface surface = AttachSurface(root, hitTarget, ResolvePalette(variant, emphasized: variant == FluentButtonVariant.Primary).NormalFill, _theme.BorderStrong, _theme.RadiusControl);
		button.targetGraphic = hitTarget;
		button.transition = Selectable.Transition.None;
		return new LegacyFluentControlHandle(root, surface);
	}

	internal LegacyFluentControlHandle AdaptTextBox(GameObject root, InputField input, Image hitTarget)
	{
		LegacyFluentSurface surface = AttachSurface(root, hitTarget, _theme.SurfaceMuted, _theme.BorderStrong, _theme.RadiusField);
		input.targetGraphic = hitTarget;
		input.transition = Selectable.Transition.None;
		input.caretColor = _theme.Focus.ToUnity();
		input.selectionColor = _theme.FocusSoft.ToUnity();
		return new LegacyFluentControlHandle(root, surface);
	}

	internal LegacyFluentControlHandle AdaptToggle(GameObject root, Toggle toggle, Image hitTarget, GameObject legacyCheck)
	{
		LegacyFluentSurface surface = AttachSurface(root, hitTarget, toggle.isOn ? _theme.AccentSoft : _theme.Surface, _theme.BorderStrong, _theme.RadiusControl);
		toggle.targetGraphic = hitTarget;
		toggle.transition = Selectable.Transition.None;
		toggle.graphic = null;
		legacyCheck?.SetActive(false);

		GameObject trackObject = CreateRoundedChild(root.transform, "SwitchTrack", new Vector2(44f, 22f), _theme.RadiusControl);
		RectTransform trackRect = trackObject.GetComponent<RectTransform>();
		trackRect.anchorMin = trackRect.anchorMax = new Vector2(1f, 0.5f);
		trackRect.pivot = new Vector2(1f, 0.5f);
		trackRect.anchoredPosition = new Vector2(-11f, 0f);
		FluentRoundedGraphic track = trackObject.GetComponent<FluentRoundedGraphic>();

		GameObject thumbObject = CreateRoundedChild(trackObject.transform, "SwitchThumb", new Vector2(16f, 16f), 8f);
		RectTransform thumbRect = thumbObject.GetComponent<RectTransform>();
		thumbRect.anchorMin = thumbRect.anchorMax = new Vector2(0.5f, 0.5f);
		thumbRect.pivot = new Vector2(0.5f, 0.5f);
		FluentRoundedGraphic thumb = thumbObject.GetComponent<FluentRoundedGraphic>();
		thumb.color = _theme.TextPrimary.ToUnity();

		UnityAction<bool> visualListener = value => UpdateSwitch(value, track, thumbRect);
		toggle.onValueChanged.AddListener(visualListener);
		UpdateSwitch(toggle.isOn, track, thumbRect);

		LegacyFluentControlHandle handle = new LegacyFluentControlHandle(root, surface);
		handle.AddCleanup(() => toggle.onValueChanged.RemoveListener(visualListener));
		return handle;
	}

	internal LegacyFluentControlHandle AdaptSlider(GameObject root, Slider slider, Image hitTarget, Image track, Image fill)
	{
		LegacyFluentSurface surface = AttachSurface(root, hitTarget, _theme.SurfaceMuted, _theme.Border, _theme.RadiusField);
		slider.targetGraphic = hitTarget;
		slider.transition = Selectable.Transition.None;
		if (track != null)
		{
			track.color = _theme.HudTrack.ToUnity();
		}
		if (fill != null)
		{
			fill.color = _theme.Accent.ToUnity();
		}
		return new LegacyFluentControlHandle(root, surface);
	}

	internal LegacyFluentTimeRingControl AdaptTimeRing(
		GameObject root,
		LegacyFluentTimeRingGraphic graphic,
		Text timeLabel,
		Text phaseLabel,
		Func<float, string> timeFormatter,
		string phasePrefix,
		Func<float, string> phaseFormatter)
	{
		return new LegacyFluentTimeRingControl(root, graphic, timeLabel, phaseLabel, timeFormatter, phasePrefix, phaseFormatter);
	}

	internal LegacyFluentControlHandle AdaptSurface(GameObject root, Image legacyImage, FluentSurfaceRole role, float radius)
	{
		RgbaColor fill = ResolveSurface(role);
		RgbaColor border = role == FluentSurfaceRole.Panel ? _theme.BorderStrong : _theme.Border;
		return new LegacyFluentControlHandle(root, AttachSurface(root, legacyImage, fill, border, radius));
	}

	internal LegacyFluentControlHandle AdaptTooltipSurface(GameObject root, Image legacyImage, float radius)
	{
		return new LegacyFluentControlHandle(root,
			AttachSurface(root, legacyImage, _theme.SurfaceRaised, _theme.TooltipBorder, radius, borderThickness: 2f));
	}

	internal FluentSelectablePalette ResolvePalette(FluentButtonVariant variant, bool emphasized)
	{
		RgbaColor normal = variant switch
		{
			FluentButtonVariant.Primary => _theme.Accent,
			FluentButtonVariant.Navigation => emphasized ? _theme.AccentSoft : _theme.Navigation,
			FluentButtonVariant.Danger => _theme.Error,
			FluentButtonVariant.Subtle => _theme.SurfaceMuted,
			_ => emphasized ? _theme.AccentSoft : _theme.Surface
		};
		RgbaColor hover = variant switch
		{
			FluentButtonVariant.Primary => _theme.AccentHover,
			FluentButtonVariant.Danger => _theme.ErrorSoft,
			_ => _theme.SurfaceHover
		};
		RgbaColor pressed = variant switch
		{
			FluentButtonVariant.Primary => _theme.AccentPressed,
			FluentButtonVariant.Danger => _theme.Error,
			_ => _theme.SurfacePressed
		};
		RgbaColor text = variant == FluentButtonVariant.Primary ? _theme.AccentText : _theme.TextPrimary;
		return new FluentSelectablePalette(
			normal, hover, pressed, _theme.FocusSoft, _theme.SurfaceMuted.WithAlpha(150),
			_theme.BorderStrong, _theme.Focus, _theme.BorderStrong, _theme.Focus, _theme.Border.WithAlpha(120),
			text, _theme.TextTertiary.WithAlpha(150));
	}

	private LegacyFluentSurface AttachSurface(GameObject root, Image legacyImage, RgbaColor fill, RgbaColor border, float radius, float borderThickness = 1f)
	{
		if (legacyImage != null)
		{
			legacyImage.color = Color.clear;
		}
		return LegacyFluentSurface.Attach(root, fill.ToUnity(), border.ToUnity(), radius, borderThickness);
	}

	private RgbaColor ResolveSurface(FluentSurfaceRole role)
	{
		return role switch
		{
			FluentSurfaceRole.Panel => _theme.AppBackground,
			FluentSurfaceRole.Navigation => _theme.Navigation,
			FluentSurfaceRole.Raised => _theme.SurfaceRaised,
			FluentSurfaceRole.Muted => _theme.SurfaceMuted,
			FluentSurfaceRole.Input => _theme.SurfaceMuted,
			FluentSurfaceRole.Accent => _theme.Accent,
			FluentSurfaceRole.Info => _theme.InfoSoft,
			FluentSurfaceRole.Success => _theme.SuccessSoft,
			FluentSurfaceRole.Warning => _theme.WarningSoft,
			FluentSurfaceRole.Error => _theme.ErrorSoft,
			FluentSurfaceRole.Hud => _theme.HudPanel,
			_ => _theme.Surface
		};
	}

	private static GameObject CreateRoundedChild(Transform parent, string name, Vector2 size, float radius)
	{
		GameObject result = new GameObject(name, typeof(RectTransform), typeof(FluentRoundedGraphic), typeof(LayoutElement));
		result.transform.SetParent(parent, false);
		RectTransform rect = result.GetComponent<RectTransform>();
		rect.sizeDelta = size;
		result.GetComponent<LayoutElement>().ignoreLayout = true;
		result.GetComponent<FluentRoundedGraphic>().Radius = radius;
		result.GetComponent<FluentRoundedGraphic>().raycastTarget = false;
		return result;
	}

	private void UpdateSwitch(bool isOn, FluentRoundedGraphic track, RectTransform thumb)
	{
		if (track != null)
		{
			track.color = (isOn ? _theme.Accent : _theme.SurfaceMuted).ToUnity();
		}
		if (thumb != null)
		{
			thumb.anchoredPosition = new Vector2(isOn ? 9f : -9f, 0f);
		}
	}
}
