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
		GameObject cardRow = CreateRow(transform, primaryCardHeight, CheatMenuStyle.Theme.Space3);
		cardRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
		GameObject timeCard = CreateWorldCard(cardRow.transform, "TimeCard", primaryCardHeight);
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

		GameObject eventCard = CreateWorldCard(cardRow.transform, "WorldEventsCard", primaryCardHeight);
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

		GameObject scanCard = CreateWorldCard(transform, "ScanInfoCard", 184f);
		CreateLabel(scanCard.transform, "扫描信息", 17, CheatMenuStyle.Text);
		CreateLabel(scanCard.transform, $"物品：{snapshot.ItemCount}（模组 {snapshot.WorkshopItemCount}）    车辆：{snapshot.VehicleCount}（模组 {snapshot.WorkshopVehicleCount}）", 14);
		CreateLabel(scanCard.transform, "只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。", 12, CheatMenuStyle.Muted);
		GameObject scanActions = CreateRow(scanCard.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateButton(scanActions.transform, "重新扫描模组资产", delegate
		{
			_worldPresenter.RefreshCatalog();
			SetStatus("资产目录已重新扫描。", 4f, StatusKind.Info);
		}, "重新读取当前已加载的原版、地图和 Workshop 物品与载具资产。", 180f, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.SurfaceRaised);
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
