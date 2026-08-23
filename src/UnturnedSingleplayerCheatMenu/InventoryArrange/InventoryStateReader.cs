using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventoryStateReader
{
    private static readonly Dictionary<string, string> AmmoGroupsByAssetGuid = new(StringComparer.Ordinal);

    internal static void RefreshAmmoCompatibility()
    {
        List<ItemAsset> assets = new();
        Assets.find(assets);

        Dictionary<string, ItemMagazineAsset> magazinesByGuid = assets
            .OfType<ItemMagazineAsset>()
            .GroupBy(asset => asset.GUID.ToString("N"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        Dictionary<string, HashSet<string>> adjacency = new(StringComparer.Ordinal);
        Dictionary<string, string> magazineGroupByGuid = new(StringComparer.Ordinal);

        foreach (ItemMagazineAsset magazine in magazinesByGuid.Values)
        {
            string localGroup = GetMagazineLocalGroup(magazine);
            string magazineNode = "mag:" + localGroup;
            EnsureNode(adjacency, magazineNode);
            magazineGroupByGuid[magazine.GUID.ToString("N")] = magazineNode;

            if (magazine.blueprints == null)
                continue;

            foreach (Blueprint blueprint in magazine.blueprints)
            {
                if (blueprint == null || blueprint.Operation != EBlueprintOperation.FillTargetItem || blueprint.supplies == null)
                    continue;

                foreach (BlueprintSupply supply in blueprint.supplies)
                {
                    ItemAsset source = supply?.FindItemAsset();
                    if (source is not ItemSupplyAsset)
                        continue;

                    string supplyNode = "supply:" + source.GUID.ToString("N");
                    EnsureNode(adjacency, supplyNode);
                    adjacency[magazineNode].Add(supplyNode);
                    adjacency[supplyNode].Add(magazineNode);
                }
            }
        }

        AmmoGroupsByAssetGuid.Clear();
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (string start in adjacency.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!visited.Add(start))
                continue;

            List<string> component = new();
            Queue<string> pending = new();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                string node = pending.Dequeue();
                component.Add(node);
                foreach (string next in adjacency[node].OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (visited.Add(next))
                        pending.Enqueue(next);
                }
            }

            string componentKey = "ammo:" + string.Join("|", component
                .Where(node => node.StartsWith("mag:", StringComparison.Ordinal))
                .OrderBy(node => node, StringComparer.Ordinal));
            if (componentKey == "ammo:")
                continue;

            foreach (string node in component)
            {
                if (node.StartsWith("mag:", StringComparison.Ordinal))
                {
                    foreach (KeyValuePair<string, string> pair in magazineGroupByGuid)
                    {
                        if (string.Equals(pair.Value, node, StringComparison.Ordinal))
                            AmmoGroupsByAssetGuid[pair.Key] = componentKey;
                    }
                }
                else if (node.StartsWith("supply:", StringComparison.Ordinal))
                {
                    AmmoGroupsByAssetGuid[node.Substring("supply:".Length)] = componentKey;
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
        if (asset is not ItemGunAsset || item?.state == null || item.state.Length <= 10)
            return false;

        ammoCount = item.state[10];
        return true;
    }

    internal static bool TryReadAmmoContainerMetadata(ItemAsset asset, out bool isMagazine, out int capacity, out string group)
    {
        isMagazine = false;
        capacity = -1;
        group = string.Empty;
        if (asset is ItemMagazineAsset magazine)
        {
            isMagazine = true;
            capacity = Math.Max(1, magazine.MaxAmount);
            string guid = magazine.GUID.ToString("N");
            group = AmmoGroupsByAssetGuid.TryGetValue(guid, out string mappedGroup)
                ? mappedGroup
                : "ammo:mag:" + GetMagazineLocalGroup(magazine);
            return true;
        }

        if (asset is not ItemSupplyAsset || !AmmoGroupsByAssetGuid.TryGetValue(asset.GUID.ToString("N"), out string supplyGroup))
            return false;

        capacity = Math.Max(1, asset.MaxAmount);
        group = supplyGroup;
        return true;
    }

    private static bool IsMappedAmmoSupply(ItemAsset asset)
    {
        return asset is ItemSupplyAsset
            && AmmoGroupsByAssetGuid.ContainsKey(asset.GUID.ToString("N"));
    }

    private static string GetMagazineLocalGroup(ItemMagazineAsset magazine)
    {
        ushort[] calibers = magazine.calibers;
        return calibers == null || calibers.Length == 0
            ? "asset:" + magazine.GUID.ToString("N")
            : "caliber:" + string.Join(",", calibers.OrderBy(value => value));
    }

    private static void EnsureNode(Dictionary<string, HashSet<string>> adjacency, string node)
    {
        if (!adjacency.ContainsKey(node))
            adjacency[node] = new HashSet<string>(StringComparer.Ordinal);
    }
}