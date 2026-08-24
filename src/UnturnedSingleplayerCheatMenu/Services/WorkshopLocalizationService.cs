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

	private bool _disposed;

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
		CancelTranslation();
		lock (_gate)
		{
			_progress.State = WorkshopLocalizationState.Scanning;
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
		if (AutoScanOnAssetsRefreshed)
		{
			ScanCurrentAssets();
		}
		else
		{
			CancelTranslation();
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
		}
		if (list2.Count == 0)
		{
			_info?.Invoke($"Workshop translation has no pending text: candidates={list.Count}, cacheEntries={_cache.Count}, force={force}.");
			return true;
		}
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		lock (_gate)
		{
			_translationCancellation?.Cancel();
			_translationCancellation = cancellationTokenSource;
			_translationTask = TranslateWorkerAsync(generation, list2.Select((WorkshopLocalizationCandidate candidate) => new WorkshopTranslationWorkItem(candidate.Key, candidate.Field, candidate.SourceText, candidate.AssetName, candidate.AssetGuid, candidate.LegacyId, candidate.ActionKey)).ToList(), cancellationTokenSource.Token);
			_info?.Invoke($"Workshop translation started: provider={Settings.ApiProtocol}, pending={list2.Count}, force={force}.");
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
			}
		}
		translationCancellation?.Cancel();
	}

	internal void Pump()
	{
		PendingApply pendingApply = Interlocked.Exchange(ref _pendingApply, null);
		if (pendingApply == null)
		{
			return;
		}
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
			}
		}
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

	private async Task TranslateWorkerAsync(int generation, IReadOnlyList<WorkshopTranslationWorkItem> workItems, CancellationToken cancellationToken)
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
				List<WorkshopTranslationRequest> requests = batch.Select(delegate(WorkshopTranslationWorkItem workItem)
				{
					WorkshopProtectedText workshopProtectedText2 = WorkshopRichTextProtector.Protect(workItem.SourceText);
					return new WorkshopTranslationRequest(workItem.Key, workItem.Field, workshopProtectedText2.Text);
				}).ToList();
				WorkshopTranslationBatchResult workshopTranslationBatchResult = await _client.TranslateAsync(requests, Settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (!workshopTranslationBatchResult.Success)
				{
					if (batch.Count > 1)
					{
						_warn?.Invoke($"Workshop translation batch failed: {workshopTranslationBatchResult.Error}; retrying {batch.Count} entries individually.");
						foreach (WorkshopTranslationWorkItem singleItem in batch)
						{
							cancellationToken.ThrowIfCancellationRequested();
							WorkshopProtectedText workshopProtectedText = WorkshopRichTextProtector.Protect(singleItem.SourceText);
							WorkshopTranslationBatchResult workshopTranslationBatchResult2 = await _client.TranslateAsync(new WorkshopTranslationRequest[1]
							{
								new WorkshopTranslationRequest(singleItem.Key, singleItem.Field, workshopProtectedText.Text)
							}, Settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							string error2;
							if (!workshopTranslationBatchResult2.Success)
							{
								failed++;
								_warn?.Invoke("Workshop translation single-entry failed for " + singleItem.Key + ": " + workshopTranslationBatchResult2.Error);
							}
							else if (!TryApplyTranslationEntry(singleItem, workshopTranslationBatchResult2, out error2))
							{
								failed++;
								_warn?.Invoke("Workshop translation entry rejected for " + singleItem.Key + ": " + error2);
							}
							else
							{
								completed++;
							}
						}
					}
					else
					{
						failed += batch.Count;
						_warn?.Invoke("Workshop translation batch failed: " + workshopTranslationBatchResult.Error);
					}
					_cache.Save(Settings.SourceLanguage, Settings.TargetLanguage);
					UpdateTranslationProgress(workItems.Count, completed, failed);
					continue;
				}
				foreach (WorkshopTranslationWorkItem item in batch)
				{
					if (!TryApplyTranslationEntry(item, workshopTranslationBatchResult, out var error3))
					{
						failed++;
						_warn?.Invoke("Workshop translation entry rejected for " + item.Key + ": " + error3);
					}
					else
					{
						completed++;
					}
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
		catch (Exception ex2)
		{
			error = "translation failed: " + ex2.Message;
			_warn?.Invoke($"Workshop translation worker failed after {completed} completed and {failed} failed: {ex2}");
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

	private bool TryApplyTranslationEntry(WorkshopTranslationWorkItem workItem, WorkshopTranslationBatchResult result, out string error)
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
			SchemaVersion = 1,
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
			CreatedUtc = utcNow,
			UpdatedUtc = utcNow,
			LastUsedUtc = utcNow
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
		Dictionary<object, string> dictionary = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary2 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary3 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary4 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary5 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary6 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary7 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary8 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary9 = new Dictionary<object, string>(ReferenceComparer.Instance);
		Dictionary<object, string> dictionary10 = new Dictionary<object, string>(ReferenceComparer.Instance);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (WorkshopLocalizationCandidate candidate in _candidates)
		{
			switch (candidate.Field)
			{
			case WorkshopTextField.ItemName:
				dictionary6[candidate.Instance] = candidate.SourceText;
				break;
			case WorkshopTextField.ItemDescription:
				dictionary7[candidate.Instance] = candidate.SourceText;
				break;
			case WorkshopTextField.VehicleName:
				dictionary8[candidate.Instance] = candidate.SourceText;
				break;
			case WorkshopTextField.ActionText:
				dictionary9[candidate.Instance] = candidate.SourceText;
				break;
			case WorkshopTextField.ActionTooltip:
				dictionary10[candidate.Instance] = candidate.SourceText;
				break;
			}
			if (_cache.TryGet(candidate.Key, out var entry) && !IsCandidateAlreadyInTargetLanguage(candidate, Settings.TargetLanguage) && string.Equals(WorkshopTextIdentity.NormalizeSource(entry.SourceText), WorkshopTextIdentity.NormalizeSource(candidate.SourceText), StringComparison.Ordinal) && entry.Field == candidate.Field)
			{
				if (hashSet.Add(candidate.Key))
				{
					_cache.Touch(candidate.Key);
				}
				switch (candidate.Field)
				{
				case WorkshopTextField.ItemName:
					dictionary[candidate.Instance] = entry.Translation;
					break;
				case WorkshopTextField.ItemDescription:
					dictionary2[candidate.Instance] = entry.Translation;
					break;
				case WorkshopTextField.VehicleName:
					dictionary3[candidate.Instance] = entry.Translation;
					break;
				case WorkshopTextField.ActionText:
					dictionary4[candidate.Instance] = entry.Translation;
					break;
				case WorkshopTextField.ActionTooltip:
					dictionary5[candidate.Instance] = entry.Translation;
					break;
				}
			}
		}
		_snapshot = new WorkshopLocalizationSnapshot(dictionary, dictionary2, dictionary3, dictionary4, dictionary5, dictionary6, dictionary7, dictionary8, dictionary9, dictionary10);
		_snapshotVersion++;
		_progress.CacheHits = _candidates.Select((WorkshopLocalizationCandidate candidate) => candidate.Key).Distinct<string>(StringComparer.Ordinal).Count((string key) => _cache.TryGet(key, out var _));
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
		if (cached.Field != candidate.Field || !string.Equals(WorkshopTextIdentity.NormalizeSource(cached.SourceText), WorkshopTextIdentity.NormalizeSource(candidate.SourceText), StringComparison.Ordinal))
		{
			return false;
		}
		if (candidate.Field != WorkshopTextField.ItemName && candidate.Field != WorkshopTextField.VehicleName)
		{
			return true;
		}
		return !string.Equals(WorkshopTextIdentity.NormalizeSource(candidate.SourceText), WorkshopTextIdentity.NormalizeSource(cached.Translation), StringComparison.Ordinal);
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
