using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.Models;

internal enum WorldMaintenanceActionKind
{
	RefreshNaturalResources,
	ClearZombies,
	ClearGroundItems,
	RestockGroundItems,
	GenerateZombies
}

internal enum WorldMaintenanceScopeKind
{
	Nearby,
	LoadedWorld,
	WholeMap
}

internal enum WorldMaintenanceRunState
{
	Idle,
	ScanningPreview,
	AwaitingConfirmation,
	Running,
	Completed,
	Failed,
	Stopped
}

internal enum WorldMaintenanceCandidateDecision
{
	Include,
	Protected,
	Blocked,
	CapabilityUnavailable,
	Invalid,
	OutOfScope
}

internal sealed class WorldMaintenancePreview
{
	internal WorldMaintenancePreview(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		float radiusMeters, int candidateCount, int protectedCount, int blockedCount,
		int snapshotVersion, int sceneVersion, bool includeSpecialZombies)
	{
		Action = action;
		Scope = scope;
		RadiusMeters = WorldMaintenancePolicy.NormalizeRadius(radiusMeters);
		CandidateCount = Math.Max(0, candidateCount);
		ProtectedCount = Math.Max(0, protectedCount);
		BlockedCount = Math.Max(0, blockedCount);
		SnapshotVersion = snapshotVersion;
		SceneVersion = sceneVersion;
		IncludeSpecialZombies = includeSpecialZombies;
	}

	internal WorldMaintenanceActionKind Action { get; }
	internal WorldMaintenanceScopeKind Scope { get; }
	internal float RadiusMeters { get; }
	internal int CandidateCount { get; }
	internal int ProtectedCount { get; }
	internal int BlockedCount { get; }
	internal int SnapshotVersion { get; }
	internal int SceneVersion { get; }
	internal bool IncludeSpecialZombies { get; }
	internal int AffectedCount => CandidateCount + BlockedCount;
}

internal sealed class WorldMaintenanceResult
{
	internal WorldMaintenanceResult(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		WorldMaintenanceRunState runState, int succeededCount, int protectedCount, int blockedCount,
		int invalidCount, int failedCount, int unprocessedCount, int originalCandidateCount, string message)
	{
		Action = action;
		Scope = scope;
		RunState = runState;
		SucceededCount = Math.Max(0, succeededCount);
		ProtectedCount = Math.Max(0, protectedCount);
		BlockedCount = Math.Max(0, blockedCount);
		InvalidCount = Math.Max(0, invalidCount);
		FailedCount = Math.Max(0, failedCount);
		UnprocessedCount = Math.Max(0, unprocessedCount);
		OriginalCandidateCount = Math.Max(0, originalCandidateCount);
		Message = message ?? string.Empty;
	}

	internal WorldMaintenanceActionKind Action { get; }
	internal WorldMaintenanceScopeKind Scope { get; }
	internal WorldMaintenanceRunState RunState { get; }
	internal int SucceededCount { get; }
	internal int ProtectedCount { get; }
	internal int BlockedCount { get; }
	internal int InvalidCount { get; }
	internal int FailedCount { get; }
	internal int UnprocessedCount { get; }
	internal int OriginalCandidateCount { get; }
	internal string Message { get; }
}

internal sealed class WorldMaintenanceProgress
{
	internal WorldMaintenanceProgress(WorldMaintenanceRunState state, WorldMaintenanceActionKind action,
		WorldMaintenanceScopeKind scope, int processedCount, int totalCount, bool canStop, string message)
	{
		State = state;
		Action = action;
		Scope = scope;
		ProcessedCount = Math.Max(0, processedCount);
		TotalCount = Math.Max(0, totalCount);
		CanStop = canStop;
		Message = message ?? string.Empty;
	}

	internal WorldMaintenanceRunState State { get; }
	internal WorldMaintenanceActionKind Action { get; }
	internal WorldMaintenanceScopeKind Scope { get; }
	internal int ProcessedCount { get; }
	internal int TotalCount { get; }
	internal bool CanStop { get; }
	internal string Message { get; }
	internal float Normalized => TotalCount <= 0 ? 0f : Math.Min(1f, (float)ProcessedCount / TotalCount);
}

internal sealed class WorldMaintenanceDashboardSnapshot
{
	internal WorldMaintenanceDashboardSnapshot(bool resourceRefreshSupported, string resourceCapabilityMessage,
		int nearbyDeadResources, int nearbyRecoverableResources, int nearbyBlockedResources,
		int wholeDeadResources, int wholeRecoverableResources, int wholeBlockedResources,
		int nearbyZombies, int loadedZombies, int protectedZombies,
		int nearbyDeadZombieSlots, int loadedDeadZombieSlots,
		int nearbyGroundItems, int allGroundItems, int snapshotVersion, int sceneVersion,
		int nearbyRestockableItems, int allRestockableItems,
		WorldMaintenanceProgress progress, WorldMaintenanceResult result)
	{
		ResourceRefreshSupported = resourceRefreshSupported;
		ResourceCapabilityMessage = resourceCapabilityMessage ?? string.Empty;
		NearbyDeadResources = Math.Max(0, nearbyDeadResources);
		NearbyRecoverableResources = Math.Max(0, nearbyRecoverableResources);
		NearbyBlockedResources = Math.Max(0, nearbyBlockedResources);
		WholeDeadResources = Math.Max(0, wholeDeadResources);
		WholeRecoverableResources = Math.Max(0, wholeRecoverableResources);
		WholeBlockedResources = Math.Max(0, wholeBlockedResources);
		NearbyZombies = Math.Max(0, nearbyZombies);
		LoadedZombies = Math.Max(0, loadedZombies);
		ProtectedZombies = Math.Max(0, protectedZombies);
		NearbyDeadZombieSlots = Math.Max(0, nearbyDeadZombieSlots);
		LoadedDeadZombieSlots = Math.Max(0, loadedDeadZombieSlots);
		NearbyGroundItems = Math.Max(0, nearbyGroundItems);
		AllGroundItems = Math.Max(0, allGroundItems);
		NearbyRestockableItems = Math.Max(0, nearbyRestockableItems);
		AllRestockableItems = Math.Max(0, allRestockableItems);
		SnapshotVersion = snapshotVersion;
		SceneVersion = sceneVersion;
		Progress = progress;
		Result = result;
	}

	internal bool ResourceRefreshSupported { get; }
	internal string ResourceCapabilityMessage { get; }
	internal int NearbyDeadResources { get; }
	internal int NearbyRecoverableResources { get; }
	internal int NearbyBlockedResources { get; }
	internal int WholeDeadResources { get; }
	internal int WholeRecoverableResources { get; }
	internal int WholeBlockedResources { get; }
	internal int NearbyZombies { get; }
	internal int LoadedZombies { get; }
	internal int ProtectedZombies { get; }
	internal int NearbyDeadZombieSlots { get; }
	internal int LoadedDeadZombieSlots { get; }
	internal int NearbyGroundItems { get; }
	internal int AllGroundItems { get; }
	internal int NearbyRestockableItems { get; }
	internal int AllRestockableItems { get; }
	internal int SnapshotVersion { get; }
	internal int SceneVersion { get; }
	internal WorldMaintenanceProgress Progress { get; }
	internal WorldMaintenanceResult Result { get; }
}

/// <summary>
/// Unity-free batch primitive shared by the runtime service and its Smoke harness. It owns one
/// immutable candidate snapshot, applies a strict per-Pump budget, revalidates immediately before
/// dispatch, and accounts every original candidate in a terminal result.
/// </summary>
internal sealed class WorldMaintenanceBatchRunner<T>
{
	private readonly Func<T, bool> _isCurrent;
	private readonly Func<T, WorldMaintenanceCandidateDecision> _classify;
	private readonly Func<T, int> _weight;
	private readonly Func<T, int> _execute;
	private IReadOnlyList<T> _candidates = Array.Empty<T>();
	private WorldMaintenancePreview _preview;
	private int _cursor;
	private int _succeeded;
	private int _protected;
	private int _blocked;
	private int _invalid;
	private int _failed;
	private int _unprocessed;
	private int _originalWeight;

	internal WorldMaintenanceBatchRunner(Func<T, bool> isCurrent,
		Func<T, WorldMaintenanceCandidateDecision> classify, Action<T> execute)
		: this(isCurrent, classify, _ => 1, candidate =>
		{
			execute(candidate);
			return 1;
		})
	{
		if (execute == null) throw new ArgumentNullException(nameof(execute));
	}

	internal WorldMaintenanceBatchRunner(Func<T, bool> isCurrent,
		Func<T, WorldMaintenanceCandidateDecision> classify, Func<T, int> weight, Func<T, int> execute)
	{
		_isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
		_classify = classify ?? throw new ArgumentNullException(nameof(classify));
		_weight = weight ?? throw new ArgumentNullException(nameof(weight));
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
	}

	internal WorldMaintenanceRunState State { get; private set; } = WorldMaintenanceRunState.Idle;
	internal WorldMaintenanceResult Result { get; private set; }
	internal int ProcessedCount => _cursor;
	internal int TotalCount => _candidates.Count;

	internal bool TryStart(WorldMaintenancePreview preview, IEnumerable<T> candidates,
		int currentSnapshotVersion, int currentSceneVersion)
	{
		if (State == WorldMaintenanceRunState.Running ||
			!WorldMaintenancePolicy.IsPreviewCurrent(preview, currentSnapshotVersion, currentSceneVersion))
		{
			return false;
		}
		T[] snapshot = candidates?.ToArray() ?? Array.Empty<T>();
		if (snapshot.Length == 0)
		{
			return false;
		}
		_preview = preview;
		_candidates = snapshot;
		_cursor = 0;
		_succeeded = 0;
		_protected = 0;
		_blocked = 0;
		_invalid = 0;
		_failed = 0;
		_unprocessed = 0;
		_originalWeight = snapshot.Sum(ResolveWeight);
		Result = null;
		State = WorldMaintenanceRunState.Running;
		return true;
	}

	internal void Pump(int currentSnapshotVersion, int currentSceneVersion)
	{
		if (State != WorldMaintenanceRunState.Running) return;
		if (!WorldMaintenancePolicy.IsPreviewCurrent(_preview, currentSnapshotVersion, currentSceneVersion))
		{
			Stop("世界或场景版本已变化，未处理候选已停止。");
			return;
		}
		int budget = WorldMaintenancePolicy.ResolveFrameBudget(_preview.Action, _preview.Scope);
		int end = Math.Min(_candidates.Count, _cursor + Math.Max(1, budget));
		while (_cursor < end)
		{
			T candidate = _candidates[_cursor++];
			int weight = ResolveWeight(candidate);
			bool current;
			try
			{
				current = _isCurrent(candidate);
			}
			catch
			{
				_failed += weight;
				continue;
			}
			if (!current)
			{
				_invalid += weight;
				continue;
			}
			WorldMaintenanceCandidateDecision decision;
			try
			{
				decision = _classify(candidate);
			}
			catch
			{
				_failed += weight;
				continue;
			}
			switch (decision)
			{
				case WorldMaintenanceCandidateDecision.CapabilityUnavailable:
					_unprocessed += weight;
					for (int index = _cursor; index < _candidates.Count; index++)
					{
						_unprocessed += ResolveWeight(_candidates[index]);
					}
					_cursor = _candidates.Count;
					Finish(WorldMaintenanceRunState.Failed, "原生能力在执行期间失效，剩余候选未处理。");
					return;
				case WorldMaintenanceCandidateDecision.Protected:
					_protected += weight;
					continue;
				case WorldMaintenanceCandidateDecision.Blocked:
					_blocked += weight;
					continue;
				case WorldMaintenanceCandidateDecision.Invalid:
				case WorldMaintenanceCandidateDecision.OutOfScope:
					_invalid += weight;
					continue;
			}
			try
			{
				int succeeded = Math.Max(0, Math.Min(weight, _execute(candidate)));
				_succeeded += succeeded;
				_invalid += weight - succeeded;
			}
			catch (WorldMaintenancePartialMutationException exception)
			{
				int succeeded = Math.Max(0, Math.Min(weight, exception.ChangedCount));
				_succeeded += succeeded;
				_failed += weight - succeeded;
			}
			catch
			{
				_failed += weight;
			}
		}
		if (_cursor >= _candidates.Count)
		{
			Finish(WorldMaintenanceRunState.Completed, "维护任务已完成。");
		}
	}

	internal void Stop(string message)
	{
		if (State != WorldMaintenanceRunState.Running) return;
		FinishOutstanding(WorldMaintenanceRunState.Stopped, message ?? "维护任务已停止。");
	}

	internal void Fail(string message)
	{
		if (State != WorldMaintenanceRunState.Running) return;
		FinishOutstanding(WorldMaintenanceRunState.Failed, message ?? "维护任务失败，剩余候选未处理。");
	}

	private void FinishOutstanding(WorldMaintenanceRunState state, string message)
	{
		for (int index = _cursor; index < _candidates.Count; index++)
		{
			_unprocessed += ResolveWeight(_candidates[index]);
		}
		_cursor = _candidates.Count;
		Finish(state, message);
	}

	private void Finish(WorldMaintenanceRunState state, string message)
	{
		int total = _originalWeight;
		int accounted = _succeeded + _protected + _blocked + _invalid + _failed + _unprocessed;
		if (accounted < total) _unprocessed += total - accounted;
		State = state;
		Result = new WorldMaintenanceResult(_preview.Action, _preview.Scope, state,
			_succeeded, _protected, _blocked, _invalid, _failed, _unprocessed, total, message);
		_candidates = Array.Empty<T>();
	}

	private int ResolveWeight(T candidate)
	{
		return Math.Max(0, _weight(candidate));
	}
}

internal sealed class WorldMaintenancePartialMutationException : Exception
{
	internal WorldMaintenancePartialMutationException(int changedCount, Exception innerException)
		: base("原生维护发生异常，已提交的修改已保留。", innerException)
	{
		ChangedCount = Math.Max(0, changedCount);
	}

	internal int ChangedCount { get; }
}

internal static class WorldMaintenanceMutationCoordinator
{
	internal static int Execute(Func<int> nativeMutation, System.Action notifyChanged,
		Action<Exception> observerFailure)
	{
		if (nativeMutation == null) throw new ArgumentNullException(nameof(nativeMutation));
		int changed;
		try
		{
			changed = nativeMutation();
		}
		catch (WorldMaintenancePartialMutationException exception)
		{
			Notify(exception.ChangedCount, notifyChanged, observerFailure);
			throw;
		}
		Notify(changed, notifyChanged, observerFailure);
		return changed;
	}

	private static void Notify(int changed, System.Action notifyChanged, Action<Exception> observerFailure)
	{
		if (changed <= 0 || notifyChanged == null) return;
		try
		{
			notifyChanged();
		}
		catch (Exception exception)
		{
			try
			{
				observerFailure?.Invoke(exception);
			}
			catch
			{
				// Notification diagnostics must never rewrite a completed native mutation.
			}
		}
	}
}

internal static class WorldMaintenanceScanCoordinator
{
	internal static T SafeScan<T>(Func<T> scan, Func<Exception, T> failClosed)
	{
		if (scan == null) throw new ArgumentNullException(nameof(scan));
		if (failClosed == null) throw new ArgumentNullException(nameof(failClosed));
		try
		{
			return scan();
		}
		catch (Exception exception)
		{
			return failClosed(exception);
		}
	}
}

internal static class WorldMaintenancePolicy
{
	internal const float DefaultRadius = 250f;
	internal const int ResourceBudget = 64;
	internal const int ZombieBudget = 16;
	internal const int ItemRegionBudget = 8;

	internal static float NormalizeRadius(float radius)
	{
		if (float.IsNaN(radius) || float.IsInfinity(radius)) return DefaultRadius;
		if (radius < 150f) return 100f;
		if (radius < 375f) return 250f;
		return 500f;
	}

	internal static bool IsWithinHorizontalRadius(float deltaX, float deltaZ, float radius)
	{
		float normalized = NormalizeRadius(radius);
		return deltaX * deltaX + deltaZ * deltaZ <= normalized * normalized;
	}

	internal static WorldMaintenanceCandidateDecision ClassifyResource(bool exists, bool isDead, bool inScope)
	{
		if (!exists || !isDead) return WorldMaintenanceCandidateDecision.Invalid;
		return inScope ? WorldMaintenanceCandidateDecision.Include : WorldMaintenanceCandidateDecision.OutOfScope;
	}

	internal static WorldMaintenanceCandidateDecision ClassifyZombie(bool exists, bool isDead, bool inScope,
		bool isBoss, bool isMega, bool hasBeacon, bool includeSpecial)
	{
		if (!exists || isDead) return WorldMaintenanceCandidateDecision.Invalid;
		if (!inScope) return WorldMaintenanceCandidateDecision.OutOfScope;
		if (!includeSpecial && (isBoss || isMega || hasBeacon)) return WorldMaintenanceCandidateDecision.Protected;
		return WorldMaintenanceCandidateDecision.Include;
	}

	internal static int ResolveFrameBudget(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope)
	{
		if (action == WorldMaintenanceActionKind.RefreshNaturalResources) return ResourceBudget;
		if (action == WorldMaintenanceActionKind.ClearZombies || action == WorldMaintenanceActionKind.GenerateZombies) return ZombieBudget;
		return scope == WorldMaintenanceScopeKind.WholeMap ? ItemRegionBudget : 1;
	}

	internal static WorldMaintenanceCandidateDecision ClassifyZombieGeneration(bool exists, bool isDead,
		bool inScope, bool isBoss, bool isMega, bool hasBeacon, bool includeSpecial)
	{
		if (!exists || !isDead) return WorldMaintenanceCandidateDecision.Invalid;
		if (!inScope) return WorldMaintenanceCandidateDecision.OutOfScope;
		if (!includeSpecial && (isBoss || isMega || hasBeacon)) return WorldMaintenanceCandidateDecision.Protected;
		return WorldMaintenanceCandidateDecision.Include;
	}

	internal static bool IsPreviewCurrent(WorldMaintenancePreview preview, int snapshotVersion, int sceneVersion)
	{
		return preview != null && preview.SnapshotVersion == snapshotVersion && preview.SceneVersion == sceneVersion;
	}

	internal static bool IsResultConserved(WorldMaintenanceResult result)
	{
		if (result == null) return false;
		return result.SucceededCount + result.ProtectedCount + result.BlockedCount +
			result.InvalidCount + result.FailedCount + result.UnprocessedCount == result.OriginalCandidateCount;
	}
}
