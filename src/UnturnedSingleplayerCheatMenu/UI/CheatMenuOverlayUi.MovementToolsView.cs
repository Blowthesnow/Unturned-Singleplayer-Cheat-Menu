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
	private void BuildMovementPanel(Transform panel)
	{
		MovementToolsSnapshot snapshot = _movementPresenter.CaptureSnapshot();
		if (!snapshot.IsAvailable)
		{
			CreateEmptyState(panel, "移动工具不可用", "插件尚未完成移动服务初始化。");
			return;
		}
		GameObject modeRow = CreateRow(panel, 40f, 8f);
		CreateLabel(modeRow.transform, string.Format(
			PluginLocalization.Translate("飞行模式：{0}"),
			PluginLocalization.Translate(snapshot.Mode.ToDisplayName())), 13, CheatMenuStyle.Text);
		CreateFlexibleSpacer(modeRow.transform);
		CreateButton(modeRow.transform, "⇆", delegate
		{
			FlightMode nextMode = snapshot.Mode == FlightMode.WorldAxis ? FlightMode.ViewDirection : FlightMode.WorldAxis;
			_movementPresenter.SetFlightMode(nextMode);
			SetStatus(string.Format(
				PluginLocalization.Translate("飞行模式已切换为 {0}。"),
				PluginLocalization.Translate(nextMode.ToDisplayName())), 5f, StatusKind.Info);
			RefreshActiveToolsPageContent();
		}, "切换飞行模式：在世界轴与视角指向之间切换。", 44f, 36f, CheatMenuStyle.Accent, 18, delayTooltip: false);
		GameObject gameObject = CreateRow(panel, 42f, 8f);
		CreateToggleAction(gameObject.transform, "飞行", snapshot.FlightEnabled, delegate(bool value)
		{
			_movementPresenter.SetFlight(value);
			SetStatus(value ? "飞行已开启。" : "飞行已关闭。", 5f, StatusKind.Info);
		});
		CreateToggleAction(gameObject.transform, "穿墙", snapshot.NoclipEnabled, delegate(bool value)
		{
			_movementPresenter.SetNoclip(value);
			SetStatus(value ? "穿墙已开启。" : "穿墙已关闭。", 5f, StatusKind.Info);
		});
		CreateMovementSpeedSlider(panel, "水平速度", snapshot.FlightSpeed, _movementPresenter.SetFlightSpeed);
		CreateMovementSpeedSlider(panel, "垂直速度", snapshot.VerticalSpeed, _movementPresenter.SetFlightVerticalSpeed);
		CreateToggleAction(panel, "关闭穿墙后寻找安全位置", snapshot.SafeExit, delegate(bool value)
		{
			_movementPresenter.SetNoclipSafeExit(value);
		});
		CreateLabel(panel, "WASD 移动，Space 上升，Ctrl 下降。", 13, CheatMenuStyle.Muted);
	}

	private void CreateMovementSpeedSlider(Transform parent, string label, float value, Action<float> changed)
	{
		GameObject gameObject = CreateRow(parent, 40f, 8f);
		CreateLabel(gameObject.transform, label, 13, CheatMenuStyle.Text, 92f);
		Text valueText = CreateText(gameObject.transform, $"{value:0}x", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, FontStyle.Normal, localize: false);
		SetLayout(valueText.gameObject, 42f, CheatMenuStyle.Theme.ControlCompact);
		SetLayout(CreateTimeSlider(gameObject.transform, Mathf.InverseLerp(1f, 10f, value), delegate(float normalized)
		{
			float num = Mathf.Clamp(Mathf.Round(Mathf.Lerp(1f, 10f, Mathf.Clamp01(normalized))), 1f, 10f);
			changed(num);
			valueText.text = $"{num:0}x";
		}).gameObject, -1f, -1f, 1f);
	}
}
