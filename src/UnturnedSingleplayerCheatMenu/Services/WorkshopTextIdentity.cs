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
		{
			return string.Empty;
		}
		return source.Replace("\r\n", "\n").Replace('\r', '\n').Trim()
			.Normalize(NormalizationForm.FormC);
	}

	internal static string CreateKey(string sourceLanguage, string targetLanguage, WorkshopTextField field, string source)
	{
		string s = string.Join("\n", 1.ToString(CultureInfo.InvariantCulture), sourceLanguage ?? string.Empty, targetLanguage ?? string.Empty, field.ToString(), NormalizeSource(source));
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", string.Empty).ToLowerInvariant();
	}
}
