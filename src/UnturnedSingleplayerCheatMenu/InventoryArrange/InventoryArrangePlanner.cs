using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

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
		InventoryArrangePlan inventoryArrangePlan = new InventoryArrangePlan();
		if (containers == null || settings == null)
		{
			return Fail(inventoryArrangePlan, "整理输入不可用。");
		}
		List<InventoryArrangeContainer> list = (from page in containers
			where page != null && page.Width > 0 && page.Height > 0
			orderby page.Page
			select page).ToList();
		if (list.Count == 0)
		{
			return Fail(inventoryArrangePlan, "没有可整理的库存容器。");
		}
		List<InventoryArrangeItem> list2 = (from item in list.SelectMany((InventoryArrangeContainer page) => page.Items)
			where item != null
			select item).ToList();
		inventoryArrangePlan.InputItemCount = list2.Count;
		if (list2.Count == 0)
		{
			inventoryArrangePlan.IsSuccess = true;
			return inventoryArrangePlan;
		}
		List<InventoryArrangeItem> list3 = list2.Where((InventoryArrangeItem item) => item.IsProtected).ToList();
		List<InventoryArrangeItem> list4 = list2.Where((InventoryArrangeItem item) => !item.IsProtected).ToList();
		bool flag = settings.Action == InventoryArrangeAction.Arrange && settings.Preset == InventorySortPreset.LowMovement;
		List<InventoryArrangeItem> list5 = (((settings.Action == InventoryArrangeAction.Compact) | flag) ? list4.OrderBy((InventoryArrangeItem item) => item.OriginalIndex).ToList() : Merge(list4, settings.AutoRefillMagazines));
		inventoryArrangePlan.MergeCount = list4.Count - list5.Count;
		if (settings.AutoRefillMagazines && ((settings.Action == InventoryArrangeAction.Compact) | flag))
		{
			list5 = RefillMagazineGroups(list5);
		}
		if (settings.Action == InventoryArrangeAction.Arrange && !flag)
		{
			list5.Sort((InventoryArrangeItem left, InventoryArrangeItem right) => Compare(left, right, InventorySortRegistry.GetProfile(settings.Preset, settings.CustomKeys)));
		}
		Dictionary<int, PageState> dictionary = list.ToDictionary((InventoryArrangeContainer page) => page.Page, (InventoryArrangeContainer page) => new PageState(page));
		foreach (InventoryArrangeItem item in list3)
		{
			if (!dictionary.TryGetValue(item.SourcePage, out var value) || !Reserve(value, item.SourceX, item.SourceY, item.Width, item.Height, item.SourceRotation))
			{
				return Fail(inventoryArrangePlan, "受保护物品的位置无效，已取消整理。");
			}
			inventoryArrangePlan.Placements.Add(Placement(item, item.SourcePage, item.SourceX, item.SourceY, item.SourceRotation));
		}
		if (flag)
		{
			foreach (InventoryArrangeItem item2 in list5)
			{
				if (!dictionary.TryGetValue(item2.SourcePage, out var value2) || !Reserve(value2, item2.SourceX, item2.SourceY, item2.Width, item2.Height, item2.SourceRotation))
				{
					return Fail(inventoryArrangePlan, "低扰动整理发现原位置无效，已取消整理。");
				}
				inventoryArrangePlan.Placements.Add(Placement(item2, item2.SourcePage, item2.SourceX, item2.SourceY, item2.SourceRotation));
			}
			return Complete(inventoryArrangePlan);
		}
		foreach (InventoryArrangeItem item3 in list5)
		{
			if (!TryPlace(dictionary, item3, settings.AutoRotate, settings.AllowCrossPage, settings.PreferredPages, out var placement))
			{
				return Fail(inventoryArrangePlan, "预检无法完整安置所有物品；不会修改库存，也不会自动掉落。");
			}
			inventoryArrangePlan.Placements.Add(placement);
		}
		return Complete(inventoryArrangePlan);
	}

	private static InventoryArrangePlan FinishMergeOnly(InventoryArrangePlan plan, IEnumerable<InventoryArrangeItem> protectedItems, IEnumerable<InventoryArrangeItem> output)
	{
		foreach (InventoryArrangeItem item in protectedItems.Concat(output))
		{
			plan.Placements.Add(Placement(item, item.SourcePage, item.SourceX, item.SourceY, item.SourceRotation));
		}
		return Complete(plan);
	}

	private static List<InventoryArrangeItem> Merge(IEnumerable<InventoryArrangeItem> items, bool autoRefillMagazines)
	{
		List<InventoryArrangeItem> list = new List<InventoryArrangeItem>();
		foreach (IGrouping<string, InventoryArrangeItem> item in items.OrderBy((InventoryArrangeItem item) => item.OriginalIndex).GroupBy<InventoryArrangeItem, string>(MergeKey, StringComparer.Ordinal))
		{
			List<InventoryArrangeItem> list2 = item.OrderBy((InventoryArrangeItem item) => item.OriginalIndex).ToList();
			if (list2[0].IsAmmoContainer)
			{
				list.AddRange(autoRefillMagazines ? RefillMagazineGroups(list2) : list2);
				continue;
			}
			if (list2[0].MaxAmount <= 1 || list2.Sum((InventoryArrangeItem item) => item.Amount) <= 0)
			{
				list.AddRange(list2);
				continue;
			}
			int num = list2.Sum((InventoryArrangeItem item) => item.Amount);
			int num2 = 0;
			while (num > 0)
			{
				InventoryArrangeItem inventoryArrangeItem = list2[Math.Min(num2, list2.Count - 1)];
				int num3 = Math.Min(inventoryArrangeItem.MaxAmount, num);
				list.Add(inventoryArrangeItem.CloneWithAmount(num3));
				num -= num3;
				num2++;
			}
		}
		return list;
	}

	private static List<InventoryArrangeItem> RefillMagazineGroups(IEnumerable<InventoryArrangeItem> items)
	{
		List<InventoryArrangeItem> list = items.ToList();
		Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		foreach (IGrouping<string, InventoryArrangeItem> item in list.Where((InventoryArrangeItem item) => item.IsAmmoContainer).GroupBy<InventoryArrangeItem, string>(AmmoGroupKey, StringComparer.Ordinal))
		{
			List<InventoryArrangeItem> source = item.ToList();
			if (!source.Any((InventoryArrangeItem item) => item.AmmoCount < 0 || item.AmmoCapacity <= 0))
			{
				int num = source.Sum((InventoryArrangeItem item) => Math.Max(0, item.AmmoCount));
				int num2 = source.Sum((InventoryArrangeItem item) => item.AmmoCapacity);
				if (num <= num2)
				{
					hashSet.Add(item.Key);
					dictionary[item.Key] = num;
				}
			}
		}
		List<InventoryArrangeItem> list2 = new List<InventoryArrangeItem>(list.Count);
		foreach (InventoryArrangeItem item2 in list)
		{
			if (!item2.IsAmmoContainer)
			{
				list2.Add(item2);
				continue;
			}
			string text = AmmoGroupKey(item2);
			if (!hashSet.Contains(text))
			{
				list2.Add(item2);
				continue;
			}
			int num3 = Math.Min(item2.AmmoCapacity, dictionary[text]);
			dictionary[text] -= num3;
			list2.Add(item2.CloneWithAmount(num3));
		}
		return list2;
	}

	private static string AmmoGroupKey(InventoryArrangeItem item)
	{
		return item.AmmoGroup + "|" + item.Quality;
	}

	private static bool TryPlace(Dictionary<int, PageState> states, InventoryArrangeItem item, bool autoRotate, bool crossPage, IReadOnlyList<int> preferredPages, out InventoryArrangePlacement placement)
	{
		List<PageState> list = states.Values.OrderBy((PageState state) => state.Container.Page).ToList();
		PageState source;
		if (preferredPages != null && preferredPages.Count > 0)
		{
			list = (from state in list
				orderby PreferredIndex(preferredPages, state.Container.Page), state.Container.Page
				select state).ToList();
		}
		else if (states.TryGetValue(item.SourcePage, out source))
		{
			list = new PageState[1] { source }.Concat(list.Where((PageState state) => state != source)).ToList();
		}
		IEnumerable<PageState> enumerable;
		if (!crossPage)
		{
			enumerable = list.Take(1);
		}
		else
		{
			IEnumerable<PageState> enumerable2 = list;
			enumerable = enumerable2;
		}
		foreach (PageState item2 in enumerable)
		{
			foreach (int item3 in Rotations(item, autoRotate))
			{
				int num = ((item3 % 2 == 0) ? item.Width : item.Height);
				int num2 = ((item3 % 2 == 0) ? item.Height : item.Width);
				for (int num3 = 0; num3 <= item2.Container.Height - num2; num3++)
				{
					for (int num4 = 0; num4 <= item2.Container.Width - num; num4++)
					{
						if (IsFree(item2, num4, num3, num, num2))
						{
							Fill(item2, num4, num3, num, num2, value: true);
							placement = Placement(item, item2.Container.Page, num4, num3, item3);
							return true;
						}
					}
				}
			}
		}
		placement = null;
		return false;
	}

	private static IEnumerable<int> Rotations(InventoryArrangeItem item, bool autoRotate)
	{
		yield return item.SourceRotation;
		int num = ((item.SourceRotation % 2 == 0) ? 1 : 0);
		if (autoRotate && item.IsRotatable && item.Width != item.Height)
		{
			yield return num;
		}
	}

	private static bool Reserve(PageState state, int x, int y, int width, int height, int rotation)
	{
		if (rotation % 2 != 0)
		{
			int num = height;
			height = width;
			width = num;
		}
		if (!IsFree(state, x, y, width, height))
		{
			return false;
		}
		Fill(state, x, y, width, height, value: true);
		return true;
	}

	private static bool IsFree(PageState state, int x, int y, int width, int height)
	{
		if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > state.Container.Width || y + height > state.Container.Height)
		{
			return false;
		}
		for (int i = x; i < x + width; i++)
		{
			for (int j = y; j < y + height; j++)
			{
				if (state.Occupied[i, j])
				{
					return false;
				}
			}
		}
		return true;
	}

	private static void Fill(PageState state, int x, int y, int width, int height, bool value)
	{
		for (int i = x; i < x + width; i++)
		{
			for (int j = y; j < y + height; j++)
			{
				state.Occupied[i, j] = value;
			}
		}
	}

	private static int PreferredIndex(IReadOnlyList<int> preferredPages, int page)
	{
		for (int i = 0; i < preferredPages.Count; i++)
		{
			if (preferredPages[i] == page)
			{
				return i;
			}
		}
		return int.MaxValue;
	}

	private static InventoryArrangePlacement Placement(InventoryArrangeItem item, int page, int x, int y, int rotation)
	{
		return new InventoryArrangePlacement
		{
			Item = item,
			Page = page,
			X = x,
			Y = y,
			Rotation = rotation
		};
	}

	private static InventoryArrangePlan Complete(InventoryArrangePlan plan)
	{
		plan.OutputItemCount = plan.Placements.Count;
		foreach (InventoryArrangePlacement placement in plan.Placements)
		{
			InventoryArrangeItem item = placement.Item;
			if (placement.Page != item.SourcePage || placement.X != item.SourceX || placement.Y != item.SourceY || placement.Rotation != item.SourceRotation)
			{
				plan.MoveCount++;
			}
			if (placement.Page != item.SourcePage)
			{
				plan.CrossPageCount++;
			}
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

	private static string MergeKey(InventoryArrangeItem item)
	{
		if (!item.IsAmmoContainer)
		{
			return item.AssetGuid + "|" + item.Quality + "|" + item.StateHash + "|" + item.MaxAmount;
		}
		return "mag|" + AmmoGroupKey(item);
	}

	private static int Compare(InventoryArrangeItem left, InventoryArrangeItem right, InventorySortProfile profile)
	{
		foreach (InventorySortKeySpec key in profile.Keys)
		{
			int num = CompareKey(left, right, key.Key);
			if (num != 0)
			{
				return (key.Direction == InventorySortDirection.Ascending) ? num : (-num);
			}
		}
		int num2 = string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal);
		if (num2 != 0)
		{
			return num2;
		}
		int num3 = string.Compare(left.StateHash, right.StateHash, StringComparison.Ordinal);
		if (num3 != 0)
		{
			return num3;
		}
		int num4 = left.SourcePage.CompareTo(right.SourcePage);
		if (num4 == 0)
		{
			return left.OriginalIndex.CompareTo(right.OriginalIndex);
		}
		return num4;
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
			InventorySortKey.StackFill => ((long)left.Amount * (long)Math.Max(1, right.MaxAmount)).CompareTo((long)right.Amount * (long)Math.Max(1, left.MaxAmount)),
			InventorySortKey.Ammo => CompareUnavailable(left.AmmoCount, right.AmmoCount),
			InventorySortKey.Hotkey => left.IsHotkeyBound.CompareTo(right.IsHotkeyBound),
			InventorySortKey.OriginalPosition => left.OriginalIndex.CompareTo(right.OriginalIndex),
			InventorySortKey.Rarity => left.RarityRank.CompareTo(right.RarityRank),
			_ => 0,
		};
	}

	private static int CompareUnavailable(int left, int right)
	{
		bool flag = left < 0;
		bool flag2 = right < 0;
		if (flag != flag2)
		{
			if (!flag)
			{
				return -1;
			}
			return 1;
		}
		return left.CompareTo(right);
	}
}
