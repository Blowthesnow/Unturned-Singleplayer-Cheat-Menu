using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;

internal sealed class SnapshotInfiniteStorageRuntime : IDisposable, ISnapshotStorageRuntimePort
{
	private readonly ManualLogSource _log;
	private readonly SnapshotStorageSession _session = new SnapshotStorageSession();
	private readonly SnapshotModuleRegistry _modules;
	private readonly SnapshotReconciliationPathLatch _reconciliationPathLatch =
		new SnapshotReconciliationPathLatch();
	private bool _enabled;
	private string _lastIdentityFailure = string.Empty;
	private SnapshotPersistence _activePersistence;
	private string _activePersistencePath = string.Empty;

	internal SnapshotInfiniteStorageRuntime(ManualLogSource log) : this(log, new SnapshotModuleRegistry())
	{
	}

	internal SnapshotInfiniteStorageRuntime(ManualLogSource log, SnapshotModuleRegistry modules)
	{
		_log = log;
		_modules = modules ?? throw new ArgumentNullException(nameof(modules));
	}

	internal bool Enabled
	{
		get => _enabled;
		set
		{
			if (_enabled && !value) _session.Reset();
			_enabled = value;
		}
	}

	public bool IsOperational => Enabled && _session.IsOperational;
	public IReadOnlyList<ItemGroupView> Groups => _session.Kernel.GetGroups()
		.Where(group => group.TotalCount > 0).ToArray();
	internal IReadOnlyList<SnapshotModuleAction> ModuleActions => _modules.Actions;
	internal SnapshotModuleExecutionResult ExecuteModule(string id, SnapshotModuleRequest request) =>
		_modules.Execute(id, request);
	internal bool CanExecuteModule(string id, SnapshotModuleRequest request) => _modules.CanExecute(id, request);
	internal bool HasReconciliationLatchForCurrentIdentity()
	{
		SteamPlayerID nativePlayerId = Player.LocalPlayer?.channel?.owner?.playerID;
		if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Level.info == null ||
			ReferenceEquals(nativePlayerId, null))
			return false;
		object nativeSteamIdValue = nativePlayerId.GetType().GetProperty("steamID")?.GetValue(nativePlayerId, null);
		string nativeSteamId = nativeSteamIdValue?.ToString() ?? string.Empty;
		if (!SnapshotRuntimeIdentity.TryCreate(Provider.serverID, Level.info.name, nativeSteamId,
			nativePlayerId.characterID, Characters.selected, PlayerSavedata.hasSync,
			out SnapshotStorageIdentity identity, out _)) return false;
		try
		{
			string path = SnapshotStoragePath.Resolve(Paths.ConfigPath, identity);
			return _reconciliationPathLatch.IsBlocked(path,
				new SnapshotPersistence(path).HasReconciliationLatch);
		}
		catch (Exception)
		{
			return false;
		}
	}

	internal void Pump()
	{
		SteamPlayerID nativePlayerId = Player.LocalPlayer?.channel?.owner?.playerID;
		if (!Enabled)
		{
			_session.Reset();
			_activePersistence = null;
			_activePersistencePath = string.Empty;
			_lastIdentityFailure = string.Empty;
			return;
		}
		if (!SingleplayerGuard.IsReady || Player.LocalPlayer == null || Level.info == null ||
			ReferenceEquals(nativePlayerId, null))
		{
			_session.Reset();
			_activePersistence = null;
			_activePersistencePath = string.Empty;
			return;
		}
		object nativeSteamIdValue = nativePlayerId.GetType().GetProperty("steamID")?.GetValue(nativePlayerId, null);
		string nativeSteamId = nativeSteamIdValue?.ToString() ?? string.Empty;
		if (!SnapshotRuntimeIdentity.TryCreate(Provider.serverID, Level.info.name, nativeSteamId,
			nativePlayerId.characterID, Characters.selected, PlayerSavedata.hasSync,
			out SnapshotStorageIdentity identity, out string identityError))
		{
			_session.Reset();
			LogIdentityFailureOnce(identityError + "|" + Provider.serverID + "|" + nativeSteamId + "|" + Characters.selected);
			return;
		}

		string path;
		try { path = SnapshotStoragePath.Resolve(Paths.ConfigPath, identity); }
		catch (Exception exception)
		{
			_session.Reset();
			LogIdentityFailureOnce("path-failed:" + exception.GetType().Name);
			return;
		}
		SnapshotPersistence persistence = new SnapshotPersistence(path);
		if (_reconciliationPathLatch.IsBlocked(path, persistence.HasReconciliationLatch))
		{
			_session.LatchIdentityFailure(identity);
			_activePersistence = persistence;
			_activePersistencePath = path;
			LogIdentityFailureOnce("reconciliation-required|" + path);
			return;
		}
		SnapshotSessionActivationResult activation = _session.Activate(identity,
			() => persistence.Load(identity), document => persistence.Save(document));
		if (activation.Success)
		{
			_lastIdentityFailure = string.Empty;
			if (activation.Attempted)
			{
				_activePersistence = persistence;
				_activePersistencePath = path;
				RefreshAssetMetadata();
			}
			return;
		}
		if (activation.Attempted)
			_log?.LogError("InfiniteStorage Snapshot activation failed: " + activation.ErrorCode);
	}

	internal bool Flush() => true;

	internal bool StoreCandidate(Item item, ItemAsset asset = null) => StoreItem(item, asset);
	internal bool StoreProduced(Item item, ItemAsset asset = null) => StoreItem(item, asset);

	internal bool RollbackCandidate(Item item, ItemAsset asset = null)
	{
		if (item == null || !IsOperational) return false;
		asset ??= ResolveAsset(item.id);
		if (asset == null || asset.id != item.id) return false;
		ItemGroupIdentity identity = IdentityFor(asset);
		ItemSnapshot snapshot = Describe(asset, item);
		SnapshotKernelTransaction transaction = BeginTransaction();
		if (transaction == null || !transaction.Withdraw(identity, snapshot, 1).Success) return false;
		return Commit(transaction).Success;
	}

	internal int StoreAllCarried()
	{
		PlayerInventory inventory = Player.LocalPlayer?.inventory;
		if (!IsOperational || inventory?.items == null) return 0;
		SnapshotInventoryCheckpoint inventoryBefore = null;
		IReadOnlyList<SnapshotInventoryBridge.CapturedItem> captured = null;
		SnapshotKernelTransaction transaction = null;
		bool nativeMutationAttempted = false;
		try
		{
			inventoryBefore = SnapshotInventoryBridge.Capture(inventory);
			captured = inventoryBefore.Items.Where(value => value.Page >= PlayerInventory.SLOTS).ToArray();
			if (captured.Count == 0) return 0;
			List<(ItemGroupIdentity Identity, ItemSnapshot Snapshot)> staged =
				new List<(ItemGroupIdentity, ItemSnapshot)>();
			foreach (SnapshotInventoryBridge.CapturedItem value in captured)
			{
				ItemAsset asset = ResolveAsset(value.Item.id);
				if (asset == null) return 0;
				ItemGroupIdentity identity = IdentityFor(asset);
				EnsureDefaultTemplate(identity, asset);
				staged.Add((identity, Describe(asset, value.Item)));
			}
			transaction = BeginTransaction();
			if (transaction == null) return 0;
			foreach (var value in staged)
			{
				if (transaction.Store(value.Identity, value.Snapshot, 1).Success) continue;
				RollbackTransaction(transaction, "store-all staging failure");
				return 0;
			}
			for (int index = captured.Count - 1; index >= 0; index--)
			{
				SnapshotInventoryBridge.CapturedItem value = captured[index];
				if (!SnapshotInventoryBridge.Matches(inventory, value))
				{
					bool storageRollbackProven = RollbackTransaction(transaction, "store-all identity drift");
					if (nativeMutationAttempted)
						TryRestoreAfterStorageRollback(storageRollbackProven, inventory,
							inventoryBefore, "store-all identity drift");
					return 0;
				}
				nativeMutationAttempted = true;
				inventory.removeItem(value.Page, value.Index);
			}
			SnapshotTransactionCommitResult committed = CommitWithReconciliation(transaction, "store-all");
			if (committed.Success) return captured.Count;
			TryRestoreAfterStorageRollback(committed.RollbackSucceeded, inventory,
				inventoryBefore, "store-all persistence failure");
			return 0;
		}
		catch (Exception exception)
		{
			bool storageRollbackProven = RollbackTransaction(transaction, "store-all exception");
			if (nativeMutationAttempted)
				TryRestoreAfterStorageRollback(storageRollbackProven, inventory,
					inventoryBefore, "store-all exception");
			_log?.LogError("InfiniteStorage Snapshot store-all failed closed:\n" + exception);
			return 0;
		}
	}

	internal long Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long requested)
	{
		PlayerInventory inventory = Player.LocalPlayer?.inventory;
		if (!IsOperational || snapshot == null || requested <= 0 || inventory == null) return 0;
		ItemGroupView group = Groups.FirstOrDefault(value => value.Identity.Equals(identity));
		SnapshotBucketView bucket = group?.Buckets.FirstOrDefault(value => value.Snapshot.Equals(snapshot));
		if (bucket == null) return 0;
		ItemAsset asset = ResolveAsset(identity.AssetId);
		SnapshotAssetResolution resolution = SnapshotAssetGuard.Evaluate(identity, asset != null,
			asset?.id ?? (ushort)0, asset?.GUID ?? Guid.Empty);
		if (!resolution.CanWithdraw) return 0;

		SnapshotInventoryCheckpoint inventoryBefore = null;
		SnapshotKernelTransaction transaction = null;
		long moved = 0;
		bool nativeMutationAttempted = false;
		try
		{
			inventoryBefore = SnapshotInventoryBridge.Capture(inventory);
			long target = Math.Min(requested, bucket.Count);
			while (moved < target)
			{
				Item output = new Item(snapshot.AssetId, snapshot.Amount, snapshot.Quality, snapshot.State);
				nativeMutationAttempted = true;
				if (!inventory.tryAddItem(output, true)) break;
				moved++;
			}
			if (moved == 0) return 0;
			transaction = BeginTransaction();
			if (transaction == null || !transaction.Withdraw(identity, snapshot, moved).Success)
			{
				bool storageRollbackProven = RollbackTransaction(transaction, "withdraw staging failure");
				TryRestoreAfterStorageRollback(storageRollbackProven, inventory,
					inventoryBefore, "withdraw staging failure");
				return 0;
			}
			SnapshotTransactionCommitResult committed = CommitWithReconciliation(transaction, "withdraw");
			if (committed.Success) return moved;
			TryRestoreAfterStorageRollback(committed.RollbackSucceeded, inventory,
				inventoryBefore, "withdraw persistence failure");
			return 0;
		}
		catch (Exception exception)
		{
			bool storageRollbackProven = RollbackTransaction(transaction, "withdraw exception");
			if (nativeMutationAttempted)
				TryRestoreAfterStorageRollback(storageRollbackProven, inventory,
					inventoryBefore, "withdraw exception");
			_log?.LogError("InfiniteStorage Snapshot withdraw failed closed:\n" + exception);
			return 0;
		}
	}

	internal SnapshotDestructionResult Destroy(ItemGroupIdentity identity, ItemSnapshot snapshot,
		SnapshotDestructionScope scope)
	{
		if (!IsOperational || snapshot == null || !CanDestroy(identity))
			return SnapshotDestructionResult.Failed("destruction-unavailable");
		if (!SnapshotDestructionPlanner.TryPlan(Groups, identity, snapshot, scope,
			out SnapshotDestructionPlan plan, out string errorCode))
			return SnapshotDestructionResult.Failed(errorCode);

		SnapshotKernelTransaction transaction = null;
		try
		{
			transaction = BeginTransaction();
			SnapshotMutationResult staged = plan.Apply(transaction);
			if (!staged.Success)
			{
				bool rollbackSucceeded = RollbackTransaction(transaction, "destroy staging failure");
				return SnapshotDestructionResult.Failed(staged.ErrorCode, rollbackSucceeded);
			}
			SnapshotTransactionCommitResult committed = CommitWithReconciliation(transaction, "destroy");
			return committed.Success
				? SnapshotDestructionResult.Succeeded(plan.DestroyedCount)
				: SnapshotDestructionResult.Failed(committed.ErrorCode, committed.RollbackSucceeded);
		}
		catch (Exception exception)
		{
			bool rollbackSucceeded = RollbackTransaction(transaction, "destroy exception");
			_log?.LogError("InfiniteStorage Snapshot destruction failed closed:\n" + exception);
			return SnapshotDestructionResult.Failed(
				"destruction-threw:" + exception.GetType().Name, rollbackSucceeded);
		}
	}

	internal bool CanWithdraw(ItemGroupIdentity identity)
	{
		ItemAsset asset = ResolveAsset(identity.AssetId);
		return SnapshotAssetGuard.Evaluate(identity, asset != null, asset?.id ?? (ushort)0,
			asset?.GUID ?? Guid.Empty).CanWithdraw;
	}

	internal bool CanDestroy(ItemGroupIdentity identity) => IsOperational && CanWithdraw(identity);

	public SnapshotKernelTransaction BeginTransaction() => _session.BeginTransaction();

	public SnapshotTransactionCommitResult Commit(SnapshotKernelTransaction transaction) =>
		CommitWithReconciliation(transaction, "module-commit");

	public SnapshotKernelCheckpoint CaptureCheckpoint() => _session.CaptureCheckpoint();

	public SnapshotTransactionCommitResult RestoreCheckpointAndPersist(SnapshotKernelCheckpoint checkpoint)
	{
		SnapshotTransactionCommitResult result = _session.RestoreCheckpointAndPersist(checkpoint);
		if (!result.Success)
			SnapshotStorageRollbackReconciliationCoordinator.Observe(result,
				"checkpoint-rollback", LatchReconciliationFailure);
		return result;
	}

	public SnapshotMutationResult StoreSnapshot(ItemGroupIdentity identity, ItemSnapshot snapshot)
	{
		SnapshotKernelTransaction transaction = BeginTransaction();
		if (transaction == null) return SnapshotMutationResult.Failed("session-not-operational");
		SnapshotMutationResult stored = transaction.Store(identity, snapshot, 1);
		if (!stored.Success)
		{
			transaction.Rollback();
			return stored;
		}
		SnapshotTransactionCommitResult committed = Commit(transaction);
		return committed.Success ? SnapshotMutationResult.Succeeded() :
			SnapshotMutationResult.Failed(committed.ErrorCode);
	}

	public SnapshotMutationResult RegisterDefaultTemplate(ItemGroupIdentity identity, ItemSnapshot snapshot) =>
		IsOperational ? _session.Kernel.RegisterDefaultTemplate(identity, snapshot) :
			SnapshotMutationResult.Failed("session-not-operational");

	public bool LatchReconciliationFailure(string reason)
	{
		string latchPath = _activePersistencePath;
		SnapshotReconciliationLatchResult result = SnapshotReconciliationLatchCoordinator.Execute(
			closeMutations: delegate
			{
				_session.CloseMutations();
				_reconciliationPathLatch.Latch(latchPath);
			},
			persistMarker: () => _activePersistence?.TryCreateReconciliationLatch(reason) == true);
		_log?.LogError("InfiniteStorage Snapshot reconciliation required; mutations remain locked" +
			(result.MarkerPersisted ? ". Marker: " + _activePersistencePath + ".reconciliation-required"
				: "; marker creation failed and the current session remains locked.") +
			" Reason: " + (reason ?? string.Empty) + "; latch=" + result.ErrorCode);
		return result.Success;
	}

	internal void RefreshAssetMetadata()
	{
		foreach (ItemGroupView group in Groups)
		{
			ItemAsset asset = ResolveAsset(group.AssetId);
			if (asset != null && asset.GUID == group.AssetGuid) EnsureDefaultTemplate(group.Identity, asset);
		}
	}

	public void Dispose() => _session.Reset();

	private bool StoreItem(Item item, ItemAsset asset)
	{
		if (item == null || !IsOperational) return false;
		asset ??= ResolveAsset(item.id);
		if (asset == null || asset.id != item.id) return false;
		ItemGroupIdentity identity = IdentityFor(asset);
		EnsureDefaultTemplate(identity, asset);
		return StoreSnapshot(identity, Describe(asset, item)).Success;
	}

	private void EnsureDefaultTemplate(ItemGroupIdentity identity, ItemAsset asset)
	{
		try
		{
			Item full = new Item(asset, EItemOrigin.ADMIN);
			_session.Kernel.RegisterDefaultTemplate(identity, Describe(asset, full));
		}
		catch (Exception exception)
		{
			_log?.LogWarning("InfiniteStorage default template unavailable for item " + asset.id + ": " +
				exception.GetType().Name);
		}
	}

	private static ItemSnapshot Describe(ItemAsset asset, Item item)
	{
		ItemSnapshot exact = new ItemSnapshot(item.id, item.amount, item.quality,
			SnapshotInventoryBridge.CloneState(item.state));
		bool isFarm = asset is ItemBarricadeAsset barricade && barricade.build == EBuild.FARM;
		return SnapshotCanonicalizer.Canonicalize(exact,
			new SnapshotCanonicalizationFacts(isFarm, asset.MaxAmountAsByte));
	}

	private static ItemGroupIdentity IdentityFor(ItemAsset asset) => new ItemGroupIdentity(asset.id, asset.GUID);
	private static ItemAsset ResolveAsset(ushort assetId) => Assets.find(EAssetType.ITEM, assetId) as ItemAsset;

	private bool TryRestoreInventory(PlayerInventory inventory,
		SnapshotInventoryCheckpoint checkpoint, string context)
	{
		SnapshotNativeRecoveryResult result = SnapshotNativeRecoveryCoordinator.Execute(
			restoreNative: () => SnapshotInventoryBridge.Restore(inventory, checkpoint),
			persistReconciliation: () => LatchReconciliationFailure("native-rollback-failed:" + context));
		if (result.Success) return true;
		_log?.LogError("InfiniteStorage Snapshot native rollback failed closed during " + context +
			"; stage=" + result.ErrorCode + "; markerPersisted=" + result.MarkerPersisted + ".");
		return false;
	}

	private bool TryRestoreAfterStorageRollback(bool storageRollbackProven,
		PlayerInventory inventory, SnapshotInventoryCheckpoint checkpoint, string context)
	{
		SnapshotCrossBoundaryRecoveryResult result = SnapshotCrossBoundaryRecoveryCoordinator.Execute(
			storageRollbackProven,
			restoreNative: () => TryRestoreInventory(inventory, checkpoint, context));
		if (!result.NativeRestoreAttempted)
			_log?.LogError("InfiniteStorage Snapshot native rollback skipped because storage rollback is unproven during " +
				context + ". Persistent reconciliation remains required.");
		return result.Success;
	}

	private bool RollbackTransaction(SnapshotKernelTransaction transaction, string context)
	{
		if (transaction == null) return true;
		SnapshotTransactionCommitResult result = transaction.Rollback();
		SnapshotStorageRollbackReconciliationCoordinator.Observe(result, context,
			LatchReconciliationFailure);
		return result.RollbackSucceeded;
	}

	private SnapshotTransactionCommitResult CommitWithReconciliation(
		SnapshotKernelTransaction transaction, string context)
	{
		SnapshotTransactionCommitResult result = _session.Commit(transaction);
		SnapshotStorageRollbackReconciliationCoordinator.Observe(result, context,
			LatchReconciliationFailure);
		return result;
	}

	private void LogIdentityFailureOnce(string failure)
	{
		if (string.Equals(_lastIdentityFailure, failure, StringComparison.Ordinal)) return;
		_lastIdentityFailure = failure;
		_log?.LogError("InfiniteStorage Snapshot identity rejected: " + failure);
	}
}
