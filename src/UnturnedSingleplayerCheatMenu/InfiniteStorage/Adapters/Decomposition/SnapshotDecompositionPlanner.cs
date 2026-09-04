using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Decomposition;

internal enum SnapshotDecompositionScope
{
	Single = 0,
	Bucket = 1,
	AssetGroup = 2
}

internal sealed class SnapshotDecompositionOutput
{
	internal SnapshotDecompositionOutput(ItemGroupIdentity identity, ItemSnapshot defaultSnapshot,
		long instancesPerInput, bool transferSourceSnapshot,
		Func<ItemSnapshot, ItemSnapshot> sourceTransformer = null)
	{
		Identity = identity;
		DefaultSnapshot = defaultSnapshot?.Clone();
		InstancesPerInput = instancesPerInput;
		TransferSourceSnapshot = transferSourceSnapshot;
		SourceTransformer = sourceTransformer;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot DefaultSnapshot { get; }
	internal long InstancesPerInput { get; }
	internal bool TransferSourceSnapshot { get; }
	internal Func<ItemSnapshot, ItemSnapshot> SourceTransformer { get; }
}

internal sealed class SnapshotDecompositionRecipe
{
	internal SnapshotDecompositionRecipe(IEnumerable<SnapshotDecompositionOutput> outputs) =>
		Outputs = (outputs ?? Array.Empty<SnapshotDecompositionOutput>()).ToArray();

	internal IReadOnlyList<SnapshotDecompositionOutput> Outputs { get; }
}

internal sealed class SnapshotDecompositionMutation
{
	internal SnapshotDecompositionMutation(ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
		Count = count;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
}

internal sealed class SnapshotDecompositionPlan
{
	private readonly IReadOnlyList<SnapshotDecompositionMutation> _removals;
	private readonly IReadOnlyList<SnapshotDecompositionMutation> _additions;

	internal SnapshotDecompositionPlan(IEnumerable<SnapshotDecompositionMutation> removals,
		IEnumerable<SnapshotDecompositionMutation> additions, long inputCount, long outputCount)
	{
		_removals = (removals ?? Array.Empty<SnapshotDecompositionMutation>()).ToArray();
		_additions = (additions ?? Array.Empty<SnapshotDecompositionMutation>()).ToArray();
		InputCount = inputCount;
		OutputCount = outputCount;
	}

	internal long InputCount { get; }
	internal long OutputCount { get; }

	internal SnapshotMutationResult Apply(SnapshotKernelTransaction transaction)
	{
		if (transaction == null) return SnapshotMutationResult.Failed("transaction-missing");
		foreach (SnapshotDecompositionMutation removal in _removals)
		{
			SnapshotWithdrawalResult result = transaction.Withdraw(removal.Identity, removal.Snapshot, removal.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return SnapshotMutationResult.Failed(result.ErrorCode);
		}
		foreach (SnapshotDecompositionMutation addition in _additions)
		{
			SnapshotMutationResult result = transaction.Store(addition.Identity, addition.Snapshot, addition.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return result;
		}
		return SnapshotMutationResult.Succeeded();
	}
}

internal static class SnapshotDecompositionPlanner
{
	internal static bool TryPlan(IReadOnlyList<ItemGroupView> groups, ItemGroupIdentity selectedIdentity,
		ItemSnapshot selectedSnapshot, SnapshotDecompositionScope scope, SnapshotDecompositionRecipe recipe,
		out SnapshotDecompositionPlan plan, out string errorCode)
	{
		plan = null;
		errorCode = string.Empty;
		if (groups == null || selectedSnapshot == null || recipe?.Outputs == null || recipe.Outputs.Count == 0 ||
			!Enum.IsDefined(typeof(SnapshotDecompositionScope), scope) || recipe.Outputs.Any(output => !IsValid(output)))
		{
			errorCode = "invalid-decomposition-request";
			return false;
		}
		ItemGroupView group = groups.FirstOrDefault(value => value.Identity.Equals(selectedIdentity));
		SnapshotBucketView selectedBucket = group?.Buckets.FirstOrDefault(value => value.Snapshot.Equals(selectedSnapshot));
		if (selectedBucket == null)
		{
			errorCode = "snapshot-not-found";
			return false;
		}

		List<SnapshotDecompositionMutation> removals = new List<SnapshotDecompositionMutation>();
		if (scope == SnapshotDecompositionScope.Single)
			removals.Add(new SnapshotDecompositionMutation(selectedIdentity, selectedBucket.Snapshot, 1));
		else if (scope == SnapshotDecompositionScope.Bucket)
			removals.Add(new SnapshotDecompositionMutation(selectedIdentity, selectedBucket.Snapshot, selectedBucket.Count));
		else
			removals.AddRange(group.Buckets.Select(bucket =>
				new SnapshotDecompositionMutation(selectedIdentity, bucket.Snapshot, bucket.Count)));

		List<SnapshotDecompositionMutation> additions = new List<SnapshotDecompositionMutation>();
		long inputCount = 0;
		long outputCount = 0;
		try
		{
			foreach (SnapshotDecompositionMutation removal in removals)
			{
				inputCount = checked(inputCount + removal.Count);
				foreach (SnapshotDecompositionOutput output in recipe.Outputs)
				{
					long count = checked(removal.Count * output.InstancesPerInput);
					outputCount = checked(outputCount + count);
					ItemSnapshot snapshot = output.TransferSourceSnapshot
						? output.SourceTransformer?.Invoke(removal.Snapshot) ??
						  new ItemSnapshot(output.Identity.AssetId, removal.Snapshot.Amount,
							  removal.Snapshot.Quality, removal.Snapshot.State)
						: output.DefaultSnapshot;
					if (snapshot == null || snapshot.AssetId != output.Identity.AssetId)
					{
						errorCode = "output-transform-invalid";
						return false;
					}
					additions.Add(new SnapshotDecompositionMutation(output.Identity, snapshot, count));
				}
			}
		}
		catch (OverflowException)
		{
			errorCode = "arithmetic-overflow";
			return false;
		}
		plan = new SnapshotDecompositionPlan(removals, additions, inputCount, outputCount);
		return true;
	}

	private static bool IsValid(SnapshotDecompositionOutput output) =>
		output != null && output.Identity.AssetId != 0 && output.Identity.AssetGuid != Guid.Empty &&
		output.DefaultSnapshot != null && output.DefaultSnapshot.AssetId == output.Identity.AssetId &&
		output.InstancesPerInput > 0;
}
