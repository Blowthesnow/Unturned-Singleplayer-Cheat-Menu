using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal sealed class AutoPickupCandidate
{
	public uint InstanceId { get; set; }

	public byte RegionX { get; set; }

	public byte RegionY { get; set; }

	public Vector3 Position { get; set; }

	public float DistanceSquared { get; set; }

	public byte Amount { get; set; }

	public byte Quality { get; set; }

	public int Rarity { get; set; }

	public byte[] State { get; set; }

	public int SnapshotVersion { get; set; }

	public ItemDrop Drop { get; set; }

	public ItemAsset Asset { get; set; }

	public AutoPickupItemDescriptor Descriptor { get; set; }
}

internal sealed class AutoPickupCandidateAdapter
{
	private readonly List<AutoPickupCandidate> _result = new List<AutoPickupCandidate>();

	public IReadOnlyList<AutoPickupCandidate> Adapt(NearbyItemSnapshot snapshot, float radius, int snapshotVersion, Vector3? consumerCenter = null)
	{
		_result.Clear();
		if (snapshot == null)
		{
			return _result;
		}
		radius = Mathf.Clamp(radius, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
		float radiusSquared = radius * radius;
		for (int index = 0; index < snapshot.Items.Count; index++)
		{
			NearbyGroundItem raw = snapshot.Items[index];
			ItemDrop drop = raw.Drop;
			ItemAsset asset = raw.Asset;
			float distanceSquared = consumerCenter.HasValue ? (raw.Position - consumerCenter.Value).sqrMagnitude : raw.DistanceSquared;
			if (drop == null || asset == null || AutoPickupDropGate.IsSuppressed(drop) || distanceSquared > radiusSquared)
			{
				continue;
			}
			_result.Add(new AutoPickupCandidate
			{
				InstanceId = raw.InstanceId,
				RegionX = raw.RegionX,
				RegionY = raw.RegionY,
				Position = raw.Position,
				DistanceSquared = distanceSquared,
				Amount = raw.Amount,
				Quality = raw.Quality,
				Rarity = (int)asset.rarity,
				State = raw.State == null ? Array.Empty<byte>() : (byte[])raw.State.Clone(),
				SnapshotVersion = snapshotVersion,
				Drop = drop,
				Asset = asset,
				Descriptor = new AutoPickupItemDescriptor
				{
					AssetId = asset.id,
					Guid = asset.GUID.ToString("D"),
					AssetName = asset.name ?? string.Empty,
					DisplayName = asset.FriendlyName ?? string.Empty,
					ItemType = asset.type.ToString(),
					Category = AssetCatalog.GetItemCategory(asset).ToString()
				}
			});
		}
		_result.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
		return _result;
	}

	public static bool IsCurrent(AutoPickupCandidate candidate)
	{
		if (candidate == null || ItemManager.regions == null || !Regions.checkSafe(candidate.RegionX, candidate.RegionY))
		{
			return false;
		}
		ItemRegion region = ItemManager.regions[candidate.RegionX, candidate.RegionY];
		if (region == null)
		{
			return false;
		}
		for (int i = 0; i < region.drops.Count; i++)
		{
			ItemDrop drop = region.drops[i];
			if (drop != null && drop.instanceID == candidate.InstanceId && drop.model != null)
			{
				return true;
			}
		}
		return false;
	}
}

internal static class AutoPickupDropGate
{
	private sealed class PendingDrop
	{
		public ushort Id;
		public byte Amount;
		public byte Quality;
		public int StateHash;
		public float ExpiresAt;
	}

	private static readonly HashSet<Transform> SuppressedModels = new HashSet<Transform>();
	private static readonly AutoPickupDropBlockRegistry SuppressedItemIds = new AutoPickupDropBlockRegistry();
	private static readonly List<PendingDrop> PendingDrops = new List<PendingDrop>();
	private static PlayerInventory _inventory;
	private static bool _installed;
	private static float _dropBlockDurationSeconds = 60f;

	public static void Install()
	{
		if (_installed)
		{
			return;
		}
		_installed = true;
		ItemManager.onItemDropAdded += OnItemDropAdded;
		ItemManager.onItemDropRemoved += OnItemDropRemoved;
	}

	public static void Attach(PlayerInventory inventory)
	{
		if (_inventory == inventory)
		{
			return;
		}
		if (_inventory != null)
		{
			_inventory.onDropItemRequested -= OnDropItemRequested;
		}
		_inventory = inventory;
		if (_inventory != null)
		{
			_inventory.onDropItemRequested += OnDropItemRequested;
		}
	}

	public static void SetDropBlockDurationMinutes(int minutes)
	{
		int normalized = Math.Max(AutoPickupSettings.MinDropBlockMinutes, Math.Min(AutoPickupSettings.MaxDropBlockMinutes, minutes));
		_dropBlockDurationSeconds = normalized * 60f;
	}

	public static bool IsSuppressed(ItemDrop drop)
	{
		if (drop == null)
		{
			return false;
		}
		float now = Time.realtimeSinceStartup;
		if (SuppressedModels.Contains(drop.model))
		{
			return true;
		}
		Item item = drop.interactableItem?.item;
		return item != null && SuppressedItemIds.IsBlocked(item.id, now);
	}

	public static void Reset()
	{
		SuppressedModels.Clear();
		SuppressedItemIds.Clear();
		PendingDrops.Clear();
	}

	public static void Dispose()
	{
		if (_inventory != null)
		{
			_inventory.onDropItemRequested -= OnDropItemRequested;
			_inventory = null;
		}
		if (_installed)
		{
			ItemManager.onItemDropAdded -= OnItemDropAdded;
			ItemManager.onItemDropRemoved -= OnItemDropRemoved;
			_installed = false;
		}
		Reset();
	}

	private static void OnDropItemRequested(PlayerInventory inventory, Item item, ref bool shouldAllow)
	{
		float now = Time.realtimeSinceStartup;
		for (int i = PendingDrops.Count - 1; i >= 0; i--)
		{
			if (PendingDrops[i].ExpiresAt < now)
			{
				PendingDrops.RemoveAt(i);
			}
		}
		if (!shouldAllow || inventory == null || item == null)
		{
			return;
		}
		SuppressedItemIds.Block(item.id, now, _dropBlockDurationSeconds);
		PendingDrops.Add(new PendingDrop
		{
			Id = item.id,
			Amount = item.amount,
			Quality = item.quality,
			StateHash = ComputeStateHash(item.state),
			ExpiresAt = now + 1.5f
		});
	}

	private static void OnItemDropAdded(Transform model, InteractableItem interactableItem)
	{
		float now = Time.realtimeSinceStartup;
		for (int i = PendingDrops.Count - 1; i >= 0; i--)
		{
			PendingDrop pending = PendingDrops[i];
			if (pending.ExpiresAt < now)
			{
				PendingDrops.RemoveAt(i);
				continue;
			}
			Item item = interactableItem?.item;
			if (item == null || pending.Id != item.id || pending.Amount != item.amount || pending.Quality != item.quality || pending.StateHash != ComputeStateHash(item.state))
			{
				continue;
			}
			if (model != null)
			{
				// Unturned passes the rendered item child here, while ItemDrop.model is its root.
				SuppressedModels.Add(model.parent == null ? model : model.parent);
			}
			PendingDrops.RemoveAt(i);
			break;
		}
	}

	private static void OnItemDropRemoved(Transform model, InteractableItem interactableItem)
	{
		if (model != null)
		{
			SuppressedModels.Remove(model.parent == null ? model : model.parent);
		}
	}

	private static int ComputeStateHash(byte[] state)
	{
		if (state == null || state.Length == 0)
		{
			return 0;
		}
		unchecked
		{
			int hash = 17;
			for (int i = 0; i < state.Length; i++)
			{
				hash = hash * 31 + state[i];
			}
			return hash;
		}
	}
}

internal sealed class NativePickupExecutor
{
	private readonly Func<AutoPickupCandidate, bool> _storeCandidate;
	private readonly Func<AutoPickupCandidate, bool> _rollbackStoredCandidate;
	private readonly Func<AutoPickupCandidate, bool> _quarantineWorldDrop;
	private readonly Func<AutoPickupCandidate, bool> _lockStorage;
	private readonly Func<bool> _preferInfiniteStorage;
	private readonly Func<bool> _requiresReconciliation;

	internal NativePickupExecutor(Func<AutoPickupCandidate, bool> storeCandidate = null,
		Func<AutoPickupCandidate, bool> rollbackStoredCandidate = null,
		Func<AutoPickupCandidate, bool> quarantineWorldDrop = null,
		Func<AutoPickupCandidate, bool> lockStorage = null,
		Func<bool> preferInfiniteStorage = null,
		Func<bool> requiresReconciliation = null)
	{
		_storeCandidate = storeCandidate;
		_rollbackStoredCandidate = rollbackStoredCandidate;
		_quarantineWorldDrop = quarantineWorldDrop;
		_lockStorage = lockStorage;
		_preferInfiniteStorage = preferInfiniteStorage;
		_requiresReconciliation = requiresReconciliation;
	}

	public NativePickupAttempt Execute(AutoPickupCandidate candidate, float requestRange)
	{
		if (!SingleplayerGuard.IsReady)
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, default, default,
				SingleplayerGuard.RejectionReason);
		}
		Player player = Player.LocalPlayer;
		if (candidate == null || player == null || !AutoPickupCandidateAdapter.IsCurrent(candidate))
		{
			return new NativePickupAttempt(AutoPickupResultKind.StaleDrop, default, default, "掉落快照已经失效。");
		}
		requestRange = Mathf.Clamp(requestRange, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
		float currentDistanceSquared = (candidate.Drop.model.position - player.transform.position).sqrMagnitude;
		if (currentDistanceSquared > requestRange * requestRange)
		{
			return new NativePickupAttempt(AutoPickupResultKind.RejectedByDistance, default, default,
				$"实际距离 {Mathf.Sqrt(currentDistanceSquared):0.0}m 超过拾取距离 {requestRange:0.0}m。");
		}
		AutoPickupInventoryEvidence currentInventory = CaptureInventoryEvidence(player.inventory, candidate);
		bool prefersInfiniteStorage = _preferInfiniteStorage?.Invoke() == true;
		bool nativePickupAvailable = HasInventorySpace(player.inventory, candidate.Asset);
		NativePickupAttempt storageAttempt = default;
		NativePickupAttempt nativeAttempt = default;
		bool storageAttempted = false;
		bool nativeAttempted = false;
		AutoPickupExecutionPath path = AutoPickupExecutionCoordinator.Execute(
			allowed: true,
			isReconciliationRequired: _requiresReconciliation,
			preferInfiniteStorage: prefersInfiniteStorage,
			nativePickupAvailable,
			tryInfiniteStorage: () =>
			{
				storageAttempted = true;
				AutoPickupStorageCallbackResult callbackResult =
					AutoPickupFinalAttemptPolicy.CaptureStorageCallback(() =>
				{
					bool accepted = TryStoreCandidate(candidate, currentInventory,
						out NativePickupAttempt attempt);
					return new AutoPickupStorageCallbackResult(accepted, attempt);
				}, currentInventory);
				storageAttempt = callbackResult.Attempt;
				return callbackResult.Accepted;
			},
			tryNativePickup: () =>
			{
				nativeAttempted = true;
				nativeAttempt = AutoPickupFinalAttemptPolicy.CaptureNativeCallback(
					() => ExecuteNativePickup(candidate, player), currentInventory);
				AutoPickupNativePostAction action = nativeAttempt.SuppressRetry
					? AutoPickupNativePostAction.SuppressUncertainNative
					: AutoPickupExecutionCoordinator.ResolveNativePostAction(
						nativeAttempt.Result, AutoPickupCandidateAdapter.IsCurrent(candidate),
						nativeAttempt.InventoryBefore, nativeAttempt.InventoryAfter,
						nativeAttempt.RequestSent);
				nativeAttempt = AutoPickupFinalAttemptPolicy.NormalizeNativeAttempt(nativeAttempt, action);
				if (action == AutoPickupNativePostAction.SuppressUncertainNative)
				{
					nativeAttempt = new NativePickupAttempt(nativeAttempt.Result,
						nativeAttempt.InventoryBefore, nativeAttempt.InventoryAfter,
						nativeAttempt.Detail + " 原生拾取请求的最终后态不确定；已暂停该掉落实例的自动重试，且未写入无限储存。",
						nativeAttempt.RequestSent, suppressRetry: true);
				}
				return AutoPickupExecutionCoordinator.StopsNativeFallback(action);
			});
		if (path == AutoPickupExecutionPath.InfiniteStorage)
			return AutoPickupFinalAttemptPolicy.EnsureRecordable(storageAttempt);
		if (path == AutoPickupExecutionPath.NativePickup)
			return AutoPickupFinalAttemptPolicy.EnsureRecordable(nativeAttempt);
		if (path == AutoPickupExecutionPath.ReconciliationBlocked)
			return new NativePickupAttempt(AutoPickupResultKind.ReconciliationRequired, currentInventory,
				currentInventory, "需要持久化快照对账；所有自动拾取已暂停。",
				suppressRetry: true);
		return AutoPickupFinalAttemptPolicy.BuildFailedAttempt(currentInventory,
			nativeAttempted, nativeAttempt, storageAttempted, storageAttempt,
			nativePickupAvailable);
	}

	private static NativePickupAttempt ExecuteNativePickup(AutoPickupCandidate candidate, Player player)
	{
		AutoPickupInventoryEvidence before = CaptureInventoryEvidence(player.inventory, candidate);
		try
		{
			ItemManager.takeItem(candidate.Drop.model, byte.MaxValue, byte.MaxValue, 0, byte.MaxValue);
		}
		catch (Exception exception)
		{
			AutoPickupInventoryEvidence failedAfter = CaptureInventoryEvidence(player.inventory, candidate);
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, before, failedAfter,
				exception.Message, requestSent: true);
		}
		AutoPickupInventoryEvidence after = CaptureInventoryEvidence(player.inventory, candidate);
		if (!AutoPickupCandidateAdapter.IsCurrent(candidate) && after.IndicatesDeliveryComparedTo(before))
		{
			return new NativePickupAttempt(AutoPickupResultKind.PickedUp, before, after,
				"掉落已移除且背包实例或数量增加。", requestSent: true);
		}
		if (!AutoPickupCandidateAdapter.IsCurrent(candidate))
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, before, after, "掉落已移除，但背包数量未增加。", requestSent: true);
		}
		return new NativePickupAttempt(AutoPickupResultKind.Requested, before, after, "原生拾取请求已发送，等待下一轮确认。", requestSent: true);
	}

	private bool TryStoreCandidate(AutoPickupCandidate candidate,
		AutoPickupInventoryEvidence inventoryEvidence, out NativePickupAttempt attempt)
	{
		attempt = default;
		AutoPickupStorageCommitResult result = AutoPickupStorageCommitCoordinator.Execute(
			storeSnapshot: () => _storeCandidate != null && _storeCandidate(candidate),
			removeWorldDrop: delegate
			{
				if (!AutoPickupCandidateAdapter.IsCurrent(candidate)) return true;
				ItemManager.ReceiveDestroyItem(candidate.RegionX, candidate.RegionY, candidate.InstanceId, true);
				return !AutoPickupCandidateAdapter.IsCurrent(candidate);
			},
			rollbackSnapshot: () => _rollbackStoredCandidate != null && _rollbackStoredCandidate(candidate),
			quarantineWorldDrop: () => _quarantineWorldDrop != null && _quarantineWorldDrop(candidate),
			lockStorage: () => _lockStorage?.Invoke(candidate) == true);
		if (!result.StoredSnapshot) return false;
		if (result.Success)
		{
			attempt = new NativePickupAttempt(AutoPickupResultKind.PickedUp, inventoryEvidence,
				inventoryEvidence, "掉落已存入无限储存。");
			return true;
		}
		if (result.RequiresReconciliation)
		{
			string detail = result.WorldDropQuarantined
				? "储存副本为权威；未解决的世界掉落已隔离，且无限储存已锁定。"
				: "储存副本为权威且无限储存已锁定；世界掉落隔离也失败。";
			if (!result.MarkerPersisted)
				detail += " 持久化对账标记创建失败；当前进程仍保持锁定。";
			attempt = new NativePickupAttempt(AutoPickupResultKind.ReconciliationRequired,
				inventoryEvidence, inventoryEvidence, detail, suppressRetry: true);
			return true;
		}
		attempt = new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, inventoryEvidence,
			inventoryEvidence, "无限储存补偿成功；世界掉落仍保留，稍后可重试。");
		return true;
	}

	internal bool TryRecoverPendingCandidate(AutoPickupCandidate candidate,
		AutoPickupInventoryEvidence inventoryEvidence, out NativePickupAttempt attempt)
	{
		bool handled = TryStoreCandidate(candidate, inventoryEvidence, out attempt);
		if (!handled)
		{
			attempt = new NativePickupAttempt(AutoPickupResultKind.UnknownFailure,
				inventoryEvidence, inventoryEvidence,
				"掉落已消失，但背包没有交付证据且无限储存未接受恢复快照。");
			return false;
		}
		return attempt.Result == AutoPickupResultKind.PickedUp;
	}

	internal static bool HasInventorySpace(PlayerInventory inventory, ItemAsset asset)
	{
		if (inventory == null || asset == null || asset.size_x == 0 || asset.size_y == 0)
		{
			return false;
		}
		return inventory.tryFindSpace(asset.size_x, asset.size_y, out byte page, out byte x, out byte y, out byte rot);
	}

	internal static AutoPickupInventoryEvidence CaptureInventoryEvidence(PlayerInventory inventory,
		AutoPickupCandidate candidate)
	{
		if (inventory == null || candidate?.Asset == null) return default;
		int matchingAssetJarCount = 0;
		int exactSnapshotJarCount = 0;
		long totalAmount = 0;
		for (byte page = 0; page < PlayerInventory.STORAGE; page++)
		{
			byte itemCount = inventory.getItemCount(page);
			for (byte index = 0; index < itemCount; index++)
			{
				Item item = inventory.getItem(page, index)?.item;
				if (item == null || item.id != candidate.Asset.id) continue;
				matchingAssetJarCount++;
				totalAmount += item.amount;
				byte[] itemState = item.state ?? Array.Empty<byte>();
				byte[] candidateState = candidate.State ?? Array.Empty<byte>();
				if (item.amount == candidate.Amount && item.quality == candidate.Quality &&
					itemState.SequenceEqual(candidateState)) exactSnapshotJarCount++;
			}
		}
		return new AutoPickupInventoryEvidence(matchingAssetJarCount, totalAmount,
			exactSnapshotJarCount);
	}
}

internal sealed class AutoPickupFeature
{
	private sealed class PendingPickup
	{
		public AutoPickupCandidate Candidate;

		public AutoPickupInventoryEvidence InventoryBefore;

		public float RequestedAt;
	}

	private readonly ManualLogSource _log;

	private readonly NearbyItemIndex _itemIndex;

	private readonly AutoPickupCandidateAdapter _candidateAdapter = new AutoPickupCandidateAdapter();

	private readonly NativePickupExecutor _executor;

	private readonly AutoPickupScheduler _scheduler = new AutoPickupScheduler();

	private readonly ItemFilterMatcher _matcher = new ItemFilterMatcher();

	private readonly AutoPickupRuleStore _store;

	private readonly Dictionary<uint, PendingPickup> _pending = new Dictionary<uint, PendingPickup>();

	private AutoPickupSettings _settings;

	private int _snapshotVersion;

	private string _worldKey = string.Empty;

	private int _candidateCount;

	private long _scanCount;

	private long _requestedCount;

	private long _pickedUpCount;

	private long _filteredCount;

	private long _failureCount;

	private long _retryCount;

	private float _lastScanTime;

	private string _lastFailure = string.Empty;

	private string _state = "已关闭";

	public AutoPickupSettings Settings => _settings.Clone();

	public AutoPickupRuleDocument Rules => new AutoPickupRuleDocument
	{
		Version = _store.Document.Version,
		Enabled = _store.Document.Enabled,
		Mode = _store.Document.Mode,
		Rules = _store.Document.Rules.Select(rule => rule.Clone()).ToList()
	};

	public string RulesPath => _store.FilePath;

	public AutoPickupDiagnosticsSnapshot Diagnostics => new AutoPickupDiagnosticsSnapshot(_candidateCount, _scanCount, _requestedCount, _pickedUpCount, _filteredCount, _failureCount, _retryCount, _lastScanTime, _lastFailure, _state);

	public AutoPickupFeature(ManualLogSource log, AutoPickupSettings settings, string rulesPath, NearbyItemIndex itemIndex = null)
	{
		_log = log;
		_itemIndex = itemIndex ?? new NearbyItemIndex();
		_settings = (settings ?? new AutoPickupSettings()).Normalize().Clone();
		_executor = new NativePickupExecutor(StoreCandidate, RollbackStoredCandidate,
			QuarantineWorldDrop, LockInfiniteStorage, PreferInfiniteStorage,
			RequiresInfiniteStorageReconciliation);
		string path = Path.IsPathRooted(rulesPath ?? string.Empty) ? rulesPath : Path.Combine(Paths.ConfigPath, string.IsNullOrWhiteSpace(rulesPath) ? "AutoPickup.rules.json" : rulesPath);
		_store = new AutoPickupRuleStore(path, message => _log?.LogInfo(message), message => _log?.LogError(message));
		AutoPickupDropGate.Install();
		AutoPickupDropGate.SetDropBlockDurationMinutes(_settings.DropBlockMinutes);
		_store.Load(_matcher);
	}

	private bool StoreCandidate(AutoPickupCandidate candidate)
	{
		CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
		return plugin?.InfiniteStorage != null && candidate?.Asset != null &&
			plugin.InfiniteStorage.StoreCandidate(new Item(candidate.Asset.id, candidate.Amount, candidate.Quality, candidate.State == null ? Array.Empty<byte>() : (byte[])candidate.State.Clone()), candidate.Asset);
	}

	private static bool RollbackStoredCandidate(AutoPickupCandidate candidate)
	{
		if (candidate?.Asset == null) return false;
		return CheatMenuPlugin.Instance?.InfiniteStorage?.RollbackCandidate(
			new Item(candidate.Asset.id, candidate.Amount, candidate.Quality,
				candidate.State == null ? Array.Empty<byte>() : (byte[])candidate.State.Clone()),
			candidate.Asset) == true;
	}

	private static bool QuarantineWorldDrop(AutoPickupCandidate candidate)
	{
		Transform model = candidate?.Drop?.model;
		if (model == null) return false;
		Transform root = model.parent == null ? model : model.parent;
		root.gameObject.SetActive(false);
		return !root.gameObject.activeSelf;
	}

	private static bool LockInfiniteStorage(AutoPickupCandidate candidate)
	{
		return CheatMenuPlugin.Instance?.InfiniteStorage?.LatchReconciliationFailure(
			"autopickup-instance=" + (candidate?.InstanceId ?? 0) +
			";asset=" + (candidate?.Asset?.id ?? 0) +
			";amount=" + (candidate?.Amount ?? 0) +
			";quality=" + (candidate?.Quality ?? 0)) == true;
	}

	private static bool PreferInfiniteStorage()
	{
		CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
		return plugin?.IsInfiniteStoragePickupPriorityEnabled == true && plugin.InfiniteStorage?.IsOperational == true;
	}

	private static bool RequiresInfiniteStorageReconciliation()
	{
		return CheatMenuPlugin.Instance?.InfiniteStorage?.HasReconciliationLatchForCurrentIdentity() == true;
	}

	public void UpdateSettings(AutoPickupSettings settings)
	{
		_settings = (settings ?? new AutoPickupSettings()).Normalize().Clone();
		AutoPickupDropGate.SetDropBlockDurationMinutes(_settings.DropBlockMinutes);
		if (!_settings.Enabled)
		{
			ResetRuntime("已关闭");
		}
	}

	public AutoPickupRuleLoadResult ReloadRules()
	{
		AutoPickupRuleLoadResult result = _store.Load(_matcher);
		_snapshotVersion++;
		_pending.Clear();
		return result;
	}

	public AutoPickupRuleLoadResult SaveRules(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules)
	{
		AutoPickupRuleLoadResult result = _store.ReplaceAndSave(enabled, mode, rules, _matcher);
		_snapshotVersion++;
		_pending.Clear();
		return result;
	}

	public void ClearDiagnostics()
	{
		_candidateCount = 0;
		_scanCount = 0;
		_requestedCount = 0;
		_pickedUpCount = 0;
		_filteredCount = 0;
		_failureCount = 0;
		_retryCount = 0;
		_lastScanTime = 0f;
		_lastFailure = string.Empty;
	}

	internal bool TryGetItemScanDemand(bool menuOpen, out float radius, out float frequencyHz)
	{
		radius = ResolveEffectiveRange();
		frequencyHz = _settings.FrequencyHz;
		if (!_settings.Enabled || !SingleplayerGuard.IsReady)
		{
			return false;
		}
		Player player = Player.LocalPlayer;
		if (player == null || (_settings.PauseWhenDead && (player.life == null || player.life.isDead)) ||
			(_settings.PauseWhenMenuOpen && menuOpen))
		{
			return false;
		}
		return true;
	}

	public void Pump(bool menuOpen, NearbyItemSnapshot sharedSnapshot = null)
	{
		AutoPickupDropGate.Attach(Player.LocalPlayer?.inventory);
		float now = Time.realtimeSinceStartup;
		string worldKey = BuildWorldKey();
		if (!string.Equals(worldKey, _worldKey, StringComparison.Ordinal))
		{
			_worldKey = worldKey;
			AutoPickupDropGate.Reset();
			ResetRuntime("世界已切换");
		}
		if (!_settings.Enabled)
		{
			_state = "已关闭";
			return;
		}
		if (!SingleplayerGuard.IsReady)
		{
			ResetRuntime("单人门禁未就绪");
			return;
		}
		if (RequiresInfiniteStorageReconciliation())
		{
			_pending.Clear();
			_state = "无限储存需人工对账，自动拾取已全局暂停";
			return;
		}
		Player player = Player.LocalPlayer;
		if (_settings.PauseWhenDead && (player?.life == null || player.life.isDead))
		{
			ResetRuntime("玩家死亡，已暂停");
			return;
		}
		if (_settings.PauseWhenMenuOpen && menuOpen)
		{
			_state = "菜单打开，已暂停";
			return;
		}
		if (!_scheduler.ShouldScan(now, _settings.FrequencyHz))
		{
			return;
		}
		_state = "运行中";
		_snapshotVersion++;
		float effectiveRange = ResolveEffectiveRange();
		NearbyItemSnapshot rawSnapshot = sharedSnapshot != null && sharedSnapshot.Radius + 0.001f >= effectiveRange
			? sharedSnapshot
			: _itemIndex.Refresh(player.transform.position, effectiveRange, _snapshotVersion);
		IReadOnlyList<AutoPickupCandidate> candidates = _candidateAdapter.Adapt(rawSnapshot, effectiveRange, _snapshotVersion, player.transform.position);
		_candidateCount = candidates.Count;
		_scanCount++;
		_lastScanTime = now;
		ConfirmPending(now);
		int budget = _scheduler.TakeRequestBudget(now, _settings.RequestsPerTick);
		for (int i = 0; i < candidates.Count && budget > 0; i++)
		{
			AutoPickupCandidate candidate = candidates[i];
			if (_pending.ContainsKey(candidate.InstanceId))
			{
				continue;
			}
			if (!_settings.AllowsRarity(candidate.Rarity))
			{
				_filteredCount++;
				continue;
			}
			if (!_settings.AllowsQuality(candidate.Quality))
			{
				_filteredCount++;
				continue;
			}
			if (_settings.UseMinimapRarityFilter && !AllowsMinimapRarity(candidate.Asset))
			{
				_filteredCount++;
				continue;
			}
			if (!_matcher.Matches(candidate.Descriptor))
			{
				_filteredCount++;
				continue;
			}
			if (!_scheduler.CanRequest(candidate.InstanceId, now))
			{
				_retryCount++;
				continue;
			}
			NativePickupAttempt attempt = _executor.Execute(candidate, effectiveRange);
			budget--;
			RecordAttempt(candidate, attempt, now);
		}
		if (_settings.Diagnostics >= AutoPickupDiagnosticsLevel.Summary && (_scanCount == 1 || _scanCount % Math.Max(1, (int)_settings.FrequencyHz * 10) == 0))
		{
			_log?.LogInfo($"[AutoPickup] 扫描摘要：候选={_candidateCount} 请求={_requestedCount} 成功={_pickedUpCount} 过滤={_filteredCount} 失败={_failureCount} 重试={_retryCount}。");
		}
	}

	private float ResolveEffectiveRange()
	{
		if (!_settings.UseMinimapRange)
		{
			return _settings.Range;
		}
		MinimapSettings minimap = CheatMenuPlugin.Instance?.Minimap?.Settings;
		return minimap == null ? _settings.Range : Mathf.Clamp(minimap.VisibleRadiusMeters, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
	}

	private static bool AllowsMinimapRarity(ItemAsset asset)
	{
		MinimapSettings minimap = CheatMenuPlugin.Instance?.Minimap?.Settings;
		if (asset == null || minimap == null)
		{
			return true;
		}
		int rarity = (int)asset.rarity;
		return rarity >= 0 && rarity <= (int)MinimapItemRarity.Mythical &&
			(minimap.ItemRarityMask & (ItemRarityMask)(1 << rarity)) != 0;
	}

	private void RecordAttempt(AutoPickupCandidate candidate, NativePickupAttempt attempt, float now)
	{
		attempt = AutoPickupFinalAttemptPolicy.EnsureRecordable(attempt);
		AutoPickupAttemptRecordCoordinator.Execute(attempt, candidate.InstanceId, now, _scheduler,
			requestSent: () => _requestedCount++,
			pickedUp: () => _pickedUpCount++,
			pending: () => _pending[candidate.InstanceId] = new PendingPickup
			{
				Candidate = candidate,
				InventoryBefore = attempt.InventoryBefore,
				RequestedAt = now
			},
			capacityBlocked: () => _state = "背包空间不足，已暂停请求",
			reconciliationRequired: () =>
			{
				_failureCount++;
				_lastFailure = attempt.Result + "：" + attempt.Detail;
				_state = "无限储存需人工对账，已隔离掉落并锁定重试";
				_log?.LogError($"[AutoPickupReconciliation] instance={candidate.InstanceId} id={candidate.Asset.id} detail={attempt.Detail}");
			},
			suppressed: () =>
			{
				_failureCount++;
				_lastFailure = attempt.Result + "：" + attempt.Detail;
				_state = "原生拾取后态不确定，已暂停该掉落实例的自动重试";
				_log?.LogError($"[AutoPickupNativeUncertain] instance={candidate.InstanceId} id={candidate.Asset.id} detail={attempt.Detail}");
			},
			failure: () => RecordFailure(candidate, attempt.Result, attempt.Detail, now));
		if (_settings.Diagnostics == AutoPickupDiagnosticsLevel.Verbose)
		{
			_log?.LogInfo($"[AutoPickupItem] result={attempt.Result} instance={candidate.InstanceId} id={candidate.Asset.id} name={candidate.Asset.FriendlyName} distance={Mathf.Sqrt(candidate.DistanceSquared):0.0}m range={_settings.Range:0.0}m detail={attempt.Detail}");
		}
	}

	private void ConfirmPending(float now)
	{
		if (_pending.Count == 0)
		{
			return;
		}
		List<uint> completed = new List<uint>();
		foreach (KeyValuePair<uint, PendingPickup> pair in _pending)
		{
			PendingPickup pending = pair.Value;
			bool worldDropIsCurrent = AutoPickupCandidateAdapter.IsCurrent(pending.Candidate);
			bool timeoutElapsed = now - pending.RequestedAt >= 0.25f;
			AutoPickupInventoryEvidence after = worldDropIsCurrent && !timeoutElapsed
				? pending.InventoryBefore
				: NativePickupExecutor.CaptureInventoryEvidence(Player.LocalPlayer.inventory,
					pending.Candidate);
			NativePickupAttempt storageAttempt = default;
			AutoPickupPendingConfirmationOutcome outcome =
				AutoPickupPendingConfirmationCoordinator.Execute(
					worldDropIsCurrent, timeoutElapsed, pending.InventoryBefore, after,
					tryInfiniteStorage: () => _executor.TryRecoverPendingCandidate(
						pending.Candidate, after, out storageAttempt),
					markNativeDelivered: () =>
					{
						_pickedUpCount++;
						_scheduler.MarkSuccess(pair.Key);
					},
					suppressRetry: () => _scheduler.SuppressUntilReset(pair.Key),
					completePending: () => completed.Add(pair.Key));
			if (outcome == AutoPickupPendingConfirmationOutcome.RecoveredWithInfiniteStorage ||
				outcome == AutoPickupPendingConfirmationOutcome.StorageRecoveryFailed)
			{
				RecordAttempt(pending.Candidate, storageAttempt, now);
			}
			else if (outcome == AutoPickupPendingConfirmationOutcome.SuppressedUncertainNative)
			{
				RecordSuppressedNativeOutcome(pending.Candidate,
					"原生拾取请求超时且掉落仍存在；最终后态不确定，已暂停该实例的自动重试，且未写入无限储存。");
			}
		}
		for (int i = 0; i < completed.Count; i++)
		{
			_pending.Remove(completed[i]);
		}
	}

	private void RecordSuppressedNativeOutcome(AutoPickupCandidate candidate, string detail)
	{
		_failureCount++;
		_lastFailure = AutoPickupResultKind.UnknownFailure + "：" + detail;
		_state = "原生拾取后态不确定，已暂停该掉落实例的自动重试";
		_log?.LogError($"[AutoPickupNativeUncertain] instance={candidate.InstanceId} id={candidate.Asset.id} detail={detail}");
	}

	private void RecordFailure(AutoPickupCandidate candidate, AutoPickupResultKind result, string detail, float now)
	{
		_failureCount++;
		_lastFailure = result + "：" + detail;
		if (_settings.Diagnostics >= AutoPickupDiagnosticsLevel.Failures)
		{
			_log?.LogWarning($"[AutoPickupFailure] result={result} instance={candidate.InstanceId} id={candidate.Asset.id} distance={Mathf.Sqrt(candidate.DistanceSquared):0.0}m range={_settings.Range:0.0}m detail={detail}");
		}
	}

	private void ResetRuntime(string state)
	{
		_pending.Clear();
		_scheduler.Reset(Time.realtimeSinceStartup);
		_candidateCount = 0;
		_state = state;
	}

	public void Dispose()
	{
		AutoPickupDropGate.Dispose();
	}

	private static string BuildWorldKey()
	{
		return (Provider.serverID ?? string.Empty) + "|" + (Level.info?.name ?? string.Empty) + "|" + (Player.LocalPlayer == null ? "no-player" : Player.LocalPlayer.GetInstanceID().ToString());
	}
}
