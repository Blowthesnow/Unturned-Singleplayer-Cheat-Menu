using System;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class ButtonTooltipText
{
	internal static string Require(string tooltip)
	{
		if (string.IsNullOrWhiteSpace(tooltip))
		{
			throw new ArgumentException("Every Button requires a non-empty hover explanation.", nameof(tooltip));
		}

		return tooltip;
	}
}
