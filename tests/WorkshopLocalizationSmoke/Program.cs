using System.Collections.Concurrent;
using BepInEx.Configuration;
using UnturnedSingleplayerCheatMenu.Services;

ApiKeysAreNormalizedAndCapped();
ApiKeysRoundTripThroughBepInExConfig();
WorkerSettingsKeepOneKeyPerParallelLane();
ParallelBatchSizeFeedsEveryAvailableWorker();
BatchSizeIsClampedToOneThroughOneHundred();
await TranslationBatchesRunInParallelExactlyOnce();
UnsupportedMonoConnectionLimitDoesNotAbortStartup();
ProductionConcurrencyUsesMonoCompatibilityBoundary();
ProductionServiceUsesTheParallelDispatcher();
ProductionUiUsesKeyRowsAndScrollableTranslationPage();
Console.WriteLine("Workshop localization smoke checks passed.");

static void ApiKeysAreNormalizedAndCapped()
{
    MachineTranslationSettings settings = new()
    {
        ApiKey = " first-key\nsecond-key;third-key,fourth-key\r\nfifth-key\nsixth-key\nsecond-key "
    };

    Check(settings.ApiKeys.SequenceEqual(new[]
    {
        "first-key", "second-key"
    }), "API keys were not trimmed, de-duplicated, ordered, and capped at two.");
    Check(settings.ApiKeyCount == MachineTranslationSettings.MaxApiKeyCount,
        "The configured API-key count does not respect the two-key maximum.");
}

static void WorkerSettingsKeepOneKeyPerParallelLane()
{
    MachineTranslationSettings settings = new()
    {
        ApiProtocol = MachineTranslationSettings.DeepSeekProvider,
        Endpoint = "https://example.invalid/v1/chat/completions",
        ApiKey = "key-a\nkey-b\nkey-c",
        Model = "test-model",
        BatchSize = 7,
        TimeoutSeconds = 45,
        MaxRetries = 4
    };

    IReadOnlyList<MachineTranslationSettings> workers = settings.CreateApiKeyWorkerSettings();
    Check(workers.Count == MachineTranslationSettings.MaxApiKeyCount, "Parallel translation did not cap workers at two API keys.");
    Check(workers.Select(worker => worker.ApiKey).SequenceEqual(new[] { "key-a", "key-b" }),
        "A parallel worker received zero or multiple API keys.");
    Check(workers.All(worker => worker.Endpoint == settings.Endpoint &&
                                worker.Model == settings.Model &&
                                worker.BatchSize == settings.BatchSize &&
                                worker.TimeoutSeconds == settings.TimeoutSeconds &&
                                worker.MaxRetries == settings.MaxRetries),
        "Worker settings did not preserve the active translation configuration.");
}

static void ApiKeysRoundTripThroughBepInExConfig()
{
    string directory = Path.Combine(Path.GetTempPath(), "UnturnedSingleplayerCheatMenu.WorkshopLocalizationSmoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "translation.cfg");
        MachineTranslationSettings expected = new() { ApiKey = "key-a\nkey-b\nkey-c" };
        ConfigFile config = new(path, saveOnInit: false);
        config.Bind("WorkshopLocalization", "ApiKey", string.Empty).Value = expected.ApiKey;
        config.Save();

        ConfigFile reloaded = new(path, saveOnInit: true);
        string persisted = reloaded.Bind("WorkshopLocalization", "ApiKey", string.Empty).Value;
        MachineTranslationSettings actual = new() { ApiKey = persisted };
        Check(actual.ApiKeys.SequenceEqual(expected.ApiKeys),
            "Multiple API keys did not survive a real BepInEx ConfigFile save/reload round trip.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void ParallelBatchSizeFeedsEveryAvailableWorker()
{
    Check(MachineTranslationSettings.ResolveParallelBatchSize(100, 50, MachineTranslationSettings.MaxApiKeyCount) == 50,
        "One hundred items were not split into two parallel batches.");
    Check(MachineTranslationSettings.ResolveParallelBatchSize(3, 50, MachineTranslationSettings.MaxApiKeyCount) == 2,
        "Small workloads were not split across every useful worker.");
    Check(MachineTranslationSettings.ResolveParallelBatchSize(3098, 100, MachineTranslationSettings.MaxApiKeyCount) == 100,
        "Large workloads should preserve the configured maximum batch size.");
}

static void BatchSizeIsClampedToOneThroughOneHundred()
{
    Check(MachineTranslationSettings.NormalizeBatchSize(0) == 1,
        "Translation batch size was not clamped to the lower bound of one.");
    Check(MachineTranslationSettings.NormalizeBatchSize(100) == 100,
        "Translation batch size rejected the upper bound of one hundred.");
    Check(MachineTranslationSettings.NormalizeBatchSize(101) == 100,
        "Translation batch size exceeded the upper bound of one hundred.");
}

static async Task TranslationBatchesRunInParallelExactlyOnce()
{
    string[] workers = { "key-a", "key-b" };
    int[] batches = Enumerable.Range(0, 12).ToArray();
    ConcurrentDictionary<int, int> visits = new();
    ConcurrentDictionary<string, int> workerVisits = new(StringComparer.Ordinal);
    TaskCompletionSource<bool> allWorkersEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int entered = 0;
    int running = 0;
    int maxRunning = 0;

    await ParallelTranslationDispatcher.RunAsync(workers, batches, async (worker, batch, cancellationToken) =>
    {
        visits.AddOrUpdate(batch, 1, static (_, count) => count + 1);
        workerVisits.AddOrUpdate(worker, 1, static (_, count) => count + 1);
        int current = Interlocked.Increment(ref running);
        UpdateMaximum(ref maxRunning, current);
        if (Interlocked.Increment(ref entered) == workers.Length)
            allWorkersEntered.TrySetResult(true);
        await allWorkersEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        await Task.Delay(5, cancellationToken);
        Interlocked.Decrement(ref running);
    }, CancellationToken.None);

    Check(visits.Count == batches.Length && visits.All(pair => pair.Value == 1),
        "A translation batch was skipped or dispatched more than once.");
    Check(workerVisits.Keys.OrderBy(value => value).SequenceEqual(workers),
        "Not every configured API key received translation work.");
    Check(maxRunning == workers.Length,
        "Translation work did not run concurrently across all configured API keys.");
}

static void UpdateMaximum(ref int maximum, int value)
{
    int observed;
    while (value > (observed = Volatile.Read(ref maximum)) &&
           Interlocked.CompareExchange(ref maximum, value, observed) != observed)
    {
    }
}

static void ProductionServiceUsesTheParallelDispatcher()
{
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string service = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "WorkshopLocalizationService.cs"));
    Check(service.Contains("Settings.CreateApiKeyWorkerSettings()", StringComparison.Ordinal),
        "The production translation service does not create one worker configuration per API key.");
    Check(service.Contains("ParallelTranslationDispatcher.RunAsync(workerSettings, batches", StringComparison.Ordinal),
        "The production translation service does not dispatch batches across the API-key workers.");
    Check(!service.Contains("_client.TranslateAsync(requests, Settings, cancellationToken)", StringComparison.Ordinal),
        "The production service still routes every batch through the shared mutable settings instance.");
}

static void ProductionConcurrencyUsesMonoCompatibilityBoundary()
{
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string client = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "MachineTranslationClient.cs"));
    string service = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "WorkshopLocalizationService.cs"));
    Check(client.Contains("HttpClientHandlerCompatibility.TrySetMaxConnectionsPerServer", StringComparison.Ordinal) &&
          !client.Contains("MaxConnectionsPerServer = MachineTranslationSettings.MaxApiKeyCount", StringComparison.Ordinal),
		"The production HTTP handler does not configure concurrency through the Unity/Mono compatibility boundary.");
    Check(service.Contains("ResolveParallelBatchSize(workItems.Count", StringComparison.Ordinal),
        "The production service can still create fewer batches than configured API-key workers.");
}

static void UnsupportedMonoConnectionLimitDoesNotAbortStartup()
{
    int configured = 0;
    Check(HttpClientHandlerCompatibility.TrySetMaxConnectionsPerServer(value => configured = value, MachineTranslationSettings.MaxApiKeyCount) && configured == MachineTranslationSettings.MaxApiKeyCount,
        "A supported HTTP handler did not receive the requested connection limit.");
    Check(!HttpClientHandlerCompatibility.TrySetMaxConnectionsPerServer(_ => throw new NotImplementedException(), MachineTranslationSettings.MaxApiKeyCount),
        "Mono's unimplemented connection-limit setter was allowed to abort plugin startup.");
    Check(!HttpClientHandlerCompatibility.TrySetMaxConnectionsPerServer(_ => throw new PlatformNotSupportedException(), MachineTranslationSettings.MaxApiKeyCount),
        "An unsupported platform connection-limit setter was allowed to abort plugin startup.");
}

static void ProductionUiUsesKeyRowsAndScrollableTranslationPage()
{
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string translationView = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.TranslationToolsView.cs"));
    string toolsShell = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.ToolsShell.cs"));
    Check(translationView.Contains("private void CreateWorkshopApiKeyRows(", StringComparison.Ordinal) &&
          translationView.Contains("MachineTranslationSettings.MaxApiKeyCount", StringComparison.Ordinal) &&
          translationView.Contains("CreateButton(row.transform, \"+\"", StringComparison.Ordinal) &&
          translationView.Contains("CreateButton(row.transform, \"×\"", StringComparison.Ordinal),
        "The production translation UI does not expose add/remove API-key rows with the two-key limit.");
    Check(toolsShell.Contains("tab == ToolsTab.Translation", StringComparison.Ordinal) &&
          toolsShell.Contains("CreateShortcutScroll(panel.transform)", StringComparison.Ordinal) &&
          toolsShell.Contains("AddShortcutScrollbar", StringComparison.Ordinal) &&
          toolsShell.Contains("AddShortcutScrollLayout", StringComparison.Ordinal),
        "The production translation UI does not use the global-shortcut vertical scroll layout.");
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
