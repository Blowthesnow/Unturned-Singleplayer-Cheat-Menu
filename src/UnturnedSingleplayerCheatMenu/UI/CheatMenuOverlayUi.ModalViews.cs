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
	private void ShowAboutDialog()
	{
		HideAboutDialog();
		HideFontPicker();
		HideShortcutDialog();
		if (!(_root == null))
		{
			_aboutRoot = CreateObject("AboutOverlay", _uiRoot.ModalLayer, typeof(Image));
			Stretch(_aboutRoot.GetComponent<RectTransform>());
			_aboutRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);
			GameObject gameObject = CreateObject("AboutDialog", _aboutRoot.transform, typeof(Image));
			gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.InputBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(760f, 390f);
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(26, 26, 20, 20);
			verticalLayoutGroup.spacing = 9f;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			GameObject gameObject2 = CreateRow(gameObject.transform, 40f, 8f);
			CreateText(gameObject2.transform, "关于此插件", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateFlexibleSpacer(gameObject2.transform);
			Button button = CreateWindowCloseButton(gameObject2.transform, HideAboutDialog, "关闭关于窗口。");
			CreateText(gameObject.transform, "Unturned Singleplayer Cheat Menu v1.9.0", 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
			CreateText(gameObject.transform, "作者：Blowthesnow", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
			SetLayout(CreateText(gameObject.transform, "此插件完全免费。如果你在其他地方购买了此插件，请立刻申请退款。", 13, CheatMenuStyle.Warning, TextAnchor.MiddleLeft).gameObject, -1f, 46f);
			CreateText(gameObject.transform, "项目地址", 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateButton(gameObject.transform, "https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu", delegate
			{
				Application.OpenURL("https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu");
			}, "在系统浏览器中打开此插件的 GitHub 项目页面。", 0f, 36f, CheatMenuStyle.Surface, 11);
			CreateText(gameObject.transform, "爱发电赞助地址", 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateButton(gameObject.transform, "https://afdian.com/a/Blowthesnow", delegate
			{
				Application.OpenURL("https://afdian.com/a/Blowthesnow");
			}, "在系统浏览器中打开作者的爱发电支持页面。", 0f, 36f, CheatMenuStyle.Surface, 11);
			_uiRoot.RegisterModal(_aboutRoot, HideAboutDialog, button.gameObject);
		}
	}

	private void HideAboutDialog()
	{
		if (!(_aboutRoot == null))
		{
			QueueForDestroy(_aboutRoot);
			_aboutRoot = null;
		}
	}

	private void ShowFontPicker()
	{
		HideFontPicker();
		HideAboutDialog();
		HideShortcutDialog();
		if (_root == null)
		{
			return;
		}

		IReadOnlyList<SystemFontFamilyOption> fontFamilies = _shellPresenter.GetInstalledChineseFontFamilies();
		_fontPickerRoot = CreateObject("FontPickerOverlay", _uiRoot.ModalLayer, typeof(Image));
		Stretch(_fontPickerRoot.GetComponent<RectTransform>());
		_fontPickerRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);

		GameObject dialog = CreateObject("FontPickerDialog", _fontPickerRoot.transform, typeof(Image));
		dialog.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = dialog.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		RectTransform dialogRect = dialog.GetComponent<RectTransform>();
		Vector2 center = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
		dialogRect.anchorMin = center;
		dialogRect.pivot = center;
		dialogRect.sizeDelta = new Vector2(760f, 680f);

		VerticalLayoutGroup dialogLayout = dialog.AddComponent<VerticalLayoutGroup>();
		dialogLayout.padding = new RectOffset(24, 24, 18, 18);
		dialogLayout.spacing = 10f;
		dialogLayout.childControlWidth = true;
		dialogLayout.childControlHeight = true;
		dialogLayout.childForceExpandWidth = true;
		dialogLayout.childForceExpandHeight = false;

		GameObject header = CreateRow(dialog.transform, 40f, 8f);
		CreateText(header.transform, "选择界面字体", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(header.transform);
		Button closeButton = CreateWindowCloseButton(header.transform, HideFontPicker, "关闭字体选择窗口。");

		string selectedFamily = _shellPresenter.SelectedFontFamily;
		string selectedDisplayName = fontFamilies
			.FirstOrDefault(option => string.Equals(option.FamilyName, selectedFamily, StringComparison.OrdinalIgnoreCase))
			?.DisplayName ?? selectedFamily;
		CreateText(dialog.transform, PluginLocalization.Translate("当前字体：") + selectedDisplayName, 13,
			CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold, localize: false);
		CreateText(dialog.transform, "仅显示能够完整呈现插件常用简体中文字符的已安装系统字体。", 12,
			CheatMenuStyle.Muted, TextAnchor.MiddleLeft);

		GameObject listPanel = CreateObject("FontListPanel", dialog.transform, typeof(Image));
		listPanel.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		SetLayout(listPanel, -1f, -1f, 1f, 1f);
		RectTransform content = CreateShortcutScroll(listPanel.transform);
		ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();
		scrollRect.scrollSensitivity = 44f;
		AddShortcutScrollbar(scrollRect);
		AddShortcutScrollLayout(content.gameObject, 8f, new RectOffset(10, 18, 10, 10));

		GameObject preferredSelection = closeButton.gameObject;
		if (fontFamilies.Count == 0)
		{
			Text emptyText = CreateText(content, "未找到支持中文的系统字体。", 13,
				CheatMenuStyle.Warning, TextAnchor.MiddleCenter);
			SetLayout(emptyText.gameObject, -1f, 48f);
		}
		else
		{
			foreach (SystemFontFamilyOption fontOption in fontFamilies)
			{
				string capturedFamily = fontOption.FamilyName;
				string capturedDisplayName = fontOption.DisplayName;
				bool selected = string.Equals(capturedFamily, selectedFamily, StringComparison.OrdinalIgnoreCase);
				Button fontButton = CreateButton(content, capturedDisplayName, delegate
				{
					ApplyFontFamily(capturedFamily, capturedDisplayName);
				}, string.Format(PluginLocalization.Translate("将菜单界面字体切换为“{0}”。"), capturedDisplayName),
					0f, 40f, selected ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 13);
				Text label = fontButton.GetComponentInChildren<Text>();
				if (label != null)
				{
					label.alignment = TextAnchor.MiddleLeft;
					SetOffsets(label.rectTransform, 14f, 14f, 2f, 2f);
				}
				if (selected)
				{
					preferredSelection = fontButton.gameObject;
				}
			}
		}

		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		float preferredHeight = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(content));
		content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
		content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		CreateText(dialog.transform, string.Format(PluginLocalization.Translate("可用中文字体：{0} 个"), fontFamilies.Count),
			11, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		_uiRoot.RegisterModal(_fontPickerRoot, HideFontPicker, preferredSelection);
	}

	private void ApplyFontFamily(string fontFamily, string displayName)
	{
		if (!_shellPresenter.TrySetFontFamily(fontFamily, out bool persisted, out string error))
		{
			SetStatus(string.IsNullOrWhiteSpace(error) ? "字体切换失败。" : error, 6f, StatusKind.Error);
			return;
		}

		_font = _shellPresenter.ResolveFont();
		HideFontPicker();
		string message = persisted
			? "界面字体已切换为：" + displayName
			: (string.IsNullOrWhiteSpace(error) ? "界面字体已切换，但配置文件保存失败。" : error);
		SetStatus(message, 6f, persisted ? StatusKind.Success : StatusKind.Warning);
	}

	private void HideFontPicker()
	{
		if (_fontPickerRoot != null)
		{
			QueueForDestroy(_fontPickerRoot);
			_fontPickerRoot = null;
		}
	}

	private void ShowShortcutDialog()
	{
		HideShortcutDialog();
		HideAboutDialog();
		HideFontPicker();
		if (!(_root == null))
		{
			_shortcutRoot = CreateObject("ShortcutOverlay", _uiRoot.ModalLayer, typeof(Image));
			Stretch(_shortcutRoot.GetComponent<RectTransform>());
			_shortcutRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);
			GameObject gameObject = CreateObject("ShortcutDialog", _shortcutRoot.transform, typeof(Image));
			gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.InputBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(930f, 780f);
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(24, 24, 18, 18);
			verticalLayoutGroup.spacing = 10f;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			GameObject gameObject2 = CreateRow(gameObject.transform, 40f, 8f);
			CreateText(gameObject2.transform, "设置全局快捷键", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateFlexibleSpacer(gameObject2.transform);
			Button button = CreateWindowCloseButton(gameObject2.transform, HideShortcutDialog, "关闭快捷键设置窗口。");
			GameObject gameObject3 = CreateObject("ShortcutBody", gameObject.transform);
			SetLayout(gameObject3, -1f, -1f, 1f, 1f);
			GameObject gameObject4 = CreateObject("ShortcutNavigation", gameObject3.transform, typeof(Image));
			gameObject4.GetComponent<Image>().color = CheatMenuStyle.Surface;
			AddVerticalLayout(gameObject4, 6f, new RectOffset(8, 8, 8, 8));
			RectTransform component2 = gameObject4.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0f, 0f);
			component2.anchorMax = new Vector2(0f, 1f);
			component2.offsetMin = Vector2.zero;
			component2.offsetMax = new Vector2(142f, 0f);
			GameObject gameObject5 = CreateObject("ShortcutContentPanel", gameObject3.transform, typeof(Image));
			gameObject5.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
			RectTransform component3 = gameObject5.GetComponent<RectTransform>();
			component3.anchorMin = new Vector2(0f, 0f);
			component3.anchorMax = new Vector2(1f, 1f);
			component3.offsetMin = new Vector2(152f, 0f);
			component3.offsetMax = Vector2.zero;
			_shortcutScrollContent = CreateShortcutScroll(gameObject5.transform);
			_shortcutScrollRect = _shortcutScrollContent.GetComponentInParent<ScrollRect>();
			_shortcutScrollRect.scrollSensitivity = 48f;
			AddShortcutScrollbar(_shortcutScrollRect);
			AddShortcutScrollLayout(_shortcutScrollContent.gameObject, 10f, new RectOffset(12, 18, 10, 10));
			RectTransform pluginHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "插件窗口");
			CreateShortcutNavigationButton(gameObject4.transform, "插件窗口", pluginHeader);
			BuildShortcutActionRow(_shortcutScrollContent, "menu.toggle");
			BuildShortcutActionRow(_shortcutScrollContent, "infinite-storage.toggle");
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform minimapHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "小地图");
			CreateShortcutNavigationButton(gameObject4.transform, "小地图", minimapHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleMinimap);
			BuildFixedShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ZoomMinimapIn);
			BuildFixedShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ZoomMinimapOut);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform inventoryHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "背包整理");
			CreateShortcutNavigationButton(gameObject4.transform, "背包整理", inventoryHeader);
			BuildShortcutActionRow(_shortcutScrollContent, "inventory.arrange.current");
			BuildShortcutActionRow(_shortcutScrollContent, "inventory.arrange.all-carried");
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform characterHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "角色");
			CreateShortcutNavigationButton(gameObject4.transform, "角色", characterHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleGodMode);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleInfiniteNeeds);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.RestoreEverything);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.RefillNeeds);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform movementHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "移动");
			CreateShortcutNavigationButton(gameObject4.transform, "移动", movementHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFlight);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleNoclip);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform pointToolHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "准星");
			CreateShortcutNavigationButton(gameObject4.transform, "准星", pointToolHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.TogglePointToolEnabled);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.NextPointToolMode);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform healthBarsHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "生物生命条");
			CreateShortcutNavigationButton(gameObject4.transform, "生物生命条", healthBarsHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleCreatureHealthBars);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform automationHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "自动化");
			CreateShortcutNavigationButton(gameObject4.transform, "自动化", automationHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleAutoPickup);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleInfiniteStorageEnabled);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform worldHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "世界");
			CreateShortcutNavigationButton(gameObject4.transform, "世界", worldHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFreezeTime);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.SetDay);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.SetNight);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFullMoon);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ClearWeather);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.DisableWeather);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.CallAirdrop);
			_shortcutCaptureText = CreateText(gameObject.transform, "点击某一行的设置后按下键盘键、鼠标中键或鼠标侧键；Escape 取消。", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
			SetLayout(_shortcutCaptureText.gameObject, -1f, 42f);
			GameObject gameObject6 = CreateRow(gameObject.transform, 38f, 8f);
			CreateFlexibleSpacer(gameObject6.transform);
			CreateButton(gameObject6.transform, "关闭", HideShortcutDialog, "关闭快捷键设置窗口。", 104f, 34f, CheatMenuStyle.Surface);
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(component);
			LayoutRebuilder.ForceRebuildLayoutImmediate(gameObject3.GetComponent<RectTransform>());
			LayoutRebuilder.ForceRebuildLayoutImmediate(component3);
			RebuildShortcutScrollLayout();
			_shortcutScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_shortcutScrollPosition);
			_uiRoot.RegisterModal(_shortcutRoot, HideShortcutDialog, button.gameObject);
		}
	}

	private RectTransform CreateShortcutSectionHeader(Transform parent, string title)
	{
		CreateShortcutDivider(parent);
		RectTransform rectTransform = CreateText(parent, title, 13, CheatMenuStyle.Muted, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform;
		CreateShortcutDivider(parent);
		return rectTransform;
	}

	private void CreateShortcutNavigationButton(Transform parent, string title, RectTransform sectionHeader)
	{
		CreateButton(parent, title, delegate
		{
			ScrollShortcutSectionIntoView(sectionHeader);
		}, string.Format(PluginLocalization.Translate("滚动到“{0}”快捷键分组。"), PluginLocalization.Translate(title)), 0f, 34f, CheatMenuStyle.SurfaceHover);
	}

	private void ScrollShortcutSectionIntoView(RectTransform sectionHeader)
	{
		if (_shortcutScrollRect == null || _shortcutScrollContent == null || sectionHeader == null)
		{
			return;
		}
		RebuildShortcutScrollLayout();
		RectTransform viewport = _shortcutScrollRect.viewport;
		if (viewport == null)
		{
			return;
		}
		float scrollableHeight = _shortcutScrollContent.rect.height - viewport.rect.height;
		if (scrollableHeight <= 0.01f)
		{
			_shortcutScrollRect.verticalNormalizedPosition = 1f;
			return;
		}
		Vector3 worldPoint = sectionHeader.TransformPoint(new Vector3(sectionHeader.rect.center.x, sectionHeader.rect.yMax, 0f));
		float sectionOffset = 0f - _shortcutScrollContent.InverseTransformPoint(worldPoint).y;
		_shortcutScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(sectionOffset / scrollableHeight);
	}

	private void RebuildShortcutScrollLayout()
	{
		if (_shortcutScrollContent == null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_shortcutScrollContent);
		float preferredHeight = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(_shortcutScrollContent));
		_shortcutScrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
		_shortcutScrollContent.anchoredPosition = new Vector2(_shortcutScrollContent.anchoredPosition.x, 0f);
		LayoutRebuilder.ForceRebuildLayoutImmediate(_shortcutScrollContent);
		Canvas.ForceUpdateCanvases();
	}

	private static VerticalLayoutGroup AddShortcutScrollLayout(GameObject target, float spacing, RectOffset padding)
	{
		VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
		layout.padding = padding;
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		return layout;
	}

	private static RectTransform CreateShortcutScroll(Transform parent)
	{
		GameObject scrollObject = CreateObject("ShortcutScroll", parent, typeof(Image), typeof(ScrollRect));
		Stretch(scrollObject.GetComponent<RectTransform>());
		scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
		SetLayout(scrollObject, -1f, -1f, 1f, 1f);
		LayoutElement layoutElement = scrollObject.GetComponent<LayoutElement>();
		layoutElement.minWidth = 1f;
		layoutElement.minHeight = 1f;

		GameObject viewportObject = CreateObject("Viewport", scrollObject.transform, typeof(RectMask2D));
		RectTransform viewport = viewportObject.GetComponent<RectTransform>();
		Stretch(viewport);

		RectTransform content = CreateObject("ShortcutContent", viewport, typeof(RectTransform)).GetComponent<RectTransform>();
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = Vector2.zero;
		content.offsetMax = Vector2.zero;

		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		scrollRect.viewport = viewport;
		scrollRect.content = content;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 48f;
		return content;
	}

	private static void AddShortcutScrollbar(ScrollRect scrollRect)
	{
		if (scrollRect == null || scrollRect.viewport == null)
		{
			return;
		}
		scrollRect.viewport.offsetMax = new Vector2(-18f, 0f);
		GameObject scrollbarObject = CreateObject("ShortcutScrollbar", scrollRect.transform, typeof(Image), typeof(Scrollbar));
		RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
		scrollbarRect.anchorMin = new Vector2(1f, 0f);
		scrollbarRect.anchorMax = new Vector2(1f, 1f);
		scrollbarRect.pivot = new Vector2(1f, 0.5f);
		scrollbarRect.offsetMin = new Vector2(-14f, 4f);
		scrollbarRect.offsetMax = new Vector2(-2f, -4f);
		scrollbarObject.GetComponent<Image>().color = CheatMenuStyle.InputBorder;
		GameObject slidingArea = CreateObject("SlidingArea", scrollbarRect);
		RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
		Stretch(slidingRect);
		slidingRect.offsetMin = new Vector2(2f, 2f);
		slidingRect.offsetMax = new Vector2(-2f, -2f);
		GameObject handleObject = CreateObject("Handle", slidingRect, typeof(Image));
		RectTransform handleRect = handleObject.GetComponent<RectTransform>();
		Stretch(handleRect);
		Image handleImage = handleObject.GetComponent<Image>();
		handleImage.color = CheatMenuStyle.Accent;
		Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
		scrollbar.handleRect = handleRect;
		scrollbar.targetGraphic = handleImage;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		scrollRect.verticalScrollbar = scrollbar;
		scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
		scrollRect.verticalScrollbarSpacing = 4f;
	}

	private static void CreateShortcutDivider(Transform parent)
	{
		GameObject gameObject = CreateObject("ShortcutDivider", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.InputBorder;
		SetLayout(gameObject, -1f, 1f, 1f);
	}

	private static void CreateShortcutSectionSpacing(Transform parent)
	{
		SetLayout(CreateObject("ShortcutSectionSpacing", parent), -1f, 24f, 1f);
	}

	private void BuildShortcutActionRow(Transform parent, string actionId)
	{
		GameObject gameObject = CreateRow(parent, 40f, 8f);
		Text actionText = CreateText(gameObject.transform, _shellPresenter.GetShortcutDisplayName(actionId), 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft);
		actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(actionText.gameObject, 240f);
		Text shortcutText = CreateText(gameObject.transform, _shellPresenter.GetShortcutLabel(actionId), 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		shortcutText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(shortcutText.gameObject, 160f, 40f);
		CreateButton(gameObject.transform, "设置", delegate
		{
			BeginShortcutCapture(actionId);
		}, string.Format(PluginLocalization.Translate("等待输入并把新按键绑定到“{0}”。"), actionText.text), 82f, 34f, CheatMenuStyle.Accent);
		CreateButton(gameObject.transform, "清除", delegate
		{
			ApplyShortcut(new KeyboardShortcut(KeyCode.None), actionId);
		}, string.Format(PluginLocalization.Translate("清除“{0}”的当前快捷键绑定。"), actionText.text), 82f, 34f, CheatMenuStyle.Surface);
		CreateButton(gameObject.transform, "默认", delegate
		{
			ApplyShortcut(_shellPresenter.GetShortcutDefault(actionId), actionId);
		}, string.Format(PluginLocalization.Translate("将“{0}”恢复为默认快捷键。"), actionText.text), 82f, 34f, CheatMenuStyle.Surface);
	}

	private void BuildFixedShortcutActionRow(Transform parent, string actionId)
	{
		GameObject gameObject = CreateRow(parent, 40f, 8f);
		Text actionText = CreateText(gameObject.transform, _shellPresenter.GetShortcutDisplayName(actionId), 13,
			CheatMenuStyle.Text, TextAnchor.MiddleLeft);
		actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(actionText.gameObject, 240f);
		Text shortcutText = CreateText(gameObject.transform, _shellPresenter.GetShortcutLabel(actionId), 13,
			CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		shortcutText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(shortcutText.gameObject, 160f, 40f);
		Text fixedText = CreateText(gameObject.transform, "固定组合", 12, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
		SetLayout(fixedText.gameObject, 246f, 40f);
	}

	private void HideShortcutDialog()
	{
		_shortcutCaptureActive = false;
		_shortcutCaptureModifiers.Clear();
		if (_shortcutScrollRect != null)
		{
			_shortcutScrollPosition = _shortcutScrollRect.verticalNormalizedPosition;
		}
		_shortcutScrollRect = null;
		_shortcutScrollContent = null;
		_shortcutCaptureText = null;
		if (!(_shortcutRoot == null))
		{
			QueueForDestroy(_shortcutRoot);
			_shortcutRoot = null;
		}
	}

	private void BeginShortcutCapture(string actionId)
	{
		_shortcutCaptureModifiers.Clear();
		_shortcutCaptureActionId = actionId;
		_shortcutCaptureActive = true;
		if (_shortcutCaptureText != null)
		{
			_shortcutCaptureText.text = PluginLocalization.Translate("按键监听中，请按下键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，已按住的 Ctrl / Alt / Shift / Win 会作为组合键。");
		}
	}

	private void CaptureShortcutInput()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			HideShortcutDialog();
			return;
		}
		KeyCode[] modifierKeys = ShortcutInputPolicy.ModifierKeys;
		foreach (KeyCode key in modifierKeys)
		{
			if (Input.GetKey(key))
			{
				_shortcutCaptureModifiers.Add(ShortcutInputPolicy.NormalizeModifier(key));
			}
		}
		modifierKeys = ShortcutInputPolicy.KeyboardMainKeys;
		foreach (KeyCode keyCode in modifierKeys)
		{
			if (Input.GetKeyDown(keyCode))
			{
				ApplyShortcut(new KeyboardShortcut(keyCode, _shortcutCaptureModifiers.ToArray()), _shortcutCaptureActionId);
				return;
			}
		}
		modifierKeys = ShortcutInputPolicy.MouseKeys;
		foreach (KeyCode keyCode2 in modifierKeys)
		{
			int mouseButtonIndex = ShortcutInputPolicy.GetMouseButtonIndex(keyCode2);
			if (mouseButtonIndex >= 0 && Input.GetMouseButtonDown(mouseButtonIndex))
			{
				ApplyShortcut(new KeyboardShortcut(keyCode2, _shortcutCaptureModifiers.ToArray()), _shortcutCaptureActionId);
				break;
			}
		}
	}

	private void ApplyShortcut(KeyboardShortcut shortcut, string actionId)
	{
		bool flag = _shellPresenter.SetShortcut(actionId, shortcut);
		_shortcutCaptureActive = false;
		_shortcutCaptureModifiers.Clear();
		ShowShortcutDialog();
		RefreshHeader();
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		SetStatus(flag ? "全局快捷键已更新。" : "全局快捷键已更新，但配置文件保存失败。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
	}
}
