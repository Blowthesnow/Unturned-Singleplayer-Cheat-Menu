using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class VirtualStorageService
{
	private readonly Dictionary<StorageKey, VirtualStorageEntry> _entries = new Dictionary<StorageKey, VirtualStorageEntry>();
	private readonly List<LegacyAmbiguousEntry> _legacyAmbiguousEntries = new List<LegacyAmbiguousEntry>();

	internal IReadOnlyList<VirtualStorageEntry> Entries => _entries.Values.OrderBy(entry => entry.AssetId).ThenBy(entry => entry.EntryId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList();
	internal IReadOnlyList<LegacyAmbiguousEntry> LegacyAmbiguousEntries => _legacyAmbiguousEntries.Select(CloneLegacy).ToList();

	internal void RestoreSnapshot(VirtualStorageDocument document)
	{
		_entries.Clear();
		_legacyAmbiguousEntries.Clear();
		if (document == null) return;
		foreach (LegacyAmbiguousEntry legacy in document.LegacyAmbiguousEntries ?? new List<LegacyAmbiguousEntry>())
		{
			if (legacy != null) _legacyAmbiguousEntries.Add(CloneLegacy(legacy));
		}
		foreach (VirtualStorageEntry raw in document.Entries ?? new List<VirtualStorageEntry>())
		{
			if (raw != null && raw.StoredUnitCount > 0 && !string.IsNullOrWhiteSpace(raw.AssetGuid)) AddLoaded(raw);
		}
	}

	internal void Load(VirtualStorageDocument document, Func<ushort, ItemAsset> assetResolver = null)
	{
		_entries.Clear();
		_legacyAmbiguousEntries.Clear();
		if (document == null) return;
		if (document.SchemaVersion == 1)
		{
			LoadLegacy(document, assetId =>
			{
				ItemAsset asset = assetResolver?.Invoke(assetId);
				return asset == null ? null : ItemInstanceSemantics.DescribeLegacy(asset, assetId, 1, 0, Array.Empty<byte>(), asset.amount);
			});
			return;
		}
		foreach (LegacyAmbiguousEntry legacy in document.LegacyAmbiguousEntries ?? new List<LegacyAmbiguousEntry>())
		{
			if (legacy != null) _legacyAmbiguousEntries.Add(CloneLegacy(legacy));
		}
		foreach (VirtualStorageEntry raw in document.Entries ?? new List<VirtualStorageEntry>())
		{
			if (raw == null || raw.StoredUnitCount <= 0 || string.IsNullOrWhiteSpace(raw.AssetGuid))
			{
				Isolate("schema 2 entry is incomplete", raw);
				continue;
			}
			ItemAsset asset = assetResolver?.Invoke(raw.AssetId);
			if (asset == null)
			{
				Isolate("asset cannot be resolved", raw);
				continue;
			}
			if (!string.Equals(asset.GUID.ToString("D"), raw.AssetGuid, StringComparison.OrdinalIgnoreCase))
			{
				Isolate("asset ID resolves to a different GUID", raw);
				continue;
			}
			AddLoaded(raw);
		}
	}

	internal void LoadForMigration(VirtualStorageDocument document, Func<ushort, ItemStorageDescriptor> legacyDescriptorResolver)
	{
		_entries.Clear();
		_legacyAmbiguousEntries.Clear();
		if (document == null) return;
		if (document.SchemaVersion == 1) LoadLegacy(document, legacyDescriptorResolver);
		else RestoreSnapshot(document);
	}

	internal VirtualStorageDocument Snapshot(string worldKey, string playerKey)
	{
		return new VirtualStorageDocument
		{
			WorldKey = worldKey ?? string.Empty, PlayerKey = playerKey ?? string.Empty, UpdatedUtc = DateTime.UtcNow,
			Entries = _entries.Values.Where(entry => entry.StoredUnitCount > 0).Select(entry => entry.Clone()).ToList(),
			LegacyAmbiguousEntries = _legacyAmbiguousEntries.Select(CloneLegacy).ToList()
		};
	}

	internal VirtualStorageMutation Add(ItemStorageDescriptor descriptor) => Add(descriptor, 1);

	internal VirtualStorageMutation Add(ItemStorageDescriptor descriptor, long occurrences)
	{
		if (descriptor == null || occurrences <= 0 || descriptor.AssetId == 0 || string.IsNullOrWhiteSpace(descriptor.AssetGuid))
			return new VirtualStorageMutation { Error = "Invalid item storage descriptor." };
		descriptor = descriptor.Clone();
		if (descriptor.QualityPolicy == QualityPolicy.Ignore) descriptor.NativeQuality = 0;
		if (descriptor.StatePolicy == StatePolicy.Ignore) descriptor.NativeState = Array.Empty<byte>();
		long unitsPerOccurrence = descriptor.UnitKind == StackUnitKind.ResourceUnits ? descriptor.NativeAmount : 1;
		if (unitsPerOccurrence <= 0) return new VirtualStorageMutation { Error = "Stored units must be greater than zero." };
		long units;
		try { units = checked(unitsPerOccurrence * occurrences); }
		catch (OverflowException) { return new VirtualStorageMutation { Error = "Stored unit count overflow." }; }
		StorageKey key = ItemInstanceSemantics.CreateKey(descriptor);
		_entries.TryGetValue(key, out VirtualStorageEntry entry);
		long nextCount;
		try { nextCount = checked((entry?.StoredUnitCount ?? 0) + units); }
		catch (OverflowException) { return new VirtualStorageMutation { Error = "Stored unit count overflow." }; }
		if (entry == null)
		{
			entry = CreateEntry(descriptor);
			_entries.Add(key, entry);
		}
		entry.StoredUnitCount = nextCount;
		return new VirtualStorageMutation { Success = true, Entry = entry.Clone() };
	}

	internal VirtualStorageMutation Remove(string entryId, long units)
	{
		VirtualStorageEntry entry = _entries.Values.FirstOrDefault(value => string.Equals(value.EntryId, entryId, StringComparison.Ordinal));
		if (entry == null || units <= 0 || entry.StoredUnitCount < units)
			return new VirtualStorageMutation { Error = "Storage entry does not exist or has insufficient units." };
		entry.StoredUnitCount -= units;
		VirtualStorageEntry result = entry.Clone();
		if (entry.StoredUnitCount == 0) _entries.Remove(CreateKey(entry));
		return new VirtualStorageMutation { Success = true, Entry = result };
	}

	internal bool Remove(ItemStorageDescriptor descriptor, long units)
	{
		if (descriptor == null || units <= 0) return false;
		StorageKey key = ItemInstanceSemantics.CreateKey(descriptor);
		if (!_entries.TryGetValue(key, out VirtualStorageEntry entry) || entry.StoredUnitCount < units) return false;
		entry.StoredUnitCount -= units;
		if (entry.StoredUnitCount == 0) _entries.Remove(key);
		return true;
	}

	internal long GetAvailableUnits(ushort assetId) => _entries.Values.Where(entry => entry.AssetId == assetId).Sum(entry => entry.StoredUnitCount);
	internal IReadOnlyList<VirtualStorageEntry> GetEntries(ushort assetId) => _entries.Values.Where(entry => entry.AssetId == assetId && entry.StoredUnitCount > 0).Select(entry => entry.Clone()).ToList();
	internal VirtualStorageEntry FindEntry(ItemStorageDescriptor descriptor)
	{
		if (descriptor == null) return null;
		return _entries.TryGetValue(ItemInstanceSemantics.CreateKey(descriptor), out VirtualStorageEntry entry) ? entry.Clone() : null;
	}

	private void LoadLegacy(VirtualStorageDocument document, Func<ushort, ItemStorageDescriptor> descriptorResolver)
	{
		foreach (VirtualStorageEntry raw in document.Entries ?? new List<VirtualStorageEntry>())
		{
			if (raw == null || raw.LegacyCount <= 0) { Isolate("legacy entry has no positive Count", raw); continue; }
			ItemStorageDescriptor classification = descriptorResolver?.Invoke(raw.AssetId);
			if (classification == null) { Isolate("legacy asset cannot be resolved", raw); continue; }
			byte[] state = raw.LegacyState ?? Array.Empty<byte>();
			ItemStorageDescriptor descriptor = classification.Clone();
			descriptor.AssetId = raw.AssetId;
			descriptor.NativeAmount = raw.LegacyMaxNativeAmount == 0 ? (byte)1 : raw.LegacyMaxNativeAmount;
			descriptor.NativeQuality = raw.LegacyQuality;
			descriptor.NativeState = ItemInstanceSemantics.CloneState(state);
			descriptor.NativeMaxStack = raw.LegacyMaxNativeAmount == 0 ? descriptor.NativeMaxStack : raw.LegacyMaxNativeAmount;
			if (descriptor == null || (descriptor.UnitKind == StackUnitKind.ResourceUnits && state.Length != 0))
			{
				Isolate("legacy entry cannot be proven to be a stateless resource", raw);
				continue;
			}
			if (descriptor.UnitKind != StackUnitKind.ResourceUnits && raw.LegacyCount != 1)
			{
				Isolate("legacy Count is ambiguous for complete instances", raw);
				continue;
			}
			if (descriptor.UnitKind == StackUnitKind.ResourceUnits)
			{
				descriptor.NativeAmount = (byte)Math.Min(byte.MaxValue, raw.LegacyCount);
				long remaining = raw.LegacyCount;
				while (remaining > 0)
				{
					descriptor.NativeAmount = (byte)Math.Min(byte.MaxValue, remaining);
					Add(descriptor);
					remaining -= descriptor.NativeAmount;
				}
			}
			else Add(descriptor);
		}
	}

	private void AddLoaded(VirtualStorageEntry raw)
	{
		VirtualStorageEntry entry = raw.Clone();
		entry.NativeState = ItemInstanceSemantics.CloneState(entry.NativeState);
		StorageKey key = CreateKey(entry);
		if (_entries.TryGetValue(key, out VirtualStorageEntry existing)) existing.StoredUnitCount = checked(existing.StoredUnitCount + entry.StoredUnitCount);
		else _entries.Add(key, entry);
	}

	private static VirtualStorageEntry CreateEntry(ItemStorageDescriptor descriptor) => new VirtualStorageEntry
	{
		AssetId = descriptor.AssetId, AssetGuid = descriptor.AssetGuid, UnitKind = descriptor.UnitKind, NativeAmount = descriptor.NativeAmount,
		NativeQuality = descriptor.NativeQuality, NativeState = ItemInstanceSemantics.CloneState(descriptor.NativeState), NativeMaxStack = descriptor.NativeMaxStack,
		AmountRole = descriptor.AmountRole, QualityPolicy = descriptor.QualityPolicy, StatePolicy = descriptor.StatePolicy,
		WithdrawPolicy = descriptor.WithdrawPolicy, DisplayPolicy = descriptor.DisplayPolicy, SemanticsRevision = ItemInstanceSemantics.CurrentRevision
	};

	private static StorageKey CreateKey(VirtualStorageEntry entry) => ItemInstanceSemantics.CreateKey(new ItemStorageDescriptor
	{
		AssetId = entry.AssetId, AssetGuid = entry.AssetGuid, UnitKind = entry.UnitKind, NativeQuality = entry.NativeQuality,
		NativeState = entry.NativeState, QualityPolicy = entry.QualityPolicy, StatePolicy = entry.StatePolicy
	});

	private void Isolate(string reason, VirtualStorageEntry raw) => _legacyAmbiguousEntries.Add(new LegacyAmbiguousEntry
	{
		Reason = reason, OriginalJson = SerializeOriginal(raw), IsolatedUtc = DateTime.UtcNow
	});

	private static string SerializeOriginal(VirtualStorageEntry raw)
	{
		if (raw == null) return "null";
		if (raw.LegacyCount > 0)
		{
			return new JObject
			{
				["EntryId"] = raw.EntryId,
				["AssetId"] = raw.AssetId,
				["Quality"] = raw.LegacyQuality,
				["State"] = JToken.FromObject(raw.LegacyState ?? Array.Empty<byte>()),
				["Count"] = raw.LegacyCount,
				["MaxNativeAmount"] = raw.LegacyMaxNativeAmount
			}.ToString(Formatting.None);
		}
		return JsonConvert.SerializeObject(raw, Formatting.None);
	}

	private static LegacyAmbiguousEntry CloneLegacy(LegacyAmbiguousEntry value) => new LegacyAmbiguousEntry { Reason = value.Reason, OriginalJson = value.OriginalJson, IsolatedUtc = value.IsolatedUtc };
}
