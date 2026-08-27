using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class InventoryBridge
{
	private readonly VirtualStorageService _storage;

	internal InventoryBridge(VirtualStorageService storage)
	{
		_storage = storage ?? throw new ArgumentNullException(nameof(storage));
	}

	internal int StoreAllCarried(PlayerInventory inventory, Func<bool> persist, VirtualStorageDocument before = null)
	{
		if (inventory?.items == null || persist == null)
		{
			return 0;
		}
		List<CapturedItem> captured = Capture(inventory);
		if (captured.Count == 0)
		{
			return 0;
		}
		before ??= _storage.Snapshot(string.Empty, string.Empty);
		foreach (CapturedItem value in captured)
		{
			ItemAsset asset = Assets.find(EAssetType.ITEM, value.Item.id) as ItemAsset;
			if (asset == null || !_storage.Add(ItemInstanceSemantics.Describe(asset, value.Item)).Success)
			{
				_storage.RestoreSnapshot(before);
				return 0;
			}
		}
		int removed = 0;
		for (int i = captured.Count - 1; i >= 0; i--)
		{
			CapturedItem value = captured[i];
			byte page = value.Page;
			byte index = value.Index;
			if (page >= inventory.items.Length || inventory.items[page] == null || index >= inventory.items[page].getItemCount())
			{
				_storage.RestoreSnapshot(before);
				Restore(inventory, captured);
				return 0;
			}
			inventory.removeItem(page, index);
			removed++;
		}
		if (!persist())
		{
			_storage.RestoreSnapshot(before);
			if (!Restore(inventory, captured)) return 0;
			persist();
			return 0;
		}
		return removed;
	}

	internal static List<CapturedItem> Capture(PlayerInventory inventory)
	{
		List<CapturedItem> captured = new List<CapturedItem>();
		if (inventory?.items == null) return captured;
		byte[] pages = { PlayerInventory.SLOTS, PlayerInventory.BACKPACK, PlayerInventory.VEST, PlayerInventory.SHIRT, PlayerInventory.PANTS };
		foreach (byte page in pages)
		{
			if (page >= inventory.items.Length || inventory.items[page] == null) continue;
			Items items = inventory.items[page];
			for (int index = 0; index < items.getItemCount(); index++)
			{
				ItemJar jar = items.getItem((byte)index);
				if (jar?.item == null) continue;
				Item item = jar.item;
				captured.Add(new CapturedItem(page, (byte)index, jar.x, jar.y, jar.rot,
					new Item(item.id, item.amount, item.quality, CloneState(item.state))));
			}
		}
		return captured;
	}

	internal static bool Restore(PlayerInventory inventory, IReadOnlyList<CapturedItem> captured)
	{
		if (inventory?.items == null || captured == null) return false;
		HashSet<byte> pages = new HashSet<byte>(captured.Select(value => value.Page));
		foreach (byte page in pages)
		{
			if (page >= inventory.items.Length || inventory.items[page] == null) return false;
			Items items = inventory.items[page];
			for (int index = items.getItemCount() - 1; index >= 0; index--) inventory.removeItem(page, (byte)index);
		}
		foreach (CapturedItem value in captured.OrderBy(item => item.Index))
		{
			Item item = new Item(value.Item.id, value.Item.amount, value.Item.quality, CloneState(value.Item.state));
			if (!inventory.tryAddItem(item, value.X, value.Y, value.Page, value.Rotation)) return false;
		}
		return true;
	}

	internal sealed class CapturedItem
	{
		internal readonly byte Page;
		internal readonly byte Index;
		internal readonly byte X;
		internal readonly byte Y;
		internal readonly byte Rotation;
		internal readonly Item Item;

		internal CapturedItem(byte page, byte index, byte x, byte y, byte rotation, Item item)
		{
			Page = page;
			Index = index;
			X = x;
			Y = y;
			Rotation = rotation;
			Item = item;
		}
	}

	private static byte[] CloneState(byte[] state)
	{
		return state == null || state.Length == 0 ? Array.Empty<byte>() : (byte[])state.Clone();
	}
}
