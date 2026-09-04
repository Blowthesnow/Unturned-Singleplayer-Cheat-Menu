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
	private void BuildInventoryArrangePanel(Transform panel)
	{
		InventoryArrangeSettings inventoryArrangeSettings = _inventoryPresenter.CaptureSettings();
		string text = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(inventoryArrangeSettings.Preset));
		CreateLabel(panel, "安全背包整理", 17, CheatMenuStyle.Text);
		CreateLabel(panel, "排序预设：" + text + "；默认禁止自动掉落。", 13, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(panel, 44f, 8f);
		CreateLabel(gameObject.transform, "排序预设", 14, CheatMenuStyle.Text, 110f);
		Button presetButton = null;
		presetButton = CreateButton(gameObject.transform, text + " ▼", delegate
		{
			ToggleInventoryPresetDrawer(presetButton);
		}, "点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。", 0f, 40f, CheatMenuStyle.Surface, 13);
		SetLayout(presetButton.gameObject, -1f, -1f, 1f);
		GameObject scopeRow = CreateRow(panel, 44f, 8f);
		CreateLabel(scopeRow.transform, "整理范围", 14, CheatMenuStyle.Text, 110f);
		Button scopeButton = null;
		scopeButton = CreateButton(scopeRow.transform, "配置随身页 ▼", delegate
		{
			ToggleInventoryScopeDrawer(scopeButton);
		}, "分别开启或关闭背包、背心、上衣和裤子储物页；关闭的页面不会参与整理。", 0f, 40f, CheatMenuStyle.Surface, 13);
		SetLayout(scopeButton.gameObject, -1f, -1f, 1f);
		if (inventoryArrangeSettings.Preset == InventorySortPreset.Custom)
		{
			CreateLabel(panel, "自定义排序链（最多 5 项）：Key:Direction,Key:Direction", 12, CheatMenuStyle.Muted);
			CreateInput(panel, _inventoryPresenter.CustomKeys, delegate(string value)
			{
				_inventoryPresenter.SetCustomKeys(value);
			}, 0f, 1f);
		}
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "自动旋转", inventoryArrangeSettings.AutoRotate, delegate(bool value)
		{
			_inventoryPresenter.SetAutoRotate(value);
			SetStatus(value ? "整理将允许自动旋转物品。" : "整理已禁止自动旋转物品。", 4f, StatusKind.Info);
		}, "允许物品在寻找位置时旋转 90°；关闭后保持原旋转。");
		CreateToggleAction(gameObject2.transform, "允许跨页", inventoryArrangeSettings.AllowCrossPage, delegate(bool value)
		{
			_inventoryPresenter.SetCrossPage(value);
			SetStatus(value ? "整理将允许在当前范围内跨页安置。" : "整理将保持原页范围。", 4f, StatusKind.Info);
		}, "允许在当前整理范围的随身页之间寻找空位；关闭后保持原页范围。");
		GameObject gameObject3 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject3.transform, "保护装备", inventoryArrangeSettings.ProtectEquipped, delegate(bool value)
		{
			_inventoryPresenter.SetProtectEquipped(value);
			SetStatus(value ? "已保护当前装备物品。" : "将允许整理当前装备物品。", 4f, StatusKind.Info);
		}, "保护当前装备物品；开启时不会移除或重排它。");
		CreateToggleAction(gameObject3.transform, "固定热键物品", inventoryArrangeSettings.FixHotkeyItems, delegate(bool value)
		{
			_inventoryPresenter.SetFixHotkeyItems(value);
			SetStatus(value ? "热键绑定物品将保持在原坐标。" : "热键绑定物品会随整理移动并更新坐标。", 4f, StatusKind.Info);
		}, "开启时固定绑定热键的物品；关闭时整理会同步快捷键到新坐标。");
		CreateLabel(panel, "安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。", 12, CheatMenuStyle.Warning);
		CreateLabel(panel, "跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。", 12, CheatMenuStyle.Muted);
		GameObject gameObject4 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject4.transform, "预览当前", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, preview: true);
		}, "只生成移动、归并和跨页摘要，不修改库存。", 120f, 34f, CheatMenuStyle.Surface, 13);
		CreateButton(gameObject4.transform, "整理当前", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, preview: false);
		}, "整理当前上下文：打开储物箱时整理储物箱，否则整理随身容器。", 120f, 34f, CheatMenuStyle.Accent, 13);
		CreateButton(gameObject4.transform, "整理全部随身", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.AllCarried, InventoryArrangeAction.Arrange, preview: false);
		}, "整理所有随身页，不处理当前储物箱；默认保护装备并固定热键物品。", 150f, 34f, CheatMenuStyle.Accent, 13);
		GameObject gameObject5 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject5.transform, "仅压缩", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Compact, preview: false);
		}, "只压缩空间并保持相对顺序；不按普通预设重排，自动补弹仍会执行。", 120f, 34f, CheatMenuStyle.Surface, 13);
		CreateButton(gameObject5.transform, "撤销上次整理", ShowInventoryArrangeUndo, "撤销最近一次成功整理；外部改变库存后撤销会失效。", 150f, 34f, CheatMenuStyle.Surface, 13);
		CreateButton(gameObject5.transform, "仅归并", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Merge, preview: false);
		}, "只归并兼容堆叠，不重排无关物品；只按相同容器身份或直接 FillTargetItem 来源→目标关系补弹。", 120f, 34f, CheatMenuStyle.Surface, 13);
		CreateLabel(gameObject5.transform, "快捷键可在窗口右下角的全局快捷键中设置。", 12, CheatMenuStyle.Muted);
	}

	private void ToggleInventoryPresetDrawer(Button trigger)
	{
		if (_inventoryPresetDrawerOpen)
		{
			HideInventoryPresetDrawer();
			return;
		}
		_inventoryPresetDrawerOpen = true;
		_inventoryPresetTriggerButton = trigger;
		BuildInventoryPresetDrawer();
	}

	private void BuildInventoryPresetDrawer()
	{
		if (_inventoryPresetTriggerButton == null || _root == null)
		{
			return;
		}
		if (_inventoryPresetDrawerRoot != null)
		{
			QueueForDestroy(_inventoryPresetDrawerRoot);
		}
		_inventoryPresetDrawerRoot = CreateObject("InventoryPresetDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_inventoryPresetDrawerRoot, HideInventoryPresetDrawer);
		Image drawerImage = _inventoryPresetDrawerRoot.GetComponent<Image>();
		drawerImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(_inventoryPresetDrawerRoot);
		_fluentControls.StyleSurface(_inventoryPresetDrawerRoot, drawerImage, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		Outline outline = _inventoryPresetDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_inventoryPresetDrawerPopup = _inventoryPresetDrawerRoot.GetComponent<RectTransform>();
		RectTransform inventoryPresetDrawerPopup = _inventoryPresetDrawerPopup;
		Vector2 anchorMin = (_inventoryPresetDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		inventoryPresetDrawerPopup.anchorMin = anchorMin;
		_inventoryPresetDrawerPopup.pivot = new Vector2(0.5f, 1f);
		InventorySortPreset[] availablePresets = InventorySortRegistry.AvailablePresets;
		int num = Mathf.CeilToInt((float)availablePresets.Length / 3f);
		_inventoryPresetDrawerPopup.sizeDelta = new Vector2(352f, CheatMenuStyle.Theme.Space4 + (float)num * CheatMenuStyle.Theme.ControlCompact + (float)Math.Max(0, num - 1) * CheatMenuStyle.Theme.Space1);
		GridLayoutGroup gridLayoutGroup = _inventoryPresetDrawerRoot.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(8, 8, 8, 8);
		gridLayoutGroup.spacing = new Vector2(4f, 4f);
		gridLayoutGroup.cellSize = new Vector2(112f, CheatMenuStyle.Theme.ControlCompact);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 3;
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		InventoryArrangeSettings inventoryArrangeSettings = _inventoryPresenter.CaptureSettings();
		InventorySortPreset[] array = availablePresets;
		foreach (InventorySortPreset inventorySortPreset in array)
		{
			InventorySortPreset capturedPreset = inventorySortPreset;
			string label = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(capturedPreset));
			SetLayout(CreateButton(_inventoryPresetDrawerRoot.transform, label, delegate
			{
				_inventoryPresenter.SetPreset(capturedPreset);
				HideInventoryPresetDrawer();
				RefreshActiveToolsPageContent();
			}, InventoryPresetTooltip(capturedPreset), 0f, CheatMenuStyle.Theme.ControlCompact, (capturedPreset == inventoryArrangeSettings.Preset) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 10).gameObject, -1f, CheatMenuStyle.Theme.ControlCompact);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryPresetDrawerPopup);
		_inventoryPresetDrawerRoot.transform.SetAsLastSibling();
		PositionInventoryPresetDrawer();
		AnimatePopupEnter(_inventoryPresetDrawerRoot, _inventoryPresetDrawerPopup);
	}

	private void MaintainInventoryPresetDrawer()
	{
		if (!(_inventoryPresetDrawerRoot == null))
		{
			PositionInventoryPresetDrawer();
			if (Input.GetMouseButtonDown(0) && _inventoryPresetTriggerButton != null && _inventoryPresetDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryPresetDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryPresetTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideInventoryPresetDrawer();
			}
		}
	}

	private void PositionInventoryPresetDrawer()
	{
		if (!(_inventoryPresetTriggerButton == null) && !(_inventoryPresetDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _inventoryPresetTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			Vector2 screenPoint3 = RectTransformUtility.WorldToScreenPoint(null, array[2]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint3, null, out var localPoint3);
			Vector2 sizeDelta = _inventoryPresetDrawerPopup.sizeDelta;
			float value = (localPoint.x + localPoint3.x) * 0.5f;
			bool flag = localPoint.y - component2.rect.yMin < sizeDelta.y + 8f;
			_inventoryPresetDrawerPopup.pivot = (flag ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f));
			float min = component2.rect.xMin + sizeDelta.x * 0.5f + 8f;
			float max = component2.rect.xMax - sizeDelta.x * 0.5f - 8f;
			float x = Mathf.Clamp(value, min, max);
			float y = (flag ? localPoint2.y : localPoint.y);
			SetPopupMotionPosition(_inventoryPresetDrawerPopup, new Vector2(x, y));
		}
	}

	private void HideInventoryPresetDrawer()
	{
		_inventoryPresetDrawerOpen = false;
		if (_inventoryPresetDrawerRoot != null)
		{
			GameObject root = _inventoryPresetDrawerRoot;
			_inventoryPresetDrawerRoot = null;
			QueuePopupExit(root, root.transform);
		}
		_inventoryPresetDrawerPopup = null;
		_inventoryPresetTriggerButton = null;
	}

	private void ToggleInventoryScopeDrawer(Button trigger)
	{
		if (_inventoryScopeDrawerOpen)
		{
			HideInventoryScopeDrawer();
			return;
		}
		_inventoryScopeDrawerOpen = true;
		_inventoryScopeTriggerButton = trigger;
		BuildInventoryScopeDrawer();
	}

	private void BuildInventoryScopeDrawer()
	{
		if (_inventoryScopeTriggerButton == null || _root == null)
		{
			return;
		}
		if (_inventoryScopeDrawerRoot != null)
		{
			QueueForDestroy(_inventoryScopeDrawerRoot);
		}
		_inventoryScopeDrawerRoot = CreateObject("InventoryScopeDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_inventoryScopeDrawerRoot, HideInventoryScopeDrawer);
		Image scopeDrawerImage = _inventoryScopeDrawerRoot.GetComponent<Image>();
		scopeDrawerImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(_inventoryScopeDrawerRoot);
		_fluentControls.StyleSurface(_inventoryScopeDrawerRoot, scopeDrawerImage, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		Outline outline = _inventoryScopeDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_inventoryScopeDrawerPopup = _inventoryScopeDrawerRoot.GetComponent<RectTransform>();
		_inventoryScopeDrawerPopup.anchorMin = new Vector2(0.5f, 0.5f);
		_inventoryScopeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
		_inventoryScopeDrawerPopup.pivot = new Vector2(0.5f, 1f);
		_inventoryScopeDrawerPopup.sizeDelta = new Vector2(248f, 218f);
		VerticalLayoutGroup layout = _inventoryScopeDrawerRoot.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(8, 8, 8, 8);
		layout.spacing = 4f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		Text title = CreateLabel(_inventoryScopeDrawerRoot.transform, "选择参与整理的随身页", 13, CheatMenuStyle.Text);
		SetLayout(title.gameObject, -1f, 24f, 1f);
		InventoryArrangeSettings settings = _inventoryPresenter.CaptureSettings();
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "背包", settings.IncludeBackpackPage, delegate(bool value)
		{
			_inventoryPresenter.SetIncludeBackpackPage(value);
			SetStatus(value ? "背包页已纳入整理范围。" : "背包页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理背包储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "背心", settings.IncludeVestPage, delegate(bool value)
		{
			_inventoryPresenter.SetIncludeVestPage(value);
			SetStatus(value ? "背心页已纳入整理范围。" : "背心页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理背心储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "上衣", settings.IncludeShirtPage, delegate(bool value)
		{
			_inventoryPresenter.SetIncludeShirtPage(value);
			SetStatus(value ? "上衣页已纳入整理范围。" : "上衣页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理上衣储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "裤子", settings.IncludePantsPage, delegate(bool value)
		{
			_inventoryPresenter.SetIncludePantsPage(value);
			SetStatus(value ? "裤子页已纳入整理范围。" : "裤子页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理裤子储物页；关闭后该页物品和位置保持不变。");
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryScopeDrawerPopup);
		_inventoryScopeDrawerRoot.transform.SetAsLastSibling();
		PositionInventoryScopeDrawer();
		AnimatePopupEnter(_inventoryScopeDrawerRoot, _inventoryScopeDrawerPopup);
	}

	private void MaintainInventoryScopeDrawer()
	{
		if (_inventoryScopeDrawerRoot != null)
		{
			PositionInventoryScopeDrawer();
			if (Input.GetMouseButtonDown(0) && _inventoryScopeTriggerButton != null && _inventoryScopeDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryScopeDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryScopeTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideInventoryScopeDrawer();
			}
		}
	}

	private void PositionInventoryScopeDrawer()
	{
		if (_inventoryScopeTriggerButton != null && _inventoryScopeDrawerPopup != null && _root != null)
		{
			RectTransform trigger = _inventoryScopeTriggerButton.GetComponent<RectTransform>();
			RectTransform root = _root.GetComponent<RectTransform>();
			Vector3[] corners = new Vector3[4];
			trigger.GetWorldCorners(corners);
			Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
			Vector2 top = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
			Vector2 rightTop = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, bottom, null, out var localBottom);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, top, null, out var localTop);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, rightTop, null, out var localRightTop);
			Vector2 size = _inventoryScopeDrawerPopup.sizeDelta;
			float centerX = (localBottom.x + localRightTop.x) * 0.5f;
			bool placeBelow = localBottom.y - root.rect.yMin < size.y + 8f;
			_inventoryScopeDrawerPopup.pivot = placeBelow ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
			float minX = root.rect.xMin + size.x * 0.5f + 8f;
			float maxX = root.rect.xMax - size.x * 0.5f - 8f;
			float x = Mathf.Clamp(centerX, minX, maxX);
			float y = placeBelow ? localTop.y : localBottom.y;
			SetPopupMotionPosition(_inventoryScopeDrawerPopup, new Vector2(x, y));
		}
	}

	private void HideInventoryScopeDrawer()
	{
		_inventoryScopeDrawerOpen = false;
		if (_inventoryScopeDrawerRoot != null)
		{
			GameObject root = _inventoryScopeDrawerRoot;
			_inventoryScopeDrawerRoot = null;
			QueuePopupExit(root, root.transform);
		}
		_inventoryScopeDrawerPopup = null;
		_inventoryScopeTriggerButton = null;
	}

	private static string InventoryPresetTooltip(InventorySortPreset preset)
	{
		return preset switch
		{
			InventorySortPreset.Compatibility => "按类型、占用格数和名称整理，适合平时的一键整理。",
			InventorySortPreset.Compact => "尽量把物品挤到前面，保持原来的先后顺序；每次整理仍会按直接 FillTargetItem 关系安全补弹。",
			InventorySortPreset.Type => "把同类物品放在一起，例如武器、衣服、食物和工具。",
			InventorySortPreset.Name => "按物品名称从 A 到 Z 排列，方便按名字找东西。",
			InventorySortPreset.LargeFirst => "先放大件，再用小件填空位，适合整理占格较大的物品。",
			InventorySortPreset.SmallFirst => "先放小件，尽量留下连续的大空位。",
			InventorySortPreset.Quality => "耐久或品质高的物品排在前面。",
			InventorySortPreset.Rarity => "按稀有度从神话、传奇、史诗、稀有、罕见到普通排列。",
			InventorySortPreset.StackFill => "相同物品会自动合堆；兼容的半弹匣或弹药箱会重新分配成满的和空的。",
			InventorySortPreset.Ammo => "剩余弹量多的弹匣或弹药箱排在前面；读不到弹量的物品不会乱改。",
			InventorySortPreset.Hotkey => "绑定快捷栏的物品优先；开启固定热键物品时，绑定物品不会被移动。",
			InventorySortPreset.LowMovement => "尽量保持原来的页、位置和旋转，只做必要的整理。",
			InventorySortPreset.Custom => "按下面输入框里的排序链执行，例如 Name:Descending,Area:Ascending。",
			_ => "按安全的默认规则整理。",
		};
	}

	private void ShowInventoryArrangeResult(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview)
	{
		InventoryArrangeResult inventoryArrangeResult = _inventoryPresenter.Execute(scope, action, preview);
		SetStatus(inventoryArrangeResult?.Message ?? "背包整理尚未准备完成。", 6f, (inventoryArrangeResult != null && inventoryArrangeResult.Success) ? StatusKind.Success : StatusKind.Warning);
	}

	private void ShowInventoryArrangeUndo()
	{
		InventoryArrangeResult inventoryArrangeResult = _inventoryPresenter.Undo();
		SetStatus(inventoryArrangeResult?.Message ?? "没有可撤销的整理记录。", 6f, (inventoryArrangeResult != null && inventoryArrangeResult.Success) ? StatusKind.Success : StatusKind.Warning);
	}
}
