using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;

internal sealed class SnapshotInventoryCheckpoint
{
	internal SnapshotInventoryCheckpoint(IReadOnlyList<SnapshotInventoryBridge.CapturedItem> items,
		SnapshotInventoryBridge.CapturedClothing clothing,
		SnapshotInventoryBridge.CapturedEquipmentSelection equipmentSelection)
	{
		Items = items ?? Array.Empty<SnapshotInventoryBridge.CapturedItem>();
		Clothing = clothing;
		EquipmentSelection = equipmentSelection;
	}

	internal IReadOnlyList<SnapshotInventoryBridge.CapturedItem> Items { get; }
	internal SnapshotInventoryBridge.CapturedClothing Clothing { get; }
	internal SnapshotInventoryBridge.CapturedEquipmentSelection EquipmentSelection { get; }
}

internal static class SnapshotInventoryBridge
{
	private static readonly byte[] CarriedPages =
	{
		(byte)0, (byte)1, PlayerInventory.SLOTS, PlayerInventory.BACKPACK,
		PlayerInventory.VEST, PlayerInventory.SHIRT, PlayerInventory.PANTS
	};

	internal static SnapshotInventoryCheckpoint Capture(PlayerInventory inventory)
	{
		Player player = Player.LocalPlayer;
		if (inventory?.items == null || player?.inventory != inventory ||
			player.clothing == null || player.equipment == null)
			throw new InvalidOperationException("A complete local-player inventory is required.");
		List<CapturedItem> captured = new List<CapturedItem>();
		foreach (byte page in CarriedPages)
		{
			if (page >= inventory.items.Length || inventory.items[page] == null)
				throw new InvalidOperationException("A required carried inventory page is unavailable: " + page);
			Items items = inventory.items[page];
			for (int index = 0; index < items.getItemCount(); index++)
			{
				ItemJar jar = items.getItem((byte)index);
				if (jar?.item == null) continue;
				captured.Add(new CapturedItem(page, (byte)index, jar.x, jar.y, jar.rot, Clone(jar.item)));
			}
		}
		return new SnapshotInventoryCheckpoint(captured,
			CapturedClothing.Capture(player.clothing),
			CapturedEquipmentSelection.Capture(player.equipment));
	}

	internal static bool Matches(PlayerInventory inventory, CapturedItem captured)
	{
		if (inventory?.items == null || captured == null || captured.Page >= inventory.items.Length ||
			inventory.items[captured.Page] == null || captured.Index >= inventory.items[captured.Page].getItemCount())
			return false;
		Item current = inventory.items[captured.Page].getItem(captured.Index)?.item;
		return current != null && current.id == captured.Item.id && current.amount == captured.Item.amount &&
			current.quality == captured.Item.quality && CloneState(current.state).SequenceEqual(captured.Item.state);
	}

	internal static bool Restore(PlayerInventory inventory, SnapshotInventoryCheckpoint checkpoint)
	{
		Player player = Player.LocalPlayer;
		if (inventory?.items == null || checkpoint == null || player?.inventory != inventory ||
			player.clothing == null || player.equipment == null) return false;
		SnapshotNativeRollbackResult result = SnapshotNativeRollbackCoordinator.Execute(
			() => checkpoint.EquipmentSelection.Prepare(player.equipment),
			() => checkpoint.Clothing.Restore(player.clothing),
			CarriedPages,
			page => ClearPage(inventory, player.equipment, page),
			() => RestoreItems(inventory, checkpoint.Items),
			() => checkpoint.EquipmentSelection.Restore(player.equipment));
		return result.Success;
	}

	private static bool ClearPage(PlayerInventory inventory, PlayerEquipment equipment, byte page)
	{
		if (page >= inventory.items.Length || inventory.items[page] == null) return false;
		Items items = inventory.items[page];
		for (int index = items.getItemCount() - 1; index >= 0; index--) inventory.removeItem(page, (byte)index);
		if (page < PlayerInventory.SLOTS) equipment.sendSlot(page);
		return items.getItemCount() == 0;
	}

	private static bool RestoreItems(PlayerInventory inventory,
		IReadOnlyList<CapturedItem> captured)
	{
		bool success = true;
		foreach (CapturedItem value in captured ?? Array.Empty<CapturedItem>())
		{
			if (!inventory.tryAddItem(Clone(value.Item), value.X, value.Y, value.Page, value.Rotation))
				success = false;
		}
		return success;
	}

	internal static Item Clone(Item item) => item == null
		? null
		: new Item(item.id, item.amount, item.quality, CloneState(item.state));

	internal static byte[] CloneState(byte[] state) =>
		state == null || state.Length == 0 ? Array.Empty<byte>() : (byte[])state.Clone();

	internal sealed class CapturedItem
	{
		internal CapturedItem(byte page, byte index, byte x, byte y, byte rotation, Item item)
		{
			Page = page;
			Index = index;
			X = x;
			Y = y;
			Rotation = rotation;
			Item = Clone(item);
		}

		internal byte Page { get; }
		internal byte Index { get; }
		internal byte X { get; }
		internal byte Y { get; }
		internal byte Rotation { get; }
		internal Item Item { get; }
	}

	internal sealed class CapturedEquipmentSelection
	{
		private CapturedEquipmentSelection(bool wasEquipped, byte page, byte x, byte y)
		{
			WasEquipped = wasEquipped;
			Page = page;
			X = x;
			Y = y;
		}

		private bool WasEquipped { get; }
		private byte Page { get; }
		private byte X { get; }
		private byte Y { get; }

		internal static CapturedEquipmentSelection Capture(PlayerEquipment equipment)
		{
			byte page = equipment?.equippedPage ?? byte.MaxValue;
			return new CapturedEquipmentSelection(page != byte.MaxValue, page,
				equipment?.equipped_x ?? byte.MaxValue, equipment?.equipped_y ?? byte.MaxValue);
		}

		internal bool Prepare(PlayerEquipment equipment)
		{
			if (equipment == null) return false;
			if (equipment.equippedPage == byte.MaxValue) return true;
			equipment.dequip();
			return equipment.equippedPage == byte.MaxValue;
		}

		internal bool Restore(PlayerEquipment equipment)
		{
			if (equipment == null) return false;
			if (!WasEquipped) return equipment.equippedPage == byte.MaxValue;
			equipment.ServerEquip(Page, X, Y);
			return equipment.checkSelection(Page, X, Y);
		}
	}

	internal sealed class CapturedClothing
	{
		private CapturedClothing(ClothingItem shirt, ClothingItem pants, ClothingItem hat,
			ClothingItem backpack, ClothingItem vest, ClothingItem mask, ClothingItem glasses)
		{
			Shirt = shirt;
			Pants = pants;
			Hat = hat;
			Backpack = backpack;
			Vest = vest;
			Mask = mask;
			Glasses = glasses;
		}

		private ClothingItem Shirt { get; }
		private ClothingItem Pants { get; }
		private ClothingItem Hat { get; }
		private ClothingItem Backpack { get; }
		private ClothingItem Vest { get; }
		private ClothingItem Mask { get; }
		private ClothingItem Glasses { get; }

		internal static CapturedClothing Capture(PlayerClothing clothing) => new CapturedClothing(
			new ClothingItem(clothing.shirt, clothing.shirtAsset?.GUID ?? Guid.Empty,
				clothing.shirtQuality, clothing.shirtState),
			new ClothingItem(clothing.pants, clothing.pantsAsset?.GUID ?? Guid.Empty,
				clothing.pantsQuality, clothing.pantsState),
			new ClothingItem(clothing.hat, clothing.hatAsset?.GUID ?? Guid.Empty,
				clothing.hatQuality, clothing.hatState),
			new ClothingItem(clothing.backpack, clothing.backpackAsset?.GUID ?? Guid.Empty,
				clothing.backpackQuality, clothing.backpackState),
			new ClothingItem(clothing.vest, clothing.vestAsset?.GUID ?? Guid.Empty,
				clothing.vestQuality, clothing.vestState),
			new ClothingItem(clothing.mask, clothing.maskAsset?.GUID ?? Guid.Empty,
				clothing.maskQuality, clothing.maskState),
			new ClothingItem(clothing.glasses, clothing.glassesAsset?.GUID ?? Guid.Empty,
				clothing.glassesQuality, clothing.glassesState));

		internal bool Restore(PlayerClothing clothing)
		{
			if (clothing == null) return false;
			RestoreIfChanged(Shirt, clothing.shirt, clothing.shirtQuality, clothing.shirtState,
				clothing.ReceiveWearShirt);
			RestoreIfChanged(Pants, clothing.pants, clothing.pantsQuality, clothing.pantsState,
				clothing.ReceiveWearPants);
			RestoreIfChanged(Hat, clothing.hat, clothing.hatQuality, clothing.hatState,
				clothing.ReceiveWearHat);
			RestoreIfChanged(Backpack, clothing.backpack, clothing.backpackQuality, clothing.backpackState,
				clothing.ReceiveWearBackpack);
			RestoreIfChanged(Vest, clothing.vest, clothing.vestQuality, clothing.vestState,
				clothing.ReceiveWearVest);
			RestoreIfChanged(Mask, clothing.mask, clothing.maskQuality, clothing.maskState,
				clothing.ReceiveWearMask);
			RestoreIfChanged(Glasses, clothing.glasses, clothing.glassesQuality, clothing.glassesState,
				clothing.ReceiveWearGlasses);
			return Shirt.Matches(clothing.shirt, clothing.shirtQuality, clothing.shirtState) &&
				Pants.Matches(clothing.pants, clothing.pantsQuality, clothing.pantsState) &&
				Hat.Matches(clothing.hat, clothing.hatQuality, clothing.hatState) &&
				Backpack.Matches(clothing.backpack, clothing.backpackQuality, clothing.backpackState) &&
				Vest.Matches(clothing.vest, clothing.vestQuality, clothing.vestState) &&
				Mask.Matches(clothing.mask, clothing.maskQuality, clothing.maskState) &&
				Glasses.Matches(clothing.glasses, clothing.glassesQuality, clothing.glassesState);
		}

		private static void RestoreIfChanged(ClothingItem expected, ushort id, byte quality,
			byte[] state, Action<Guid, byte, byte[], bool> receive)
		{
			if (!expected.Matches(id, quality, state))
				receive(expected.AssetGuid, expected.Quality, expected.State, false);
		}
	}

	private sealed class ClothingItem
	{
		private readonly byte[] _state;

		internal ClothingItem(ushort id, Guid assetGuid, byte quality, byte[] state)
		{
			Id = id;
			AssetGuid = assetGuid;
			Quality = quality;
			_state = CloneState(state);
		}

		internal ushort Id { get; }
		internal Guid AssetGuid { get; }
		internal byte Quality { get; }
		internal byte[] State => CloneState(_state);

		internal bool Matches(ushort id, byte quality, byte[] state) =>
			Id == id && Quality == quality && _state.SequenceEqual(CloneState(state));
	}
}
