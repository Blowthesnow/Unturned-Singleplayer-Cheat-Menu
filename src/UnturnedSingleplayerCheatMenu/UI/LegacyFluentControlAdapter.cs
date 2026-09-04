using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using UnturnedSingleplayerCheatMenu.UI.Motion;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Bridges existing GameObjects/callbacks to the Legacy Fluent visual and handle contracts.</summary>
internal sealed class LegacyFluentControlAdapter
{
	private readonly LegacyFluentUiContext _context;
	private readonly LegacyFluentHandleRegistry _registry;
	private readonly LegacyFluentTooltipBridge _tooltips;
	private readonly LegacyFluentControlFactory _factory;
	private readonly UiMotionDriver _motion;

	internal LegacyFluentControlAdapter(
		LegacyFluentUiContext context,
		LegacyFluentHandleRegistry registry,
		LegacyFluentTooltipBridge tooltips,
		UiMotionDriver motion)
	{
		_context = context ?? throw new ArgumentNullException(nameof(context));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_tooltips = tooltips ?? throw new ArgumentNullException(nameof(tooltips));
		_motion = motion;
		_factory = new LegacyFluentControlFactory(context.Theme, context.ResolveFont, motion);
	}

	internal LegacyFluentControlHandle AdaptButton(
		GameObject root,
		Button button,
		Image hitTarget,
		UiText label,
		GameObject focusIndicator,
		Outline legacyOutline,
		Color baseColor,
		Action action,
		string tooltip,
		bool delayTooltip)
	{
		FluentButtonVariant variant = ResolveVariant(baseColor);
		LegacyFluentControlHandle handle = _factory.AdaptButton(root, button, hitTarget, variant);
		if (action != null)
		{
			UnityAction listener = action.Invoke;
			button.onClick.AddListener(listener);
			handle.AddCleanup(() => button.onClick.RemoveListener(listener));
		}
		OverlaySelectableVisual visual = root.GetComponent<OverlaySelectableVisual>() ?? root.AddComponent<OverlaySelectableVisual>();
		visual.Initialize(button, hitTarget, label, focusIndicator, legacyOutline, baseColor, _motion, handle.Surface,
			color => ResolvePalette(color, variant));
		_tooltips.Attach(visual, tooltip, delayTooltip);
		_registry.Track(root, handle);
		return handle;
	}

	internal LegacyFluentControlHandle AdaptInput(
		GameObject root,
		InputField input,
		Image hitTarget,
		UiText text,
		GameObject focusIndicator,
		Outline legacyOutline,
		Action<string> changed,
		bool commitOnEndEdit,
		string tooltip)
	{
		LegacyFluentControlHandle handle = _factory.AdaptTextBox(root, input, hitTarget);
		if (changed != null)
		{
			UnityAction<string> listener = changed.Invoke;
			if (commitOnEndEdit)
			{
				input.onEndEdit.AddListener(listener);
				handle.AddCleanup(() => input.onEndEdit.RemoveListener(listener));
			}
			else
			{
				input.onValueChanged.AddListener(listener);
				handle.AddCleanup(() => input.onValueChanged.RemoveListener(listener));
			}
		}
		OverlaySelectableVisual visual = root.GetComponent<OverlaySelectableVisual>() ?? root.AddComponent<OverlaySelectableVisual>();
		visual.Initialize(input, hitTarget, text, focusIndicator, legacyOutline, _context.Theme.SurfaceMuted.ToUnity(), _motion, handle.Surface,
			color => _factory.ResolvePalette(FluentButtonVariant.Subtle, emphasized: false));
		_tooltips.Attach(visual, tooltip, delay: false);
		_registry.Track(root, handle);
		return handle;
	}

	internal LegacyFluentControlHandle AdaptToggle(
		GameObject root,
		Toggle toggle,
		Image hitTarget,
		UiText label,
		GameObject legacyCheck,
		GameObject focusIndicator,
		Outline legacyOutline,
		Color baseColor,
		Action<bool> changed,
		string tooltip,
		Color? activeColor = null)
	{
		LegacyFluentControlHandle handle = _factory.AdaptToggle(root, toggle, hitTarget,
			legacyCheck, activeColor);
		if (changed != null)
		{
			UnityAction<bool> listener = changed.Invoke;
			toggle.onValueChanged.AddListener(listener);
			handle.AddCleanup(() => toggle.onValueChanged.RemoveListener(listener));
		}
		OverlaySelectableVisual visual = root.GetComponent<OverlaySelectableVisual>() ?? root.AddComponent<OverlaySelectableVisual>();
		visual.Initialize(toggle, hitTarget, label, focusIndicator, legacyOutline, baseColor, _motion, handle.Surface,
			_ => ResolveTogglePalette());
		visual.SetInteractableChanged(handle.InteractableChanged);
		handle.AddCleanup(() =>
		{
			if (visual != null) visual.SetInteractableChanged(null);
		});
		_tooltips.Attach(visual, tooltip, delay: false);
		_registry.Track(root, handle);
		return handle;
	}

	internal LegacyFluentControlHandle AdaptSlider(
		GameObject root,
		Slider slider,
		Image hitTarget,
		Image track,
		Image fill,
		Action<float> changed)
	{
		foreach (Outline outline in root.GetComponents<Outline>())
		{
			outline.enabled = false;
		}
		LegacyFluentControlHandle handle = _factory.AdaptSlider(root, slider, hitTarget, track, fill);
		if (changed != null)
		{
			UnityAction<float> listener = changed.Invoke;
			slider.onValueChanged.AddListener(listener);
			handle.AddCleanup(() => slider.onValueChanged.RemoveListener(listener));
		}
		_registry.Track(root, handle);
		return handle;
	}

	internal LegacyFluentTimeRingControl AdaptTimeRing(
		GameObject root,
		LegacyFluentTimeRingGraphic graphic,
		UiText timeLabel,
		UiText phaseLabel,
		Func<float, string> timeFormatter,
		string phasePrefix,
		Func<float, string> phaseFormatter,
		Action<float> changed)
	{
		LegacyFluentTimeRingControl handle = _factory.AdaptTimeRing(
			root, graphic, timeLabel, phaseLabel, timeFormatter, phasePrefix, phaseFormatter);
		if (changed != null)
		{
			handle.TimeChanged += changed;
			handle.AddCleanup(() => handle.TimeChanged -= changed);
		}
		_registry.Track(root, handle);
		return handle;
	}

	internal void TrackInputEndEdit(GameObject root, InputField input, Action<string> changed)
	{
		if (root == null)
		{
			throw new ArgumentNullException(nameof(root));
		}
		if (input == null)
		{
			throw new ArgumentNullException(nameof(input));
		}
		if (changed == null)
		{
			return;
		}

		LegacyFluentControlHandle handle = new LegacyFluentControlHandle(root, surface: null);
		UnityAction<string> listener = changed.Invoke;
		input.onEndEdit.AddListener(listener);
		handle.AddCleanup(() => input.onEndEdit.RemoveListener(listener));
		_registry.Track(root, handle);
	}

	internal LegacyFluentControlHandle StyleSurface(GameObject root, Image image, FluentSurfaceRole role, float radius)
	{
		foreach (Outline outline in root.GetComponents<Outline>())
		{
			outline.enabled = false;
		}
		LegacyFluentControlHandle handle = _factory.AdaptSurface(root, image, role, radius);
		_registry.Track(root, handle);
		return handle;
	}

	internal LegacyFluentControlHandle StyleTooltipSurface(GameObject root, Image image, float radius)
	{
		foreach (Outline outline in root.GetComponents<Outline>())
		{
			outline.enabled = false;
		}
		LegacyFluentControlHandle handle = _factory.AdaptTooltipSurface(root, image, radius);
		_registry.Track(root, handle);
		return handle;
	}

	internal void MarkNavigation(Button button)
	{
		OverlaySelectableVisual visual = button != null ? button.GetComponent<OverlaySelectableVisual>() : null;
		if (visual != null)
		{
			visual.SetPaletteResolver(color => ResolvePalette(color, FluentButtonVariant.Navigation));
		}
	}

	internal void MarkDestructiveHover(Button button)
	{
		OverlaySelectableVisual visual = button != null ? button.GetComponent<OverlaySelectableVisual>() : null;
		if (visual == null)
		{
			return;
		}
		FluentSelectablePalette standard = _factory.ResolvePalette(FluentButtonVariant.Standard, emphasized: false);
		visual.SetPaletteResolver(_ => new FluentSelectablePalette(
			standard.NormalFill, _context.Theme.Error, _context.Theme.Error,
			standard.FocusedFill, standard.DisabledFill,
			standard.NormalBorder, _context.Theme.Error, _context.Theme.Error,
			standard.FocusedBorder, standard.DisabledBorder,
			standard.NormalText, standard.DisabledText));
	}

	internal void TrackLifetime(GameObject root, IDisposable lifetime) => _registry.Track(root, lifetime);

	private FluentSelectablePalette ResolvePalette(Color color, FluentButtonVariant preferred)
	{
		bool emphasized = Approximately(color, _context.Theme.Accent.ToUnity()) ||
			Approximately(color, _context.Theme.AccentHover.ToUnity());
		if (preferred == FluentButtonVariant.Navigation)
		{
			return _factory.ResolvePalette(FluentButtonVariant.Navigation, emphasized);
		}
		if (Approximately(color, _context.Theme.Error.ToUnity()))
		{
			return _factory.ResolvePalette(FluentButtonVariant.Danger, emphasized: true);
		}
		if (Approximately(color, _context.Theme.Success.ToUnity()) || Approximately(color, CheatMenuStyle.Success))
		{
			return CreateSemanticPalette(_context.Theme.Success, _context.Theme.SuccessSoft);
		}
		if (Approximately(color, _context.Theme.Warning.ToUnity()) || Approximately(color, CheatMenuStyle.Warning) ||
			Approximately(color, CheatMenuStyle.Favorite))
		{
			return CreateSemanticPalette(_context.Theme.Warning, _context.Theme.WarningSoft);
		}
		if (emphasized)
		{
			return _factory.ResolvePalette(FluentButtonVariant.Primary, emphasized: true);
		}
		if (Approximately(color, CheatMenuStyle.Surface) || Approximately(color, CheatMenuStyle.SurfaceHover) ||
			Approximately(color, CheatMenuStyle.SurfaceRaised) || Approximately(color, CheatMenuStyle.SurfaceInput) ||
			Approximately(color, CheatMenuStyle.SurfaceInset))
		{
			return _factory.ResolvePalette(preferred, emphasized: false);
		}
		return CreateCustomPalette(color);
	}

	private FluentSelectablePalette CreateSemanticPalette(RgbaColor normal, RgbaColor soft)
	{
		return new FluentSelectablePalette(
			normal, soft, normal, soft, _context.Theme.SurfaceMuted.WithAlpha(150),
			_context.Theme.BorderStrong, _context.Theme.Focus, _context.Theme.BorderStrong, _context.Theme.Focus,
			_context.Theme.Border.WithAlpha(120), _context.Theme.TextPrimary, _context.Theme.TextTertiary.WithAlpha(150));
	}

	private FluentSelectablePalette ResolveTogglePalette()
	{
		FluentSelectablePalette standard = _factory.ResolvePalette(FluentButtonVariant.Standard, emphasized: false);
		return new FluentSelectablePalette(
			standard.NormalFill,
			standard.HoverFill,
			standard.PressedFill,
			_context.Theme.SurfaceHover,
			standard.DisabledFill,
			standard.NormalBorder,
			standard.HoverBorder,
			standard.PressedBorder,
			_context.Theme.Focus,
			standard.DisabledBorder,
			standard.NormalText,
			standard.DisabledText);
	}

	private FluentSelectablePalette CreateCustomPalette(Color color)
	{
		RgbaColor normal = ToRgba(color);
		RgbaColor hover = ToRgba(Color.Lerp(color, Color.white, 0.14f));
		RgbaColor pressed = ToRgba(Color.Lerp(color, Color.black, 0.2f));
		return new FluentSelectablePalette(
			normal, hover, pressed, _context.Theme.FocusSoft, _context.Theme.SurfaceMuted.WithAlpha(150),
			_context.Theme.BorderStrong, _context.Theme.Focus, _context.Theme.BorderStrong, _context.Theme.Focus,
			_context.Theme.Border.WithAlpha(120), _context.Theme.TextPrimary, _context.Theme.TextTertiary.WithAlpha(150));
	}

	private static RgbaColor ToRgba(Color color) => new RgbaColor(
		(byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
		(byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
		(byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
		(byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));

	private FluentButtonVariant ResolveVariant(Color color)
	{
		if (Approximately(color, _context.Theme.Accent.ToUnity()))
		{
			return FluentButtonVariant.Primary;
		}
		if (Approximately(color, _context.Theme.Error.ToUnity()))
		{
			return FluentButtonVariant.Danger;
		}
		return FluentButtonVariant.Standard;
	}

	private static bool Approximately(Color left, Color right) =>
		Mathf.Abs(left.r - right.r) < 0.02f && Mathf.Abs(left.g - right.g) < 0.02f &&
		Mathf.Abs(left.b - right.b) < 0.02f && Mathf.Abs(left.a - right.a) < 0.02f;
}
