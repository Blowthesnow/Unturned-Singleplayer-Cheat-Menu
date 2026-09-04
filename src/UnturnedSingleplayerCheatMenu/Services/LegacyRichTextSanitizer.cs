using System;
using System.Text;
using System.Text.RegularExpressions;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class LegacyRichTextSanitizer
{
	private static readonly Regex ColorTag = new Regex(
		@"<\s*(/?)\s*color(?:\s*=\s*[^>]+)?\s*>",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	internal static string Normalize(string source)
	{
		if (string.IsNullOrEmpty(source)) return source;
		MatchCollection matches = ColorTag.Matches(source);
		if (matches.Count == 0) return source;
		StringBuilder result = new StringBuilder(source.Length + 8);
		int cursor = 0;
		int openColorDepth = 0;
		foreach (Match match in matches)
		{
			result.Append(source, cursor, match.Index - cursor);
			bool closing = match.Groups[1].Length > 0;
			bool parameterizedOpening = !closing && match.Value.IndexOf('=') >= 0;
			if (closing)
			{
				if (openColorDepth > 0) openColorDepth--;
				result.Append(match.Value);
			}
			else if (parameterizedOpening)
			{
				openColorDepth++;
				result.Append(match.Value);
			}
			else if (openColorDepth > 0)
			{
				openColorDepth--;
				result.Append("</color>");
			}
			else
			{
				result.Append(match.Value);
			}
			cursor = match.Index + match.Length;
		}
		result.Append(source, cursor, source.Length - cursor);
		return result.ToString();
	}
}
