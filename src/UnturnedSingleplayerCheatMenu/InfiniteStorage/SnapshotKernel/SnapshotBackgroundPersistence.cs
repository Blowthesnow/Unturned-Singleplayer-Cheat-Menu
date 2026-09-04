using System;
using System.Threading.Tasks;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal sealed class SnapshotBackgroundPersistence
{
	private readonly Task<SnapshotSaveResult> _task;

	private SnapshotBackgroundPersistence(Task<SnapshotSaveResult> task)
	{
		_task = task ?? throw new ArgumentNullException(nameof(task));
	}

	internal bool IsCompleted => _task.IsCompleted;

	internal static SnapshotBackgroundPersistence Start(SnapshotDocument document,
		Func<SnapshotDocument, SnapshotSaveResult> persist)
	{
		if (document == null) throw new ArgumentNullException(nameof(document));
		if (persist == null) throw new ArgumentNullException(nameof(persist));
		return new SnapshotBackgroundPersistence(Task.Run(() =>
		{
			try
			{
				return persist(document) ?? SnapshotSaveResult.Failed(
					"background-persistence-null", mayHaveCommitted: true);
			}
			catch (Exception exception)
			{
				return SnapshotSaveResult.Failed("background-persistence-threw:" +
					exception.GetType().Name, mayHaveCommitted: true);
			}
		}));
	}

	internal bool TryGetResult(out SnapshotSaveResult result)
	{
		if (!_task.IsCompleted)
		{
			result = null;
			return false;
		}
		result = WaitForResult();
		return true;
	}

	internal SnapshotSaveResult WaitForResult()
	{
		try
		{
			return _task.GetAwaiter().GetResult() ?? SnapshotSaveResult.Failed(
				"background-persistence-null", mayHaveCommitted: true);
		}
		catch (Exception exception)
		{
			return SnapshotSaveResult.Failed("background-persistence-task-threw:" +
				exception.GetType().Name, mayHaveCommitted: true);
		}
	}
}

internal enum SnapshotBackgroundStoreDisposition
{
	CommittedCurrentSession,
	SafeFailureCurrentSession,
	UncertainCurrentSession,
	DetachedBeforeCommit,
	DetachedCommittedOrUncertain
}

internal sealed class SnapshotBackgroundStoreCompletionResult
{
	private SnapshotBackgroundStoreCompletionResult(SnapshotBackgroundStoreDisposition disposition,
		SnapshotTransactionCommitResult transaction)
	{
		Disposition = disposition;
		Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
	}

	internal SnapshotBackgroundStoreDisposition Disposition { get; }
	internal SnapshotTransactionCommitResult Transaction { get; }
	internal bool ReconciliationMarkerPersisted { get; private set; }
	internal bool SessionDetached => Disposition == SnapshotBackgroundStoreDisposition.DetachedBeforeCommit ||
		Disposition == SnapshotBackgroundStoreDisposition.DetachedCommittedOrUncertain;
	internal bool CanFinalizeWorldDrop =>
		Disposition == SnapshotBackgroundStoreDisposition.CommittedCurrentSession;
	internal bool CanUseNativeFallback =>
		Disposition == SnapshotBackgroundStoreDisposition.SafeFailureCurrentSession;
	internal bool RequiresReconciliation =>
		Disposition == SnapshotBackgroundStoreDisposition.UncertainCurrentSession ||
		Disposition == SnapshotBackgroundStoreDisposition.DetachedCommittedOrUncertain;

	internal SnapshotBackgroundStoreCompletionResult WithReconciliationMarker(bool persisted)
	{
		ReconciliationMarkerPersisted = persisted;
		return this;
	}

	internal static SnapshotBackgroundStoreCompletionResult Completed(
		SnapshotBackgroundStoreDisposition disposition, SnapshotTransactionCommitResult transaction) =>
		new SnapshotBackgroundStoreCompletionResult(disposition, transaction);
}

internal static class SnapshotBackgroundStoreCompletionPolicy
{
	internal static SnapshotBackgroundStoreCompletionResult Resolve(SnapshotSaveResult saved,
		bool sessionCurrent, Func<SnapshotMutationResult> applyPersistedMutation)
	{
		if (!sessionCurrent)
		{
			bool definitelyNotCommitted = saved != null && !saved.Success && !saved.MayHaveCommitted;
			SnapshotTransactionCommitResult detached = SnapshotTransactionCommitResult.Failed(
				saved?.Success == true
					? "background-session-detached-after-persist"
					: "background-session-detached:" +
						(saved?.ErrorCode ?? "background-persistence-missing-result"),
				definitelyNotCommitted,
				saved?.RecoveryBackupPath, saved?.CandidateRecoveryPath,
				saved?.CandidateHistoryRecoveryPath);
			return SnapshotBackgroundStoreCompletionResult.Completed(
				definitelyNotCommitted
					? SnapshotBackgroundStoreDisposition.DetachedBeforeCommit
					: SnapshotBackgroundStoreDisposition.DetachedCommittedOrUncertain,
				detached);
		}

		if (saved?.Success != true)
		{
			SnapshotTransactionCommitResult failure = SnapshotTransactionCommitResult.Failed(
				saved?.ErrorCode ?? "background-persistence-missing-result",
				rollbackSucceeded: saved != null && !saved.MayHaveCommitted,
				recoveryBackupPath: saved?.RecoveryBackupPath,
				candidateRecoveryPath: saved?.CandidateRecoveryPath,
				candidateHistoryRecoveryPath: saved?.CandidateHistoryRecoveryPath);
			return SnapshotBackgroundStoreCompletionResult.Completed(
				failure.RollbackSucceeded
					? SnapshotBackgroundStoreDisposition.SafeFailureCurrentSession
					: SnapshotBackgroundStoreDisposition.UncertainCurrentSession,
				failure);
		}

		SnapshotMutationResult applied;
		try
		{
			applied = applyPersistedMutation?.Invoke() ??
				SnapshotMutationResult.Failed("background-memory-apply-missing");
		}
		catch (Exception exception)
		{
			applied = SnapshotMutationResult.Failed("background-memory-apply-threw:" +
				exception.GetType().Name);
		}
		SnapshotTransactionCommitResult transaction = applied.Success
			? SnapshotTransactionCommitResult.Succeeded()
			: SnapshotTransactionCommitResult.Failed(
				"background-memory-apply-failed:" + applied.ErrorCode, false,
				saved.RecoveryBackupPath, saved.CandidateRecoveryPath,
				saved.CandidateHistoryRecoveryPath);
		return SnapshotBackgroundStoreCompletionResult.Completed(
			transaction.Success
				? SnapshotBackgroundStoreDisposition.CommittedCurrentSession
				: SnapshotBackgroundStoreDisposition.UncertainCurrentSession,
			transaction);
	}
}

internal static class SnapshotBackgroundStoreCompletionRouter
{
	internal static T Route<T>(SnapshotBackgroundStoreCompletionResult completion,
		Func<T> onDetached, Func<T> onCommittedCurrentSession,
		Func<T> onSafeFailureCurrentSession, Func<T> onUncertainCurrentSession)
	{
		if (onDetached == null) throw new ArgumentNullException(nameof(onDetached));
		if (onCommittedCurrentSession == null)
			throw new ArgumentNullException(nameof(onCommittedCurrentSession));
		if (onSafeFailureCurrentSession == null)
			throw new ArgumentNullException(nameof(onSafeFailureCurrentSession));
		if (onUncertainCurrentSession == null)
			throw new ArgumentNullException(nameof(onUncertainCurrentSession));

		if (completion?.SessionDetached == true) return onDetached();
		if (completion?.CanFinalizeWorldDrop == true) return onCommittedCurrentSession();
		if (completion?.CanUseNativeFallback == true) return onSafeFailureCurrentSession();
		return onUncertainCurrentSession();
	}
}

internal static class SnapshotDocumentMutation
{
	internal static SnapshotMutationResult TryStoreExact(SnapshotDocument document,
		ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		if (document?.Groups == null) return SnapshotMutationResult.Failed("snapshot-document-null");
		if (identity.AssetId == 0 || identity.AssetGuid == Guid.Empty)
			return SnapshotMutationResult.Failed("invalid-identity");
		if (snapshot == null || snapshot.AssetId != identity.AssetId)
			return SnapshotMutationResult.Failed("snapshot-identity-mismatch");
		if (count <= 0) return SnapshotMutationResult.Failed("invalid-count");

		SnapshotDocumentGroup matchingGroup = null;
		for (int index = 0; index < document.Groups.Count; index++)
		{
			SnapshotDocumentGroup group = document.Groups[index];
			if (group == null) return SnapshotMutationResult.Failed("invalid-snapshot-group");
			if (group.AssetId != identity.AssetId) continue;
			if (matchingGroup != null) return SnapshotMutationResult.Failed("duplicate-asset-group");
			if (!Guid.TryParseExact(group.AssetGuid, "D", out Guid existingGuid))
				return SnapshotMutationResult.Failed("invalid-snapshot-group");
			if (existingGuid != identity.AssetGuid)
				return SnapshotMutationResult.Failed("asset-guid-conflict");
			matchingGroup = group;
		}

		byte[] state = snapshot.State;
		if (matchingGroup != null)
		{
			if (matchingGroup.Buckets == null)
				return SnapshotMutationResult.Failed("invalid-snapshot-group");
			for (int index = 0; index < matchingGroup.Buckets.Count; index++)
			{
				SnapshotDocumentBucket bucket = matchingGroup.Buckets[index];
				if (bucket == null || bucket.State == null)
					return SnapshotMutationResult.Failed("invalid-snapshot-bucket");
				if (bucket.Amount != snapshot.Amount || bucket.Quality != snapshot.Quality ||
					!SameBytes(bucket.State, state)) continue;
				long nextCount;
				try { nextCount = checked(bucket.Count + count); }
				catch (OverflowException) { return SnapshotMutationResult.Failed("count-overflow"); }
				bucket.Count = nextCount;
				return SnapshotMutationResult.Succeeded();
			}
		}

		SnapshotDocumentBucket added = new SnapshotDocumentBucket
		{
			Amount = snapshot.Amount,
			Quality = snapshot.Quality,
			State = state,
			Count = count
		};
		if (matchingGroup == null)
		{
			matchingGroup = new SnapshotDocumentGroup
			{
				AssetId = identity.AssetId,
				AssetGuid = identity.AssetGuid.ToString("D"),
				Buckets = new System.Collections.Generic.List<SnapshotDocumentBucket> { added }
			};
			int groupIndex = document.Groups.FindIndex(group => group.AssetId > identity.AssetId);
			if (groupIndex < 0) document.Groups.Add(matchingGroup);
			else document.Groups.Insert(groupIndex, matchingGroup);
			return SnapshotMutationResult.Succeeded();
		}

		int bucketIndex = matchingGroup.Buckets.FindIndex(bucket => CompareBuckets(added, bucket) < 0);
		if (bucketIndex < 0) matchingGroup.Buckets.Add(added);
		else matchingGroup.Buckets.Insert(bucketIndex, added);
		return SnapshotMutationResult.Succeeded();
	}

	private static int CompareBuckets(SnapshotDocumentBucket left, SnapshotDocumentBucket right)
	{
		int amount = left.Amount.CompareTo(right.Amount);
		if (amount != 0) return amount;
		int quality = left.Quality.CompareTo(right.Quality);
		if (quality != 0) return quality;
		return string.Compare(Convert.ToBase64String(left.State),
			Convert.ToBase64String(right.State), StringComparison.Ordinal);
	}

	private static bool SameBytes(byte[] left, byte[] right)
	{
		if (ReferenceEquals(left, right)) return true;
		if (left == null || right == null || left.Length != right.Length) return false;
		for (int index = 0; index < left.Length; index++)
		{
			if (left[index] != right[index]) return false;
		}
		return true;
	}
}
