using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationService : IDisposable
{
	private sealed class PendingApply
	{
		internal int Generation { get; }

		internal bool Changed { get; }

		internal string Error { get; }

		internal PendingApply(int generation, bool changed, string error)
		{
			Generation = generation;
			Changed = changed;
			Error = error;
		}
	}

	private sealed class TranslationCounters
	{
		internal int Completed;

		internal int Failed;
	}

	private sealed class ReferenceComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceComparer Instance = new ReferenceComparer();

		public new bool Equals(object left, object right)
		{
			return left == right;
		}

		public int GetHashCode(object value)
		{
			if (value != null)
			{
				return RuntimeHelpers.GetHashCode(value);
			}
			return 0;
		}
	}

	private readonly object _gate = new object();

	private readonly object _cacheSaveGate = new object();

	private readonly WorkshopLocalizationScanner _scanner;

	private readonly WorkshopTranslationCache _cache;

	private readonly IWorkshopTranslationClient _client;

	private readonly Action<string> _info;

	private readonly Action<string> _warn;

	private readonly List<WorkshopLocalizationCandidate> _candidates = new List<WorkshopLocalizationCandidate>();

	private WorkshopLocalizationSnapshot _snapshot = WorkshopLocalizationSnapshot.Empty;

	private WorkshopLocalizationProgress _progress = new WorkshopLocalizationProgress();

	private CancellationTokenSource _translationCancellation;

	private Task _translationTask;

	private PendingApply _pendingApply;

	private int _generation;

	private int _snapshotVersion;

	private int _presentationVersion;

	private bool _deferredAssetRescan;

	private bool _disposed;

	internal MachineTranslationSettings Settings { get; }

	internal bool Enabled { get; set; }

	internal bool AutoScanOnAssetsRefreshed { get; set; } = true;

	internal bool AutoTranslateMissing { get; set; }

	internal bool ApplyToNativeUi { get; set; } = true;

	internal bool SkipLikelyTargetLanguageText { get; set; } = true;

	internal string CachePath => _cache.Path;

	internal int SnapshotVersion => Volatile.Read(ref _snapshotVersion);

	internal int PresentationVersion => Volatile.Read(ref _presentationVersion);

	internal int CacheEntryCount => _cache.Count;

	internal bool ApiConfigured => Settings.IsConfigured;

	internal bool IsTranslationRunning
	{
		get
		{
			lock (_gate)
			{
				return _translationTask != null && !_translationTask.IsCompleted;
			}
		}
	}

	internal WorkshopLocalizationProgress Progress
	{
		get
		{
			lock (_gate)
			{
				return _progress.Clone();
			}
		}
	}

	internal WorkshopLocalizationService(string cachePath, MachineTranslationSettings settings, Action<string> info, Action<string> warn, IWorkshopTranslationClient client = null)
	{
		Settings = settings ?? new MachineTranslationSettings();
		_info = info;
		_warn = warn;
		_cache = new WorkshopTranslationCache(cachePath, warn);
		_scanner = new WorkshopLocalizationScanner(warn);
		_client = client ?? new MachineTranslationClient();
	}

	internal bool TryGetOriginal(object instance, WorkshopTextField field, out string source)
	{
		return Volatile.Read(ref _snapshot).TryGetOriginal(instance, field, out source);
	}

	internal bool TryGetTranslation(object instance, WorkshopTextField field, out string translation)
	{
		return Volatile.Read(ref _snapshot).TryGet(instance, field, out translation);
	}

	internal bool ShouldPreserveNativeTranslation(object instance, WorkshopTextField field, string nativeText)
	{
		if (instance == null || string.IsNullOrWhiteSpace(nativeText))
		{
			return false;
		}
		if (!Volatile.Read(ref _snapshot).TryGetOriginal(instance, field, out var original))
		{
			return false;
		}
		string text = WorkshopTextIdentity.NormalizeSource(nativeText);
		if (string.Equals(text, original, StringComparison.Ordinal))
		{
			return false;
		}
		return LooksLikeTargetLanguage(text, Settings.TargetLanguage);
	}

	internal void ScanCurrentAssets(bool allowAutoTranslate = true)
	{
		if (_disposed)
		{
			return;
		}
		lock (_gate)
		{
			_deferredAssetRescan = false;
		}
		CancelTranslation();
		lock (_gate)
		{
			_progress.State = WorkshopLocalizationState.Scanning;
			MarkPresentationChangedLocked();
		}
		WorkshopLocalizationScanResult workshopLocalizationScanResult = _scanner.Scan(Settings.SourceLanguage, Settings.TargetLanguage);
		List<WorkshopLocalizationCandidate> list = workshopLocalizationScanResult.Candidates.ToList();
		lock (_gate)
		{
			_generation++;
			_candidates.Clear();
			_candidates.AddRange(list);
			SetProgressLocked(new WorkshopLocalizationProgress
			{
				State = WorkshopLocalizationState.Idle,
				ItemsScanned = workshopLocalizationScanResult.ItemCount,
				VehiclesScanned = workshopLocalizationScanResult.VehicleCount,
				TotalTexts = list.Select((WorkshopLocalizationCandidate candidate) => candidate.Key).Distinct<string>(StringComparer.Ordinal).Count(),
				Message = "scan complete"
			});
			PublishSnapshotLocked();
		}
		_info?.Invoke($"Asset localization scan complete: {workshopLocalizationScanResult.ItemCount} official/workshop items, {workshopLocalizationScanResult.VehicleCount} official/workshop vehicles, {_candidates.Count} text records.");
		if (allowAutoTranslate && AutoTranslateMissing && ApiConfigured)
		{
			StartTranslation(force: false);
		}
	}

	internal void OnAssetsRefreshed()
	{
		if (_disposed || !AutoScanOnAssetsRefreshed)
		{
			return;
		}
		lock (_gate)
		{
			if (_translationTask != null && !_translationTask.IsCompleted)
			{
				_deferredAssetRescan = true;
				return;
			}
		}
		ScanCurrentAssets();
	}

	private bool TryTakeDeferredAssetRescan()
	{
		lock (_gate)
		{
			if (!_deferredAssetRescan)
				return false;
			if (_translationTask != null && !_translationTask.IsCompleted)
				return false;
			_deferredAssetRescan = false;
			return AutoScanOnAssetsRefreshed && !_disposed;
		}
	}

	internal void ReloadCache()
	{
		if (_disposed)
		{
			return;
		}
		_cache.Reload();
		lock (_gate)
		{
			PublishSnapshotLocked();
		}
	}

	internal void NotifySettingsChanged()
	{
		lock (_gate)
		{
			MarkPresentationChangedLocked();
		}
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
		List<WorkshopLocalizationCandidate> list;
		int generation;
		lock (_gate)
		{
			list = _candidates.ToList();
			generation = _generation;
		}
		List<WorkshopLocalizationCandidate> list2 = new List<WorkshopLocalizationCandidate>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		int num = 0;
		bool flag = false;
		foreach (WorkshopLocalizationCandidate item in list)
		{
			if (!hashSet.Add(item.Key))
			{
				continue;
			}
			WorkshopTranslationCacheEntry entry;
			if (force)
			{
				_cache.Remove(item.Key);
				flag = true;
			}
			else if (_cache.TryGet(item.Key, out entry))
			{
				if (ShouldTreatCachedTranslationAsComplete(item, entry))
				{
					continue;
				}
				_cache.Remove(item.Key);
				flag = true;
			}
			if (!force && SkipLikelyTargetLanguageText && LooksLikeTargetLanguage(item.SourceText, Settings.TargetLanguage))
			{
				num++;
			}
			else
			{
				list2.Add(item);
			}
		}
		if (flag)
		{
			_cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
		}
		lock (_gate)
		{
			PublishSnapshotLocked();
			_progress.PendingTranslations = list2.Count;
			_progress.SkippedTargetLanguageTexts = num;
			_progress.CompletedTranslations = 0;
			_progress.FailedTranslations = 0;
			_progress.State = ((list2.Count != 0) ? WorkshopLocalizationState.Translating : WorkshopLocalizationState.Idle);
			_progress.Message = ((list2.Count == 0) ? "no uncached text" : "translation started");
			MarkPresentationChangedLocked();
		}
		if (list2.Count == 0)
		{
			_info?.Invoke($"Workshop translation has no pending text: candidates={list.Count}, cacheEntries={_cache.Count}, force={force}.");
			return true;
		}
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		IReadOnlyList<MachineTranslationSettings> workerSettings = Settings.CreateApiKeyWorkerSettings();
		lock (_gate)
		{
			_translationCancellation?.Cancel();
			_translationCancellation = cancellationTokenSource;
			_translationTask = TranslateWorkerAsync(generation, list2.Select((WorkshopLocalizationCandidate candidate) => new WorkshopTranslationWorkItem(candidate.Key, candidate.Field, candidate.SourceText, candidate.AssetName, candidate.AssetGuid, candidate.LegacyId, candidate.ActionKey, candidate.AssetType, candidate.LocalizationKey, candidate.SchemaVersion)).ToList(), workerSettings, cancellationTokenSource.Token);
			_info?.Invoke($"Workshop translation started: provider={Settings.ApiProtocol}, pending={list2.Count}, workers={workerSettings.Count}, force={force}.");
		}
		return true;
	}

	internal void CancelTranslation()
	{
		CancellationTokenSource translationCancellation;
		lock (_gate)
		{
			translationCancellation = _translationCancellation;
			_translationCancellation = null;
			if (_translationTask != null && !_translationTask.IsCompleted)
			{
				_progress.State = WorkshopLocalizationState.Cancelled;
				_progress.Message = "translation cancelled";
				MarkPresentationChangedLocked();
			}
		}
		translationCancellation?.Cancel();
	}

	internal void Pump()
	{
		PendingApply pendingApply = Interlocked.Exchange(ref _pendingApply, null);
		if (pendingApply != null)
		{
			lock (_gate)
			{
				if (pendingApply.Generation == _generation)
				{
					PublishSnapshotLocked();
					if (pendingApply.Error != null)
					{
						_progress.State = WorkshopLocalizationState.Error;
						_progress.Message = pendingApply.Error;
					}
					else if (_progress.State != WorkshopLocalizationState.Cancelled)
					{
						_progress.State = WorkshopLocalizationState.Idle;
						_progress.Message = (pendingApply.Changed ? "translation complete" : "translation produced no valid entries");
					}
					MarkPresentationChangedLocked();
				}
			}
		}
		if (TryTakeDeferredAssetRescan())
			ScanCurrentAssets();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		CancelTranslation();
		if (_client is IDisposable disposable)
		{
			disposable.Dispose();
		}
		_cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
		lock (_gate)
		{
			_candidates.Clear();
			_snapshot = WorkshopLocalizationSnapshot.Empty;
		}
	}

	private async Task TranslateWorkerAsync(int generation, IReadOnlyList<WorkshopTranslationWorkItem> workItems, IReadOnlyList<MachineTranslationSettings> workerSettings, CancellationToken cancellationToken)
	{
		int batchSize = MachineTranslationSettings.ResolveParallelBatchSize(workItems.Count, workerSettings[0].BatchSize, workerSettings.Count);
		TranslationCounters counters = new TranslationCounters();
		string error = null;
		try
		{
			List<IReadOnlyList<WorkshopTranslationWorkItem>> batches = new List<IReadOnlyList<WorkshopTranslationWorkItem>>();
			for (int offset = 0; offset < workItems.Count; offset += batchSize)
			{
				batches.Add(workItems.Skip(offset).Take(batchSize).ToList());
			}
			_info?.Invoke($"Workshop translation dispatch: workers={workerSettings.Count}, batches={batches.Count}, effectiveConcurrency={Math.Min(workerSettings.Count, batches.Count)}, batchSize={batchSize}.");
			await ParallelTranslationDispatcher.RunAsync(workerSettings, batches, (settings, batch, token) => TranslateBatchAsync(workItems.Count, batch, settings, counters, token), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
			lock (_gate)
			{
				if (generation == _generation)
				{
					_progress.State = WorkshopLocalizationState.Cancelled;
					_progress.Message = "translation cancelled";
					MarkPresentationChangedLocked();
				}
			}
		}
		catch (Exception ex2)
		{
			error = "translation failed: " + ex2.Message;
			_warn?.Invoke($"Workshop translation worker failed after {Volatile.Read(ref counters.Completed)} completed and {Volatile.Read(ref counters.Failed)} failed: {ex2}");
		}
		finally
		{
			int completed = Volatile.Read(ref counters.Completed);
			int failed = Volatile.Read(ref counters.Failed);
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
				MarkPresentationChangedLocked();
			}
		}
	}

	private async Task TranslateBatchAsync(int total, IReadOnlyList<WorkshopTranslationWorkItem> batch, MachineTranslationSettings workerSettings, TranslationCounters counters, CancellationToken cancellationToken)
	{
		List<WorkshopTranslationRequest> requests = batch.Select(delegate(WorkshopTranslationWorkItem workItem)
		{
			WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect(workItem.SourceText);
			return new WorkshopTranslationRequest(workItem.Key, workItem.Field, protectedText.Text);
		}).ToList();
		WorkshopTranslationBatchResult result = await _client.TranslateAsync(requests, workerSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!result.Success)
		{
			if (batch.Count > 1)
			{
				_warn?.Invoke($"Workshop translation batch failed: {result.Error}; retrying {batch.Count} entries individually.");
				foreach (WorkshopTranslationWorkItem singleItem in batch)
				{
					cancellationToken.ThrowIfCancellationRequested();
					WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect(singleItem.SourceText);
					WorkshopTranslationBatchResult singleResult = await _client.TranslateAsync(new WorkshopTranslationRequest[1]
					{
						new WorkshopTranslationRequest(singleItem.Key, singleItem.Field, protectedText.Text)
					}, workerSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (!singleResult.Success)
					{
						Interlocked.Increment(ref counters.Failed);
						_warn?.Invoke("Workshop translation single-entry failed for " + singleItem.Key + ": " + singleResult.Error);
					}
					else if (!TryApplyTranslationEntry(singleItem, singleResult, workerSettings, out string applyError))
					{
						Interlocked.Increment(ref counters.Failed);
						_warn?.Invoke("Workshop translation entry rejected for " + singleItem.Key + ": " + applyError);
					}
					else
					{
						Interlocked.Increment(ref counters.Completed);
					}
				}
			}
			else
			{
				Interlocked.Add(ref counters.Failed, batch.Count);
				_warn?.Invoke("Workshop translation batch failed: " + result.Error);
			}
		}
		else
		{
			foreach (WorkshopTranslationWorkItem item in batch)
			{
				if (!TryApplyTranslationEntry(item, result, workerSettings, out string applyError))
				{
					Interlocked.Increment(ref counters.Failed);
					_warn?.Invoke("Workshop translation entry rejected for " + item.Key + ": " + applyError);
				}
				else
				{
					Interlocked.Increment(ref counters.Completed);
				}
			}
		}
		SaveTranslationCache(workerSettings);
		UpdateTranslationProgress(total, counters);
	}

	private bool TryApplyTranslationEntry(WorkshopTranslationWorkItem workItem, WorkshopTranslationBatchResult result, MachineTranslationSettings workerSettings, out string error)
	{
		error = null;
		if (!result.Translations.TryGetValue(workItem.Key, out var value))
		{
			error = "response did not contain the requested id";
			return false;
		}
		if (!WorkshopRichTextProtector.TryRestore(WorkshopRichTextProtector.Protect(workItem.SourceText), value, out var restored, out var error2) || string.IsNullOrWhiteSpace(restored))
		{
			error = error2 ?? "empty translation";
			return false;
		}
		DateTime utcNow = DateTime.UtcNow;
		_cache.Upsert(new WorkshopTranslationCacheEntry
		{
			Key = workItem.Key,
			SchemaVersion = workItem.SchemaVersion,
			SourceLanguage = workerSettings.SourceLanguage,
			TargetLanguage = workerSettings.TargetLanguage,
			Field = workItem.Field,
			SourceText = workItem.SourceText,
			Translation = restored,
			AssetName = workItem.AssetName,
			AssetGuid = workItem.AssetGuid,
			LegacyId = workItem.LegacyId,
			ActionKey = workItem.ActionKey,
			AssetType = workItem.AssetType,
			LocalizationKey = workItem.LocalizationKey,
			ApiProtocol = workerSettings.ApiProtocol,
			Provider = workerSettings.ApiProtocol,
			Model = workerSettings.Model,
			CreatedUtc = utcNow,
			UpdatedUtc = utcNow,
			LastUsedUtc = utcNow
		});
		return true;
	}

	private void SaveTranslationCache(MachineTranslationSettings workerSettings)
	{
		lock (_cacheSaveGate)
		{
			_cache.Save(workerSettings.SourceLanguage, workerSettings.TargetLanguage);
		}
	}

	private void SetProgressLocked(WorkshopLocalizationProgress progress)
	{
		_progress = progress ?? new WorkshopLocalizationProgress();
		MarkPresentationChangedLocked();
	}

	private void PublishSnapshotLocked()
	{
		Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> dictionary = new Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>>(ReferenceComparer.Instance);
		Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> dictionary2 = new Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>>(ReferenceComparer.Instance);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (WorkshopLocalizationCandidate candidate in _candidates)
		{
			AddSnapshotValue(dictionary2, candidate.Instance, candidate.Field, candidate.SourceText);
			if (_cache.TryGet(candidate.Key, out var entry) && !IsCandidateAlreadyInTargetLanguage(candidate, Settings.TargetLanguage) && IsCacheEntryMatch(candidate, entry))
			{
				if (hashSet.Add(candidate.Key))
				{
					_cache.Touch(candidate.Key);
				}
				AddSnapshotValue(dictionary, candidate.Instance, candidate.Field, entry.Translation);
			}
		}
		_snapshot = new WorkshopLocalizationSnapshot(dictionary, dictionary2);
		_snapshotVersion++;
		_progress.CacheHits = _candidates.Select((WorkshopLocalizationCandidate candidate) => candidate.Key).Distinct<string>(StringComparer.Ordinal).Count((string key) => _cache.TryGet(key, out var _));
		MarkPresentationChangedLocked();
	}

	private void SetMessage(WorkshopLocalizationState state, string message)
	{
		lock (_gate)
		{
			_progress.State = state;
			_progress.Message = message;
			MarkPresentationChangedLocked();
		}
	}

	private void UpdateTranslationProgress(int total, TranslationCounters counters)
	{
		lock (_gate)
		{
			int completed = Volatile.Read(ref counters.Completed);
			int failed = Volatile.Read(ref counters.Failed);
			_progress.CompletedTranslations = completed;
			_progress.FailedTranslations = failed;
			_progress.PendingTranslations = Math.Max(0, total - completed - failed);
			MarkPresentationChangedLocked();
		}
	}

	private void MarkPresentationChangedLocked()
	{
		int version = Interlocked.Increment(ref _presentationVersion);
		_progress.Version = version;
	}

	private bool ShouldTreatCachedTranslationAsComplete(WorkshopLocalizationCandidate candidate, WorkshopTranslationCacheEntry cached)
	{
		if (candidate == null || cached == null || string.IsNullOrWhiteSpace(cached.Translation))
		{
			return false;
		}
		if (IsCandidateAlreadyInTargetLanguage(candidate, Settings.TargetLanguage))
		{
			return false;
		}
		if (!IsCacheEntryMatch(candidate, cached))
		{
			return false;
		}
		if (candidate.Field != WorkshopTextField.ItemName && candidate.Field != WorkshopTextField.VehicleName)
		{
			return true;
		}
		return !string.Equals(WorkshopTextIdentity.NormalizeSource(candidate.SourceText), WorkshopTextIdentity.NormalizeSource(cached.Translation), StringComparison.Ordinal);
	}

	private static void AddSnapshotValue(Dictionary<object, IReadOnlyDictionary<WorkshopTextField, string>> lookup, object instance, WorkshopTextField field, string value)
	{
		if (instance == null)
		{
			return;
		}
		if (!lookup.TryGetValue(instance, out var readOnlyValues))
		{
			readOnlyValues = new Dictionary<WorkshopTextField, string>();
			lookup.Add(instance, readOnlyValues);
		}
		((Dictionary<WorkshopTextField, string>)readOnlyValues)[field] = value;
	}

	private static bool IsCacheEntryMatch(WorkshopLocalizationCandidate candidate, WorkshopTranslationCacheEntry entry)
	{
		if (entry == null || entry.SchemaVersion != candidate.SchemaVersion || entry.Field != candidate.Field || !string.Equals(WorkshopTextIdentity.NormalizeSource(entry.SourceText), WorkshopTextIdentity.NormalizeSource(candidate.SourceText), StringComparison.Ordinal))
		{
			return false;
		}
		if (candidate.SchemaVersion != WorkshopTextIdentity.AssetScopedSchemaVersion)
		{
			return true;
		}
		return string.Equals(entry.AssetType, candidate.AssetType, StringComparison.Ordinal) && string.Equals(entry.AssetGuid, candidate.AssetGuid, StringComparison.Ordinal) && string.Equals(entry.LocalizationKey, candidate.LocalizationKey, StringComparison.Ordinal);
	}

	private static bool IsCandidateAlreadyInTargetLanguage(WorkshopLocalizationCandidate candidate, string targetLanguage)
	{
		if (candidate != null)
		{
			return LooksLikeTargetLanguage(candidate.SourceText, targetLanguage);
		}
		return false;
	}

	private static bool LooksLikeTargetLanguage(string text, string targetLanguage)
	{
		return MachineTranslationSettings.LooksLikeTargetLanguage(text, targetLanguage);
	}
}
