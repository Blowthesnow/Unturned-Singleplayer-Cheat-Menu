namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

internal static class FluentThemes
{
	internal static FluentTheme DarkInk { get; } = CreateDarkInk();

	private static FluentTheme CreateDarkInk()
	{
		return new FluentTheme(
			"深墨",
			RgbaColor.Parse("#171A1B"), RgbaColor.Parse("#1D2122"), RgbaColor.Parse("#222728"),
			RgbaColor.Parse("#292F30"), RgbaColor.Parse("#202526"), RgbaColor.Parse("#303738"),
			RgbaColor.Parse("#394142"), RgbaColor.Parse("#F3EDE3"), RgbaColor.Parse("#C0B7AD"),
			RgbaColor.Parse("#A69D94"), RgbaColor.Parse("#3A4243"), RgbaColor.Parse("#566061"),
			RgbaColor.Parse("#0078D4"), RgbaColor.Parse("#168CE0"), RgbaColor.Parse("#005FA8"),
			RgbaColor.Parse("#153A55"), RgbaColor.Parse("#FFFFFF"), RgbaColor.Parse("#4CA6EA"),
			RgbaColor.Parse("#1C465F"), RgbaColor.Parse("#6BC08C"), RgbaColor.Parse("#294337"),
			RgbaColor.Parse("#E2B766"), RgbaColor.Parse("#4A3E28"), RgbaColor.Parse("#EF8181"),
			RgbaColor.Parse("#4D2D30"), RgbaColor.Parse("#79B7D4"), RgbaColor.Parse("#293E48"),
			RgbaColor.Parse("#000000B0"), RgbaColor.Parse("#000000A0"),
			RgbaColor.Parse("#0D1113F2"), RgbaColor.Parse("#272E30E6"), RgbaColor.Parse("#FFF9F1"),
			RgbaColor.Parse("#C9C0B6C8"), RgbaColor.Parse("#FFFFFF4A"), RgbaColor.Parse("#FFFFFF78"));
	}
}
