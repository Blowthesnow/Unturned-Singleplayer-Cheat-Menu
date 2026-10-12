using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorldMaintenanceService : IDisposable
{
	private sealed class ResourceCandidate
	{
		internal ResourceCandidate(ResourceManager manager, byte x, byte y, ushort index, ResourceSpawnpoint spawn)
		{
			Manager = manager;
			X = x;
			Y = y;
			Index = index;
			Spawn = spawn;
		}

		internal ResourceManager Manager { get; }
		internal byte X { get; }
		internal byte Y { get; }
		internal ushort Index { get; }
		internal ResourceSpawnpoint Spawn { get; }
	}

	private sealed class ZombieCandidate
	{
		internal ZombieCandidate(Zombie zombie, bool includeSpecial)
		{
			Zombie = zombie;
			IncludeSpecial = includeSpecial;
		}

		internal Zombie Zombie { get; }
		internal bool IncludeSpecial { get; }
	}

	private sealed class ItemRegionCandidate
	{
		internal ItemRegionCandidate(byte x, byte y, ItemRegion region, int initialCount)
		{
			X = x;
			Y = y;
			Region = region;
			InitialCount = Math.Max(0, initialCount);
		}

		internal byte X { get; }
		internal byte Y { get; }
		internal ItemRegion Region { get; }
		internal int InitialCount { get; }
	}

	private sealed class NearbyItemCandidate
	{
		internal NearbyItemCandidate(Vector3 center, float radius, int initialCount)
		{
			Center = center;
			Radius = radius;
			InitialCount = Math.Max(0, initialCount);
		}

		internal Vector3 Center { get; }
		internal float Radius { get; }
		internal int InitialCount { get; }
	}

	private sealed class ItemRestockCandidate
	{
		internal ItemRestockCandidate(byte x, byte y, ItemRegion region, List<ItemSpawnpoint> spawns,
			int desiredCount, int availableCount)
		{
			X = x;
			Y = y;
			Region = region;
			Spawns = spawns;
			InitialCount = region.items.Count;
			DesiredCount = desiredCount;
			AvailableCount = availableCount;
			ExpectedCount = Math.Min(Math.Max(0, desiredCount - InitialCount), availableCount);
		}

		internal byte X { get; }
		internal byte Y { get; }
		internal ItemRegion Region { get; }
		internal List<ItemSpawnpoint> Spawns { get; }
		internal int InitialCount { get; }
		internal int DesiredCount { get; }
		internal int AvailableCount { get; }
		internal int ExpectedCount { get; }
	}

	private sealed class CandidateBatch
	{
		internal readonly List<ResourceCandidate> Resources = new List<ResourceCandidate>();
		internal readonly List<ZombieCandidate> Zombies = new List<ZombieCandidate>();
		internal readonly List<ItemRegionCandidate> ItemRegions = new List<ItemRegionCandidate>();
		internal readonly List<ItemRestockCandidate> ItemRestock = new List<ItemRestockCandidate>();
		internal NearbyItemCandidate NearbyItems;
		internal int CandidateCount;
		internal int ProtectedCount;
		internal int BlockedCount;
		internal bool ScanSucceeded = true;
		internal bool ResourceSupported = true;
		internal string Message = string.Empty;
	}

	private readonly ManualLogSource _log;
	private readonly WorldMaintenanceNativeBridge _native;
	private readonly Func<bool> _canExecuteGameplay;
	private WorldMaintenanceBatchRunner<ResourceCandidate> _resourceRunner;
	private WorldMaintenanceBatchRunner<ZombieCandidate> _zombieRunner;
	private WorldMaintenanceBatchRunner<ItemRegionCandidate> _itemRegionRunner;
	private WorldMaintenanceBatchRunner<NearbyItemCandidate> _nearbyItemRunner;
	private WorldMaintenanceBatchRunner<ItemRestockCandidate> _itemRestockRunner;
	private WorldMaintenanceBatchRunner<ZombieCandidate> _zombieGenerationRunner;
	private WorldMaintenanceActionKind _activeAction;
	private WorldMaintenanceScopeKind _activeScope;
	private int _snapshotVersion = 1;
	private int _sceneVersion = 1;
	private bool _disposed;
	private string _message = "尚未运行维护任务。";
	private WorldMaintenanceResult _lastResult;

	internal WorldMaintenanceService(ManualLogSource log, Func<bool> canExecuteGameplay)
	{
		_log = log;
		_canExecuteGameplay = canExecuteGameplay ?? throw new ArgumentNullException(nameof(canExecuteGameplay));
		_native = new WorldMaintenanceNativeBridge(log);
	}

	internal event System.Action GroundItemsChanged;
	internal WorldMaintenanceRunState State { get; private set; } = WorldMaintenanceRunState.Idle;
	internal int SnapshotVersion => _snapshotVersion;
	internal int SceneVersion => _sceneVersion;

	internal WorldMaintenanceDashboardSnapshot CaptureDashboard(float radius)
	{
		if (_disposed || !_canExecuteGameplay())
		{
			string unavailable = _disposed ? "世界维护服务已释放。" : "世界维护仅在受支持的单人世界中可用。";
			return new WorldMaintenanceDashboardSnapshot(
				resourceRefreshSupported: false, resourceCapabilityMessage: unavailable,
				nearbyDeadResources: 0, nearbyRecoverableResources: 0, nearbyBlockedResources: 0,
				wholeDeadResources: 0, wholeRecoverableResources: 0, wholeBlockedResources: 0,
				nearbyZombies: 0, loadedZombies: 0, protectedZombies: 0,
				nearbyDeadZombieSlots: 0, loadedDeadZombieSlots: 0,
				nearbyGroundItems: 0, allGroundItems: 0,
				snapshotVersion: _snapshotVersion, sceneVersion: _sceneVersion,
				nearbyRestockableItems: 0, allRestockableItems: 0,
				progress: CaptureProgress(), result: _lastResult);
		}
		float normalizedRadius = WorldMaintenancePolicy.NormalizeRadius(radius);
		CandidateBatch nearbyResources = SafeScan(
			() => ScanResources(WorldMaintenanceScopeKind.Nearby, normalizedRadius), "nearby resources");
		CandidateBatch wholeResources = SafeScan(
			() => ScanResources(WorldMaintenanceScopeKind.WholeMap, normalizedRadius), "whole-map resources");
		CandidateBatch nearbyZombies = SafeScan(
			() => ScanZombies(WorldMaintenanceScopeKind.Nearby, normalizedRadius, includeSpecial: false, generate: false), "nearby zombies");
		CandidateBatch loadedZombies = SafeScan(
			() => ScanZombies(WorldMaintenanceScopeKind.LoadedWorld, normalizedRadius, includeSpecial: false, generate: false), "loaded zombies");
		CandidateBatch nearbyGenerateZombies = SafeScan(
			() => ScanZombies(WorldMaintenanceScopeKind.Nearby, normalizedRadius, includeSpecial: false, generate: true), "nearby zombie generation");
		CandidateBatch loadedGenerateZombies = SafeScan(
			() => ScanZombies(WorldMaintenanceScopeKind.LoadedWorld, normalizedRadius, includeSpecial: false, generate: true), "loaded zombie generation");
		CandidateBatch nearbyItems = SafeScan(
			() => ScanItems(WorldMaintenanceScopeKind.Nearby, normalizedRadius), "nearby ground items");
		CandidateBatch allItems = SafeScan(
			() => ScanItems(WorldMaintenanceScopeKind.WholeMap, normalizedRadius), "whole-map ground items");
		CandidateBatch nearbyRestock = SafeScan(
			() => ScanItemRestock(WorldMaintenanceScopeKind.Nearby, normalizedRadius), "nearby item restock");
		CandidateBatch allRestock = SafeScan(
			() => ScanItemRestock(WorldMaintenanceScopeKind.WholeMap, normalizedRadius), "whole-map item restock");
		WorldMaintenanceProgress progress = CaptureProgress();
		bool scansSucceeded = nearbyResources.ScanSucceeded && wholeResources.ScanSucceeded &&
			nearbyZombies.ScanSucceeded && loadedZombies.ScanSucceeded &&
			nearbyGenerateZombies.ScanSucceeded && loadedGenerateZombies.ScanSucceeded &&
			nearbyItems.ScanSucceeded && allItems.ScanSucceeded &&
			nearbyRestock.ScanSucceeded && allRestock.ScanSucceeded;
		bool resourceSupported = scansSucceeded && nearbyResources.ResourceSupported && wholeResources.ResourceSupported;
		string capability = resourceSupported ? _native.ResourceCapabilityMessage :
			FirstMessage(nearbyResources.Message, wholeResources.Message, nearbyZombies.Message,
				loadedZombies.Message, nearbyGenerateZombies.Message, loadedGenerateZombies.Message,
				nearbyItems.Message, allItems.Message, nearbyRestock.Message, allRestock.Message,
				_native.ResourceCapabilityMessage);
		return new WorldMaintenanceDashboardSnapshot(resourceSupported, capability,
			nearbyResources.CandidateCount + nearbyResources.BlockedCount,
			nearbyResources.CandidateCount, nearbyResources.BlockedCount,
			wholeResources.CandidateCount + wholeResources.BlockedCount,
			wholeResources.CandidateCount, wholeResources.BlockedCount,
			nearbyZombies.CandidateCount, loadedZombies.CandidateCount, loadedZombies.ProtectedCount,
			nearbyGenerateZombies.CandidateCount, loadedGenerateZombies.CandidateCount,
			nearbyItems.CandidateCount, allItems.CandidateCount,
			_snapshotVersion, _sceneVersion,
			nearbyRestock.CandidateCount, allRestock.CandidateCount,
			progress, _lastResult);
	}

	internal bool TryCreatePreview(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		float radius, bool includeSpecialZombies, out WorldMaintenancePreview preview, out string message)
	{
		preview = null;
		message = string.Empty;
		if (!CanOperate(out message) || State == WorldMaintenanceRunState.Running)
		{
			if (State == WorldMaintenanceRunState.Running) message = "已有世界维护任务正在运行。";
			return false;
		}
		if (!IsValidScope(action, scope))
		{
			message = "维护操作与作用域不匹配。";
			return false;
		}
		State = WorldMaintenanceRunState.ScanningPreview;
		float normalizedRadius = WorldMaintenancePolicy.NormalizeRadius(radius);
		CandidateBatch batch = SafeScan(
			() => Scan(action, scope, normalizedRadius, includeSpecialZombies), action.ToString());
		preview = ToPreview(action, scope, normalizedRadius, includeSpecialZombies, batch);
		message = batch.Message;
		if (!batch.ScanSucceeded || !batch.ResourceSupported)
		{
			State = WorldMaintenanceRunState.Failed;
			_message = FirstMessage(message, _native.ResourceCapabilityMessage);
			return false;
		}
		if (preview.CandidateCount <= 0)
		{
			State = WorldMaintenanceRunState.Idle;
			_message = "没有符合条件的目标。";
			message = _message;
			return true;
		}
		State = WorldMaintenanceRunState.AwaitingConfirmation;
		_message = "预览已生成，等待确认。";
		return true;
	}

	internal bool TryStart(WorldMaintenancePreview preview, out WorldMaintenancePreview currentPreview, out string message)
	{
		currentPreview = null;
		message = string.Empty;
		if (preview == null || !CanOperate(out message)) return false;
		if (State == WorldMaintenanceRunState.Running)
		{
			message = "已有世界维护任务正在运行。";
			return false;
		}
		CandidateBatch batch = SafeScan(
			() => Scan(preview.Action, preview.Scope, preview.RadiusMeters, preview.IncludeSpecialZombies),
			preview.Action.ToString());
		currentPreview = ToPreview(preview.Action, preview.Scope, preview.RadiusMeters,
			preview.IncludeSpecialZombies, batch);
		if (!batch.ScanSucceeded || !batch.ResourceSupported)
		{
			State = WorldMaintenanceRunState.Failed;
			message = FirstMessage(batch.Message, _native.ResourceCapabilityMessage);
			return false;
		}
		if (!WorldMaintenancePolicy.IsPreviewCurrent(preview, _snapshotVersion, _sceneVersion) ||
			!PreviewsMatch(preview, currentPreview))
		{
			State = WorldMaintenanceRunState.AwaitingConfirmation;
			message = "世界状态已变化，预览已刷新，请再次确认。";
			return false;
		}
		if (currentPreview.CandidateCount <= 0)
		{
			State = WorldMaintenanceRunState.Idle;
			message = "没有符合条件的目标。";
			return false;
		}
		_activeAction = preview.Action;
		_activeScope = preview.Scope;
		_lastResult = null;
		bool started = StartRunner(currentPreview, batch);
		if (!started)
		{
			State = WorldMaintenanceRunState.Failed;
			message = "维护候选未能进入执行队列。";
			return false;
		}
		State = WorldMaintenanceRunState.Running;
		_message = "维护任务正在分帧执行。";
		message = _message;
		return true;
	}

	internal void Pump()
	{
		if (_disposed || State != WorldMaintenanceRunState.Running) return;
		if (!_canExecuteGameplay())
		{
			Stop("已离开受支持的单人游戏状态，未处理候选已停止。");
			return;
		}
		if (_resourceRunner != null) PumpRunner(_resourceRunner);
		else if (_zombieRunner != null) PumpRunner(_zombieRunner);
		else if (_zombieGenerationRunner != null) PumpRunner(_zombieGenerationRunner);
		else if (_itemRegionRunner != null) PumpRunner(_itemRegionRunner);
		else if (_nearbyItemRunner != null) PumpRunner(_nearbyItemRunner);
		else if (_itemRestockRunner != null) PumpRunner(_itemRestockRunner);
	}

	internal void Stop(string message = null)
	{
		if (State != WorldMaintenanceRunState.Running) return;
		if (_resourceRunner != null) { _resourceRunner.Stop(message); Complete(_resourceRunner.Result); }
		else if (_zombieRunner != null) { _zombieRunner.Stop(message); Complete(_zombieRunner.Result); }
		else if (_zombieGenerationRunner != null) { _zombieGenerationRunner.Stop(message); Complete(_zombieGenerationRunner.Result); }
		else if (_itemRegionRunner != null) { _itemRegionRunner.Stop(message); Complete(_itemRegionRunner.Result); }
		else if (_nearbyItemRunner != null) { _nearbyItemRunner.Stop(message); Complete(_nearbyItemRunner.Result); }
		else if (_itemRestockRunner != null) { _itemRestockRunner.Stop(message); Complete(_itemRestockRunner.Result); }
	}

	internal void Fail(Exception exception)
	{
		if (_disposed) return;
		string message = "维护任务异常失败，剩余候选未处理。";
		try
		{
			if (_resourceRunner != null) { _resourceRunner.Fail(message); Complete(_resourceRunner.Result); }
			else if (_zombieRunner != null) { _zombieRunner.Fail(message); Complete(_zombieRunner.Result); }
			else if (_zombieGenerationRunner != null) { _zombieGenerationRunner.Fail(message); Complete(_zombieGenerationRunner.Result); }
			else if (_itemRegionRunner != null) { _itemRegionRunner.Fail(message); Complete(_itemRegionRunner.Result); }
			else if (_nearbyItemRunner != null) { _nearbyItemRunner.Fail(message); Complete(_nearbyItemRunner.Result); }
			else if (_itemRestockRunner != null) { _itemRestockRunner.Fail(message); Complete(_itemRestockRunner.Result); }
			else
			{
				State = WorldMaintenanceRunState.Failed;
				_message = message;
				_snapshotVersion++;
			}
		}
		catch
		{
			State = WorldMaintenanceRunState.Failed;
			_message = message;
			_snapshotVersion++;
			ClearRunners();
		}
	}

	internal void HandleSceneChanged()
	{
		if (_disposed) return;
		bool stoppedRunningTask = State == WorldMaintenanceRunState.Running;
		Stop("场景已切换，未处理候选已停止。已完成的世界修改不会回滚。");
		_sceneVersion++;
		_snapshotVersion++;
		ClearRunners();
		State = stoppedRunningTask ? WorldMaintenanceRunState.Stopped : WorldMaintenanceRunState.Idle;
		_message = "场景已切换，维护统计需要刷新。";
	}

	public void Dispose()
	{
		if (_disposed) return;
		Stop("插件正在卸载，未处理候选已停止。已完成的世界修改不会回滚。");
		_disposed = true;
		ClearRunners();
		GroundItemsChanged = null;
	}

	private bool StartRunner(WorldMaintenancePreview preview, CandidateBatch batch)
	{
		ClearRunners();
		if (preview.Action == WorldMaintenanceActionKind.RefreshNaturalResources)
		{
			_resourceRunner = new WorldMaintenanceBatchRunner<ResourceCandidate>(
				IsResourceCurrent, ClassifyResource, ExecuteResource);
			return _resourceRunner.TryStart(preview, batch.Resources, _snapshotVersion, _sceneVersion);
		}
		if (preview.Action == WorldMaintenanceActionKind.ClearZombies)
		{
			_zombieRunner = new WorldMaintenanceBatchRunner<ZombieCandidate>(
				candidate => candidate?.Zombie != null && !candidate.Zombie.isDead,
				ClassifyZombie, candidate => _native.SendZombieDead(candidate.Zombie));
			return _zombieRunner.TryStart(preview, batch.Zombies, _snapshotVersion, _sceneVersion);
		}
		if (preview.Action == WorldMaintenanceActionKind.GenerateZombies)
		{
			_zombieGenerationRunner = new WorldMaintenanceBatchRunner<ZombieCandidate>(
				candidate => candidate?.Zombie != null && candidate.Zombie.isDead,
				ClassifyZombieGeneration, _ => 1, ExecuteZombieGeneration);
			return _zombieGenerationRunner.TryStart(preview, batch.Zombies, _snapshotVersion, _sceneVersion);
		}
		if (preview.Action == WorldMaintenanceActionKind.RestockGroundItems)
		{
			_itemRestockRunner = new WorldMaintenanceBatchRunner<ItemRestockCandidate>(
				IsItemRestockCurrent, ClassifyItemRestock,
				candidate => candidate.ExpectedCount, ExecuteItemRestock);
			return _itemRestockRunner.TryStart(preview, batch.ItemRestock, _snapshotVersion, _sceneVersion);
		}
		if (preview.Scope == WorldMaintenanceScopeKind.WholeMap)
		{
			_itemRegionRunner = new WorldMaintenanceBatchRunner<ItemRegionCandidate>(
				IsItemRegionCurrent, _ => WorldMaintenanceCandidateDecision.Include,
				candidate => candidate.InitialCount, ExecuteItemRegion);
			return _itemRegionRunner.TryStart(preview, batch.ItemRegions, _snapshotVersion, _sceneVersion);
		}
		_nearbyItemRunner = new WorldMaintenanceBatchRunner<NearbyItemCandidate>(
			candidate => candidate != null && ItemManager.regions != null,
			_ => WorldMaintenanceCandidateDecision.Include,
			candidate => candidate.InitialCount, ExecuteNearbyItems);
		return _nearbyItemRunner.TryStart(preview, new[] { batch.NearbyItems }, _snapshotVersion, _sceneVersion);
	}

	private void PumpRunner<T>(WorldMaintenanceBatchRunner<T> runner)
	{
		runner.Pump(_snapshotVersion, _sceneVersion);
		if (runner.State != WorldMaintenanceRunState.Running)
		{
			Complete(runner.Result);
		}
	}

	private void Complete(WorldMaintenanceResult result)
	{
		if (result == null) return;
		_lastResult = result;
		State = result.RunState;
		_message = FormatResult(result);
		_snapshotVersion++;
		ClearRunners();
		_log?.LogInfo($"[WorldMaintenance] action={_activeAction} scope={_activeScope} {_message}");
	}

	internal WorldMaintenanceProgress CaptureProgress()
	{
		int processed = 0;
		int total = 0;
		if (_resourceRunner != null) { processed = _resourceRunner.ProcessedCount; total = _resourceRunner.TotalCount; }
		else if (_zombieRunner != null) { processed = _zombieRunner.ProcessedCount; total = _zombieRunner.TotalCount; }
		else if (_zombieGenerationRunner != null) { processed = _zombieGenerationRunner.ProcessedCount; total = _zombieGenerationRunner.TotalCount; }
		else if (_itemRegionRunner != null) { processed = _itemRegionRunner.ProcessedCount; total = _itemRegionRunner.TotalCount; }
		else if (_nearbyItemRunner != null) { processed = _nearbyItemRunner.ProcessedCount; total = _nearbyItemRunner.TotalCount; }
		else if (_itemRestockRunner != null) { processed = _itemRestockRunner.ProcessedCount; total = _itemRestockRunner.TotalCount; }
		return new WorldMaintenanceProgress(State, _activeAction, _activeScope,
			processed, total, State == WorldMaintenanceRunState.Running, _message);
	}

	private CandidateBatch Scan(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		float radius, bool includeSpecialZombies)
	{
		if (action == WorldMaintenanceActionKind.RefreshNaturalResources) return ScanResources(scope, radius);
		if (action == WorldMaintenanceActionKind.ClearZombies) return ScanZombies(scope, radius, includeSpecialZombies, generate: false);
		if (action == WorldMaintenanceActionKind.GenerateZombies) return ScanZombies(scope, radius, includeSpecialZombies, generate: true);
		if (action == WorldMaintenanceActionKind.RestockGroundItems) return ScanItemRestock(scope, radius);
		return ScanItems(scope, radius);
	}

	private CandidateBatch SafeScan(Func<CandidateBatch> scan, string operation)
	{
		return WorldMaintenanceScanCoordinator.SafeScan(scan, exception =>
		{
			_log?.LogWarning("[WorldMaintenance] " + operation + " 扫描失败；本次操作已 fail closed: " + exception);
			return new CandidateBatch
			{
				ScanSucceeded = false,
				ResourceSupported = false,
				Message = "世界维护扫描失败，操作已关闭。"
			};
		});
	}

	private CandidateBatch ScanResources(WorldMaintenanceScopeKind scope, float radius)
	{
		CandidateBatch batch = new CandidateBatch();
		if (!_native.TryGetResourceManager(out ResourceManager manager))
		{
			batch.ResourceSupported = false;
			batch.Message = _native.ResourceCapabilityMessage;
			return batch;
		}
		Player player = Player.LocalPlayer;
		Vector3 center = player != null ? player.transform.position : default;
		for (int x = 0; x < Regions.WORLD_SIZE; x++)
		{
			for (int y = 0; y < Regions.WORLD_SIZE; y++)
			{
				List<ResourceSpawnpoint> spawns = LevelGround.GetTreesOrNullInRegion((byte)x, (byte)y);
				for (int index = 0; spawns != null && index < spawns.Count && index <= ushort.MaxValue; index++)
				{
					ResourceSpawnpoint spawn = spawns[index];
					if (spawn == null || !spawn.isDead) continue;
					bool inScope = scope == WorldMaintenanceScopeKind.WholeMap ||
						(player != null && WorldMaintenancePolicy.IsWithinHorizontalRadius(
							spawn.point.x - center.x, spawn.point.z - center.z, radius));
					if (WorldMaintenancePolicy.ClassifyResource(exists: true, isDead: true, inScope) !=
						WorldMaintenanceCandidateDecision.Include) continue;
					ResourceCandidate candidate = new ResourceCandidate(manager, (byte)x, (byte)y, (ushort)index, spawn);
					batch.Resources.Add(candidate);
					if (!_native.TryIsResourceBlocked(manager, spawn, out bool blocked))
					{
						batch.ResourceSupported = false;
						batch.Message = _native.ResourceCapabilityMessage;
						return batch;
					}
					if (blocked) batch.BlockedCount++;
					else batch.CandidateCount++;
				}
			}
		}
		return batch;
	}

	private CandidateBatch ScanZombies(WorldMaintenanceScopeKind scope, float radius, bool includeSpecial, bool generate)
	{
		CandidateBatch batch = new CandidateBatch();
		Player player = Player.LocalPlayer;
		Vector3 center = player != null ? player.transform.position : default;
		Zombie[] zombies = ZombieManager.AllZombies?.ToArray() ?? Array.Empty<Zombie>();
		foreach (Zombie zombie in zombies)
		{
			bool inScope = scope == WorldMaintenanceScopeKind.LoadedWorld ||
				(player != null && zombie != null && WorldMaintenancePolicy.IsWithinHorizontalRadius(
					zombie.transform.position.x - center.x, zombie.transform.position.z - center.z, radius));
			bool hasBeacon = zombie != null && HasBeacon(zombie);
			WorldMaintenanceCandidateDecision decision = generate
				? WorldMaintenancePolicy.ClassifyZombieGeneration(zombie != null, zombie?.isDead == true, inScope,
					zombie?.isBoss == true, zombie?.isMega == true, hasBeacon, includeSpecial)
				: WorldMaintenancePolicy.ClassifyZombie(zombie != null, zombie?.isDead == true, inScope,
					zombie?.isBoss == true, zombie?.isMega == true, hasBeacon, includeSpecial);
			if (decision == WorldMaintenanceCandidateDecision.Include)
			{
				batch.Zombies.Add(new ZombieCandidate(zombie, includeSpecial));
				batch.CandidateCount++;
			}
			else if (decision == WorldMaintenanceCandidateDecision.Protected)
			{
				batch.Zombies.Add(new ZombieCandidate(zombie, includeSpecial));
				batch.ProtectedCount++;
			}
		}
		return batch;
	}

	private CandidateBatch ScanItemRestock(WorldMaintenanceScopeKind scope, float radius)
	{
		CandidateBatch batch = new CandidateBatch();
		if (!_native.ItemRespawnCapabilityResolved)
		{
			batch.ScanSucceeded = false;
			batch.Message = _native.ItemRespawnCapabilityMessage;
			return batch;
		}
		if (LevelItems.spawns == null || ItemManager.regions == null) return batch;
		Player player = Player.LocalPlayer;
		Vector3 center = player != null ? player.transform.position : default;
		for (int x = 0; x < LevelItems.spawns.GetLength(0); x++)
		{
			for (int y = 0; y < LevelItems.spawns.GetLength(1); y++)
			{
				List<ItemSpawnpoint> spawns = LevelItems.spawns[x, y];
				ItemRegion region = x < ItemManager.regions.GetLength(0) && y < ItemManager.regions.GetLength(1)
					? ItemManager.regions[x, y] : null;
				if (spawns == null || region?.items == null || spawns.Count == 0) continue;
				int desiredCount = (int)(spawns.Count * Provider.modeConfigData.Items.Spawn_Chance);
				int deficit = Math.Max(0, desiredCount - region.items.Count);
				if (deficit == 0) continue;
				bool inScope = scope == WorldMaintenanceScopeKind.WholeMap;
				int availableCount = 0;
				for (int index = 0; index < spawns.Count; index++)
				{
					ItemSpawnpoint spawn = spawns[index];
					if (spawn == null || !SafezoneManager.checkPointValid(spawn.point) ||
						!IsItemRestockPointAvailable(region, spawn.point)) continue;
					availableCount++;
					if (player != null && WorldMaintenancePolicy.IsWithinHorizontalRadius(
						spawn.point.x - center.x, spawn.point.z - center.z, radius))
					{
						inScope = true;
					}
				}
				int expectedCount = Math.Min(deficit, availableCount);
				if (!inScope || expectedCount == 0) continue;
				batch.ItemRestock.Add(new ItemRestockCandidate((byte)x, (byte)y, region, spawns, desiredCount, availableCount));
				batch.CandidateCount += expectedCount;
			}
		}
		return batch;
	}

	private static bool IsItemRestockPointAvailable(ItemRegion region, Vector3 point)
	{
		for (int index = 0; region?.items != null && index < region.items.Count; index++)
		{
			ItemData item = region.items[index];
			if (item != null && (item.point - point).sqrMagnitude < 4f) return false;
		}
		return true;
	}

	private CandidateBatch ScanItems(WorldMaintenanceScopeKind scope, float radius)
	{
		CandidateBatch batch = new CandidateBatch();
		ItemRegion[,] regions = ItemManager.regions;
		if (regions == null) return batch;
		Player player = Player.LocalPlayer;
		Vector3 center = player != null ? player.transform.position : default;
		float squaredRadius = radius * radius;
		for (int x = 0; x < regions.GetLength(0); x++)
		{
			for (int y = 0; y < regions.GetLength(1); y++)
			{
				ItemRegion region = regions[x, y];
				if (region?.items == null || region.items.Count == 0) continue;
				if (scope == WorldMaintenanceScopeKind.WholeMap)
				{
					batch.ItemRegions.Add(new ItemRegionCandidate((byte)x, (byte)y, region, region.items.Count));
					batch.CandidateCount += region.items.Count;
				}
				else if (player != null)
				{
					for (int index = 0; index < region.items.Count; index++)
					{
						ItemData item = region.items[index];
						if (item != null && (item.point - center).sqrMagnitude <= squaredRadius) batch.CandidateCount++;
					}
				}
			}
		}
		if (scope == WorldMaintenanceScopeKind.Nearby)
		{
			batch.NearbyItems = new NearbyItemCandidate(center, radius, batch.CandidateCount);
		}
		return batch;
	}

	private bool IsResourceCurrent(ResourceCandidate candidate)
	{
		if (candidate?.Manager == null || candidate.Spawn == null || !candidate.Spawn.isDead) return false;
		List<ResourceSpawnpoint> current = LevelGround.GetTreesOrNullInRegion(candidate.X, candidate.Y);
		return current != null && candidate.Index < current.Count && ReferenceEquals(current[candidate.Index], candidate.Spawn);
	}

	private WorldMaintenanceCandidateDecision ClassifyResource(ResourceCandidate candidate)
	{
		if (!_native.TryIsResourceBlocked(candidate.Manager, candidate.Spawn, out bool blocked))
		{
			return WorldMaintenanceCandidateDecision.CapabilityUnavailable;
		}
		return blocked ? WorldMaintenanceCandidateDecision.Blocked : WorldMaintenanceCandidateDecision.Include;
	}

	private void ExecuteResource(ResourceCandidate candidate)
	{
		_native.SetResourceAlive(candidate.X, candidate.Y, candidate.Index);
	}

	private static WorldMaintenanceCandidateDecision ClassifyZombie(ZombieCandidate candidate)
	{
		Zombie zombie = candidate?.Zombie;
		if (zombie == null || zombie.isDead) return WorldMaintenanceCandidateDecision.Invalid;
		return !candidate.IncludeSpecial && IsProtectedZombie(zombie)
			? WorldMaintenanceCandidateDecision.Protected
			: WorldMaintenanceCandidateDecision.Include;
	}

	private static WorldMaintenanceCandidateDecision ClassifyZombieGeneration(ZombieCandidate candidate)
	{
		Zombie zombie = candidate?.Zombie;
		if (zombie == null || !zombie.isDead) return WorldMaintenanceCandidateDecision.Invalid;
		return !candidate.IncludeSpecial && IsProtectedZombie(zombie)
			? WorldMaintenanceCandidateDecision.Protected
			: WorldMaintenanceCandidateDecision.Include;
	}

	private int ExecuteZombieGeneration(ZombieCandidate candidate)
	{
		Zombie zombie = candidate?.Zombie;
		if (zombie == null || !zombie.isDead) return 0;
		List<ZombieSpawnpoint> spawns = LevelZombies.zombies[zombie.bound];
		if (spawns == null || spawns.Count == 0) return 0;
		for (int attempt = 0; attempt < spawns.Count; attempt++)
		{
			ZombieSpawnpoint spawn = spawns[UnityEngine.Random.Range(0, spawns.Count)];
			if (spawn == null || !SafezoneManager.checkPointValid(spawn.point) ||
				!IsZombieSpawnPointAvailable(zombie.bound, spawn.point)) continue;
			_native.SendZombieAlive(zombie, zombie.type, (byte)zombie.speciality, zombie.shirt,
				zombie.pants, zombie.hat, zombie.gear, spawn.point + (Vector3.up * 0.5f),
				UnityEngine.Random.Range(0f, 360f));
			return 1;
		}
		return 0;
	}

	private static bool IsZombieSpawnPointAvailable(byte bound, Vector3 point)
	{
		ZombieRegion[] regions = ZombieManager.regions;
		if (regions == null || bound >= regions.Length || regions[bound] == null) return false;
		List<Zombie> zombies = regions[bound].zombies;
		for (int index = 0; zombies != null && index < zombies.Count; index++)
		{
			Zombie zombie = zombies[index];
			if (zombie != null && !zombie.isDead && (zombie.transform.position - point).sqrMagnitude < 4f) return false;
		}
		return true;
	}

	private bool IsItemRestockCurrent(ItemRestockCandidate candidate)
	{
		if (candidate?.Region == null || ItemManager.regions == null || LevelItems.spawns == null ||
			candidate.X >= LevelItems.spawns.GetLength(0) || candidate.Y >= LevelItems.spawns.GetLength(1) ||
			candidate.X >= ItemManager.regions.GetLength(0) || candidate.Y >= ItemManager.regions.GetLength(1)) return false;
		List<ItemSpawnpoint> spawns = LevelItems.spawns[candidate.X, candidate.Y];
		ItemRegion region = ItemManager.regions[candidate.X, candidate.Y];
		return spawns != null && ReferenceEquals(spawns, candidate.Spawns) && region != null &&
			ReferenceEquals(region, candidate.Region) && region.items != null;
	}

	private WorldMaintenanceCandidateDecision ClassifyItemRestock(ItemRestockCandidate candidate)
	{
		if (!_native.ItemRespawnCapabilityResolved || Provider.modeConfigData?.Items == null)
			return WorldMaintenanceCandidateDecision.CapabilityUnavailable;
		if (!IsItemRestockCurrent(candidate)) return WorldMaintenanceCandidateDecision.Invalid;
		int desiredCount = (int)(candidate.Spawns.Count * Provider.modeConfigData.Items.Spawn_Chance);
		if (candidate.Region.items.Count != candidate.InitialCount || desiredCount != candidate.DesiredCount)
			return WorldMaintenanceCandidateDecision.Invalid;
		int availableCount = 0;
		for (int index = 0; index < candidate.Spawns.Count; index++)
		{
			ItemSpawnpoint spawn = candidate.Spawns[index];
			if (spawn != null && SafezoneManager.checkPointValid(spawn.point) &&
				IsItemRestockPointAvailable(candidate.Region, spawn.point)) availableCount++;
		}
		return availableCount == candidate.AvailableCount ? WorldMaintenanceCandidateDecision.Include :
			WorldMaintenanceCandidateDecision.Invalid;
	}

	private int ExecuteItemRestock(ItemRestockCandidate candidate)
	{
		return WorldMaintenanceMutationCoordinator.Execute(delegate
		{
			return _native.TryRespawnItems(candidate.X, candidate.Y);
		}, NotifyGroundItemsChanged, LogGroundItemNotificationFailure);
	}

	private static bool IsProtectedZombie(Zombie zombie)
	{
		return zombie != null && (zombie.isBoss || zombie.isMega || HasBeacon(zombie));
	}

	private static bool HasBeacon(Zombie zombie)
	{
		ZombieRegion[] regions = ZombieManager.regions;
		return zombie != null && regions != null && zombie.bound < regions.Length &&
			regions[zombie.bound] != null && regions[zombie.bound].hasBeacon;
	}

	private static bool IsItemRegionCurrent(ItemRegionCandidate candidate)
	{
		ItemRegion[,] regions = ItemManager.regions;
		return candidate != null && regions != null && candidate.X < regions.GetLength(0) &&
			candidate.Y < regions.GetLength(1) && candidate.Region != null && candidate.Region.items != null &&
			candidate.Region.items.Count > 0 && ReferenceEquals(regions[candidate.X, candidate.Y], candidate.Region);
	}

	private int ExecuteItemRegion(ItemRegionCandidate candidate)
	{
		return WorldMaintenanceMutationCoordinator.Execute(delegate
		{
			int before = candidate.Region.items.Count;
			_native.ClearRegionItems(candidate.X, candidate.Y);
			int after = candidate.Region.items.Count;
			return Math.Min(candidate.InitialCount, Math.Max(0, before - after));
		}, NotifyGroundItemsChanged, LogGroundItemNotificationFailure);
	}

	private int ExecuteNearbyItems(NearbyItemCandidate candidate)
	{
		return WorldMaintenanceMutationCoordinator.Execute(delegate
		{
			int before = CountItemsInSphere(candidate.Center, candidate.Radius);
			_native.ClearItemsInSphere(candidate.Center, candidate.Radius);
			int after = CountItemsInSphere(candidate.Center, candidate.Radius);
			return Math.Min(candidate.InitialCount, Math.Max(0, before - after));
		}, NotifyGroundItemsChanged, LogGroundItemNotificationFailure);
	}

	private void NotifyGroundItemsChanged()
	{
		System.Action handlers = GroundItemsChanged;
		if (handlers == null) return;
		foreach (System.Action handler in handlers.GetInvocationList())
		{
			try
			{
				handler();
			}
			catch (Exception exception)
			{
				LogGroundItemNotificationFailure(exception);
			}
		}
	}

	private void LogGroundItemNotificationFailure(Exception exception)
	{
		_log?.LogWarning("[WorldMaintenance] Ground-item snapshot invalidation observer failed after native deletion: " + exception);
	}

	private static int CountItemsInSphere(Vector3 center, float radius)
	{
		ItemRegion[,] regions = ItemManager.regions;
		if (regions == null) return 0;
		float squaredRadius = radius * radius;
		int count = 0;
		for (int x = 0; x < regions.GetLength(0); x++)
		{
			for (int y = 0; y < regions.GetLength(1); y++)
			{
				List<ItemData> items = regions[x, y]?.items;
				for (int index = 0; items != null && index < items.Count; index++)
				{
					ItemData item = items[index];
					if (item != null && (item.point - center).sqrMagnitude <= squaredRadius) count++;
				}
			}
		}
		return count;
	}

	private WorldMaintenancePreview ToPreview(WorldMaintenanceActionKind action,
		WorldMaintenanceScopeKind scope, float radius, bool includeSpecial, CandidateBatch batch)
	{
		return new WorldMaintenancePreview(action, scope, radius, batch.CandidateCount,
			batch.ProtectedCount, batch.BlockedCount, _snapshotVersion, _sceneVersion, includeSpecial);
	}

	private static bool PreviewsMatch(WorldMaintenancePreview left, WorldMaintenancePreview right)
	{
		return left != null && right != null && left.Action == right.Action && left.Scope == right.Scope &&
			Math.Abs(left.RadiusMeters - right.RadiusMeters) < 0.001f &&
			left.CandidateCount == right.CandidateCount && left.ProtectedCount == right.ProtectedCount &&
			left.BlockedCount == right.BlockedCount && left.IncludeSpecialZombies == right.IncludeSpecialZombies;
	}

	private bool CanOperate(out string message)
	{
		if (_disposed)
		{
			message = "世界维护服务已释放。";
			return false;
		}
		if (!_canExecuteGameplay())
		{
			message = "世界维护仅在受支持的单人世界中可用。";
			return false;
		}
		message = string.Empty;
		return true;
	}

	private static bool IsValidScope(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope)
	{
		return action switch
		{
			WorldMaintenanceActionKind.RefreshNaturalResources => scope == WorldMaintenanceScopeKind.Nearby || scope == WorldMaintenanceScopeKind.WholeMap,
			WorldMaintenanceActionKind.ClearZombies => scope == WorldMaintenanceScopeKind.Nearby || scope == WorldMaintenanceScopeKind.LoadedWorld,
			WorldMaintenanceActionKind.ClearGroundItems => scope == WorldMaintenanceScopeKind.Nearby || scope == WorldMaintenanceScopeKind.WholeMap,
			WorldMaintenanceActionKind.RestockGroundItems => scope == WorldMaintenanceScopeKind.Nearby || scope == WorldMaintenanceScopeKind.WholeMap,
			WorldMaintenanceActionKind.GenerateZombies => scope == WorldMaintenanceScopeKind.Nearby || scope == WorldMaintenanceScopeKind.LoadedWorld,
			_ => false
		};
	}

	private static string FormatResult(WorldMaintenanceResult result)
	{
		string prefix = result.RunState == WorldMaintenanceRunState.Stopped ? "任务已停止" :
			result.RunState == WorldMaintenanceRunState.Failed ? "任务失败" : "任务完成";
		return $"{prefix}：成功 {result.SucceededCount}，保护 {result.ProtectedCount}，阻挡 {result.BlockedCount}，" +
			$"失效 {result.InvalidCount}，失败 {result.FailedCount}，未处理 {result.UnprocessedCount}。";
	}

	private static string FirstMessage(params string[] messages)
	{
		return messages.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
	}

	private void ClearRunners()
	{
		_resourceRunner = null;
		_zombieRunner = null;
		_zombieGenerationRunner = null;
		_itemRegionRunner = null;
		_nearbyItemRunner = null;
		_itemRestockRunner = null;
	}
}
