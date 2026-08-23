using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnturnedSingleplayerCheatMenu.Models;

internal sealed class WorkshopLocalizationSnapshot
{
    internal static readonly WorkshopLocalizationSnapshot Empty = new(
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance),
        new Dictionary<object, string>(ReferenceComparer.Instance));

    internal WorkshopLocalizationSnapshot(
        IReadOnlyDictionary<object, string> itemNames,
        IReadOnlyDictionary<object, string> itemDescriptions,
        IReadOnlyDictionary<object, string> vehicleNames,
        IReadOnlyDictionary<object, string> actionTexts,
        IReadOnlyDictionary<object, string> actionTooltips,
        IReadOnlyDictionary<object, string> itemOriginalNames,
        IReadOnlyDictionary<object, string> itemOriginalDescriptions,
        IReadOnlyDictionary<object, string> vehicleOriginalNames,
        IReadOnlyDictionary<object, string> actionOriginalTexts,
        IReadOnlyDictionary<object, string> actionOriginalTooltips)
    {
        ItemNames = itemNames;
        ItemDescriptions = itemDescriptions;
        VehicleNames = vehicleNames;
        ActionTexts = actionTexts;
        ActionTooltips = actionTooltips;
        ItemOriginalNames = itemOriginalNames;
        ItemOriginalDescriptions = itemOriginalDescriptions;
        VehicleOriginalNames = vehicleOriginalNames;
        ActionOriginalTexts = actionOriginalTexts;
        ActionOriginalTooltips = actionOriginalTooltips;
    }

    internal IReadOnlyDictionary<object, string> ItemNames { get; }
    internal IReadOnlyDictionary<object, string> ItemDescriptions { get; }
    internal IReadOnlyDictionary<object, string> VehicleNames { get; }
    internal IReadOnlyDictionary<object, string> ActionTexts { get; }
    internal IReadOnlyDictionary<object, string> ActionTooltips { get; }
    internal IReadOnlyDictionary<object, string> ItemOriginalNames { get; }
    internal IReadOnlyDictionary<object, string> ItemOriginalDescriptions { get; }
    internal IReadOnlyDictionary<object, string> VehicleOriginalNames { get; }
    internal IReadOnlyDictionary<object, string> ActionOriginalTexts { get; }
    internal IReadOnlyDictionary<object, string> ActionOriginalTooltips { get; }

    internal bool TryGet(object instance, WorkshopTextField field, out string translation)
    {
        translation = null;
        IReadOnlyDictionary<object, string> map = field switch
        {
            WorkshopTextField.ItemName => ItemNames,
            WorkshopTextField.ItemDescription => ItemDescriptions,
            WorkshopTextField.VehicleName => VehicleNames,
            WorkshopTextField.ActionText => ActionTexts,
            WorkshopTextField.ActionTooltip => ActionTooltips,
            _ => null
        };
        return instance != null && map != null && map.TryGetValue(instance, out translation);
    }

    internal bool TryGetOriginal(object instance, WorkshopTextField field, out string original)
    {
        original = null;
        IReadOnlyDictionary<object, string> map = field switch
        {
            WorkshopTextField.ItemName => ItemOriginalNames,
            WorkshopTextField.ItemDescription => ItemOriginalDescriptions,
            WorkshopTextField.VehicleName => VehicleOriginalNames,
            WorkshopTextField.ActionText => ActionOriginalTexts,
            WorkshopTextField.ActionTooltip => ActionOriginalTooltips,
            _ => null
        };
        return instance != null && map != null && map.TryGetValue(instance, out original);
    }
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();

        public new bool Equals(object left, object right) => ReferenceEquals(left, right);

        public int GetHashCode(object value) => value == null ? 0 : RuntimeHelpers.GetHashCode(value);
    }
}
