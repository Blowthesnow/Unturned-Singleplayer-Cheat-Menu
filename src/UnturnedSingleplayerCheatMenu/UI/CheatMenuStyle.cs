using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class CheatMenuStyle
{
    internal static readonly Color Panel = new(0.035f, 0.045f, 0.060f, 0.985f);
    internal static readonly Color Surface = new(0.105f, 0.145f, 0.185f, 0.99f);
    internal static readonly Color SurfaceRaised = new(0.145f, 0.205f, 0.275f, 1f);
    internal static readonly Color SurfaceHover = SurfaceRaised;
    internal static readonly Color SurfaceInput = new(0.04f, 0.055f, 0.075f, 1f);
    internal static readonly Color SurfaceInset = new(0.070f, 0.095f, 0.125f, 1f);
    internal static readonly Color Accent = new(0.08f, 0.55f, 0.88f, 1f);
    internal static readonly Color AccentBright = new(0.18f, 0.72f, 1f, 1f);
    internal static readonly Color Favorite = new(0.82f, 0.60f, 0.12f, 1f);
    internal static readonly Color Success = new(0.30f, 0.76f, 0.54f, 1f);
    internal static readonly Color Warning = new(0.91f, 0.73f, 0.30f, 1f);
    internal static readonly Color Danger = new(0.72f, 0.15f, 0.20f, 1f);
    internal static readonly Color Text = new(0.94f, 0.97f, 1f, 1f);
    internal static readonly Color Muted = new(0.62f, 0.69f, 0.76f, 1f);
    internal static readonly Color DisabledText = new(0.48f, 0.53f, 0.59f, 1f);
    internal static readonly Color DisabledSurface = new(0.055f, 0.068f, 0.087f, 0.92f);
    internal static readonly Color ButtonBorder = new(0.30f, 0.40f, 0.50f, 0.88f);
    internal static readonly Color ButtonBorderHover = new(0.30f, 0.70f, 0.95f, 0.98f);
    internal static readonly Color ButtonBorderPressed = new(0.20f, 0.30f, 0.40f, 0.95f);
    internal static readonly Color ButtonBorderDisabled = new(0.20f, 0.24f, 0.29f, 0.55f);
    internal static readonly Color InputBorder = new(0.22f, 0.30f, 0.39f, 0.95f);
    internal static readonly Color SliderTrack = new(0.055f, 0.075f, 0.100f, 1f);
    internal static readonly Color SliderBorder = new(0.25f, 0.34f, 0.43f, 0.95f);
    internal static readonly Color TeleportBorder = new(0.18f, 0.26f, 0.34f, 0.95f);
    internal static readonly Color StatusBorder = new(0.20f, 0.29f, 0.38f, 0.95f);
    internal static readonly Color ToggleBorder = new(0.24f, 0.33f, 0.42f, 0.95f);
    internal static readonly Color SummaryBorder = new(0.24f, 0.34f, 0.44f, 0.95f);

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
