using UnityEngine;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class CheatMenuStyle
{
	internal static readonly FluentTheme Theme = FluentThemes.DarkInk;
	internal static Color DefaultAccent => new Color32(0, 120, 212, 255);

	internal static readonly Color Panel = Theme.AppBackground.ToUnity();

	internal static readonly Color Surface = Theme.Surface.ToUnity();

	internal static readonly Color SurfaceRaised = Theme.SurfaceRaised.ToUnity();

	internal static readonly Color SurfaceHover = Theme.SurfaceHover.ToUnity();

	internal static readonly Color SurfaceInput = Theme.SurfaceMuted.ToUnity();

	internal static readonly Color SurfaceInset = Theme.Navigation.ToUnity();

	internal static Color Accent => Theme.Accent.ToUnity();

	internal static Color AccentBright => Theme.Focus.ToUnity();

	internal static readonly Color Favorite = new Color(0.82f, 0.6f, 0.12f, 1f);

	internal static readonly Color Success = Theme.Success.ToUnity();

	internal static readonly Color Warning = Theme.Warning.ToUnity();

	internal static readonly Color Danger = Theme.Error.ToUnity();

	internal static readonly Color Text = Theme.TextPrimary.ToUnity();

	internal static readonly Color Muted = Theme.TextSecondary.ToUnity();

	internal static readonly Color DisabledText = Theme.TextTertiary.WithAlpha(150).ToUnity();

	internal static readonly Color DisabledSurface = Theme.SurfaceMuted.WithAlpha(150).ToUnity();

	internal static readonly Color ButtonBorder = Theme.BorderStrong.ToUnity();

	internal static Color ButtonBorderHover => Theme.Focus.ToUnity();

	internal static readonly Color ButtonBorderPressed = Theme.BorderStrong.ToUnity();

	internal static readonly Color ButtonBorderDisabled = Theme.Border.WithAlpha(120).ToUnity();

	internal static readonly Color InputBorder = Theme.BorderStrong.ToUnity();

	internal static readonly Color SliderTrack = Theme.HudTrack.ToUnity();

	internal static readonly Color SliderBorder = Theme.BorderStrong.ToUnity();

	internal static readonly Color TeleportBorder = Theme.BorderStrong.ToUnity();

	internal static readonly Color StatusBorder = Theme.BorderStrong.ToUnity();

	internal static readonly Color ToggleBorder = Theme.BorderStrong.ToUnity();

	internal static readonly Color SummaryBorder = Theme.BorderStrong.ToUnity();

	internal static Color TooltipBorder => Theme.TooltipBorder.ToUnity();

	internal static readonly Color HudBackdrop = new Color(0f, 0f, 0f, 0.8f);

	internal static Color HudBorder => Theme.HudBorder.ToUnity();

	internal static int AccentRevision => Theme.AccentRevision;

	internal static void ApplyAccent(Color color)
	{
		color.r = Mathf.Clamp01(color.r);
		color.g = Mathf.Clamp01(color.g);
		color.b = Mathf.Clamp01(color.b);
		color.a = 1f;
		Theme.ApplyAccent(color.ToRgba());
	}

	internal const int TitleFontSize = 25;

	internal const int SectionFontSize = 17;

	internal const int BodyFontSize = 13;

	internal const int MutedFontSize = 12;

	internal const float ButtonHeight = 40f;

	internal const float InputHeight = 34f;

	internal const float SliderWidth = 170f;

	internal const float SliderHeight = 34f;

	internal const float StatusHeight = 40f;

	internal const float TabHeight = 40f;

	internal const float RowSpacing = 8f;

	internal const float CardWidth = 155f;

	internal const float CardHeight = 158f;

	internal const float CategorySidebarWidth = 176f;

	internal const float CategoryButtonHeight = 40f;

	internal const float MainNavigationWidth = 168f;

	internal const float ToolbarFieldLabelWidth = 64f;

	internal const float InfiniteStorageWithdrawLabelWidth = 132f;

	internal const float ColorChannelLabelWidth = 50f;
}
