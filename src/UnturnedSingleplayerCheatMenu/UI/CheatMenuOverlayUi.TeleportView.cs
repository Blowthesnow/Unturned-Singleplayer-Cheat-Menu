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
	private void BuildTeleportsTab()
	{
		Transform transform = _contentHost.transform;
		AddVerticalLayout(_contentHost, 8f, new RectOffset(12, 12, 10, 10));
		GameObject gameObject = CreateRow(transform, 38f, 8f);
		CreateButton(gameObject.transform, "地图", delegate
		{
			SwitchTeleportView(TeleportView.Map);
		}, "切换到地图视图，在 GPS 图层上查看和选择传送位置。", 0f, 36f, (_teleportView == TeleportView.Map) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 14);
		CreateButton(gameObject.transform, "传送点", delegate
		{
			SwitchTeleportView(TeleportView.Points);
		}, "切换到传送点视图，保存、使用或删除当前地图的传送点。", 0f, 36f, (_teleportView == TeleportView.Points) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 14);
		foreach (Transform item in gameObject.transform)
		{
			SetLayout(item.gameObject, -1f, -1f, 1f);
		}
		if (_teleportView == TeleportView.Map)
		{
			BuildTeleportMapView(transform);
		}
		else
		{
			BuildTeleportPointsView(transform);
		}
	}

	private void BuildTeleportMapView(Transform content)
	{
		CreateSection(content, "当前地图 GPS");
		TeleportMapSnapshot teleportMapSnapshot = _teleportPresenter.ScanCurrentMap();
		string text = (teleportMapSnapshot.IsGps ? PluginLocalization.Translate("GPS/卫星图") : (teleportMapSnapshot.IsChart ? PluginLocalization.Translate("地形图") : PluginLocalization.Translate("地图预览")));
		GameObject gameObject = CreateRow(content, 34f, 8f);
		CreateLabel(gameObject.transform, teleportMapSnapshot.IsAvailable ? $"{text} · {teleportMapSnapshot.MapName} · {teleportMapSnapshot.Texture.width}×{teleportMapSnapshot.Texture.height}" : teleportMapSnapshot.Error, 12, teleportMapSnapshot.IsAvailable ? CheatMenuStyle.Muted : CheatMenuStyle.Warning);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "扫描地图", delegate
		{
			ShowTab(MenuTab.Teleports);
			SetStatus((_teleportMapSurface != null) ? ("已重新扫描地图：" + _teleportPresenter.CurrentMap + "。支持 Map.png / Chart.png。") : "当前地图没有可用的 Map.png / Chart.png。", 5f, (_teleportMapSurface != null) ? StatusKind.Success : StatusKind.Warning);
		}, "重新从当前地图目录读取 Map.png 或 Chart.png。", 105f, 32f);
		if (!teleportMapSnapshot.IsAvailable)
		{
			CreateEmptyState(content, "当前地图没有可用 GPS 图层。", "请确认地图目录中存在 Map.png 或 Chart.png。");
			return;
		}
		string currentMap = _teleportPresenter.CurrentMap;
		TeleportPoint[] teleportPoints = _teleportPresenter.Points
			.Where(point => string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase))
			.ToArray();
		GameObject gameObject2 = CreateObject("TeleportMapSurface", content, typeof(Image));
		SetLayout(gameObject2, -1f, 480f, -1f, 1f);
		Outline outline = gameObject2.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.TeleportBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_teleportMapSurface = gameObject2.AddComponent<TeleportMapSurface>();
		_teleportMapSurface.Initialize(teleportMapSnapshot.Texture, delegate(Vector3 worldPosition)
		{
			bool flag = _teleportPresenter.TeleportToMapPosition(worldPosition);
			SetStatus(flag ? $"已传送到地图坐标 X {worldPosition.x:F1} · Z {worldPosition.z:F1}。" : "地图传送失败：未找到安全落点、目标超出地图边界或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
		}, teleportPoints, delegate(TeleportPoint point)
		{
			bool flag = _teleportPresenter.Teleport(point);
			SetStatus(flag ? ("已传送到 " + point.Name + "。") : "传送失败：目标位置有障碍物且无法放置在障碍物顶部，或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
		});
		GameObject gameObject3 = CreateRow(content, 34f, 8f);
		CreateFlexibleSpacer(gameObject3.transform);
		CreateButton(gameObject3.transform, "−", delegate
		{
			_teleportMapSurface?.ZoomOut();
		}, "缩小当前地图视图以显示更大范围。", 42f, 32f);
		CreateButton(gameObject3.transform, "＋", delegate
		{
			_teleportMapSurface?.ZoomIn();
		}, "放大当前地图视图以查看更小范围。", 42f, 32f);
		CreateButton(gameObject3.transform, "居中", delegate
		{
			if (_teleportPresenter.TryGetLocalPlayerPosition(out Vector3 playerPosition))
			{
				_teleportMapSurface?.CenterOnWorld(playerPosition);
			}
		}, "把地图视图中心移动到本地玩家当前位置。", 70f, 32f);
		CreateLabel(gameObject3.transform, "左键拖动 · 滚轮缩放 · 悬停标记查看信息 · 右键标记快速传送", 11, CheatMenuStyle.Muted, 0f, TextAnchor.MiddleRight);
	}

	private void BuildTeleportPointsView(Transform content)
	{
		if (!_teleportPresenter.TryGetLocalPlayerPosition(out Vector3 position))
		{
			CreateEmptyState(content, "玩家状态尚未准备完成。", "进入地图后即可保存和使用传送点。");
			return;
		}
		CreateSection(content, "保存当前位置");
		CreateLabel(content, $"地图 {_teleportPresenter.CurrentMap}    X {position.x:F1}    Y {position.y:F1}    Z {position.z:F1}", 12, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(content, 38f, 8f);
		CreateInput(gameObject.transform, _teleportName, delegate(string value)
		{
			_teleportName = value;
		}, 0f, 1f);
		Button markerPickerButton = null;
		markerPickerButton = CreateMarkerPickerButton(gameObject.transform, _teleportMarkerKind, delegate
		{
			ToggleTeleportMarkerDrawer(markerPickerButton);
		}, 108f, compact: true);
		CreateColorPickerButton(gameObject.transform);
		CreateButton(gameObject.transform, "保存位置", delegate
		{
			TeleportPoint teleportPoint = _teleportPresenter.AddCurrent(_teleportName, _teleportMarkerKind, ColorUtility.ToHtmlStringRGB(_teleportMarkerColor).Insert(0, "#"));
			if (teleportPoint != null)
			{
				_teleportName = string.Empty;
				SetStatus("已保存传送点：" + teleportPoint.Name + "。", 4f);
				ShowTab(MenuTab.Teleports);
			}
		}, "用输入的名称、标记样式和颜色保存当前玩家位置。", 120f, 35f);
		string currentMap = _teleportPresenter.CurrentMap;
		TeleportPoint[] array = _teleportPresenter.Points
			.Where(point => string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase))
			.ToArray();
		CreateSection(content, $"当前地图传送点（{array.Length}）");
		int num = Math.Max(1, Mathf.CeilToInt((float)array.Length / 6f));
		_teleportPage = Mathf.Clamp(_teleportPage, 0, num - 1);
		BuildPagination(content, _teleportPage, num, delegate(int value)
		{
			_teleportPage = value;
			ShowTab(MenuTab.Teleports);
		});
		if (array.Length == 0)
		{
			CreateEmptyState(content, "当前地图还没有保存的传送点。", "请先输入名称并保存当前位置；不同地图的传送点会自动隔离。");
			return;
		}
		int num2 = _teleportPage * 6;
		int num3 = Math.Min(num2 + 6, array.Length);
		for (int num4 = num2; num4 < num3; num4++)
		{
			TeleportPoint point = array[num4];
			GameObject gameObject2 = CreateRow(content, 62f, 8f);
			gameObject2.AddComponent<Image>().color = CheatMenuStyle.SurfaceInset;
			Outline outline = gameObject2.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.TeleportBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			HorizontalLayoutGroup component = gameObject2.GetComponent<HorizontalLayoutGroup>();
			component.padding = new RectOffset(13, 11, 7, 7);
			component.spacing = 10f;
			CreateTeleportMarkerImage(gameObject2.transform, point.MarkerKind, 28f, compact: false, ParseMarkerColor(point.MarkerColorHex));
			SetLayout(CreateText(gameObject2.transform, $"{point.Name}\n{point.Map}  ·  ({point.X:F1}, {point.Y:F1}, {point.Z:F1})", 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false).gameObject, -1f, -1f, 1f);
			CreateButton(gameObject2.transform, "传送", delegate
			{
				bool flag = _teleportPresenter.Teleport(point);
				SetStatus(flag ? ("已传送到 " + point.Name + "。") : "传送失败：目标被阻挡、地图不同或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
			}, string.Format(PluginLocalization.Translate("传送到保存点“{0}”；被阻挡、地图不符或在载具中时会失败。"), point.Name), 105f, 38f);
			Button[] teleportButtons = gameObject2.GetComponentsInChildren<Button>();
			if (teleportButtons.Length > 0)
			{
				teleportButtons[teleportButtons.Length - 1].interactable = string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase);
			}
			CreateButton(gameObject2.transform, "×", delegate
			{
				ShowTeleportDeleteConfirmation(point);
			}, string.Format(PluginLocalization.Translate("打开删除传送点“{0}”的确认窗口。"), point.Name), 44f, 38f, CheatMenuStyle.Danger, 20);
		}
	}

	private void SwitchTeleportView(TeleportView view)
	{
		_teleportView = view;
		_shellPresenter.SetLastTeleportView(view.ToString());
		ShowTab(MenuTab.Teleports);
		SetStatus((view == TeleportView.Map) ? "已切换到地图视图。" : "已切换到传送点视图。", 3f, StatusKind.Info);
	}

	private void ToggleTeleportMarkerDrawer(Button trigger)
	{
		if (_teleportMarkerDrawerOpen)
		{
			HideTeleportMarkerDrawer();
			return;
		}
		_teleportMarkerDrawerOpen = true;
		_teleportMarkerTriggerButton = trigger;
		BuildTeleportMarkerDrawer();
	}

	private void BuildTeleportMarkerDrawer()
	{
		if (_teleportMarkerTriggerButton == null || _root == null)
		{
			return;
		}
		if (_teleportMarkerDrawerRoot != null)
		{
			QueueForDestroy(_teleportMarkerDrawerRoot);
		}
		_teleportMarkerDrawerRoot = null;
		_teleportMarkerDrawerPopup = null;
		_teleportMarkerDrawerOpen = true;
		_teleportMarkerDrawerRoot = CreateObject("TeleportMarkerDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_teleportMarkerDrawerRoot, HideTeleportMarkerDrawer);
		_teleportMarkerDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _teleportMarkerDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_teleportMarkerDrawerPopup = _teleportMarkerDrawerRoot.GetComponent<RectTransform>();
		RectTransform teleportMarkerDrawerPopup = _teleportMarkerDrawerPopup;
		Vector2 anchorMin = (_teleportMarkerDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		teleportMarkerDrawerPopup.anchorMin = anchorMin;
		_teleportMarkerDrawerPopup.pivot = new Vector2(0f, 1f);
		_teleportMarkerDrawerPopup.sizeDelta = new Vector2(108f, 184f);
		VerticalLayoutGroup verticalLayoutGroup = _teleportMarkerDrawerRoot.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		TeleportMarkerKind[] markerKinds = MarkerKinds;
		foreach (TeleportMarkerKind teleportMarkerKind in markerKinds)
		{
			TeleportMarkerKind capturedMarkerKind = teleportMarkerKind;
			SetLayout(CreateMarkerPickerButton(_teleportMarkerDrawerRoot.transform, capturedMarkerKind, delegate
			{
				_teleportMarkerKind = capturedMarkerKind;
				HideTeleportMarkerDrawer();
				ShowTab(MenuTab.Teleports);
			}, 0f, compact: true, 1f).gameObject, -1f, 42f, 1f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_teleportMarkerDrawerPopup);
		PositionTeleportMarkerDrawer();
	}

	private void PositionTeleportMarkerDrawer()
	{
		if (!(_teleportMarkerTriggerButton == null) && !(_teleportMarkerDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _teleportMarkerTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			Vector2 sizeDelta = _teleportMarkerDrawerPopup.sizeDelta;
			sizeDelta.x = component.rect.width;
			_teleportMarkerDrawerPopup.sizeDelta = sizeDelta;
			RectTransform teleportMarkerDrawerPopup = _teleportMarkerDrawerPopup;
			Vector2 anchorMin = (_teleportMarkerDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
			teleportMarkerDrawerPopup.anchorMin = anchorMin;
			bool flag = localPoint.y - component2.rect.yMin < _teleportMarkerDrawerPopup.rect.height + 8f;
			_teleportMarkerDrawerPopup.pivot = (flag ? new Vector2(0f, 0f) : new Vector2(0f, 1f));
			_teleportMarkerDrawerPopup.anchoredPosition = (flag ? localPoint2 : localPoint);
		}
	}

	private void HideTeleportMarkerDrawer()
	{
		_teleportMarkerDrawerOpen = false;
		if (_teleportMarkerDrawerRoot != null)
		{
			QueueForDestroy(_teleportMarkerDrawerRoot);
		}
		_teleportMarkerDrawerRoot = null;
		_teleportMarkerDrawerPopup = null;
		_teleportMarkerTriggerButton = null;
	}

	private static MenuTab ParseMenuTab(string value)
	{
		if (!Enum.TryParse<MenuTab>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(MenuTab), result))
		{
			return MenuTab.Character;
		}
		return result;
	}

	private static TeleportView ParseTeleportView(string value)
	{
		if (!Enum.TryParse<TeleportView>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(TeleportView), result))
		{
			return TeleportView.Map;
		}
		return result;
	}

	private void ShowTeleportDeleteConfirmation(TeleportPoint point)
	{
		HideConfirmDialog();
		_confirmRoot = CreateObject("ConfirmOverlay", _uiRoot.ModalLayer, typeof(Image));
		Stretch(_confirmRoot.GetComponent<RectTransform>());
		_confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
		GameObject gameObject = CreateObject("ConfirmDialog", _confirmRoot.transform, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(470f, 190f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(22, 22, 18, 18);
		verticalLayoutGroup.spacing = 10f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		CreateText(gameObject.transform, "确认删除传送点？", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateText(gameObject.transform, "“" + point.Name + "”将从当前保存列表中删除。", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		GameObject gameObject2 = CreateRow(gameObject.transform, 38f, 8f);
		CreateFlexibleSpacer(gameObject2.transform);
		Button button = CreateButton(gameObject2.transform, "取消", HideConfirmDialog, "保留此传送点并关闭删除确认窗口。", 100f, 34f, CheatMenuStyle.Surface);
		CreateButton(gameObject2.transform, "删除", delegate
		{
			HideConfirmDialog();
			if (_teleportPresenter.Remove(point.Id))
			{
				SetStatus("已删除传送点：" + point.Name + "。", 4f);
				ShowTab(MenuTab.Teleports);
			}
		}, string.Format(PluginLocalization.Translate("永久删除传送点“{0}”。"), point.Name), 100f, 34f, CheatMenuStyle.Danger);
		_uiRoot.RegisterModal(_confirmRoot, HideConfirmDialog, button.gameObject);
	}

	private void HideConfirmDialog()
	{
		if (!(_confirmRoot == null))
		{
			QueueForDestroy(_confirmRoot);
			_confirmRoot = null;
		}
	}
}
