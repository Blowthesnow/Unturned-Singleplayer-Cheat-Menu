using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal static class MinimapDisplayText
{
	public static string Shape(MinimapShape value)
	{
		return value == MinimapShape.Square ? "正方形" : "圆形";
	}

	public static string Anchor(MinimapAnchor value)
	{
		return value switch
		{
			MinimapAnchor.TopRight => "右上",
			MinimapAnchor.BottomLeft => "左下",
			MinimapAnchor.BottomRight => "右下",
			_ => "左上"
		};
	}

	public static string BackgroundMode(MinimapBackgroundMode value)
	{
		return value switch
		{
			MinimapBackgroundMode.NativeMapOnly => "仅原生地图",
			MinimapBackgroundMode.RealtimeSurface => "实时地表",
			MinimapBackgroundMode.RealtimeHeightSlice => "实时高度切片",
			MinimapBackgroundMode.Disabled => "关闭背景",
			_ => "自动优先原生地图"
		};
	}

	public static string BackgroundState(MinimapBackgroundState value)
	{
		return value switch
		{
			MinimapBackgroundState.RealtimeSurface => "实时地表",
			MinimapBackgroundState.RealtimeHeightSlice => "实时高度切片",
			MinimapBackgroundState.FallbackGrid => "备用网格",
			MinimapBackgroundState.Disabled => "背景已关闭",
			_ => "原生地图裁剪"
		};
	}

	public static string Rarity(ItemRarityMask value)
	{
		value &= ItemRarityMask.All;
		if (value == ItemRarityMask.None) return "无";
		if (value == ItemRarityMask.All) return "全部";
		List<string> labels = new List<string>(6);
		Append(labels, value, ItemRarityMask.Common, "普通");
		Append(labels, value, ItemRarityMask.Uncommon, "罕见");
		Append(labels, value, ItemRarityMask.Rare, "稀有");
		Append(labels, value, ItemRarityMask.Epic, "史诗");
		Append(labels, value, ItemRarityMask.Legendary, "传奇");
		Append(labels, value, ItemRarityMask.Mythical, "神话");
		return string.Join("、", labels);
	}

	private static void Append(List<string> labels, ItemRarityMask value, ItemRarityMask bit, string label)
	{
		if ((value & bit) != 0) labels.Add(label);
	}
}
