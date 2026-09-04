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

internal sealed class SnapshotBackgroundStoreHandle
{
	internal SnapshotBackgroundStoreHandle(SnapshotStorageIdentity storageIdentity,
		string connectionKey, string persistencePath, SnapshotPersistence targetPersistence,
		ItemGroupIdentity identity, ItemSnapshot snapshot,
		SnapshotBackgroundPersistence persistence)
	{
		StorageIdentity = storageIdentity;
		ConnectionKey = connectionKey ?? string.Empty;
		PersistencePath = persistencePath ?? string.Empty;
		TargetPersistence = targetPersistence ?? throw new ArgumentNullException(nameof(targetPersistence));
		Identity = identity;
		Snapshot = snapshot?.Clone() ?? throw new ArgumentNullException(nameof(snapshot));
		Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
	}

	internal SnapshotStorageIdentity StorageIdentity { get; }
	internal string ConnectionKey { get; }
	internal string PersistencePath { get; }
	internal SnapshotPersistence TargetPersistence { get; }
	internal ItemGroupIdentity Identity { get; }
	internal ItemSnapshot Snapshot { get; }
	internal SnapshotBackgroundPersistence Persistence { get; }
	internal bool SessionInvalidated { get; private set; }

	internal void InvalidateSession() => SessionInvalidated = true;
}

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
	private SnapshotBackgroundStoreHandle _backgroundStore;

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
			if (_enabled && !value && _backgroundStore == null) _session.Reset();
			_enabled = value;
		}
	}

	public bool IsOperational => Enabled && RuntimeSessionBoundary.CaptureCurrent().CanUseSnapshotStorage &&
		_session.IsOperational && _backgroundStore == null;
	public IReadOnlyList<ItemGroupView> Groups => _session.Kernel.GetGroups()
		.Where(group => group.TotalCount > 0).ToArray();
	public bool TryGetGroup(ItemGroupIdentity identity, out ItemGroupView group)
	{
		if (IsOperational && _session.Kernel.TryGetGroup(identity, out group) && group.TotalCount > 0)
			return true;
		group = null;
		return false;
	}
	internal IReadOnlyList<SnapshotModuleAction> ModuleActions => _modules.Actions;
	internal SnapshotModuleExecutionResult ExecuteModule(string id, SnapshotModuleRequest request) =>
		IsOperational ? _modules.Execute(id, request) : SnapshotModuleExecutionResult.Failed("session-not-operational");
	internal bool CanExecuteModule(string id, SnapshotModuleRequest request) =>
		IsOperational && _modules.CanExecute(id, request);
	internal bool HasReconciliationLatchForCurrentIdentity()
	{
		SteamPlayerID nativePlayerId = Player.LocalPlayer?.channel?.owner?.playerID;
		if (!RuntimeSessionBoundary.CaptureCurrent().CanUseSnapshotStorage || Player.LocalPlayer == null || Level.info == null ||
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
		if (_backgroundStore != null)
		{
			if (!IsBackgroundStoreSessionCurrent(_backgroundStore))
			{
				_backgroundStore.InvalidateSession();
				ResetActiveSession();
			}
			return;
		}
		SteamPlayerID nativePlayerId = Player.LocalPlayer?.channel?.owner?.playerID;
		if (!Enabled)
		{
			_session.Reset();
			_activePersistence = null;
			_activePersistencePath = string.Empty;
			_lastIdentityFailure = string.Empty;
			return;
		}
		if (!RuntimeSessionBoundary.CaptureCurrent().CanUseSnapshotStorage || Player.LocalPlayer == null || Level.info == null ||
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

	internal bool TryBeginStoreCandidateInBackground(Item item, ItemAsset asset,
		out SnapshotBackgroundStoreHandle handle, out string errorCode)
	{
		handle = null;
		errorCode = string.Empty;
		if (_backgroundStore != null)
		{
			errorCode = "background-store-busy";
			return false;
		}
		if (!IsOperational || _activePersistence == null)
		{
			errorCode = "session-not-operational";
			return false;
		}
		if (!TryCaptureCurrentStorageContext(out SnapshotStorageIdentity liveIdentity,
			out string connectionKey) || !SameStorageIdentity(liveIdentity, _session.Identity))
		{
			errorCode = "background-session-not-current";
			return false;
		}
		if (item == null)
		{
			errorCode = "item-null";
			return false;
		}
		asset ??= ResolveAsset(item.id);
		if (asset == null || asset.id != item.id)
		{
			errorCode = "asset-mismatch";
			return false;
		}

		ItemGroupIdentity identity = IdentityFor(asset);
		ItemSnapshot snapshot = Describe(asset, item);
		EnsureDefaultTemplate(identity, asset);
		try
		{
			SnapshotDocument candidate = _session.Kernel.ExportDocument(_session.Identity);
			SnapshotMutationResult stored = SnapshotDocumentMutation.TryStoreExact(
				candidate, identity, snapshot, 1);
			if (!stored.Success)
			{
				errorCode = "background-stage-store-failed:" + stored.ErrorCode;
				return false;
			}
			SnapshotPersistence persistence = _activePersistence;
			string persistencePath = _activePersistencePath;
			SnapshotBackgroundPersistence background = SnapshotBackgroundPersistence.Start(
				candidate, persistence.Save);
			handle = new SnapshotBackgroundStoreHandle(_session.Identity, connectionKey,
				persistencePath, persistence, identity, snapshot, background);
			_backgroundStore = handle;
			return true;
		}
		catch (Exception exception)
		{
			errorCode = "background-stage-threw:" + exception.GetType().Name;
			return false;
		}
	}

	internal bool TryCompleteBackgroundStore(SnapshotBackgroundStoreHandle handle, bool wait,
		out SnapshotBackgroundStoreCompletionResult result)
	{
		result = null;
		if (handle == null || !ReferenceEquals(handle, _backgroundStore))
		{
			result = SnapshotBackgroundStoreCompletionResult.Completed(
				SnapshotBackgroundStoreDisposition.UncertainCurrentSession,
				SnapshotTransactionCommitResult.Failed(
					"background-store-handle-mismatch", false));
			return true;
		}

		SnapshotSaveResult saved;
		if (wait)
			saved = handle.Persistence.WaitForResult();
		else if (!handle.Persistence.TryGetResult(out saved))
			return false;

		_backgroundStore = null;
		bool sessionCurrent = !handle.SessionInvalidated &&
			IsBackgroundStoreSessionCurrent(handle);
		if (!sessionCurrent) handle.InvalidateSession();
		result = SnapshotBackgroundStoreCompletionPolicy.Resolve(saved, sessionCurrent, () =>
		{
			bool sameIdentity = _session.IsOperational &&
				string.Equals(_session.Identity.WorldKey, handle.StorageIdentity.WorldKey,
					StringComparison.Ordinal) &&
				string.Equals(_session.Identity.PlayerKey, handle.StorageIdentity.PlayerKey,
					StringComparison.Ordinal);
			return sameIdentity
				? _session.Kernel.Store(handle.Identity, handle.Snapshot, 1)
				: SnapshotMutationResult.Failed("background-session-identity-changed");
		});
		if (result.SessionDetached)
		{
			if (result.RequiresReconciliation)
			{
				SnapshotStorageRollbackReconciliationResult reconciliation =
					SnapshotStorageRollbackReconciliationCoordinator.Observe(result.Transaction,
						"autopickup-background-store-detached",
						reason => LatchReconciliationAt(handle.PersistencePath,
							handle.TargetPersistence, reason));
				result.WithReconciliationMarker(reconciliation.MarkerPersisted);
			}
			ResetActiveSession();
			return true;
		}
		if (result.Transaction.Success) return true;

		_session.CloseMutations();
		SnapshotStorageRollbackReconciliationResult currentReconciliation =
			SnapshotStorageRollbackReconciliationCoordinator.Observe(result.Transaction,
				"autopickup-background-store", LatchReconciliationFailure);
		result.WithReconciliationMarker(currentReconciliation.MarkerPersisted);
		return true;
	}

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
		if (!IsOperational) return false;
		ItemAsset asset = ResolveAsset(identity.AssetId);
		return SnapshotAssetGuard.Evaluate(identity, asset != null, asset?.id ?? (ushort)0,
			asset?.GUID ?? Guid.Empty).CanWithdraw;
	}

	internal bool CanDestroy(ItemGroupIdentity identity) => IsOperational && CanWithdraw(identity);

	public SnapshotKernelTransaction BeginTransaction() => IsOperational ? _session.BeginTransaction() : null;

	public SnapshotTransactionCommitResult Commit(SnapshotKernelTransaction transaction)
	{
		if (!IsOperational)
		{
			SnapshotTransactionCommitResult rollback = transaction?.Rollback();
			return SnapshotTransactionCommitResult.Failed("session-not-operational",
				rollback?.RollbackSucceeded ?? true);
		}
		return CommitWithReconciliation(transaction, "module-commit");
	}

	public SnapshotKernelCheckpoint CaptureCheckpoint() => _session.CaptureCheckpoint();

	public SnapshotTransactionCommitResult RestoreCheckpointAndPersist(SnapshotKernelCheckpoint checkpoint)
	{
		if (!IsOperational)
			return SnapshotTransactionCommitResult.Failed("session-not-operational", true);
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
		if (!RuntimeSessionBoundary.CaptureCurrent().CanUseSnapshotStorage)
		{
			_session.CloseMutations();
			return false;
		}
		return LatchBackgroundStoreReconciliation(reason);
	}

	internal bool LatchBackgroundStoreReconciliation(string reason)
	{
		return LatchReconciliationAt(_activePersistencePath, _activePersistence, reason);
	}

	private bool LatchReconciliationAt(string persistencePath, SnapshotPersistence persistence,
		string reason)
	{
		string latchPath = persistencePath ?? string.Empty;
		SnapshotReconciliationLatchResult result = SnapshotReconciliationLatchCoordinator.Execute(
			closeMutations: delegate
			{
				_session.CloseMutations();
				_reconciliationPathLatch.Latch(latchPath);
			},
			persistMarker: () => persistence?.TryCreateReconciliationLatch(reason) == true);
		_log?.LogError("InfiniteStorage Snapshot reconciliation required; mutations remain locked" +
			(result.MarkerPersisted ? ". Marker: " + latchPath + ".reconciliation-required"
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

	public void Dispose()
	{
		if (_backgroundStore != null)
		{
			SnapshotBackgroundStoreHandle abandoned = _backgroundStore;
			TryCompleteBackgroundStore(abandoned, wait: true,
				out SnapshotBackgroundStoreCompletionResult abandonedResult);
			if (abandonedResult?.CanFinalizeWorldDrop == true)
			{
				_session.CloseMutations();
				bool markerPersisted = _activePersistence?.TryCreateReconciliationLatch(
					"autopickup-background-store-completed-without-world-finalization") == true;
				_log?.LogError("InfiniteStorage background store completed during teardown before " +
					"the world drop could be finalized; reconciliation is required. MarkerPersisted=" +
					markerPersisted + ".");
			}
		}
		_session.Reset();
	}

	private bool IsBackgroundStoreSessionCurrent(SnapshotBackgroundStoreHandle handle)
	{
		if (handle == null || handle.SessionInvalidated ||
			!TryCaptureCurrentStorageContext(out SnapshotStorageIdentity liveIdentity,
				out string connectionKey)) return false;
		return string.Equals(connectionKey, handle.ConnectionKey, StringComparison.Ordinal) &&
			SameStorageIdentity(liveIdentity, handle.StorageIdentity);
	}

	private static bool TryCaptureCurrentStorageContext(out SnapshotStorageIdentity identity,
		out string connectionKey)
	{
		identity = default;
		connectionKey = string.Empty;
		try
		{
			RuntimeSessionState state = RuntimeSessionBoundary.CaptureCurrent();
			if (!state.CanUseSnapshotStorage || Player.LocalPlayer == null || Level.info == null)
				return false;
			SteamPlayerID nativePlayerId = Player.LocalPlayer.channel?.owner?.playerID;
			if (ReferenceEquals(nativePlayerId, null)) return false;
			object nativeSteamIdValue = nativePlayerId.GetType().GetProperty("steamID")?
				.GetValue(nativePlayerId, null);
			string nativeSteamId = nativeSteamIdValue?.ToString() ?? string.Empty;
			if (!SnapshotRuntimeIdentity.TryCreate(Provider.serverID, Level.info.name,
				nativeSteamId, nativePlayerId.characterID, Characters.selected,
				PlayerSavedata.hasSync, out identity, out _)) return false;
			connectionKey = RuntimeSessionBoundary.CaptureConnectionKey(state);
			return !string.IsNullOrEmpty(connectionKey);
		}
		catch (Exception)
		{
			identity = default;
			connectionKey = string.Empty;
			return false;
		}
	}

	private static bool SameStorageIdentity(SnapshotStorageIdentity left,
		SnapshotStorageIdentity right) =>
		string.Equals(left.WorldKey, right.WorldKey, StringComparison.Ordinal) &&
		string.Equals(left.PlayerKey, right.PlayerKey, StringComparison.Ordinal);

	private void ResetActiveSession()
	{
		_session.Reset();
		_activePersistence = null;
		_activePersistencePath = string.Empty;
	}

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
		if (!IsOperational)
		{
			SnapshotTransactionCommitResult rollback = transaction?.Rollback();
			return SnapshotTransactionCommitResult.Failed("session-not-operational",
				rollback?.RollbackSucceeded ?? true);
		}
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
