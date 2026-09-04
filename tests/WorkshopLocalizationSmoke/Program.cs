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
ProductionTranslationLifecycleSurvivesSessionChangesAndDefersAssetRefresh();
ProductionTranslationPumpRunsBeforeOtherRuntimeWork();
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

static void ProductionTranslationLifecycleSurvivesSessionChangesAndDefersAssetRefresh()
{
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string runtimeContext = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "PluginRuntimeContext.cs"));
    string translationService = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "WorkshopLocalizationService.cs"));
    string translationView = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.TranslationToolsView.cs"));
    string plugin = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "CheatMenuPlugin.cs"));
    int assetRefreshStart = translationService.IndexOf("internal void OnAssetsRefreshed()", StringComparison.Ordinal);
    int deferredRescanStart = assetRefreshStart < 0
        ? -1
        : translationService.IndexOf("private bool TryTakeDeferredAssetRescan()", assetRefreshStart, StringComparison.Ordinal);
    int reloadCacheStart = deferredRescanStart < 0
        ? -1
        : translationService.IndexOf("internal void ReloadCache()", deferredRescanStart, StringComparison.Ordinal);
    Check(assetRefreshStart >= 0 && deferredRescanStart > assetRefreshStart &&
          reloadCacheStart > deferredRescanStart,
        "Workshop asset-refresh method boundaries were not found.");
    string assetRefresh = translationService.Substring(assetRefreshStart, deferredRescanStart - assetRefreshStart);
    string deferredRescan = translationService.Substring(deferredRescanStart, reloadCacheStart - deferredRescanStart);
    int scanStart = translationService.IndexOf("internal void ScanCurrentAssets(bool allowAutoTranslate = true)", StringComparison.Ordinal);
    int assetRefreshMethodStart = translationService.IndexOf("internal void OnAssetsRefreshed()", scanStart, StringComparison.Ordinal);
    Check(scanStart >= 0 && assetRefreshMethodStart > scanStart,
        "Workshop manual-scan method boundaries were not found.");
    string manualScan = translationService.Substring(scanStart, assetRefreshMethodStart - scanStart);
    int pumpStart = translationService.IndexOf("internal void Pump()", reloadCacheStart, StringComparison.Ordinal);
    int disposeStart = pumpStart < 0
        ? -1
        : translationService.IndexOf("public void Dispose()", pumpStart, StringComparison.Ordinal);
    Check(pumpStart >= 0 && disposeStart > pumpStart,
        "Workshop Pump method boundaries were not found.");
    string pump = translationService.Substring(pumpStart, disposeStart - pumpStart);
    string compactAssetRefresh = Compact(assetRefresh);
    string compactDeferredRescan = Compact(deferredRescan);
    string compactPump = Compact(pump);
    int pluginAssetsRefreshedStart = plugin.IndexOf("internal void HandleAssetsRefreshed()", StringComparison.Ordinal);
    Check(pluginAssetsRefreshedStart >= 0,
        "CheatMenuPlugin asset-refresh handler was not found.");
    string pluginAssetsRefreshed = ExtractBraceBlock(plugin, pluginAssetsRefreshedStart);

    Check(!runtimeContext.Contains("_workshopLocalization?.CancelTranslation()", StringComparison.Ordinal),
        "Runtime session changes must never cancel Workshop translation.");
    Check(!assetRefresh.Contains("CancelTranslation()", StringComparison.Ordinal) &&
          compactAssetRefresh.Contains(
              "if(_translationTask!=null&&!_translationTask.IsCompleted){_deferredAssetRescan=true;return;}",
              StringComparison.Ordinal),
        "Automatic asset refresh must not cancel an active translation.");
    Check(translationService.Contains("private bool _deferredAssetRescan;", StringComparison.Ordinal) &&
          compactDeferredRescan.Contains(
              "if(!_deferredAssetRescan)returnfalse;if(_translationTask!=null&&!_translationTask.IsCompleted)returnfalse;_deferredAssetRescan=false;returnAutoScanOnAssetsRefreshed&&!_disposed;",
              StringComparison.Ordinal) &&
          CountOccurrences(pump, "TryTakeDeferredAssetRescan()") == 1 &&
          CountOccurrences(pump, "ScanCurrentAssets();") == 1 &&
          compactPump.Contains("if(TryTakeDeferredAssetRescan())ScanCurrentAssets();", StringComparison.Ordinal),
        "Asset refresh must set one deferred rescan, keep it pending during translation, and let Pump consume it exactly once.");
    Check(manualScan.Contains("_deferredAssetRescan = false;", StringComparison.Ordinal),
        "An explicit manual scan must consume any older deferred asset rescan instead of running a duplicate scan later.");
    Check(!translationService.Contains("GameplayFeatureAccessPolicy", StringComparison.Ordinal) &&
          !translationView.Contains("GameplayFeatureAccessPolicy", StringComparison.Ordinal),
        "Workshop translation must remain outside the multiplayer gameplay gate.");
    Check(pluginAssetsRefreshed.Contains("WorkshopLocalization?.OnAssetsRefreshed();", StringComparison.Ordinal) &&
          pluginAssetsRefreshed.Contains("RefreshCatalog(scanWorkshopLocalization: false);", StringComparison.Ordinal),
        "Every asset refresh must reach the deferred-rescan boundary even while a plugin window is open.");
}

static void ProductionTranslationPumpRunsBeforeOtherRuntimeWork()
{
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string runtimeContext = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "PluginRuntimeContext.cs"));
    int updateStart = runtimeContext.IndexOf("internal void Update()", StringComparison.Ordinal);
    int lateUpdateStart = runtimeContext.IndexOf("internal void LateUpdate()", updateStart, StringComparison.Ordinal);
    Check(updateStart >= 0 && lateUpdateStart > updateStart,
        "PluginRuntimeContext Update method boundaries were not found.");
    string update = runtimeContext.Substring(updateStart, lateUpdateStart - updateStart);
    int frameClaim = update.IndexOf("_lastRuntimeUpdateFrame = Time.frameCount;", StringComparison.Ordinal);
    int translationPump = update.IndexOf("_workshopLocalization?.Pump();", StringComparison.Ordinal);
    int sessionObservation = update.IndexOf("_plugin.ObserveRuntimeSession();", StringComparison.Ordinal);
    int iconPump = update.IndexOf("_icons.PumpOne()", StringComparison.Ordinal);
    int uiPump = update.IndexOf("_uiRoot?.Pump();", StringComparison.Ordinal);
    Check(frameClaim >= 0 && translationPump > frameClaim &&
          sessionObservation > translationPump && iconPump > translationPump && uiPump > translationPump,
        "Workshop translation Pump must be the first runtime operation after frame de-duplication so session, icon, or UI failures cannot starve it.");
    int workshopTry = update.IndexOf("try", frameClaim + "_lastRuntimeUpdateFrame = Time.frameCount;".Length,
        StringComparison.Ordinal);
    int workshopCatch = update.IndexOf("catch (Exception exception)", translationPump, StringComparison.Ordinal);
    string betweenFrameClaimAndTry = workshopTry < 0
        ? string.Empty
        : update.Substring(frameClaim + "_lastRuntimeUpdateFrame = Time.frameCount;".Length,
            workshopTry - frameClaim - "_lastRuntimeUpdateFrame = Time.frameCount;".Length);
    int workshopTryOpen = workshopTry < 0 ? -1 : update.IndexOf('{', workshopTry);
    int workshopTryClose = workshopTryOpen < 0 ? -1 : FindMatchingBrace(update, workshopTryOpen);
    int workshopCatchOpen = workshopCatch < 0 ? -1 : update.IndexOf('{', workshopCatch);
    int workshopCatchClose = workshopCatchOpen < 0 ? -1 : FindMatchingBrace(update, workshopCatchOpen);
    string workshopTryBody = workshopTryOpen < 0 || workshopTryClose < 0
        ? string.Empty
        : update.Substring(workshopTryOpen + 1, workshopTryClose - workshopTryOpen - 1);
    string workshopCatchBody = workshopCatchOpen < 0 || workshopCatchClose < 0
        ? string.Empty
        : update.Substring(workshopCatchOpen + 1, workshopCatchClose - workshopCatchOpen - 1);
    Check(workshopTry > frameClaim && workshopTry < translationPump && workshopCatch > translationPump &&
          workshopCatch < sessionObservation && string.IsNullOrWhiteSpace(betweenFrameClaimAndTry) &&
          workshopTryClose < workshopCatch && workshopCatchClose < sessionObservation &&
          Compact(workshopTryBody) ==
              "_workshopLocalization?.Pump();_hasLoggedWorkshopPumpFailure=false;" &&
          !workshopCatchBody.Contains("return;", StringComparison.Ordinal) &&
          !workshopCatchBody.Contains("throw", StringComparison.Ordinal),
        "Workshop translation Pump must have its own exception boundary before the remaining runtime graph runs.");
}

static string Compact(string text) => new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());

static string ExtractBraceBlock(string text, int searchStart)
{
    int open = text.IndexOf('{', searchStart);
    int close = open < 0 ? -1 : FindMatchingBrace(text, open);
    Check(open >= 0 && close > open, "A required source block has unbalanced braces.");
    return text.Substring(open + 1, close - open - 1);
}

static int FindMatchingBrace(string text, int open)
{
    int depth = 0;
    for (int index = open; index < text.Length; index++)
    {
        if (text[index] == '{') depth++;
        else if (text[index] == '}' && --depth == 0) return index;
    }
    return -1;
}

static int CountOccurrences(string text, string value)
{
    int count = 0;
    int offset = 0;
    while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += value.Length;
    }
    return count;
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
