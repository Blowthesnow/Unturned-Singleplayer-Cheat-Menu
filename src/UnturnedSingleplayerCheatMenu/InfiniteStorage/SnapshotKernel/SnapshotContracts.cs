using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal readonly struct ItemGroupIdentity : IEquatable<ItemGroupIdentity>
{
	internal ItemGroupIdentity(ushort assetId, Guid assetGuid)
	{
		AssetId = assetId;
		AssetGuid = assetGuid;
	}

	internal ushort AssetId { get; }
	internal Guid AssetGuid { get; }

	public bool Equals(ItemGroupIdentity other) => AssetId == other.AssetId && AssetGuid == other.AssetGuid;
	public override bool Equals(object obj) => obj is ItemGroupIdentity other && Equals(other);
	public override int GetHashCode() => (AssetId * 397) ^ AssetGuid.GetHashCode();
}

internal sealed class ItemSnapshot : IEquatable<ItemSnapshot>
{
	private readonly byte[] _state;

	internal ItemSnapshot(ushort assetId, byte amount, byte quality, byte[] state)
	{
		AssetId = assetId;
		Amount = amount;
		Quality = quality;
		_state = CloneBytes(state);
	}

	internal ushort AssetId { get; }
	internal byte Amount { get; }
	internal byte Quality { get; }
	internal byte[] State => CloneBytes(_state);

	internal ItemSnapshot Clone() => new ItemSnapshot(AssetId, Amount, Quality, _state);

	public bool Equals(ItemSnapshot other)
	{
		return other != null && AssetId == other.AssetId && Amount == other.Amount &&
			Quality == other.Quality && _state.SequenceEqual(other._state);
	}

	public override bool Equals(object obj) => Equals(obj as ItemSnapshot);

	public override int GetHashCode()
	{
		unchecked
		{
			int hash = AssetId;
			hash = (hash * 397) ^ Amount;
			hash = (hash * 397) ^ Quality;
			for (int i = 0; i < _state.Length; i++) hash = (hash * 397) ^ _state[i];
			return hash;
		}
	}

	private static byte[] CloneBytes(byte[] bytes) =>
		bytes == null || bytes.Length == 0 ? Array.Empty<byte>() : (byte[])bytes.Clone();
}

internal sealed class SnapshotMutationResult
{
	internal bool Success { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotMutationResult Succeeded() => new SnapshotMutationResult { Success = true };
	internal static SnapshotMutationResult Failed(string errorCode) =>
		new SnapshotMutationResult { ErrorCode = errorCode ?? "snapshot-error" };
}

internal sealed class SnapshotWithdrawalResult
{
	internal bool Success { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;
	internal ItemSnapshot Snapshot { get; private set; }
	internal long Count { get; private set; }

	internal static SnapshotWithdrawalResult Succeeded(ItemSnapshot snapshot, long count) =>
		new SnapshotWithdrawalResult { Success = true, Snapshot = snapshot?.Clone(), Count = count };

	internal static SnapshotWithdrawalResult Failed(string errorCode) =>
		new SnapshotWithdrawalResult { ErrorCode = errorCode ?? "snapshot-error" };
}

internal sealed class SnapshotBucketView
{
	internal SnapshotBucketView(string keyHash, ItemSnapshot snapshot, long count, bool isDefaultTemplate)
	{
		KeyHash = keyHash ?? string.Empty;
		Snapshot = snapshot?.Clone();
		Count = count;
		IsDefaultTemplate = isDefaultTemplate;
	}

	internal string KeyHash { get; }
	internal ItemSnapshot Snapshot { get; }
	internal long Count { get; }
	internal bool IsDefaultTemplate { get; }
}

internal sealed class ItemGroupView
{
	internal ItemGroupView(ItemGroupIdentity identity, ItemSnapshot defaultTemplate,
		IEnumerable<SnapshotBucketView> buckets, long totalCount)
	{
		Identity = identity;
		DefaultTemplate = defaultTemplate?.Clone();
		Buckets = (buckets ?? Array.Empty<SnapshotBucketView>()).ToArray();
		TotalCount = totalCount;
	}

	internal ItemGroupIdentity Identity { get; }
	internal ushort AssetId => Identity.AssetId;
	internal Guid AssetGuid => Identity.AssetGuid;
	internal ItemSnapshot DefaultTemplate { get; }
	internal IReadOnlyList<SnapshotBucketView> Buckets { get; }
	internal long TotalCount { get; }
}

internal interface ISnapshotHashProvider
{
	string ComputeHash(byte[] encodedSnapshot);
}
