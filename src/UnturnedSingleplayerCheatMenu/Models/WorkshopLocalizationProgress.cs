namespace UnturnedSingleplayerCheatMenu.Models;

internal enum WorkshopLocalizationState
{
    Idle,
    Scanning,
    Translating,
    Cancelled,
    Error
}

internal sealed class WorkshopLocalizationProgress
{
    public WorkshopLocalizationState State { get; internal set; }
    public int ItemsScanned { get; internal set; }
    public int VehiclesScanned { get; internal set; }
    public int TotalTexts { get; internal set; }
    public int CacheHits { get; internal set; }
    public int PendingTranslations { get; internal set; }
    public int SkippedTargetLanguageTexts { get; internal set; }
    public int FailedTranslations { get; internal set; }
    public int CompletedTranslations { get; internal set; }
    public string Message { get; internal set; }
    public int Version { get; internal set; }

    internal WorkshopLocalizationProgress Clone() => new()
    {
        State = State,
        ItemsScanned = ItemsScanned,
        VehiclesScanned = VehiclesScanned,
        TotalTexts = TotalTexts,
        CacheHits = CacheHits,
        PendingTranslations = PendingTranslations,
        SkippedTargetLanguageTexts = SkippedTargetLanguageTexts,
        FailedTranslations = FailedTranslations,
        CompletedTranslations = CompletedTranslations,
        Message = Message,
        Version = Version
    };
}
