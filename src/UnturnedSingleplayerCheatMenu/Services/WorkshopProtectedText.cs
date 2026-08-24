using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopProtectedText
{
	internal string Text { get; }

	internal IReadOnlyList<string> Tokens { get; }

	internal WorkshopProtectedText(string text, IReadOnlyList<string> tokens)
	{
		Text = text;
		Tokens = tokens;
	}
}
