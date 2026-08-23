using System;
using System.Linq;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventorySortRegistry
{
    internal static readonly InventorySortPreset[] FirstRoundPresets =
    {
        InventorySortPreset.Compatibility,
        InventorySortPreset.Compact,
        InventorySortPreset.Type,
        InventorySortPreset.Name,
        InventorySortPreset.LargeFirst,
        InventorySortPreset.SmallFirst
    };
    internal static readonly InventorySortPreset[] AvailablePresets =
    {
        InventorySortPreset.Compatibility,
        InventorySortPreset.Compact,
        InventorySortPreset.Type,
        InventorySortPreset.Name,
        InventorySortPreset.LargeFirst,
        InventorySortPreset.SmallFirst,
        InventorySortPreset.Rarity,
        InventorySortPreset.Quality,
        InventorySortPreset.StackFill,
        InventorySortPreset.Hotkey,
        InventorySortPreset.Custom,
        InventorySortPreset.Ammo,
        InventorySortPreset.LowMovement
    };
    internal static InventorySortProfile GetProfile(InventorySortPreset preset)
    {
        return preset switch
        {
            InventorySortPreset.Compact => Profile(preset, "紧凑", Key(InventorySortKey.Area, InventorySortDirection.Descending), Key(InventorySortKey.Type, InventorySortDirection.Ascending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
            InventorySortPreset.Type => Profile(preset, "类型", Key(InventorySortKey.Type, InventorySortDirection.Ascending), Key(InventorySortKey.Name, InventorySortDirection.Ascending), Key(InventorySortKey.Area, InventorySortDirection.Descending)),
            InventorySortPreset.Name => Profile(preset, "名称", Key(InventorySortKey.Name, InventorySortDirection.Ascending), Key(InventorySortKey.Type, InventorySortDirection.Ascending), Key(InventorySortKey.Area, InventorySortDirection.Descending)),
            InventorySortPreset.LargeFirst => Profile(preset, "大件优先", Key(InventorySortKey.Area, InventorySortDirection.Descending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
            InventorySortPreset.SmallFirst => Profile(preset, "小件优先", Key(InventorySortKey.Area, InventorySortDirection.Ascending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
            InventorySortPreset.Quality => Profile(preset, "品质优先", Key(InventorySortKey.Quality, InventorySortDirection.Descending), Key(InventorySortKey.Type, InventorySortDirection.Ascending)),
            InventorySortPreset.Rarity => Profile(preset, "高稀有度优先（神话→普通）", Key(InventorySortKey.Rarity, InventorySortDirection.Descending), Key(InventorySortKey.Type, InventorySortDirection.Ascending)),
            InventorySortPreset.StackFill => Profile(preset, "堆叠整理", Key(InventorySortKey.StackFill, InventorySortDirection.Descending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
            InventorySortPreset.Hotkey => Profile(preset, "热键优先", Key(InventorySortKey.Hotkey, InventorySortDirection.Descending), Key(InventorySortKey.Type, InventorySortDirection.Ascending)),
            InventorySortPreset.Ammo => Profile(preset, "弹药管理", Key(InventorySortKey.Ammo, InventorySortDirection.Descending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
            InventorySortPreset.LowMovement => Profile(preset, "低扰动", Key(InventorySortKey.OriginalPosition, InventorySortDirection.Ascending)),
            InventorySortPreset.Custom => Profile(preset, "自定义", Key(InventorySortKey.OriginalPosition, InventorySortDirection.Ascending)),
            _ => Profile(InventorySortPreset.Compatibility, "参考兼容", Key(InventorySortKey.Type, InventorySortDirection.Ascending), Key(InventorySortKey.Area, InventorySortDirection.Descending), Key(InventorySortKey.Name, InventorySortDirection.Ascending))
        };
    }

    internal static InventorySortProfile GetProfile(InventorySortPreset preset, IReadOnlyList<InventorySortKeySpec> customKeys)
    {
        if (preset == InventorySortPreset.Custom && customKeys != null && customKeys.Count > 0)
            return new InventorySortProfile(preset, "自定义", customKeys.ToArray());
        return GetProfile(preset);
    }

    internal static bool TryParseCustom(string text, out List<InventorySortKeySpec> keys)
    {
        keys = new List<InventorySortKeySpec>();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (string rawPart in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = rawPart.Trim().Split(new[] { ':', '=' }, 2);
            if (parts.Length == 0 || !Enum.TryParse(parts[0].Trim(), true, out InventorySortKey key) || !IsRuntimeAvailable(key))
                continue;
            InventorySortDirection direction = InventorySortDirection.Ascending;
            if (parts.Length > 1 && !Enum.TryParse(parts[1].Trim(), true, out direction))
                continue;
            if (keys.Any(existing => existing.Key == key))
                continue;
            keys.Add(new InventorySortKeySpec(key, direction));
            if (keys.Count >= 5)
                break;
        }
        return keys.Count > 0;
    }

    internal static string GetDisplayName(InventorySortPreset preset) => GetProfile(preset).DisplayName;

    internal static bool IsRuntimeAvailable(InventorySortKey key) => true;

    private static InventorySortProfile Profile(InventorySortPreset preset, string name, params InventorySortKeySpec[] keys)
    {
        return new InventorySortProfile(preset, name, keys);
    }

    private static InventorySortKeySpec Key(InventorySortKey key, InventorySortDirection direction)
    {
        return new InventorySortKeySpec(key, direction);
    }
}

internal sealed class InventoryArrangePlanner
{
    private sealed class PageState
    {
        internal readonly InventoryArrangeContainer Container;
        internal readonly bool[,] Occupied;

        internal PageState(InventoryArrangeContainer container)
        {
            Container = container;
            Occupied = new bool[container.Width, container.Height];
        }
    }

    internal InventoryArrangePlan CreatePlan(IReadOnlyList<InventoryArrangeContainer> containers, InventoryArrangeSettings settings)
    {
        InventoryArrangePlan plan = new();
        if (containers == null || settings == null)
            return Fail(plan, "整理输入不可用。");

        List<InventoryArrangeContainer> pages = containers
            .Where(page => page != null && page.Width > 0 && page.Height > 0)
            .OrderBy(page => page.Page)
            .ToList();
        if (pages.Count == 0)
            return Fail(plan, "没有可整理的库存容器。");

        List<InventoryArrangeItem> input = pages.SelectMany(page => page.Items).Where(item => item != null).ToList();
        plan.InputItemCount = input.Count;
        if (input.Count == 0)
        {
            plan.IsSuccess = true;
            return plan;
        }

        List<InventoryArrangeItem> protectedItems = input.Where(item => item.IsProtected).ToList();
        List<InventoryArrangeItem> movable = input.Where(item => !item.IsProtected).ToList();
        bool preservePositions = settings.Action == InventoryArrangeAction.Arrange
            && settings.Preset == InventorySortPreset.LowMovement;
        List<InventoryArrangeItem> output = settings.Action == InventoryArrangeAction.Compact || preservePositions
            ? movable.OrderBy(item => item.OriginalIndex).ToList()
            : Merge(movable, settings.AutoRefillMagazines);
        plan.MergeCount = movable.Count - output.Count;
        if (settings.AutoRefillMagazines && (settings.Action == InventoryArrangeAction.Compact || preservePositions))
            output = RefillMagazineGroups(output);
        if (settings.Action == InventoryArrangeAction.Arrange && !preservePositions)
            output.Sort((left, right) => Compare(left, right, InventorySortRegistry.GetProfile(settings.Preset, settings.CustomKeys)));

        Dictionary<int, PageState> states = pages.ToDictionary(page => page.Page, page => new PageState(page));
        foreach (InventoryArrangeItem item in protectedItems)
        {
            if (!states.TryGetValue(item.SourcePage, out PageState state)
                || !Reserve(state, item.SourceX, item.SourceY, item.Width, item.Height, item.SourceRotation))
            {
                return Fail(plan, "受保护物品的位置无效，已取消整理。");
            }
            plan.Placements.Add(Placement(item, item.SourcePage, item.SourceX, item.SourceY, item.SourceRotation));
        }

        if (preservePositions)
        {
            foreach (InventoryArrangeItem item in output)
            {
                if (!states.TryGetValue(item.SourcePage, out PageState state)
                    || !Reserve(state, item.SourceX, item.SourceY, item.Width, item.Height, item.SourceRotation))
                {
                    return Fail(plan, "低扰动整理发现原位置无效，已取消整理。");
                }
                plan.Placements.Add(Placement(item, item.SourcePage, item.SourceX, item.SourceY, item.SourceRotation));
            }
            return Complete(plan);
        }


        foreach (InventoryArrangeItem item in output)
        {
            if (!TryPlace(states, item, settings.AutoRotate, settings.AllowCrossPage, settings.PreferredPages, out InventoryArrangePlacement placement))
                return Fail(plan, "预检无法完整安置所有物品；不会修改库存，也不会自动掉落。");
            plan.Placements.Add(placement);
        }

        return Complete(plan);
    }

    private static InventoryArrangePlan FinishMergeOnly(InventoryArrangePlan plan, IEnumerable<InventoryArrangeItem> protectedItems, IEnumerable<InventoryArrangeItem> output)
    {
        foreach (InventoryArrangeItem item in protectedItems.Concat(output))
            plan.Placements.Add(Placement(item, item.SourcePage, item.SourceX, item.SourceY, item.SourceRotation));
        return Complete(plan);
    }

    private static List<InventoryArrangeItem> Merge(IEnumerable<InventoryArrangeItem> items, bool autoRefillMagazines)
    {
        List<InventoryArrangeItem> output = new();
        foreach (IGrouping<string, InventoryArrangeItem> group in items.OrderBy(item => item.OriginalIndex).GroupBy(MergeKey, StringComparer.Ordinal))
        {
            List<InventoryArrangeItem> source = group.OrderBy(item => item.OriginalIndex).ToList();
            if (source[0].IsAmmoContainer)
            {
                output.AddRange(autoRefillMagazines ? RefillMagazineGroups(source) : source);
                continue;
            }
            if (source[0].MaxAmount <= 1 || source.Sum(item => item.Amount) <= 0)
            {
                output.AddRange(source);
                continue;
            }
            int remaining = source.Sum(item => item.Amount);
            int index = 0;
            while (remaining > 0)
            {
                InventoryArrangeItem template = source[Math.Min(index, source.Count - 1)];
                int amount = Math.Min(template.MaxAmount, remaining);
                output.Add(template.CloneWithAmount(amount));
                remaining -= amount;
                index++;
            }
        }
        return output;
    }

    private static List<InventoryArrangeItem> RefillMagazineGroups(IEnumerable<InventoryArrangeItem> items)
    {
        List<InventoryArrangeItem> input = items.ToList();
        Dictionary<string, int> remainingByGroup = new(StringComparer.Ordinal);
        HashSet<string> validGroups = new(StringComparer.Ordinal);
        foreach (IGrouping<string, InventoryArrangeItem> group in input.Where(item => item.IsAmmoContainer).GroupBy(AmmoGroupKey, StringComparer.Ordinal))
        {
            List<InventoryArrangeItem> magazines = group.ToList();
            if (magazines.Any(item => item.AmmoCount < 0 || item.AmmoCapacity <= 0))
                continue;
            int total = magazines.Sum(item => Math.Max(0, item.AmmoCount));
            int capacity = magazines.Sum(item => item.AmmoCapacity);
            if (total > capacity)
                continue;
            validGroups.Add(group.Key);
            remainingByGroup[group.Key] = total;
        }

        List<InventoryArrangeItem> output = new(input.Count);
        foreach (InventoryArrangeItem item in input)
        {
            if (!item.IsAmmoContainer)
            {
                output.Add(item);
                continue;
            }
            string key = AmmoGroupKey(item);
            if (!validGroups.Contains(key))
            {
                output.Add(item);
                continue;
            }
            int amount = Math.Min(item.AmmoCapacity, remainingByGroup[key]);
            remainingByGroup[key] -= amount;
            output.Add(item.CloneWithAmount(amount));
        }
        return output;
    }

    private static string AmmoGroupKey(InventoryArrangeItem item)
    {
        return item.AmmoGroup + "|" + item.Quality;
    }


    private static bool TryPlace(Dictionary<int, PageState> states, InventoryArrangeItem item, bool autoRotate, bool crossPage, IReadOnlyList<int> preferredPages, out InventoryArrangePlacement placement)
    {
        List<PageState> ordered = states.Values.OrderBy(state => state.Container.Page).ToList();
        if (preferredPages != null && preferredPages.Count > 0)
        {
            ordered = ordered
                .OrderBy(state => PreferredIndex(preferredPages, state.Container.Page))
                .ThenBy(state => state.Container.Page)
                .ToList();
        }
        else if (states.TryGetValue(item.SourcePage, out PageState source))
        {
            ordered = new[] { source }.Concat(ordered.Where(state => !ReferenceEquals(state, source))).ToList();
        }

        IEnumerable<PageState> candidates = crossPage ? ordered : ordered.Take(1);

        foreach (PageState state in candidates)
        {
            foreach (int rotation in Rotations(item, autoRotate))
            {
                int width = rotation % 2 == 0 ? item.Width : item.Height;
                int height = rotation % 2 == 0 ? item.Height : item.Width;
                for (int y = 0; y <= state.Container.Height - height; y++)
                for (int x = 0; x <= state.Container.Width - width; x++)
                {
                    if (!IsFree(state, x, y, width, height)) continue;
                    Fill(state, x, y, width, height, true);
                    placement = Placement(item, state.Container.Page, x, y, rotation);
                    return true;
                }
            }
        }
        placement = null;
        return false;
    }

    private static IEnumerable<int> Rotations(InventoryArrangeItem item, bool autoRotate)
    {
        yield return item.SourceRotation;
        int alternate = item.SourceRotation % 2 == 0 ? 1 : 0;
        if (autoRotate && item.IsRotatable && item.Width != item.Height)
            yield return alternate;
    }

    private static bool Reserve(PageState state, int x, int y, int width, int height, int rotation)
    {
        if (rotation % 2 != 0) (width, height) = (height, width);
        if (!IsFree(state, x, y, width, height)) return false;
        Fill(state, x, y, width, height, true);
        return true;
    }

    private static bool IsFree(PageState state, int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > state.Container.Width || y + height > state.Container.Height)
            return false;
        for (int col = x; col < x + width; col++)
        for (int row = y; row < y + height; row++)
            if (state.Occupied[col, row]) return false;
        return true;
    }

    private static void Fill(PageState state, int x, int y, int width, int height, bool value)
    {
        for (int col = x; col < x + width; col++)
        for (int row = y; row < y + height; row++)
            state.Occupied[col, row] = value;
    }

    private static int PreferredIndex(IReadOnlyList<int> preferredPages, int page)
    {
        for (int index = 0; index < preferredPages.Count; index++)
        {
            if (preferredPages[index] == page)
                return index;
        }
        return int.MaxValue;
    }

    private static InventoryArrangePlacement Placement(InventoryArrangeItem item, int page, int x, int y, int rotation)
    {
        return new InventoryArrangePlacement { Item = item, Page = page, X = x, Y = y, Rotation = rotation };
    }

    private static InventoryArrangePlan Complete(InventoryArrangePlan plan)
    {
        plan.OutputItemCount = plan.Placements.Count;
        foreach (InventoryArrangePlacement placement in plan.Placements)
        {
            InventoryArrangeItem item = placement.Item;
            if (placement.Page != item.SourcePage || placement.X != item.SourceX || placement.Y != item.SourceY || placement.Rotation != item.SourceRotation)
                plan.MoveCount++;
            if (placement.Page != item.SourcePage) plan.CrossPageCount++;
        }
        plan.IsSuccess = true;
        return plan;
    }

    private static InventoryArrangePlan Fail(InventoryArrangePlan plan, string reason)
    {
        plan.IsSuccess = false;
        plan.FailureReason = reason;
        plan.Placements.Clear();
        return plan;
    }

    private static string MergeKey(InventoryArrangeItem item) => item.IsAmmoContainer ? "mag|" + AmmoGroupKey(item) : item.AssetGuid + "|" + item.Quality + "|" + item.StateHash + "|" + item.MaxAmount;

    private static int Compare(InventoryArrangeItem left, InventoryArrangeItem right, InventorySortProfile profile)
    {
        foreach (InventorySortKeySpec key in profile.Keys)
        {
            int result = CompareKey(left, right, key.Key);
            if (result != 0) return key.Direction == InventorySortDirection.Ascending ? result : -result;
        }
        int resultGuid = string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal);
        if (resultGuid != 0) return resultGuid;
        int resultState = string.Compare(left.StateHash, right.StateHash, StringComparison.Ordinal);
        if (resultState != 0) return resultState;
        int resultPage = left.SourcePage.CompareTo(right.SourcePage);
        return resultPage != 0 ? resultPage : left.OriginalIndex.CompareTo(right.OriginalIndex);
    }

    private static int CompareKey(InventoryArrangeItem left, InventoryArrangeItem right, InventorySortKey key)
    {
        return key switch
        {
            InventorySortKey.Type => left.TypeRank.CompareTo(right.TypeRank),
            InventorySortKey.Name => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase),
            InventorySortKey.Area => left.Area.CompareTo(right.Area),
            InventorySortKey.Width => left.Width.CompareTo(right.Width),
            InventorySortKey.Height => left.Height.CompareTo(right.Height),
            InventorySortKey.LegacyId => left.LegacyId.CompareTo(right.LegacyId),
            InventorySortKey.Guid => string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal),
            InventorySortKey.Quality => left.Quality.CompareTo(right.Quality),
            InventorySortKey.StackFill => ((long)left.Amount * Math.Max(1, right.MaxAmount)).CompareTo((long)right.Amount * Math.Max(1, left.MaxAmount)),
            InventorySortKey.Ammo => CompareUnavailable(left.AmmoCount, right.AmmoCount),
            InventorySortKey.Hotkey => left.IsHotkeyBound.CompareTo(right.IsHotkeyBound),
            InventorySortKey.OriginalPosition => left.OriginalIndex.CompareTo(right.OriginalIndex),
            InventorySortKey.Rarity => left.RarityRank.CompareTo(right.RarityRank),
            _ => 0
        };
    }

    private static int CompareUnavailable(int left, int right)
    {
        bool leftUnavailable = left < 0;
        bool rightUnavailable = right < 0;
        if (leftUnavailable != rightUnavailable)
            return leftUnavailable ? 1 : -1;
        return left.CompareTo(right);
    }
}
