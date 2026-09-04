using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.ItemStacking;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.AmmoRefill;

internal sealed class SnapshotAmmoRefillModule : ISnapshotStorageModule
{
	internal const string ModuleId = "ammo-refill";
	private readonly ISnapshotStorageRuntimePort _storage;
	private readonly AmmoCompatibilityIndex _compatibility = new AmmoCompatibilityIndex();
	private readonly ManualLogSource _log;

	internal SnapshotAmmoRefillModule(ISnapshotStorageRuntimePort storage, bool enabled,
		ManualLogSource log = null)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
		IsEnabled = enabled;
		_log = log;
	}

	public string Id => ModuleId;
	public string Label => "一键填充弹药容器";
	public string Tooltip => "独立弹药填充模块负责解释原生数量与填充目标物品的兼容关系；快照内核不解释弹量。";
	public SnapshotModuleKind Kind => SnapshotModuleKind.GlobalAction;
	public bool IsEnabled { get; }

	public bool CanExecute(SnapshotModuleRequest request) => IsEnabled && _storage.IsOperational;

	public SnapshotModuleExecutionResult Execute(SnapshotModuleRequest request)
	{
		List<SnapshotAmmoCandidate> candidates = new List<SnapshotAmmoCandidate>();
		int sequence = 0;
		foreach (ItemGroupView group in _storage.Groups)
		{
			ItemAsset asset = Assets.find(EAssetType.ITEM, group.AssetId) as ItemAsset;
			if (asset == null || asset.GUID != group.AssetGuid ||
				!_compatibility.TryGetRefillRules(asset, out AmmoRefillAssetRules rules)) continue;
			SnapshotAmmoRole role = SnapshotAmmoRole.None;
			if (rules.IsDirectSource) role |= SnapshotAmmoRole.Source;
			if (rules.IsMagazine || rules.IsDirectTarget) role |= SnapshotAmmoRole.Target;
			if (role == SnapshotAmmoRole.None) continue;
			foreach (SnapshotBucketView bucket in group.Buckets)
			{
				candidates.Add(new SnapshotAmmoCandidate("lot-" + sequence++, group.Identity,
					bucket.Snapshot, bucket.Count, asset.MaxAmount, role, rules));
			}
		}
		if (candidates.Count == 0)
			return SnapshotModuleExecutionResult.Succeeded("没有可安全填充的弹药来源或容器。");
		if (!SnapshotAmmoRefillPlanner.TryPlan(candidates, out SnapshotAmmoRefillPlan plan, out string errorCode))
			return SnapshotModuleExecutionResult.Failed(errorCode);
		if (!plan.Changed)
			return SnapshotModuleExecutionResult.Succeeded("弹药容器已达到稳定状态。");
		SnapshotKernelTransaction transaction = _storage.BeginTransaction();
		SnapshotMutationResult applied = plan.Apply(transaction);
		if (!applied.Success) return SnapshotModuleExecutionResult.Failed(applied.ErrorCode);
		SnapshotTransactionCommitResult committed = _storage.Commit(transaction);
		if (!committed.Success) return SnapshotModuleExecutionResult.Failed(committed.ErrorCode);
		return SnapshotModuleExecutionResult.Succeeded(
			"已填充 " + plan.ChangedContainerCount + " 个容器；弹量总和 " + plan.TotalRounds + "。", true);
	}

	internal void RefreshAssets()
	{
		if (!IsEnabled) return;
		try
		{
			List<ItemAsset> items = new List<ItemAsset>();
			Assets.find(items);
			_compatibility.Rebuild(items);
		}
		catch (Exception exception)
		{
			_compatibility.Rebuild(null);
			_log?.LogWarning("Snapshot AmmoRefill asset refresh failed; Core remains available: " +
				exception.GetType().Name);
		}
	}
}
