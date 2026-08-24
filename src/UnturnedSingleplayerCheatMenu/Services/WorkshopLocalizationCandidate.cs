using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationCandidate
{
	internal object Instance { get; set; }

	internal WorkshopTextField Field { get; set; }

	internal string SourceText { get; set; }

	internal string Key { get; set; }

	internal string AssetName { get; set; }

	internal string AssetGuid { get; set; }

	internal ushort LegacyId { get; set; }

	internal string ActionKey { get; set; }
}
