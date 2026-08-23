using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopProtectedText
{
    internal WorkshopProtectedText(string text, IReadOnlyList<string> tokens)
    {
        Text = text;
        Tokens = tokens;
    }

    internal string Text { get; }
    internal IReadOnlyList<string> Tokens { get; }
}

internal static class WorkshopRichTextProtector
{
    private const string TokenPrefix = "__USCM_PROTECTED_";
    private static readonly Regex ProtectedPart = new(
        @"<[^>\r\n]+>|\{\d+\}|https?://[^\s<>]+|\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b|\b\d+\b|\r\n|\r|\n",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static WorkshopProtectedText Protect(string source)
    {
        List<string> tokens = new();
        string protectedText = ProtectedPart.Replace(source ?? string.Empty, match =>
        {
            tokens.Add(match.Value);
            return TokenPrefix + (tokens.Count - 1).ToString("D4") + "__";
        });
        return new WorkshopProtectedText(protectedText, tokens);
    }

    internal static bool TryRestore(
        WorkshopProtectedText protectedSource,
        string translated,
        out string restored,
        out string error)
    {
        restored = null;
        error = null;
        if (protectedSource == null || translated == null)
        {
            error = "protected text or translation is null";
            return false;
        }

        List<int> indexes = new();
        MatchCollection matches = Regex.Matches(
            translated,
            Regex.Escape(TokenPrefix) + @"(\d{4})__",
            RegexOptions.CultureInvariant);
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out int index)
                || index < 0
                || index >= protectedSource.Tokens.Count)
            {
                error = "response contains an unknown protection marker";
                return false;
            }
            indexes.Add(index);
        }

        if (indexes.Count != protectedSource.Tokens.Count)
        {
            error = $"response protection marker count mismatch: expected {protectedSource.Tokens.Count}, got {indexes.Count}";
            return false;
        }

        HashSet<int> seen = new();
        foreach (int index in indexes)
        {
            if (!seen.Add(index))
            {
                error = "response protection markers were duplicated";
                return false;
            }
        }

        restored = Regex.Replace(
            translated,
            Regex.Escape(TokenPrefix) + @"(\d{4})__",
            match => protectedSource.Tokens[int.Parse(match.Groups[1].Value)],
            RegexOptions.CultureInvariant);
        return true;
    }
}
