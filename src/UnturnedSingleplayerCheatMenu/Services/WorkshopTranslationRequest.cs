using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopTranslationRequest
{
	internal string Id { get; }

	internal WorkshopTextField Field { get; }

	internal string SourceText { get; }

	internal WorkshopTranslationRequest(string id, WorkshopTextField field, string sourceText)
	{
		Id = id;
		Field = field;
		SourceText = sourceText;
	}
}
