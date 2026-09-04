using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private void RebuildVisualTreesPreservingVisibility()
	{
		bool rebuildShell = _root != null;
		bool reopenShell = rebuildShell && _root.activeSelf;
		bool rebuildInfiniteStorage = _infiniteStorageRoot != null;
		bool reopenInfiniteStorage = rebuildInfiniteStorage && _infiniteStorageRoot.activeSelf;
		RunWithoutTransientMotion(CloseTransientVisualSurfaces);
		if (rebuildShell)
		{
			RebuildShellVisualTree(reopenShell);
		}
		if (rebuildInfiniteStorage)
		{
			RebuildInfiniteStorageVisualTree(reopenInfiniteStorage);
		}
	}

	private void RebuildShellVisualTree(bool reopen)
	{
		GameObject root = _root;
		GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		bool replacePluginSelection = SelectionBelongsToRoot(selected, root);
		_pageViewHost?.DetachForAncestorDestroy();
		_pageViewHost = null;
		_toolsPageViewHost?.DetachForAncestorDestroy();
		_toolsPageViewHost = null;
		if (root != null)
		{
			QueueForDestroy(root);
		}
		DisposeSearchInputs();
		ResetShellVisualReferences();
		_fluentHandles.Clear();
		EnsureShell();
		RefreshHeader();
		_root.SetActive(reopen);
		GameObject preferredSelection = null;
		if (reopen)
		{
			ShowTab(_activeTab);
			if (_tabButtons.TryGetValue(_activeTab, out Button activeTabButton))
			{
				preferredSelection = activeTabButton.gameObject;
			}
			if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
			{
				RefreshWorkshopLocalizationPageValues();
			}
		}
		RestoreSelectionAfterRebuild(replacePluginSelection, reopen, preferredSelection, _root);
	}

	private void RebuildInfiniteStorageVisualTree(bool reopen)
	{
		GameObject root = _infiniteStorageRoot;
		GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		bool replacePluginSelection = SelectionBelongsToRoot(selected, root);
		if (root != null)
		{
			QueueForDestroy(root);
		}
		_infiniteStorageRoot = null;
		_infiniteStoragePanel = null;
		_infiniteStorageContentHost = null;
		_infiniteStorageContentRow = null;
		_infiniteStorageCategoryHost = null;
		_infiniteStorageResultsHost = null;
		_infiniteStorageFilterButtonText = null;
		_infiniteStorageSummaryText = null;
		EnsureInfiniteStorageShell();
		_infiniteStorageRoot.SetActive(reopen);
		if (reopen)
		{
			RefreshInfiniteStorageGeometry();
			RebuildInfiniteStoragePage();
		}
		RestoreSelectionAfterRebuild(replacePluginSelection, reopen, preferredSelection: null, _infiniteStorageRoot);
	}

	private static bool SelectionBelongsToRoot(GameObject selected, GameObject root)
	{
		return selected != null && root != null &&
			(selected == root || selected.transform.IsChildOf(root.transform));
	}

	private static void RestoreSelectionAfterRebuild(bool replacePluginSelection, bool reopen, GameObject preferredSelection, GameObject rebuiltRoot)
	{
		if (!replacePluginSelection || EventSystem.current == null)
		{
			return;
		}
		if (!reopen)
		{
			EventSystem.current.SetSelectedGameObject(null);
			return;
		}

		GameObject target = preferredSelection;
		Selectable selectable = target != null ? target.GetComponent<Selectable>() : null;
		if (target == null || !target.activeInHierarchy || selectable == null || !selectable.interactable)
		{
			selectable = rebuiltRoot != null ? rebuiltRoot.GetComponentInChildren<Selectable>(includeInactive: false) : null;
			target = selectable != null && selectable.interactable ? selectable.gameObject : null;
		}
		EventSystem.current.SetSelectedGameObject(target);
	}

	private void CloseTransientVisualSurfaces()
	{
		HideHoverExplanation();
		if (_hoverExplanationRoot != null)
		{
			QueueForDestroy(_hoverExplanationRoot);
			_hoverExplanationRoot = null;
			_hoverExplanationText = null;
		}
		HideVehicleThumbnailSettings();
		HideTeleportColorPicker();
		HideItemFilterPanel();
		HideAutoPickupBlacklistPicker();
		HideAutoPickupRarityPicker();
		HideAboutDialog();
		HideFontPicker();
		HideShortcutDialog();
		HideTeleportMarkerDrawer();
		HidePointToolModeDrawer();
		HideInventoryPresetDrawer();
		HideInventoryScopeDrawer();
		HideWorkshopProtocolDrawer();
		HideConfirmDialog();
	}

	private void ResetShellVisualReferences()
	{
		_root = null;
		_panel = null;
		_shellBody = null;
		_narrowShellBody = null;
		_pageRegion = null;
		_navigationViewport = null;
		_navigationContent = null;
		_narrowNavigationContent = null;
		_navigationScroll = null;
		_wideBodyLayout = null;
		_narrowBodyLayout = null;
		_wideNavigationLayout = null;
		_narrowNavigationLayout = null;
		_navigationFitter = null;
		_narrowNavigationFitter = null;
		_wideShellLayout = null;
		_contentHost = null;
		_itemResultsHost = null;
		_vehicleResultsHost = null;
		_favoriteResultsHost = null;
		_mapAndShortcutText = null;
		_statusText = null;
		_statusAccent = null;
		_aboutRoot = null;
		_fontPickerRoot = null;
		_shortcutRoot = null;
		_shortcutScrollRect = null;
		_shortcutScrollContent = null;
		_toolsPageHost = null;
		_toolsPageContentHost = null;
		_workshopLocalizationScrollRect = null;
		_workshopLocalizationScrollContent = null;
		_shortcutCaptureText = null;
		_shortcutCaptureModifiers.Clear();
		_shortcutCaptureActive = false;
		_lastHeaderMap = string.Empty;
		_lastHeaderShortcut = string.Empty;
		_tabButtons.Clear();
		_toolsTabButtons.Clear();
	}

	private void BuildTabs(Transform parent)
	{
		_navigationViewport = CreateObject("MainTabScroll", parent, typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
		Image viewportImage = _navigationViewport.GetComponent<Image>();
		viewportImage.color = CheatMenuStyle.SurfaceInset;
		_fluentControls.StyleSurface(_navigationViewport, viewportImage, FluentSurfaceRole.Navigation, CheatMenuStyle.Theme.RadiusControl);
		RectTransform viewportRect = _navigationViewport.GetComponent<RectTransform>();
		GameObject content = CreateObject("MainTabContentWide", _navigationViewport.transform);
		_navigationContent = content.GetComponent<RectTransform>();
		_wideNavigationLayout = content.AddComponent<VerticalLayoutGroup>();
		_wideNavigationLayout.padding = new RectOffset(8, 8, 8, 8);
		_wideNavigationLayout.spacing = CheatMenuStyle.Theme.Space2;
		_wideNavigationLayout.childAlignment = TextAnchor.UpperCenter;
		_wideNavigationLayout.childControlWidth = true;
		_wideNavigationLayout.childControlHeight = true;
		_wideNavigationLayout.childForceExpandWidth = true;
		_wideNavigationLayout.childForceExpandHeight = false;
		_navigationFitter = content.AddComponent<ContentSizeFitter>();
		_navigationFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		_navigationFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		GameObject narrowContent = CreateObject("MainTabContentNarrow", _navigationViewport.transform);
		_narrowNavigationContent = narrowContent.GetComponent<RectTransform>();
		_narrowNavigationLayout = narrowContent.AddComponent<HorizontalLayoutGroup>();
		_narrowNavigationLayout.padding = new RectOffset(8, 8, 4, 4);
		_narrowNavigationLayout.spacing = CheatMenuStyle.Theme.Space2;
		_narrowNavigationLayout.childAlignment = TextAnchor.MiddleCenter;
		_narrowNavigationLayout.childControlWidth = true;
		_narrowNavigationLayout.childControlHeight = true;
		_narrowNavigationLayout.childForceExpandWidth = false;
		_narrowNavigationLayout.childForceExpandHeight = false;
		_narrowNavigationFitter = narrowContent.AddComponent<ContentSizeFitter>();
		_narrowNavigationFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		_narrowNavigationFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
		narrowContent.SetActive(value: false);
		_navigationScroll = _navigationViewport.GetComponent<ScrollRect>();
		_navigationScroll.viewport = viewportRect;
		_navigationScroll.content = _navigationContent;
		_navigationScroll.movementType = ScrollRect.MovementType.Clamped;
		_navigationScroll.scrollSensitivity = 32f;
		AddTab(content.transform, MenuTab.Character, "角色");
		AddTab(content.transform, MenuTab.Items, "物品");
		AddTab(content.transform, MenuTab.Vehicles, "载具");
		AddTab(content.transform, MenuTab.Favorites, "收藏");
		AddTab(content.transform, MenuTab.Teleports, "传送");
		AddTab(content.transform, MenuTab.Tools, "工具");
		AddTab(content.transform, MenuTab.Other, "其他");
	}

	private void AddTab(Transform parent, MenuTab tab, string label)
	{
		Button button = CreateButton(parent, label, delegate
		{
			ShowTab(tab, invalidate: false);
		}, string.Format(PluginLocalization.Translate("切换到“{0}”主页面。"), PluginLocalization.Translate(label)), 140f, 40f, CheatMenuStyle.Surface, 15);
		SetLayout(button.gameObject, 140f, 40f, 0f, 0f);
		_fluentControls.MarkNavigation(button);
		_tabButtons[tab] = button;
	}

	private void ShowTab(MenuTab tab, bool invalidate = true)
	{
		bool replacingView = invalidate || _activeTab != tab;
		_activeTab = tab;
		_shellPresenter.SetLastMainTab(tab.ToString());
		RunWithoutTransientMotion(delegate
		{
			HideHoverExplanation();
			HidePointToolModeDrawer();
			HideInventoryPresetDrawer();
			HideInventoryScopeDrawer();
			HideWorkshopProtocolDrawer();
			HideAutoPickupBlacklistPicker();
			HideAutoPickupRarityPicker();
		});
		if (replacingView)
		{
			ResetCatalogPageViewReferences();
		}
		_contentHost = _pageViewHost.Activate(tab, invalidate);
		foreach (KeyValuePair<MenuTab, Button> tabButton in _tabButtons)
		{
			Color color = tabButton.Key == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
			OverlaySelectableVisual component = tabButton.Value.GetComponent<OverlaySelectableVisual>();
			if (component != null)
			{
				component.SetBaseColor(color);
			}
			else
			{
				tabButton.Value.GetComponent<Image>().color = color;
			}
		}
	}

	private void RefreshShellLayout()
	{
		if (_panel == null || _shellBody == null || _narrowShellBody == null || _pageRegion == null ||
			_navigationViewport == null || _navigationContent == null || _narrowNavigationContent == null ||
			_navigationScroll == null || _wideBodyLayout == null || _narrowBodyLayout == null ||
			_wideNavigationLayout == null || _narrowNavigationLayout == null ||
			_navigationFitter == null || _narrowNavigationFitter == null)
		{
			return;
		}

		bool wide = _panel.rect.width >= 1040f;
		_wideShellLayout = wide;
		GameObject activeBody = wide ? _shellBody : _narrowShellBody;
		GameObject inactiveBody = wide ? _narrowShellBody : _shellBody;
		activeBody.SetActive(value: true);
		_navigationViewport.transform.SetParent(activeBody.transform, worldPositionStays: false);
		_pageRegion.transform.SetParent(activeBody.transform, worldPositionStays: false);
		_navigationViewport.transform.SetSiblingIndex(0);
		_pageRegion.transform.SetSiblingIndex(1);
		inactiveBody.SetActive(value: false);

		RectTransform activeNavigationContent = wide ? _navigationContent : _narrowNavigationContent;
		RectTransform inactiveNavigationContent = wide ? _narrowNavigationContent : _navigationContent;
		activeNavigationContent.gameObject.SetActive(value: true);
		foreach (Button tab in _tabButtons.Values)
		{
			tab.transform.SetParent(activeNavigationContent, worldPositionStays: false);
		}
		inactiveNavigationContent.gameObject.SetActive(value: false);
		_navigationScroll.content = activeNavigationContent;

		LayoutElement viewportLayout = _navigationViewport.GetComponent<LayoutElement>();
		if (viewportLayout == null)
		{
			viewportLayout = _navigationViewport.AddComponent<LayoutElement>();
		}
		if (wide)
		{
			viewportLayout.minWidth = CheatMenuStyle.MainNavigationWidth;
			viewportLayout.preferredWidth = CheatMenuStyle.MainNavigationWidth;
			viewportLayout.flexibleWidth = 0f;
			viewportLayout.minHeight = -1f;
			viewportLayout.preferredHeight = -1f;
			viewportLayout.flexibleHeight = 1f;
			activeNavigationContent.anchorMin = new Vector2(0f, 1f);
			activeNavigationContent.anchorMax = new Vector2(1f, 1f);
			activeNavigationContent.pivot = new Vector2(0.5f, 1f);
			activeNavigationContent.offsetMin = Vector2.zero;
			activeNavigationContent.offsetMax = Vector2.zero;
			_navigationScroll.horizontal = false;
			_navigationScroll.vertical = true;
		}
		else
		{
			viewportLayout.minWidth = -1f;
			viewportLayout.preferredWidth = -1f;
			viewportLayout.flexibleWidth = 1f;
			viewportLayout.minHeight = 44f;
			viewportLayout.preferredHeight = 44f;
			viewportLayout.flexibleHeight = 0f;
			activeNavigationContent.anchorMin = new Vector2(0f, 0f);
			activeNavigationContent.anchorMax = new Vector2(0f, 1f);
			activeNavigationContent.pivot = new Vector2(0f, 0.5f);
			activeNavigationContent.offsetMin = Vector2.zero;
			activeNavigationContent.offsetMax = Vector2.zero;
			_navigationScroll.horizontal = true;
			_navigationScroll.vertical = false;
		}

		foreach (Button tab in _tabButtons.Values)
		{
			LayoutElement tabLayout = tab.GetComponent<LayoutElement>();
			if (tabLayout == null)
			{
				tabLayout = tab.gameObject.AddComponent<LayoutElement>();
			}
			tabLayout.minWidth = wide ? -1f : 132f;
			tabLayout.preferredWidth = wide ? -1f : 132f;
			tabLayout.flexibleWidth = wide ? 1f : 0f;
			tabLayout.minHeight = 40f;
			tabLayout.preferredHeight = 40f;
			tabLayout.flexibleHeight = 0f;
		}
	}

}
