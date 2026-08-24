using System;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class WorkshopTextRecord
{
	public string Key { get; set; }

	public int SchemaVersion { get; set; }

	public string SourceLanguage { get; set; }

	public string TargetLanguage { get; set; }

	public WorkshopTextField Field { get; set; }

	public string SourceText { get; set; }

	public string Translation { get; set; }

	public string AssetName { get; set; }

	public string AssetGuid { get; set; }

	public ushort LegacyId { get; set; }

	public string ActionKey { get; set; }

	public string Provider { get; set; }

	public string Model { get; set; }

	public DateTime CreatedUtc { get; set; }

	public DateTime UpdatedUtc { get; set; }

	public DateTime LastUsedUtc { get; set; }
}
