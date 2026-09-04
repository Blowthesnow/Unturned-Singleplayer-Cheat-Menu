using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal enum SnapshotDestructionScope
{
	Single = 0,
	Bucket = 1,
	AssetId = 2
}

internal sealed class SnapshotDestructionResult
{
	internal bool Success { get; private set; }
	internal long DestroyedCount { get; private set; }
	internal bool RollbackSucceeded { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotDestructionResult Succeeded(long destroyedCount) =>
		new SnapshotDestructionResult
		{
			Success = true,
			DestroyedCount = destroyedCount,
			RollbackSucceeded = true
		};

	internal static SnapshotDestructionResult Failed(string errorCode, bool rollbackSucceeded = true) =>
		new SnapshotDestructionResult
		{
			ErrorCode = errorCode ?? "snapshot-destruction-failed",
			RollbackSucceeded = rollbackSucceeded
		};
}

internal sealed class SnapshotDestructionPlan
{
	private readonly IReadOnlyList<SnapshotDestructionMutation> _removals;

	internal SnapshotDestructionPlan(IEnumerable<SnapshotDestructionMutation> removals, long destroyedCount)
	{
		_removals = (removals ?? Array.Empty<SnapshotDestructionMutation>()).ToArray();
		DestroyedCount = destroyedCount;
	}

	internal long DestroyedCount { get; }
	internal int RemovalCount => _removals.Count;

	internal SnapshotMutationResult Apply(SnapshotKernelTransaction transaction)
	{
		if (transaction == null) return SnapshotMutationResult.Failed("transaction-unavailable");
		foreach (SnapshotDestructionMutation removal in _removals)
		{
			SnapshotWithdrawalResult removed = transaction.Withdraw(removal.Identity,
				removal.Snapshot, removal.Count);
			if (!removed.Success) return SnapshotMutationResult.Failed(removed.ErrorCode);
		}
		return SnapshotMutationResult.Succeeded();
	}
}

internal sealed class SnapshotDestructionMutation
{
	internal SnapshotDestructionMutation(ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
		Count = count;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
}

internal static class SnapshotDestructionPlanner
{
	internal static bool TryPlan(IEnumerable<ItemGroupView> groups, ItemGroupIdentity selectedIdentity,
		ItemSnapshot selectedSnapshot, SnapshotDestructionScope scope,
		out SnapshotDestructionPlan plan, out string errorCode)
	{
		plan = null;
		errorCode = string.Empty;
		if (!Enum.IsDefined(typeof(SnapshotDestructionScope), scope))
		{
			errorCode = "invalid-destruction-scope";
			return false;
		}
		if (selectedSnapshot == null || selectedSnapshot.AssetId != selectedIdentity.AssetId)
		{
			errorCode = "snapshot-identity-mismatch";
			return false;
		}

		ItemGroupView group = (groups ?? Array.Empty<ItemGroupView>())
			.FirstOrDefault(value => value != null && value.Identity.Equals(selectedIdentity));
		if (group == null)
		{
			errorCode = "group-not-found";
			return false;
		}
		SnapshotBucketView selectedBucket = group.Buckets
			.FirstOrDefault(value => value?.Snapshot != null && value.Snapshot.Equals(selectedSnapshot));
		if (selectedBucket == null || selectedBucket.Count <= 0)
		{
			errorCode = "snapshot-not-found";
			return false;
		}

		List<SnapshotDestructionMutation> removals = new List<SnapshotDestructionMutation>();
		long destroyedCount = 0;
		try
		{
			if (scope == SnapshotDestructionScope.Single)
			{
				removals.Add(new SnapshotDestructionMutation(group.Identity, selectedBucket.Snapshot, 1));
				destroyedCount = 1;
			}
			else if (scope == SnapshotDestructionScope.Bucket)
			{
				removals.Add(new SnapshotDestructionMutation(group.Identity, selectedBucket.Snapshot,
					selectedBucket.Count));
				destroyedCount = selectedBucket.Count;
			}
			else
			{
				foreach (SnapshotBucketView bucket in group.Buckets)
				{
					if (bucket?.Snapshot == null || bucket.Count <= 0) continue;
					removals.Add(new SnapshotDestructionMutation(group.Identity, bucket.Snapshot, bucket.Count));
					destroyedCount = checked(destroyedCount + bucket.Count);
				}
			}
		}
		catch (OverflowException)
		{
			errorCode = "destruction-count-overflow";
			return false;
		}
		if (removals.Count == 0 || destroyedCount <= 0)
		{
			errorCode = "snapshot-not-found";
			return false;
		}
		plan = new SnapshotDestructionPlan(removals, destroyedCount);
		return true;
	}
}
