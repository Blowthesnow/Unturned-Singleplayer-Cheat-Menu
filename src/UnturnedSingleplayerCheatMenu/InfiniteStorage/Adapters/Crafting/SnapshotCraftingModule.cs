using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;

internal sealed class SnapshotCraftingSyntheticItem
{
	internal SnapshotCraftingSyntheticItem(ItemGroupIdentity identity, ItemSnapshot snapshot)
	{
		Identity = identity;
		Snapshot = snapshot?.Clone();
	}

	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
}

internal sealed class SnapshotCraftingModule : ISnapshotStorageModule
{
	internal const string ModuleId = "crafting";
	private readonly ISnapshotStorageRuntimePort _storage;

	internal SnapshotCraftingModule(ISnapshotStorageRuntimePort storage, bool enabled)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
		IsEnabled = enabled;
	}

	public string Id => ModuleId;
	public string Label => "制作适配";
	public string Tooltip => "独立制作模块负责解释所需物品总数与原生数量总和；快照内核不解释制作语义。";
	public SnapshotModuleKind Kind => SnapshotModuleKind.RuntimeHook;
	public bool IsEnabled { get; }
	internal bool IsOperational => IsEnabled && _storage.IsOperational;
	public bool CanExecute(SnapshotModuleRequest request) => false;
	public SnapshotModuleExecutionResult Execute(SnapshotModuleRequest request) =>
		SnapshotModuleExecutionResult.Failed("runtime-hook-only");

	internal IReadOnlyList<SnapshotCraftingSyntheticItem> FindSyntheticItems(BlueprintSupply supply,
		in PlayerInventorySearchParameters parameters)
	{
		if (!IsEnabled || !_storage.IsOperational || supply == null || !parameters.AssetRef.HasValue ||
			parameters.Results == null) return Array.Empty<SnapshotCraftingSyntheticItem>();
		ItemAsset requested = supply.FindItemAsset();
		if (requested == null) return Array.Empty<SnapshotCraftingSyntheticItem>();
		ItemGroupView group = _storage.Groups.FirstOrDefault(value =>
			value.AssetId == requested.id && value.AssetGuid == requested.GUID);
		if (group == null) return Array.Empty<SnapshotCraftingSyntheticItem>();

		ItemJar itemToIgnore = parameters.ItemToIgnore;
		PlayerInventorySearchResultV2[] eligibleNativeResults = parameters.Results
			.Where(result => !SnapshotCraftingSearchPolicy.ShouldExcludeItemToIgnore(
				result.Jar, itemToIgnore)).ToArray();
		long existingUnits = supply.CountingMethod == ECraftingInputCountingMethod.TotalItems
			? eligibleNativeResults.Length
			: eligibleNativeResults.Aggregate(0L, (total, result) => checked(total +
				(supply.ShouldCountEmptyAsOne ? Math.Max(1, (int)result.Jar.item.amount) : result.Jar.item.amount)));
		long needed = Math.Max(0L, (long)supply.amount - existingUnits);
		if (needed == 0) return Array.Empty<SnapshotCraftingSyntheticItem>();
		IEnumerable<SnapshotBucketView> buckets = Order(group.Buckets, supply.Prioritization);
		List<SnapshotCraftingSyntheticItem> result = new List<SnapshotCraftingSyntheticItem>();
		foreach (SnapshotBucketView bucket in buckets)
		{
			ItemSnapshot snapshot = bucket.Snapshot;
			if ((snapshot.Amount < 1 && !parameters.IncludeEmpty) ||
				(snapshot.Quality >= 100 && !parameters.IncludeMaxQuality) ||
				(parameters.ExcludeFullAmount && snapshot.Amount >= requested.MaxAmount)) continue;
			long contribution = supply.CountingMethod == ECraftingInputCountingMethod.TotalItems
				? 1L
				: supply.ShouldCountEmptyAsOne ? Math.Max(1, (int)snapshot.Amount) : snapshot.Amount;
			if (contribution <= 0) continue;
			long take = Math.Min(bucket.Count, DivideRoundUp(needed, contribution));
			for (long index = 0; index < take; index++)
			{
				result.Add(new SnapshotCraftingSyntheticItem(group.Identity, snapshot));
				needed -= Math.Min(needed, contribution);
				if (parameters.MaxResultsCount > 0 &&
					parameters.Results.Count + result.Count >= parameters.MaxResultsCount) return result;
				if (needed == 0) return result;
			}
		}
		return result;
	}

	internal bool ConsumeWhole(SnapshotCraftingSyntheticItem synthetic)
	{
		if (synthetic?.Snapshot == null || !SnapshotCraftingPlanner.TryPlanSelected(_storage.Groups,
			synthetic.Identity, synthetic.Snapshot, 1, SnapshotCraftingCountingMethod.TotalItems,
			countEmptyAsOne: false, preserveZeroResidual: false, out SnapshotCraftingPlan plan, out _))
			return false;
		SnapshotKernelTransaction transaction = _storage.BeginTransaction();
		return transaction != null && plan.Apply(transaction).Success && _storage.Commit(transaction).Success;
	}

	internal uint ConsumeAmount(SnapshotCraftingSyntheticItem synthetic, uint desiredAmount,
		bool alwaysDeleteAtZeroAmount)
	{
		if (synthetic?.Snapshot == null || desiredAmount == 0) return 0;
		ItemAsset asset = Assets.find(EAssetType.ITEM, synthetic.Identity.AssetId) as ItemAsset;
		if (asset == null || asset.GUID != synthetic.Identity.AssetGuid) return 0;
		uint removed = Math.Min(desiredAmount, synthetic.Snapshot.Amount);
		if (removed == 0) return 0;
		bool preserveZero = removed == synthetic.Snapshot.Amount &&
			!alwaysDeleteAtZeroAmount && !asset.ShouldDeleteAtZeroAmount;
		if (!SnapshotCraftingPlanner.TryPlanSelected(_storage.Groups, synthetic.Identity,
			synthetic.Snapshot, removed, SnapshotCraftingCountingMethod.TotalAmount,
			countEmptyAsOne: false, preserveZero, out SnapshotCraftingPlan plan, out _)) return 0;
		SnapshotKernelTransaction transaction = _storage.BeginTransaction();
		return transaction != null && plan.Apply(transaction).Success && _storage.Commit(transaction).Success
			? removed
			: 0;
	}

	internal bool StoreProduced(Item item)
	{
		ItemAsset asset = item?.GetAsset();
		if (asset == null) return false;
		ItemSnapshot snapshot = Canonicalize(asset, item);
		ItemGroupIdentity identity = new ItemGroupIdentity(asset.id, asset.GUID);
		Item fullItem = new Item(asset, EItemOrigin.ADMIN);
		if (!_storage.RegisterDefaultTemplate(identity, Canonicalize(asset, fullItem)).Success) return false;
		return _storage.StoreSnapshot(identity, snapshot).Success;
	}

	internal SnapshotKernelCheckpoint CaptureCheckpoint() => _storage.CaptureCheckpoint();
	internal SnapshotTransactionCommitResult RestoreCheckpoint(SnapshotKernelCheckpoint checkpoint) =>
		_storage.RestoreCheckpointAndPersist(checkpoint);
	internal bool LatchReconciliationFailure(string reason) =>
		_storage.LatchReconciliationFailure(reason);

	private static IEnumerable<SnapshotBucketView> Order(IEnumerable<SnapshotBucketView> source,
		ECraftingInputPrioritization prioritization)
	{
		return prioritization switch
		{
			ECraftingInputPrioritization.LowestAmount => source.OrderBy(value => value.Snapshot.Amount),
			ECraftingInputPrioritization.HighestAmount => source.OrderByDescending(value => value.Snapshot.Amount),
			ECraftingInputPrioritization.LowestQuality => source.OrderBy(value => value.Snapshot.Quality),
			ECraftingInputPrioritization.HighestQuality => source.OrderByDescending(value => value.Snapshot.Quality),
			_ => source
		};
	}

	private static long DivideRoundUp(long value, long divisor) => checked((value + divisor - 1) / divisor);

	private static ItemSnapshot Canonicalize(ItemAsset asset, Item item)
	{
		ItemSnapshot exact = new ItemSnapshot(item.id, item.amount, item.quality,
			item.state == null ? Array.Empty<byte>() : (byte[])item.state.Clone());
		bool isFarm = asset is ItemBarricadeAsset barricade && barricade.build == EBuild.FARM;
		return SnapshotCanonicalizer.Canonicalize(exact,
			new SnapshotCanonicalizationFacts(isFarm, asset.MaxAmountAsByte));
	}
}
