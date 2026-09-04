using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal readonly struct InventoryItemAddress : IEquatable<InventoryItemAddress>
{
	internal InventoryItemAddress(byte page, byte x, byte y)
	{
		Page = page;
		X = x;
		Y = y;
	}

	internal byte Page { get; }
	internal byte X { get; }
	internal byte Y { get; }

	public bool Equals(InventoryItemAddress other) => Page == other.Page && X == other.X && Y == other.Y;
	public override bool Equals(object obj) => obj is InventoryItemAddress other && Equals(other);
	public override int GetHashCode() => ((Page * 397) ^ X) * 397 ^ Y;
	public static bool operator ==(InventoryItemAddress left, InventoryItemAddress right) => left.Equals(right);
	public static bool operator !=(InventoryItemAddress left, InventoryItemAddress right) => !left.Equals(right);
}

internal readonly struct InventoryHotkeyBindingState
{
	internal InventoryHotkeyBindingState(int hotkeyIndex, ushort assetId, InventoryItemAddress address)
	{
		HotkeyIndex = hotkeyIndex;
		AssetId = assetId;
		Address = address;
	}

	internal int HotkeyIndex { get; }
	internal ushort AssetId { get; }
	internal InventoryItemAddress Address { get; }

	internal InventoryHotkeyBindingState WithAddress(InventoryItemAddress address) =>
		new InventoryHotkeyBindingState(HotkeyIndex, AssetId, address);
}

internal readonly struct InventoryItemMove
{
	internal InventoryItemMove(ushort assetId, InventoryItemAddress source, InventoryItemAddress destination)
	{
		AssetId = assetId;
		Source = source;
		Destination = destination;
	}

	internal ushort AssetId { get; }
	internal InventoryItemAddress Source { get; }
	internal InventoryItemAddress Destination { get; }
}

internal static class InventoryHotkeyBindingPolicy
{
	private readonly struct MoveKey : IEquatable<MoveKey>
	{
		internal MoveKey(ushort assetId, InventoryItemAddress source)
		{
			AssetId = assetId;
			Source = source;
		}

		private ushort AssetId { get; }
		private InventoryItemAddress Source { get; }

		public bool Equals(MoveKey other) => AssetId == other.AssetId && Source == other.Source;
		public override bool Equals(object obj) => obj is MoveKey other && Equals(other);
		public override int GetHashCode() => (AssetId * 397) ^ Source.GetHashCode();
	}

	internal static IReadOnlyList<InventoryHotkeyBindingState> Remap(
		IEnumerable<InventoryHotkeyBindingState> bindings,
		IEnumerable<InventoryItemMove> moves)
	{
		Dictionary<MoveKey, InventoryItemAddress> destinations = new Dictionary<MoveKey, InventoryItemAddress>();
		if (moves != null)
		{
			foreach (InventoryItemMove move in moves)
			{
				destinations[new MoveKey(move.AssetId, move.Source)] = move.Destination;
			}
		}

		List<InventoryHotkeyBindingState> result = new List<InventoryHotkeyBindingState>();
		if (bindings == null) return result;
		foreach (InventoryHotkeyBindingState binding in bindings)
		{
			result.Add(destinations.TryGetValue(new MoveKey(binding.AssetId, binding.Address), out InventoryItemAddress destination)
				? binding.WithAddress(destination)
				: binding);
		}
		return result;
	}
}

internal static class InventoryArrangeExecutionPolicy
{
	internal static bool RequiresInventoryRebuild(InventoryArrangePlan plan, Func<string, int> originalAmountResolver)
	{
		if (plan == null || !plan.IsSuccess || originalAmountResolver == null) return true;
		if (plan.MoveCount != 0 || plan.MergeCount != 0 || plan.InputItemCount != plan.OutputItemCount) return true;
		foreach (InventoryArrangePlacement placement in plan.Placements)
		{
			if (placement?.Item == null || originalAmountResolver(placement.Item.ItemKey) != placement.Item.Amount) return true;
		}
		return false;
	}

	internal static bool RequiresHotkeyDisplayRefresh(bool inventoryWasRebuilt, int capturedBindingCount) =>
		inventoryWasRebuilt && capturedBindingCount > 0;
}
