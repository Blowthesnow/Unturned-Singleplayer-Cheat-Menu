using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class CheatMenuStyle
{
	internal static readonly Color Panel = new Color(0.035f, 0.045f, 0.06f, 0.985f);

	internal static readonly Color Surface = new Color(0.105f, 0.145f, 0.185f, 0.99f);

	internal static readonly Color SurfaceRaised = new Color(0.145f, 0.205f, 0.275f, 1f);

	internal static readonly Color SurfaceHover = SurfaceRaised;

	internal static readonly Color SurfaceInput = new Color(0.04f, 0.055f, 0.075f, 1f);

	internal static readonly Color SurfaceInset = new Color(0.07f, 0.095f, 0.125f, 1f);

	internal static readonly Color Accent = new Color(0.08f, 0.55f, 0.88f, 1f);

	internal static readonly Color AccentBright = new Color(0.18f, 0.72f, 1f, 1f);

	internal static readonly Color Favorite = new Color(0.82f, 0.6f, 0.12f, 1f);

	internal static readonly Color Success = new Color(0.3f, 0.76f, 0.54f, 1f);

	internal static readonly Color Warning = new Color(0.91f, 0.73f, 0.3f, 1f);

	internal static readonly Color Danger = new Color(0.72f, 0.15f, 0.2f, 1f);

	internal static readonly Color Text = new Color(0.94f, 0.97f, 1f, 1f);

	internal static readonly Color Muted = new Color(0.62f, 0.69f, 0.76f, 1f);

	internal static readonly Color DisabledText = new Color(0.48f, 0.53f, 0.59f, 1f);

	internal static readonly Color DisabledSurface = new Color(0.055f, 0.068f, 0.087f, 0.92f);

	internal static readonly Color ButtonBorder = new Color(0.3f, 0.4f, 0.5f, 0.88f);

	internal static readonly Color ButtonBorderHover = new Color(0.3f, 0.7f, 0.95f, 0.98f);

	internal static readonly Color ButtonBorderPressed = new Color(0.2f, 0.3f, 0.4f, 0.95f);

	internal static readonly Color ButtonBorderDisabled = new Color(0.2f, 0.24f, 0.29f, 0.55f);

	internal static readonly Color InputBorder = new Color(0.22f, 0.3f, 0.39f, 0.95f);

	internal static readonly Color SliderTrack = new Color(0.055f, 0.075f, 0.1f, 1f);

	internal static readonly Color SliderBorder = new Color(0.25f, 0.34f, 0.43f, 0.95f);

	internal static readonly Color TeleportBorder = new Color(0.18f, 0.26f, 0.34f, 0.95f);

	internal static readonly Color StatusBorder = new Color(0.2f, 0.29f, 0.38f, 0.95f);

	internal static readonly Color ToggleBorder = new Color(0.24f, 0.33f, 0.42f, 0.95f);

	internal static readonly Color SummaryBorder = new Color(0.24f, 0.34f, 0.44f, 0.95f);

	internal const int TitleFontSize = 25;

	internal const int SectionFontSize = 17;

	internal const int BodyFontSize = 13;

	internal const int MutedFontSize = 12;

	internal const float ButtonHeight = 36f;

	internal const float InputHeight = 34f;

	internal const float SliderWidth = 170f;

	internal const float SliderHeight = 22f;

	internal const float StatusHeight = 34f;

	internal const float TabHeight = 40f;

	internal const float RowSpacing = 8f;

	internal const float CardWidth = 155f;

	internal const float CardHeight = 158f;
}
