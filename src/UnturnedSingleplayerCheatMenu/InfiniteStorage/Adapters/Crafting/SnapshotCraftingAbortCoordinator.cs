using System;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;

internal sealed class SnapshotCraftingAbortResult
{
	internal bool Success { get; private set; }
	internal bool StorageRollbackAttempted { get; private set; }
	internal bool NativeRollbackAttempted { get; private set; }
	internal bool MutationsClosed { get; private set; }
	internal bool ReconciliationAttempted { get; private set; }
	internal bool MarkerPersisted { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotCraftingAbortResult Completed(bool success, bool storageAttempted,
		bool nativeAttempted, bool mutationsClosed, bool reconciliationAttempted,
		bool markerPersisted, string errorCode) => new SnapshotCraftingAbortResult
	{
		Success = success,
		StorageRollbackAttempted = storageAttempted,
		NativeRollbackAttempted = nativeAttempted,
		MutationsClosed = mutationsClosed,
		ReconciliationAttempted = reconciliationAttempted,
		MarkerPersisted = markerPersisted,
		ErrorCode = errorCode ?? string.Empty
	};
}

internal static class SnapshotCraftingRollbackGate
{
	[ThreadStatic] private static int _depth;

	internal static bool IsActive => _depth > 0;

	internal static T Execute<T>(Func<T> action)
	{
		if (action == null) throw new ArgumentNullException(nameof(action));
		checked { _depth++; }
		try { return action(); }
		finally { _depth--; }
	}
}

internal static class SnapshotCraftingReconciliationReason
{
	internal static string Build(string exceptionType,
		SnapshotTransactionCommitResult storageResult)
	{
		string reason = "crafting-abort:" +
			(string.IsNullOrWhiteSpace(exceptionType) ? "unknown" : exceptionType);
		if (!string.IsNullOrWhiteSpace(storageResult?.ErrorCode))
			reason += ";storage=" + storageResult.ErrorCode;
		if (!string.IsNullOrWhiteSpace(storageResult?.RecoveryBackupPath))
			reason += ";recovery=" + storageResult.RecoveryBackupPath;
		if (!string.IsNullOrWhiteSpace(storageResult?.CandidateRecoveryPath))
			reason += ";candidate=" + storageResult.CandidateRecoveryPath;
		if (!string.IsNullOrWhiteSpace(storageResult?.CandidateHistoryRecoveryPath))
			reason += ";candidate-history=" + storageResult.CandidateHistoryRecoveryPath;
		return reason;
	}
}

internal static class SnapshotCraftingAbortCoordinator
{
	internal static SnapshotCraftingAbortResult Execute(Func<bool> restoreStorage,
		Func<bool> restoreNative, Func<bool> latchReconciliation)
	{
		bool storageRestored = TryInvoke(restoreStorage);
		if (!storageRestored)
		{
			bool markerPersisted = TryLatch(latchReconciliation, out bool closed);
			return SnapshotCraftingAbortResult.Completed(false, true, false, closed,
				latchReconciliation != null,
				markerPersisted, FailureCode("storage-rollback-failed", markerPersisted));
		}
		bool nativeRestored = TryInvoke(restoreNative);
		if (!nativeRestored)
		{
			bool markerPersisted = TryLatch(latchReconciliation, out bool closed);
			return SnapshotCraftingAbortResult.Completed(false, true, true, closed,
				latchReconciliation != null,
				markerPersisted, FailureCode("native-rollback-failed", markerPersisted));
		}
		return SnapshotCraftingAbortResult.Completed(true, true, true, false, false, false, string.Empty);
	}

	private static bool TryInvoke(Func<bool> callback)
	{
		if (callback == null) return false;
		try { return callback(); }
		catch (Exception) { return false; }
	}

	private static bool TryLatch(Func<bool> latchReconciliation, out bool mutationsClosed)
	{
		mutationsClosed = false;
		if (latchReconciliation == null) return false;
		try
		{
			bool markerPersisted = latchReconciliation();
			mutationsClosed = true;
			return markerPersisted;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static string FailureCode(string stage, bool markerPersisted) => markerPersisted
		? stage
		: stage + ":marker-persistence-failed";
}
