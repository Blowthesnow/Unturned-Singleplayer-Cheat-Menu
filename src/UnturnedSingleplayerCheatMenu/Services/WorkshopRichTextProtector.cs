using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class WorkshopRichTextProtector
{
	private const string TokenPrefix = "__USCM_PROTECTED_";

	private static readonly Regex ProtectedPart = new Regex("<[^>\\r\\n]+>|\\{\\d+\\}|https?://[^\\s<>]+|\\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\\b|\\b\\d+\\b|\\r\\n|\\r|\\n", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	internal static WorkshopProtectedText Protect(string source)
	{
		List<string> tokens = new List<string>();
		return new WorkshopProtectedText(ProtectedPart.Replace(source ?? string.Empty, delegate(Match match)
		{
			tokens.Add(match.Value);
			return "__USCM_PROTECTED_" + (tokens.Count - 1).ToString("D4") + "__";
		}), tokens);
	}

	internal static bool TryRestore(WorkshopProtectedText protectedSource, string translated, out string restored, out string error)
	{
		restored = null;
		error = null;
		if (protectedSource == null || translated == null)
		{
			error = "protected text or translation is null";
			return false;
		}
		List<int> list = new List<int>();
		foreach (Match item in Regex.Matches(translated, Regex.Escape("__USCM_PROTECTED_") + "(\\d{4})__", RegexOptions.CultureInvariant))
		{
			if (!int.TryParse(item.Groups[1].Value, out var result) || result < 0 || result >= protectedSource.Tokens.Count)
			{
				error = "response contains an unknown protection marker";
				return false;
			}
			list.Add(result);
		}
		if (list.Count != protectedSource.Tokens.Count)
		{
			error = $"response protection marker count mismatch: expected {protectedSource.Tokens.Count}, got {list.Count}";
			return false;
		}
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < list.Count; i++)
		{
			int item = list[i];
			if (!hashSet.Add(item))
			{
				error = "response protection markers were duplicated";
				return false;
			}
			if (item != i)
			{
				error = "response protection markers were reordered";
				return false;
			}
		}
		restored = Regex.Replace(translated, Regex.Escape("__USCM_PROTECTED_") + "(\\d{4})__", (Match match) => protectedSource.Tokens[int.Parse(match.Groups[1].Value)], RegexOptions.CultureInvariant);
		return true;
	}
}
