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

	internal string AssetType { get; }

	internal string LocalizationKey { get; }

	internal int SchemaVersion { get; }

	internal WorkshopTranslationWorkItem(string key, WorkshopTextField field, string sourceText, string assetName, string assetGuid, ushort legacyId, string actionKey, string assetType, string localizationKey, int schemaVersion)
	{
		Key = key;
		Field = field;
		SourceText = sourceText;
		AssetName = assetName;
		AssetGuid = assetGuid;
		LegacyId = legacyId;
		ActionKey = actionKey;
		AssetType = assetType;
		LocalizationKey = localizationKey;
		SchemaVersion = schemaVersion;
	}
}
