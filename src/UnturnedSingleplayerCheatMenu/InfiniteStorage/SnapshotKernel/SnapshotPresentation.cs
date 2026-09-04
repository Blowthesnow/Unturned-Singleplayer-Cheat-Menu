using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal sealed class SnapshotAssetResolution
{
	internal bool CanWithdraw { get; private set; }
	internal string ErrorCode { get; private set; } = string.Empty;

	internal static SnapshotAssetResolution Allowed() => new SnapshotAssetResolution { CanWithdraw = true };
	internal static SnapshotAssetResolution Rejected(string errorCode) =>
		new SnapshotAssetResolution { ErrorCode = errorCode ?? "asset-invalid" };
}

internal static class SnapshotAssetGuard
{
	internal static SnapshotAssetResolution Evaluate(ItemGroupIdentity persisted, bool assetExists,
		ushort runtimeAssetId, Guid runtimeAssetGuid)
	{
		if (!assetExists) return SnapshotAssetResolution.Rejected("asset-missing");
		if (persisted.AssetId != runtimeAssetId) return SnapshotAssetResolution.Rejected("asset-id-conflict");
		if (persisted.AssetGuid != runtimeAssetGuid) return SnapshotAssetResolution.Rejected("asset-guid-conflict");
		return SnapshotAssetResolution.Allowed();
	}
}

internal readonly struct SnapshotBucketDisplayFacts
{
	private SnapshotBucketDisplayFacts(bool isAmmoContainer, int capacity)
	{
		IsAmmoContainer = isAmmoContainer;
		Capacity = capacity;
	}

	internal bool IsAmmoContainer { get; }
	internal int Capacity { get; }
	internal static SnapshotBucketDisplayFacts NativeAmount => default;

	internal static SnapshotBucketDisplayFacts AmmoContainer(int capacity)
	{
		if (capacity <= 0 || capacity > byte.MaxValue)
			throw new ArgumentOutOfRangeException(nameof(capacity));
		return new SnapshotBucketDisplayFacts(true, capacity);
	}
}

internal sealed class SnapshotBucketDisplayGroup
{
	internal SnapshotBucketDisplayGroup(IEnumerable<SnapshotBucketView> buckets)
	{
		Buckets = (buckets ?? Array.Empty<SnapshotBucketView>())
			.Where(bucket => bucket?.Snapshot != null && bucket.Count > 0).ToArray();
		if (Buckets.Count == 0) throw new ArgumentException("At least one Snapshot bucket is required.", nameof(buckets));
		long total = 0;
		foreach (SnapshotBucketView bucket in Buckets) total = checked(total + bucket.Count);
		TotalCount = total;
	}

	internal IReadOnlyList<SnapshotBucketView> Buckets { get; }
	internal SnapshotBucketView Representative => Buckets[0];
	internal long TotalCount { get; }
	internal bool IsDefaultTemplate => Buckets.Any(bucket => bucket.IsDefaultTemplate);
}

internal static class SnapshotBucketDisplayAggregator
{
	internal static IReadOnlyList<SnapshotBucketDisplayGroup> Build(ItemGroupView group,
		bool mergeHiddenQuality)
	{
		if (group == null) return Array.Empty<SnapshotBucketDisplayGroup>();
		List<List<SnapshotBucketView>> partitions = new List<List<SnapshotBucketView>>();
		foreach (SnapshotBucketView bucket in group.Buckets ?? Array.Empty<SnapshotBucketView>())
		{
			if (bucket?.Snapshot == null || bucket.Count <= 0) continue;
			List<SnapshotBucketView> partition = mergeHiddenQuality
				? partitions.FirstOrDefault(value => SameVisiblePayload(value[0].Snapshot, bucket.Snapshot))
				: null;
			if (partition == null)
			{
				partition = new List<SnapshotBucketView>();
				partitions.Add(partition);
			}
			partition.Add(bucket);
		}
		return partitions.Select(value => new SnapshotBucketDisplayGroup(value)).ToArray();
	}

	private static bool SameVisiblePayload(ItemSnapshot left, ItemSnapshot right) =>
		left != null && right != null && left.Amount == right.Amount &&
		left.State.SequenceEqual(right.State);
}

internal sealed class SnapshotBucketMetadata
{
	internal string Text { get; private set; } = string.Empty;

	internal static SnapshotBucketMetadata Build(ItemSnapshot snapshot, long count, bool showQuality,
		SnapshotBucketDisplayFacts displayFacts = default)
	{
		if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
		if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
		List<string> parts = new List<string>
		{
			displayFacts.IsAmmoContainer
				? "弹药 " + snapshot.Amount.ToString(CultureInfo.InvariantCulture) + "/" +
					displayFacts.Capacity.ToString(CultureInfo.InvariantCulture)
				: "原生数量 " + snapshot.Amount.ToString(CultureInfo.InvariantCulture),
			"实例 ×" + count.ToString(CultureInfo.InvariantCulture)
		};
		if (showQuality) parts.Add("品质 " + snapshot.Quality.ToString(CultureInfo.InvariantCulture));
		byte[] state = snapshot.State;
		parts.Add(state.Length == 0 ? "状态 空" : "状态指纹 " + StateFingerprint(state));
		return new SnapshotBucketMetadata { Text = string.Join(" · ", parts) };
	}

	private static string StateFingerprint(byte[] state)
	{
		using SHA256 sha256 = SHA256.Create();
		string hash = BitConverter.ToString(sha256.ComputeHash(state)).Replace("-", string.Empty);
		return hash.Substring(0, 8);
	}
}
