using System;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal sealed class SnapshotSessionActivationResult
{
	internal bool Attempted { get; private set; }
	internal bool Success { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotSessionActivationResult Succeeded(bool attempted) =>
		new SnapshotSessionActivationResult { Attempted = attempted, Success = true };

	internal static SnapshotSessionActivationResult Failed(bool attempted, string errorCode) =>
		new SnapshotSessionActivationResult { Attempted = attempted, ErrorCode = errorCode ?? "activation-failed" };
}

internal sealed class SnapshotStorageSession
{
	private readonly SnapshotActivationAttemptPolicy _activationAttempts = new SnapshotActivationAttemptPolicy();
	private SnapshotKernel _kernel = new SnapshotKernel();
	private SnapshotStorageIdentity _identity;
	private Func<SnapshotDocument, SnapshotSaveResult> _persist;

	internal SnapshotKernel Kernel => _kernel;
	internal SnapshotStorageIdentity Identity => _identity;
	internal bool IsOperational { get; private set; }

	internal SnapshotSessionActivationResult Activate(SnapshotStorageIdentity identity,
		Func<SnapshotLoadResult> load, Func<SnapshotDocument, SnapshotSaveResult> persist)
	{
		if (!identity.IsValid)
			return SnapshotSessionActivationResult.Failed(false, "invalid-storage-identity");
		if (IsOperational && SameIdentity(_identity, identity))
			return SnapshotSessionActivationResult.Succeeded(false);
		if (!_activationAttempts.ShouldAttempt(identity))
			return SnapshotSessionActivationResult.Failed(false, "activation-suppressed");
		if (load == null || persist == null)
		{
			FailIdentity(identity);
			return SnapshotSessionActivationResult.Failed(true, "activation-callback-missing");
		}

		IsOperational = false;
		try
		{
			SnapshotLoadResult loaded = load();
			if (loaded?.Success != true || loaded.Document == null)
			{
				FailIdentity(identity);
				return SnapshotSessionActivationResult.Failed(true, loaded?.ErrorCode ?? "load-failed");
			}
			if (!string.Equals(loaded.Document.WorldKey, identity.WorldKey, StringComparison.Ordinal) ||
				!string.Equals(loaded.Document.PlayerKey, identity.PlayerKey, StringComparison.Ordinal))
			{
				FailIdentity(identity);
				return SnapshotSessionActivationResult.Failed(true, "document-identity-mismatch");
			}
			SnapshotKernel staged = new SnapshotKernel();
			SnapshotMutationResult imported = staged.ReplaceFromDocument(loaded.Document);
			if (!imported.Success)
			{
				FailIdentity(identity);
				return SnapshotSessionActivationResult.Failed(true, imported.ErrorCode);
			}
			_kernel = staged;
			_identity = identity;
			_persist = persist;
			IsOperational = true;
			_activationAttempts.RecordSuccess(identity);
			return SnapshotSessionActivationResult.Succeeded(true);
		}
		catch (Exception exception)
		{
			FailIdentity(identity);
			return SnapshotSessionActivationResult.Failed(true, "activation-threw:" + exception.GetType().Name);
		}
	}

	internal SnapshotKernelTransaction BeginTransaction()
	{
		return IsOperational ? new SnapshotKernelTransaction(_kernel, _identity) : null;
	}

	internal SnapshotKernelCheckpoint CaptureCheckpoint() =>
		IsOperational ? _kernel.CreateCheckpoint() : null;

	internal SnapshotTransactionCommitResult RestoreCheckpointAndPersist(
		SnapshotKernelCheckpoint checkpoint, Func<SnapshotDocument, SnapshotSaveResult> persistOverride = null)
	{
		if (!IsOperational || checkpoint == null)
			return SnapshotTransactionCommitResult.Failed("session-not-operational", false);
		SnapshotKernelCheckpoint persistedState = _kernel.CreateCheckpoint();
		SnapshotMutationResult restored = _kernel.RestoreCheckpoint(checkpoint);
		if (!restored.Success)
		{
			_kernel.RestoreCheckpoint(persistedState);
			CloseMutations();
			return SnapshotTransactionCommitResult.Failed(restored.ErrorCode, false);
		}
		try
		{
			SnapshotSaveResult saved = (persistOverride ?? _persist)?.Invoke(_kernel.ExportDocument(_identity));
			if (saved?.Success == true) return SnapshotTransactionCommitResult.Succeeded();
			_kernel.RestoreCheckpoint(persistedState);
			CloseMutations();
			return SnapshotTransactionCommitResult.Failed(
				saved?.ErrorCode ?? "rollback-persistence-failed", false,
				saved?.RecoveryBackupPath, saved?.CandidateRecoveryPath,
				saved?.CandidateHistoryRecoveryPath);
		}
		catch (Exception exception)
		{
			_kernel.RestoreCheckpoint(persistedState);
			CloseMutations();
			return SnapshotTransactionCommitResult.Failed("rollback-persistence-threw:" +
				exception.GetType().Name, false);
		}
	}

	internal SnapshotTransactionCommitResult Commit(SnapshotKernelTransaction transaction,
		Func<SnapshotDocument, SnapshotSaveResult> persistOverride = null)
	{
		if (!IsOperational || transaction == null || !transaction.BelongsTo(_kernel))
			return SnapshotTransactionCommitResult.Failed("session-not-operational", false);
		SnapshotTransactionCommitResult result = transaction.Commit(persistOverride ?? _persist);
		if (result.Success) return result;
		IsOperational = false;
		_activationAttempts.RecordFailure(_identity);
		return result;
	}

	internal void Reset()
	{
		IsOperational = false;
		_kernel = new SnapshotKernel();
		_identity = default;
		_persist = null;
		_activationAttempts.Reset();
	}

	internal void CloseMutations()
	{
		IsOperational = false;
		if (_identity.IsValid) _activationAttempts.RecordFailure(_identity);
	}

	internal void LatchIdentityFailure(SnapshotStorageIdentity identity)
	{
		if (!identity.IsValid) return;
		FailIdentity(identity);
	}

	private void FailIdentity(SnapshotStorageIdentity identity)
	{
		_kernel = new SnapshotKernel();
		_identity = identity;
		_persist = null;
		IsOperational = false;
		_activationAttempts.RecordFailure(identity);
	}

	private static bool SameIdentity(SnapshotStorageIdentity left, SnapshotStorageIdentity right) =>
		string.Equals(left.WorldKey, right.WorldKey, StringComparison.Ordinal) &&
		string.Equals(left.PlayerKey, right.PlayerKey, StringComparison.Ordinal);
}
