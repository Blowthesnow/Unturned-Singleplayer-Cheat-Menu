using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Patches;

internal static class ZombieRegionRetentionPatch
{
	private readonly struct FarRetainedRegion
	{
		internal FarRetainedRegion(byte bound, long sequence, ZombieRegion region)
		{
			Bound = bound;
			Sequence = sequence;
			Region = region;
		}

		internal byte Bound { get; }
		internal long Sequence { get; }
		internal ZombieRegion Region { get; }
	}

	[ThreadStatic]
	private static int _boundUpdateDepth;
	[ThreadStatic]
	private static int _cleanupDepth;

	private static ManualLogSource _log;
	private static readonly Dictionary<byte, long> RetainedRegions = new Dictionary<byte, long>();
	private static readonly List<byte> StaleBounds = new List<byte>();
	private static readonly List<FarRetainedRegion> FarRetainedRegions = new List<FarRetainedRegion>();
	private static long _retentionSequence;

	internal static void Install(Harmony harmony, ManualLogSource log)
	{
		try
		{
			MethodInfo boundUpdate = AccessTools.DeclaredMethod(typeof(ZombieManager), "onBoundUpdated",
				new[] { typeof(Player), typeof(byte), typeof(byte) });
			MethodInfo destroy = AccessTools.DeclaredMethod(typeof(ZombieRegion), "destroy", Type.EmptyTypes);
			MethodInfo generate = AccessTools.DeclaredMethod(typeof(ZombieManager), "generateZombies",
				new[] { typeof(byte) });
			if (boundUpdate == null || destroy == null || generate == null)
			{
				throw new MissingMethodException("Current Unturned zombie-region lifecycle methods could not be resolved.");
			}

			_log = log;
			ResetForSceneTransition();
			harmony.Patch(boundUpdate,
				prefix: new HarmonyMethod(AccessTools.Method(typeof(ZombieRegionRetentionPatch), nameof(BoundUpdatePrefix))),
				finalizer: new HarmonyMethod(AccessTools.Method(typeof(ZombieRegionRetentionPatch), nameof(BoundUpdateFinalizer))));
			harmony.Patch(destroy,
				prefix: new HarmonyMethod(AccessTools.Method(typeof(ZombieRegionRetentionPatch), nameof(DestroyPrefix))));
			harmony.Patch(generate,
				prefix: new HarmonyMethod(AccessTools.Method(typeof(ZombieRegionRetentionPatch), nameof(GeneratePrefix))));
			_log?.LogInfo($"[MinimapZombieRetention] 已挂接导航区保留、重复生成保护和远区 LRU 回收；" +
				$"farCap={ZombieRegionRetentionPolicy.MaxFarRetainedRegions}，" +
				$"padding={ZombieRegionRetentionPolicy.ProtectionRadiusPaddingMeters:0.#}m。");
		}
		catch (Exception exception)
		{
			_log = null;
			log?.LogWarning("[MinimapZombieRetention] 无法挂接僵尸导航区生命周期；保持游戏原生行为。\n" + exception);
		}
	}

	private static void BoundUpdatePrefix(byte oldBound, byte newBound, out bool __state)
	{
		__state = TryGetProtectionContext(out _, out _);
		if (!__state)
		{
			return;
		}

		_boundUpdateDepth++;
		_log?.LogDebug($"[MinimapZombieRetention] player bound {oldBound} -> {newBound}; evaluating retained regions.");
	}

	private static Exception BoundUpdateFinalizer(Exception __exception, bool __state)
	{
		if (__state)
		{
			_boundUpdateDepth = Math.Max(0, _boundUpdateDepth - 1);
			if (_boundUpdateDepth == 0)
			{
				Maintain();
			}
		}
		return __exception;
	}

	private static bool DestroyPrefix(ZombieRegion __instance)
	{
		if (__instance == null)
		{
			return true;
		}

		int count = __instance.zombies?.Count ?? 0;
		bool retentionEnabled = TryGetProtectionContext(out _, out _);
		if (_cleanupDepth > 0 || !ZombieRegionRetentionPolicy.ShouldSuppressBoundExitDestroy(
			retentionEnabled, _boundUpdateDepth > 0, count))
		{
			RetainedRegions.Remove(__instance.nav);
			return true;
		}

		TrackRetainedRegion(__instance.nav);
		_log?.LogDebug($"[MinimapZombieRetention] retained nav={__instance.nav} zombies={count} during bound exit.");
		return false;
	}

	private static bool GeneratePrefix(byte bound)
	{
		ZombieRegion[] regions = ZombieManager.regions;
		int existingCount = regions != null && bound < regions.Length && regions[bound]?.zombies != null
			? regions[bound].zombies.Count
			: 0;
		bool retentionEnabled = TryGetProtectionContext(out _, out _);
		if (!ZombieRegionRetentionPolicy.ShouldReuseExistingRegion(
			retentionEnabled, _boundUpdateDepth > 0, existingCount))
		{
			RetainedRegions.Remove(bound);
			return true;
		}

		RetainedRegions.Remove(bound);
		regions[bound].isNetworked = true;
		_log?.LogInfo($"[MinimapZombieRetention] reused nav={bound} zombies={existingCount}; skipped duplicate generation.");
		return false;
	}

	internal static void Maintain()
	{
		if (_boundUpdateDepth > 0 || _cleanupDepth > 0 || RetainedRegions.Count == 0)
		{
			return;
		}

		try
		{
			bool retentionEnabled = TryGetProtectionContext(out Vector3 center, out float protectionRadius);
			RecycleRetainedRegions(retentionEnabled, center, protectionRadius,
				retentionEnabled ? ZombieRegionRetentionPolicy.MaxFarRetainedRegions : 0,
				retentionEnabled ? "far-cap" : "feature-disabled");
		}
		catch (Exception exception)
		{
			_log?.LogWarning("[MinimapZombieRetention] 维护远区保留列表失败；本轮未继续清理。\n" + exception);
		}
	}

	internal static void ReleaseAll()
	{
		if (_boundUpdateDepth > 0 || _cleanupDepth > 0)
		{
			return;
		}

		try
		{
			RecycleRetainedRegions(retentionEnabled: false, default, 0f, 0, "plugin-teardown");
		}
		catch (Exception exception)
		{
			_log?.LogWarning("[MinimapZombieRetention] 卸载时清理保留区域失败；游戏后续生命周期仍可接管。\n" + exception);
		}
		finally
		{
			RetainedRegions.Clear();
			StaleBounds.Clear();
			FarRetainedRegions.Clear();
		}
	}

	internal static void ResetForSceneTransition()
	{
		RetainedRegions.Clear();
		StaleBounds.Clear();
		FarRetainedRegions.Clear();
		_retentionSequence = 0;
		_boundUpdateDepth = 0;
		_cleanupDepth = 0;
	}

	private static void RecycleRetainedRegions(bool retentionEnabled, Vector3 center, float protectionRadius,
		int farRetainedCap, string reason)
	{
		ZombieRegion[] regions = ZombieManager.regions;
		if (regions == null)
		{
			RetainedRegions.Clear();
			return;
		}

		StaleBounds.Clear();
		FarRetainedRegions.Clear();
		foreach (KeyValuePair<byte, long> pair in RetainedRegions)
		{
			byte bound = pair.Key;
			if (bound >= regions.Length || regions[bound] == null)
			{
				StaleBounds.Add(bound);
				continue;
			}

			ZombieRegion region = regions[bound];
			if (region.isNetworked || region.PlayerCountInRegion > 0)
			{
				StaleBounds.Add(bound);
				continue;
			}

			bool protectedByRadar = retentionEnabled &&
				HasLiveZombieInsideProtectionRadius(region, center, protectionRadius);
			if (!protectedByRadar)
			{
				FarRetainedRegions.Add(new FarRetainedRegion(bound, pair.Value, region));
			}
		}

		for (int index = 0; index < StaleBounds.Count; index++)
		{
			RetainedRegions.Remove(StaleBounds[index]);
		}

		FarRetainedRegions.Sort(CompareFarRetainedRegions);
		int recycleCount = ZombieRegionRetentionPolicy.ResolveFarRecycleCount(
			FarRetainedRegions.Count, farRetainedCap);
		int recycled = 0;
		for (int index = 0; index < recycleCount; index++)
		{
			FarRetainedRegion candidate = FarRetainedRegions[index];
			if (TryRecycle(candidate.Bound, candidate.Region, reason))
			{
				recycled++;
			}
		}

		if (recycled > 0)
		{
			_log?.LogInfo($"[MinimapZombieRetention] recycled={recycled} reason={reason}; " +
				$"retained={RetainedRegions.Count} farCap={farRetainedCap}.");
		}
	}

	private static bool TryRecycle(byte bound, ZombieRegion region, string reason)
	{
		bool tracked = RetainedRegions.ContainsKey(bound);
		if (region == null || !ZombieRegionRetentionPolicy.CanRecycleRegion(
			tracked, region.isNetworked, region.PlayerCountInRegion, hasLiveZombieInsideProtectionRadius: false))
		{
			return false;
		}

		RetainedRegions.Remove(bound);
		_cleanupDepth++;
		try
		{
			region.destroy();
			return true;
		}
		catch (Exception exception)
		{
			_log?.LogWarning($"[MinimapZombieRetention] recycle nav={bound} reason={reason} failed.\n{exception}");
			return false;
		}
		finally
		{
			_cleanupDepth = Math.Max(0, _cleanupDepth - 1);
		}
	}

	private static bool HasLiveZombieInsideProtectionRadius(ZombieRegion region, Vector3 center,
		float protectionRadius)
	{
		List<Zombie> zombies = region?.zombies;
		for (int index = 0; zombies != null && index < zombies.Count; index++)
		{
			Zombie zombie = zombies[index];
			if (zombie == null || zombie.isDead)
			{
				continue;
			}
			Vector3 position = zombie.transform.position;
			if (ZombieRegionRetentionPolicy.IsInsideProtectionRadius(
				position.x - center.x, position.z - center.z, protectionRadius))
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryGetProtectionContext(out Vector3 center, out float protectionRadius)
	{
		center = default;
		protectionRadius = 0f;
		MinimapSettings settings = CheatMenuPlugin.Instance?.Minimap?.Settings;
		Player player = Player.LocalPlayer;
		bool enabled = ZombieRegionRetentionPolicy.IsEnabled(
			SingleplayerGuard.IsReady,
			settings?.Enabled == true,
			settings?.ShowZombies == true) && player != null;
		if (!enabled)
		{
			return false;
		}

		center = player.transform.position;
		float candidateScanRadius = MinimapProjection.ResolveCandidateScanRadius(
			settings.VisibleRadiusMeters, settings.Shape);
		protectionRadius = ZombieRegionRetentionPolicy.ResolveProtectionRadius(candidateScanRadius);
		return true;
	}

	private static void TrackRetainedRegion(byte bound)
	{
		_retentionSequence++;
		RetainedRegions[bound] = _retentionSequence;
	}

	private static int CompareFarRetainedRegions(FarRetainedRegion left, FarRetainedRegion right)
	{
		int sequence = left.Sequence.CompareTo(right.Sequence);
		return sequence != 0 ? sequence : left.Bound.CompareTo(right.Bound);
	}
}
