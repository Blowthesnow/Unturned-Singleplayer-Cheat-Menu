using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;

internal enum SnapshotCraftingSearchRole
{
	Target = 0,
	Supply = 1
}

internal static class SnapshotCraftingSearchPolicy
{
	internal static bool ShouldHandleSearchContents(
		bool moduleOperational,
		int? activeSearchCount,
		bool hasResults) =>
		moduleOperational && activeSearchCount.GetValueOrDefault() > 0 && hasResults;

	internal static bool ShouldInjectSynthetic(SnapshotCraftingSearchRole role) =>
		role == SnapshotCraftingSearchRole.Supply;

	internal static bool ShouldExcludeItemToIgnore(object candidate, object itemToIgnore) =>
		itemToIgnore != null && ReferenceEquals(candidate, itemToIgnore);
}

internal enum SnapshotCraftingCountingMethod
{
	TotalItems = 0,
	TotalAmount = 1
}

internal sealed class SnapshotCraftingPlan
{
	private readonly IReadOnlyList<SnapshotCraftingMutation> _removals;
	private readonly IReadOnlyList<SnapshotCraftingMutation> _additions;

	internal SnapshotCraftingPlan(IEnumerable<SnapshotCraftingMutation> removals,
		IEnumerable<SnapshotCraftingMutation> additions)
	{
		_removals = (removals ?? Array.Empty<SnapshotCraftingMutation>()).ToArray();
		_additions = (additions ?? Array.Empty<SnapshotCraftingMutation>()).ToArray();
	}

	internal SnapshotMutationResult Apply(SnapshotKernelTransaction transaction)
	{
		if (transaction == null) return SnapshotMutationResult.Failed("transaction-missing");
		foreach (SnapshotCraftingMutation removal in _removals)
		{
			SnapshotWithdrawalResult result = transaction.Withdraw(removal.Identity, removal.Snapshot, removal.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return SnapshotMutationResult.Failed(result.ErrorCode);
		}
		foreach (SnapshotCraftingMutation addition in _additions)
		{
			SnapshotMutationResult result = transaction.Store(addition.Identity, addition.Snapshot, addition.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return result;
		}
		return SnapshotMutationResult.Succeeded();
	}
}

internal sealed class SnapshotCraftingMutation
{
	internal SnapshotCraftingMutation(ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
		Count = count;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
}

internal static class SnapshotCraftingPlanner
{
	internal static bool TryPlanSelected(IReadOnlyList<ItemGroupView> groups, ItemGroupIdentity identity,
		ItemSnapshot selectedSnapshot, long requiredUnits, SnapshotCraftingCountingMethod countingMethod,
		bool countEmptyAsOne, bool preserveZeroResidual, out SnapshotCraftingPlan plan,
		out string errorCode)
	{
		if (groups == null || selectedSnapshot == null || requiredUnits <= 0 ||
			(countingMethod != SnapshotCraftingCountingMethod.TotalItems &&
			 countingMethod != SnapshotCraftingCountingMethod.TotalAmount))
		{
			plan = null;
			errorCode = "invalid-requirement";
			return false;
		}
		ItemGroupView group = groups.FirstOrDefault(value => value.Identity.Equals(identity));
		return TryPlanSelected(group, identity, selectedSnapshot, requiredUnits, countingMethod,
			countEmptyAsOne, preserveZeroResidual, out plan, out errorCode);
	}

	internal static bool TryPlanSelected(ItemGroupView group, ItemGroupIdentity identity,
		ItemSnapshot selectedSnapshot, long requiredUnits, SnapshotCraftingCountingMethod countingMethod,
		bool countEmptyAsOne, bool preserveZeroResidual, out SnapshotCraftingPlan plan,
		out string errorCode)
	{
		plan = null;
		errorCode = string.Empty;
		if (selectedSnapshot == null || requiredUnits <= 0 ||
			(countingMethod != SnapshotCraftingCountingMethod.TotalItems &&
			 countingMethod != SnapshotCraftingCountingMethod.TotalAmount))
		{
			errorCode = "invalid-requirement";
			return false;
		}
		SnapshotBucketView bucket = group != null && group.Identity.Equals(identity)
			? group.Buckets.FirstOrDefault(value => value.Snapshot.Equals(selectedSnapshot))
			: null;
		if (bucket == null)
		{
			errorCode = "snapshot-not-found";
			return false;
		}
		List<SnapshotCraftingMutation> removals = new List<SnapshotCraftingMutation>();
		List<SnapshotCraftingMutation> additions = new List<SnapshotCraftingMutation>();
		if (countingMethod == SnapshotCraftingCountingMethod.TotalItems)
		{
			if (requiredUnits > bucket.Count)
			{
				errorCode = "insufficient-units";
				return false;
			}
			removals.Add(new SnapshotCraftingMutation(identity, bucket.Snapshot, requiredUnits));
			plan = new SnapshotCraftingPlan(removals, additions);
			return true;
		}

		long unitsPerSnapshot = bucket.Snapshot.Amount > 0 ? bucket.Snapshot.Amount : countEmptyAsOne ? 1L : 0L;
		if (unitsPerSnapshot <= 0)
		{
			errorCode = "insufficient-units";
			return false;
		}
		try
		{
			if (requiredUnits > checked(bucket.Count * unitsPerSnapshot))
			{
				errorCode = "insufficient-units";
				return false;
			}
			long wholeSnapshots = requiredUnits / unitsPerSnapshot;
			long partialUnits = requiredUnits % unitsPerSnapshot;
			long removeCount = checked(wholeSnapshots + (partialUnits > 0 ? 1L : 0L));
			removals.Add(new SnapshotCraftingMutation(identity, bucket.Snapshot, removeCount));
			if (preserveZeroResidual && bucket.Snapshot.Amount > 0 && wholeSnapshots > 0)
				additions.Add(new SnapshotCraftingMutation(identity,
					new ItemSnapshot(bucket.Snapshot.AssetId, 0, bucket.Snapshot.Quality,
						bucket.Snapshot.State), wholeSnapshots));
			if (partialUnits > 0 && bucket.Snapshot.Amount > partialUnits)
				additions.Add(new SnapshotCraftingMutation(identity,
					new ItemSnapshot(bucket.Snapshot.AssetId,
						(byte)(bucket.Snapshot.Amount - partialUnits), bucket.Snapshot.Quality,
						bucket.Snapshot.State), 1));
		}
		catch (OverflowException)
		{
			errorCode = "arithmetic-overflow";
			return false;
		}
		plan = new SnapshotCraftingPlan(removals, additions);
		return true;
	}

	internal static bool TryPlan(IReadOnlyList<ItemGroupView> groups, ItemGroupIdentity identity,
		long requiredUnits, SnapshotCraftingCountingMethod countingMethod, bool countEmptyAsOne,
		out SnapshotCraftingPlan plan, out string errorCode)
	{
		plan = null;
		errorCode = string.Empty;
		if (groups == null || requiredUnits <= 0 ||
			(countingMethod != SnapshotCraftingCountingMethod.TotalItems &&
			 countingMethod != SnapshotCraftingCountingMethod.TotalAmount))
		{
			errorCode = "invalid-requirement";
			return false;
		}
		ItemGroupView group = groups.FirstOrDefault(value => value.Identity.Equals(identity));
		if (group == null)
		{
			errorCode = "group-not-found";
			return false;
		}

		List<SnapshotCraftingMutation> removals = new List<SnapshotCraftingMutation>();
		List<SnapshotCraftingMutation> additions = new List<SnapshotCraftingMutation>();
		long remaining = requiredUnits;
		try
		{
			foreach (SnapshotBucketView bucket in group.Buckets)
			{
				if (remaining == 0) break;
				long unitsPerSnapshot = countingMethod == SnapshotCraftingCountingMethod.TotalItems
					? 1L
					: bucket.Snapshot.Amount > 0 ? bucket.Snapshot.Amount : countEmptyAsOne ? 1L : 0L;
				if (unitsPerSnapshot <= 0) continue;
				long bucketUnits = checked(unitsPerSnapshot * bucket.Count);
				if (remaining >= bucketUnits)
				{
					removals.Add(new SnapshotCraftingMutation(identity, bucket.Snapshot, bucket.Count));
					remaining -= bucketUnits;
					continue;
				}

				long wholeSnapshots = remaining / unitsPerSnapshot;
				long partialUnits = remaining % unitsPerSnapshot;
				long snapshotsToRemove = checked(wholeSnapshots + (partialUnits > 0 ? 1L : 0L));
				if (snapshotsToRemove > 0)
					removals.Add(new SnapshotCraftingMutation(identity, bucket.Snapshot, snapshotsToRemove));
				if (partialUnits > 0 && bucket.Snapshot.Amount > partialUnits)
				{
					ItemSnapshot residual = new ItemSnapshot(bucket.Snapshot.AssetId,
						(byte)(bucket.Snapshot.Amount - partialUnits), bucket.Snapshot.Quality, bucket.Snapshot.State);
					additions.Add(new SnapshotCraftingMutation(identity, residual, 1));
				}
				remaining = 0;
			}
		}
		catch (OverflowException)
		{
			errorCode = "arithmetic-overflow";
			return false;
		}
		if (remaining != 0)
		{
			errorCode = "insufficient-units";
			return false;
		}
		plan = new SnapshotCraftingPlan(removals, additions);
		return true;
	}
}
