using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class VirtualStorageDocument
{
	internal const int CurrentSchemaVersion = 2;
	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public string WorldKey { get; set; } = string.Empty;
	public string PlayerKey { get; set; } = string.Empty;
	public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
	public List<VirtualStorageEntry> Entries { get; set; } = new List<VirtualStorageEntry>();
	public List<LegacyAmbiguousEntry> LegacyAmbiguousEntries { get; set; } = new List<LegacyAmbiguousEntry>();
}

internal sealed class VirtualStorageEntry
{
	public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
	public ushort AssetId { get; set; }
	public string AssetGuid { get; set; } = string.Empty;
	public StackUnitKind UnitKind { get; set; }
	public long StoredUnitCount { get; set; }
	public byte NativeAmount { get; set; }
	public byte NativeQuality { get; set; }
	public byte[] NativeState { get; set; } = Array.Empty<byte>();
	public byte NativeMaxStack { get; set; }
	public AmountRole AmountRole { get; set; }
	public QualityPolicy QualityPolicy { get; set; }
	public StatePolicy StatePolicy { get; set; }
	public WithdrawPolicy WithdrawPolicy { get; set; }
	public DisplayPolicy DisplayPolicy { get; set; }
	public int SemanticsRevision { get; set; } = ItemInstanceSemantics.CurrentRevision;

	// Read-only migration input for schema 1. It is never written into schema 2 snapshots.
	[JsonProperty("Count")]
	public long LegacyCount { get; set; }
	[JsonProperty("Quality")]
	public byte LegacyQuality { get; set; }
	[JsonProperty("State")]
	public byte[] LegacyState { get; set; }
	[JsonProperty("MaxNativeAmount")]
	public byte LegacyMaxNativeAmount { get; set; }
	public bool ShouldSerializeLegacyCount() => false;
	public bool ShouldSerializeLegacyQuality() => false;
	public bool ShouldSerializeLegacyState() => false;
	public bool ShouldSerializeLegacyMaxNativeAmount() => false;

	public VirtualStorageEntry Clone()
	{
		return new VirtualStorageEntry
		{
			EntryId = EntryId, AssetId = AssetId, AssetGuid = AssetGuid, UnitKind = UnitKind, StoredUnitCount = StoredUnitCount,
			NativeAmount = NativeAmount, NativeQuality = NativeQuality, NativeState = ItemInstanceSemantics.CloneState(NativeState),
			NativeMaxStack = NativeMaxStack, AmountRole = AmountRole, QualityPolicy = QualityPolicy, StatePolicy = StatePolicy,
			WithdrawPolicy = WithdrawPolicy, DisplayPolicy = DisplayPolicy, SemanticsRevision = SemanticsRevision
		};
	}
}

internal sealed class LegacyAmbiguousEntry
{
	public string Reason { get; set; } = string.Empty;
	public string OriginalJson { get; set; } = string.Empty;
	public DateTime IsolatedUtc { get; set; } = DateTime.UtcNow;
}

internal sealed class VirtualStorageMutation
{
	public bool Success { get; set; }
	public string Error { get; set; } = string.Empty;
	public VirtualStorageEntry Entry { get; set; }
}
