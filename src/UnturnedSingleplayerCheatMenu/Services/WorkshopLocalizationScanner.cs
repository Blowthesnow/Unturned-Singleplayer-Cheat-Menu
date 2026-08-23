using System;
using System.Collections.Generic;
using System.Reflection;
using SDG.Unturned;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopLocalizationCandidate
{
    internal object Instance { get; set; }
    internal WorkshopTextField Field { get; set; }
    internal string SourceText { get; set; }
    internal string Key { get; set; }
    internal string AssetName { get; set; }
    internal string AssetGuid { get; set; }
    internal ushort LegacyId { get; set; }
    internal string ActionKey { get; set; }
}

internal sealed class WorkshopLocalizationScanResult
{
    internal IReadOnlyList<WorkshopLocalizationCandidate> Candidates { get; set; }
    internal int ItemCount { get; set; }
    internal int VehicleCount { get; set; }
}

internal sealed class WorkshopLocalizationScanner
{
    private readonly FieldInfo _itemNameField = typeof(ItemAsset).GetField("_itemName", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly FieldInfo _itemDescriptionField = typeof(ItemAsset).GetField("_itemDescription", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly FieldInfo _vehicleNameField = typeof(VehicleAsset).GetField("_vehicleName", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly FieldInfo _actionTextField = typeof(SDG.Unturned.Action).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly FieldInfo _actionTooltipField = typeof(SDG.Unturned.Action).GetField("_tooltip", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly FieldInfo _actionKeyField = typeof(SDG.Unturned.Action).GetField("_key", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly Action<string> _warn;
    private bool _warnedUnavailable;

    internal WorkshopLocalizationScanner(Action<string> warn = null)
    {
        _warn = warn;
    }

    internal WorkshopLocalizationScanResult Scan(string sourceLanguage, string targetLanguage)
    {
        if (_itemNameField == null || _itemDescriptionField == null || _vehicleNameField == null
            || _actionTextField == null || _actionTooltipField == null || _actionKeyField == null)
        {
            if (!_warnedUnavailable)
            {
                _warnedUnavailable = true;
                _warn?.Invoke("Workshop localization disabled: one or more verified private asset fields could not be resolved.");
            }
            return new WorkshopLocalizationScanResult
            {
                Candidates = Array.Empty<WorkshopLocalizationCandidate>(),
                ItemCount = 0,
                VehicleCount = 0
            };
        }

        List<ItemAsset> items = new();
        List<VehicleAsset> vehicles = new();
        Assets.find(items);
        Assets.find(vehicles);
        List<WorkshopLocalizationCandidate> candidates = new();
        int itemCount = 0;
        int vehicleCount = 0;

        foreach (ItemAsset item in items)
        {
            if (!IsSupportedAsset(item))
                continue;
            itemCount++;
            Add(candidates, item, WorkshopTextField.ItemName, Read(_itemNameField, item), item, sourceLanguage, targetLanguage);
            Add(candidates, item, WorkshopTextField.ItemDescription, Read(_itemDescriptionField, item), item, sourceLanguage, targetLanguage);

            IReadOnlyList<SDG.Unturned.Action> actions = item.actions;
            if (actions == null)
                continue;
            foreach (SDG.Unturned.Action action in actions)
            {
                if (action == null)
                    continue;
                string actionKey = Read(_actionKeyField, action);
                Add(candidates, action, WorkshopTextField.ActionText, Read(_actionTextField, action), item, sourceLanguage, targetLanguage, actionKey);
                Add(candidates, action, WorkshopTextField.ActionTooltip, Read(_actionTooltipField, action), item, sourceLanguage, targetLanguage, actionKey);
            }
        }

        foreach (VehicleAsset vehicle in vehicles)
        {
            if (!IsSupportedAsset(vehicle))
                continue;
            vehicleCount++;
            Add(candidates, vehicle, WorkshopTextField.VehicleName, Read(_vehicleNameField, vehicle), vehicle, sourceLanguage, targetLanguage);
        }

        return new WorkshopLocalizationScanResult
        {
            Candidates = candidates,
            ItemCount = itemCount,
            VehicleCount = vehicleCount
        };
    }

#pragma warning disable CS0618
    private static bool IsSupportedAsset(Asset asset)
        => asset != null
            && (asset.assetOrigin == EAssetOrigin.OFFICIAL || asset.assetOrigin == EAssetOrigin.WORKSHOP);
#pragma warning restore CS0618

    private static void Add(
        ICollection<WorkshopLocalizationCandidate> candidates,
        object instance,
        WorkshopTextField field,
        string source,
        Asset asset,
        string sourceLanguage,
        string targetLanguage,
        string actionKey = null)
    {
        string normalized = WorkshopTextIdentity.NormalizeSource(source);
        if (!HasTranslatableCharacters(normalized))
            return;

        candidates.Add(new WorkshopLocalizationCandidate
        {
            Instance = instance,
            Field = field,
            SourceText = normalized,
            Key = WorkshopTextIdentity.CreateKey(sourceLanguage, targetLanguage, field, normalized),
            AssetName = asset?.name,
            AssetGuid = asset?.GUID.ToString("N"),
            LegacyId = asset?.id ?? 0,
            ActionKey = actionKey
        });
    }

    private static string Read(FieldInfo field, object instance)
    {
        try { return field?.GetValue(instance) as string; }
        catch { return null; }
    }

    private static bool HasTranslatableCharacters(string text)
    {
        foreach (char character in text ?? string.Empty)
        {
            if (char.IsLetter(character))
                return true;
        }
        return false;
    }
}
