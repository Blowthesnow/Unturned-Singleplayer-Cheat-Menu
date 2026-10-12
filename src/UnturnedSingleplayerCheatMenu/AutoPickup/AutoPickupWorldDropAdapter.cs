using System;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal static class AutoPickupWorldDropAdapter
{
	internal static bool RemovePersistedDrop(byte x, byte y, uint instanceId,
		ItemDrop expectedDrop, ushort itemId, byte amount, byte quality, byte[] state)
	{
		if (!Provider.isServer || ItemManager.regions == null || !Regions.checkSafe(x, y) ||
			expectedDrop == null || expectedDrop.instanceID != instanceId || expectedDrop.model == null)
			return false;
		ItemRegion region = ItemManager.regions[x, y];
		if (region?.items == null || region.drops == null) return false;
		ItemDrop[] matchingDrops = region.drops.Where(drop => drop.instanceID == instanceId).ToArray();
		if (matchingDrops.Length != 1 || !ReferenceEquals(matchingDrops[0], expectedDrop) ||
			!Matches(expectedDrop.interactableItem?.item, itemId, amount, quality, state)) return false;
		int dataIndex = -1;
		for (int i = 0; i < region.items.Count; i++)
		{
			if (region.items[i].instanceID != instanceId) continue;
			if (dataIndex >= 0) return false;
			dataIndex = i;
		}
		if (dataIndex < 0) return false;
		ItemData data = region.items[dataIndex];
		if (!Matches(data.item, itemId, amount, quality, state)) return false;

		// ReceiveDestroyItem only removes the client model/drop. Match native pickup's
		// authoritative-data-first order so respawn count and point occupancy are released.
		region.items.RemoveAt(dataIndex);
		try
		{
			ItemManager.ReceiveDestroyItem(x, y, instanceId, true);
		}
		catch (Exception)
		{
			if (IsRemoved(region, instanceId)) return true;
			RestoreDataIfDropRemains(region, data, dataIndex);
			throw;
		}
		if (IsRemoved(region, instanceId)) return true;
		RestoreDataIfDropRemains(region, data, dataIndex);
		return false;
	}

	private static bool Matches(Item item, ushort itemId, byte amount, byte quality, byte[] state) =>
		item != null && item.id == itemId && item.amount == amount && item.quality == quality &&
		(item.state ?? Array.Empty<byte>()).SequenceEqual(state ?? Array.Empty<byte>());

	private static bool IsRemoved(ItemRegion region, uint instanceId) =>
		!region.items.Any(item => item.instanceID == instanceId) &&
		!region.drops.Any(drop => drop.instanceID == instanceId);

	private static void RestoreDataIfDropRemains(ItemRegion region, ItemData data, int index)
	{
		if (region.drops.Any(drop => drop.instanceID == data.instanceID) &&
			!region.items.Any(item => item.instanceID == data.instanceID))
			region.items.Insert(Math.Min(index, region.items.Count), data);
	}
}
