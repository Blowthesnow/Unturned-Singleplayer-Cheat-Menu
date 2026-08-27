using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal enum StackUnitKind
{
	ResourceUnits,
	ContainerInstances,
	SingletonInstances
}

internal enum AmountRole
{
	StackCount,
	AmmoRounds,
	FuelUnits,
	WaterUnits,
	ContainerContents,
	NotApplicable
}

internal enum QualityPolicy { Ignore, Exact }
internal enum StatePolicy { Ignore, ExactBytes }
internal enum WithdrawPolicy { SplitResourceUnits, WholeInstancesOnly }
internal enum DisplayPolicy { ResourceTotal, InstanceCount, AmmoContainer, FuelOrWaterContainer, ConservativeInstance }

internal sealed class ItemStorageDescriptor
{
	internal ushort AssetId { get; set; }
	internal string AssetGuid { get; set; } = string.Empty;
	internal StackUnitKind UnitKind { get; set; }
	internal AmountRole AmountRole { get; set; }
	internal QualityPolicy QualityPolicy { get; set; }
	internal StatePolicy StatePolicy { get; set; }
	internal WithdrawPolicy WithdrawPolicy { get; set; }
	internal DisplayPolicy DisplayPolicy { get; set; }
	internal byte NativeAmount { get; set; }
	internal byte NativeQuality { get; set; }
	internal byte[] NativeState { get; set; } = Array.Empty<byte>();
	internal byte NativeMaxStack { get; set; }

	internal ItemStorageDescriptor Clone()
	{
		return new ItemStorageDescriptor
		{
			AssetId = AssetId, AssetGuid = AssetGuid, UnitKind = UnitKind, AmountRole = AmountRole,
			QualityPolicy = QualityPolicy, StatePolicy = StatePolicy, WithdrawPolicy = WithdrawPolicy,
			DisplayPolicy = DisplayPolicy, NativeAmount = NativeAmount, NativeQuality = NativeQuality,
			NativeState = ItemInstanceSemantics.CloneState(NativeState), NativeMaxStack = NativeMaxStack
		};
	}
}

internal readonly struct StorageKey : IEquatable<StorageKey>
{
	private readonly string _value;
	internal StorageKey(string value) => _value = value ?? string.Empty;
	public bool Equals(StorageKey other) => string.Equals(_value, other._value, StringComparison.Ordinal);
	public override bool Equals(object obj) => obj is StorageKey other && Equals(other);
	public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value);
	public override string ToString() => _value;
}

internal readonly struct NativeItemPayload
{
	internal readonly ushort AssetId;
	internal readonly byte Amount;
	internal readonly byte Quality;
	internal readonly byte[] State;
	internal NativeItemPayload(ushort assetId, byte amount, byte quality, byte[] state)
	{
		AssetId = assetId; Amount = amount; Quality = quality; State = ItemInstanceSemantics.CloneState(state);
	}
}

internal static class ItemInstanceSemantics
{
	internal const int CurrentRevision = 1;
	private static readonly HashSet<Guid> AmmoContainerGuids = new HashSet<Guid>();

	internal static void SetAmmoContainerGuids(IEnumerable<Guid> guids)
	{
		AmmoContainerGuids.Clear();
		if (guids == null) return;
		foreach (Guid guid in guids) AmmoContainerGuids.Add(guid);
	}

	internal static ItemStorageDescriptor Describe(ItemAsset asset, Item item)
	{
		if (asset == null) throw new ArgumentNullException(nameof(asset));
		if (item == null) throw new ArgumentNullException(nameof(item));
		string typeName = asset.GetType().Name;
		bool isResource = string.Equals(typeName, "ItemResourceAsset", StringComparison.Ordinal) && (item.state == null || item.state.Length == 0);
		bool isAmmoContainer = asset is ItemMagazineAsset ||
			(asset is ItemSupplyAsset && AmmoContainerGuids.Contains(asset.GUID));
		bool isFuel = string.Equals(typeName, "ItemFuelAsset", StringComparison.Ordinal);
		bool isTank = string.Equals(typeName, "ItemTankAsset", StringComparison.Ordinal);
		bool isContentsContainer = string.Equals(typeName, "ItemGeneratorAsset", StringComparison.Ordinal) ||
			string.Equals(typeName, "ItemStorageAsset", StringComparison.Ordinal) ||
			string.Equals(typeName, "ItemLibraryAsset", StringComparison.Ordinal);

		if (isResource)
		{
			return Create(asset, item, StackUnitKind.ResourceUnits, AmountRole.StackCount, QualityPolicy.Ignore, StatePolicy.Ignore,
				WithdrawPolicy.SplitResourceUnits, DisplayPolicy.ResourceTotal);
		}
		if (isAmmoContainer)
		{
			return Create(asset, item, StackUnitKind.ContainerInstances, AmountRole.AmmoRounds, QualityPolicy.Exact, StatePolicy.ExactBytes,
				WithdrawPolicy.WholeInstancesOnly, DisplayPolicy.AmmoContainer);
		}
		if (isFuel || isTank)
		{
			return Create(asset, item, StackUnitKind.ContainerInstances, isTank ? AmountRole.WaterUnits : AmountRole.FuelUnits,
				QualityPolicy.Exact, StatePolicy.ExactBytes, WithdrawPolicy.WholeInstancesOnly, DisplayPolicy.FuelOrWaterContainer);
		}
		if (isContentsContainer)
		{
			return Create(asset, item, StackUnitKind.ContainerInstances, AmountRole.ContainerContents, QualityPolicy.Exact, StatePolicy.ExactBytes,
				WithdrawPolicy.WholeInstancesOnly, DisplayPolicy.ConservativeInstance);
		}
		return Create(asset, item, StackUnitKind.SingletonInstances, AmountRole.NotApplicable, QualityPolicy.Exact, StatePolicy.ExactBytes,
			WithdrawPolicy.WholeInstancesOnly, DisplayPolicy.ConservativeInstance);
	}

	internal static ItemStorageDescriptor DescribeLegacy(ItemAsset asset, ushort assetId, byte nativeAmount, byte quality, byte[] state, byte nativeMaxStack)
	{
		if (asset == null) return null;
		return Describe(asset, new Item(assetId, nativeAmount == 0 ? (byte)1 : nativeAmount, quality, CloneState(state)));
	}

	internal static StorageKey CreateKey(ItemStorageDescriptor descriptor)
	{
		if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
		string quality = descriptor.QualityPolicy == QualityPolicy.Exact ? descriptor.NativeQuality.ToString() : "-";
		string state = descriptor.StatePolicy == StatePolicy.ExactBytes ? Convert.ToBase64String(CloneState(descriptor.NativeState)) : "-";
		return new StorageKey(descriptor.AssetId + "|" + (descriptor.AssetGuid ?? string.Empty) + "|" + descriptor.UnitKind + "|" + quality + "|" + state);
	}

	internal static IReadOnlyList<NativeItemPayload> CreateWithdrawPayloads(VirtualStorageEntry entry, long requestedUnits)
	{
		List<NativeItemPayload> items = new List<NativeItemPayload>();
		if (entry == null || requestedUnits <= 0) return items;
		long available = Math.Min(requestedUnits, entry.StoredUnitCount);
		if (entry.WithdrawPolicy == WithdrawPolicy.WholeInstancesOnly)
		{
			for (long i = 0; i < available; i++) items.Add(CreateNativePayload(entry));
			return items;
		}
		byte max = entry.NativeMaxStack == 0 ? (byte)1 : entry.NativeMaxStack;
		while (available > 0)
		{
			byte amount = (byte)Math.Min(max, available);
			items.Add(new NativeItemPayload(entry.AssetId, amount, entry.NativeQuality, entry.NativeState));
			available -= amount;
		}
		return items;
	}

	internal static IReadOnlyList<Item> CreateWithdrawItems(VirtualStorageEntry entry, long requestedUnits)
	{
		return CreateWithdrawPayloads(entry, requestedUnits).Select(payload => new Item(payload.AssetId, payload.Amount, payload.Quality, CloneState(payload.State))).ToList();
	}

	internal static InfiniteStorageCardMetadata BuildCardMetadata(ItemAsset asset, VirtualStorageEntry entry)
	{
		return InfiniteStorageCardMetadata.Build(entry, assetResolved: asset != null, showQuality: asset != null && asset.showQuality);
	}

	internal static byte[] CloneState(byte[] state) => state == null || state.Length == 0 ? Array.Empty<byte>() : (byte[])state.Clone();

	private static ItemStorageDescriptor Create(ItemAsset asset, Item item, StackUnitKind unitKind, AmountRole amountRole,
		QualityPolicy qualityPolicy, StatePolicy statePolicy, WithdrawPolicy withdrawPolicy, DisplayPolicy displayPolicy)
	{
		return new ItemStorageDescriptor
		{
			AssetId = item.id, AssetGuid = asset.GUID.ToString("D"), UnitKind = unitKind, AmountRole = amountRole,
			QualityPolicy = qualityPolicy, StatePolicy = statePolicy, WithdrawPolicy = withdrawPolicy, DisplayPolicy = displayPolicy,
			NativeAmount = item.amount, NativeQuality = qualityPolicy == QualityPolicy.Exact ? item.quality : (byte)0,
			NativeState = statePolicy == StatePolicy.ExactBytes ? CloneState(item.state) : Array.Empty<byte>(), NativeMaxStack = asset.amount
		};
	}

	private static NativeItemPayload CreateNativePayload(VirtualStorageEntry entry)
	{
		return new NativeItemPayload(entry.AssetId, entry.NativeAmount == 0 ? (byte)1 : entry.NativeAmount, entry.NativeQuality, entry.NativeState);
	}
}
