namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

internal enum FluentButtonVariant
{
	Standard,
	Primary,
	Subtle,
	Navigation,
	Danger
}

internal enum FluentSurfaceRole
{
	Panel,
	Navigation,
	Surface,
	Raised,
	Muted,
	Input,
	Accent,
	Info,
	Success,
	Warning,
	Error,
	Hud
}

/// <summary>Legacy-uGUI projection of the upstream circular world-time picker options.</summary>
internal sealed class FluentTimeRingOptions
{
	internal float Time { get; set; } = 12f;

	internal float Step { get; set; } = 0.25f;

	internal float RingThickness { get; set; } = 18f;

	internal float HandleSize { get; set; } = 28f;
}

internal readonly struct FluentSelectablePalette
{
	internal FluentSelectablePalette(
		RgbaColor normalFill,
		RgbaColor hoverFill,
		RgbaColor pressedFill,
		RgbaColor focusedFill,
		RgbaColor disabledFill,
		RgbaColor normalBorder,
		RgbaColor hoverBorder,
		RgbaColor pressedBorder,
		RgbaColor focusedBorder,
		RgbaColor disabledBorder,
		RgbaColor normalText,
		RgbaColor disabledText)
	{
		NormalFill = normalFill;
		HoverFill = hoverFill;
		PressedFill = pressedFill;
		FocusedFill = focusedFill;
		DisabledFill = disabledFill;
		NormalBorder = normalBorder;
		HoverBorder = hoverBorder;
		PressedBorder = pressedBorder;
		FocusedBorder = focusedBorder;
		DisabledBorder = disabledBorder;
		NormalText = normalText;
		DisabledText = disabledText;
	}

	internal RgbaColor NormalFill { get; }
	internal RgbaColor HoverFill { get; }
	internal RgbaColor PressedFill { get; }
	internal RgbaColor FocusedFill { get; }
	internal RgbaColor DisabledFill { get; }
	internal RgbaColor NormalBorder { get; }
	internal RgbaColor HoverBorder { get; }
	internal RgbaColor PressedBorder { get; }
	internal RgbaColor FocusedBorder { get; }
	internal RgbaColor DisabledBorder { get; }
	internal RgbaColor NormalText { get; }
	internal RgbaColor DisabledText { get; }
}
