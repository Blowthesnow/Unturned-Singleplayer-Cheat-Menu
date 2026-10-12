using System;
using System.Reflection;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

/// <summary>
/// Single native write boundary for world maintenance. Private resource collision members are
/// resolved exactly once; if either signature drifts, resource restoration remains disabled.
/// </summary>
internal sealed class WorldMaintenanceNativeBridge
{
	private delegate bool ResourceOverlapDelegate(ResourceManager manager, ResourceSpawnpoint spawn, int layerMask);
	private delegate bool RespawnItemsDelegate(ItemManager manager);

	private readonly ManualLogSource _log;
	private readonly FieldInfo _resourceManagerField;
	private readonly ResourceOverlapDelegate _resourceOverlap;
	private readonly FieldInfo _respawnItemsXField;
	private readonly FieldInfo _respawnItemsYField;
	private readonly RespawnItemsDelegate _respawnItems;

	internal WorldMaintenanceNativeBridge(ManualLogSource log)
	{
		_log = log;
		try
		{
			_resourceManagerField = typeof(ResourceManager).GetField("manager",
				BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo overlap = typeof(ResourceManager).GetMethod("overlapTreeColliders",
				BindingFlags.Instance | BindingFlags.NonPublic, null,
				new[] { typeof(ResourceSpawnpoint), typeof(int) }, null);
			if (_resourceManagerField == null || _resourceManagerField.FieldType != typeof(ResourceManager))
			{
				throw new MissingFieldException(typeof(ResourceManager).FullName, "manager");
			}
			if (overlap == null || overlap.ReturnType != typeof(bool))
			{
				throw new MissingMethodException(typeof(ResourceManager).FullName,
					"overlapTreeColliders(ResourceSpawnpoint, Int32)");
			}
			_resourceOverlap = (ResourceOverlapDelegate)Delegate.CreateDelegate(
				typeof(ResourceOverlapDelegate), overlap, throwOnBindFailure: true);
			ResourceCapabilityMessage = "安全资源刷新入口已就绪。";
		}
		catch (Exception exception)
		{
			_resourceManagerField = null;
			_resourceOverlap = null;
			ResourceCapabilityMessage = "当前版本不支持安全资源刷新。";
			_log?.LogWarning("[WorldMaintenance] Resource collision capability disabled (fail closed): " + exception);
		}
		try
		{
			_respawnItemsXField = typeof(ItemManager).GetField("respawnItems_X", BindingFlags.Static | BindingFlags.NonPublic);
			_respawnItemsYField = typeof(ItemManager).GetField("respawnItems_Y", BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo respawn = typeof(ItemManager).GetMethod("respawnItems",
				BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			if (_respawnItemsXField == null || _respawnItemsYField == null || respawn == null ||
				_respawnItemsXField.FieldType != typeof(byte) || _respawnItemsYField.FieldType != typeof(byte) ||
				respawn.ReturnType != typeof(bool))
			{
				throw new MissingMethodException(typeof(ItemManager).FullName, "respawnItems()");
			}
			_respawnItems = (RespawnItemsDelegate)Delegate.CreateDelegate(
				typeof(RespawnItemsDelegate), respawn, throwOnBindFailure: true);
			ItemRespawnCapabilityMessage = "原生物资补货入口已就绪。";
		}
		catch (Exception exception)
		{
			_respawnItemsXField = null;
			_respawnItemsYField = null;
			_respawnItems = null;
			ItemRespawnCapabilityMessage = "当前版本不支持原生物资补货。";
			_log?.LogWarning("[WorldMaintenance] Native item respawn capability disabled: " + exception);
		}
	}

	internal bool ResourceCapabilityResolved => _resourceManagerField != null && _resourceOverlap != null;
	internal string ResourceCapabilityMessage { get; private set; }
	internal bool ItemRespawnCapabilityResolved => _respawnItems != null && _respawnItemsXField != null && _respawnItemsYField != null;
	internal string ItemRespawnCapabilityMessage { get; private set; }

	internal bool TryGetResourceManager(out ResourceManager manager)
	{
		manager = null;
		if (!ResourceCapabilityResolved) return false;
		try
		{
			manager = _resourceManagerField.GetValue(null) as ResourceManager;
			if (manager != null)
			{
				ResourceCapabilityMessage = "安全资源刷新入口已就绪。";
				return true;
			}
			ResourceCapabilityMessage = "资源管理器尚未就绪，安全资源刷新已禁用。";
			return false;
		}
		catch (Exception exception)
		{
			ResourceCapabilityMessage = "读取资源管理器失败，安全资源刷新已禁用。";
			_log?.LogWarning("[WorldMaintenance] Resource manager access failed closed: " + exception);
			return false;
		}
	}

	internal bool TryIsResourceBlocked(ResourceManager manager, ResourceSpawnpoint spawn, out bool blocked)
	{
		blocked = true;
		if (!ResourceCapabilityResolved || manager == null || spawn == null) return false;
		try
		{
			int layerMask = RayMasks.PLAYER | RayMasks.ENEMY;
			if (Provider.modeConfigData.Objects.Items_Obstruct_Tree_Respawns)
			{
				layerMask |= RayMasks.BARRICADE;
			}
			blocked = _resourceOverlap(manager, spawn, layerMask);
			return true;
		}
		catch (Exception exception)
		{
			ResourceCapabilityMessage = "资源碰撞检查失败，安全资源刷新已禁用。";
			_log?.LogWarning("[WorldMaintenance] Resource overlap check failed closed: " + exception);
			return false;
		}
	}

	internal void SetResourceAlive(byte x, byte y, ushort index)
	{
		ResourceManager.ServerSetResourceAlive(x, y, index);
	}

	internal void SendZombieDead(Zombie zombie)
	{
		ZombieManager.sendZombieDead(zombie, Vector3.zero, ERagdollEffect.None);
	}

	internal void ClearItemsInSphere(Vector3 center, float radius)
	{
		ItemManager.ServerClearItemsInSphere(center, radius);
	}

	internal int TryRespawnItems(byte x, byte y)
	{
		if (!ItemRespawnCapabilityResolved ||
			!Provider.isServer || ItemManager.instance == null || ItemManager.regions == null ||
			LevelItems.spawns == null || x >= LevelItems.spawns.GetLength(0) || y >= LevelItems.spawns.GetLength(1) ||
			x >= ItemManager.regions.GetLength(0) || y >= ItemManager.regions.GetLength(1)) return 0;
		ItemRegion region = ItemManager.regions[x, y];
		if (region?.items == null || LevelItems.spawns[x, y] == null || Provider.modeConfigData?.Items == null) return 0;
		int beforeCount = region.items.Count;
		int desiredCount = (int)(LevelItems.spawns[x, y].Count * Provider.modeConfigData.Items.Spawn_Chance);
		byte oldX = (byte)_respawnItemsXField.GetValue(null);
		byte oldY = (byte)_respawnItemsYField.GetValue(null);
		float oldLastRespawn = region.lastRespawn;
		try
		{
			_respawnItemsXField.SetValue(null, x);
			_respawnItemsYField.SetValue(null, y);
			region.lastRespawn = Time.realtimeSinceStartup - Provider.modeConfigData.Items.Respawn_Time - 0.01f;
			bool nativeResult = false;
			Exception nativeFailure = null;
			try
			{
				nativeResult = _respawnItems(ItemManager.instance);
			}
			catch (Exception exception)
			{
				nativeFailure = exception;
			}
			int afterCount = region.items.Count;
			int addedCount = Math.Max(0, afterCount - beforeCount);
			region.lastRespawn = addedCount == 0 ? oldLastRespawn : Time.realtimeSinceStartup;
			// SendItem already loops back through ReceiveItem and the native instantiation queue.
			// Spawning here as well would create two client drops with the same instanceID.
			string reason = addedCount > 0 ? "committed" : beforeCount >= desiredCount ? "quota-filled" : "no-valid-spawn";
			_log?.LogInfo($"[WorldMaintenance] native item respawn region=({x},{y}) data={beforeCount}->{afterCount} desired={desiredCount} added={addedCount} nativeResult={nativeResult} networked={region.isNetworked} reason={reason}.");
			if (nativeFailure != null)
			{
				_log?.LogWarning($"[WorldMaintenance] Native item respawn failed after committing {addedCount} items: {nativeFailure}");
				throw new WorldMaintenancePartialMutationException(addedCount, nativeFailure);
			}
			return addedCount;
		}
		finally
		{
			_respawnItemsXField.SetValue(null, oldX);
			_respawnItemsYField.SetValue(null, oldY);
		}
	}

	internal void ClearRegionItems(byte x, byte y)
	{
		ItemManager.askClearRegionItems(x, y);
	}

	internal void SendZombieAlive(Zombie zombie, byte type, byte speciality, byte shirt,
		byte pants, byte hat, byte gear, Vector3 position, float angle)
	{
		ZombieManager.sendZombieAlive(zombie, type, speciality, shirt, pants, hat, gear,
			position, MeasurementTool.angleToByte(angle));
	}
}
