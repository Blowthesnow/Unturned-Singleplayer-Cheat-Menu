using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class AmmoCompatibilityIndex
{
    private readonly Dictionary<Guid, IReadOnlyList<ItemAsset>> _byGunGuid = new Dictionary<Guid, IReadOnlyList<ItemAsset>>();
    private readonly HashSet<Guid> _ammoContainerGuids = new HashSet<Guid>();

    public void Rebuild(IReadOnlyList<ItemAsset> items)
    {
        _byGunGuid.Clear();
        _ammoContainerGuids.Clear();
        UnturnedSingleplayerCheatMenu.InfiniteStorage.ItemInstanceSemantics.SetAmmoContainerGuids(_ammoContainerGuids);
        if (items == null)
        {
            return;
        }

        List<ItemMagazineAsset> magazines = items.OfType<ItemMagazineAsset>().ToList();
        foreach (ItemMagazineAsset magazine in magazines)
        {
            _ammoContainerGuids.Add(magazine.GUID);
            foreach (Blueprint blueprint in magazine.blueprints ?? new List<Blueprint>())
            {
                if (blueprint == null || blueprint.Operation != EBlueprintOperation.FillTargetItem || blueprint.supplies == null)
                {
                    continue;
                }
                foreach (BlueprintSupply supply in blueprint.supplies)
                {
                    if (supply?.FindItemAsset() is ItemSupplyAsset ammoSupply)
                    {
                        _ammoContainerGuids.Add(ammoSupply.GUID);
                    }
                }
                foreach (BlueprintOutput output in blueprint.outputs ?? Array.Empty<BlueprintOutput>())
                {
                    if (output?.FindItemAsset() is ItemSupplyAsset ammoSupply)
                    {
                        _ammoContainerGuids.Add(ammoSupply.GUID);
                    }
                }
            }
        }
        foreach (ItemGunAsset gun in items.OfType<ItemGunAsset>())
        {
            HashSet<ushort> gunCalibers = new HashSet<ushort>(gun.magazineCalibers ?? new ushort[0]);
            HashSet<ItemAsset> matches = new HashSet<ItemAsset>();
            foreach (ItemMagazineAsset magazine in magazines)
            {
                if (magazine.calibers == null || !magazine.calibers.Any(gunCalibers.Contains))
                {
                    continue;
                }
                foreach (Blueprint blueprint in magazine.blueprints ?? new List<Blueprint>())
                {
                    if (blueprint == null || blueprint.Operation != EBlueprintOperation.FillTargetItem)
                    {
                        continue;
                    }
                    AddSupplies(matches, blueprint.supplies, magazine);
                    AddOutputs(matches, blueprint.outputs, magazine);
                }
            }
            _byGunGuid[gun.GUID] = matches
                .Where(item => item != null && !(item is ItemMagazineAsset))
                .OrderBy(item => item.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        UnturnedSingleplayerCheatMenu.InfiniteStorage.ItemInstanceSemantics.SetAmmoContainerGuids(_ammoContainerGuids);
    }

    public IReadOnlyList<ItemAsset> GetCompatibleAmmo(ItemGunAsset gun)
    {
        return gun != null && _byGunGuid.TryGetValue(gun.GUID, out IReadOnlyList<ItemAsset> result)
            ? result
            : Array.Empty<ItemAsset>();
    }

    public bool IsAmmoContainer(ItemAsset asset)
    {
        return asset != null && _ammoContainerGuids.Contains(asset.GUID);
    }

    private static void AddSupplies(ISet<ItemAsset> matches, IEnumerable<BlueprintSupply> supplies, ItemAsset magazine)
    {
        if (supplies == null) return;
        foreach (BlueprintSupply supply in supplies)
        {
            ItemAsset item = supply?.FindItemAsset();
            if (item != null && item != magazine && !(item is ItemMagazineAsset)) matches.Add(item);
        }
    }

    private static void AddOutputs(ISet<ItemAsset> matches, IEnumerable<BlueprintOutput> outputs, ItemAsset magazine)
    {
        if (outputs == null) return;
        foreach (BlueprintOutput output in outputs)
        {
            ItemAsset item = output?.FindItemAsset();
            if (item != null && item != magazine && !(item is ItemMagazineAsset)) matches.Add(item);
        }
    }
}
