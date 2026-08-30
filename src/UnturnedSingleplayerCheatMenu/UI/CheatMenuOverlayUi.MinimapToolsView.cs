using System;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Services;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private int _minimapSettingsPage;

	private void BuildMinimapPanel(Transform panel)
	{
		MinimapSettings settings = _minimapPresenter.CaptureSettings();
		CreateSummaryPanel(panel, $"状态：{MinimapDisplayText.BackgroundState(_minimapPresenter.BackgroundState)}  ·  默认关闭  ·  北向固定");
		GameObject pages = CreateRow(panel, 36f, 6f);
		CreateMinimapPageButton(pages.transform, 0, "常规");
		CreateMinimapPageButton(pages.transform, 1, "标记");
		CreateMinimapPageButton(pages.transform, 2, "地下");
		CreateMinimapPageButton(pages.transform, 3, "联动");
		switch (_minimapSettingsPage)
		{
			case 0: BuildMinimapGeneralPage(panel, settings); break;
			case 1: BuildMinimapMarkerPage(panel, settings); break;
			case 2: BuildMinimapUndergroundPage(panel, settings); break;
			default: BuildMinimapLinkPage(panel, settings); break;
		}
	}

	private void CreateMinimapPageButton(Transform parent, int page, string label)
	{
		Button button = CreateButton(parent, label, delegate
		{
			_minimapSettingsPage = page;
			RefreshActiveToolsPageContent();
		}, string.Format(PluginLocalization.Translate("切换到小地图“{0}”设置页。"), PluginLocalization.Translate(label)),
			0f, 34f, _minimapSettingsPage == page ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 12);
		SetLayout(button.gameObject, -1f, -1f, 1f);
	}

	private void BuildMinimapGeneralPage(Transform panel, MinimapSettings settings)
	{
		GameObject toggles = CreateRow(panel, 40f, 7f);
		CreateToggleAction(toggles.transform, "启用小地图", settings.Enabled, value => ChangeMinimap(s => s.Enabled = value));
		CreateToggleAction(toggles.transform, "布局编辑", settings.LayoutEditMode, SetMinimapLayoutEdit,
			"进入后插件窗口会隐藏；拖动或缩放小地图，按回车键应用。正常状态不拦截鼠标。");
		GameObject modes = CreateRow(panel, 40f, 7f);
		CreateCycleButton(modes.transform, "形状：" + MinimapDisplayText.Shape(settings.Shape), () => ChangeMinimap(s => s.Shape = NextEnum(s.Shape)), "循环切换小地图视口形状。");
		CreateCycleButton(modes.transform, "锚点：" + MinimapDisplayText.Anchor(settings.Anchor), () => ChangeMinimap(s => s.Anchor = NextEnum(s.Anchor)), "循环切换小地图在屏幕上的锚点位置。");
		CreateCycleButton(modes.transform, "背景：" + MinimapDisplayText.BackgroundMode(settings.BackgroundMode), () => ChangeMinimap(s => s.BackgroundMode = NextEnum(s.BackgroundMode)), "循环切换小地图使用的背景来源。");
		CreateMinimapSlider(panel, "大小", settings.Size, MinimapSettings.MinSize, MinimapSettings.MaxSize, 10f, " 像素", value => s => s.Size = value);
		CreateMinimapSlider(panel, "可见半径", settings.VisibleRadiusMeters, MinimapSettings.MinVisibleRadiusMeters,
			MinimapSettings.MaxVisibleRadiusMeters, 5f, " 米", value => s => s.VisibleRadiusMeters = value);
		GameObject frequencies = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(frequencies.transform, "背景", settings.BackgroundRenderHz,
			MinimapSettings.MinRenderHz, MinimapSettings.MaxRenderHz, 1f, " 次/秒", value => s => s.BackgroundRenderHz = value);
		CreateMinimapCompactSlider(frequencies.transform, "扫描", settings.WorldScanHz,
			MinimapSettings.MinScanHz, MinimapSettings.MaxScanHz, 1f, " 次/秒", value => s => s.WorldScanHz = value);
		GameObject display = CreateRow(panel, 40f, 7f);
		CreateToggleAction(display.transform, "实时建筑", settings.ShowLoadedBuildings, value => ChangeMinimap(s => s.ShowLoadedBuildings = value));
		CreateToggleAction(display.transform, "指南针", settings.ShowCompass, value => ChangeMinimap(s => s.ShowCompass = value));
		CreateToggleAction(display.transform, "坐标", settings.ShowCoordinates, value => ChangeMinimap(s => s.ShowCoordinates = value));
		CreateLabel(panel, "圆形与正方形视口均已预创建；切换不会在画布重建期间销毁后重建。", 12, CheatMenuStyle.Muted);
	}

	private void BuildMinimapMarkerPage(Transform panel, MinimapSettings settings)
	{
		GameObject row1 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(row1.transform, "原生标记", settings.ShowNativeMapMarkers, value => ChangeMinimap(s => s.ShowNativeMapMarkers = value));
		CreateToggleAction(row1.transform, "传送点", settings.ShowTeleportMarkers, value => ChangeMinimap(s => s.ShowTeleportMarkers = value));
		CreateToggleAction(row1.transform, "其他玩家", settings.ShowPlayers, value => ChangeMinimap(s => s.ShowPlayers = value));
		GameObject row2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(row2.transform, "僵尸", settings.ShowZombies, value => ChangeMinimap(s => s.ShowZombies = value));
		CreateToggleAction(row2.transform, "动物", settings.ShowAnimals, value => ChangeMinimap(s => s.ShowAnimals = value));
		CreateToggleAction(row2.transform, "地面物品", settings.ShowItems, value => ChangeMinimap(s => s.ShowItems = value));
		CreateSection(panel, "物品稀有度");
		GameObject rare1 = CreateRow(panel, 38f, 6f);
		CreateRarityToggle(rare1.transform, "普通", ItemRarityMask.Common, settings);
		CreateRarityToggle(rare1.transform, "罕见", ItemRarityMask.Uncommon, settings);
		CreateRarityToggle(rare1.transform, "稀有", ItemRarityMask.Rare, settings);
		GameObject rare2 = CreateRow(panel, 38f, 6f);
		CreateRarityToggle(rare2.transform, "史诗", ItemRarityMask.Epic, settings);
		CreateRarityToggle(rare2.transform, "传奇", ItemRarityMask.Legendary, settings);
		CreateRarityToggle(rare2.transform, "神话", ItemRarityMask.Mythical, settings);
		CreateButton(rare2.transform, "全选", () => ChangeMinimap(s => s.ItemRarityMask = ItemRarityMask.All), "允许小地图显示所有稀有度的地面物品。", 64f, 34f, CheatMenuStyle.Surface, 11);
		CreateButton(rare2.transform, "全不选", () => ChangeMinimap(s => s.ItemRarityMask = ItemRarityMask.None), "取消所有物品稀有度，小地图将不显示地面物品标记。", 72f, 34f, CheatMenuStyle.Surface, 11);
		GameObject floor = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(floor.transform, "合并", settings.ItemMergePixels,
			MinimapSettings.MinItemMergePixels, MinimapSettings.MaxItemMergePixels, 1f, " 像素", value => s => s.ItemMergePixels = value);
		CreateMinimapCompactSlider(floor.transform, "同层", settings.SameFloorTolerance,
			MinimapSettings.MinSameFloorTolerance, MinimapSettings.MaxSameFloorTolerance, 0.5f, " 米", value => s => s.SameFloorTolerance = value);
		CreateToggleAction(panel, "显示其他楼层", settings.ShowOtherFloors, value => ChangeMinimap(s => s.ShowOtherFloors = value),
			"默认显示全部楼层；淡化的三角图形表示目标在上方或下方。");
	}

	private void BuildMinimapUndergroundPage(Transform panel, MinimapSettings settings)
	{
		CreateLabel(panel, "5 点双向探针按连续厚度判定；薄楼板与楼层间空气不会累计。", 12, CheatMenuStyle.Muted);
		GameObject row1 = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(row1.transform, "探针", settings.OcclusionProbeHz,
			MinimapSettings.MinProbeHz, MinimapSettings.MaxProbeHz, 0.5f, " 次/秒", value => s => s.OcclusionProbeHz = value);
		CreateMinimapCompactSlider(row1.transform, "合并间隙", settings.OcclusionMergeGap,
			0f, 5f, 0.05f, " 米", value => s => s.OcclusionMergeGap = value);
		GameObject row2 = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(row2.transform, "进入厚度", settings.OcclusionEnterThickness,
			0.25f, 10f, 0.25f, " 米", value => s => s.OcclusionEnterThickness = value);
		CreateMinimapCompactSlider(row2.transform, "退出厚度", settings.OcclusionExitThickness,
			0.1f, 9f, 0.25f, " 米", value => s => s.OcclusionExitThickness = value);
		GameObject row3 = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(row3.transform, "进入延时", settings.OcclusionEnterDelaySeconds,
			0f, 5f, 0.25f, " 秒", value => s => s.OcclusionEnterDelaySeconds = value);
		CreateMinimapCompactSlider(row3.transform, "退出延时", settings.OcclusionExitDelaySeconds,
			0f, 5f, 0.25f, " 秒", value => s => s.OcclusionExitDelaySeconds = value);
		GameObject row4 = CreateRow(panel, 42f, 8f);
		CreateMinimapCompactSlider(row4.transform, "楼板排除", settings.FloorExclusionMaxThickness,
			0f, 5f, 0.25f, " 米", value => s => s.FloorExclusionMaxThickness = value);
		CreateMinimapCompactSlider(row4.transform, "切片上方", settings.UndergroundSliceAbove,
			MinimapSettings.MinUndergroundSlice, MinimapSettings.MaxUndergroundSlice, 0.5f, " 米", value => s => s.UndergroundSliceAbove = value);
		CreateMinimapSlider(panel, "切片下方", settings.UndergroundSliceBelow,
			MinimapSettings.MinUndergroundSlice, MinimapSettings.MaxUndergroundSlice, 0.5f, " 米", value => s => s.UndergroundSliceBelow = value);
		CreateLabel(panel, "安全关系必须满足：楼板排除 ≤ 退出厚度 < 进入厚度；无效组合会回退默认值。", 12, CheatMenuStyle.Warning);
	}

	private void BuildMinimapLinkPage(Transform panel, MinimapSettings settings)
	{
		AutoPickupSettings autoSettings = _autoPickupPresenter.CaptureSnapshot().Settings;
		CreateLabel(panel, "两个联动均默认关闭，并且只允许小地图设置单向约束自动拾取。", 13, CheatMenuStyle.Muted);
		GameObject links = CreateRow(panel, 40f, 7f);
		CreateToggleAction(links.transform, "拾取范围跟随小地图", autoSettings.UseMinimapRange, _autoPickupPresenter.SetUseMinimapRange);
		CreateToggleAction(links.transform, "拾取稀有度跟随小地图", autoSettings.UseMinimapRarityFilter, _autoPickupPresenter.SetUseMinimapRarityFilter);
		CreateLabel(panel, "小地图物品层始终读取原始地面物品快照；自动拾取关闭、品质、名单和丢弃抑制都不会隐藏物品图形。", 12, CheatMenuStyle.Text);
		CreateLabel(panel, $"当前可见半径：{settings.VisibleRadiusMeters:0.#} 米  ·  稀有度：{MinimapDisplayText.Rarity(settings.ItemRarityMask)}", 12, CheatMenuStyle.Muted);
		OverheadOcclusionDiagnostics diagnostics = _minimapPresenter.OcclusionDiagnostics;
		CreateLabel(panel, $"背景：{MinimapDisplayText.BackgroundState(_minimapPresenter.BackgroundState)}  ·  厚遮挡探针：{diagnostics.Summary.EnterProbeCount}/{diagnostics.Summary.ProbeCount}  ·  最大连续厚度：{diagnostics.Summary.MaximumEffectiveThickness:0.00} 米", 12, CheatMenuStyle.Muted);
		CreateLabel(panel, "地下白名单辅助证据：" + (diagnostics.UndergroundAllowlistContainsPlayer ? "是" : "否"), 12, CheatMenuStyle.Muted);
	}

	private void CreateRarityToggle(Transform parent, string label, ItemRarityMask bit, MinimapSettings settings)
	{
		CreateToggleAction(parent, label, (settings.ItemRarityMask & bit) != 0, value => ChangeMinimap(s =>
		{
			s.ItemRarityMask = value ? s.ItemRarityMask | bit : s.ItemRarityMask & ~bit;
		}));
	}

	private void CreateCycleButton(Transform parent, string label, Action action, string tooltip)
	{
		Button button = CreateButton(parent, label, action, tooltip, 0f, 36f, CheatMenuStyle.Surface, 12);
		SetLayout(button.gameObject, -1f, -1f, 1f);
	}

	private void CreateMinimapSlider(Transform parent, string label, float value, float min, float max,
		float step, string suffix, Func<float, Action<MinimapSettings>> change)
	{
		GameObject row = CreateRow(parent, 42f, 8f);
		CreateMinimapSliderContent(row.transform, label, value, min, max, step, suffix, change);
	}

	private void CreateMinimapCompactSlider(Transform parent, string label, float value, float min, float max,
		float step, string suffix, Func<float, Action<MinimapSettings>> change)
	{
		GameObject host = CreateObject("MinimapSlider", parent);
		HorizontalLayoutGroup layout = host.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 6f;
		layout.childAlignment = TextAnchor.MiddleLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		SetLayout(host, -1f, 40f, 1f);
		CreateMinimapSliderContent(host.transform, label, value, min, max, step, suffix, change);
	}

	private void CreateMinimapSliderContent(Transform parent, string label, float value, float min, float max,
		float step, string suffix, Func<float, Action<MinimapSettings>> change)
	{
		CreateLabel(parent, label, 12, CheatMenuStyle.Text, 78f);
		Text valueText = CreateText(parent, value.ToString("0.##") + suffix, 11, CheatMenuStyle.Muted,
			TextAnchor.MiddleRight, FontStyle.Normal, localize: false);
		SetLayout(valueText.gameObject, 64f, 30f);
		Slider slider = CreateTimeSlider(parent, Mathf.InverseLerp(min, max, value), normalized =>
		{
			float selected = Mathf.Clamp(Mathf.Round(Mathf.Lerp(min, max, normalized) / step) * step, min, max);
			valueText.text = selected.ToString("0.##") + suffix;
			_minimapSettingsDebouncer.Schedule(() => _minimapPresenter.UpdateSettings(change(selected)));
		});
		SetLayout(slider.gameObject, -1f, -1f, 1f);
	}

	private void ChangeMinimap(Action<MinimapSettings> change)
	{
		_minimapPresenter.UpdateSettings(change);
		RefreshActiveToolsPageContent();
	}

	private void SetMinimapLayoutEdit(bool value)
	{
		if (!value)
		{
			ChangeMinimap(settings => settings.LayoutEditMode = false);
			return;
		}
		_minimapPresenter.UpdateSettings(settings =>
		{
			settings.Enabled = true;
			settings.LayoutEditMode = true;
		});
		_shellPresenter.CloseMenu();
	}

	private static T NextEnum<T>(T current) where T : struct, Enum
	{
		T[] values = (T[])Enum.GetValues(typeof(T));
		int next = (Array.IndexOf(values, current) + 1) % values.Length;
		return values[next];
	}
}
