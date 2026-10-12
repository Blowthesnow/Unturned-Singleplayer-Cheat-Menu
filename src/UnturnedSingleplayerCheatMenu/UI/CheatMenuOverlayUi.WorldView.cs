using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;
using InputField = UnityEngine.UI.InputField;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private Text _worldEnvironmentWeatherText;
	private Text _worldEnvironmentStateText;

	private void BuildOtherTab()
	{
		RectTransform worldScrollContent = CreateShortcutScroll(_contentHost.transform, styleSurface: false);
		ScrollRect worldScroll = worldScrollContent.GetComponentInParent<ScrollRect>();
		AddShortcutScrollbar(worldScroll);
		AddShortcutScrollLayout(worldScrollContent.gameObject, CheatMenuStyle.Theme.Space3, new RectOffset(12, 16, 8, 8));
		worldScrollContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		Transform transform = worldScrollContent;
		WorldPageSnapshot snapshot = _worldPresenter.CaptureSnapshot();
		uint cycle = snapshot.Cycle;
		float num = snapshot.NormalizedTime;

		const float primaryCardHeight = 460f;
		GameObject cardRow = CreateRow(transform, 0f, CheatMenuStyle.Theme.Space3);
		cardRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
		GameObject timeCard = CreateWorldCard(cardRow.transform, "TimeCard", primaryCardHeight);
		SetLayout(timeCard, 360f, primaryCardHeight, 1f, 0f);
		timeCard.GetComponent<LayoutElement>().minWidth = 360f;
		CreateLabel(timeCard.transform, "时间", 17, CheatMenuStyle.Text);
		_timeCurrentText = CreateLabel(timeCard.transform, $"当前时间：{snapshot.Time:N0} / {cycle:N0}（{num:P0}）", 14);
		_timeRing = CreateWorldTimeRing(timeCard.transform, num, delegate(float hours)
		{
			float normalized = WorldTimeClockMapping.ClockHoursToGameNormalized(hours);
			UpdateTimePreview(normalized, cycle);
			_timeApplyDebouncer.Schedule(delegate
			{
				ApplyTimeNormalized(normalized, cycle);
			});
		});
		_timePreviewText = CreateLabel(timeCard.transform, $"预览：{Mathf.RoundToInt(num * 100f)}%", 12, CheatMenuStyle.Muted);
		GameObject timeActions = CreateRow(timeCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(timeActions.transform, "设为白天", delegate
		{
			_timeApplyDebouncer.Cancel();
			_worldPresenter.SetDay();
			SetStatus("已切换到白天。");
		}, "立即把当前世界时间切换到白天时段。");
		CreateAction(timeActions.transform, "设为夜晚", delegate
		{
			_timeApplyDebouncer.Cancel();
			_worldPresenter.SetNight();
			SetStatus("已切换到夜晚。");
		}, "立即把当前世界时间切换到夜晚时段。");
		GameObject timeToggles = CreateRow(timeCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateToggleAction(timeToggles.transform, "冻结时间", snapshot.FreezeTimeEnabled, delegate(bool value)
		{
			_worldPresenter.SetFreezeTime(value);
			SetStatus(value ? "时间已冻结。" : "时间已恢复流动。");
		}, "冻结或恢复世界时间的自然流动。");
		CreateToggleAction(timeToggles.transform, "强制满月", snapshot.FullMoonEnabled, delegate(bool value)
		{
			_worldPresenter.SetFullMoon(value);
			SetStatus(value ? "已开启满月。" : "已关闭满月。");
		}, "强制启用或关闭当前世界的满月状态。");

		GameObject detailsColumn = CreateObject("WorldDetailsColumn", cardRow.transform);
		AddVerticalLayout(detailsColumn, CheatMenuStyle.Theme.Space3, new RectOffset(0, 0, 0, 0));
		SetLayout(detailsColumn, 360f, -1f, 1f, 0f);
		detailsColumn.GetComponent<LayoutElement>().minWidth = 360f;
		GameObject environmentCard = CreateWorldCard(detailsColumn.transform, "EnvironmentStatusCard", -1f);
		CreateLabel(environmentCard.transform, "环境状态", 17, CheatMenuStyle.Text);
		string weatherName = PluginLocalization.Translate(snapshot.ActiveWeatherName);
		_worldEnvironmentWeatherText = CreateFlowLabel(environmentCard.transform,
			string.Format(PluginLocalization.Translate("当前天气：{0}"), weatherName),
			CheatMenuStyle.Text);
		string timeState = PluginLocalization.Translate(snapshot.FreezeTimeEnabled ? "已冻结" : "自然流动");
		string moonState = PluginLocalization.Translate(snapshot.FullMoonEnabled ? "已开启" : "未开启");
		_worldEnvironmentStateText = CreateFlowLabel(environmentCard.transform, string.Format(
			PluginLocalization.Translate("时间：{0}    满月：{1}"), timeState, moonState), CheatMenuStyle.Muted);
		GameObject eventCard = CreateWorldCard(detailsColumn.transform, "WorldEventsCard", -1f);
		CreateLabel(eventCard.transform, "世界事件", 17, CheatMenuStyle.Text);
		GameObject eventRow1 = CreateRow(eventCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(eventRow1.transform, "立即呼叫空投", delegate
		{
			bool flag = _worldPresenter.CallAirdrop();
			SetStatus(flag ? "空投已呼叫。" : "此地图没有可用的空投节点或货物表。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
		}, "在当前地图的可用空投节点立即触发一次空投。");
		CreateAction(eventRow1.transform, "下雨", delegate
		{
			bool flag = _worldPresenter.StartRain();
			SetStatus(flag ? "已触发雨天。" : "当前地图没有默认雨天资产。", 3f, flag ? StatusKind.Success : StatusKind.Warning);
		}, "使用当前地图的默认雨天资产立即开始降雨。");
		CreateAction(eventRow1.transform, "暴雪", delegate
		{
			bool flag = _worldPresenter.StartSnow();
			SetStatus(flag ? "已触发暴雪。" : "当前地图没有默认雪天资产。", 3f, flag ? StatusKind.Success : StatusKind.Warning);
		}, "使用当前地图的默认雪天资产立即开始暴雪。");
		GameObject eventRow2 = CreateRow(eventCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(eventRow2.transform, "清除当前天气", delegate
		{
			_worldPresenter.ClearWeather();
			SetStatus("当前天气已清除，后续仍可自然调度。");
		}, "结束当前天气效果，但保留后续自然天气调度。");
		CreateAction(eventRow2.transform, "关闭天气调度", delegate
		{
			_worldPresenter.DisableWeather();
			SetStatus("当前天气和自然天气调度已关闭。", 3f, StatusKind.Warning);
		}, "结束当前天气并关闭后续自然天气调度。");

		GameObject scenarioCard = CreateWorldCard(detailsColumn.transform, "WorldScenarioCard", -1f);
		CreateLabel(scenarioCard.transform, "情景预设", 17, CheatMenuStyle.Text);
		GameObject scenarioRow1 = CreateRow(scenarioCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(scenarioRow1.transform, "晴朗正午", delegate
		{
			bool applied = _worldPresenter.ApplyScenario(WorldScenarioKind.ClearNoon);
			SetStatus(applied ? "已应用晴朗正午预设。" : "情景预设仅在受支持的单人世界中可用。",
				4f, applied ? StatusKind.Success : StatusKind.Warning);
		}, "设置白天、清除当前天气并关闭强制满月。");
		CreateAction(scenarioRow1.transform, "满月之夜", delegate
		{
			bool applied = _worldPresenter.ApplyScenario(WorldScenarioKind.FullMoonNight);
			SetStatus(applied ? "已应用满月之夜预设。" : "情景预设仅在受支持的单人世界中可用。",
				4f, applied ? StatusKind.Success : StatusKind.Warning);
		}, "设置夜晚、开启满月并清除当前天气。");
		GameObject scenarioRow2 = CreateRow(scenarioCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateAction(scenarioRow2.transform, "暴雪夜晚", delegate
		{
			bool started = _worldPresenter.ApplyScenario(WorldScenarioKind.SnowstormNight);
			string failure = _shellPresenter.SessionKind == RuntimeSessionKind.Singleplayer
				? "当前地图没有默认雪天资产。" : "情景预设仅在受支持的单人世界中可用。";
			SetStatus(started ? "已应用暴雪夜晚预设。" : failure, 4f,
				started ? StatusKind.Success : StatusKind.Warning);
		}, "设置夜晚并使用当前地图的默认暴雪资产。");
		CreateAction(scenarioRow2.transform, "恢复自然", delegate
		{
			bool applied = _worldPresenter.ApplyScenario(WorldScenarioKind.RestoreNatural);
			SetStatus(applied ? "时间、满月和天气已恢复自然状态。" : "情景预设仅在受支持的单人世界中可用。",
				4f, applied ? StatusKind.Success : StatusKind.Warning);
		}, "解除时间冻结、关闭强制满月并恢复自然天气调度。");

		GameObject scanCard = CreateWorldCard(transform, "AssetOverviewCard", -1f);
		CreateLabel(scanCard.transform, "资产概览", 17, CheatMenuStyle.Text);
		GameObject metricRow = CreateRow(scanCard.transform, 56f, CheatMenuStyle.Theme.Space2);
		CreateWorldMetric(metricRow.transform, "全部物品", snapshot.ItemCount);
		CreateWorldMetric(metricRow.transform, "Workshop 物品", snapshot.WorkshopItemCount);
		CreateWorldMetric(metricRow.transform, "全部载具", snapshot.VehicleCount);
		CreateWorldMetric(metricRow.transform, "Workshop 载具", snapshot.WorkshopVehicleCount);
		CreateFlowLabel(scanCard.transform, "只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。", CheatMenuStyle.Muted);
		GameObject scanActions = CreateRow(scanCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateButton(scanActions.transform, "重新扫描模组资产", delegate
		{
			_worldPresenter.RefreshCatalog();
			SetStatus("资产目录已重新扫描。", 4f, StatusKind.Info);
		}, "重新读取当前已加载的原版、地图和 Workshop 物品与载具资产。", 180f, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.SurfaceRaised);

		BuildWorldMaintenanceView(transform);
	}

	private void RefreshWorldEnvironment(WorldPageSnapshot snapshot)
	{
		if (snapshot == null) return;
		if (_worldEnvironmentWeatherText != null)
		{
			string weatherName = PluginLocalization.Translate(snapshot.ActiveWeatherName);
			_worldEnvironmentWeatherText.text = string.Format(
				PluginLocalization.Translate("当前天气：{0}"), weatherName);
		}
		if (_worldEnvironmentStateText != null)
		{
			string timeState = PluginLocalization.Translate(snapshot.FreezeTimeEnabled ? "已冻结" : "自然流动");
			string moonState = PluginLocalization.Translate(snapshot.FullMoonEnabled ? "已开启" : "未开启");
			_worldEnvironmentStateText.text = string.Format(
				PluginLocalization.Translate("时间：{0}    满月：{1}"), timeState, moonState);
		}
	}

	private void CreateWorldMetric(Transform parent, string label, int value)
	{
		GameObject metric = CreateObject("AssetMetric", parent, typeof(Image));
		Image image = metric.GetComponent<Image>();
		image.color = CheatMenuStyle.SurfaceInset;
		_fluentControls.StyleSurface(metric, image, ControlLibrary.Core.FluentSurfaceRole.Navigation,
			CheatMenuStyle.Theme.RadiusField);
		AddVerticalLayout(metric, 0f, new RectOffset(8, 8, 4, 4)).childAlignment = TextAnchor.MiddleCenter;
		SetLayout(metric, -1f, 56f, 1f);
		CreateLabel(metric.transform, value.ToString("N0"), 17, CheatMenuStyle.Text, alignment: TextAnchor.MiddleCenter, localize: false);
		CreateLabel(metric.transform, label, 11, CheatMenuStyle.Muted, alignment: TextAnchor.MiddleCenter);
	}

	private GameObject CreateWorldCard(Transform parent, string name, float height)
	{
		GameObject card = CreateObject(name, parent, typeof(Image));
		Image image = card.GetComponent<Image>();
		image.color = CheatMenuStyle.SurfaceRaised;
		_fluentControls.StyleSurface(card, image, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(card, CheatMenuStyle.Theme.Space2, new RectOffset(16, 16, 16, 16));
		SetLayout(card, -1f, height, 1f, 0f);
		return card;
	}
}
