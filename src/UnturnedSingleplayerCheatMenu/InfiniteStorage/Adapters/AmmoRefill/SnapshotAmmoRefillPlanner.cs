using System;
using System.Collections.Generic;
using System.Linq;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.AmmoRefill;

[Flags]
internal enum SnapshotAmmoRole
{
	None = 0,
	Source = 1,
	Target = 2
}

internal sealed class SnapshotAmmoCandidate
{
	internal SnapshotAmmoCandidate(string lotKey, ItemGroupIdentity identity, ItemSnapshot snapshot,
		long count, int capacity, SnapshotAmmoRole role, AmmoRefillAssetRules rules)
	{
		LotKey = lotKey ?? string.Empty;
		Identity = identity;
		Snapshot = snapshot?.Clone();
		Count = count;
		Capacity = capacity;
		Role = role;
		Rules = rules;
	}

	internal string LotKey { get; }
	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
	internal int Capacity { get; }
	internal SnapshotAmmoRole Role { get; }
	internal AmmoRefillAssetRules Rules { get; }
	internal bool IsSource => (Role & SnapshotAmmoRole.Source) != 0;
	internal bool IsTarget => (Role & SnapshotAmmoRole.Target) != 0;
}

internal sealed class SnapshotAmmoMutation
{
	internal SnapshotAmmoMutation(ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
		Count = count;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
}

internal sealed class SnapshotAmmoRefillPlan
{
	private readonly IReadOnlyList<SnapshotAmmoMutation> _removals;
	private readonly IReadOnlyList<SnapshotAmmoMutation> _additions;

	internal SnapshotAmmoRefillPlan(IEnumerable<SnapshotAmmoMutation> removals,
		IEnumerable<SnapshotAmmoMutation> additions, long totalRounds, long containerCount,
		long changedContainerCount)
	{
		_removals = (removals ?? Array.Empty<SnapshotAmmoMutation>()).ToArray();
		_additions = (additions ?? Array.Empty<SnapshotAmmoMutation>()).ToArray();
		TotalRounds = totalRounds;
		ContainerCount = containerCount;
		ChangedContainerCount = changedContainerCount;
	}

	internal long TotalRounds { get; }
	internal long ContainerCount { get; }
	internal long ChangedContainerCount { get; }
	internal bool Changed => _removals.Count > 0 || _additions.Count > 0;

	internal SnapshotMutationResult Apply(SnapshotKernelTransaction transaction)
	{
		if (transaction == null) return SnapshotMutationResult.Failed("transaction-missing");
		foreach (SnapshotAmmoMutation removal in _removals)
		{
			SnapshotWithdrawalResult result = transaction.Withdraw(removal.Identity, removal.Snapshot, removal.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return SnapshotMutationResult.Failed(result.ErrorCode);
		}
		foreach (SnapshotAmmoMutation addition in _additions)
		{
			SnapshotMutationResult result = transaction.Store(addition.Identity, addition.Snapshot, addition.Count);
			if (result.Success) continue;
			transaction.Rollback();
			return result;
		}
		return SnapshotMutationResult.Succeeded();
	}
}

internal static class SnapshotAmmoRefillPlanner
{
	internal static bool TryPlan(IEnumerable<SnapshotAmmoCandidate> source,
		out SnapshotAmmoRefillPlan plan, out string errorCode)
	{
		plan = null;
		errorCode = string.Empty;
		SnapshotAmmoCandidate[] candidates = (source ?? Enumerable.Empty<SnapshotAmmoCandidate>())
			.Where(value => value != null).ToArray();
		if (candidates.Length == 0 || candidates.Select(value => value.LotKey)
			.Distinct(StringComparer.Ordinal).Count() != candidates.Length || candidates.Any(value => !IsValid(value)))
		{
			errorCode = "invalid-ammo-candidate";
			return false;
		}

		List<AmmoRefillLot> lots = new List<AmmoRefillLot>();
		try
		{
			foreach (SnapshotAmmoCandidate candidate in candidates)
			{
				if (candidate.IsTarget)
				{
					string identity = AmmoRefillIdentity.BuildContainerIdentity(
						candidate.Identity.AssetGuid.ToString("D"), candidate.Snapshot.Quality,
						Convert.ToBase64String(candidate.Snapshot.State), candidate.Capacity);
					lots.Add(new AmmoRefillLot(candidate.LotKey, identity, candidate.Rules,
						candidate.Count, candidate.Snapshot.Amount, candidate.Capacity));
				}
				else
				{
					long donorRounds = checked(candidate.Count * candidate.Snapshot.Amount);
					lots.Add(AmmoRefillLot.SourceOnly(candidate.LotKey,
						"source|" + candidate.Identity.AssetGuid.ToString("N"), candidate.Rules, donorRounds));
				}
			}
		}
		catch (OverflowException)
		{
			errorCode = "arithmetic-overflow";
			return false;
		}

		if (!AmmoRefillAllocator.TryPlan(lots, out AmmoRefillPlan allocation))
		{
			errorCode = "allocation-failed";
			return false;
		}
		Dictionary<string, SnapshotAmmoCandidate> byKey = candidates.ToDictionary(value => value.LotKey,
			StringComparer.Ordinal);
		Dictionary<string, AmmoRefillLot> lotsByKey = lots.ToDictionary(value => value.LotKey,
			StringComparer.Ordinal);
		List<SnapshotAmmoMutation> removals = new List<SnapshotAmmoMutation>();
		List<SnapshotAmmoMutation> additions = new List<SnapshotAmmoMutation>();
		foreach (AmmoRefillLotAllocation targetAllocation in allocation.Allocations)
		{
			SnapshotAmmoCandidate candidate = byKey[targetAllocation.LotKey];
			if (AmmoRefillAllocator.CountChangedInstances(lotsByKey[targetAllocation.LotKey],
				targetAllocation.Allocation) == 0) continue;
			removals.Add(new SnapshotAmmoMutation(candidate.Identity, candidate.Snapshot, candidate.Count));
			AddBatch(additions, candidate, targetAllocation.Allocation);
		}
		foreach (AmmoRefillSourceRemainder remainder in allocation.SourceRemainders)
		{
			if (remainder.ConsumedRounds <= 0) continue;
			SnapshotAmmoCandidate candidate = byKey[remainder.LotKey];
			removals.Add(new SnapshotAmmoMutation(candidate.Identity, candidate.Snapshot, candidate.Count));
			if (candidate.Snapshot.Amount <= 0 || remainder.RemainingRounds <= 0) continue;
			long fullCount = remainder.RemainingRounds / candidate.Snapshot.Amount;
			long partialAmount = remainder.RemainingRounds % candidate.Snapshot.Amount;
			if (fullCount > 0) additions.Add(new SnapshotAmmoMutation(candidate.Identity, candidate.Snapshot, fullCount));
			if (partialAmount > 0)
				additions.Add(new SnapshotAmmoMutation(candidate.Identity,
					WithAmount(candidate.Snapshot, (byte)partialAmount), 1));
		}
		plan = new SnapshotAmmoRefillPlan(removals, additions, allocation.TotalRounds,
			allocation.ContainerCount, allocation.ChangedContainerCount);
		return true;
	}

	private static bool IsValid(SnapshotAmmoCandidate candidate)
	{
		if (string.IsNullOrWhiteSpace(candidate.LotKey) || candidate.Snapshot == null ||
			candidate.Snapshot.AssetId != candidate.Identity.AssetId || candidate.Count <= 0 ||
			candidate.Capacity <= 0 || candidate.Capacity > byte.MaxValue || candidate.Rules == null ||
			!string.Equals(AmmoRefillIdentity.NormalizeAssetGuid(candidate.Rules.AssetGuid),
				AmmoRefillIdentity.NormalizeAssetGuid(candidate.Identity.AssetGuid.ToString("D")),
				StringComparison.Ordinal) || (!candidate.IsSource && !candidate.IsTarget))
			return false;
		if (candidate.Snapshot.Amount > candidate.Capacity) return false;
		if (candidate.IsSource && !candidate.IsTarget && !candidate.Rules.IsDirectSource) return false;
		return true;
	}

	private static void AddBatch(ICollection<SnapshotAmmoMutation> output,
		SnapshotAmmoCandidate candidate, AmmoRefillBatch batch)
	{
		if (batch.FullCount > 0)
			output.Add(new SnapshotAmmoMutation(candidate.Identity,
				WithAmount(candidate.Snapshot, (byte)candidate.Capacity), batch.FullCount));
		if (batch.PartialCount > 0)
			output.Add(new SnapshotAmmoMutation(candidate.Identity,
				WithAmount(candidate.Snapshot, (byte)batch.PartialAmount), batch.PartialCount));
		if (batch.EmptyCount > 0)
			output.Add(new SnapshotAmmoMutation(candidate.Identity,
				WithAmount(candidate.Snapshot, 0), batch.EmptyCount));
	}

	private static ItemSnapshot WithAmount(ItemSnapshot source, byte amount) =>
		new ItemSnapshot(source.AssetId, amount, source.Quality, source.State);
}
