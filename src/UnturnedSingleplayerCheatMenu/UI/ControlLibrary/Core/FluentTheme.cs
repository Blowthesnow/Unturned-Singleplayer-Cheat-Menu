using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

/// <summary>Legacy-uGUI projection of the upstream semantic theme contract.</summary>
internal sealed class FluentTheme
{
	internal FluentTheme(
		string name,
		RgbaColor appBackground,
		RgbaColor navigation,
		RgbaColor surface,
		RgbaColor surfaceRaised,
		RgbaColor surfaceMuted,
		RgbaColor surfaceHover,
		RgbaColor surfacePressed,
		RgbaColor textPrimary,
		RgbaColor textSecondary,
		RgbaColor textTertiary,
		RgbaColor border,
		RgbaColor borderStrong,
		RgbaColor accent,
		RgbaColor accentHover,
		RgbaColor accentPressed,
		RgbaColor accentSoft,
		RgbaColor accentText,
		RgbaColor focus,
		RgbaColor focusSoft,
		RgbaColor success,
		RgbaColor successSoft,
		RgbaColor warning,
		RgbaColor warningSoft,
		RgbaColor error,
		RgbaColor errorSoft,
		RgbaColor info,
		RgbaColor infoSoft,
		RgbaColor scrim,
		RgbaColor shadow,
		RgbaColor hudBackdrop,
		RgbaColor hudPanel,
		RgbaColor hudText,
		RgbaColor hudSubtle,
		RgbaColor hudTrack,
		RgbaColor hudBorder)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new ArgumentException("A theme name is required.", nameof(name));
		}

		Name = name;
		AppBackground = appBackground;
		Navigation = navigation;
		Surface = surface;
		SurfaceRaised = surfaceRaised;
		SurfaceMuted = surfaceMuted;
		SurfaceHover = surfaceHover;
		SurfacePressed = surfacePressed;
		TextPrimary = textPrimary;
		TextSecondary = textSecondary;
		TextTertiary = textTertiary;
		Border = border;
		BorderStrong = borderStrong;
		Accent = accent;
		AccentHover = accentHover;
		AccentPressed = accentPressed;
		AccentSoft = accentSoft;
		AccentText = accentText;
		Focus = focus;
		FocusSoft = focusSoft;
		Success = success;
		SuccessSoft = successSoft;
		Warning = warning;
		WarningSoft = warningSoft;
		Error = error;
		ErrorSoft = errorSoft;
		Info = info;
		InfoSoft = infoSoft;
		Scrim = scrim;
		Shadow = shadow;
		HudBackdrop = hudBackdrop;
		HudPanel = hudPanel;
		HudText = hudText;
		HudSubtle = hudSubtle;
		HudTrack = hudTrack;
		HudBorder = hudBorder;
	}

	internal string Name { get; }
	internal RgbaColor AppBackground { get; }
	internal RgbaColor Navigation { get; }
	internal RgbaColor Surface { get; }
	internal RgbaColor SurfaceRaised { get; }
	internal RgbaColor SurfaceMuted { get; }
	internal RgbaColor SurfaceHover { get; }
	internal RgbaColor SurfacePressed { get; }
	internal RgbaColor TextPrimary { get; }
	internal RgbaColor TextSecondary { get; }
	internal RgbaColor TextTertiary { get; }
	internal RgbaColor Border { get; }
	internal RgbaColor BorderStrong { get; }
	internal RgbaColor Accent { get; private set; }
	internal RgbaColor AccentHover { get; private set; }
	internal RgbaColor AccentPressed { get; private set; }
	internal RgbaColor AccentSoft { get; private set; }
	internal RgbaColor AccentText { get; private set; }
	internal RgbaColor Focus { get; private set; }
	internal RgbaColor FocusSoft { get; private set; }
	internal RgbaColor Success { get; }
	internal RgbaColor SuccessSoft { get; }
	internal RgbaColor Warning { get; }
	internal RgbaColor WarningSoft { get; }
	internal RgbaColor Error { get; }
	internal RgbaColor ErrorSoft { get; }
	internal RgbaColor Info { get; }
	internal RgbaColor InfoSoft { get; }
	internal RgbaColor Scrim { get; }
	internal RgbaColor Shadow { get; }
	internal RgbaColor HudBackdrop { get; }
	internal RgbaColor HudPanel { get; }
	internal RgbaColor HudText { get; }
	internal RgbaColor HudSubtle { get; }
	internal RgbaColor HudTrack { get; }
	internal RgbaColor HudBorder { get; }
	internal RgbaColor TooltipBorder => Blend(BorderStrong, new RgbaColor(255, 255, 255), 0.14f);
	internal int AccentRevision { get; private set; }

	/// <summary>Updates only the accent/focus family; success, warning, error, and info remain semantic.</summary>
	internal void ApplyAccent(RgbaColor accent)
	{
		accent = new RgbaColor(accent.Red, accent.Green, accent.Blue);
		Accent = accent;
		AccentHover = Blend(accent, new RgbaColor(255, 255, 255), 0.16f);
		AccentPressed = Blend(accent, new RgbaColor(0, 0, 0), 0.22f);
		AccentSoft = Blend(AppBackground, accent, 0.24f);
		Focus = Blend(accent, new RgbaColor(255, 255, 255), 0.24f);
		FocusSoft = Blend(AppBackground, accent, 0.30f);
		AccentText = RelativeLuminance(accent) >= 0.58f
			? new RgbaColor(18, 22, 24)
			: new RgbaColor(255, 255, 255);
		AccentRevision++;
	}

	internal float Space1 => 4f;
	internal float Space2 => 8f;
	internal float Space3 => 12f;
	internal float Space4 => 16f;
	internal float Space6 => 24f;
	internal float Space8 => 32f;
	internal float RadiusField => 6f;
	internal float RadiusControl => 10f;
	internal float RadiusPanel => 16f;
	internal float ControlCompact => 34f;
	internal float ControlDefault => 40f;
	internal float ControlTouch => 44f;
	internal float TypeCaption => 12f;
	internal float TypeBody => 14f;
	internal float TypeLead => 17f;
	internal float TypeSection => 22f;
	internal float TypeTitle => 34f;

	internal float NormalizeSpacing(float value)
	{
		if (value <= 0f)
		{
			return 0f;
		}
		float[] tokens = { Space1, Space2, Space3, Space4, Space6, Space8 };
		float best = tokens[0];
		float distance = Mathf.Abs(value - best);
		for (int index = 1; index < tokens.Length; index++)
		{
			float candidateDistance = Mathf.Abs(value - tokens[index]);
			if (candidateDistance < distance)
			{
				best = tokens[index];
				distance = candidateDistance;
			}
		}
		return best;
	}

	internal float NormalizeControlHeight(float value)
	{
		if (value <= 0f || value > 48f)
		{
			return value;
		}
		if (value <= 37f)
		{
			return ControlCompact;
		}
		return value <= 42f ? ControlDefault : ControlTouch;
	}

	internal RectOffset NormalizePadding(RectOffset padding)
	{
		if (padding == null)
		{
			return new RectOffset();
		}
		return new RectOffset(
			Mathf.RoundToInt(NormalizeSpacing(padding.left)),
			Mathf.RoundToInt(NormalizeSpacing(padding.right)),
			Mathf.RoundToInt(NormalizeSpacing(padding.top)),
			Mathf.RoundToInt(NormalizeSpacing(padding.bottom)));
	}

	private static RgbaColor Blend(RgbaColor from, RgbaColor to, float amount)
	{
		amount = Mathf.Clamp01(amount);
		return new RgbaColor(
			(byte)Mathf.RoundToInt(Mathf.Lerp(from.Red, to.Red, amount)),
			(byte)Mathf.RoundToInt(Mathf.Lerp(from.Green, to.Green, amount)),
			(byte)Mathf.RoundToInt(Mathf.Lerp(from.Blue, to.Blue, amount)),
			(byte)Mathf.RoundToInt(Mathf.Lerp(from.Alpha, to.Alpha, amount)));
	}

	private static float RelativeLuminance(RgbaColor color)
	{
		return 0.2126f * Linear(color.Red) + 0.7152f * Linear(color.Green) + 0.0722f * Linear(color.Blue);
	}

	private static float Linear(byte channel)
	{
		float value = channel / 255f;
		return value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
	}
}
