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
	private void BuildToolsTab()
	{
		_toolsTabButtons.Clear();
		_toolsPageViewHost?.Dispose();
		_toolsPageViewHost = new CheatMenuToolsPageViewHost(CreateToolsPageView);
		AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		AddToolsTab(gameObject.transform, ToolsTab.Interaction, "交互");
		AddToolsTab(gameObject.transform, ToolsTab.Inventory, "背包整理");
		AddToolsTab(gameObject.transform, ToolsTab.AutoPickup, "自动拾取");
		AddToolsTab(gameObject.transform, ToolsTab.Movement, "移动");
		AddToolsTab(gameObject.transform, ToolsTab.Minimap, "小地图");
		AddToolsTab(gameObject.transform, ToolsTab.Translation, "翻译");
		_toolsPageHost = CreateObject("ToolsPageHost", _contentHost.transform, typeof(RectTransform));
		SetLayout(_toolsPageHost, -1f, -1f, 1f, 1f);
		ShowToolsTab(_activeToolsTab, invalidate: false);
	}

	private void ShowToolsTab(ToolsTab tab, bool invalidate)
	{
		_activeToolsTab = tab;
		_toolsPageViewHost.Activate(tab, invalidate);
		foreach (KeyValuePair<ToolsTab, Button> pair in _toolsTabButtons)
		{
			Color color = pair.Key == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
			OverlaySelectableVisual visual = pair.Value.GetComponent<OverlaySelectableVisual>();
			if (visual != null)
			{
				visual.SetBaseColor(color);
			}
			else
			{
				pair.Value.GetComponent<Image>().color = color;
			}
		}
	}

	private UiPageView<ToolsTab> CreateToolsPageView(ToolsTab tab)
	{
		GameObject panel = CreateObject(tab + "ToolsPageView", _toolsPageHost.transform, typeof(Image));
		panel.GetComponent<Image>().color = CheatMenuStyle.Surface;
		AddVerticalLayout(panel, 10f, new RectOffset(18, 18, 16, 16));
		Stretch(panel.GetComponent<RectTransform>());
		if (tab == ToolsTab.Translation)
		{
			CreateLabel(panel.transform, GetToolsTabTitle(tab), 19, CheatMenuStyle.Text);
			_workshopLocalizationScrollContent = CreateShortcutScroll(panel.transform);
			_workshopLocalizationScrollRect = _workshopLocalizationScrollContent.GetComponentInParent<ScrollRect>();
			_workshopLocalizationScrollRect.scrollSensitivity = 48f;
			AddShortcutScrollbar(_workshopLocalizationScrollRect);
			AddShortcutScrollLayout(_workshopLocalizationScrollContent.gameObject, 10f, new RectOffset(0, 18, 0, 0));
			_toolsPageContentHost = _workshopLocalizationScrollContent.gameObject;
			BuildActiveToolsPageContent(tab, _workshopLocalizationScrollContent);
			RebuildWorkshopLocalizationScrollLayout(resetToTop: true);
		}
		else
		{
			_workshopLocalizationScrollRect = null;
			_workshopLocalizationScrollContent = null;
			BuildToolsPageView(tab, panel);
		}
		return new UiPageView<ToolsTab>(tab, panel, QueueForDestroy);
	}

	private void BuildToolsPageView(ToolsTab tab, GameObject panel)
	{
		CreateLabel(panel.transform, GetToolsTabTitle(tab), 19, CheatMenuStyle.Text);
		_toolsPageContentHost = CreateObject("ToolsPageContentHost", panel.transform);
		AddVerticalLayout(_toolsPageContentHost, 10f, new RectOffset(0, 0, 0, 0));
		SetLayout(_toolsPageContentHost, -1f, -1f, 1f, 1f);
		BuildActiveToolsPageContent(tab, _toolsPageContentHost.transform);
	}

	private void BuildActiveToolsPageContent(ToolsTab tab, Transform panel)
	{
		_autoPickupContentHost = null;
		if (tab == ToolsTab.Interaction)
		{
			BuildPointToolPanel(panel);
		}
		else if (tab == ToolsTab.Inventory)
		{
			BuildInventoryArrangePanel(panel);
		}
		else if (tab == ToolsTab.AutoPickup)
		{
			BuildAutoPickupPanel(panel);
		}
		else if (tab == ToolsTab.Movement)
		{
			BuildMovementPanel(panel);
		}
		else if (tab == ToolsTab.Minimap)
		{
			BuildMinimapPanel(panel);
		}
		else
		{
			BuildWorkshopLocalizationPanel(panel);
		}
	}

	private void RefreshActiveToolsPageContent()
	{
		if (_activeTab != MenuTab.Tools || _toolsPageContentHost == null)
		{
			return;
		}
		HidePointToolModeDrawer();
		HideInventoryPresetDrawer();
		HideInventoryScopeDrawer();
		HideWorkshopProtocolDrawer();
		HideAutoPickupBlacklistPicker();
		ClearResultHost(_toolsPageContentHost);
		BuildActiveToolsPageContent(_activeToolsTab, _toolsPageContentHost.transform);
		RebuildWorkshopLocalizationScrollLayout();
	}

	private static string GetToolsTabTitle(ToolsTab tab)
	{
		return tab switch
		{
			ToolsTab.Interaction => "准星交互工具",
			ToolsTab.Inventory => "背包整理",
			ToolsTab.AutoPickup => "自动拾取",
			ToolsTab.Movement => "移动工具",
			ToolsTab.Minimap => "小地图与世界雷达",
			_ => "翻译"
		};
	}

	private void AddToolsTab(Transform parent, ToolsTab tab, string label)
	{
		Button button = CreateButton(parent, label, delegate
		{
			ShowToolsTab(tab, invalidate: false);
		}, string.Format(PluginLocalization.Translate("切换到“{0}”工具页面。"), PluginLocalization.Translate(label)), 0f, 40f, (_activeToolsTab == tab) ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 14);
		_toolsTabButtons[tab] = button;
		SetLayout(button.gameObject, -1f, -1f, 1f);
	}
}
