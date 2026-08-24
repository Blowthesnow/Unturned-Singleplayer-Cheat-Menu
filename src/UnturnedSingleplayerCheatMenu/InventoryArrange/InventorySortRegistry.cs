using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.InventoryArrange;

internal static class InventorySortRegistry
{
	internal static readonly InventorySortPreset[] FirstRoundPresets = new InventorySortPreset[6]
	{
		InventorySortPreset.Compatibility,
		InventorySortPreset.Compact,
		InventorySortPreset.Type,
		InventorySortPreset.Name,
		InventorySortPreset.LargeFirst,
		InventorySortPreset.SmallFirst
	};

	internal static readonly InventorySortPreset[] AvailablePresets = new InventorySortPreset[13]
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
			_ => Profile(InventorySortPreset.Compatibility, "参考兼容", Key(InventorySortKey.Type, InventorySortDirection.Ascending), Key(InventorySortKey.Area, InventorySortDirection.Descending), Key(InventorySortKey.Name, InventorySortDirection.Ascending)),
		};
	}

	internal static InventorySortProfile GetProfile(InventorySortPreset preset, IReadOnlyList<InventorySortKeySpec> customKeys)
	{
		if (preset == InventorySortPreset.Custom && customKeys != null && customKeys.Count > 0)
		{
			return new InventorySortProfile(preset, "自定义", customKeys.ToArray());
		}
		return GetProfile(preset);
	}

	internal static bool TryParseCustom(string text, out List<InventorySortKeySpec> keys)
	{
		keys = new List<InventorySortKeySpec>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string[] array = text.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string obj in array)
		{
			string[] array2 = obj.Trim().Split(new char[2] { ':', '=' }, 2);
			if (array2.Length == 0 || !Enum.TryParse<InventorySortKey>(array2[0].Trim(), ignoreCase: true, out var key) || !IsRuntimeAvailable(key))
			{
				continue;
			}
			InventorySortDirection result = InventorySortDirection.Ascending;
			if ((array2.Length <= 1 || Enum.TryParse<InventorySortDirection>(array2[1].Trim(), ignoreCase: true, out result)) && !keys.Any((InventorySortKeySpec existing) => existing.Key == key))
			{
				keys.Add(new InventorySortKeySpec(key, result));
				if (keys.Count >= 5)
				{
					break;
				}
			}
		}
		return keys.Count > 0;
	}

	internal static string GetDisplayName(InventorySortPreset preset)
	{
		return GetProfile(preset).DisplayName;
	}

	internal static bool IsRuntimeAvailable(InventorySortKey key)
	{
		return true;
	}

	private static InventorySortProfile Profile(InventorySortPreset preset, string name, params InventorySortKeySpec[] keys)
	{
		return new InventorySortProfile(preset, name, keys);
	}

	private static InventorySortKeySpec Key(InventorySortKey key, InventorySortDirection direction)
	{
		return new InventorySortKeySpec(key, direction);
	}
}
