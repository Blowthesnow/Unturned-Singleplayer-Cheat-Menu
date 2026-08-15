using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>
/// Screen-space overlay replacement for the legacy IMGUI menu. Unturned's
/// Glazier UI renders after MonoBehaviour.OnGUI, so IMGUI can be fully covered
/// even with a very low GUI.depth. A top-sorted uGUI Canvas participates in the
/// final UI render pass and remains visible without hiding the native HUD.
/// </summary>
internal sealed class CheatMenuOverlayUi : IDisposable
{
    // Unturned's Glazier uGUI cursor has its own nested Canvas at 30000.
    // Keep the menu directly below it, while remaining above the ordinary
    // Glazier HUD canvas (15, or 29000 when hackSortOrder is enabled).
    private const int OverlaySortingOrder = 29900;
    private const int CardsPerOverlayPage = 15;
    private const int TeleportsPerOverlayPage = 6;

    private enum MenuTab
    {
        Character,
        Items,
        Vehicles,
        Favorites,
        Teleports,
        Other
    }

    private enum FavoriteKind
    {
        Items,
        Vehicles
    }

    private static readonly string[] ItemCategories =
    {
        "全部", "武器", "弹药与配件", "衣物", "食物与饮料", "医疗与防护", "建筑与放置物", "工具", "其他"
    };

    private static readonly string[] VehicleCategories =
    {
        "全部", "陆地车辆", "固定翼飞机", "直升机", "飞艇", "船只", "火车", "其他"
    };

    private static readonly Color Panel = new(0.035f, 0.045f, 0.060f, 0.985f);
    private static readonly Color Surface = new(0.085f, 0.105f, 0.135f, 0.99f);
    private static readonly Color SurfaceHover = new(0.13f, 0.16f, 0.21f, 1f);
    private static readonly Color Accent = new(0.08f, 0.55f, 0.88f, 1f);
    private static readonly Color AccentBright = new(0.18f, 0.72f, 1f, 1f);
    private static readonly Color Favorite = new(0.82f, 0.60f, 0.12f, 1f);
    private static readonly Color Danger = new(0.72f, 0.15f, 0.20f, 1f);
    private static readonly Color TextColor = new(0.94f, 0.97f, 1f, 1f);
    private static readonly Color MutedColor = new(0.62f, 0.69f, 0.76f, 1f);

    private readonly CheatMenuPlugin _plugin;
    private readonly List<ItemAsset> _itemResults = new();
    private readonly List<VehicleAsset> _vehicleResults = new();
    private readonly List<ItemAsset> _favoriteItemResults = new();
    private readonly List<VehicleAsset> _favoriteVehicleResults = new();
    private readonly Dictionary<MenuTab, Button> _tabButtons = new();

    private GameObject _root;
    private GameObject _ownedEventSystem;
    private RectTransform _panel;
    private GameObject _contentHost;
    private Text _mapAndShortcutText;
    private Text _statusText;
    private Font _font;
    private MenuTab _activeTab;
    private string _itemQuery = string.Empty;
    private string _vehicleQuery = string.Empty;
    private string _itemCategory = "全部";
    private string _vehicleCategory = "全部";
    private string _favoriteQuery = string.Empty;
    private string _favoriteItemCategory = "全部";
    private string _favoriteVehicleCategory = "全部";
    private string _teleportName = string.Empty;
    private string _lastHeaderMap = string.Empty;
    private string _lastHeaderShortcut = string.Empty;
    private string _healthTarget = "100";
    private string _foodTarget = "100";
    private string _waterTarget = "100";
    private string _virusTarget = "100";
    private string _staminaTarget = "100";
    private string _oxygenTarget = "100";
    private string _experienceAmount = "1000";
    private string _reputationAmount = "100";
    private string _itemAmountText = "1";
    private string _vehicleAmountText = "1";
    private string _timePercentText = "50";
    private int _itemPage;
    private int _vehiclePage;
    private int _favoritePage;
    private int _teleportPage;
    private FavoriteKind _favoriteKind;
    private byte _giveAmount = 1;
    private int _spawnVehicleAmount = 1;
    private float _statusUntil;

    public CheatMenuOverlayUi(CheatMenuPlugin plugin)
    {
        _plugin = plugin;
    }

    public void OnOpened()
    {
        BuildItemResults();
        BuildVehicleResults();
        BuildFavoriteResults();
        EnsureShell();
        RefreshHeader();
        _root.SetActive(true);
        ShowTab(_activeTab);
        SetStatus($"自动扫描完成：{_plugin.Catalog.Items.Count} 个物品、{_plugin.Catalog.Vehicles.Count} 辆载具。", 5f);
    }

    public void OnClosed()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void OnCatalogRefreshed()
    {
        BuildItemResults();
        BuildVehicleResults();
        BuildFavoriteResults();
        if (_root != null
            && _root.activeSelf
            && (_activeTab == MenuTab.Items || _activeTab == MenuTab.Vehicles || _activeTab == MenuTab.Favorites))
            ShowTab(_activeTab);
    }

    public void Maintain()
    {
        if (_root == null || !_root.activeSelf)
            return;

        Canvas canvas = _root.GetComponent<Canvas>();
        if (canvas != null && canvas.sortingOrder != OverlaySortingOrder)
            canvas.sortingOrder = OverlaySortingOrder;

        RefreshHeader();

        if (_statusUntil > 0f && Time.unscaledTime > _statusUntil)
        {
            _statusUntil = 0f;
            if (_statusText != null)
                _statusText.text = "准备就绪。";
        }
    }

    public void Dispose()
    {
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        if (_ownedEventSystem != null)
            UnityEngine.Object.Destroy(_ownedEventSystem);
        if (_font != null)
            UnityEngine.Object.Destroy(_font);
        _root = null;
        _ownedEventSystem = null;
        _mapAndShortcutText = null;
        _lastHeaderMap = string.Empty;
        _lastHeaderShortcut = string.Empty;
        _font = null;
    }

    private void EnsureShell()
    {
        if (_root != null)
            return;

        _font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 18);

        _root = new GameObject("UnturnedSingleplayerCheatMenu.Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEngine.Object.DontDestroyOnLoad(_root);
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            _ownedEventSystem = new GameObject("UnturnedSingleplayerCheatMenu.EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
        }

        GameObject panelObject = CreateObject("Panel", _root.transform, typeof(Image));
        _panel = panelObject.GetComponent<RectTransform>();
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(1120f, 790f);
        _panel.localScale = Vector3.one * _plugin.UiScale;
        panelObject.GetComponent<Image>().color = Panel;

        VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(18, 18, 14, 14);
        panelLayout.spacing = 9f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        BuildHeader(panelObject.transform);
        BuildTabs(panelObject.transform);
        ReplaceContentHost();

        GameObject statusBar = CreateObject("Status", panelObject.transform, typeof(Image));
        statusBar.GetComponent<Image>().color = new Color(0.018f, 0.025f, 0.035f, 1f);
        SetLayout(statusBar, preferredHeight: 31f);
        _statusText = CreateText(statusBar.transform, "准备就绪。", 13, MutedColor, TextAnchor.MiddleLeft);
        SetOffsets(_statusText.rectTransform, 11f, 11f, 2f, 2f);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject row = CreateRow(parent, 46f, 10f);
        Text title = CreateText(row.transform, "单人作弊指令菜单", 25, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetLayout(title.gameObject, preferredWidth: 255f);

        GameObject badgePanel = CreateObject("SingleplayerBadge", row.transform, typeof(Image));
        badgePanel.GetComponent<Image>().color = new Color(0.06f, 0.30f, 0.19f, 1f);
        SetLayout(badgePanel, preferredWidth: 165f, preferredHeight: 28f);
        Text badge = CreateText(badgePanel.transform, "仅限 SINGLEPLAYER", 12, new Color(0.55f, 1f, 0.73f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(badge.rectTransform);

        CreateFlexibleSpacer(row.transform);
        _mapAndShortcutText = CreateText(row.transform, string.Empty, 13, MutedColor, TextAnchor.MiddleRight);
        SetLayout(_mapAndShortcutText.gameObject, preferredWidth: 250f);
        CreateButton(row.transform, "×", () => _plugin.CloseMenu(), 44f, 36f, Danger, 23);
    }

    private void RefreshHeader()
    {
        if (_mapAndShortcutText == null)
            return;

        string mapName = string.IsNullOrWhiteSpace(Provider.map) ? "未进入地图" : Provider.map;
        string shortcut = _plugin.ShortcutLabel;
        if (string.Equals(_lastHeaderMap, mapName, StringComparison.Ordinal)
            && string.Equals(_lastHeaderShortcut, shortcut, StringComparison.Ordinal))
        {
            return;
        }

        _lastHeaderMap = mapName;
        _lastHeaderShortcut = shortcut;
        _mapAndShortcutText.text = $"{mapName}  ·  {shortcut}";
    }

    private void BuildTabs(Transform parent)
    {
        GameObject row = CreateRow(parent, 43f, 7f);
        AddTab(row.transform, MenuTab.Character, "角色");
        AddTab(row.transform, MenuTab.Items, "物品");
        AddTab(row.transform, MenuTab.Vehicles, "车辆");
        AddTab(row.transform, MenuTab.Favorites, "收藏");
        AddTab(row.transform, MenuTab.Teleports, "传送");
        AddTab(row.transform, MenuTab.Other, "其他");
    }

    private void AddTab(Transform parent, MenuTab tab, string label)
    {
        Button button = CreateButton(parent, label, () => ShowTab(tab), 0f, 40f, Surface, 15);
        SetLayout(button.gameObject, flexibleWidth: 1f);
        _tabButtons[tab] = button;
    }

    private void ShowTab(MenuTab tab)
    {
        _activeTab = tab;
        ReplaceContentHost();
        foreach (KeyValuePair<MenuTab, Button> pair in _tabButtons)
            pair.Value.GetComponent<Image>().color = pair.Key == tab ? Accent : Surface;

        switch (tab)
        {
            case MenuTab.Character:
                BuildCharacterTab();
                break;
            case MenuTab.Items:
                BuildItemsTab();
                break;
            case MenuTab.Vehicles:
                BuildVehiclesTab();
                break;
            case MenuTab.Favorites:
                BuildFavoritesTab();
                break;
            case MenuTab.Teleports:
                BuildTeleportsTab();
                break;
            case MenuTab.Other:
                BuildOtherTab();
                break;
        }
    }

    private void ReplaceContentHost()
    {
        if (_contentHost != null)
        {
            _contentHost.SetActive(false);
            UnityEngine.Object.Destroy(_contentHost);
        }

        _contentHost = CreateObject("ContentHost", _panel, typeof(Image));
        _contentHost.transform.SetSiblingIndex(2);
        _contentHost.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        SetLayout(_contentHost, flexibleHeight: 1f);
    }

    private void BuildCharacterTab()
    {
        Transform content = _contentHost.transform;
        VerticalLayoutGroup column = AddVerticalLayout(_contentHost, 9f, new RectOffset(12, 12, 10, 10));
        column.childForceExpandHeight = false;

        Player player = Player.LocalPlayer;
        PlayerLife life = player?.life;
        PlayerSkills skills = player?.skills;
        if (life == null || skills == null)
        {
            CreateLabel(content, "玩家状态尚未准备完成。", 17);
            return;
        }

        CreateSection(content, "生存状态");
        CreateLabel(content, $"生命 {life.health}/100    饱食 {life.food}/100    水分 {life.water}/100    免疫 {life.virus}/100    体力 {life.stamina}/100    氧气 {life.oxygen}/100", 14);
        GameObject toggles = CreateRow(content, 42f, 8f);
        CreateToggleAction(toggles.transform, "无敌模式", _plugin.Actions.GodModeEnabled, value =>
        {
            _plugin.Actions.SetGodMode(value);
            SetStatus(value ? "无敌模式已开启。" : "无敌模式已关闭。");
            ShowTab(MenuTab.Character);
        });
        CreateToggleAction(toggles.transform, "无限生存状态", _plugin.Actions.InfiniteNeedsEnabled, value =>
        {
            _plugin.Actions.SetInfiniteNeeds(value);
            SetStatus(value ? "无限生存状态已开启。" : "无限生存状态已关闭。");
            ShowTab(MenuTab.Character);
        });

        CreateLabel(content, "设置指定数值（0–100；生命最低 1）", 12, MutedColor);
        GameObject stats = CreateRow(content, 36f, 6f);
        CreateLabeledInput(stats.transform, "生命", _healthTarget, value => _healthTarget = value);
        CreateLabeledInput(stats.transform, "饱食", _foodTarget, value => _foodTarget = value);
        CreateLabeledInput(stats.transform, "水分", _waterTarget, value => _waterTarget = value);
        CreateLabeledInput(stats.transform, "免疫", _virusTarget, value => _virusTarget = value);
        CreateLabeledInput(stats.transform, "体力", _staminaTarget, value => _staminaTarget = value);
        CreateLabeledInput(stats.transform, "氧气", _oxygenTarget, value => _oxygenTarget = value);
        CreateButton(stats.transform, "应用", () =>
        {
            bool ok = _plugin.Actions.SetLifeStats(
                ParseClamped(_healthTarget, 1, 100, 100), ParseClamped(_foodTarget, 0, 100, 100),
                ParseClamped(_waterTarget, 0, 100, 100), ParseClamped(_virusTarget, 0, 100, 100),
                ParseClamped(_staminaTarget, 0, 100, 100), ParseClamped(_oxygenTarget, 0, 100, 100));
            SetStatus(ok ? "角色生存数值已设置。" : "设置失败。");
            ShowTab(MenuTab.Character);
        }, 76f, 34f);

        GameObject restore = CreateRow(content, 42f, 8f);
        CreateAction(restore.transform, "全部恢复", () => SetStatus(_plugin.Actions.RestoreEverything() ? "角色状态已全部恢复。" : "恢复失败。"));
        CreateAction(restore.transform, "补满生存状态", () => SetStatus(_plugin.Actions.RefillNeeds() ? "生存状态已补满。" : "操作失败。"));
        CreateAction(restore.transform, "治疗流血/骨折", () => SetStatus(_plugin.Actions.CureInjuries() ? "伤势已治疗。" : "操作失败。"));

        CreateSection(content, "经验、声望与技能");
        CreateLabel(content, $"经验 {skills.experience:N0}    声望 {skills.reputation:N0}", 14);
        GameObject skillsRow = CreateRow(content, 38f, 8f);
        CreateLabeledInput(skillsRow.transform, "经验", _experienceAmount, value => _experienceAmount = value, 150f);
        CreateButton(skillsRow.transform, "增加经验", () =>
        {
            uint amount = (uint)ParseClamped(_experienceAmount, 1, 100000000, 1000);
            SetStatus(_plugin.Actions.AddExperience(amount) ? $"已增加 {amount:N0} 经验。" : "增加经验失败。");
            ShowTab(MenuTab.Character);
        }, 110f, 34f);
        CreateLabeledInput(skillsRow.transform, "声望", _reputationAmount, value => _reputationAmount = value, 150f);
        CreateButton(skillsRow.transform, "变更声望", () =>
        {
            int amount = ParseClamped(_reputationAmount, -1000000, 1000000, 100);
            SetStatus(_plugin.Actions.AddReputation(amount) ? $"已变更 {amount:N0} 声望。" : "变更声望失败。");
            ShowTab(MenuTab.Character);
        }, 110f, 34f);
        CreateAction(skillsRow.transform, "全部技能满级", () =>
        {
            int changed = _plugin.Actions.MaxAllSkills();
            SetStatus($"技能已处理，实际变更 {changed} 项。");
            ShowTab(MenuTab.Character);
        });
    }

    private void BuildItemsTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
        GameObject toolbar = CreateRow(_contentHost.transform, 38f, 7f);
        CreateLabel(toolbar.transform, "搜索", 13, TextColor, 48f);
        CreateInput(toolbar.transform, _itemQuery, value =>
        {
            _itemQuery = value;
            BuildItemResults();
            ShowTab(MenuTab.Items);
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, TextColor, 48f);
        CreateButton(toolbar.transform, "−", () =>
        {
            _giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
            _itemAmountText = _giveAmount.ToString();
            ShowTab(MenuTab.Items);
        }, 36f, 34f);
        CreateInput(toolbar.transform, _itemAmountText, value => _itemAmountText = value, 62f);
        CreateButton(toolbar.transform, "+", () =>
        {
            _giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
            _itemAmountText = _giveAmount.ToString();
            ShowTab(MenuTab.Items);
        }, 36f, 34f);
        CreateFlexibleSpacer(toolbar.transform);
        CreateButton(toolbar.transform, "重新扫描", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }, 105f, 34f);
        CreateLabel(_contentHost.transform, $"显示 {_itemResults.Count} / {_plugin.Catalog.Items.Count}；模组物品 {_plugin.Catalog.WorkshopItemCount}。搜索支持名称、ID、GUID 和来源。", 12, MutedColor);

        GameObject body = CreateRow(_contentHost.transform, 0f, 10f);
        SetLayout(body, flexibleHeight: 1f);
        BuildCategorySidebar(body.transform, "物品分类", ItemCategories, _itemCategory, category =>
        {
            _itemCategory = category;
            BuildItemResults();
            ShowTab(MenuTab.Items);
        });
        BuildItemGrid(body.transform);
    }

    private void BuildVehiclesTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
        GameObject toolbar = CreateRow(_contentHost.transform, 38f, 7f);
        CreateLabel(toolbar.transform, "搜索", 13, TextColor, 48f);
        CreateInput(toolbar.transform, _vehicleQuery, value =>
        {
            _vehicleQuery = value;
            BuildVehicleResults();
            ShowTab(MenuTab.Vehicles);
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, TextColor, 48f);
        CreateButton(toolbar.transform, "−", () =>
        {
            _spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            ShowTab(MenuTab.Vehicles);
        }, 36f, 34f);
        CreateInput(toolbar.transform, _vehicleAmountText, value => _vehicleAmountText = value, 62f);
        CreateButton(toolbar.transform, "+", () =>
        {
            _spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            ShowTab(MenuTab.Vehicles);
        }, 36f, 34f);
        CreateFlexibleSpacer(toolbar.transform);
        CreateButton(toolbar.transform, "重新扫描", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }, 105f, 34f);
        CreateLabel(_contentHost.transform, $"显示 {_vehicleResults.Count} / {_plugin.Catalog.Vehicles.Count}；模组车辆 {_plugin.Catalog.WorkshopVehicleCount}。每次生成 1–20 辆。", 12, MutedColor);

        GameObject body = CreateRow(_contentHost.transform, 0f, 10f);
        SetLayout(body, flexibleHeight: 1f);
        BuildCategorySidebar(body.transform, "车辆分类", VehicleCategories, _vehicleCategory, category =>
        {
            _vehicleCategory = category;
            BuildVehicleResults();
            ShowTab(MenuTab.Vehicles);
        });
        BuildVehicleGrid(body.transform);
    }

    private void BuildFavoritesTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));

        int loadedFavoriteItems = _plugin.Catalog.Items.Count(_plugin.Favorites.IsItemFavorite);
        int loadedFavoriteVehicles = _plugin.Catalog.Vehicles.Count(_plugin.Favorites.IsVehicleFavorite);
        GameObject kindRow = CreateRow(_contentHost.transform, 38f, 8f);
        CreateButton(
            kindRow.transform,
            $"物品收藏  {loadedFavoriteItems}/{_plugin.Favorites.ItemCount}",
            () =>
            {
                _favoriteKind = FavoriteKind.Items;
                _favoritePage = 0;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            },
            0f,
            36f,
            _favoriteKind == FavoriteKind.Items ? Favorite : SurfaceHover,
            14);
        CreateButton(
            kindRow.transform,
            $"车辆收藏  {loadedFavoriteVehicles}/{_plugin.Favorites.VehicleCount}",
            () =>
            {
                _favoriteKind = FavoriteKind.Vehicles;
                _favoritePage = 0;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            },
            0f,
            36f,
            _favoriteKind == FavoriteKind.Vehicles ? Favorite : SurfaceHover,
            14);
        foreach (Transform child in kindRow.transform)
            SetLayout(child.gameObject, flexibleWidth: 1f);

        GameObject toolbar = CreateRow(_contentHost.transform, 38f, 7f);
        CreateLabel(toolbar.transform, "搜索", 13, TextColor, 48f);
        CreateInput(toolbar.transform, _favoriteQuery, value =>
        {
            _favoriteQuery = value;
            BuildFavoriteResults();
            ShowTab(MenuTab.Favorites);
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, TextColor, 48f);

        if (_favoriteKind == FavoriteKind.Items)
        {
            CreateButton(toolbar.transform, "−", () =>
            {
                _giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
                _itemAmountText = _giveAmount.ToString();
                ShowTab(MenuTab.Favorites);
            }, 36f, 34f);
            CreateInput(toolbar.transform, _itemAmountText, value => _itemAmountText = value, 62f);
            CreateButton(toolbar.transform, "+", () =>
            {
                _giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
                _itemAmountText = _giveAmount.ToString();
                ShowTab(MenuTab.Favorites);
            }, 36f, 34f);
        }
        else
        {
            CreateButton(toolbar.transform, "−", () =>
            {
                _spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
                _vehicleAmountText = _spawnVehicleAmount.ToString();
                ShowTab(MenuTab.Favorites);
            }, 36f, 34f);
            CreateInput(toolbar.transform, _vehicleAmountText, value => _vehicleAmountText = value, 62f);
            CreateButton(toolbar.transform, "+", () =>
            {
                _spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
                _vehicleAmountText = _spawnVehicleAmount.ToString();
                ShowTab(MenuTab.Favorites);
            }, 36f, 34f);
        }

        CreateFlexibleSpacer(toolbar.transform);
        CreateButton(toolbar.transform, "重新扫描", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }, 105f, 34f);

        if (_favoriteKind == FavoriteKind.Items)
        {
            CreateLabel(
                _contentHost.transform,
                $"显示 {_favoriteItemResults.Count} 个已加载收藏物品；收藏记录 {_plugin.Favorites.ItemCount} 个。搜索支持名称、ID、GUID 和来源。",
                12,
                MutedColor);
        }
        else
        {
            CreateLabel(
                _contentHost.transform,
                $"显示 {_favoriteVehicleResults.Count} 辆已加载收藏车辆；收藏记录 {_plugin.Favorites.VehicleCount} 辆。",
                12,
                MutedColor);
        }

        GameObject body = CreateRow(_contentHost.transform, 0f, 10f);
        SetLayout(body, flexibleHeight: 1f);
        if (_favoriteKind == FavoriteKind.Items)
        {
            BuildCategorySidebar(body.transform, "收藏物品分类", ItemCategories, _favoriteItemCategory, category =>
            {
                _favoriteItemCategory = category;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            });
        }
        else
        {
            BuildCategorySidebar(body.transform, "收藏车辆分类", VehicleCategories, _favoriteVehicleCategory, category =>
            {
                _favoriteVehicleCategory = category;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            });
        }
        BuildFavoriteGrid(body.transform);
    }

    private void BuildCategorySidebar(Transform parent, string title, IEnumerable<string> categories, string selected, Action<string> select)
    {
        GameObject sidebar = CreateObject("Categories", parent, typeof(Image));
        sidebar.GetComponent<Image>().color = Surface;
        AddVerticalLayout(sidebar, 5f, new RectOffset(7, 7, 7, 7));
        SetLayout(sidebar, preferredWidth: 165f, flexibleWidth: 0f);
        CreateLabel(sidebar.transform, title, 16, TextColor);
        foreach (string category in categories)
        {
            string captured = category;
            CreateButton(sidebar.transform, category, () => select(captured), 0f, 34f, category == selected ? Accent : SurfaceHover, 13);
        }
    }

    private void BuildItemGrid(Transform parent)
    {
        GameObject column = CreateObject("ItemGridColumn", parent);
        AddVerticalLayout(column, 7f, new RectOffset(0, 0, 0, 0));
        SetLayout(column, flexibleWidth: 1f, flexibleHeight: 1f);
        int pageCount = Math.Max(1, Mathf.CeilToInt(_itemResults.Count / (float)CardsPerOverlayPage));
        _itemPage = Mathf.Clamp(_itemPage, 0, pageCount - 1);
        BuildPagination(column.transform, _itemPage, pageCount, value => { _itemPage = value; ShowTab(MenuTab.Items); });

        GameObject gridObject = CreateObject("ItemGrid", column.transform);
        SetLayout(gridObject, preferredHeight: 500f, flexibleHeight: 1f);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.spacing = new Vector2(8f, 8f);
        grid.cellSize = new Vector2(155f, 158f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        int start = _itemPage * CardsPerOverlayPage;
        int end = Math.Min(start + CardsPerOverlayPage, _itemResults.Count);
        for (int index = start; index < end; index++)
            CreateItemCard(gridObject.transform, _itemResults[index]);
    }

    private void BuildVehicleGrid(Transform parent)
    {
        GameObject column = CreateObject("VehicleGridColumn", parent);
        AddVerticalLayout(column, 7f, new RectOffset(0, 0, 0, 0));
        SetLayout(column, flexibleWidth: 1f, flexibleHeight: 1f);
        int pageCount = Math.Max(1, Mathf.CeilToInt(_vehicleResults.Count / (float)CardsPerOverlayPage));
        _vehiclePage = Mathf.Clamp(_vehiclePage, 0, pageCount - 1);
        BuildPagination(column.transform, _vehiclePage, pageCount, value => { _vehiclePage = value; ShowTab(MenuTab.Vehicles); });

        GameObject gridObject = CreateObject("VehicleGrid", column.transform);
        SetLayout(gridObject, preferredHeight: 500f, flexibleHeight: 1f);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.spacing = new Vector2(8f, 8f);
        grid.cellSize = new Vector2(155f, 158f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        int start = _vehiclePage * CardsPerOverlayPage;
        int end = Math.Min(start + CardsPerOverlayPage, _vehicleResults.Count);
        for (int index = start; index < end; index++)
            CreateVehicleCard(gridObject.transform, _vehicleResults[index]);
    }

    private void BuildFavoriteGrid(Transform parent)
    {
        int count = _favoriteKind == FavoriteKind.Items
            ? _favoriteItemResults.Count
            : _favoriteVehicleResults.Count;
        GameObject column = CreateObject("FavoriteGridColumn", parent);
        AddVerticalLayout(column, 7f, new RectOffset(0, 0, 0, 0));
        SetLayout(column, flexibleWidth: 1f, flexibleHeight: 1f);
        int pageCount = Math.Max(1, Mathf.CeilToInt(count / (float)CardsPerOverlayPage));
        _favoritePage = Mathf.Clamp(_favoritePage, 0, pageCount - 1);
        BuildPagination(column.transform, _favoritePage, pageCount, value =>
        {
            _favoritePage = value;
            ShowTab(MenuTab.Favorites);
        });

        if (count == 0)
        {
            CreateLabel(
                column.transform,
                _favoriteKind == FavoriteKind.Items
                    ? "当前分类没有已加载的收藏物品。请在物品卡片右上角点击 ☆ 收藏。"
                    : "当前分类没有已加载的收藏车辆。请在车辆卡片右上角点击 ☆ 收藏。",
                16,
                MutedColor,
                0f,
                TextAnchor.MiddleCenter);
            return;
        }

        GameObject gridObject = CreateObject("FavoriteGrid", column.transform);
        SetLayout(gridObject, preferredHeight: 500f, flexibleHeight: 1f);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.spacing = new Vector2(8f, 8f);
        grid.cellSize = new Vector2(155f, 158f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        int start = _favoritePage * CardsPerOverlayPage;
        int end = Math.Min(start + CardsPerOverlayPage, count);
        if (_favoriteKind == FavoriteKind.Items)
        {
            for (int index = start; index < end; index++)
                CreateItemCard(gridObject.transform, _favoriteItemResults[index]);
        }
        else
        {
            for (int index = start; index < end; index++)
                CreateVehicleCard(gridObject.transform, _favoriteVehicleResults[index]);
        }
    }

    private void CreateItemCard(Transform parent, ItemAsset asset)
    {
        Button button = CreateButton(parent, string.Empty, () =>
        {
            _giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
            _itemAmountText = _giveAmount.ToString();
            bool ok = _plugin.Actions.GiveItem(asset, _giveAmount);
            SetStatus(ok ? $"已给予 {_giveAmount} × {asset.FriendlyName}。" : $"无法给予 {asset.FriendlyName}。", 4f);
        }, 155f, 158f, Surface);
        AddCardContents(
            button.transform,
            asset.FriendlyName,
            $"ID {asset.id}  ·  {AssetCatalog.GetOriginLabel(asset)}",
            () => _plugin.Icons.GetItemIcon(asset),
            isVehicle: false);
        AddFavoriteButton(
            button.transform,
            _plugin.Favorites.IsItemFavorite(asset),
            () => ToggleItemFavorite(asset));
    }

    private void CreateVehicleCard(Transform parent, VehicleAsset asset)
    {
        Button button = CreateButton(parent, string.Empty, () =>
        {
            _spawnVehicleAmount = ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            int spawned = _plugin.Actions.SpawnVehicles(asset, _spawnVehicleAmount);
            SetStatus(spawned > 0 ? $"已在玩家前方生成 {spawned} × {asset.FriendlyName}。" : $"无法生成 {asset.FriendlyName}。", 4f);
        }, 155f, 158f, Surface);
        string id = asset.id == 0 ? "GUID" : $"ID {asset.id}";
        AddCardContents(
            button.transform,
            asset.FriendlyName,
            $"{id}  ·  {AssetCatalog.GetOriginLabel(asset)}",
            () => _plugin.Icons.GetVehicleIcon(asset),
            isVehicle: true);
        AddFavoriteButton(
            button.transform,
            _plugin.Favorites.IsVehicleFavorite(asset),
            () => ToggleVehicleFavorite(asset));
    }

    private void AddFavoriteButton(Transform parent, bool isFavorite, System.Action toggle)
    {
        Button favoriteButton = CreateButton(
            parent,
            isFavorite ? "★" : "☆",
            toggle,
            0f,
            0f,
            isFavorite ? Favorite : SurfaceHover,
            18);
        RectTransform rect = favoriteButton.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-5f, -5f);
        rect.sizeDelta = new Vector2(32f, 28f);
        favoriteButton.transform.SetAsLastSibling();
    }

    private void ToggleItemFavorite(ItemAsset asset)
    {
        bool favorite = !_plugin.Favorites.IsItemFavorite(asset);
        if (!_plugin.Favorites.TrySetItemFavorite(asset, favorite))
        {
            SetStatus($"无法保存 {asset.FriendlyName} 的收藏状态。", 5f);
            return;
        }

        MenuTab returnTab = _activeTab;
        BuildFavoriteResults();
        ShowTab(returnTab);
        SetStatus(favorite ? $"已收藏物品：{asset.FriendlyName}。" : $"已取消收藏物品：{asset.FriendlyName}。", 4f);
    }

    private void ToggleVehicleFavorite(VehicleAsset asset)
    {
        bool favorite = !_plugin.Favorites.IsVehicleFavorite(asset);
        if (!_plugin.Favorites.TrySetVehicleFavorite(asset, favorite))
        {
            SetStatus($"无法保存 {asset.FriendlyName} 的收藏状态。", 5f);
            return;
        }

        MenuTab returnTab = _activeTab;
        BuildFavoriteResults();
        ShowTab(returnTab);
        SetStatus(favorite ? $"已收藏车辆：{asset.FriendlyName}。" : $"已取消收藏车辆：{asset.FriendlyName}。", 4f);
    }

    private void AddCardContents(
        Transform parent,
        string name,
        string meta,
        Func<Texture2D> iconLoader,
        bool isVehicle)
    {
        GameObject iconObject = CreateObject("Icon", parent, typeof(RawImage), typeof(OverlayIconBinder));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.12f, 0.40f);
        iconRect.anchorMax = new Vector2(0.88f, 0.95f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        RawImage raw = iconObject.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        iconObject.GetComponent<OverlayIconBinder>().Initialize(raw, iconLoader, isVehicle);

        Text nameText = CreateText(parent, name, 12, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        nameText.rectTransform.anchorMin = new Vector2(0.04f, 0.13f);
        nameText.rectTransform.anchorMax = new Vector2(0.96f, 0.40f);
        nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;
        Text metaText = CreateText(parent, meta, 10, MutedColor, TextAnchor.MiddleCenter);
        metaText.rectTransform.anchorMin = new Vector2(0.04f, 0.01f);
        metaText.rectTransform.anchorMax = new Vector2(0.96f, 0.14f);
        metaText.rectTransform.offsetMin = metaText.rectTransform.offsetMax = Vector2.zero;
    }

    private void BuildPagination(Transform parent, int page, int pageCount, Action<int> changePage)
    {
        GameObject row = CreateRow(parent, 34f, 8f);
        Button previous = CreateButton(row.transform, "‹ 上一页", () => changePage(page - 1), 100f, 32f);
        previous.interactable = page > 0;
        CreateFlexibleSpacer(row.transform);
        CreateLabel(row.transform, $"第 {page + 1} / {pageCount} 页", 12, MutedColor, 110f, TextAnchor.MiddleCenter);
        CreateFlexibleSpacer(row.transform);
        Button next = CreateButton(row.transform, "下一页 ›", () => changePage(page + 1), 100f, 32f);
        next.interactable = page < pageCount - 1;
    }

    private void BuildTeleportsTab()
    {
        Transform content = _contentHost.transform;
        AddVerticalLayout(_contentHost, 8f, new RectOffset(12, 12, 10, 10));
        Player player = Player.LocalPlayer;
        if (player == null)
            return;

        Vector3 position = player.transform.position;
        CreateSection(content, "保存当前位置");
        CreateLabel(content, $"地图 {Provider.map}    X {position.x:F1}    Y {position.y:F1}    Z {position.z:F1}", 12, MutedColor);
        GameObject saveRow = CreateRow(content, 38f, 8f);
        CreateInput(saveRow.transform, _teleportName, value => _teleportName = value, 0f, 1f);
        CreateButton(saveRow.transform, "保存位置", () =>
        {
            TeleportPoint point = _plugin.Teleports.AddCurrent(_teleportName);
            if (point != null)
            {
                _teleportName = string.Empty;
                SetStatus($"已保存传送点：{point.Name}。", 4f);
                ShowTab(MenuTab.Teleports);
            }
        }, 120f, 35f);
        CreateSection(content, $"已保存位置（{_plugin.Teleports.Points.Count}）");
        TeleportPoint[] points = _plugin.Teleports.Points.ToArray();
        int pageCount = Math.Max(1, Mathf.CeilToInt(points.Length / (float)TeleportsPerOverlayPage));
        _teleportPage = Mathf.Clamp(_teleportPage, 0, pageCount - 1);
        BuildPagination(content, _teleportPage, pageCount, value => { _teleportPage = value; ShowTab(MenuTab.Teleports); });
        int start = _teleportPage * TeleportsPerOverlayPage;
        int end = Math.Min(start + TeleportsPerOverlayPage, points.Length);
        for (int index = start; index < end; index++)
        {
            TeleportPoint point = points[index];
            bool sameMap = string.Equals(point.Map, Provider.map, StringComparison.OrdinalIgnoreCase);
            GameObject row = CreateRow(content, 62f, 8f);
            row.AddComponent<Image>().color = Surface;
            Text pointText = CreateText(row.transform, $"{point.Name}\n{point.Map}  ·  ({point.X:F1}, {point.Y:F1}, {point.Z:F1})", 13, sameMap ? TextColor : MutedColor, TextAnchor.MiddleLeft);
            SetLayout(pointText.gameObject, flexibleWidth: 1f);
            Button teleport = CreateButton(row.transform, sameMap ? "传送" : "其他地图", () =>
            {
                bool ok = _plugin.Actions.Teleport(point);
                SetStatus(ok ? $"已传送到 {point.Name}。" : "传送失败：目标被阻挡、地图不同或玩家正在载具中。", 5f);
            }, 105f, 38f);
            teleport.interactable = sameMap;
            CreateButton(row.transform, "×", () =>
            {
                if (_plugin.Teleports.Remove(point.Id))
                {
                    SetStatus($"已删除传送点：{point.Name}。", 4f);
                    ShowTab(MenuTab.Teleports);
                }
            }, 44f, 38f, Danger, 20);
        }
    }

    private void BuildOtherTab()
    {
        Transform content = _contentHost.transform;
        AddVerticalLayout(_contentHost, 9f, new RectOffset(12, 12, 10, 10));
        uint cycle = Math.Max(1u, LightingManager.cycle);
        float normalized = LightingManager.time / (float)cycle;
        _timePercentText = Mathf.RoundToInt(normalized * 100f).ToString();
        CreateSection(content, "时间");
        CreateLabel(content, $"当前时间：{LightingManager.time:N0} / {cycle:N0}（{normalized:P0}）", 14);
        GameObject timeRow = CreateRow(content, 38f, 8f);
        CreateLabeledInput(timeRow.transform, "时间百分比", _timePercentText, value => _timePercentText = value, 210f);
        CreateButton(timeRow.transform, "应用", () =>
        {
            int percent = ParseClamped(_timePercentText, 0, 99, 50);
            _timePercentText = percent.ToString();
            _plugin.Actions.SetTime((uint)(cycle * (percent / 100f)));
            SetStatus($"时间已设置为周期的 {percent}% 。");
            ShowTab(MenuTab.Other);
        }, 82f, 34f);
        CreateAction(timeRow.transform, "设为白天", () => { _plugin.Actions.SetDay(); SetStatus("已切换到白天。"); ShowTab(MenuTab.Other); });
        CreateAction(timeRow.transform, "设为夜晚", () => { _plugin.Actions.SetNight(); SetStatus("已切换到夜晚。"); ShowTab(MenuTab.Other); });

        GameObject switches = CreateRow(content, 42f, 8f);
        CreateToggleAction(switches.transform, "冻结时间", _plugin.Actions.FreezeTimeEnabled, value =>
        {
            _plugin.Actions.SetFreezeTime(value);
            SetStatus(value ? "时间已冻结。" : "时间已恢复流动。");
            ShowTab(MenuTab.Other);
        });
        CreateToggleAction(switches.transform, "强制满月", LightingManager.isFullMoon, value =>
        {
            _plugin.Actions.SetFullMoon(value);
            SetStatus(value ? "已开启满月。" : "已关闭满月。");
            ShowTab(MenuTab.Other);
        });

        CreateSection(content, "世界事件");
        GameObject eventsRow = CreateRow(content, 42f, 8f);
        CreateAction(eventsRow.transform, "立即呼叫空投", () => SetStatus(_plugin.Actions.CallAirdrop() ? "空投已呼叫。" : "此地图没有可用的空投节点或货物表。", 5f));
        CreateAction(eventsRow.transform, "下雨", () => SetStatus(_plugin.Actions.StartRain() ? "已触发雨天。" : "当前地图没有默认雨天资产。"));
        CreateAction(eventsRow.transform, "暴雪", () => SetStatus(_plugin.Actions.StartSnow() ? "已触发暴雪。" : "当前地图没有默认雪天资产。"));
        GameObject weatherRow = CreateRow(content, 42f, 8f);
        CreateAction(weatherRow.transform, "清除当前天气", () => { _plugin.Actions.ClearWeather(); SetStatus("当前天气已清除，后续仍可自然调度。"); });
        CreateAction(weatherRow.transform, "关闭天气调度", () => { _plugin.Actions.DisableWeather(); SetStatus("当前天气和自然天气调度已关闭。"); });
        CreateAction(weatherRow.transform, "重新扫描模组资产", () => { _plugin.RefreshCatalog(); SetStatus("资产目录已重新扫描。", 4f); });

        CreateSection(content, "扫描信息");
        CreateLabel(content, $"物品：{_plugin.Catalog.Items.Count}（模组 {_plugin.Catalog.WorkshopItemCount}）    车辆：{_plugin.Catalog.Vehicles.Count}（模组 {_plugin.Catalog.WorkshopVehicleCount}）", 14);
        CreateLabel(content, "只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。", 12, MutedColor);
    }

    private void CreateToggleAction(Transform parent, string label, bool current, Action<bool> change)
    {
        Button button = CreateButton(parent, current ? $"✓ {label}" : label, () => change(!current), 0f, 40f, current ? Accent : Surface);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void CreateAction(Transform parent, string label, System.Action action)
    {
        Button button = CreateButton(parent, label, action, 0f, 40f);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void CreateSection(Transform parent, string text)
    {
        Text label = CreateText(parent, text, 17, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetLayout(label.gameObject, preferredHeight: 30f);
    }

    private Text CreateLabel(Transform parent, string text, int size, Color? color = null, float width = 0f, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Text label = CreateText(parent, text, size, color ?? TextColor, alignment);
        SetLayout(label.gameObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: 27f);
        return label;
    }

    private void CreateLabeledInput(Transform parent, string label, string value, Action<string> changed, float width = 120f)
    {
        GameObject group = CreateRow(parent, 34f, 4f);
        SetLayout(group, preferredWidth: width, flexibleWidth: 0f);
        CreateLabel(group.transform, label, 12, MutedColor, 46f, TextAnchor.MiddleRight);
        CreateInput(group.transform, value, changed, 0f, 1f);
    }

    private InputField CreateInput(Transform parent, string value, Action<string> changed, float width = 0f, float flexibleWidth = 0f)
    {
        GameObject inputObject = CreateObject("Input", parent, typeof(Image), typeof(InputField));
        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.055f, 0.075f, 1f);
        InputField field = inputObject.GetComponent<InputField>();
        field.targetGraphic = background;
        field.text = value ?? string.Empty;
        field.lineType = InputField.LineType.SingleLine;
        field.contentType = InputField.ContentType.Standard;
        Text text = CreateText(inputObject.transform, field.text, 13, TextColor, TextAnchor.MiddleLeft);
        SetOffsets(text.rectTransform, 9f, 9f, 2f, 2f);
        field.textComponent = text;
        Text placeholder = CreateText(inputObject.transform, "输入…", 13, MutedColor, TextAnchor.MiddleLeft, FontStyle.Italic);
        SetOffsets(placeholder.rectTransform, 9f, 9f, 2f, 2f);
        field.placeholder = placeholder;
        field.onEndEdit.AddListener(changed.Invoke);
        SetLayout(inputObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: 34f, flexibleWidth: flexibleWidth);
        return field;
    }

    private Button CreateButton(Transform parent, string label, System.Action action, float width = 0f, float height = 36f, Color? color = null, int fontSize = 13)
    {
        GameObject buttonObject = CreateObject("Button", parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = color ?? Surface;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.80f, 0.86f, 0.92f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        button.colors = colors;
        if (action != null)
            button.onClick.AddListener(action.Invoke);
        Text text = CreateText(buttonObject.transform, label, fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetOffsets(text.rectTransform, 4f, 4f, 2f, 2f);
        SetLayout(buttonObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: height);
        return button;
    }

    private Text CreateText(Transform parent, string value, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
    {
        GameObject textObject = CreateObject("Text", parent, typeof(Text));
        Text text = textObject.GetComponent<Text>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform CreateScroll(Transform parent)
    {
        GameObject scrollObject = CreateObject("Scroll", parent, typeof(Image), typeof(ScrollRect));
        Stretch(scrollObject.GetComponent<RectTransform>());
        scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        SetLayout(scrollObject, flexibleWidth: 1f, flexibleHeight: 1f);

        GameObject viewportObject = CreateObject("Viewport", scrollObject.transform, typeof(Image), typeof(Mask));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = CreateObject("Content", viewport, typeof(RectTransform));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        return content;
    }

    private GameObject CreateRow(Transform parent, float height, float spacing)
    {
        GameObject row = CreateObject("Row", parent);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        if (height > 0f)
            SetLayout(row, preferredHeight: height);
        return row;
    }

    private VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding)
    {
        VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        if (target.GetComponent<ContentSizeFitter>() == null && target.name == "Content")
        {
            ContentSizeFitter fitter = target.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return layout;
    }

    private static GameObject CreateObject(string name, Transform parent, params Type[] components)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        foreach (Type component in components)
        {
            if (component != typeof(RectTransform) && gameObject.GetComponent(component) == null)
                gameObject.AddComponent(component);
        }
        return gameObject;
    }

    private static void SetLayout(GameObject target, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
    {
        LayoutElement element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
        if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
    }

    private static void CreateFlexibleSpacer(Transform parent)
    {
        GameObject spacer = CreateObject("Spacer", parent);
        SetLayout(spacer, flexibleWidth: 1f);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetOffsets(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void BuildItemResults()
    {
        _itemResults.Clear();
        _itemResults.AddRange(_plugin.Catalog.Items.Where(asset =>
            (_itemCategory == "全部" || AssetCatalog.GetItemCategory(asset) == _itemCategory)
            && AssetCatalog.Matches(asset, asset.FriendlyName, _itemQuery)));
        _itemPage = 0;
    }

    private void BuildVehicleResults()
    {
        _vehicleResults.Clear();
        _vehicleResults.AddRange(_plugin.Catalog.Vehicles.Where(asset =>
            (_vehicleCategory == "全部" || AssetCatalog.GetVehicleCategory(asset) == _vehicleCategory)
            && AssetCatalog.Matches(asset, asset.FriendlyName, _vehicleQuery)));
        _vehiclePage = 0;
    }

    private void BuildFavoriteResults()
    {
        _favoriteItemResults.Clear();
        _favoriteItemResults.AddRange(_plugin.Catalog.Items.Where(asset =>
            _plugin.Favorites.IsItemFavorite(asset)
            && (_favoriteItemCategory == "全部" || AssetCatalog.GetItemCategory(asset) == _favoriteItemCategory)
            && AssetCatalog.Matches(asset, asset.FriendlyName, _favoriteQuery)));

        _favoriteVehicleResults.Clear();
        _favoriteVehicleResults.AddRange(_plugin.Catalog.Vehicles.Where(asset =>
            _plugin.Favorites.IsVehicleFavorite(asset)
            && (_favoriteVehicleCategory == "全部" || AssetCatalog.GetVehicleCategory(asset) == _favoriteVehicleCategory)
            && AssetCatalog.Matches(asset, asset.FriendlyName, _favoriteQuery)));
        _favoritePage = 0;
    }

    private void SetStatus(string message, float seconds = 3f)
    {
        if (_statusText != null)
            _statusText.text = message;
        _statusUntil = Time.unscaledTime + seconds;
    }

    private static int ParseClamped(string text, int min, int max, int fallback)
    {
        return int.TryParse(text, out int value) ? Mathf.Clamp(value, min, max) : Mathf.Clamp(fallback, min, max);
    }
}

internal sealed class OverlayIconBinder : MonoBehaviour
{
    private static readonly List<OverlayIconBinder> Pending = new();

    private RawImage _target;
    private Func<Texture2D> _loader;
    private bool _isVehicle;
    private float _nextAttempt;

    public void Initialize(RawImage target, Func<Texture2D> loader, bool isVehicle)
    {
        _target = target;
        _loader = loader;
        _isVehicle = isVehicle;
        if (!Pending.Contains(this))
            Pending.Add(this);
        TryLoad();
    }

    private void Update()
    {
        PumpIfDue();
    }

    internal static void PumpPending()
    {
        for (int index = Pending.Count - 1; index >= 0; index--)
        {
            OverlayIconBinder binder = Pending[index];
            if (binder == null || binder._loader == null)
            {
                Pending.RemoveAt(index);
                continue;
            }

            binder.PumpIfDue();
            if (binder == null || binder._loader == null)
                Pending.RemoveAt(index);
        }
    }

    private void PumpIfDue()
    {
        if (_loader == null || Time.unscaledTime < _nextAttempt)
            return;
        TryLoad();
    }

    private void TryLoad()
    {
        _nextAttempt = Time.unscaledTime + 0.25f;
        Texture2D texture = _loader?.Invoke();
        if (texture == null)
            return;
        _target.texture = texture;
        if (_isVehicle)
            CheatMenuPlugin.Instance?.LogVehicleIconBound(texture);
        _loader = null;
        enabled = false;
    }

    private void OnDestroy()
    {
        Pending.Remove(this);
        _target = null;
        _loader = null;
        _isVehicle = false;
    }
}
