using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopTranslationBatchResult
{
	internal bool Success { get; set; }

	internal IReadOnlyDictionary<string, string> Translations { get; set; }

	internal string Error { get; set; }

	internal WorkshopTranslationErrorKind ErrorKind { get; set; }

	internal int? StatusCode { get; set; }

	internal bool Retryable { get; set; }

	internal static WorkshopTranslationBatchResult Failed(string error, WorkshopTranslationErrorKind errorKind = WorkshopTranslationErrorKind.Unknown, bool retryable = false, int? statusCode = null)
	{
		return new WorkshopTranslationBatchResult
		{
			Success = false,
			Error = error,
			ErrorKind = errorKind,
			Retryable = retryable,
			StatusCode = statusCode,
			Translations = new Dictionary<string, string>(StringComparer.Ordinal)
		};
	}
}
