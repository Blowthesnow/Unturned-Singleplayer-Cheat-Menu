using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class WorkshopTranslationCacheDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string SourceLanguage { get; set; }
    public string TargetLanguage { get; set; }
    public List<WorkshopTranslationCacheEntry> Entries { get; set; } = new();
}
