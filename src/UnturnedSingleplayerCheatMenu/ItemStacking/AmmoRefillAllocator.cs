using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.ItemStacking;

internal readonly struct AmmoRefillSource
{
	internal AmmoRefillSource(long instanceCount, int amount, int capacity)
	{
		InstanceCount = instanceCount;
		Amount = amount;
		Capacity = capacity;
	}

	internal long InstanceCount { get; }
	internal int Amount { get; }
	internal int Capacity { get; }
}

internal readonly struct AmmoRefillBatch
{
	internal AmmoRefillBatch(long fullCount, long partialCount, int partialAmount, long emptyCount)
	{
		FullCount = fullCount;
		PartialCount = partialCount;
		PartialAmount = partialAmount;
		EmptyCount = emptyCount;
	}

	internal long FullCount { get; }
	internal long PartialCount { get; }
	internal int PartialAmount { get; }
	internal long EmptyCount { get; }
}

internal sealed class AmmoRefillAssetRules
{
	internal AmmoRefillAssetRules(string assetGuid, bool isMagazine, bool isDirectSource,
		IEnumerable<string> directSourceAssetGuids)
	{
		AssetGuid = AmmoRefillIdentity.NormalizeAssetGuid(assetGuid);
		IsMagazine = isMagazine;
		IsDirectSource = isDirectSource;
		DirectSourceAssetGuids = (directSourceAssetGuids ?? Enumerable.Empty<string>())
			.Select(AmmoRefillIdentity.NormalizeAssetGuid)
			.Where(value => !string.IsNullOrEmpty(value))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(value => value, StringComparer.Ordinal)
			.ToArray();
	}

	internal string AssetGuid { get; }
	internal bool IsMagazine { get; }
	internal bool IsDirectSource { get; }
	internal IReadOnlyList<string> DirectSourceAssetGuids { get; }
	internal bool IsDirectTarget => DirectSourceAssetGuids.Count > 0;

	internal bool CanDonateWithoutIndirectConversionTo(AmmoRefillAssetRules target)
	{
		if (target == null || !target.DirectSourceAssetGuids.Contains(AssetGuid, StringComparer.Ordinal)) return false;
		// Refillable boxes are valid donors when the receiver directly accepts every
		// upstream ammo type. Do not infer missing A -> C edges through A -> B -> C.
		return DirectSourceAssetGuids.All(source => string.Equals(source, AssetGuid, StringComparison.Ordinal) ||
			target.DirectSourceAssetGuids.Contains(source, StringComparer.Ordinal));
	}
}

internal sealed class AmmoRefillLot
{
	internal AmmoRefillLot(string lotKey, string containerIdentity, AmmoRefillAssetRules rules,
		long instanceCount, int amount, int capacity)
	{
		LotKey = lotKey ?? string.Empty;
		ContainerIdentity = containerIdentity ?? string.Empty;
		Rules = rules;
		InstanceCount = instanceCount;
		Amount = amount;
		Capacity = capacity;
		IsTargetContainer = true;
		DonorRounds = -1;
	}

	internal static AmmoRefillLot SourceOnly(string lotKey, string sourceIdentity,
		AmmoRefillAssetRules rules, long donorRounds)
	{
		return new AmmoRefillLot(lotKey, sourceIdentity, rules, 0, 0, 0, donorRounds);
	}

	private AmmoRefillLot(string lotKey, string containerIdentity, AmmoRefillAssetRules rules,
		long instanceCount, int amount, int capacity, long donorRounds)
	{
		LotKey = lotKey ?? string.Empty;
		ContainerIdentity = containerIdentity ?? string.Empty;
		Rules = rules;
		InstanceCount = instanceCount;
		Amount = amount;
		Capacity = capacity;
		IsTargetContainer = false;
		DonorRounds = donorRounds;
	}

	internal string LotKey { get; }
	internal string ContainerIdentity { get; }
	internal AmmoRefillAssetRules Rules { get; }
	internal long InstanceCount { get; }
	internal int Amount { get; }
	internal int Capacity { get; }
	internal bool IsTargetContainer { get; }
	internal long DonorRounds { get; }
}

internal sealed class AmmoRefillLotAllocation
{
	internal AmmoRefillLotAllocation(string lotKey, AmmoRefillBatch allocation)
	{
		LotKey = lotKey;
		Allocation = allocation;
	}

	internal string LotKey { get; }
	internal AmmoRefillBatch Allocation { get; }
}

internal sealed class AmmoRefillTransfer
{
	internal AmmoRefillTransfer(string sourceAssetGuid, string sourceLotKey,
		string targetAssetGuid, string targetLotKey, long rounds)
	{
		SourceAssetGuid = sourceAssetGuid;
		SourceLotKey = sourceLotKey;
		TargetAssetGuid = targetAssetGuid;
		TargetLotKey = targetLotKey;
		Rounds = rounds;
	}

	internal string SourceAssetGuid { get; }
	internal string SourceLotKey { get; }
	internal string TargetAssetGuid { get; }
	internal string TargetLotKey { get; }
	internal long Rounds { get; }
}

internal sealed class AmmoRefillSourceRemainder
{
	internal AmmoRefillSourceRemainder(string lotKey, long originalRounds, long remainingRounds)
	{
		LotKey = lotKey;
		OriginalRounds = originalRounds;
		RemainingRounds = remainingRounds;
	}

	internal string LotKey { get; }
	internal long OriginalRounds { get; }
	internal long RemainingRounds { get; }
	internal long ConsumedRounds => OriginalRounds - RemainingRounds;
}

internal sealed class AmmoRefillPlan
{
	internal AmmoRefillPlan(IReadOnlyList<AmmoRefillLotAllocation> allocations,
		IReadOnlyList<AmmoRefillTransfer> transfers, IReadOnlyList<AmmoRefillSourceRemainder> sourceRemainders,
		long containerCount, long changedContainerCount, long totalRounds)
	{
		Allocations = allocations;
		Transfers = transfers;
		SourceRemainders = sourceRemainders;
		ContainerCount = containerCount;
		ChangedContainerCount = changedContainerCount;
		TotalRounds = totalRounds;
	}

	internal IReadOnlyList<AmmoRefillLotAllocation> Allocations { get; }
	internal IReadOnlyList<AmmoRefillTransfer> Transfers { get; }
	internal IReadOnlyList<AmmoRefillSourceRemainder> SourceRemainders { get; }
	internal long ContainerCount { get; }
	internal long ChangedContainerCount { get; }
	internal long TotalRounds { get; }
	internal bool Changed => ChangedContainerCount > 0 || SourceRemainders.Any(value => value.ConsumedRounds > 0);
}

internal static class AmmoRefillIdentity
{
	internal static bool TryGetValidCapacity(int maxAmount, out int capacity)
	{
		capacity = maxAmount;
		return capacity > 0;
	}

	internal static string NormalizeAssetGuid(string value)
	{
		if (Guid.TryParse(value, out Guid guid)) return guid.ToString("N");
		return (value ?? string.Empty).Trim().ToLowerInvariant();
	}

	internal static string BuildContainerIdentity(string assetGuid, int quality, string stateIdentity, int capacity)
	{
		return NormalizeAssetGuid(assetGuid) + "|quality:" + quality + "|state:" +
			(stateIdentity ?? string.Empty) + "|capacity:" + capacity;
	}
}

/// <summary>
/// Shared round-conservation allocator used by inventory arranging and infinite storage.
/// Donor budgets are frozen from the pre-operation snapshot, so received rounds cannot be
/// donated again during the same plan. Cross-asset transfers require a direct source-to-target rule.
/// </summary>
internal static class AmmoRefillAllocator
{
	private sealed class FrozenBudget
	{
		internal AmmoRefillLot Lot;
		internal long OriginalRounds;
		internal long RemainingRounds;
	}

	internal static bool TryGetTotals(IEnumerable<AmmoRefillSource> sources, out long totalRounds, out long totalCapacity)
	{
		totalRounds = 0;
		totalCapacity = 0;
		if (sources == null) return false;
		bool hasSource = false;
		try
		{
			foreach (AmmoRefillSource source in sources)
			{
				hasSource = true;
				if (source.InstanceCount <= 0 || source.Capacity <= 0 || source.Amount < 0 || source.Amount > source.Capacity)
				{
					return false;
				}
				totalRounds = checked(totalRounds + checked(source.InstanceCount * source.Amount));
				totalCapacity = checked(totalCapacity + checked(source.InstanceCount * source.Capacity));
			}
		}
		catch (OverflowException)
		{
			totalRounds = 0;
			totalCapacity = 0;
			return false;
		}
		return hasSource && totalRounds <= totalCapacity;
	}

	internal static bool TryAllocate(long instanceCount, int capacity, ref long remainingRounds, out AmmoRefillBatch allocation)
	{
		allocation = default;
		if (instanceCount <= 0 || capacity <= 0 || remainingRounds < 0) return false;

		long fullCount = Math.Min(instanceCount, remainingRounds / capacity);
		remainingRounds -= fullCount * capacity;
		long availableAfterFull = instanceCount - fullCount;
		long partialCount = 0;
		int partialAmount = 0;
		if (availableAfterFull > 0 && remainingRounds > 0)
		{
			partialCount = 1;
			partialAmount = (int)Math.Min((long)capacity, remainingRounds);
			remainingRounds -= partialAmount;
		}
		allocation = new AmmoRefillBatch(fullCount, partialCount, partialAmount, availableAfterFull - partialCount);
		return true;
	}

	internal static bool TryPlan(IEnumerable<AmmoRefillLot> sourceLots, out AmmoRefillPlan plan)
	{
		plan = null;
		if (sourceLots == null) return false;

		List<AmmoRefillLot> lots = sourceLots.Where(value => value != null).ToList();
		if (lots.Count == 0 || lots.Select(value => value.LotKey).Distinct(StringComparer.Ordinal).Count() != lots.Count)
		{
			return false;
		}

		List<FrozenBudget> budgets = new List<FrozenBudget>(lots.Count);
		long totalRounds = 0;
		long containerCount = 0;
		try
		{
			foreach (AmmoRefillLot lot in lots)
			{
				if (!IsValid(lot)) return false;
				long rounds = lot.IsTargetContainer ? checked(lot.InstanceCount * lot.Amount) : lot.DonorRounds;
				totalRounds = checked(totalRounds + rounds);
				if (lot.IsTargetContainer) containerCount = checked(containerCount + lot.InstanceCount);
				budgets.Add(new FrozenBudget { Lot = lot, OriginalRounds = rounds, RemainingRounds = rounds });
			}
		}
		catch (OverflowException)
		{
			return false;
		}

		budgets = budgets
			.OrderBy(value => value.Lot.Rules.AssetGuid, StringComparer.Ordinal)
			.ThenBy(value => value.Lot.ContainerIdentity, StringComparer.Ordinal)
			.ThenBy(value => value.Lot.LotKey, StringComparer.Ordinal)
			.ToList();
		List<AmmoRefillLot> targets = lots.Where(value => value.IsTargetContainer)
			.OrderBy(TargetPriority)
			.ThenBy(value => value.Rules.AssetGuid, StringComparer.Ordinal)
			.ThenBy(value => value.ContainerIdentity, StringComparer.Ordinal)
			.ThenByDescending(value => value.Amount)
			.ThenBy(value => value.LotKey, StringComparer.Ordinal)
			.ToList();
		List<AmmoRefillLotAllocation> allocations = new List<AmmoRefillLotAllocation>(targets.Count);
		List<AmmoRefillTransfer> transfers = new List<AmmoRefillTransfer>();
		long changedContainerCount = 0;

		try
		{
			foreach (AmmoRefillLot target in targets)
			{
				long targetCapacity = checked(target.InstanceCount * target.Capacity);
				long assignedRounds = 0;
				assignedRounds = checked(assignedRounds + TakeFromBudgets(
					budgets.Where(value => string.Equals(value.Lot.ContainerIdentity, target.ContainerIdentity, StringComparison.Ordinal)),
					target, targetCapacity - assignedRounds, transfers));
				if (assignedRounds < targetCapacity && target.Rules.IsDirectTarget)
				{
					HashSet<string> directSources = new HashSet<string>(target.Rules.DirectSourceAssetGuids, StringComparer.Ordinal);
					assignedRounds = checked(assignedRounds + TakeFromBudgets(
						budgets.Where(value => !string.Equals(value.Lot.ContainerIdentity, target.ContainerIdentity, StringComparison.Ordinal) &&
							directSources.Contains(value.Lot.Rules.AssetGuid) &&
							(!value.Lot.IsTargetContainer || value.Lot.Rules.CanDonateWithoutIndirectConversionTo(target.Rules))),
						target, targetCapacity - assignedRounds, transfers));
				}

				long remainingForAllocation = assignedRounds;
				if (!TryAllocate(target.InstanceCount, target.Capacity, ref remainingForAllocation, out AmmoRefillBatch allocation) ||
					remainingForAllocation != 0)
				{
					return false;
				}
				changedContainerCount = checked(changedContainerCount + CountChangedInstances(target, allocation));
				allocations.Add(new AmmoRefillLotAllocation(target.LotKey, allocation));
			}
		}
		catch (OverflowException)
		{
			return false;
		}

		if (budgets.Any(value => value.Lot.IsTargetContainer && value.RemainingRounds != 0)) return false;
		List<AmmoRefillSourceRemainder> sourceRemainders = budgets
			.Where(value => !value.Lot.IsTargetContainer)
			.Select(value => new AmmoRefillSourceRemainder(value.Lot.LotKey, value.OriginalRounds, value.RemainingRounds))
			.ToList();
		plan = new AmmoRefillPlan(allocations, transfers, sourceRemainders,
			containerCount, changedContainerCount, totalRounds);
		return true;
	}

	private static bool IsValid(AmmoRefillLot lot)
	{
		if (string.IsNullOrWhiteSpace(lot.LotKey) || string.IsNullOrWhiteSpace(lot.ContainerIdentity) ||
			lot.Rules == null || string.IsNullOrWhiteSpace(lot.Rules.AssetGuid)) return false;
		return lot.IsTargetContainer
			? lot.InstanceCount > 0 && lot.Capacity > 0 && lot.Amount >= 0 && lot.Amount <= lot.Capacity
			: lot.DonorRounds >= 0 && lot.Rules.IsDirectSource;
	}

	private static int TargetPriority(AmmoRefillLot lot)
	{
		if (lot.Rules.IsDirectTarget && lot.Rules.IsMagazine) return 0;
		if (lot.Rules.IsDirectTarget) return 1;
		if (lot.Rules.IsMagazine && !lot.Rules.IsDirectSource) return 2;
		if (lot.Rules.IsMagazine) return 3;
		return lot.Rules.IsDirectSource ? 5 : 4;
	}

	private static long TakeFromBudgets(IEnumerable<FrozenBudget> sources, AmmoRefillLot target,
		long requestedRounds, ICollection<AmmoRefillTransfer> transfers)
	{
		if (requestedRounds <= 0) return 0;
		long taken = 0;
		foreach (FrozenBudget source in sources)
		{
			if (source.RemainingRounds <= 0) continue;
			long amount = Math.Min(source.RemainingRounds, requestedRounds - taken);
			if (amount <= 0) break;
			source.RemainingRounds -= amount;
			taken += amount;
			transfers.Add(new AmmoRefillTransfer(source.Lot.Rules.AssetGuid, source.Lot.LotKey,
				target.Rules.AssetGuid, target.LotKey, amount));
			if (taken == requestedRounds) break;
		}
		return taken;
	}

	internal static long CountChangedInstances(AmmoRefillLot lot, AmmoRefillBatch allocation)
	{
		if (lot == null || !lot.IsTargetContainer) return 0;
		long changed = 0;
		if (lot.Amount != lot.Capacity) changed = checked(changed + allocation.FullCount);
		if (allocation.PartialCount > 0 && lot.Amount != allocation.PartialAmount)
			changed = checked(changed + allocation.PartialCount);
		if (lot.Amount != 0) changed = checked(changed + allocation.EmptyCount);
		return changed;
	}
}
