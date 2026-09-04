using System;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal sealed class SnapshotKernelCheckpoint
{
	internal SnapshotKernelCheckpoint(ItemGroupView[] groups) => Groups = groups ?? Array.Empty<ItemGroupView>();
	internal ItemGroupView[] Groups { get; }
}

internal sealed class SnapshotTransactionCommitResult
{
	internal bool Success { get; private set; }
	internal bool RollbackSucceeded { get; private set; }
	internal string RecoveryBackupPath { get; private set; } = string.Empty;
	internal string CandidateRecoveryPath { get; private set; } = string.Empty;
	internal string CandidateHistoryRecoveryPath { get; private set; } = string.Empty;
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotTransactionCommitResult Succeeded() =>
		new SnapshotTransactionCommitResult { Success = true, RollbackSucceeded = true };

	internal static SnapshotTransactionCommitResult Failed(string errorCode, bool rollbackSucceeded,
		string recoveryBackupPath = "", string candidateRecoveryPath = "",
		string candidateHistoryRecoveryPath = "") =>
		new SnapshotTransactionCommitResult
		{
			ErrorCode = errorCode ?? "snapshot-transaction-failed",
			RollbackSucceeded = rollbackSucceeded,
			RecoveryBackupPath = recoveryBackupPath ?? string.Empty,
			CandidateRecoveryPath = candidateRecoveryPath ?? string.Empty,
			CandidateHistoryRecoveryPath = candidateHistoryRecoveryPath ?? string.Empty
		};
}

internal sealed class SnapshotStorageRollbackReconciliationResult
{
	internal bool RequiresReconciliation { get; private set; }
	internal bool ReconciliationAttempted { get; private set; }
	internal bool MarkerPersistenceThrew { get; private set; }
	internal bool MarkerPersisted { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotStorageRollbackReconciliationResult Completed(bool required,
		bool attempted, bool markerThrew, bool markerPersisted, string errorCode) =>
		new SnapshotStorageRollbackReconciliationResult
		{
			RequiresReconciliation = required,
			ReconciliationAttempted = attempted,
			MarkerPersistenceThrew = markerThrew,
			MarkerPersisted = markerPersisted,
			ErrorCode = errorCode ?? string.Empty
		};
}

internal static class SnapshotStorageRollbackReconciliationCoordinator
{
	internal static SnapshotStorageRollbackReconciliationResult Observe(
		SnapshotTransactionCommitResult transactionResult, string context,
		Func<string, bool> persistReconciliation)
	{
		bool required = transactionResult == null ||
			(!transactionResult.Success && !transactionResult.RollbackSucceeded);
		if (!required)
			return SnapshotStorageRollbackReconciliationResult.Completed(false, false, false, false,
				transactionResult?.ErrorCode ?? string.Empty);

		string reason = "storage-rollback-failed:" + (context ?? string.Empty) + ":" +
			(transactionResult?.ErrorCode ?? "missing-result");
		if (!string.IsNullOrWhiteSpace(transactionResult?.RecoveryBackupPath))
			reason += ";recovery=" + transactionResult.RecoveryBackupPath;
		if (!string.IsNullOrWhiteSpace(transactionResult?.CandidateRecoveryPath))
			reason += ";candidate=" + transactionResult.CandidateRecoveryPath;
		if (!string.IsNullOrWhiteSpace(transactionResult?.CandidateHistoryRecoveryPath))
			reason += ";candidate-history=" + transactionResult.CandidateHistoryRecoveryPath;
		bool attempted = persistReconciliation != null;
		bool markerThrew = false;
		bool markerPersisted = false;
		try { markerPersisted = persistReconciliation?.Invoke(reason) == true; }
		catch (Exception) { markerThrew = true; }
		string errorCode = markerPersisted ? reason : reason + ":marker-persistence-failed";
		return SnapshotStorageRollbackReconciliationResult.Completed(true, attempted,
			markerThrew, markerPersisted, errorCode);
	}
}

internal sealed class SnapshotKernelTransaction
{
	private readonly SnapshotKernel _kernel;
	private readonly SnapshotStorageIdentity _identity;
	private readonly SnapshotKernelCheckpoint _checkpoint;
	private bool _completed;

	internal SnapshotKernelTransaction(SnapshotKernel kernel, SnapshotStorageIdentity identity)
	{
		_kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
		if (!identity.IsValid) throw new ArgumentException("A complete storage identity is required.", nameof(identity));
		_identity = identity;
		_checkpoint = kernel.CreateCheckpoint();
	}

	internal bool BelongsTo(SnapshotKernel kernel) => ReferenceEquals(_kernel, kernel);

	internal SnapshotMutationResult Store(ItemGroupIdentity identity, ItemSnapshot snapshot, long count = 1) =>
		_completed ? SnapshotMutationResult.Failed("transaction-completed") : _kernel.Store(identity, snapshot, count);

	internal SnapshotWithdrawalResult Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long count = 1) =>
		_completed ? SnapshotWithdrawalResult.Failed("transaction-completed") : _kernel.Withdraw(identity, snapshot, count);

	internal SnapshotTransactionCommitResult Commit(Func<SnapshotDocument, SnapshotSaveResult> persist)
	{
		if (_completed) return SnapshotTransactionCommitResult.Failed("transaction-completed", false);
		if (persist == null) return FailAndRollback("persist-callback-missing");
		try
		{
			SnapshotSaveResult save = persist(_kernel.ExportDocument(_identity));
			if (save?.Success != true) return FailAndRollback(save?.ErrorCode ?? "persistence-failed",
				saveDefinitelyNotCommitted: save != null && !save.MayHaveCommitted,
				recoveryBackupPath: save?.RecoveryBackupPath,
				candidateRecoveryPath: save?.CandidateRecoveryPath,
				candidateHistoryRecoveryPath: save?.CandidateHistoryRecoveryPath);
			_completed = true;
			return SnapshotTransactionCommitResult.Succeeded();
		}
		catch (Exception exception)
		{
			return FailAndRollback("persistence-threw:" + exception.GetType().Name,
				saveDefinitelyNotCommitted: false);
		}
	}

	internal SnapshotTransactionCommitResult Rollback()
	{
		if (_completed) return SnapshotTransactionCommitResult.Failed("transaction-completed", false);
		return FailAndRollback("transaction-rolled-back");
	}

	private SnapshotTransactionCommitResult FailAndRollback(string errorCode,
		bool saveDefinitelyNotCommitted = true, string recoveryBackupPath = "",
		string candidateRecoveryPath = "", string candidateHistoryRecoveryPath = "")
	{
		SnapshotMutationResult restored = _kernel.RestoreCheckpoint(_checkpoint);
		_completed = true;
		return SnapshotTransactionCommitResult.Failed(errorCode,
			restored.Success && saveDefinitelyNotCommitted, recoveryBackupPath,
			candidateRecoveryPath, candidateHistoryRecoveryPath);
	}
}
