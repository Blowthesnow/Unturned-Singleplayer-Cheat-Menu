using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopTranslationWorkItem
{
	internal string Key { get; }

	internal WorkshopTextField Field { get; }

	internal string SourceText { get; }

	internal string AssetName { get; }

	internal string AssetGuid { get; }

	internal ushort LegacyId { get; }

	internal string ActionKey { get; }

	internal WorkshopTranslationWorkItem(string key, WorkshopTextField field, string sourceText, string assetName, string assetGuid, ushort legacyId, string actionKey)
	{
		Key = key;
		Field = field;
		SourceText = sourceText;
		AssetName = assetName;
		AssetGuid = assetGuid;
		LegacyId = legacyId;
		ActionKey = actionKey;
	}
}
