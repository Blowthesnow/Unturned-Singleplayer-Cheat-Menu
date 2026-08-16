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

    private enum TeleportView
    {
        Map,
        Points
    }

    private enum StatusKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    private static readonly string[] ItemCategories =
    {
        "全部", "武器", "弹药与配件", "衣物", "食物与饮料", "医疗与防护", "建筑与放置物", "工具", "其他"
    };

    private static readonly string[] VehicleCategories =
    {
        "全部", "陆地车辆", "固定翼飞机", "直升机", "飞艇", "船只", "火车", "其他"
    };

    private static readonly TeleportMarkerKind[] MarkerKinds =
    {
        TeleportMarkerKind.Star,
        TeleportMarkerKind.Square,
        TeleportMarkerKind.Circle,
        TeleportMarkerKind.Diamond
    };

    private static readonly string[] TeleportMarkerColorPresets =
    {
        "#252525", "#F7F5EF", "#1E6BB8", "#4BA3DF", "#2D8036", "#9AC65A",
        "#C27B10", "#F1BF45", "#AF3030", "#E94A4A", "#CB5A97", "#8659D4",
        "#D5D4CC", "#8C8D86", "#CDE5FA", "#F8ECD7", "#1C425A", "#4B3917"
    };

    private sealed class ColorSwatchControl
    {
        public Color Color;
        public GameObject Check;
    }

    private readonly CheatMenuPlugin _plugin;
    private readonly List<ItemAsset> _itemResults = new();
    private readonly List<VehicleAsset> _vehicleResults = new();
    private readonly List<ItemAsset> _favoriteItemResults = new();
    private readonly List<VehicleAsset> _favoriteVehicleResults = new();
    private readonly List<GameObject> _deferredDestroy = new();
    private readonly Dictionary<MenuTab, Button> _tabButtons = new();
    private readonly Dictionary<TeleportMarkerKind, Sprite> _teleportMarkerSprites = new();
    private readonly Dictionary<TeleportMarkerKind, Texture2D> _teleportMarkerTextures = new();
    private readonly UiDebouncer _searchDebouncer = new(0.15f);
    private readonly UiDebouncer _timeApplyDebouncer = new(0.15f);

    private GameObject _root;
    private GameObject _ownedEventSystem;
    private RectTransform _panel;
    private GameObject _contentHost;
    private Text _mapAndShortcutText;
    private Text _statusText;
    private Image _statusAccent;
    private TeleportMapSurface _teleportMapSurface;
    private Text _timePreviewText;
    private Slider _timeSlider;
    private InputField _itemAmountInput;
    private InputField _vehicleAmountInput;
    private GameObject _confirmRoot;
    private Font _font;
    private Texture2D _sliderGripTexture;
    private Texture2D _closeGlyphTexture;
    private Texture2D _favoriteFilledTexture;
    private Texture2D _favoriteOutlineTexture;
    private Sprite _sliderGripSprite;
    private Sprite _closeGlyphSprite;
    private Sprite _favoriteFilledSprite;
    private Sprite _favoriteOutlineSprite;
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
    private TeleportView _teleportView;
    private TeleportMarkerKind _teleportMarkerKind = TeleportMarkerKind.Star;
    private Color _teleportMarkerColor = new(0.96f, 0.77f, 0.26f, 1f);
    private bool _teleportMarkerDrawerOpen;
    private GameObject _teleportColorPickerRoot;
    private Color _teleportColorDraft;
    private Image _teleportColorPreview;
    private Text _teleportColorHexPreview;
    private InputField _teleportColorHexInput;
    private Slider _teleportColorRedSlider;
    private Slider _teleportColorGreenSlider;
    private Slider _teleportColorBlueSlider;
    private Text _teleportColorRedValue;
    private Text _teleportColorGreenValue;
    private Text _teleportColorBlueValue;
    private readonly List<ColorSwatchControl> _teleportColorSwatches = new();
    private byte _giveAmount = 1;
    private int _spawnVehicleAmount = 1;
    private float _statusUntil;

    public CheatMenuOverlayUi(CheatMenuPlugin plugin)
    {
        _plugin = plugin;
        _activeTab = ParseMenuTab(plugin.LastMainTab);
        _teleportView = ParseTeleportView(plugin.LastTeleportView);
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
        SetStatus($"自动扫描完成：{_plugin.Catalog.Items.Count} 个物品、{_plugin.Catalog.Vehicles.Count} 辆载具。", 5f, StatusKind.Info);
    }

    public void OnClosed()
    {
        HideConfirmDialog();
        HideTeleportColorPicker();
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
        FlushDeferredDestroy();

        if (_root == null || !_root.activeSelf)
            return;

        _searchDebouncer.Tick();
        _timeApplyDebouncer.Tick();

        Canvas canvas = _root.GetComponent<Canvas>();
        if (canvas != null && canvas.sortingOrder != OverlaySortingOrder)
            canvas.sortingOrder = OverlaySortingOrder;
        if (_panel != null)
            _panel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);

        RefreshHeader();
        if (_activeTab == MenuTab.Teleports && _teleportMapSurface != null && Player.LocalPlayer != null)
            _teleportMapSurface.RefreshPlayerMarker(Player.LocalPlayer.transform.position);

        if (_statusUntil > 0f && Time.unscaledTime > _statusUntil)
        {
            _statusUntil = 0f;
            ResetStatus();
        }
    }

    public void Dispose()
    {
        _searchDebouncer.Dispose();
        _timeApplyDebouncer.Dispose();
        FlushDeferredDestroy();
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        if (_ownedEventSystem != null)
            UnityEngine.Object.Destroy(_ownedEventSystem);
        if (_font != null)
            UnityEngine.Object.Destroy(_font);
        DestroyGeneratedSprite(ref _sliderGripSprite, ref _sliderGripTexture);
        DestroyGeneratedSprite(ref _closeGlyphSprite, ref _closeGlyphTexture);
        DestroyGeneratedSprite(ref _favoriteFilledSprite, ref _favoriteFilledTexture);
        DestroyGeneratedSprite(ref _favoriteOutlineSprite, ref _favoriteOutlineTexture);
        foreach (Sprite sprite in _teleportMarkerSprites.Values)
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }
        foreach (Texture2D texture in _teleportMarkerTextures.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
        _teleportMarkerSprites.Clear();
        _teleportMarkerTextures.Clear();
        if (_confirmRoot != null)
            UnityEngine.Object.Destroy(_confirmRoot);
        if (_teleportColorPickerRoot != null)
            UnityEngine.Object.Destroy(_teleportColorPickerRoot);
        _root = null;
        _ownedEventSystem = null;
        _mapAndShortcutText = null;
        _statusText = null;
        _statusAccent = null;
        _teleportMapSurface = null;
        _timePreviewText = null;
        _timeSlider = null;
        _itemAmountInput = null;
        _vehicleAmountInput = null;
        _confirmRoot = null;
        _teleportColorPickerRoot = null;
        _teleportColorPreview = null;
        _teleportColorHexPreview = null;
        _teleportColorHexInput = null;
        _teleportColorRedSlider = null;
        _teleportColorGreenSlider = null;
        _teleportColorBlueSlider = null;
        _teleportColorRedValue = null;
        _teleportColorGreenValue = null;
        _teleportColorBlueValue = null;
        _teleportColorSwatches.Clear();
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
        _panel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);
        panelObject.GetComponent<Image>().color = CheatMenuStyle.Panel;

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
        statusBar.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
        Outline statusOutline = statusBar.AddComponent<Outline>();
        statusOutline.effectColor = CheatMenuStyle.StatusBorder;
        statusOutline.effectDistance = Vector2.one;
        statusOutline.useGraphicAlpha = false;
        SetLayout(statusBar, preferredHeight: CheatMenuStyle.StatusHeight, flexibleHeight: 0f);
        LayoutElement statusElement = statusBar.GetComponent<LayoutElement>();
        statusElement.minHeight = CheatMenuStyle.StatusHeight;
        HorizontalLayoutGroup statusLayout = statusBar.AddComponent<HorizontalLayoutGroup>();
        statusLayout.padding = new RectOffset(11, 11, 0, 0);
        statusLayout.spacing = 9f;
        statusLayout.childAlignment = TextAnchor.MiddleLeft;
        statusLayout.childControlWidth = true;
        statusLayout.childControlHeight = true;
        statusLayout.childForceExpandWidth = false;
        statusLayout.childForceExpandHeight = false;

        GameObject accent = CreateObject("StatusAccent", statusBar.transform, typeof(Image));
        _statusAccent = accent.GetComponent<Image>();
        _statusAccent.raycastTarget = false;
        SetLayout(accent, preferredWidth: 5f, preferredHeight: 24f, flexibleWidth: 0f, flexibleHeight: 0f);
        _statusText = CreateText(statusBar.transform, "准备就绪。", CheatMenuStyle.MutedFontSize, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
        _statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _statusText.verticalOverflow = VerticalWrapMode.Truncate;
        SetLayout(_statusText.gameObject, preferredHeight: 24f, flexibleWidth: 1f, flexibleHeight: 0f);
        ResetStatus();
    }

    private void BuildHeader(Transform parent)
    {
        GameObject row = CreateRow(parent, 46f, 10f);
        Text title = CreateText(row.transform, "单人作弊指令菜单", CheatMenuStyle.TitleFontSize, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetLayout(
            title.gameObject,
            preferredWidth: PluginLocalization.CurrentLanguage == PluginLanguage.English ? 315f : 255f);

        GameObject badgePanel = CreateObject("SingleplayerBadge", row.transform, typeof(Image));
        badgePanel.GetComponent<Image>().color = new Color(0.06f, 0.30f, 0.19f, 1f);
        SetLayout(badgePanel, preferredWidth: 165f, preferredHeight: 28f);
        Text badge = CreateText(badgePanel.transform, "仅限 SINGLEPLAYER", 12, new Color(0.55f, 1f, 0.73f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(badge.rectTransform);

        CreateFlexibleSpacer(row.transform);
        CreateButton(
            row.transform,
            PluginLocalization.SwitchButtonLabel,
            () =>
            {
                bool persisted = _plugin.ToggleLanguageFromUi();
                RebuildForLanguageChange(persisted);
            },
            58f,
            36f,
            CheatMenuStyle.Accent,
            13);
        _mapAndShortcutText = CreateText(row.transform, string.Empty, 13, CheatMenuStyle.Muted, TextAnchor.MiddleRight);
        SetLayout(_mapAndShortcutText.gameObject, preferredWidth: 250f);
        Button closeButton = CreateButton(row.transform, "×", () => _plugin.CloseMenu(), 44f, CheatMenuStyle.ButtonHeight, CheatMenuStyle.Danger, 23);
        OverlaySelectableVisual closeVisual = closeButton.GetComponent<OverlaySelectableVisual>();
        closeVisual?.SetOutlineVisible(false);
        closeVisual?.SetFocusIndicatorVisible(false);
    }

    private void RebuildForLanguageChange(bool persisted)
    {
        GameObject previousRoot = _root;
        _root = null;
        _panel = null;
        _contentHost = null;
        _mapAndShortcutText = null;
        _statusText = null;
        _statusAccent = null;
        _lastHeaderMap = string.Empty;
        _lastHeaderShortcut = string.Empty;
        _tabButtons.Clear();

        if (previousRoot != null)
        {
            QueueForDestroy(previousRoot);
        }

        EnsureShell();
        RefreshHeader();
        _root.SetActive(true);
        ShowTab(_activeTab);
        SetStatus(PluginLocalization.LanguageChangedMessage(persisted), 5f, persisted ? StatusKind.Success : StatusKind.Warning);
    }

    private void RefreshHeader()
    {
        if (_mapAndShortcutText == null)
            return;

        string mapName = string.IsNullOrWhiteSpace(Provider.map)
            ? PluginLocalization.Translate("未进入地图")
            : Provider.map;
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
        AddTab(row.transform, MenuTab.Vehicles, "载具");
        AddTab(row.transform, MenuTab.Favorites, "收藏");
        AddTab(row.transform, MenuTab.Teleports, "传送");
        AddTab(row.transform, MenuTab.Other, "其他");
    }

    private void AddTab(Transform parent, MenuTab tab, string label)
    {
        Button button = CreateButton(parent, label, () => ShowTab(tab), 0f, CheatMenuStyle.TabHeight, CheatMenuStyle.Surface, 15);
        SetLayout(button.gameObject, flexibleWidth: 1f);
        _tabButtons[tab] = button;
    }

    private void ShowTab(MenuTab tab)
    {
        _activeTab = tab;
        _plugin.SetLastMainTab(tab.ToString());
        ReplaceContentHost();
        foreach (KeyValuePair<MenuTab, Button> pair in _tabButtons)
        {
            Color color = pair.Key == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
            OverlaySelectableVisual visual = pair.Value.GetComponent<OverlaySelectableVisual>();
            if (visual != null)
                visual.SetBaseColor(color);
            else
                pair.Value.GetComponent<Image>().color = color;
        }

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
            QueueForDestroy(_contentHost);
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
        CreateSummaryPanel(
            content,
            $"生命 {life.health}/100    饱食 {life.food}/100    水分 {life.water}/100    免疫 {life.virus}/100    体力 {life.stamina}/100    氧气 {life.oxygen}/100");
        GameObject toggles = CreateRow(content, 42f, 8f);
        CreateToggleAction(toggles.transform, "无敌模式", _plugin.Actions.GodModeEnabled, value =>
        {
            _plugin.Actions.SetGodMode(value);
            SetStatus(value ? "无敌模式已开启。" : "无敌模式已关闭。");
        });
        CreateToggleAction(toggles.transform, "无限生存状态", _plugin.Actions.InfiniteNeedsEnabled, value =>
        {
            _plugin.Actions.SetInfiniteNeeds(value);
            SetStatus(value ? "无限生存状态已开启。" : "无限生存状态已关闭。");
        });

        CreateLabel(content, "设置指定数值（0–100；生命最低 1）", 12, CheatMenuStyle.Muted);
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
            SetStatus(ok ? "角色生存数值已设置。" : "设置失败。", kind: ok ? StatusKind.Success : StatusKind.Error);
        }, 76f, 34f, CheatMenuStyle.Accent);

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
        }, 110f, 34f);
        CreateLabeledInput(skillsRow.transform, "声望", _reputationAmount, value => _reputationAmount = value, 150f);
        CreateButton(skillsRow.transform, "变更声望", () =>
        {
            int amount = ParseClamped(_reputationAmount, -1000000, 1000000, 100);
            SetStatus(_plugin.Actions.AddReputation(amount) ? $"已变更 {amount:N0} 声望。" : "变更声望失败。");
        }, 110f, 34f);
        CreateAction(skillsRow.transform, "全部技能满级", () =>
        {
            int changed = _plugin.Actions.MaxAllSkills();
            SetStatus($"技能已处理，实际变更 {changed} 项。");
        });
    }

    private void BuildItemsTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
        GameObject toolbar = CreateRow(_contentHost.transform, 38f, 7f);
        CreateLabel(toolbar.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
        CreateInput(toolbar.transform, _itemQuery, value =>
        {
            _itemQuery = value;
            _searchDebouncer.Schedule(() =>
            {
                BuildItemResults();
                if (_activeTab == MenuTab.Items)
                    ShowTab(MenuTab.Items);
            });
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, CheatMenuStyle.Text, 48f);
        CreateButton(toolbar.transform, "−", () =>
        {
            _giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
            _itemAmountText = _giveAmount.ToString();
            UpdateItemAmountInput();
        }, 36f, 34f);
        _itemAmountInput = CreateInput(toolbar.transform, _itemAmountText, value =>
        {
            _itemAmountText = value;
            if (int.TryParse(value, out int parsed))
                _giveAmount = (byte)Mathf.Clamp(parsed, 1, 255);
        }, 62f);
        CreateButton(toolbar.transform, "+", () =>
        {
            _giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
            _itemAmountText = _giveAmount.ToString();
            UpdateItemAmountInput();
        }, 36f, 34f);
        CreateFlexibleSpacer(toolbar.transform);
        CreateButton(toolbar.transform, "重新扫描", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }, 105f, 34f);
        CreateLabel(_contentHost.transform, $"显示 {_itemResults.Count} / {_plugin.Catalog.Items.Count}；模组物品 {_plugin.Catalog.WorkshopItemCount}。搜索支持名称、ID、GUID 和来源。", 12, CheatMenuStyle.Muted);

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
        CreateLabel(toolbar.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
        CreateInput(toolbar.transform, _vehicleQuery, value =>
        {
            _vehicleQuery = value;
            _searchDebouncer.Schedule(() =>
            {
                BuildVehicleResults();
                if (_activeTab == MenuTab.Vehicles)
                    ShowTab(MenuTab.Vehicles);
            });
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, CheatMenuStyle.Text, 48f);
        CreateButton(toolbar.transform, "−", () =>
        {
            _spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            UpdateVehicleAmountInput();
        }, 36f, 34f);
        _vehicleAmountInput = CreateInput(toolbar.transform, _vehicleAmountText, value =>
        {
            _vehicleAmountText = value;
            if (int.TryParse(value, out int parsed))
                _spawnVehicleAmount = Mathf.Clamp(parsed, 1, 20);
        }, 62f);
        CreateButton(toolbar.transform, "+", () =>
        {
            _spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            UpdateVehicleAmountInput();
        }, 36f, 34f);
        CreateFlexibleSpacer(toolbar.transform);
        CreateButton(toolbar.transform, "重新扫描", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }, 105f, 34f);
        CreateLabel(_contentHost.transform, $"显示 {_vehicleResults.Count} / {_plugin.Catalog.Vehicles.Count}；模组车辆 {_plugin.Catalog.WorkshopVehicleCount}。每次生成 1–20 辆。", 12, CheatMenuStyle.Muted);

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
            $"物品收藏  {loadedFavoriteItems}",
            () =>
            {
                _favoriteKind = FavoriteKind.Items;
                _favoritePage = 0;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            },
            0f,
            36f,
            _favoriteKind == FavoriteKind.Items ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover,
            14);
        CreateButton(
            kindRow.transform,
            $"车辆收藏  {loadedFavoriteVehicles}",
            () =>
            {
                _favoriteKind = FavoriteKind.Vehicles;
                _favoritePage = 0;
                BuildFavoriteResults();
                ShowTab(MenuTab.Favorites);
            },
            0f,
            36f,
            _favoriteKind == FavoriteKind.Vehicles ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover,
            14);
        foreach (Transform child in kindRow.transform)
            SetLayout(child.gameObject, flexibleWidth: 1f);

        GameObject toolbar = CreateRow(_contentHost.transform, 38f, 7f);
        CreateLabel(toolbar.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
        CreateInput(toolbar.transform, _favoriteQuery, value =>
        {
            _favoriteQuery = value;
            _searchDebouncer.Schedule(() =>
            {
                BuildFavoriteResults();
                if (_activeTab == MenuTab.Favorites)
                    ShowTab(MenuTab.Favorites);
            });
        }, 280f);
        CreateLabel(toolbar.transform, "数量", 13, CheatMenuStyle.Text, 48f);

        if (_favoriteKind == FavoriteKind.Items)
        {
            CreateButton(toolbar.transform, "−", () =>
            {
                _giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
                _itemAmountText = _giveAmount.ToString();
                UpdateItemAmountInput();
            }, 36f, 34f);
            _itemAmountInput = CreateInput(toolbar.transform, _itemAmountText, value =>
            {
                _itemAmountText = value;
                if (int.TryParse(value, out int parsed))
                    _giveAmount = (byte)Mathf.Clamp(parsed, 1, 255);
            }, 62f);
            CreateButton(toolbar.transform, "+", () =>
            {
                _giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
                _itemAmountText = _giveAmount.ToString();
                UpdateItemAmountInput();
            }, 36f, 34f);
        }
        else
        {
            CreateButton(toolbar.transform, "−", () =>
            {
                _spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
                _vehicleAmountText = _spawnVehicleAmount.ToString();
                UpdateVehicleAmountInput();
            }, 36f, 34f);
            _vehicleAmountInput = CreateInput(toolbar.transform, _vehicleAmountText, value =>
            {
                _vehicleAmountText = value;
                if (int.TryParse(value, out int parsed))
                    _spawnVehicleAmount = Mathf.Clamp(parsed, 1, 20);
            }, 62f);
            CreateButton(toolbar.transform, "+", () =>
            {
                _spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
                _vehicleAmountText = _spawnVehicleAmount.ToString();
                UpdateVehicleAmountInput();
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
                $"显示 {_favoriteItemResults.Count} 个已加载收藏物品。搜索支持名称、ID、GUID 和来源。",
                12,
                CheatMenuStyle.Muted);
        }
        else
        {
            CreateLabel(
                _contentHost.transform,
                $"显示 {_favoriteVehicleResults.Count} 辆已加载收藏车辆。",
                12,
                CheatMenuStyle.Muted);
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
        sidebar.GetComponent<Image>().color = CheatMenuStyle.Surface;
        AddVerticalLayout(sidebar, 5f, new RectOffset(7, 7, 7, 7));
        SetLayout(sidebar, preferredWidth: 165f, flexibleWidth: 0f);
        CreateLabel(sidebar.transform, title, 16, CheatMenuStyle.Text);
        foreach (string category in categories)
        {
            string captured = category;
            CreateButton(sidebar.transform, category, () => select(captured), 0f, 34f, category == selected ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 13);
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

        if (_itemResults.Count == 0)
        {
            CreateEmptyState(
                column.transform,
                string.IsNullOrWhiteSpace(_itemQuery) ? "当前分类没有可用物品。" : "没有匹配的物品。",
                string.IsNullOrWhiteSpace(_itemQuery)
                    ? "请选择其他分类，或重新扫描当前加载的资产。"
                    : "请清空搜索或尝试其他关键词。");
            return;
        }
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

        if (_vehicleResults.Count == 0)
        {
            CreateEmptyState(
                column.transform,
                string.IsNullOrWhiteSpace(_vehicleQuery) ? "当前分类没有可用车辆。" : "没有匹配的车辆。",
                string.IsNullOrWhiteSpace(_vehicleQuery)
                    ? "请选择其他分类，或重新扫描当前加载的资产。"
                    : "请清空搜索或尝试其他关键词。");
            return;
        }
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
            CreateEmptyState(
                column.transform,
                _favoriteKind == FavoriteKind.Items
                    ? "当前分类没有已加载的收藏物品。"
                    : "当前分类没有已加载的收藏车辆。",
                _favoriteKind == FavoriteKind.Items
                    ? "请在物品卡片右上角点击 ☆ 收藏。"
                    : "请在车辆卡片右上角点击 ☆ 收藏。");
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
            SetStatus(ok ? $"已给予 {_giveAmount} × {asset.FriendlyName}。" : $"无法给予 {asset.FriendlyName}。", 4f, ok ? StatusKind.Success : StatusKind.Error);
        }, 155f, 158f, CheatMenuStyle.Surface);
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
            SetStatus(spawned > 0 ? $"已在玩家前方生成 {spawned} × {asset.FriendlyName}。" : $"无法生成 {asset.FriendlyName}。", 4f, spawned > 0 ? StatusKind.Success : StatusKind.Error);
        }, 155f, 158f, CheatMenuStyle.Surface);
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
            isFavorite ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover,
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
            SetStatus($"无法保存 {asset.FriendlyName} 的收藏状态。", 5f, StatusKind.Error);
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
            SetStatus($"无法保存 {asset.FriendlyName} 的收藏状态。", 5f, StatusKind.Error);
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
        Text previewState = CreateText(iconObject.transform, "加载预览…", 11, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
        Stretch(previewState.rectTransform);
        iconObject.GetComponent<OverlayIconBinder>().Initialize(raw, previewState, iconLoader, isVehicle);

        Text nameText = CreateText(parent, name, 12, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold, localize: false);
        nameText.rectTransform.anchorMin = new Vector2(0.04f, 0.13f);
        nameText.rectTransform.anchorMax = new Vector2(0.96f, 0.40f);
        nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;
        Text metaText = CreateText(parent, meta, 10, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
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
        CreateLabel(row.transform, $"第 {page + 1} / {pageCount} 页", 12, CheatMenuStyle.Muted, 110f, TextAnchor.MiddleCenter);
        CreateFlexibleSpacer(row.transform);
        Button next = CreateButton(row.transform, "下一页 ›", () => changePage(page + 1), 100f, 32f);
        next.interactable = page < pageCount - 1;
    }

    private void BuildTeleportsTab()
    {
        Transform content = _contentHost.transform;
        AddVerticalLayout(_contentHost, 8f, new RectOffset(12, 12, 10, 10));
        GameObject viewRow = CreateRow(content, 38f, 8f);
        CreateButton(
            viewRow.transform,
            "地图",
            () => SwitchTeleportView(TeleportView.Map),
            0f,
            36f,
            _teleportView == TeleportView.Map ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
            14);
        CreateButton(
            viewRow.transform,
            "传送点",
            () => SwitchTeleportView(TeleportView.Points),
            0f,
            36f,
            _teleportView == TeleportView.Points ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
            14);
        foreach (Transform child in viewRow.transform)
            SetLayout(child.gameObject, flexibleWidth: 1f);

        if (_teleportView == TeleportView.Map)
            BuildTeleportMapView(content);
        else
            BuildTeleportPointsView(content);
    }

    private void BuildTeleportMapView(Transform content)
    {
        CreateSection(content, "当前地图 GPS");
        TeleportMapSnapshot map = _plugin.TeleportMaps.ScanCurrentMap();
        string mapLayer = map.IsGps
            ? PluginLocalization.Translate("GPS/卫星图")
            : map.IsChart
                ? PluginLocalization.Translate("地形图")
                : PluginLocalization.Translate("地图预览");
        GameObject mapToolbar = CreateRow(content, 34f, 8f);
        CreateLabel(
            mapToolbar.transform,
            map.IsAvailable
                ? $"{mapLayer} · {map.MapName} · {map.Texture.width}×{map.Texture.height}"
                : map.Error,
            12,
            map.IsAvailable ? CheatMenuStyle.Muted : CheatMenuStyle.Warning);
        CreateFlexibleSpacer(mapToolbar.transform);
        CreateButton(mapToolbar.transform, "扫描地图", () =>
        {
            ShowTab(MenuTab.Teleports);
            SetStatus(
                _teleportMapSurface != null
                    ? $"已重新扫描地图：{Provider.map}。支持 Map.png / Chart.png。"
                    : "当前地图没有可用的 Map.png / Chart.png。",
                5f,
                _teleportMapSurface != null ? StatusKind.Success : StatusKind.Warning);
        }, 105f, 32f);

        if (!map.IsAvailable)
        {
            CreateEmptyState(content, "当前地图没有可用 GPS 图层。", "请确认地图目录中存在 Map.png 或 Chart.png。");
            return;
        }

        string currentMap = Provider.map ?? string.Empty;
        TeleportPoint[] points = _plugin.Teleports.Points
            .Where(point => string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        GameObject mapSurfaceObject = CreateObject("TeleportMapSurface", content, typeof(Image));
        SetLayout(mapSurfaceObject, preferredHeight: 480f, flexibleHeight: 1f);
        Outline mapOutline = mapSurfaceObject.AddComponent<Outline>();
        mapOutline.effectColor = CheatMenuStyle.TeleportBorder;
        mapOutline.effectDistance = Vector2.one;
        mapOutline.useGraphicAlpha = false;
        _teleportMapSurface = mapSurfaceObject.AddComponent<TeleportMapSurface>();
        _teleportMapSurface.Initialize(
            map.Texture,
            worldPosition =>
            {
                bool success = _plugin.Actions.TeleportToMapPosition(worldPosition);
                SetStatus(
                    success
                        ? $"已传送到地图坐标 X {worldPosition.x:F1} · Z {worldPosition.z:F1}。"
                        : "地图传送失败：未找到安全落点、目标超出地图边界或玩家正在载具中。",
                    5f,
                    success ? StatusKind.Success : StatusKind.Error);
            },
            points,
            point =>
            {
                bool success = _plugin.Actions.Teleport(point);
                SetStatus(
                    success
                        ? $"已传送到 {point.Name}。"
                        : "传送失败：目标位置有障碍物且无法放置在障碍物顶部，或玩家正在载具中。",
                    5f,
                    success ? StatusKind.Success : StatusKind.Error);
            });

        GameObject mapControls = CreateRow(content, 34f, 8f);
        CreateFlexibleSpacer(mapControls.transform);
        CreateButton(mapControls.transform, "−", () => _teleportMapSurface?.ZoomOut(), 42f, 32f);
        CreateButton(mapControls.transform, "＋", () => _teleportMapSurface?.ZoomIn(), 42f, 32f);
        CreateButton(mapControls.transform, "居中", () =>
        {
            if (Player.LocalPlayer != null)
                _teleportMapSurface?.CenterOnWorld(Player.LocalPlayer.transform.position);
        }, 70f, 32f);
        CreateLabel(mapControls.transform, "左键拖动 · 滚轮缩放 · 悬停标记查看信息 · 右键标记快速传送", 11, CheatMenuStyle.Muted, 0f, TextAnchor.MiddleRight);
    }

    private void BuildTeleportPointsView(Transform content)
    {
        Player player = Player.LocalPlayer;
        if (player == null)
        {
            CreateEmptyState(content, "玩家状态尚未准备完成。", "进入地图后即可保存和使用传送点。");
            return;
        }

        Vector3 position = player.transform.position;
        CreateSection(content, "保存当前位置");
        CreateLabel(content, $"地图 {Provider.map}    X {position.x:F1}    Y {position.y:F1}    Z {position.z:F1}", 12, CheatMenuStyle.Muted);
        GameObject saveRow = CreateRow(content, 38f, 8f);
        CreateInput(saveRow.transform, _teleportName, value => _teleportName = value, 0f, 1f);
        CreateMarkerPickerButton(
            saveRow.transform,
            _teleportMarkerKind,
            () =>
            {
                _teleportMarkerDrawerOpen = !_teleportMarkerDrawerOpen;
                ShowTab(MenuTab.Teleports);
            },
            108f,
            compact: true);
        CreateColorPickerButton(saveRow.transform);
        CreateButton(saveRow.transform, "保存位置", () =>
        {
            TeleportPoint point = _plugin.Teleports.AddCurrent(
                _teleportName,
                _teleportMarkerKind,
                ColorUtility.ToHtmlStringRGB(_teleportMarkerColor).Insert(0, "#"));
            if (point != null)
            {
                _teleportName = string.Empty;
                SetStatus($"已保存传送点：{point.Name}。", 4f);
                ShowTab(MenuTab.Teleports);
            }
        }, 120f, 35f);

        if (_teleportMarkerDrawerOpen)
        {
            GameObject markerDrawer = CreateRow(content, 58f, 7f);
            foreach (TeleportMarkerKind markerKind in MarkerKinds)
            {
                TeleportMarkerKind capturedMarkerKind = markerKind;
                CreateMarkerPickerButton(
                    markerDrawer.transform,
                    capturedMarkerKind,
                    () =>
                    {
                        _teleportMarkerKind = capturedMarkerKind;
                        _teleportMarkerDrawerOpen = false;
                        ShowTab(MenuTab.Teleports);
                    },
                    0f,
                    compact: false,
                    flexibleWidth: 1f);
            }
        }

        string currentMap = Provider.map ?? string.Empty;
        TeleportPoint[] points = _plugin.Teleports.Points
            .Where(point => string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        CreateSection(content, $"当前地图传送点（{points.Length}）");
        int pageCount = Math.Max(1, Mathf.CeilToInt(points.Length / (float)TeleportsPerOverlayPage));
        _teleportPage = Mathf.Clamp(_teleportPage, 0, pageCount - 1);
        BuildPagination(content, _teleportPage, pageCount, value => { _teleportPage = value; ShowTab(MenuTab.Teleports); });
        if (points.Length == 0)
        {
            CreateEmptyState(content, "当前地图还没有保存的传送点。", "请先输入名称并保存当前位置；不同地图的传送点会自动隔离。");
            return;
        }

        int start = _teleportPage * TeleportsPerOverlayPage;
        int end = Math.Min(start + TeleportsPerOverlayPage, points.Length);
        for (int index = start; index < end; index++)
        {
            TeleportPoint point = points[index];
            GameObject row = CreateRow(content, 62f, 8f);
            row.AddComponent<Image>().color = CheatMenuStyle.SurfaceInset;
            Outline rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = CheatMenuStyle.TeleportBorder;
            rowOutline.effectDistance = Vector2.one;
            rowOutline.useGraphicAlpha = false;
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(13, 11, 7, 7);
            rowLayout.spacing = 10f;
            CreateTeleportMarkerImage(
                row.transform,
                point.MarkerKind,
                28f,
                color: ParseMarkerColor(point.MarkerColorHex));
            Text pointText = CreateText(
                row.transform,
                $"{point.Name}\n{point.Map}  ·  ({point.X:F1}, {point.Y:F1}, {point.Z:F1})",
                13,
                CheatMenuStyle.Text,
                TextAnchor.MiddleLeft,
                localize: false);
            SetLayout(pointText.gameObject, flexibleWidth: 1f);
            CreateButton(row.transform, "传送", () =>
            {
                bool ok = _plugin.Actions.Teleport(point);
                SetStatus(ok ? $"已传送到 {point.Name}。" : "传送失败：目标被阻挡、地图不同或玩家正在载具中。", 5f, ok ? StatusKind.Success : StatusKind.Error);
            }, 105f, 38f);
            CreateButton(row.transform, "×", () => ShowTeleportDeleteConfirmation(point), 44f, 38f, CheatMenuStyle.Danger, 20);
        }
    }

    private void SwitchTeleportView(TeleportView view)
    {
        _teleportView = view;
        _plugin.SetLastTeleportView(view.ToString());
        ShowTab(MenuTab.Teleports);
        SetStatus(
            view == TeleportView.Map ? "已切换到地图视图。" : "已切换到传送点视图。",
            3f,
            StatusKind.Info);
    }

    private static MenuTab ParseMenuTab(string value)
    {
        return Enum.TryParse(value, true, out MenuTab tab) && Enum.IsDefined(typeof(MenuTab), tab)
            ? tab
            : MenuTab.Character;
    }

    private static TeleportView ParseTeleportView(string value)
    {
        return Enum.TryParse(value, true, out TeleportView view) && Enum.IsDefined(typeof(TeleportView), view)
            ? view
            : TeleportView.Map;
    }

    private void ShowTeleportDeleteConfirmation(TeleportPoint point)
    {
        HideConfirmDialog();
        _confirmRoot = CreateObject("ConfirmOverlay", _root.transform, typeof(Image));
        Stretch(_confirmRoot.GetComponent<RectTransform>());
        _confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

        GameObject dialog = CreateObject("ConfirmDialog", _confirmRoot.transform, typeof(Image));
        Image dialogImage = dialog.GetComponent<Image>();
        dialogImage.color = CheatMenuStyle.Panel;
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(470f, 190f);
        VerticalLayoutGroup layout = dialog.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateText(dialog.transform, "确认删除传送点？", CheatMenuStyle.SectionFontSize, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        CreateText(dialog.transform, $"“{point.Name}”将从当前保存列表中删除。", CheatMenuStyle.BodyFontSize, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, localize: false);
        GameObject buttons = CreateRow(dialog.transform, 38f, CheatMenuStyle.RowSpacing);
        CreateFlexibleSpacer(buttons.transform);
        Button cancel = CreateButton(buttons.transform, "取消", HideConfirmDialog, 100f, 34f, CheatMenuStyle.Surface);
        CreateButton(buttons.transform, "删除", () =>
        {
            HideConfirmDialog();
            if (_plugin.Teleports.Remove(point.Id))
            {
                SetStatus($"已删除传送点：{point.Name}。", 4f);
                ShowTab(MenuTab.Teleports);
            }
        }, 100f, 34f, CheatMenuStyle.Danger);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(cancel.gameObject);
    }

    private void HideConfirmDialog()
    {
        if (_confirmRoot == null)
            return;

        QueueForDestroy(_confirmRoot);
        _confirmRoot = null;
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
        CreateLabeledInput(timeRow.transform, "时间百分比", _timePercentText, value =>
        {
            _timePercentText = value;
            if (int.TryParse(value, out int percent) && _timeSlider != null)
            {
                percent = Mathf.Clamp(percent, 0, 99);
                _timeSlider.SetValueWithoutNotify(percent / 100f);
                UpdateTimePreview(percent / 100f, cycle);
            }
        }, 210f);
        _timeSlider = CreateTimeSlider(timeRow.transform, normalized, value =>
        {
            _timePercentText = Mathf.RoundToInt(value * 100f).ToString();
            UpdateTimePreview(value, cycle);
            int percent = Mathf.Clamp(Mathf.RoundToInt(value * 100f), 0, 99);
            _timeApplyDebouncer.Schedule(() => ApplyTimePercent(percent, cycle));
        });
        CreateButton(timeRow.transform, "应用", () =>
        {
            _timeApplyDebouncer.Cancel();
            ApplyTimePercent(ParseClamped(_timePercentText, 0, 99, 50), cycle);
        }, 82f, 34f);
        CreateAction(timeRow.transform, "设为白天", () =>
        {
            _timeApplyDebouncer.Cancel();
            _plugin.Actions.SetDay();
            SetStatus("已切换到白天。");
        });
        CreateAction(timeRow.transform, "设为夜晚", () =>
        {
            _timeApplyDebouncer.Cancel();
            _plugin.Actions.SetNight();
            SetStatus("已切换到夜晚。");
        });
        _timePreviewText = CreateLabel(content, $"预览：{Mathf.RoundToInt(normalized * 100f)}%", 12, CheatMenuStyle.Muted);

        GameObject switches = CreateRow(content, 42f, 8f);
        CreateToggleAction(switches.transform, "冻结时间", _plugin.Actions.FreezeTimeEnabled, value =>
        {
            _plugin.Actions.SetFreezeTime(value);
            SetStatus(value ? "时间已冻结。" : "时间已恢复流动。");
        });
        CreateToggleAction(switches.transform, "强制满月", LightingManager.isFullMoon, value =>
        {
            _plugin.Actions.SetFullMoon(value);
            SetStatus(value ? "已开启满月。" : "已关闭满月。");
        });

        CreateSection(content, "世界事件");
        GameObject eventsRow = CreateRow(content, 42f, 8f);
        CreateAction(eventsRow.transform, "立即呼叫空投", () =>
        {
            bool ok = _plugin.Actions.CallAirdrop();
            SetStatus(ok ? "空投已呼叫。" : "此地图没有可用的空投节点或货物表。", 5f, ok ? StatusKind.Success : StatusKind.Warning);
        });
        CreateAction(eventsRow.transform, "下雨", () =>
        {
            bool ok = _plugin.Actions.StartRain();
            SetStatus(ok ? "已触发雨天。" : "当前地图没有默认雨天资产。", kind: ok ? StatusKind.Success : StatusKind.Warning);
        });
        CreateAction(eventsRow.transform, "暴雪", () =>
        {
            bool ok = _plugin.Actions.StartSnow();
            SetStatus(ok ? "已触发暴雪。" : "当前地图没有默认雪天资产。", kind: ok ? StatusKind.Success : StatusKind.Warning);
        });
        GameObject weatherRow = CreateRow(content, 42f, 8f);
        CreateAction(weatherRow.transform, "清除当前天气", () => { _plugin.Actions.ClearWeather(); SetStatus("当前天气已清除，后续仍可自然调度。"); });
        CreateAction(weatherRow.transform, "关闭天气调度", () => { _plugin.Actions.DisableWeather(); SetStatus("当前天气和自然天气调度已关闭。", kind: StatusKind.Warning); });

        CreateSection(content, "扫描信息");
        CreateLabel(content, $"物品：{_plugin.Catalog.Items.Count}（模组 {_plugin.Catalog.WorkshopItemCount}）    车辆：{_plugin.Catalog.Vehicles.Count}（模组 {_plugin.Catalog.WorkshopVehicleCount}）", 14);
        CreateLabel(content, "只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。", 12, CheatMenuStyle.Muted);
        GameObject scanRow = CreateRow(content, 40f, 8f);
        CreateButton(scanRow.transform, "重新扫描模组资产", () =>
        {
            _plugin.RefreshCatalog();
            SetStatus("资产目录已重新扫描。", 4f, StatusKind.Info);
        }, 180f, 36f, CheatMenuStyle.SurfaceRaised);
    }

    private void CreateToggleAction(Transform parent, string label, bool current, Action<bool> change)
    {
        GameObject toggleObject = CreateObject("Toggle", parent, typeof(Image), typeof(Toggle));
        Image background = toggleObject.GetComponent<Image>();
        background.color = current ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
        Outline outline = toggleObject.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.ToggleBorder;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.88f, 0.94f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        toggle.colors = colors;

        Text checkmark = CreateText(toggleObject.transform, "✓", 17, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        checkmark.rectTransform.anchorMin = new Vector2(0f, 0.1f);
        checkmark.rectTransform.anchorMax = new Vector2(0f, 0.9f);
        checkmark.rectTransform.pivot = new Vector2(0f, 0.5f);
        checkmark.rectTransform.sizeDelta = new Vector2(30f, 0f);
        checkmark.rectTransform.anchoredPosition = new Vector2(7f, 0f);
        toggle.graphic = checkmark;

        Text text = CreateText(toggleObject.transform, $"{label}  {(current ? "ON" : "OFF")}", 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetOffsets(text.rectTransform, 43f, 8f, 2f, 2f);
        GameObject focusIndicator = CreateObject("FocusIndicator", toggleObject.transform, typeof(Image));
        Image focusImage = focusIndicator.GetComponent<Image>();
        focusImage.color = CheatMenuStyle.AccentBright;
        focusImage.raycastTarget = false;
        RectTransform focusRect = focusIndicator.GetComponent<RectTransform>();
        focusRect.anchorMin = new Vector2(0f, 0f);
        focusRect.anchorMax = new Vector2(1f, 0f);
        focusRect.pivot = new Vector2(0.5f, 0f);
        focusRect.anchoredPosition = Vector2.zero;
        focusRect.sizeDelta = new Vector2(0f, 3f);
        focusIndicator.SetActive(false);
        toggleObject.AddComponent<OverlayFocusIndicator>().Initialize(focusIndicator);
        toggle.isOn = current;
        toggle.onValueChanged.AddListener(value =>
        {
            background.color = value ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
            text.text = $"{PluginLocalization.Translate(label)}  {(value ? "ON" : "OFF")}";
            change(value);
        });
        SetLayout(toggleObject, flexibleWidth: 1f, preferredHeight: 40f);
    }

    private void CreateAction(Transform parent, string label, System.Action action)
    {
        Button button = CreateButton(parent, label, action, 0f, 40f);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void CreatePrimaryAction(Transform parent, string label, System.Action action)
    {
        Button button = CreateButton(parent, label, action, 0f, 40f, CheatMenuStyle.Accent);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void CreateSection(Transform parent, string text)
    {
        GameObject row = CreateRow(parent, 30f, 8f);
        GameObject marker = CreateObject("SectionMarker", row.transform, typeof(Image));
        Image markerImage = marker.GetComponent<Image>();
        markerImage.color = CheatMenuStyle.Accent;
        markerImage.raycastTarget = false;
        SetLayout(marker, preferredWidth: 4f, preferredHeight: 18f, flexibleWidth: 0f);
        Text label = CreateText(row.transform, text, CheatMenuStyle.SectionFontSize, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetLayout(label.gameObject, flexibleWidth: 1f);
    }

    private void CreateSummaryPanel(Transform parent, string text)
    {
        GameObject panel = CreateObject("Summary", parent, typeof(Image));
        panel.GetComponent<Image>().color = CheatMenuStyle.SurfaceRaised;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.SummaryBorder;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        SetLayout(panel, preferredHeight: 42f);
        Text label = CreateText(panel.transform, text, 14, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetOffsets(label.rectTransform, 12f, 12f, 4f, 4f);
    }

    private void CreateEmptyState(Transform parent, string title, string hint)
    {
        GameObject panel = CreateObject("EmptyState", parent, typeof(Image));
        panel.GetComponent<Image>().color = CheatMenuStyle.SurfaceRaised;
        VerticalLayoutGroup layout = AddVerticalLayout(panel, 4f, new RectOffset(18, 18, 14, 14));
        layout.childAlignment = TextAnchor.MiddleCenter;
        SetLayout(panel, preferredHeight: 92f, flexibleWidth: 1f);
        Text titleText = CreateText(panel.transform, title, 16, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetLayout(titleText.gameObject, preferredHeight: 28f);
        Text hintText = CreateText(panel.transform, hint, 12, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
        SetLayout(hintText.gameObject, preferredHeight: 24f);
    }

    private Text CreateLabel(Transform parent, string text, int size, Color? color = null, float width = 0f, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        Text label = CreateText(parent, text, size, color ?? CheatMenuStyle.Text, alignment);
        SetLayout(label.gameObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: 27f);
        return label;
    }

    private void CreateLabeledInput(Transform parent, string label, string value, Action<string> changed, float width = 120f)
    {
        GameObject group = CreateRow(parent, 34f, 4f);
        SetLayout(group, preferredWidth: width, flexibleWidth: 0f);
        string localizedLabel = PluginLocalization.Translate(label);
        float labelWidth = PluginLocalization.CurrentLanguage == PluginLanguage.English
            ? Mathf.Clamp(localizedLabel.Length * 7f + 10f, 58f, Math.Min(112f, width * 0.62f))
            : 46f;
        CreateLabel(group.transform, localizedLabel, 12, CheatMenuStyle.Muted, labelWidth, TextAnchor.MiddleRight);
        CreateInput(group.transform, value, changed, 0f, 1f);
    }

    private InputField CreateInput(Transform parent, string value, Action<string> changed, float width = 0f, float flexibleWidth = 0f)
    {
        GameObject inputObject = CreateObject("Input", parent, typeof(Image), typeof(InputField));
        Image background = inputObject.GetComponent<Image>();
        background.color = CheatMenuStyle.SurfaceInput;
        Outline outline = inputObject.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.InputBorder;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        InputField field = inputObject.GetComponent<InputField>();
        field.targetGraphic = background;
        ColorBlock inputColors = field.colors;
        inputColors.normalColor = Color.white;
        inputColors.highlightedColor = new Color(1.05f, 1.08f, 1.12f, 1f);
        inputColors.pressedColor = new Color(0.90f, 0.94f, 1f, 1f);
        inputColors.selectedColor = new Color(0.95f, 1f, 1f, 1f);
        inputColors.disabledColor = new Color(0.45f, 0.48f, 0.52f, 0.55f);
        field.colors = inputColors;
        field.text = value ?? string.Empty;
        field.lineType = InputField.LineType.SingleLine;
        field.contentType = InputField.ContentType.Standard;
        Text text = CreateText(inputObject.transform, field.text, 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, localize: false);
        SetOffsets(text.rectTransform, 9f, 9f, 2f, 2f);
        field.textComponent = text;
        Text placeholder = CreateText(inputObject.transform, "输入…", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
        SetOffsets(placeholder.rectTransform, 9f, 9f, 2f, 2f);
        field.placeholder = placeholder;
        field.onValueChanged.AddListener(changed.Invoke);
        GameObject focusIndicator = CreateObject("FocusIndicator", inputObject.transform, typeof(Image));
        Image focusImage = focusIndicator.GetComponent<Image>();
        focusImage.color = CheatMenuStyle.AccentBright;
        focusImage.raycastTarget = false;
        RectTransform focusRect = focusIndicator.GetComponent<RectTransform>();
        focusRect.anchorMin = new Vector2(0f, 0f);
        focusRect.anchorMax = new Vector2(0f, 1f);
        focusRect.pivot = new Vector2(0f, 0.5f);
        focusRect.anchoredPosition = Vector2.zero;
        focusRect.sizeDelta = new Vector2(3f, 0f);
        focusIndicator.SetActive(false);
        inputObject.AddComponent<OverlayFocusIndicator>().Initialize(focusIndicator);
        SetLayout(inputObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: 34f, flexibleWidth: flexibleWidth);
        return field;
    }

    private Slider CreateTimeSlider(Transform parent, float normalized, Action<float> changed)
    {
        GameObject sliderObject = CreateObject("TimeSlider", parent, typeof(Image), typeof(Slider));
        Image background = sliderObject.GetComponent<Image>();
        background.color = CheatMenuStyle.SurfaceInset;
        Outline sliderOutline = sliderObject.AddComponent<Outline>();
        sliderOutline.effectColor = CheatMenuStyle.SliderBorder;
        sliderOutline.effectDistance = Vector2.one;
        sliderOutline.useGraphicAlpha = false;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(normalized);
        slider.direction = Slider.Direction.LeftToRight;
        SetLayout(
            sliderObject,
            preferredWidth: CheatMenuStyle.SliderWidth,
            preferredHeight: CheatMenuStyle.SliderHeight,
            flexibleWidth: 0f,
            flexibleHeight: 0f);

        GameObject fillArea = CreateObject("FillArea", sliderObject.transform, typeof(Image));
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillArea.GetComponent<Image>().color = CheatMenuStyle.SliderTrack;
        Outline trackOutline = fillArea.AddComponent<Outline>();
        trackOutline.effectColor = CheatMenuStyle.InputBorder;
        trackOutline.effectDistance = Vector2.one;
        trackOutline.useGraphicAlpha = false;
        fillAreaRect.anchorMin = new Vector2(0f, 0.34f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.66f);
        fillAreaRect.offsetMin = new Vector2(7f, 0f);
        fillAreaRect.offsetMax = new Vector2(-7f, 0f);
        GameObject fill = CreateObject("Fill", fillArea.transform, typeof(Image));
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = CheatMenuStyle.Accent;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        GameObject handle = CreateObject("Handle", sliderObject.transform, typeof(Image));
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = GetSliderGripSprite();
        handleImage.color = Color.white;
        handleImage.preserveAspect = true;
        Outline handleOutline = handle.AddComponent<Outline>();
        handleOutline.effectColor = CheatMenuStyle.SliderBorder;
        handleOutline.effectDistance = Vector2.one;
        handleOutline.useGraphicAlpha = false;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 18f);
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        slider.handleRect = handleRect;
        slider.onValueChanged.AddListener(changed.Invoke);
        return slider;
    }

    private void UpdateTimePreview(float normalized, uint cycle)
    {
        if (_timePreviewText != null)
            _timePreviewText.text = $"预览：{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%";
    }

    private void ApplyTimePercent(int percent, uint cycle)
    {
        percent = Mathf.Clamp(percent, 0, 99);
        _timePercentText = percent.ToString();
        _plugin.Actions.SetTime((uint)(cycle * (percent / 100f)));
        if (_timeSlider != null)
            _timeSlider.SetValueWithoutNotify(percent / 100f);
        UpdateTimePreview(percent / 100f, cycle);
        SetStatus($"时间已设置为周期的 {percent}% 。");
    }

    private void UpdateItemAmountInput()
    {
        if (_itemAmountInput != null)
            _itemAmountInput.SetTextWithoutNotify(_itemAmountText);
    }

    private void UpdateVehicleAmountInput()
    {
        if (_vehicleAmountInput != null)
            _vehicleAmountInput.SetTextWithoutNotify(_vehicleAmountText);
    }

    private Button CreateMarkerPickerButton(
        Transform parent,
        TeleportMarkerKind markerKind,
        System.Action action,
        float width,
        bool compact,
        float flexibleWidth = 0f)
    {
        Color buttonColor = _teleportMarkerKind == markerKind
            ? CheatMenuStyle.Accent
            : CheatMenuStyle.SurfaceHover;
        float height = compact ? 34f : 54f;
        Button button = CreateButton(parent, string.Empty, action, width, height, buttonColor, 11);
        SetLayout(
            button.gameObject,
            preferredWidth: width > 0f ? width : -1f,
            preferredHeight: height,
            flexibleWidth: flexibleWidth);

        Text generatedText = button.GetComponentInChildren<Text>(true);
        if (generatedText != null)
            generatedText.gameObject.SetActive(false);

        CreateTeleportMarkerImage(button.transform, markerKind, compact ? 22f : 26f, compact, _teleportMarkerColor);
        if (compact)
        {
            Text label = CreateText(
                button.transform,
                PluginLocalization.Translate(MarkerLabel(markerKind)),
                10,
                CheatMenuStyle.Text,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(34f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -2f);
        }
        else
        {
            Text label = CreateText(
                button.transform,
                PluginLocalization.Translate(MarkerLabel(markerKind)),
                10,
                CheatMenuStyle.Text,
                TextAnchor.LowerCenter,
                FontStyle.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(2f, 1f);
            label.rectTransform.offsetMax = new Vector2(-2f, 19f);
        }

        return button;
    }

    private Button CreateColorPickerButton(Transform parent)
    {
        Button button = CreateButton(
            parent,
            string.Empty,
            ShowTeleportColorPicker,
            82f,
            34f,
            CheatMenuStyle.SurfaceHover,
            11);

        Text generatedText = button.GetComponentInChildren<Text>(true);
        if (generatedText != null)
            generatedText.gameObject.SetActive(false);

        GameObject swatchObject = CreateObject("ColorSwatch", button.transform, typeof(Image));
        Image swatch = swatchObject.GetComponent<Image>();
        swatch.color = _teleportMarkerColor;
        swatch.raycastTarget = false;
        RectTransform swatchRect = swatch.rectTransform;
        swatchRect.anchorMin = new Vector2(0f, 0.5f);
        swatchRect.anchorMax = new Vector2(0f, 0.5f);
        swatchRect.pivot = new Vector2(0.5f, 0.5f);
        swatchRect.anchoredPosition = new Vector2(17f, 0f);
        swatchRect.sizeDelta = new Vector2(20f, 20f);
        Outline swatchOutline = swatchObject.AddComponent<Outline>();
        swatchOutline.effectColor = Color.white;
        swatchOutline.effectDistance = Vector2.one;
        swatchOutline.useGraphicAlpha = false;

        Text label = CreateText(
            button.transform,
            "颜色",
            11,
            CheatMenuStyle.Text,
            TextAnchor.MiddleLeft,
            FontStyle.Bold);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(33f, 2f);
        label.rectTransform.offsetMax = new Vector2(-4f, -2f);
        return button;
    }

    private void ShowTeleportColorPicker()
    {
        HideTeleportColorPicker();
        _teleportColorDraft = _teleportMarkerColor;

        _teleportColorPickerRoot = CreateObject("TeleportColorPickerOverlay", _root.transform, typeof(Image));
        Stretch(_teleportColorPickerRoot.GetComponent<RectTransform>());
        _teleportColorPickerRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);

        GameObject dialog = CreateObject("TeleportColorPickerDialog", _teleportColorPickerRoot.transform, typeof(Image));
        Image dialogImage = dialog.GetComponent<Image>();
        dialogImage.color = CheatMenuStyle.Panel;
        Outline dialogOutline = dialog.AddComponent<Outline>();
        dialogOutline.effectColor = CheatMenuStyle.InputBorder;
        dialogOutline.effectDistance = Vector2.one;
        dialogOutline.useGraphicAlpha = false;
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(780f, 620f);

        VerticalLayoutGroup dialogLayout = dialog.AddComponent<VerticalLayoutGroup>();
        dialogLayout.padding = new RectOffset(28, 28, 20, 20);
        dialogLayout.spacing = 12f;
        dialogLayout.childControlWidth = true;
        dialogLayout.childControlHeight = true;
        dialogLayout.childForceExpandWidth = true;
        dialogLayout.childForceExpandHeight = false;

        GameObject header = CreateRow(dialog.transform, 48f, 10f);
        GameObject headerSwatch = CreateObject("HeaderColor", header.transform, typeof(Image));
        headerSwatch.GetComponent<Image>().color = _teleportColorDraft;
        headerSwatch.GetComponent<Image>().raycastTarget = false;
        SetLayout(headerSwatch, preferredWidth: 34f, preferredHeight: 34f);
        Outline headerSwatchOutline = headerSwatch.AddComponent<Outline>();
        headerSwatchOutline.effectColor = Color.white;
        headerSwatchOutline.effectDistance = Vector2.one;
        headerSwatchOutline.useGraphicAlpha = false;
        CreateText(header.transform, "选择颜色", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
        CreateFlexibleSpacer(header.transform);
        CreateButton(header.transform, "×", HideTeleportColorPicker, 44f, 42f, CheatMenuStyle.Surface, 23);

        GameObject body = CreateRow(dialog.transform, 0f, 26f);
        SetLayout(body, flexibleHeight: 1f);

        GameObject previewColumn = CreateObject("PreviewColumn", body.transform, typeof(Image));
        previewColumn.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
        AddVerticalLayout(previewColumn, 8f, new RectOffset(0, 0, 0, 0));
        SetLayout(previewColumn, preferredWidth: 326f, flexibleWidth: 0f, flexibleHeight: 1f);
        CreateLabel(previewColumn.transform, "预览", 16, CheatMenuStyle.Text);
        GameObject previewObject = CreateObject("Preview", previewColumn.transform, typeof(Image));
        _teleportColorPreview = previewObject.GetComponent<Image>();
        _teleportColorPreview.raycastTarget = false;
        SetLayout(previewObject, preferredHeight: 158f, flexibleWidth: 1f);
        Outline previewOutline = previewObject.AddComponent<Outline>();
        previewOutline.effectColor = CheatMenuStyle.InputBorder;
        previewOutline.effectDistance = Vector2.one;
        previewOutline.useGraphicAlpha = false;
        GameObject previewBadge = CreateObject("PreviewHex", previewObject.transform, typeof(Image));
        previewBadge.GetComponent<Image>().color = new Color(0.02f, 0.12f, 0.22f, 0.76f);
        RectTransform previewBadgeRect = previewBadge.GetComponent<RectTransform>();
        previewBadgeRect.anchorMin = previewBadgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewBadgeRect.pivot = new Vector2(0.5f, 0.5f);
        previewBadgeRect.sizeDelta = new Vector2(132f, 48f);
        _teleportColorHexPreview = CreateText(
            previewBadge.transform,
            string.Empty,
            18,
            Color.white,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            localize: false);
        Stretch(_teleportColorHexPreview.rectTransform);
        CreateLabel(previewColumn.transform, "常用颜色", 16, CheatMenuStyle.Text);
        GameObject presetGrid = CreateObject("PresetGrid", previewColumn.transform);
        GridLayoutGroup presetLayout = presetGrid.AddComponent<GridLayoutGroup>();
        presetLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        presetLayout.constraintCount = 6;
        presetLayout.cellSize = new Vector2(48f, 42f);
        presetLayout.spacing = new Vector2(10f, 8f);
        presetLayout.childAlignment = TextAnchor.UpperLeft;
        SetLayout(presetGrid, preferredHeight: 142f, flexibleWidth: 1f);
        _teleportColorSwatches.Clear();
        foreach (string preset in TeleportMarkerColorPresets)
        {
            if (ColorUtility.TryParseHtmlString(preset, out Color presetColor))
                CreateColorSwatchButton(presetGrid.transform, presetColor);
        }

        GameObject controls = CreateObject("ColorControls", body.transform, typeof(Image));
        controls.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
        AddVerticalLayout(controls, 8f, new RectOffset(0, 0, 0, 0));
        SetLayout(controls, flexibleWidth: 1f, flexibleHeight: 1f);
        CreateLabel(controls.transform, "颜色值", 16, CheatMenuStyle.Text);
        _teleportColorHexInput = CreateInput(controls.transform, string.Empty, value =>
        {
            TryUpdateTeleportDraftFromHex(value);
        }, 0f, 1f);
        SetLayout(_teleportColorHexInput.gameObject, preferredHeight: 38f, flexibleWidth: 1f);
        _teleportColorHexInput.onEndEdit.AddListener(TryUpdateTeleportDraftFromHex);
        CreateLabel(controls.transform, "通道", 16, CheatMenuStyle.Text);

        GameObject redRow = CreateRow(controls.transform, 36f, 8f);
        CreateLabel(redRow.transform, "红", 13, CheatMenuStyle.Text, 32f);
        _teleportColorRedSlider = CreateColorChannelSlider(
            redRow.transform,
            _teleportColorDraft.r * 255f,
            new Color(0.75f, 0.24f, 0.24f, 1f),
            value =>
            {
                _teleportColorDraft.r = value / 255f;
                UpdateTeleportColorPickerControls();
            },
            out _teleportColorRedValue);
        GameObject greenRow = CreateRow(controls.transform, 36f, 8f);
        CreateLabel(greenRow.transform, "绿", 13, CheatMenuStyle.Text, 32f);
        _teleportColorGreenSlider = CreateColorChannelSlider(
            greenRow.transform,
            _teleportColorDraft.g * 255f,
            new Color(0.25f, 0.68f, 0.30f, 1f),
            value =>
            {
                _teleportColorDraft.g = value / 255f;
                UpdateTeleportColorPickerControls();
            },
            out _teleportColorGreenValue);
        GameObject blueRow = CreateRow(controls.transform, 36f, 8f);
        CreateLabel(blueRow.transform, "蓝", 13, CheatMenuStyle.Text, 32f);
        _teleportColorBlueSlider = CreateColorChannelSlider(
            blueRow.transform,
            _teleportColorDraft.b * 255f,
            new Color(0.22f, 0.50f, 0.78f, 1f),
            value =>
            {
                _teleportColorDraft.b = value / 255f;
                UpdateTeleportColorPickerControls();
            },
            out _teleportColorBlueValue);

        GameObject footer = CreateRow(dialog.transform, 48f, 10f);
        CreateFlexibleSpacer(footer.transform);
        CreateButton(footer.transform, "取消", HideTeleportColorPicker, 110f, 38f, CheatMenuStyle.Surface);
        CreateButton(footer.transform, "确定", () =>
        {
            _teleportMarkerColor = _teleportColorDraft;
            HideTeleportColorPicker();
            ShowTab(MenuTab.Teleports);
        }, 110f, 38f, CheatMenuStyle.Accent);

        UpdateTeleportColorPickerControls();
        if (EventSystem.current != null && _teleportColorHexInput != null)
            EventSystem.current.SetSelectedGameObject(_teleportColorHexInput.gameObject);
    }

    private void HideTeleportColorPicker()
    {
        if (_teleportColorPickerRoot != null)
            QueueForDestroy(_teleportColorPickerRoot);

        _teleportColorPickerRoot = null;
        _teleportColorPreview = null;
        _teleportColorHexPreview = null;
        _teleportColorHexInput = null;
        _teleportColorRedSlider = null;
        _teleportColorGreenSlider = null;
        _teleportColorBlueSlider = null;
        _teleportColorRedValue = null;
        _teleportColorGreenValue = null;
        _teleportColorBlueValue = null;
        _teleportColorSwatches.Clear();
    }

    private void UpdateTeleportColorPickerControls()
    {
        if (_teleportColorPreview != null)
            _teleportColorPreview.color = _teleportColorDraft;
        if (_teleportColorHexPreview != null)
            _teleportColorHexPreview.text = ToMarkerColorHex(_teleportColorDraft);
        if (_teleportColorHexInput != null)
            _teleportColorHexInput.SetTextWithoutNotify(ToMarkerColorHex(_teleportColorDraft));

        int red = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.r) * 255f);
        int green = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.g) * 255f);
        int blue = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.b) * 255f);
        if (_teleportColorRedSlider != null)
            _teleportColorRedSlider.SetValueWithoutNotify(red);
        if (_teleportColorGreenSlider != null)
            _teleportColorGreenSlider.SetValueWithoutNotify(green);
        if (_teleportColorBlueSlider != null)
            _teleportColorBlueSlider.SetValueWithoutNotify(blue);
        if (_teleportColorRedValue != null)
            _teleportColorRedValue.text = red.ToString();
        if (_teleportColorGreenValue != null)
            _teleportColorGreenValue.text = green.ToString();
        if (_teleportColorBlueValue != null)
            _teleportColorBlueValue.text = blue.ToString();

        foreach (ColorSwatchControl swatch in _teleportColorSwatches)
        {
            if (swatch?.Check != null)
                swatch.Check.SetActive(ColorsMatch(swatch.Color, _teleportColorDraft));
        }
    }

    private void TryUpdateTeleportDraftFromHex(string value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (!candidate.StartsWith("#", StringComparison.Ordinal))
            candidate = "#" + candidate;
        if (candidate.Length != 7 || !ColorUtility.TryParseHtmlString(candidate, out Color color))
            return;

        color.a = 1f;
        _teleportColorDraft = color;
        UpdateTeleportColorPickerControls();
    }

    private void CreateColorSwatchButton(Transform parent, Color color)
    {
        Button button = CreateButton(
            parent,
            string.Empty,
            () =>
            {
                _teleportColorDraft = color;
                UpdateTeleportColorPickerControls();
            },
            48f,
            42f,
            CheatMenuStyle.SurfaceHover,
            10);
        Text generatedText = button.GetComponentInChildren<Text>(true);
        if (generatedText != null)
            generatedText.gameObject.SetActive(false);

        GameObject swatchObject = CreateObject("Swatch", button.transform, typeof(Image));
        Image swatchImage = swatchObject.GetComponent<Image>();
        swatchImage.color = color;
        swatchImage.raycastTarget = false;
        SetOffsets(swatchImage.rectTransform, 3f, 3f, 3f, 3f);
        Outline swatchOutline = swatchObject.AddComponent<Outline>();
        swatchOutline.effectColor = new Color(1f, 1f, 1f, 0.80f);
        swatchOutline.effectDistance = Vector2.one;
        swatchOutline.useGraphicAlpha = false;

        GameObject check = CreateObject("Check", button.transform, typeof(Text));
        Text checkText = check.GetComponent<Text>();
        checkText.font = _font;
        checkText.text = "✓";
        checkText.fontSize = 20;
        checkText.fontStyle = FontStyle.Bold;
        checkText.color = Color.white;
        checkText.alignment = TextAnchor.MiddleCenter;
        checkText.raycastTarget = false;
        Stretch(checkText.rectTransform);
        ColorSwatchControl control = new() { Color = color, Check = check };
        _teleportColorSwatches.Add(control);
        check.SetActive(ColorsMatch(color, _teleportColorDraft));
    }

    private Slider CreateColorChannelSlider(
        Transform parent,
        float value,
        Color fillColor,
        Action<int> changed,
        out Text valueText)
    {
        GameObject sliderObject = CreateObject("ColorChannelSlider", parent, typeof(Image), typeof(Slider));
        Image background = sliderObject.GetComponent<Image>();
        background.color = CheatMenuStyle.SurfaceInset;
        background.raycastTarget = false;
        Outline sliderOutline = sliderObject.AddComponent<Outline>();
        sliderOutline.effectColor = CheatMenuStyle.SliderBorder;
        sliderOutline.effectDistance = Vector2.one;
        sliderOutline.useGraphicAlpha = false;
        SetLayout(sliderObject, flexibleWidth: 1f, preferredHeight: 30f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 255f;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(value, 0f, 255f);
        slider.direction = Slider.Direction.LeftToRight;

        GameObject fillArea = CreateObject("FillArea", sliderObject.transform, typeof(Image));
        Image fillAreaImage = fillArea.GetComponent<Image>();
        fillAreaImage.color = CheatMenuStyle.SliderTrack;
        fillAreaImage.raycastTarget = false;
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.34f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.66f);
        fillAreaRect.offsetMin = new Vector2(7f, 0f);
        fillAreaRect.offsetMax = new Vector2(-7f, 0f);
        GameObject fill = CreateObject("Fill", fillArea.transform, typeof(Image));
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        GameObject handle = CreateObject("Handle", sliderObject.transform, typeof(Image));
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = GetSliderGripSprite();
        handleImage.color = Color.white;
        handleImage.preserveAspect = true;
        handleImage.raycastTarget = false;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 18f);
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        slider.handleRect = handleRect;

        valueText = CreateText(parent, "0", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, localize: false);
        SetLayout(valueText.gameObject, preferredWidth: 30f, preferredHeight: 30f);
        slider.onValueChanged.AddListener(channelValue => changed(Mathf.RoundToInt(channelValue)));
        return slider;
    }

    private static string ToMarkerColorHex(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            1f));
    }

    private static bool ColorsMatch(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) < 0.002f
            && Mathf.Abs(left.g - right.g) < 0.002f
            && Mathf.Abs(left.b - right.b) < 0.002f;
    }

    private Image CreateTeleportMarkerImage(
        Transform parent,
        TeleportMarkerKind markerKind,
        float size,
        bool compact = false,
        Color? color = null)
    {
        GameObject markerObject = CreateObject("TeleportMarkerIcon", parent, typeof(Image));
        Image marker = markerObject.GetComponent<Image>();
        marker.sprite = GetTeleportMarkerSprite(markerKind);
        marker.color = color ?? new Color(1f, 0.82f, 0.08f, 1f);
        marker.preserveAspect = true;
        marker.raycastTarget = false;
        RectTransform rect = marker.rectTransform;
        rect.anchorMin = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
        rect.anchorMax = compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = compact ? new Vector2(18f, 0f) : new Vector2(0f, -16f);
        rect.sizeDelta = new Vector2(size, size);
        SetLayout(markerObject, preferredWidth: size, preferredHeight: size, flexibleWidth: 0f, flexibleHeight: 0f);
        return marker;
    }

    private Sprite GetTeleportMarkerSprite(TeleportMarkerKind markerKind)
    {
        if (_teleportMarkerSprites.TryGetValue(markerKind, out Sprite sprite) && sprite != null)
            return sprite;

        sprite = TeleportMarkerIconFactory.CreateSprite(
            markerKind,
            32,
            out Texture2D texture);
        _teleportMarkerSprites[markerKind] = sprite;
        _teleportMarkerTextures[markerKind] = texture;
        return sprite;
    }

    private static string MarkerLabel(TeleportMarkerKind markerKind)
    {
        return markerKind switch
        {
            TeleportMarkerKind.Square => "正方形",
            TeleportMarkerKind.Circle => "圆形",
            TeleportMarkerKind.Diamond => "菱形",
            _ => "五角星"
        };
    }

    private static Color ParseMarkerColor(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color)
            ? color
            : new Color(0.96f, 0.77f, 0.26f, 1f);
    }

    private Button CreateButton(Transform parent, string label, System.Action action, float width = 0f, float height = 36f, Color? color = null, int fontSize = 13)
    {
        GameObject buttonObject = CreateObject("Button", parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = color ?? CheatMenuStyle.Surface;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.ButtonBorder;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.84f, 0.90f, 1f);
        colors.disabledColor = new Color(0.42f, 0.45f, 0.49f, 0.55f);
        button.colors = colors;
        if (action != null)
            button.onClick.AddListener(action.Invoke);
        Text text = null;
        if (label == "×" || label == "★" || label == "☆")
        {
            GameObject glyphObject = CreateObject("Glyph", buttonObject.transform, typeof(Image));
            Image glyphImage = glyphObject.GetComponent<Image>();
            glyphImage.sprite = label == "×"
                ? GetCloseGlyphSprite()
                : label == "★"
                    ? GetFavoriteFilledSprite()
                    : GetFavoriteOutlineSprite();
            glyphImage.color = Color.white;
            glyphImage.preserveAspect = true;
            glyphImage.raycastTarget = false;
            RectTransform glyphRect = glyphObject.GetComponent<RectTransform>();
            glyphRect.anchorMin = glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.anchoredPosition = Vector2.zero;
            glyphRect.sizeDelta = label == "×"
                ? new Vector2(20f, 20f)
                : new Vector2(21f, 21f);
        }
        else
        {
            text = CreateText(buttonObject.transform, label, fontSize, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetOffsets(text.rectTransform, 4f, 4f, 2f, 2f);
        }
        GameObject focusIndicator = CreateObject("FocusIndicator", buttonObject.transform, typeof(Image));
        Image focusImage = focusIndicator.GetComponent<Image>();
        focusImage.color = CheatMenuStyle.AccentBright;
        focusImage.raycastTarget = false;
        RectTransform focusRect = focusIndicator.GetComponent<RectTransform>();
        focusRect.anchorMin = new Vector2(0f, 0f);
        focusRect.anchorMax = new Vector2(1f, 0f);
        focusRect.pivot = new Vector2(0.5f, 0f);
        focusRect.anchoredPosition = Vector2.zero;
        focusRect.sizeDelta = new Vector2(0f, 3f);
        focusIndicator.SetActive(false);
        button.transition = Selectable.Transition.None;
        buttonObject.AddComponent<OverlaySelectableVisual>().Initialize(button, image, text, focusIndicator, outline, image.color);
        SetLayout(buttonObject, preferredWidth: width > 0f ? width : -1f, preferredHeight: height);
        return button;
    }

    private Text CreateText(
        Transform parent,
        string value,
        int size,
        Color color,
        TextAnchor alignment,
        FontStyle style = FontStyle.Normal,
        bool localize = true)
    {
        GameObject textObject = CreateObject("Text", parent, typeof(Text));
        Text text = textObject.GetComponent<Text>();
        text.font = _font;
        text.text = localize ? PluginLocalization.Translate(value) : value;
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

    private static float CalculateSafeScale(float requestedScale)
    {
        const float panelWidth = 1120f;
        const float panelHeight = 790f;
        const float screenMargin = 32f;
        float widthScale = (Screen.width - screenMargin) / panelWidth;
        float heightScale = (Screen.height - screenMargin) / panelHeight;
        float safeScale = Mathf.Min(widthScale, heightScale);
        return Mathf.Clamp(Mathf.Min(requestedScale, safeScale), 0.75f, 1.5f);
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

    private void QueueForDestroy(GameObject target)
    {
        if (target == null)
            return;

        target.SetActive(false);
        if (!_deferredDestroy.Contains(target))
            _deferredDestroy.Add(target);
    }

    private void FlushDeferredDestroy()
    {
        if (_deferredDestroy.Count == 0)
            return;

        foreach (GameObject target in _deferredDestroy)
        {
            if (target != null)
                UnityEngine.Object.Destroy(target);
        }

        _deferredDestroy.Clear();
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

    private void ResetStatus()
    {
        if (_statusText != null)
        {
            _statusText.text = PluginLocalization.Translate("准备就绪。");
            _statusText.color = CheatMenuStyle.Muted;
        }
        if (_statusAccent != null)
            _statusAccent.color = CheatMenuStyle.Accent;
    }

    private void SetStatus(string message, float seconds = 3f, StatusKind? kind = null)
    {
        StatusKind resolvedKind = kind ?? InferStatusKind(message);
        if (_statusText != null)
        {
            string prefix = PluginLocalization.Translate(GetStatusPrefix(resolvedKind));
            string separator = PluginLocalization.CurrentLanguage == PluginLanguage.English ? ": " : "：";
            _statusText.text = prefix + separator + PluginLocalization.Translate(message);
            _statusText.color = resolvedKind == StatusKind.Warning ? CheatMenuStyle.Warning : CheatMenuStyle.Text;
        }
        if (_statusAccent != null)
            _statusAccent.color = GetStatusColor(resolvedKind);
        _statusUntil = Time.unscaledTime + seconds;
    }

    private static StatusKind InferStatusKind(string message)
    {
        if (message.IndexOf("失败", StringComparison.Ordinal) >= 0
            || message.StartsWith("无法", StringComparison.Ordinal))
            return StatusKind.Error;
        if (message.IndexOf("没有可用", StringComparison.Ordinal) >= 0
            || message.IndexOf("没有默认", StringComparison.Ordinal) >= 0)
            return StatusKind.Warning;
        return StatusKind.Success;
    }

    private static string GetStatusPrefix(StatusKind kind)
    {
        return kind switch
        {
            StatusKind.Success => "成功",
            StatusKind.Warning => "提示",
            StatusKind.Error => "失败",
            _ => "信息"
        };
    }

    private static Color GetStatusColor(StatusKind kind)
    {
        return kind switch
        {
            StatusKind.Success => CheatMenuStyle.Success,
            StatusKind.Warning => CheatMenuStyle.Warning,
            StatusKind.Error => CheatMenuStyle.Danger,
            _ => CheatMenuStyle.Accent
        };
    }

    private static int ParseClamped(string text, int min, int max, int fallback)
    {
        return int.TryParse(text, out int value) ? Mathf.Clamp(value, min, max) : Mathf.Clamp(fallback, min, max);
    }

    private Sprite GetSliderGripSprite()
    {
        if (_sliderGripSprite != null)
            return _sliderGripSprite;

        _sliderGripTexture = CreateSliderGripTexture(32);
        _sliderGripSprite = Sprite.Create(
            _sliderGripTexture,
            new Rect(0f, 0f, _sliderGripTexture.width, _sliderGripTexture.height),
            new Vector2(0.5f, 0.5f),
            32f);
        return _sliderGripSprite;
    }

    private Sprite GetCloseGlyphSprite()
    {
        if (_closeGlyphSprite != null)
            return _closeGlyphSprite;

        _closeGlyphTexture = CreateCloseGlyphTexture(32);
        _closeGlyphSprite = Sprite.Create(
            _closeGlyphTexture,
            new Rect(0f, 0f, _closeGlyphTexture.width, _closeGlyphTexture.height),
            new Vector2(0.5f, 0.5f),
            32f);
        return _closeGlyphSprite;
    }

    private Sprite GetFavoriteFilledSprite()
    {
        if (_favoriteFilledSprite != null)
            return _favoriteFilledSprite;

        _favoriteFilledTexture = CreateStarTexture(32, outlineOnly: false);
        _favoriteFilledSprite = Sprite.Create(
            _favoriteFilledTexture,
            new Rect(0f, 0f, _favoriteFilledTexture.width, _favoriteFilledTexture.height),
            new Vector2(0.5f, 0.5f),
            32f);
        return _favoriteFilledSprite;
    }

    private Sprite GetFavoriteOutlineSprite()
    {
        if (_favoriteOutlineSprite != null)
            return _favoriteOutlineSprite;

        _favoriteOutlineTexture = CreateStarTexture(32, outlineOnly: true);
        _favoriteOutlineSprite = Sprite.Create(
            _favoriteOutlineTexture,
            new Rect(0f, 0f, _favoriteOutlineTexture.width, _favoriteOutlineTexture.height),
            new Vector2(0.5f, 0.5f),
            32f);
        return _favoriteOutlineSprite;
    }

    private static Texture2D CreateSliderGripTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color stripe = new(0.03f, 0.18f, 0.28f, 1f);
        Color fill = CheatMenuStyle.AccentBright;
        Color border = CheatMenuStyle.SliderBorder;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                bool diagonalStripe = ((x + y) % 9) < 2;
                texture.SetPixel(x, y, edge ? border : diagonalStripe ? stripe : fill);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateCloseGlyphTexture(int size)
    {
        Texture2D texture = CreateTransparentTexture(size);
        float center = (size - 1) * 0.5f;
        Color glyph = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                bool firstStroke = Mathf.Abs(dx - dy) <= 1.8f;
                bool secondStroke = Mathf.Abs(dx + dy) <= 1.8f;
                if (firstStroke || secondStroke)
                    texture.SetPixel(x, y, glyph);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateStarTexture(int size, bool outlineOnly)
    {
        Texture2D texture = CreateTransparentTexture(size);
        Vector2[] vertices = new Vector2[10];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.43f;
        float innerRadius = size * 0.19f;

        for (int index = 0; index < vertices.Length; index++)
        {
            float radius = index % 2 == 0 ? outerRadius : innerRadius;
            float angle = Mathf.PI * 0.5f + index * Mathf.PI / 5f;
            vertices[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        Color glyph = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                bool draw = outlineOnly
                    ? IsNearPolygonEdge(point, vertices, 1.45f)
                    : IsInsidePolygon(point, vertices);
                if (draw)
                    texture.SetPixel(x, y, glyph);
            }
        }

        texture.Apply();
        return texture;
    }

    private static Texture2D CreateTransparentTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[size * size];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = clear;
        texture.SetPixels(pixels);
        return texture;
    }

    private static bool IsInsidePolygon(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        for (int index = 0, previous = vertices.Length - 1; index < vertices.Length; previous = index++)
        {
            Vector2 current = vertices[index];
            Vector2 prior = vertices[previous];
            bool crosses = current.y > point.y != prior.y > point.y;
            if (crosses && point.x < (prior.x - current.x) * (point.y - current.y) / (prior.y - current.y) + current.x)
                inside = !inside;
        }

        return inside;
    }

    private static bool IsNearPolygonEdge(Vector2 point, Vector2[] vertices, float thickness)
    {
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector2 start = vertices[index];
            Vector2 end = vertices[(index + 1) % vertices.Length];
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float projection = lengthSquared <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 closest = start + segment * projection;
            if (Vector2.Distance(point, closest) <= thickness)
                return true;
        }

        return false;
    }

    private static void DestroyGeneratedSprite(ref Sprite sprite, ref Texture2D texture)
    {
        if (sprite != null)
            UnityEngine.Object.Destroy(sprite);
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
        sprite = null;
        texture = null;
    }
}

internal sealed class OverlayIconBinder : MonoBehaviour
{
    private static readonly List<OverlayIconBinder> Pending = new();

    private RawImage _target;
    private Text _previewState;
    private Func<Texture2D> _loader;
    private bool _isVehicle;
    private float _nextAttempt;
    private int _attemptCount;

    public void Initialize(RawImage target, Text previewState, Func<Texture2D> loader, bool isVehicle)
    {
        _target = target;
        _previewState = previewState;
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
        _attemptCount++;
        _nextAttempt = Time.unscaledTime + (_attemptCount >= 12 ? 1f : 0.25f);
        Texture2D texture = _loader?.Invoke();
        if (texture == null)
        {
            if (_previewState != null && _attemptCount >= 12)
                _previewState.text = PluginLocalization.Translate("无预览");
            return;
        }
        _target.texture = texture;
        if (_previewState != null)
            _previewState.gameObject.SetActive(false);
        if (_isVehicle)
            CheatMenuPlugin.Instance?.LogVehicleIconBound(texture);
        _loader = null;
        enabled = false;
    }

    private void OnDestroy()
    {
        Pending.Remove(this);
        _target = null;
        _previewState = null;
        _loader = null;
        _isVehicle = false;
    }
}

internal sealed class OverlaySelectableVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private Selectable _selectable;
    private Image _background;
    private Text _label;
    private GameObject _focusIndicator;
    private Outline _outline;
    private Color _baseColor;
    private bool _hovered;
    private bool _pressed;
    private bool _selected;
    private bool _showOutline = true;
    private bool _showFocusIndicator = true;
    private bool? _lastInteractable;

    public void Initialize(
        Selectable selectable,
        Image background,
        Text label,
        GameObject focusIndicator,
        Outline outline,
        Color baseColor)
    {
        _selectable = selectable;
        _background = background;
        _label = label;
        _focusIndicator = focusIndicator;
        _outline = outline;
        _baseColor = baseColor;
        _lastInteractable = null;
        Refresh();
    }

    public void SetBaseColor(Color baseColor)
    {
        _baseColor = baseColor;
        Refresh();
    }

    public void SetOutlineVisible(bool visible)
    {
        _showOutline = visible;
        Refresh();
    }

    public void SetFocusIndicatorVisible(bool visible)
    {
        _showFocusIndicator = visible;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        Refresh();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        Refresh();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        _selected = false;
        if (_focusIndicator != null)
            _focusIndicator.SetActive(false);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (_selectable != null && _lastInteractable != _selectable.interactable)
            Refresh();
    }

    private void Refresh()
    {
        if (_selectable == null || _background == null)
            return;

        _lastInteractable = _selectable.interactable;
        if (!_selectable.interactable)
        {
            _background.color = CheatMenuStyle.DisabledSurface;
            if (_label != null)
                _label.color = CheatMenuStyle.DisabledText;
            if (_focusIndicator != null)
                _focusIndicator.SetActive(false);
            if (_outline != null)
                _outline.effectColor = _showOutline ? CheatMenuStyle.ButtonBorderDisabled : Color.clear;
            return;
        }

        if (_label != null)
            _label.color = CheatMenuStyle.Text;
        if (_focusIndicator != null)
        {
            bool showFocusIndicator = _showFocusIndicator && _selected;
            _focusIndicator.SetActive(showFocusIndicator);
            if (showFocusIndicator)
                _focusIndicator.transform.SetAsLastSibling();
        }

        if (_pressed)
        {
            _background.color = Color.Lerp(_baseColor, Color.black, 0.20f);
            if (_outline != null)
                _outline.effectColor = _showOutline ? CheatMenuStyle.ButtonBorderPressed : Color.clear;
        }
        else if (_hovered || _selected)
        {
            _background.color = Color.Lerp(_baseColor, Color.white, 0.14f);
            if (_outline != null)
                _outline.effectColor = _showOutline ? CheatMenuStyle.ButtonBorderHover : Color.clear;
        }
        else
        {
            _background.color = _baseColor;
            if (_outline != null)
                _outline.effectColor = _showOutline ? CheatMenuStyle.ButtonBorder : Color.clear;
        }
    }
}

internal sealed class OverlayFocusIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    private GameObject _indicator;

    public void Initialize(GameObject indicator)
    {
        _indicator = indicator;
        if (_indicator != null)
            _indicator.SetActive(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_indicator != null)
            _indicator.SetActive(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (_indicator != null)
            _indicator.SetActive(false);
    }
}
