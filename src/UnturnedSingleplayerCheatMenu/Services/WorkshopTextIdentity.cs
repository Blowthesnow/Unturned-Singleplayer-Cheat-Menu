using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class WorkshopTextIdentity
{
    internal const int SchemaVersion = 1;

    internal static string NormalizeSource(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        return source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim()
            .Normalize(NormalizationForm.FormC);
    }

    internal static string CreateKey(
        string sourceLanguage,
        string targetLanguage,
        WorkshopTextField field,
        string source)
    {
        string payload = string.Join("\n", new[]
        {
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            sourceLanguage ?? string.Empty,
            targetLanguage ?? string.Empty,
            field.ToString(),
            NormalizeSource(source)
        });

        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
