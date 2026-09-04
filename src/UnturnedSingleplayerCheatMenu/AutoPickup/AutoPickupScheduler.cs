using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal enum AutoPickupExecutionPath
{
	Filtered,
	InfiniteStorage,
	NativePickup,
	Failed,
	ReconciliationBlocked
}

internal enum AutoPickupNativePostAction
{
	AcceptNative,
	AwaitNativeConfirmation,
	RecoverWithInfiniteStorage,
	SuppressUncertainNative
}

internal readonly struct AutoPickupInventoryEvidence
{
	internal AutoPickupInventoryEvidence(int matchingAssetJarCount, long totalAmount,
		int exactSnapshotJarCount = 0)
	{
		MatchingAssetJarCount = Math.Max(0, matchingAssetJarCount);
		TotalAmount = Math.Max(0L, totalAmount);
		ExactSnapshotJarCount = Math.Max(0, exactSnapshotJarCount);
	}

	internal int MatchingAssetJarCount { get; }
	internal long TotalAmount { get; }
	internal int ExactSnapshotJarCount { get; }

	internal bool IndicatesDeliveryComparedTo(AutoPickupInventoryEvidence before)
	{
		return MatchingAssetJarCount > before.MatchingAssetJarCount ||
			TotalAmount > before.TotalAmount ||
			ExactSnapshotJarCount > before.ExactSnapshotJarCount;
	}
}

internal readonly struct NativePickupAttempt
{
	public bool IsInitialized { get; }
	public AutoPickupResultKind Result { get; }
	public AutoPickupInventoryEvidence InventoryBefore { get; }
	public AutoPickupInventoryEvidence InventoryAfter { get; }
	public string Detail { get; }
	public bool RequestSent { get; }
	public bool SuppressRetry { get; }

	public NativePickupAttempt(AutoPickupResultKind result, AutoPickupInventoryEvidence inventoryBefore,
		AutoPickupInventoryEvidence inventoryAfter, string detail, bool requestSent = false,
		bool suppressRetry = false)
	{
		IsInitialized = true;
		Result = result;
		InventoryBefore = inventoryBefore;
		InventoryAfter = inventoryAfter;
		Detail = detail ?? string.Empty;
		RequestSent = requestSent;
		SuppressRetry = suppressRetry;
	}
}

internal readonly struct AutoPickupStorageCallbackResult
{
	internal AutoPickupStorageCallbackResult(bool accepted, NativePickupAttempt attempt)
	{
		Accepted = accepted;
		Attempt = attempt;
	}

	internal bool Accepted { get; }
	internal NativePickupAttempt Attempt { get; }
}

internal static class AutoPickupFinalAttemptPolicy
{
	internal static NativePickupAttempt CaptureNativeCallback(Func<NativePickupAttempt> callback,
		AutoPickupInventoryEvidence baseline)
	{
		if (callback == null)
			return CreateNativeCallbackFailure(baseline, "原生拾取回调不可用。");
		try
		{
			NativePickupAttempt attempt = callback();
			return attempt.IsInitialized
				? attempt
				: CreateNativeCallbackFailure(baseline, "原生拾取回调未返回有效尝试结果。");
		}
		catch (Exception)
		{
			return CreateNativeCallbackFailure(baseline, "原生拾取回调异常，最终状态不确定。");
		}
	}

	internal static AutoPickupStorageCallbackResult CaptureStorageCallback(
		Func<AutoPickupStorageCallbackResult> callback,
		AutoPickupInventoryEvidence baseline)
	{
		if (callback == null)
			return new AutoPickupStorageCallbackResult(false,
				CreateStorageCallbackFailure(baseline, "无限储存恢复回调不可用。"));
		try
		{
			AutoPickupStorageCallbackResult result = callback();
			return result.Attempt.IsInitialized
				? result
				: new AutoPickupStorageCallbackResult(false,
					CreateStorageCallbackFailure(baseline,
						"无限储存恢复回调未返回有效尝试结果。"));
		}
		catch (Exception)
		{
			return new AutoPickupStorageCallbackResult(false,
				CreateStorageCallbackFailure(baseline, "无限储存恢复回调异常。"));
		}
	}

	internal static NativePickupAttempt NormalizeNativeAttempt(NativePickupAttempt attempt,
		AutoPickupNativePostAction action)
	{
		if (!attempt.IsInitialized)
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, default, default,
				"原生拾取回调未返回有效尝试结果。", requestSent: true,
				suppressRetry: true);
		}
		if (action == AutoPickupNativePostAction.AcceptNative &&
			attempt.Result != AutoPickupResultKind.PickedUp)
		{
			const string confirmation = "背包复合证据已确认原生拾取交付。";
			string detail = string.IsNullOrEmpty(attempt.Detail)
				? confirmation
				: attempt.Detail + "；" + confirmation;
			return new NativePickupAttempt(AutoPickupResultKind.PickedUp,
				attempt.InventoryBefore, attempt.InventoryAfter,
				detail, attempt.RequestSent);
		}
		return attempt;
	}

	internal static NativePickupAttempt EnsureRecordable(NativePickupAttempt attempt)
	{
		return attempt.IsInitialized
			? attempt
			: new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, default, default,
				"自动拾取尝试结果未初始化。", requestSent: false);
	}

	internal static NativePickupAttempt BuildFailedAttempt(AutoPickupInventoryEvidence baseline,
		bool nativeAttempted, NativePickupAttempt nativeAttempt, bool storageAttempted,
		NativePickupAttempt storageAttempt, bool nativePickupAvailable)
	{
		if (nativeAttempted)
		{
			nativeAttempt = nativeAttempt.IsInitialized
				? nativeAttempt
				: CreateNativeCallbackFailure(baseline, "原生拾取回调未返回有效尝试结果。");
			if (storageAttempted)
			{
				storageAttempt = storageAttempt.IsInitialized
					? storageAttempt
					: CreateStorageCallbackFailure(baseline,
						"无限储存恢复回调未返回有效尝试结果。");
				return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
					nativeAttempt.InventoryBefore, nativeAttempt.InventoryAfter,
					"原生拾取未确认交付，且无限储存恢复失败。",
					nativeAttempt.RequestSent);
			}
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
				nativeAttempt.InventoryBefore, nativeAttempt.InventoryAfter,
				"原生拾取未确认交付，且未执行无限储存恢复。",
				nativeAttempt.RequestSent, nativeAttempt.SuppressRetry);
		}
		if (storageAttempted)
		{
			return storageAttempt.IsInitialized
				? storageAttempt
				: CreateStorageCallbackFailure(baseline,
					"无限储存恢复回调未返回有效尝试结果。");
		}
		return nativePickupAvailable
			? new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, baseline, baseline,
				"自动拾取执行未产生有效尝试结果。")
			: new NativePickupAttempt(AutoPickupResultKind.RejectedByCapacity, baseline, baseline,
				"背包没有可放置该物品的剩余空间，且无限储存未接受该物品。");
	}

	private static NativePickupAttempt CreateNativeCallbackFailure(
		AutoPickupInventoryEvidence baseline, string detail)
	{
		return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, baseline, baseline,
			detail, requestSent: true, suppressRetry: true);
	}

	private static NativePickupAttempt CreateStorageCallbackFailure(
		AutoPickupInventoryEvidence baseline, string detail)
	{
		return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, baseline, baseline,
			detail, requestSent: false);
	}
}

internal enum AutoPickupAttemptRecordOutcome
{
	PickedUp,
	Pending,
	CapacityBlocked,
	ReconciliationRequired,
	Suppressed,
	Failure,
	IgnoredStale
}

internal static class AutoPickupAttemptRecordCoordinator
{
	internal static AutoPickupAttemptRecordOutcome Execute(NativePickupAttempt rawAttempt,
		uint instanceId, float now, AutoPickupScheduler scheduler, Action requestSent,
		Action pickedUp, Action pending, Action capacityBlocked, Action reconciliationRequired,
		Action suppressed, Action failure)
	{
		if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
		NativePickupAttempt attempt = AutoPickupFinalAttemptPolicy.EnsureRecordable(rawAttempt);
		if (attempt.RequestSent) requestSent?.Invoke();
		if (attempt.Result == AutoPickupResultKind.PickedUp)
		{
			pickedUp?.Invoke();
			scheduler.MarkSuccess(instanceId);
			return AutoPickupAttemptRecordOutcome.PickedUp;
		}
		if (attempt.Result == AutoPickupResultKind.Requested)
		{
			pending?.Invoke();
			return AutoPickupAttemptRecordOutcome.Pending;
		}
		if (attempt.Result == AutoPickupResultKind.RejectedByCapacity && !attempt.RequestSent)
		{
			capacityBlocked?.Invoke();
			scheduler.MarkCapacityBlocked(instanceId, now);
			return AutoPickupAttemptRecordOutcome.CapacityBlocked;
		}
		if (attempt.Result == AutoPickupResultKind.ReconciliationRequired)
		{
			reconciliationRequired?.Invoke();
			scheduler.SuppressUntilReset(instanceId);
			return AutoPickupAttemptRecordOutcome.ReconciliationRequired;
		}
		if (attempt.SuppressRetry)
		{
			suppressed?.Invoke();
			scheduler.SuppressUntilReset(instanceId);
			return AutoPickupAttemptRecordOutcome.Suppressed;
		}
		if (attempt.Result == AutoPickupResultKind.StaleDrop)
			return AutoPickupAttemptRecordOutcome.IgnoredStale;
		failure?.Invoke();
		scheduler.MarkFailure(instanceId, now);
		return AutoPickupAttemptRecordOutcome.Failure;
	}
}

internal static class AutoPickupExecutionCoordinator
{
	internal static AutoPickupNativePostAction ResolveNativePostAction(AutoPickupResultKind result,
		bool worldDropIsCurrent, AutoPickupInventoryEvidence inventoryBefore,
		AutoPickupInventoryEvidence inventoryAfter, bool requestSent)
	{
		if (result == AutoPickupResultKind.PickedUp)
			return AutoPickupNativePostAction.AcceptNative;
		if (result == AutoPickupResultKind.Requested)
			return AutoPickupNativePostAction.AwaitNativeConfirmation;
		if (worldDropIsCurrent && requestSent)
			return AutoPickupNativePostAction.SuppressUncertainNative;
		if (result == AutoPickupResultKind.ReconciliationRequired)
			return AutoPickupNativePostAction.SuppressUncertainNative;
		if (inventoryAfter.IndicatesDeliveryComparedTo(inventoryBefore))
			return AutoPickupNativePostAction.AcceptNative;
		if (!worldDropIsCurrent)
			return AutoPickupNativePostAction.RecoverWithInfiniteStorage;
		return AutoPickupNativePostAction.RecoverWithInfiniteStorage;
	}

	internal static bool StopsNativeFallback(AutoPickupNativePostAction action) =>
		action != AutoPickupNativePostAction.RecoverWithInfiniteStorage;

	internal static AutoPickupExecutionPath Execute(bool allowed, bool preferInfiniteStorage,
		bool nativePickupAvailable, Func<bool> tryInfiniteStorage, Func<bool> tryNativePickup)
	{
		return Execute(allowed, () => false, preferInfiniteStorage, nativePickupAvailable,
			tryInfiniteStorage, tryNativePickup);
	}

	internal static AutoPickupExecutionPath Execute(bool allowed, bool reconciliationRequired,
		bool preferInfiniteStorage, bool nativePickupAvailable,
		Func<bool> tryInfiniteStorage, Func<bool> tryNativePickup)
	{
		return Execute(allowed, () => reconciliationRequired, preferInfiniteStorage,
			nativePickupAvailable, tryInfiniteStorage, tryNativePickup);
	}

	internal static AutoPickupExecutionPath Execute(bool allowed,
		Func<bool> isReconciliationRequired, bool preferInfiniteStorage,
		bool nativePickupAvailable, Func<bool> tryInfiniteStorage, Func<bool> tryNativePickup)
	{
		if (!allowed) return AutoPickupExecutionPath.Filtered;
		if (RequiresReconciliation(isReconciliationRequired))
			return AutoPickupExecutionPath.ReconciliationBlocked;
		if (preferInfiniteStorage)
		{
			if (tryInfiniteStorage?.Invoke() == true) return AutoPickupExecutionPath.InfiniteStorage;
			if (RequiresReconciliation(isReconciliationRequired))
				return AutoPickupExecutionPath.ReconciliationBlocked;
			return nativePickupAvailable && tryNativePickup?.Invoke() == true
				? AutoPickupExecutionPath.NativePickup
				: AutoPickupExecutionPath.Failed;
		}
		if (nativePickupAvailable && tryNativePickup?.Invoke() == true)
			return AutoPickupExecutionPath.NativePickup;
		if (tryInfiniteStorage?.Invoke() == true) return AutoPickupExecutionPath.InfiniteStorage;
		return RequiresReconciliation(isReconciliationRequired)
			? AutoPickupExecutionPath.ReconciliationBlocked
			: AutoPickupExecutionPath.Failed;
	}

	private static bool RequiresReconciliation(Func<bool> callback)
	{
		if (callback == null) return false;
		try { return callback(); }
		catch (Exception) { return true; }
	}
}

internal enum AutoPickupPendingConfirmationOutcome
{
	Waiting,
	NativeDelivered,
	RecoveredWithInfiniteStorage,
	StorageRecoveryFailed,
	SuppressedUncertainNative
}

internal static class AutoPickupPendingConfirmationCoordinator
{
	internal static AutoPickupPendingConfirmationOutcome Execute(bool worldDropIsCurrent,
		bool timeoutElapsed, AutoPickupInventoryEvidence inventoryBefore,
		AutoPickupInventoryEvidence inventoryAfter, Func<bool> tryInfiniteStorage,
		Action markNativeDelivered, Action suppressRetry, Action completePending)
	{
		if (worldDropIsCurrent && !timeoutElapsed)
			return AutoPickupPendingConfirmationOutcome.Waiting;

		AutoPickupNativePostAction action = AutoPickupExecutionCoordinator.ResolveNativePostAction(
			AutoPickupResultKind.UnknownFailure, worldDropIsCurrent, inventoryBefore,
			inventoryAfter, requestSent: true);
		switch (action)
		{
			case AutoPickupNativePostAction.AcceptNative:
				markNativeDelivered?.Invoke();
				completePending?.Invoke();
				return AutoPickupPendingConfirmationOutcome.NativeDelivered;
			case AutoPickupNativePostAction.RecoverWithInfiniteStorage:
				bool recovered = TryInvoke(tryInfiniteStorage);
				completePending?.Invoke();
				return recovered
					? AutoPickupPendingConfirmationOutcome.RecoveredWithInfiniteStorage
					: AutoPickupPendingConfirmationOutcome.StorageRecoveryFailed;
			default:
				suppressRetry?.Invoke();
				completePending?.Invoke();
				return AutoPickupPendingConfirmationOutcome.SuppressedUncertainNative;
		}
	}

	private static bool TryInvoke(Func<bool> callback)
	{
		if (callback == null) return false;
		try { return callback(); }
		catch (Exception) { return false; }
	}
}

internal sealed class AutoPickupStorageCommitResult
{
	internal bool Success { get; private set; }
	internal bool StoredSnapshot { get; private set; }
	internal bool SnapshotRollbackSucceeded { get; private set; }
	internal bool RequiresReconciliation { get; private set; }
	internal bool StorageIsAuthoritative { get; private set; }
	internal bool WorldDropQuarantined { get; private set; }
	internal bool SuppressNativeFallback { get; private set; }
	internal bool SuppressRetry { get; private set; }
	internal bool StorageLocked { get; private set; }
	internal bool MarkerPersisted { get; private set; }

	internal static AutoPickupStorageCommitResult Create(bool success, bool storedSnapshot,
		bool snapshotRollbackSucceeded, bool requiresReconciliation, bool storageIsAuthoritative,
		bool worldDropQuarantined, bool suppressNativeFallback, bool suppressRetry,
		bool storageLocked, bool markerPersisted) => new AutoPickupStorageCommitResult
	{
		Success = success,
		StoredSnapshot = storedSnapshot,
		SnapshotRollbackSucceeded = snapshotRollbackSucceeded,
		RequiresReconciliation = requiresReconciliation,
		StorageIsAuthoritative = storageIsAuthoritative,
		WorldDropQuarantined = worldDropQuarantined,
		SuppressNativeFallback = suppressNativeFallback,
		SuppressRetry = suppressRetry,
		StorageLocked = storageLocked,
		MarkerPersisted = markerPersisted
	};
}

internal static class AutoPickupStorageCommitCoordinator
{
	internal static AutoPickupStorageCommitResult Execute(Func<bool> storeSnapshot,
		Func<bool> removeWorldDrop, Func<bool> rollbackSnapshot, Func<bool> quarantineWorldDrop,
		Func<bool> lockStorage)
	{
		bool stored = TryInvoke(storeSnapshot);
		if (!stored)
			return AutoPickupStorageCommitResult.Create(false, false, false, false, false,
				false, false, false, false, false);
		bool removed = TryInvoke(removeWorldDrop);
		if (removed)
			return AutoPickupStorageCommitResult.Create(true, true, false, false, true,
				false, true, false, false, false);
		bool rolledBack = TryInvoke(rollbackSnapshot);
		if (rolledBack)
			return AutoPickupStorageCommitResult.Create(false, true, true, false, false,
				false, true, false, false, false);
		bool markerPersisted = TryLock(lockStorage, out bool locked);
		bool quarantined = TryInvoke(quarantineWorldDrop);
		return AutoPickupStorageCommitResult.Create(false, true, false, true, true,
			quarantined, true, true, locked, markerPersisted);
	}

	private static bool TryInvoke(Func<bool> callback)
	{
		if (callback == null) return false;
		try { return callback(); }
		catch (Exception) { return false; }
	}

	private static bool TryLock(Func<bool> callback, out bool storageLocked)
	{
		storageLocked = false;
		if (callback == null) return false;
		try
		{
			bool markerPersisted = callback();
			storageLocked = true;
			return markerPersisted;
		}
		catch (Exception)
		{
			return false;
		}
	}
}

internal sealed class AutoPickupScheduler
{
	private sealed class FailureState
	{
		public int Count;

		public float RetryAt;
	}

	private readonly Dictionary<uint, FailureState> _failures = new Dictionary<uint, FailureState>();
	private readonly HashSet<uint> _suppressedUntilReset = new HashSet<uint>();

	private float _nextScanAt;

	private float _requestTokens = 1f;

	private float _lastTokenTime;

	public bool ShouldScan(float now, float frequencyHz)
	{
		frequencyHz = Math.Max(AutoPickupSettings.MinFrequencyHz, Math.Min(AutoPickupSettings.MaxFrequencyHz, frequencyHz));
		if (now < _nextScanAt)
		{
			return false;
		}
		_nextScanAt = now + 1f / frequencyHz;
		return true;
	}

	public bool CanRequest(uint instanceId, float now)
	{
		return !_suppressedUntilReset.Contains(instanceId) &&
			(!_failures.TryGetValue(instanceId, out FailureState state) || now >= state.RetryAt);
	}

	public int TakeRequestBudget(float now, int desired)
	{
		desired = Math.Max(0, Math.Min(AutoPickupSettings.MaxRequestsPerTick, desired));
		float elapsed = Math.Max(0f, now - _lastTokenTime);
		_lastTokenTime = now;
		_requestTokens = Math.Min(10f, _requestTokens + elapsed * 10f);
		int granted = Math.Min(desired, (int)_requestTokens);
		_requestTokens -= granted;
		return granted;
	}

	public float MarkFailure(uint instanceId, float now)
	{
		if (!_failures.TryGetValue(instanceId, out FailureState state))
		{
			state = new FailureState();
			_failures.Add(instanceId, state);
		}
		state.Count = Math.Min(state.Count + 1, 4);
		float delay = 0.25f * (1 << (state.Count - 1));
		state.RetryAt = now + Math.Min(2f, delay);
		return state.RetryAt;
	}

	public float MarkCapacityBlocked(uint instanceId, float now)
	{
		if (!_failures.TryGetValue(instanceId, out FailureState state))
		{
			state = new FailureState();
			_failures.Add(instanceId, state);
		}
		state.RetryAt = now + 5f;
		return state.RetryAt;
	}

	public void MarkSuccess(uint instanceId)
	{
		_failures.Remove(instanceId);
	}

	public void SuppressUntilReset(uint instanceId)
	{
		_suppressedUntilReset.Add(instanceId);
		_failures.Remove(instanceId);
	}

	public void Reset(float now = 0f)
	{
		_failures.Clear();
		_suppressedUntilReset.Clear();
		_nextScanAt = now;
		_requestTokens = 1f;
		_lastTokenTime = now;
	}
}
