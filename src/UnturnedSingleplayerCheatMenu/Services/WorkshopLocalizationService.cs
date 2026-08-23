using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationService : IDisposable
{
    private readonly object _gate = new();
    private readonly WorkshopLocalizationScanner _scanner;
    private readonly WorkshopTranslationCache _cache;
    private readonly IWorkshopTranslationClient _client;
    private readonly Action<string> _info;
    private readonly Action<string> _warn;
    private readonly List<WorkshopLocalizationCandidate> _candidates = new();
    private WorkshopLocalizationSnapshot _snapshot = WorkshopLocalizationSnapshot.Empty;
    private WorkshopLocalizationProgress _progress = new();
    private CancellationTokenSource _translationCancellation;
    private Task _translationTask;
    private PendingApply _pendingApply;
    private int _generation;
    private int _snapshotVersion;
    private bool _disposed;

    internal WorkshopLocalizationService(
        string cachePath,
        MachineTranslationSettings settings,
        Action<string> info,
        Action<string> warn,
        IWorkshopTranslationClient client = null)
    {
        Settings = settings ?? new MachineTranslationSettings();
        _info = info;
        _warn = warn;
        _cache = new WorkshopTranslationCache(cachePath, warn);
        _scanner = new WorkshopLocalizationScanner(warn);
        _client = client ?? new MachineTranslationClient();
    }

    internal MachineTranslationSettings Settings { get; }
    internal bool Enabled { get; set; }
    internal bool AutoScanOnAssetsRefreshed { get; set; } = true;
    internal bool AutoTranslateMissing { get; set; }
    internal bool ApplyToNativeUi { get; set; } = true;
    internal bool SkipLikelyTargetLanguageText { get; set; } = true;
    internal string CachePath => _cache.Path;
    internal int SnapshotVersion => Volatile.Read(ref _snapshotVersion);
    internal int CacheEntryCount => _cache.Count;
    internal bool ApiConfigured => Settings.IsConfigured;
    internal bool IsTranslationRunning
    {
        get
        {
            lock (_gate)
                return _translationTask != null && !_translationTask.IsCompleted;
        }
    }

    internal WorkshopLocalizationProgress Progress
    {
        get
        {
            lock (_gate)
                return _progress.Clone();
        }
    }

    internal bool TryGetOriginal(object instance, WorkshopTextField field, out string source)
    {
        WorkshopLocalizationSnapshot snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TryGetOriginal(instance, field, out source);
    }
    internal bool TryGetTranslation(object instance, WorkshopTextField field, out string translation)
    {
        WorkshopLocalizationSnapshot snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TryGet(instance, field, out translation);
    }

    internal bool ShouldPreserveNativeTranslation(object instance, WorkshopTextField field, string nativeText)
    {
        if (instance == null || string.IsNullOrWhiteSpace(nativeText))
            return false;

        WorkshopLocalizationSnapshot snapshot = Volatile.Read(ref _snapshot);
        if (!snapshot.TryGetOriginal(instance, field, out string originalText))
            return false;

        string normalizedNativeText = WorkshopTextIdentity.NormalizeSource(nativeText);
        if (string.Equals(normalizedNativeText, originalText, StringComparison.Ordinal))
            return false;

        return LooksLikeTargetLanguage(normalizedNativeText, Settings.TargetLanguage);
    }

    internal void ScanCurrentAssets(bool allowAutoTranslate = true)
    {
        if (_disposed)
            return;

        CancelTranslation();
        lock (_gate)
            _progress.State = WorkshopLocalizationState.Scanning;
        WorkshopLocalizationScanResult scan = _scanner.Scan(Settings.SourceLanguage, Settings.TargetLanguage);
        List<WorkshopLocalizationCandidate> candidates = scan.Candidates.ToList();
        lock (_gate)
        {
            _generation++;
            _candidates.Clear();
            _candidates.AddRange(candidates);
            SetProgressLocked(new WorkshopLocalizationProgress
            {
                State = WorkshopLocalizationState.Idle,
                ItemsScanned = scan.ItemCount,
                VehiclesScanned = scan.VehicleCount,
                TotalTexts = candidates.Select(candidate => candidate.Key).Distinct(StringComparer.Ordinal).Count(),
                Message = "scan complete"
            });
            PublishSnapshotLocked();
        }

        _info?.Invoke($"Asset localization scan complete: {scan.ItemCount} official/workshop items, {scan.VehicleCount} official/workshop vehicles, {_candidates.Count} text records.");
        if (allowAutoTranslate && AutoTranslateMissing && ApiConfigured)
            StartTranslation(force: false);
    }

    internal void OnAssetsRefreshed()
    {
        if (AutoScanOnAssetsRefreshed)
            ScanCurrentAssets();
        else
            CancelTranslation();
    }

    internal void ReloadCache()
    {
        if (_disposed)
            return;
        _cache.Reload();
        lock (_gate)
            PublishSnapshotLocked();
    }

    internal bool StartTranslation(bool force)
    {
        if (_disposed || !ApiConfigured)
        {
            _warn?.Invoke($"Workshop translation did not start: provider={Settings.ApiProtocol}, configured={ApiConfigured}, endpointSet={!string.IsNullOrWhiteSpace(Settings.Endpoint)}, apiKeySet={!string.IsNullOrWhiteSpace(Settings.ApiKey)}, secretKeyRequired={Settings.RequiresSecretKey}, modelRequired={Settings.IsLargeModelProvider}");
            SetMessage(WorkshopLocalizationState.Error, "translation API is not configured");
            return false;
        }

        lock (_gate)
        {
            if (_translationTask != null && !_translationTask.IsCompleted)
            {
                _info?.Invoke("Workshop translation did not start: a translation task is already running.");
                return false;
            }
        }

        List<WorkshopLocalizationCandidate> current;
        int generation;
        lock (_gate)
        {
            current = _candidates.ToList();
            generation = _generation;
        }

        List<WorkshopLocalizationCandidate> unique = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        int skippedTargetLanguageTexts = 0;
        bool cacheChanged = false;
        foreach (WorkshopLocalizationCandidate candidate in current)
        {
            if (!seen.Add(candidate.Key))
                continue;
            if (force)
            {
                _cache.Remove(candidate.Key);
                cacheChanged = true;
            }
            else if (_cache.TryGet(candidate.Key, out WorkshopTranslationCacheEntry cached))
            {
                if (!ShouldTreatCachedTranslationAsComplete(candidate, cached))
                {
                    _cache.Remove(candidate.Key);
                    cacheChanged = true;
                }
                else
                    continue;
            }
            if (!force && SkipLikelyTargetLanguageText && LooksLikeTargetLanguage(candidate.SourceText, Settings.TargetLanguage))
            {
                skippedTargetLanguageTexts++;
                continue;
            }
            unique.Add(candidate);
        }

        if (cacheChanged)
            _cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);

        lock (_gate)
        {
            PublishSnapshotLocked();
            _progress.PendingTranslations = unique.Count;
            _progress.SkippedTargetLanguageTexts = skippedTargetLanguageTexts;
            _progress.CompletedTranslations = 0;
            _progress.FailedTranslations = 0;
            _progress.State = unique.Count == 0 ? WorkshopLocalizationState.Idle : WorkshopLocalizationState.Translating;
            _progress.Message = unique.Count == 0 ? "no uncached text" : "translation started";
        }

        if (unique.Count == 0)
        {
            _info?.Invoke($"Workshop translation has no pending text: candidates={current.Count}, cacheEntries={_cache.Count}, force={force}.");
            return true;
        }

        CancellationTokenSource cancellation = new();
        lock (_gate)
        {
            _translationCancellation?.Cancel();
            _translationCancellation = cancellation;
            _translationTask = TranslateWorkerAsync(generation, unique.Select(candidate => new WorkshopTranslationWorkItem(candidate.Key, candidate.Field, candidate.SourceText, candidate.AssetName, candidate.AssetGuid, candidate.LegacyId, candidate.ActionKey)).ToList(), cancellation.Token);
            _info?.Invoke($"Workshop translation started: provider={Settings.ApiProtocol}, pending={unique.Count}, force={force}.");
        }
        return true;
    }

    internal void CancelTranslation()
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            cancellation = _translationCancellation;
            _translationCancellation = null;
            if (_translationTask != null && !_translationTask.IsCompleted)
            {
                _progress.State = WorkshopLocalizationState.Cancelled;
                _progress.Message = "translation cancelled";
            }
        }
        cancellation?.Cancel();
    }

    internal void Pump()
    {
        PendingApply pending = Interlocked.Exchange(ref _pendingApply, null);
        if (pending == null)
            return;

        lock (_gate)
        {
            if (pending.Generation != _generation)
                return;
            PublishSnapshotLocked();
            if (pending.Error != null)
            {
                _progress.State = WorkshopLocalizationState.Error;
                _progress.Message = pending.Error;
            }
            else if (_progress.State != WorkshopLocalizationState.Cancelled)
            {
                _progress.State = WorkshopLocalizationState.Idle;
                _progress.Message = pending.Changed ? "translation complete" : "translation produced no valid entries";
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CancelTranslation();
        if (_client is IDisposable disposable)
            disposable.Dispose();
        _cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
        lock (_gate)
        {
            _candidates.Clear();
            _snapshot = WorkshopLocalizationSnapshot.Empty;
        }
    }

    private async Task TranslateWorkerAsync(
        int generation,
        IReadOnlyList<WorkshopTranslationWorkItem> workItems,
        CancellationToken cancellationToken)
    {
        int batchSize = MachineTranslationSettings.NormalizeBatchSize(Settings.BatchSize);
        int completed = 0;
        int failed = 0;
        string error = null;
        try
        {
            for (int offset = 0; offset < workItems.Count; offset += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                List<WorkshopTranslationWorkItem> batch = workItems.Skip(offset).Take(batchSize).ToList();
                List<WorkshopTranslationRequest> requests = batch.Select(workItem =>
                {
                    WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect(workItem.SourceText);
                    return new WorkshopTranslationRequest(workItem.Key, workItem.Field, protectedText.Text);
                }).ToList();
                WorkshopTranslationBatchResult result = await _client.TranslateAsync(requests, Settings, cancellationToken).ConfigureAwait(false);
                if (!result.Success)
                {
                    if (batch.Count > 1)
                    {
                        _warn?.Invoke($"Workshop translation batch failed: {result.Error}; retrying {batch.Count} entries individually.");
                        foreach (WorkshopTranslationWorkItem singleItem in batch)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            WorkshopProtectedText singleProtected = WorkshopRichTextProtector.Protect(singleItem.SourceText);
                            WorkshopTranslationBatchResult singleResult = await _client.TranslateAsync(
                                new[] { new WorkshopTranslationRequest(singleItem.Key, singleItem.Field, singleProtected.Text) },
                                Settings,
                                cancellationToken).ConfigureAwait(false);
                            if (!singleResult.Success)
                            {
                                failed++;
                                _warn?.Invoke($"Workshop translation single-entry failed for {singleItem.Key}: {singleResult.Error}");
                                continue;
                            }

                            if (!TryApplyTranslationEntry(singleItem, singleResult, out string singleError))
                            {
                                failed++;
                                _warn?.Invoke($"Workshop translation entry rejected for {singleItem.Key}: {singleError}");
                                continue;
                            }
                            completed++;
                        }
                    }
                    else
                    {
                        failed += batch.Count;
                        _warn?.Invoke($"Workshop translation batch failed: {result.Error}");
                    }

                    _cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
                    UpdateTranslationProgress(workItems.Count, completed, failed);
                    continue;
                }

                foreach (WorkshopTranslationWorkItem workItem in batch)
                {
                    if (!TryApplyTranslationEntry(workItem, result, out string validationError))
                    {
                        failed++;
                        _warn?.Invoke($"Workshop translation entry rejected for {workItem.Key}: {validationError}");
                        continue;
                    }
                    completed++;
                }

                _cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
                UpdateTranslationProgress(workItems.Count, completed, failed);
            }
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (generation == _generation)
                {
                    _progress.State = WorkshopLocalizationState.Cancelled;
                    _progress.Message = "translation cancelled";
                }
            }
        }
        catch (Exception ex)
        {
            error = $"translation failed: {ex.Message}";
            _warn?.Invoke($"Workshop translation worker failed after {completed} completed and {failed} failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _pendingApply, new PendingApply(generation, completed > 0, error));
            lock (_gate)
            {
                _progress.CompletedTranslations = completed;
                _progress.FailedTranslations = failed;
                if (error != null && generation == _generation)
                {
                    _progress.State = WorkshopLocalizationState.Error;
                    _progress.Message = error;
                }
            }
        }
    }

    private bool TryApplyTranslationEntry(
        WorkshopTranslationWorkItem workItem,
        WorkshopTranslationBatchResult result,
        out string error)
    {
        error = null;
        if (!result.Translations.TryGetValue(workItem.Key, out string translated))
        {
            error = "response did not contain the requested id";
            return false;
        }

        WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect(workItem.SourceText);
        if (!WorkshopRichTextProtector.TryRestore(protectedText, translated, out string restored, out string validationError)
            || string.IsNullOrWhiteSpace(restored))
        {
            error = validationError ?? "empty translation";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        _cache.Upsert(new WorkshopTranslationCacheEntry
        {
            Key = workItem.Key,
            SchemaVersion = WorkshopTextIdentity.SchemaVersion,
            SourceLanguage = Settings.SourceLanguage,
            TargetLanguage = Settings.TargetLanguage,
            Field = workItem.Field,
            SourceText = workItem.SourceText,
            Translation = restored,
            AssetName = workItem.AssetName,
            AssetGuid = workItem.AssetGuid,
            LegacyId = workItem.LegacyId,
            ActionKey = workItem.ActionKey,
            ApiProtocol = Settings.ApiProtocol,
            Provider = Settings.ApiProtocol,
            Model = Settings.Model,
            CreatedUtc = now,
            UpdatedUtc = now,
            LastUsedUtc = now
        });
        return true;
    }

    private void SetProgressLocked(WorkshopLocalizationProgress progress)
    {
        _progress = progress ?? new WorkshopLocalizationProgress();
        _progress.Version = _snapshotVersion;
    }
    private void UpdateTranslationProgress(int total, int completed, int failed)
    {
        lock (_gate)
        {
            _progress.CompletedTranslations = completed;
            _progress.FailedTranslations = failed;
            _progress.PendingTranslations = Math.Max(0, total - completed - failed);
        }
    }

    private void PublishSnapshotLocked()
    {
        Dictionary<object, string> itemNames = new(ReferenceComparer.Instance);
        Dictionary<object, string> itemDescriptions = new(ReferenceComparer.Instance);
        Dictionary<object, string> vehicleNames = new(ReferenceComparer.Instance);
        Dictionary<object, string> actionTexts = new(ReferenceComparer.Instance);
        Dictionary<object, string> actionTooltips = new(ReferenceComparer.Instance);
        Dictionary<object, string> itemOriginalNames = new(ReferenceComparer.Instance);
        Dictionary<object, string> itemOriginalDescriptions = new(ReferenceComparer.Instance);
        Dictionary<object, string> vehicleOriginalNames = new(ReferenceComparer.Instance);
        Dictionary<object, string> actionOriginalTexts = new(ReferenceComparer.Instance);
        Dictionary<object, string> actionOriginalTooltips = new(ReferenceComparer.Instance);
        HashSet<string> touchedKeys = new(StringComparer.Ordinal);
        foreach (WorkshopLocalizationCandidate candidate in _candidates)
        {
            switch (candidate.Field)
            {
                case WorkshopTextField.ItemName: itemOriginalNames[candidate.Instance] = candidate.SourceText; break;
                case WorkshopTextField.ItemDescription: itemOriginalDescriptions[candidate.Instance] = candidate.SourceText; break;
                case WorkshopTextField.VehicleName: vehicleOriginalNames[candidate.Instance] = candidate.SourceText; break;
                case WorkshopTextField.ActionText: actionOriginalTexts[candidate.Instance] = candidate.SourceText; break;
                case WorkshopTextField.ActionTooltip: actionOriginalTooltips[candidate.Instance] = candidate.SourceText; break;
            }
            if (!_cache.TryGet(candidate.Key, out WorkshopTranslationCacheEntry entry)
                || IsCandidateAlreadyInTargetLanguage(candidate, Settings.TargetLanguage)
                || !string.Equals(
                    WorkshopTextIdentity.NormalizeSource(entry.SourceText),
                    WorkshopTextIdentity.NormalizeSource(candidate.SourceText),
                    StringComparison.Ordinal)
                || entry.Field != candidate.Field)
                continue;
            if (touchedKeys.Add(candidate.Key))
                _cache.Touch(candidate.Key);
            switch (candidate.Field)
            {
                case WorkshopTextField.ItemName: itemNames[candidate.Instance] = entry.Translation; break;
                case WorkshopTextField.ItemDescription: itemDescriptions[candidate.Instance] = entry.Translation; break;
                case WorkshopTextField.VehicleName: vehicleNames[candidate.Instance] = entry.Translation; break;
                case WorkshopTextField.ActionText: actionTexts[candidate.Instance] = entry.Translation; break;
                case WorkshopTextField.ActionTooltip: actionTooltips[candidate.Instance] = entry.Translation; break;
            }
        }
        _snapshot = new WorkshopLocalizationSnapshot(
            itemNames,
            itemDescriptions,
            vehicleNames,
            actionTexts,
            actionTooltips,
            itemOriginalNames,
            itemOriginalDescriptions,
            vehicleOriginalNames,
            actionOriginalTexts,
            actionOriginalTooltips);
        _snapshotVersion++;
        _progress.CacheHits = _candidates.Select(candidate => candidate.Key).Distinct(StringComparer.Ordinal).Count(key => _cache.TryGet(key, out _));
        _progress.Version = _snapshotVersion;
    }

    private void SetMessage(WorkshopLocalizationState state, string message)
    {
        lock (_gate)
        {
            _progress.State = state;
            _progress.Message = message;
        }
    }

    private bool ShouldTreatCachedTranslationAsComplete(
        WorkshopLocalizationCandidate candidate,
        WorkshopTranslationCacheEntry cached)
    {
        if (candidate == null || cached == null || string.IsNullOrWhiteSpace(cached.Translation))
            return false;
        if (IsCandidateAlreadyInTargetLanguage(candidate, Settings.TargetLanguage))
            return false;
        if (cached.Field != candidate.Field
            || !string.Equals(
                WorkshopTextIdentity.NormalizeSource(cached.SourceText),
                WorkshopTextIdentity.NormalizeSource(candidate.SourceText),
                StringComparison.Ordinal))
            return false;
        if (candidate.Field != WorkshopTextField.ItemName && candidate.Field != WorkshopTextField.VehicleName)
            return true;
        return !string.Equals(
            WorkshopTextIdentity.NormalizeSource(candidate.SourceText),
            WorkshopTextIdentity.NormalizeSource(cached.Translation),
            StringComparison.Ordinal);
    }

    private static bool IsCandidateAlreadyInTargetLanguage(
        WorkshopLocalizationCandidate candidate,
        string targetLanguage)
        => candidate != null && LooksLikeTargetLanguage(candidate.SourceText, targetLanguage);

    private static bool LooksLikeTargetLanguage(string text, string targetLanguage)
        => MachineTranslationSettings.LooksLikeTargetLanguage(text, targetLanguage);

    private sealed class PendingApply
    {
        internal PendingApply(int generation, bool changed, string error)
        {
            Generation = generation;
            Changed = changed;
            Error = error;
        }

        internal int Generation { get; }
        internal bool Changed { get; }
        internal string Error { get; }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();

        public new bool Equals(object left, object right) => ReferenceEquals(left, right);

        public int GetHashCode(object value) => value == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
