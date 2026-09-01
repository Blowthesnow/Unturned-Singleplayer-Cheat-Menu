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
	private void BuildPointToolPanel(Transform panel)
	{
		InteractionToolsSnapshot snapshot = _interactionPresenter.CaptureSnapshot();
		if (!snapshot.IsAvailable)
		{
			CreateEmptyState(panel, "准星工具不可用", "插件尚未完成交互服务初始化。");
			return;
		}
		GameObject creatureHealthRow = CreateRow(panel, 40f, 7f);
		CreateToggleAction(creatureHealthRow.transform, "生物头顶血条", snapshot.CreatureHealthBarEnabled, delegate(bool value)
		{
			_interactionPresenter.SetCreatureHealthBarEnabled(value);
			SetStatus(value ? "生物头顶血条已开启。" : "生物头顶血条已关闭。", 5f, StatusKind.Info);
		});
		CreateLabel(panel, "准心目标或实际受伤后的生物会显示当前生命 / 生命上限。", 12, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject.transform, "启用准星交互工具", snapshot.Enabled, delegate(bool value)
		{
			_interactionPresenter.SetPointToolEnabled(value);
			SetStatus(value ? "准星交互工具已开启。" : "准星交互工具已关闭。", 5f, StatusKind.Info);
		});
		Button modeTrigger = null;
		modeTrigger = CreatePointToolModeButton(gameObject.transform, snapshot.Mode, delegate
		{
			TogglePointToolModeDrawer(modeTrigger);
		});
		CreateLabel(panel, "中键触发；删除必须同时按住 Shift。", 13, CheatMenuStyle.Muted);
		CreateButton(panel, $"最大距离 {snapshot.Range:0}m", delegate
		{
			float pointToolRange = ((snapshot.Range >= 250f) ? 25f : (snapshot.Range + 25f));
			_interactionPresenter.SetRange(pointToolRange);
			RefreshActiveToolsPageContent();
		}, "将准星工具最大作用距离增加 25 米；到 250 米后循环回 25 米。", 220f);
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "名称", snapshot.ShowTargetName, delegate(bool value)
		{
			_interactionPresenter.SetShowTargetName(value);
		});
		CreateToggleAction(gameObject2.transform, "ID / GUID", snapshot.ShowId, delegate(bool value)
		{
			_interactionPresenter.SetShowId(value);
		});
		CreateToggleAction(gameObject2.transform, "生命 / 耐久", snapshot.ShowHealth, delegate(bool value)
		{
			_interactionPresenter.SetShowHealth(value);
		});
		BuildPointToolDescriptions(panel);
	}

	private void BuildPointToolDescriptions(Transform parent)
	{
		GameObject gameObject = CreateObject("PointToolDescriptions", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		AddVerticalLayout(gameObject, CheatMenuStyle.Theme.Space1, new RectOffset(12, 12, 8, 8));
		SetLayout(gameObject, -1f, 286f, 1f, 1f);
		CreateLabel(gameObject.transform, "各模式功能介绍", 14, CheatMenuStyle.Text);
		AddPointToolDescription(gameObject.transform, PointToolMode.Smart, "根据准星目标自动选择单一动作：维修、实用、准星传送、检查或 Shift + 中键删除。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Inspect, "查看准星指向对象的名称、ID / GUID、生命 / 耐久和位置等信息。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Repair, "维修车辆耐久、电瓶、油量、轮胎并扶正翻倒车辆，也可修复路障和建筑。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Teleport, "将玩家传送到准星指向的世界表面，并自动寻找安全落点。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Utility, "执行准星指向对象的原生交互，例如上车、打开容器或切换设备。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Delete, "删除准星指向的可删除对象；必须同时按住 Shift 再点击中键。");
	}

	private void AddPointToolDescription(Transform parent, PointToolMode mode, string description)
	{
		GameObject gameObject = CreateRow(parent, 32f, 10f);
		CreateLabel(gameObject.transform, PointToolModeLabel(mode), 12, CheatMenuStyle.Accent, 72f);
		SetLayout(CreateText(gameObject.transform, description, 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft).gameObject, -1f, 32f, 1f);
	}

	private static string PointToolModeLabel(PointToolMode mode)
	{
		return mode switch
		{
			PointToolMode.Smart => "智能",
			PointToolMode.Inspect => "检查",
			PointToolMode.Repair => "维修",
			PointToolMode.Teleport => "传送",
			PointToolMode.Utility => "实用",
			PointToolMode.Delete => "删除",
			_ => "智能",
		};
	}

	private Button CreatePointToolModeButton(Transform parent, PointToolMode mode, System.Action action)
	{
		return CreateButton(parent, PointToolModeLabel(mode), action,
			string.Format(PluginLocalization.Translate("打开准星工具模式列表；当前为“{0}”。"), PluginLocalization.Translate(PointToolModeLabel(mode))),
			150f, 40f, (_interactionPresenter.CaptureSnapshot().Mode == mode) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12);
	}

	private void TogglePointToolModeDrawer(Button trigger)
	{
		if (_pointToolModeDrawerOpen)
		{
			HidePointToolModeDrawer();
			return;
		}
		_pointToolModeDrawerOpen = true;
		_pointToolModeTriggerButton = trigger;
		BuildPointToolModeDrawer();
	}

	private void BuildPointToolModeDrawer()
	{
		if (_pointToolModeTriggerButton == null || _root == null)
		{
			return;
		}
		InteractionToolsSnapshot snapshot = _interactionPresenter.CaptureSnapshot();
		if (_pointToolModeDrawerRoot != null)
		{
			QueueForDestroy(_pointToolModeDrawerRoot);
		}
		_pointToolModeDrawerRoot = CreateObject("PointToolModeDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_pointToolModeDrawerRoot, HidePointToolModeDrawer);
		Image drawerImage = _pointToolModeDrawerRoot.GetComponent<Image>();
		drawerImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(_pointToolModeDrawerRoot);
		_fluentControls.StyleSurface(_pointToolModeDrawerRoot, drawerImage, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		Outline outline = _pointToolModeDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_pointToolModeDrawerPopup = _pointToolModeDrawerRoot.GetComponent<RectTransform>();
		RectTransform pointToolModeDrawerPopup = _pointToolModeDrawerPopup;
		Vector2 anchorMin = (pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		pointToolModeDrawerPopup.anchorMin = anchorMin;
		pointToolModeDrawerPopup.pivot = new Vector2(0f, 1f);
		pointToolModeDrawerPopup.sizeDelta = new Vector2(150f, 280f);
		VerticalLayoutGroup verticalLayoutGroup = _pointToolModeDrawerRoot.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		PointToolMode[] pointToolModes = PointToolModes;
		foreach (PointToolMode pointToolMode in pointToolModes)
		{
			PointToolMode capturedMode = pointToolMode;
			SetLayout(CreateButton(_pointToolModeDrawerRoot.transform, PointToolModeLabel(capturedMode), delegate
			{
				_interactionPresenter.SetMode(capturedMode);
				HidePointToolModeDrawer();
				RefreshActiveToolsPageContent();
			}, string.Format(PluginLocalization.Translate("将准星工具切换为“{0}”模式。"), PluginLocalization.Translate(PointToolModeLabel(capturedMode))),
				0f, 42f, (snapshot.Mode == capturedMode) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12).gameObject, -1f, 42f, 1f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_pointToolModeDrawerPopup);
		PositionPointToolModeDrawer();
	}

	private void MaintainPointToolModeDrawer()
	{
		if (!(_pointToolModeDrawerRoot == null))
		{
			PositionPointToolModeDrawer();
			if (Input.GetMouseButtonDown(0) && _pointToolModeTriggerButton != null && _pointToolModeDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_pointToolModeDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_pointToolModeTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HidePointToolModeDrawer();
			}
		}
	}

	private void PositionPointToolModeDrawer()
	{
		if (!(_pointToolModeTriggerButton == null) && !(_pointToolModeDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _pointToolModeTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			Vector2 sizeDelta = _pointToolModeDrawerPopup.sizeDelta;
			sizeDelta.x = component.rect.width;
			_pointToolModeDrawerPopup.sizeDelta = sizeDelta;
			RectTransform pointToolModeDrawerPopup = _pointToolModeDrawerPopup;
			Vector2 anchorMin = (_pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
			pointToolModeDrawerPopup.anchorMin = anchorMin;
			bool flag = localPoint.y - component2.rect.yMin < _pointToolModeDrawerPopup.rect.height + 8f;
			_pointToolModeDrawerPopup.pivot = (flag ? new Vector2(0f, 0f) : new Vector2(0f, 1f));
			_pointToolModeDrawerPopup.anchoredPosition = (flag ? localPoint2 : localPoint);
		}
	}

	private void HidePointToolModeDrawer()
	{
		_pointToolModeDrawerOpen = false;
		if (_pointToolModeDrawerRoot != null)
		{
			QueueForDestroy(_pointToolModeDrawerRoot);
		}
		_pointToolModeDrawerRoot = null;
		_pointToolModeDrawerPopup = null;
		_pointToolModeTriggerButton = null;
	}
}
