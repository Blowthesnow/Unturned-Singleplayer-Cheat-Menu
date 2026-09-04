using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;

internal sealed class SnapshotKernel
{
	private readonly ISnapshotHashProvider _hashProvider;
	private readonly Dictionary<ushort, ItemGroupState> _groups = new Dictionary<ushort, ItemGroupState>();

	internal SnapshotKernel() : this(new Sha256SnapshotHashProvider())
	{
	}

	internal SnapshotKernel(ISnapshotHashProvider hashProvider)
	{
		_hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
	}

	internal SnapshotMutationResult RegisterDefaultTemplate(ItemGroupIdentity identity, ItemSnapshot snapshot)
	{
		string validation = ValidateIdentityAndSnapshot(identity, snapshot);
		if (!string.IsNullOrEmpty(validation)) return SnapshotMutationResult.Failed(validation);
		if (_groups.TryGetValue(identity.AssetId, out ItemGroupState existing) &&
			existing.Identity.AssetGuid != identity.AssetGuid)
			return SnapshotMutationResult.Failed("asset-guid-conflict");
		ItemGroupState group = existing ?? new ItemGroupState(identity);
		group.DefaultTemplate = snapshot.Clone();
		if (existing == null) _groups.Add(identity.AssetId, group);
		return SnapshotMutationResult.Succeeded();
	}

	internal SnapshotMutationResult Store(ItemGroupIdentity identity, ItemSnapshot snapshot, long count = 1)
	{
		string validation = ValidateIdentityAndSnapshot(identity, snapshot);
		if (!string.IsNullOrEmpty(validation)) return SnapshotMutationResult.Failed(validation);
		if (count <= 0) return SnapshotMutationResult.Failed("invalid-count");
		if (_groups.TryGetValue(identity.AssetId, out ItemGroupState existing) &&
			existing.Identity.AssetGuid != identity.AssetGuid)
			return SnapshotMutationResult.Failed("asset-guid-conflict");

		string keyHash;
		try { keyHash = SnapshotKey.ComputeHash(snapshot, _hashProvider); }
		catch (Exception) { return SnapshotMutationResult.Failed("key-failed"); }
		if (string.IsNullOrWhiteSpace(keyHash)) return SnapshotMutationResult.Failed("key-failed");

		ItemGroupState group = existing ?? new ItemGroupState(identity);
		SnapshotBucketState bucket = group.FindBucket(keyHash, snapshot);
		long nextBucketCount;
		long nextTotalCount;
		try
		{
			nextBucketCount = checked((bucket?.Count ?? 0) + count);
			nextTotalCount = checked(group.TotalCount + count);
		}
		catch (OverflowException)
		{
			return SnapshotMutationResult.Failed("count-overflow");
		}

		if (existing == null) _groups.Add(identity.AssetId, group);
		if (bucket == null)
		{
			bucket = new SnapshotBucketState(keyHash, snapshot.Clone());
			group.AddBucket(bucket);
		}
		bucket.Count = nextBucketCount;
		group.TotalCount = nextTotalCount;
		return SnapshotMutationResult.Succeeded();
	}

	internal SnapshotWithdrawalResult Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long count)
	{
		string validation = ValidateIdentityAndSnapshot(identity, snapshot);
		if (!string.IsNullOrEmpty(validation)) return SnapshotWithdrawalResult.Failed(validation);
		if (count <= 0) return SnapshotWithdrawalResult.Failed("invalid-count");
		if (!_groups.TryGetValue(identity.AssetId, out ItemGroupState group))
			return SnapshotWithdrawalResult.Failed("bucket-not-found");
		if (group.Identity.AssetGuid != identity.AssetGuid)
			return SnapshotWithdrawalResult.Failed("asset-guid-conflict");

		string keyHash;
		try { keyHash = SnapshotKey.ComputeHash(snapshot, _hashProvider); }
		catch (Exception) { return SnapshotWithdrawalResult.Failed("key-failed"); }
		SnapshotBucketState bucket = group.FindBucket(keyHash, snapshot);
		if (bucket == null) return SnapshotWithdrawalResult.Failed("bucket-not-found");
		if (bucket.Count < count) return SnapshotWithdrawalResult.Failed("insufficient-count");

		bucket.Count -= count;
		group.TotalCount -= count;
		if (bucket.Count == 0) group.RemoveBucket(bucket);
		if (group.TotalCount == 0 && group.DefaultTemplate == null) _groups.Remove(identity.AssetId);
		return SnapshotWithdrawalResult.Succeeded(snapshot, count);
	}

	internal IReadOnlyList<ItemGroupView> GetGroups()
	{
		return _groups.Values.OrderBy(group => group.Identity.AssetId)
			.Select(group => group.ToView())
			.ToArray();
	}

	internal bool TryGetGroup(ItemGroupIdentity identity, out ItemGroupView group)
	{
		if (_groups.TryGetValue(identity.AssetId, out ItemGroupState state) &&
			state.Identity.Equals(identity))
		{
			group = state.ToView();
			return true;
		}
		group = null;
		return false;
	}

	internal SnapshotDocument ExportDocument(SnapshotStorageIdentity identity)
	{
		SnapshotDocument document = SnapshotDocument.Empty(identity);
		document.Groups = GetGroups()
			.Where(group => group.TotalCount > 0)
			.Select(group => new SnapshotDocumentGroup
			{
				AssetId = group.AssetId,
				AssetGuid = group.AssetGuid.ToString("D"),
				Buckets = group.Buckets.Where(bucket => bucket.Count > 0)
					.Select(bucket => new SnapshotDocumentBucket
					{
						Amount = bucket.Snapshot.Amount,
						Quality = bucket.Snapshot.Quality,
						State = bucket.Snapshot.State,
						Count = bucket.Count
					}).ToList()
			}).Where(group => group.Buckets.Count > 0).ToList();
		return document;
	}

	internal SnapshotMutationResult ReplaceFromDocument(SnapshotDocument document)
	{
		if (!SnapshotDocumentValidator.TryValidate(document, out string validationError))
			return SnapshotMutationResult.Failed(validationError);
		SnapshotKernel staged = new SnapshotKernel(_hashProvider);
		foreach (SnapshotDocumentGroup group in document.Groups)
		{
			if (!Guid.TryParseExact(group.AssetGuid, "D", out Guid assetGuid))
				return SnapshotMutationResult.Failed("invalid-snapshot-group");
			ItemGroupIdentity identity = new ItemGroupIdentity(group.AssetId, assetGuid);
			foreach (SnapshotDocumentBucket bucket in group.Buckets)
			{
				SnapshotMutationResult stored = staged.Store(identity,
					new ItemSnapshot(group.AssetId, bucket.Amount, bucket.Quality, bucket.State), bucket.Count);
				if (!stored.Success) return stored;
			}
		}
		_groups.Clear();
		foreach (KeyValuePair<ushort, ItemGroupState> pair in staged._groups) _groups.Add(pair.Key, pair.Value);
		return SnapshotMutationResult.Succeeded();
	}

	internal SnapshotKernelCheckpoint CreateCheckpoint() =>
		new SnapshotKernelCheckpoint(GetGroups().Select(group => new ItemGroupView(group.Identity,
			group.DefaultTemplate, group.Buckets, group.TotalCount)).ToArray());

	internal SnapshotMutationResult RestoreCheckpoint(SnapshotKernelCheckpoint checkpoint)
	{
		if (checkpoint == null) return SnapshotMutationResult.Failed("checkpoint-null");
		SnapshotKernel staged = new SnapshotKernel(_hashProvider);
		foreach (ItemGroupView group in checkpoint.Groups)
		{
			if (group.DefaultTemplate != null)
			{
				SnapshotMutationResult registered = staged.RegisterDefaultTemplate(group.Identity, group.DefaultTemplate);
				if (!registered.Success) return registered;
			}
			foreach (SnapshotBucketView bucket in group.Buckets)
			{
				SnapshotMutationResult stored = staged.Store(group.Identity, bucket.Snapshot, bucket.Count);
				if (!stored.Success) return stored;
			}
		}
		_groups.Clear();
		foreach (KeyValuePair<ushort, ItemGroupState> pair in staged._groups) _groups.Add(pair.Key, pair.Value);
		return SnapshotMutationResult.Succeeded();
	}

	private static string ValidateIdentityAndSnapshot(ItemGroupIdentity identity, ItemSnapshot snapshot)
	{
		if (identity.AssetId == 0 || identity.AssetGuid == Guid.Empty) return "invalid-identity";
		if (snapshot == null || snapshot.AssetId != identity.AssetId) return "snapshot-identity-mismatch";
		return string.Empty;
	}

	private sealed class ItemGroupState
	{
		private readonly Dictionary<string, List<SnapshotBucketState>> _bucketsByHash =
			new Dictionary<string, List<SnapshotBucketState>>(StringComparer.Ordinal);

		internal ItemGroupState(ItemGroupIdentity identity) => Identity = identity;

		internal ItemGroupIdentity Identity { get; }
		internal ItemSnapshot DefaultTemplate { get; set; }
		internal long TotalCount { get; set; }

		internal SnapshotBucketState FindBucket(string keyHash, ItemSnapshot snapshot)
		{
			return _bucketsByHash.TryGetValue(keyHash, out List<SnapshotBucketState> candidates)
				? candidates.FirstOrDefault(candidate => candidate.Snapshot.Equals(snapshot))
				: null;
		}

		internal void AddBucket(SnapshotBucketState bucket)
		{
			if (!_bucketsByHash.TryGetValue(bucket.KeyHash, out List<SnapshotBucketState> candidates))
			{
				candidates = new List<SnapshotBucketState>();
				_bucketsByHash.Add(bucket.KeyHash, candidates);
			}
			candidates.Add(bucket);
		}

		internal void RemoveBucket(SnapshotBucketState bucket)
		{
			if (!_bucketsByHash.TryGetValue(bucket.KeyHash, out List<SnapshotBucketState> candidates)) return;
			candidates.Remove(bucket);
			if (candidates.Count == 0) _bucketsByHash.Remove(bucket.KeyHash);
		}

		internal ItemGroupView ToView()
		{
			SnapshotBucketState[] ordered = _bucketsByHash.Values.SelectMany(value => value)
				.OrderByDescending(bucket => DefaultTemplate != null && bucket.Snapshot.Equals(DefaultTemplate))
				.ThenBy(bucket => bucket.Snapshot.Amount)
				.ThenBy(bucket => bucket.Snapshot.Quality)
				.ThenBy(bucket => Convert.ToBase64String(bucket.Snapshot.State), StringComparer.Ordinal)
				.ThenBy(bucket => bucket.KeyHash, StringComparer.Ordinal)
				.ToArray();
			return new ItemGroupView(Identity, DefaultTemplate,
				ordered.Select(bucket => new SnapshotBucketView(bucket.KeyHash, bucket.Snapshot, bucket.Count,
					DefaultTemplate != null && bucket.Snapshot.Equals(DefaultTemplate))), TotalCount);
		}
	}

	private sealed class SnapshotBucketState
	{
		internal SnapshotBucketState(string keyHash, ItemSnapshot snapshot)
		{
			KeyHash = keyHash;
			Snapshot = snapshot;
		}

		internal string KeyHash { get; }
		internal ItemSnapshot Snapshot { get; }
		internal long Count { get; set; }
	}
}
