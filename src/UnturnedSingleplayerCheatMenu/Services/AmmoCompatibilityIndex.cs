using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.ItemStacking;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class AmmoCompatibilityIndex
{
    private readonly Dictionary<Guid, IReadOnlyList<ItemAsset>> _byGunGuid = new Dictionary<Guid, IReadOnlyList<ItemAsset>>();
    private readonly HashSet<Guid> _ammoRelatedGuids = new HashSet<Guid>();
    private readonly HashSet<Guid> _directSourceGuids = new HashSet<Guid>();
    private readonly HashSet<Guid> _containerTargetGuids = new HashSet<Guid>();
    private readonly Dictionary<string, ItemAsset> _itemsByGuid = new Dictionary<string, ItemAsset>(StringComparer.Ordinal);
    private AmmoCompatibilityCatalog _catalog = AmmoCompatibilityCatalog.Empty;

    public void Rebuild(IReadOnlyList<ItemAsset> items)
    {
        _byGunGuid.Clear();
        _ammoRelatedGuids.Clear();
        _directSourceGuids.Clear();
        _containerTargetGuids.Clear();
        _itemsByGuid.Clear();
        _catalog = AmmoCompatibilityCatalog.Empty;
        if (items == null)
        {
            return;
        }

        _catalog = UnturnedAmmoCompatibilityReader.BuildCatalog(items);
        foreach (ItemAsset item in items.Where(value => value != null))
        {
            _itemsByGuid[AmmoRefillIdentity.NormalizeAssetGuid(item.GUID.ToString("N"))] = item;
        }
        foreach (string assetGuid in _catalog.AmmoRelatedAssetGuids)
        {
            if (Guid.TryParseExact(assetGuid, "N", out Guid guid)) _ammoRelatedGuids.Add(guid);
        }
        foreach (string assetGuid in _catalog.DirectSourceAssetGuids)
        {
            if (Guid.TryParseExact(assetGuid, "N", out Guid guid)) _directSourceGuids.Add(guid);
        }
        foreach (string assetGuid in _catalog.ContainerAssetGuids)
        {
            if (Guid.TryParseExact(assetGuid, "N", out Guid guid)) _containerTargetGuids.Add(guid);
        }
        List<ItemMagazineAsset> magazines = items.OfType<ItemMagazineAsset>().ToList();
        foreach (ItemGunAsset gun in items.OfType<ItemGunAsset>())
        {
            HashSet<ushort> gunCalibers = new HashSet<ushort>(gun.magazineCalibers ?? new ushort[0]);
            HashSet<string> compatibleMagazineGuids = new HashSet<string>(magazines
                .Where(magazine => magazine.calibers != null && magazine.calibers.Any(gunCalibers.Contains))
                .Select(magazine => magazine.GUID.ToString("N"))
                .Select(AmmoRefillIdentity.NormalizeAssetGuid), StringComparer.Ordinal);
            _byGunGuid[gun.GUID] = _catalog.ProjectDirectSources(compatibleMagazineGuids)
                .Select(assetGuid => _itemsByGuid.TryGetValue(assetGuid, out ItemAsset item) ? item : null)
                .Where(item => item != null && !compatibleMagazineGuids.Contains(
                    AmmoRefillIdentity.NormalizeAssetGuid(item.GUID.ToString("N"))))
                .OrderBy(item => item.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<ItemAsset> GetCompatibleAmmo(ItemGunAsset gun)
    {
        return gun != null && _byGunGuid.TryGetValue(gun.GUID, out IReadOnlyList<ItemAsset> result)
            ? result
            : Array.Empty<ItemAsset>();
    }

    public bool IsAmmoRelated(ItemAsset asset)
    {
        return asset != null && _ammoRelatedGuids.Contains(asset.GUID);
    }

    public bool TryGetRefillRules(ItemAsset asset, out AmmoRefillAssetRules rules)
    {
        rules = null;
        if (asset == null || !_catalog.IsAmmoRelated(asset.GUID.ToString("N"))) return false;
        rules = _catalog.GetRules(asset.GUID.ToString("N"), asset is ItemMagazineAsset);
        return true;
    }

    public bool TryGetAmmoContainerCapacity(ItemAsset asset, out int capacity)
    {
        capacity = -1;
        if (!TryGetRefillRules(asset, out AmmoRefillAssetRules rules) ||
            !(rules.IsMagazine || rules.IsDirectTarget)) return false;
        return AmmoRefillIdentity.TryGetValidCapacity(asset.MaxAmount, out capacity);
    }

}
