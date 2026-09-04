using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal static class SnapshotRuntimeIdentity
{
	private const string IdentityNamespace = "snapshot-storage-identity-v1";
	internal const int MinimumCharacterSlot = 0;
	internal const int NativeCharacterSlotCount = 5;
	internal const int MaximumCharacterSlot = NativeCharacterSlotCount - 1;

	internal static bool TryCreate(string serverId, string levelName, string steamId,
		int nativeCharacterSlot, int selectedCharacterSlot, bool hasSync,
		out SnapshotStorageIdentity identity, out string errorCode)
	{
		identity = default;
		errorCode = string.Empty;
		if (string.IsNullOrWhiteSpace(levelName))
		{
			errorCode = "identity-level-missing";
			return false;
		}
		if (!ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedSteamId) ||
			parsedSteamId == 0)
		{
			errorCode = "identity-steam-id-invalid";
			return false;
		}
		if (!IsValidCharacterSlot(nativeCharacterSlot) || !IsValidCharacterSlot(selectedCharacterSlot))
		{
			errorCode = "identity-character-slot-out-of-range";
			return false;
		}
		if (nativeCharacterSlot != selectedCharacterSlot)
		{
			errorCode = "identity-character-slot-mismatch";
			return false;
		}
		string slot = selectedCharacterSlot.ToString(CultureInfo.InvariantCulture);
		if (!string.Equals(serverId, "Singleplayer_" + slot, StringComparison.Ordinal))
		{
			errorCode = "identity-singleplayer-server-id-mismatch";
			return false;
		}

		string root = hasSync ? "Sync" : "Worlds/" + serverId;
		identity = new SnapshotStorageIdentity(
			IdentityNamespace + "|root=" + Segment(root) + "|level=" + Segment(levelName),
			IdentityNamespace + "|steam=" + parsedSteamId.ToString(CultureInfo.InvariantCulture) +
			"|character=" + slot);
		return true;
	}

	internal static bool IsValidCharacterSlot(int characterSlot) =>
		characterSlot >= MinimumCharacterSlot && characterSlot < NativeCharacterSlotCount;

	private static string Segment(string value)
	{
		string normalized = value ?? string.Empty;
		return normalized.Length.ToString(CultureInfo.InvariantCulture) + ":" + normalized;
	}
}

internal sealed class SnapshotReconciliationLatchResult
{
	internal bool Success { get; private set; }
	internal bool MutationsClosed { get; private set; }
	internal bool MarkerPersistenceAttempted { get; private set; }
	internal bool MarkerPersistenceThrew { get; private set; }
	internal bool MarkerPersisted { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotReconciliationLatchResult Completed(bool mutationsClosed,
		bool markerAttempted, bool markerThrew, bool markerPersisted, string errorCode) =>
		new SnapshotReconciliationLatchResult
		{
			Success = mutationsClosed && markerPersisted,
			MutationsClosed = mutationsClosed,
			MarkerPersistenceAttempted = markerAttempted,
			MarkerPersistenceThrew = markerThrew,
			MarkerPersisted = markerPersisted,
			ErrorCode = errorCode ?? string.Empty
		};
}

internal static class SnapshotReconciliationLatchCoordinator
{
	internal static SnapshotReconciliationLatchResult Execute(Action closeMutations,
		Func<bool> persistMarker)
	{
		bool mutationsClosed = false;
		try
		{
			closeMutations?.Invoke();
			mutationsClosed = closeMutations != null;
		}
		catch (Exception)
		{
			mutationsClosed = false;
		}

		bool markerAttempted = persistMarker != null;
		bool markerThrew = false;
		bool markerPersisted = false;
		try { markerPersisted = persistMarker?.Invoke() == true; }
		catch (Exception) { markerThrew = true; }

		string errorCode = string.Empty;
		if (!mutationsClosed) errorCode = "mutation-close-failed";
		if (!markerPersisted)
			errorCode += (errorCode.Length == 0 ? string.Empty : ":") + "marker-persistence-failed";
		return SnapshotReconciliationLatchResult.Completed(mutationsClosed, markerAttempted,
			markerThrew, markerPersisted, errorCode);
	}
}

internal sealed class SnapshotReconciliationPathLatch
{
	private static readonly object Sync = new object();
	private static readonly HashSet<string> Paths =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	internal void Latch(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return;
		lock (Sync) Paths.Add(path);
	}

	internal bool IsBlocked(string path, bool persistentMarkerExists)
	{
		if (string.IsNullOrWhiteSpace(path)) return false;
		lock (Sync)
		{
			if (persistentMarkerExists) Paths.Add(path);
			return Paths.Contains(path);
		}
	}
}

internal sealed class SnapshotActivationAttemptPolicy
{
	private SnapshotStorageIdentity _identity;
	private bool _hasIdentity;
	private bool _failureLatched;

	internal bool ShouldAttempt(SnapshotStorageIdentity identity)
	{
		if (!Matches(identity)) SetIdentity(identity);
		return identity.IsValid && !_failureLatched;
	}

	internal void RecordFailure(SnapshotStorageIdentity identity)
	{
		if (!Matches(identity)) SetIdentity(identity);
		_failureLatched = true;
	}

	internal void RecordSuccess(SnapshotStorageIdentity identity)
	{
		if (!Matches(identity)) SetIdentity(identity);
		_failureLatched = false;
	}

	internal void Reset()
	{
		_identity = default;
		_hasIdentity = false;
		_failureLatched = false;
	}

	private bool Matches(SnapshotStorageIdentity identity) => _hasIdentity &&
		string.Equals(_identity.WorldKey, identity.WorldKey, StringComparison.Ordinal) &&
		string.Equals(_identity.PlayerKey, identity.PlayerKey, StringComparison.Ordinal);

	private void SetIdentity(SnapshotStorageIdentity identity)
	{
		_identity = identity;
		_hasIdentity = true;
		_failureLatched = false;
	}
}
