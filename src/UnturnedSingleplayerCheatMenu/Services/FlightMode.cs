namespace UnturnedSingleplayerCheatMenu.Services;

internal enum FlightMode
{
	WorldAxis,
	ViewDirection
}

internal static class FlightModeExtensions
{
	internal static string ToDisplayName(this FlightMode mode)
	{
		return mode switch
		{
			FlightMode.ViewDirection => "视角指向",
			_ => "世界轴"
		};
	}
}
