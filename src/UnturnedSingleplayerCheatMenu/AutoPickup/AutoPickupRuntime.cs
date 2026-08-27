using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
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

	public byte[] State { get; set; }

	public int SnapshotVersion { get; set; }

	public ItemDrop Drop { get; set; }

	public ItemAsset Asset { get; set; }

	public AutoPickupItemDescriptor Descriptor { get; set; }
}

internal sealed class DropScanner
{
	private readonly List<RegionCoordinate> _regions = new List<RegionCoordinate>();

	private readonly HashSet<uint> _instances = new HashSet<uint>();

	public IReadOnlyList<AutoPickupCandidate> Scan(Vector3 center, float radius, int snapshotVersion)
	{
		List<AutoPickupCandidate> result = new List<AutoPickupCandidate>();
		_regions.Clear();
		_instances.Clear();
		if (ItemManager.regions == null)
		{
			return result;
		}
		radius = Mathf.Clamp(radius, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
		float radiusSquared = radius * radius;
		Regions.getRegionsInRadius(center, radius, _regions);
		for (int regionIndex = 0; regionIndex < _regions.Count; regionIndex++)
		{
			RegionCoordinate coordinate = _regions[regionIndex];
			ItemRegion region = ItemManager.regions[coordinate.x, coordinate.y];
			if (region == null)
			{
				continue;
			}
			List<ItemDrop> drops = region.drops;
			for (int dropIndex = 0; dropIndex < drops.Count; dropIndex++)
			{
				ItemDrop drop = drops[dropIndex];
				InteractableItem interactable = drop?.interactableItem;
				ItemAsset asset = interactable?.asset;
				Item item = interactable?.item;
				Transform model = drop?.model;
				if (drop == null || model == null || interactable == null || asset == null || item == null || AutoPickupDropGate.IsSuppressed(drop) || !_instances.Add(drop.instanceID))
				{
					continue;
				}
				float distanceSquared = (model.position - center).sqrMagnitude;
				if (distanceSquared > radiusSquared)
				{
					continue;
				}
				result.Add(new AutoPickupCandidate
				{
					InstanceId = drop.instanceID,
					RegionX = coordinate.x,
					RegionY = coordinate.y,
					Position = model.position,
					DistanceSquared = distanceSquared,
					Amount = item.amount,
					Quality = item.quality,
					State = item.state == null ? Array.Empty<byte>() : (byte[])item.state.Clone(),
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
		}
		result.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
		return result;
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

internal readonly struct NativePickupAttempt
{
	public AutoPickupResultKind Result { get; }

	public int InventoryAmountBefore { get; }

	public int InventoryAmountAfter { get; }

	public string Detail { get; }

	public bool RequestSent { get; }

	public NativePickupAttempt(AutoPickupResultKind result, int inventoryAmountBefore, int inventoryAmountAfter, string detail, bool requestSent = false)
	{
		Result = result;
		InventoryAmountBefore = inventoryAmountBefore;
		InventoryAmountAfter = inventoryAmountAfter;
		Detail = detail ?? string.Empty;
		RequestSent = requestSent;
	}
}

internal sealed class NativePickupExecutor
{
	private readonly Func<AutoPickupCandidate, bool> _storeCandidate;
	private readonly Action<AutoPickupCandidate> _rollbackStoredCandidate;
	private readonly Func<bool> _preferInfiniteStorage;

	internal NativePickupExecutor(Func<AutoPickupCandidate, bool> storeCandidate = null, Action<AutoPickupCandidate> rollbackStoredCandidate = null, Func<bool> preferInfiniteStorage = null)
	{
		_storeCandidate = storeCandidate;
		_rollbackStoredCandidate = rollbackStoredCandidate;
		_preferInfiniteStorage = preferInfiniteStorage;
	}

	public NativePickupAttempt Execute(AutoPickupCandidate candidate, float requestRange)
	{
		if (!SingleplayerGuard.IsReady)
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, 0, 0, SingleplayerGuard.RejectionReason);
		}
		Player player = Player.LocalPlayer;
		if (candidate == null || player == null || !DropScanner.IsCurrent(candidate))
		{
			return new NativePickupAttempt(AutoPickupResultKind.StaleDrop, 0, 0, "掉落快照已经失效。");
		}
		requestRange = Mathf.Clamp(requestRange, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
		float currentDistanceSquared = (candidate.Drop.model.position - player.transform.position).sqrMagnitude;
		if (currentDistanceSquared > requestRange * requestRange)
		{
			return new NativePickupAttempt(AutoPickupResultKind.RejectedByDistance, 0, 0, $"实际距离 {Mathf.Sqrt(currentDistanceSquared):0.0}m 超过拾取距离 {requestRange:0.0}m。");
		}
		int currentAmount = CountInventoryAmount(player.inventory, candidate.Asset.id);
		bool prefersInfiniteStorage = _preferInfiniteStorage?.Invoke() == true;
		if (prefersInfiniteStorage && TryStoreCandidate(candidate, currentAmount, out NativePickupAttempt storageAttempt))
		{
			return storageAttempt;
		}
		if (!HasInventorySpace(player.inventory, candidate.Asset))
		{
			if (!prefersInfiniteStorage && TryStoreCandidate(candidate, currentAmount, out storageAttempt))
			{
				return storageAttempt;
			}
			return new NativePickupAttempt(AutoPickupResultKind.RejectedByCapacity, currentAmount, currentAmount, "背包没有可放置该物品的剩余空间；跳过原生请求。");
		}
		int before = CountInventoryAmount(player.inventory, candidate.Asset.id);
		try
		{
			ItemManager.takeItem(candidate.Drop.model, byte.MaxValue, byte.MaxValue, 0, byte.MaxValue);
		}
		catch (Exception exception)
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, before, before, exception.Message, requestSent: true);
		}
		int after = CountInventoryAmount(player.inventory, candidate.Asset.id);
		if (!DropScanner.IsCurrent(candidate) && after > before)
		{
			return new NativePickupAttempt(AutoPickupResultKind.PickedUp, before, after, "掉落已移除且背包数量增加。", requestSent: true);
		}
		if (!DropScanner.IsCurrent(candidate))
		{
			return new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, before, after, "掉落已移除，但背包数量未增加。", requestSent: true);
		}
		return new NativePickupAttempt(AutoPickupResultKind.Requested, before, after, "原生拾取请求已发送，等待下一轮确认。", requestSent: true);
	}

	private bool TryStoreCandidate(AutoPickupCandidate candidate, int inventoryAmount, out NativePickupAttempt attempt)
	{
		attempt = default;
		if (_storeCandidate == null || !_storeCandidate(candidate))
		{
			return false;
		}
		bool removed = false;
		try
		{
			ItemManager.ReceiveDestroyItem(candidate.RegionX, candidate.RegionY, candidate.InstanceId, true);
			removed = !DropScanner.IsCurrent(candidate);
		}
		catch
		{
			removed = false;
		}
		if (!removed)
		{
			_rollbackStoredCandidate?.Invoke(candidate);
		}
		attempt = removed
			? new NativePickupAttempt(AutoPickupResultKind.PickedUp, inventoryAmount, inventoryAmount, "Drop stored in infinite storage.")
			: new NativePickupAttempt(AutoPickupResultKind.UnknownFailure, inventoryAmount, inventoryAmount, "Storage write succeeded but the drop remains.");
		return true;
	}

	internal static bool HasInventorySpace(PlayerInventory inventory, ItemAsset asset)
	{
		if (inventory == null || asset == null || asset.size_x == 0 || asset.size_y == 0)
		{
			return false;
		}
		return inventory.tryFindSpace(asset.size_x, asset.size_y, out byte page, out byte x, out byte y, out byte rot);
	}

	internal static int CountInventoryAmount(PlayerInventory inventory, ushort assetId)
	{
		if (inventory == null)
		{
			return 0;
		}
		int total = 0;
#pragma warning disable CS0618
		List<InventorySearch> matches = inventory.search(assetId, findEmpty: false, findHealthy: true);
#pragma warning restore CS0618
		for (int i = 0; i < matches.Count; i++)
		{
			ItemJar jar = matches[i]?.jar;
			if (jar?.item != null)
			{
				total += jar.item.amount;
			}
		}
		return total;
	}
}

internal sealed class AutoPickupFeature
{
	private sealed class PendingPickup
	{
		public AutoPickupCandidate Candidate;

		public int InventoryAmountBefore;

		public float RequestedAt;
	}

	private readonly ManualLogSource _log;

	private readonly DropScanner _scanner = new DropScanner();

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

	public AutoPickupFeature(ManualLogSource log, AutoPickupSettings settings, string rulesPath)
	{
		_log = log;
		_settings = (settings ?? new AutoPickupSettings()).Normalize().Clone();
		_executor = new NativePickupExecutor(StoreCandidate, RollbackStoredCandidate, PreferInfiniteStorage);
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

	private static void RollbackStoredCandidate(AutoPickupCandidate candidate)
	{
		if (candidate?.Asset == null) return;
		CheatMenuPlugin.Instance?.InfiniteStorage?.RollbackCandidate(new Item(candidate.Asset.id, candidate.Amount, candidate.Quality, candidate.State == null ? Array.Empty<byte>() : (byte[])candidate.State.Clone()), candidate.Asset);
	}

	private static bool PreferInfiniteStorage()
	{
		CheatMenuPlugin plugin = CheatMenuPlugin.Instance;
		return plugin?.IsInfiniteStoragePickupPriorityEnabled == true && plugin.InfiniteStorage?.IsOperational == true;
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

	public void Pump(bool menuOpen)
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
		IReadOnlyList<AutoPickupCandidate> candidates = _scanner.Scan(player.transform.position, _settings.Range, _snapshotVersion);
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
			if (!_settings.AllowsQuality(candidate.Quality))
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
			NativePickupAttempt attempt = _executor.Execute(candidate, _settings.Range);
			budget--;
			RecordAttempt(candidate, attempt, now);
		}
		if (_settings.Diagnostics >= AutoPickupDiagnosticsLevel.Summary && (_scanCount == 1 || _scanCount % Math.Max(1, (int)_settings.FrequencyHz * 10) == 0))
		{
			_log?.LogInfo($"[AutoPickup] 扫描摘要：候选={_candidateCount} 请求={_requestedCount} 成功={_pickedUpCount} 过滤={_filteredCount} 失败={_failureCount} 重试={_retryCount}。");
		}
	}

	private void RecordAttempt(AutoPickupCandidate candidate, NativePickupAttempt attempt, float now)
	{
		if (attempt.RequestSent)
		{
			_requestedCount++;
		}
		if (attempt.Result == AutoPickupResultKind.PickedUp)
		{
			_pickedUpCount++;
			_scheduler.MarkSuccess(candidate.InstanceId);
		}
		else if (attempt.Result == AutoPickupResultKind.Requested)
		{
			_pending[candidate.InstanceId] = new PendingPickup
			{
				Candidate = candidate,
				InventoryAmountBefore = attempt.InventoryAmountBefore,
				RequestedAt = now
			};
		}
		else if (attempt.Result == AutoPickupResultKind.RejectedByCapacity && !attempt.RequestSent)
		{
			_state = "背包空间不足，已暂停请求";
			_scheduler.MarkCapacityBlocked(candidate.InstanceId, now);
		}
		else if (attempt.Result != AutoPickupResultKind.StaleDrop)
		{
			RecordFailure(candidate, attempt.Result, attempt.Detail, now);
		}
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
			if (!DropScanner.IsCurrent(pending.Candidate))
			{
				int after = NativePickupExecutor.CountInventoryAmount(Player.LocalPlayer.inventory, pending.Candidate.Asset.id);
				if (after > pending.InventoryAmountBefore)
				{
					_pickedUpCount++;
					_scheduler.MarkSuccess(pair.Key);
				}
				else
				{
					RecordFailure(pending.Candidate, AutoPickupResultKind.UnknownFailure, "掉落消失但背包数量未增加。", now);
				}
				completed.Add(pair.Key);
			}
			else if (now - pending.RequestedAt >= 0.25f)
			{
				RecordFailure(pending.Candidate, AutoPickupResultKind.RejectedByCapacity, "请求后掉落仍存在；背包可能没有容量，或原生请求被拒绝。", now);
				completed.Add(pair.Key);
			}
		}
		for (int i = 0; i < completed.Count; i++)
		{
			_pending.Remove(completed[i]);
		}
	}

	private void RecordFailure(AutoPickupCandidate candidate, AutoPickupResultKind result, string detail, float now)
	{
		_failureCount++;
		_lastFailure = result + "：" + detail;
		_scheduler.MarkFailure(candidate.InstanceId, now);
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
