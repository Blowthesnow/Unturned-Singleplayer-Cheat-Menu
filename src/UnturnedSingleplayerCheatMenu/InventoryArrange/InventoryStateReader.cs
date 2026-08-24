using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryStateReader
{
	private static readonly Dictionary<string, string> AmmoGroupsByAssetGuid = new Dictionary<string, string>(StringComparer.Ordinal);

	internal static void RefreshAmmoCompatibility()
	{
		List<ItemAsset> list = new List<ItemAsset>();
		Assets.find(list);
		Dictionary<string, ItemMagazineAsset> dictionary = list.OfType<ItemMagazineAsset>().GroupBy<ItemMagazineAsset, string>((ItemMagazineAsset asset) => asset.GUID.ToString("N"), StringComparer.Ordinal).ToDictionary<IGrouping<string, ItemMagazineAsset>, string, ItemMagazineAsset>((IGrouping<string, ItemMagazineAsset> group) => group.Key, (IGrouping<string, ItemMagazineAsset> group) => group.First(), StringComparer.Ordinal);
		Dictionary<string, HashSet<string>> dictionary2 = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		Dictionary<string, string> dictionary3 = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (ItemMagazineAsset value in dictionary.Values)
		{
			string magazineLocalGroup = GetMagazineLocalGroup(value);
			string text = "mag:" + magazineLocalGroup;
			EnsureNode(dictionary2, text);
			dictionary3[value.GUID.ToString("N")] = text;
			if (value.blueprints == null)
			{
				continue;
			}
			foreach (Blueprint blueprint in value.blueprints)
			{
				if (blueprint == null || blueprint.Operation != EBlueprintOperation.FillTargetItem || blueprint.supplies == null)
				{
					continue;
				}
				BlueprintSupply[] supplies = blueprint.supplies;
				for (int num = 0; num < supplies.Length; num++)
				{
					ItemAsset itemAsset = supplies[num]?.FindItemAsset();
					if (itemAsset is ItemSupplyAsset)
					{
						string text2 = "supply:" + itemAsset.GUID.ToString("N");
						EnsureNode(dictionary2, text2);
						dictionary2[text].Add(text2);
						dictionary2[text2].Add(text);
					}
				}
			}
		}
		AmmoGroupsByAssetGuid.Clear();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (string item in dictionary2.Keys.OrderBy<string, string>((string value) => value, StringComparer.Ordinal))
		{
			if (!hashSet.Add(item))
			{
				continue;
			}
			List<string> list2 = new List<string>();
			Queue<string> queue = new Queue<string>();
			queue.Enqueue(item);
			while (queue.Count > 0)
			{
				string text3 = queue.Dequeue();
				list2.Add(text3);
				foreach (string item2 in dictionary2[text3].OrderBy<string, string>((string value) => value, StringComparer.Ordinal))
				{
					if (hashSet.Add(item2))
					{
						queue.Enqueue(item2);
					}
				}
			}
			string text4 = "ammo:" + string.Join("|", list2.Where((string node) => node.StartsWith("mag:", StringComparison.Ordinal)).OrderBy<string, string>((string node) => node, StringComparer.Ordinal));
			if (text4 == "ammo:")
			{
				continue;
			}
			foreach (string item3 in list2)
			{
				if (item3.StartsWith("mag:", StringComparison.Ordinal))
				{
					foreach (KeyValuePair<string, string> item4 in dictionary3)
					{
						if (string.Equals(item4.Value, item3, StringComparison.Ordinal))
						{
							AmmoGroupsByAssetGuid[item4.Key] = text4;
						}
					}
				}
				else if (item3.StartsWith("supply:", StringComparison.Ordinal))
				{
					AmmoGroupsByAssetGuid[item3.Substring("supply:".Length)] = text4;
				}
			}
		}
	}

	internal static bool TryReadAmmoCount(ItemAsset asset, Item item, out int ammoCount)
	{
		ammoCount = -1;
		if (asset is ItemMagazineAsset || IsMappedAmmoSupply(asset))
		{
			ammoCount = item?.amount ?? 0;
			return item != null;
		}
		if (!(asset is ItemGunAsset) || item?.state == null || item.state.Length <= 10)
		{
			return false;
		}
		ammoCount = item.state[10];
		return true;
	}

	internal static bool TryReadAmmoContainerMetadata(ItemAsset asset, out bool isMagazine, out int capacity, out string group)
	{
		isMagazine = false;
		capacity = -1;
		group = string.Empty;
		if (asset is ItemMagazineAsset itemMagazineAsset)
		{
			isMagazine = true;
			capacity = Math.Max(1, itemMagazineAsset.MaxAmount);
			string key = itemMagazineAsset.GUID.ToString("N");
			group = (AmmoGroupsByAssetGuid.TryGetValue(key, out var value) ? value : ("ammo:mag:" + GetMagazineLocalGroup(itemMagazineAsset)));
			return true;
		}
		if (!(asset is ItemSupplyAsset) || !AmmoGroupsByAssetGuid.TryGetValue(asset.GUID.ToString("N"), out var value2))
		{
			return false;
		}
		capacity = Math.Max(1, asset.MaxAmount);
		group = value2;
		return true;
	}

	private static bool IsMappedAmmoSupply(ItemAsset asset)
	{
		if (asset is ItemSupplyAsset)
		{
			return AmmoGroupsByAssetGuid.ContainsKey(asset.GUID.ToString("N"));
		}
		return false;
	}

	private static string GetMagazineLocalGroup(ItemMagazineAsset magazine)
	{
		ushort[] calibers = magazine.calibers;
		if (calibers != null && calibers.Length != 0)
		{
			return "caliber:" + string.Join(",", calibers.OrderBy((ushort value) => value));
		}
		return "asset:" + magazine.GUID.ToString("N");
	}

	private static void EnsureNode(Dictionary<string, HashSet<string>> adjacency, string node)
	{
		if (!adjacency.ContainsKey(node))
		{
			adjacency[node] = new HashSet<string>(StringComparer.Ordinal);
		}
	}
}
