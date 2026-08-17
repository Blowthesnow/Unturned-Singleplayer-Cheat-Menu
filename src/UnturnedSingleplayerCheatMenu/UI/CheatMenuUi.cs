using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class CheatMenuUi : IDisposable
{
    private enum MenuTab
    {
        Character,
        Items,
        Vehicles,
        Teleports,
        Other
    }

    private static readonly string[] VehicleCategories =
    {
        "全部", "陆地车辆", "固定翼飞机", "直升机", "飞艇", "船只", "火车", "其他"
    };

    private readonly CheatMenuPlugin _plugin;
    private readonly List<ItemAsset> _itemResults = new();
    private readonly List<VehicleAsset> _vehicleResults = new();
    private readonly List<Texture2D> _ownedTextures = new();
    private readonly ItemFilterState _itemFilter = new();

    private Rect _windowRect;
    private MenuTab _activeTab;
    private Vector2 _characterScroll;
    private Vector2 _itemCategoryScroll;
    private Vector2 _vehicleCategoryScroll;
    private Vector2 _teleportScroll;
    private Vector2 _otherScroll;
    private Vector2 _itemGridScroll;
    private Vector2 _vehicleGridScroll;
    private string _itemQuery = string.Empty;
    private string _vehicleQuery = string.Empty;
    private string _vehicleCategory = "全部";
    private string _teleportName = string.Empty;
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
    private int _itemPage;
    private int _vehiclePage;
    private byte _giveAmount = 1;
    private int _spawnVehicleAmount = 1;
    private string _status = "准备就绪。";
    private float _statusUntil;
    private bool _stylesReady;

    private GUIStyle _windowStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _subheaderStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _tabStyle;
    private GUIStyle _tabActiveStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _dangerButtonStyle;
    private GUIStyle _toggleStyle;
    private GUIStyle _toggleActiveStyle;
    private GUIStyle _cardStyle;
    private GUIStyle _cardNameStyle;
    private GUIStyle _cardMetaStyle;
    private GUIStyle _searchStyle;
    private GUIStyle _statusStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _transparentButton;

    public CheatMenuUi(CheatMenuPlugin plugin)
    {
        _plugin = plugin;
    }

    public void OnOpened()
    {
        EnsureWindowPosition();
        BuildItemResults();
        BuildVehicleResults();
        SetStatus($"自动扫描完成：{_plugin.Catalog.Items.Count} 个物品、{_plugin.Catalog.Vehicles.Count} 辆载具。", 5f);
    }

    public void OnCatalogRefreshed()
    {
        BuildItemResults();
        BuildVehicleResults();
    }

    public void Draw()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Color previousBackgroundColor = GUI.backgroundColor;
        Color previousContentColor = GUI.contentColor;
        int previousDepth = GUI.depth;
        bool previousEnabled = GUI.enabled;

        try
        {
            // Unturned's OnGUI handlers leave shared IMGUI state configured for
            // their own widgets. Because this menu is drawn from a Harmony
            // postfix, explicitly establish a clean top-most state first.
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.depth = -10000;
            GUI.enabled = true;

            EnsureStyles();
            EnsureWindowPosition();

            float scale = _plugin.UiScale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            _windowRect = GUI.Window(0x554E5455, _windowRect, DrawWindow, string.Empty, _windowStyle);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;
            GUI.depth = previousDepth;
            GUI.enabled = previousEnabled;
        }
    }

    public void Dispose()
    {
        foreach (Texture2D texture in _ownedTextures)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
        _ownedTextures.Clear();
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.BeginVertical();
        DrawHeader();
        DrawTabs();
        GUILayout.Space(8f);

        switch (_activeTab)
        {
            case MenuTab.Character:
                DrawCharacterTab();
                break;
            case MenuTab.Items:
                DrawItemsTab();
                break;
            case MenuTab.Vehicles:
                DrawVehiclesTab();
                break;
            case MenuTab.Teleports:
                DrawTeleportsTab();
                break;
            case MenuTab.Other:
                DrawOtherTab();
                break;
        }

        GUILayout.FlexibleSpace();
        if (_statusUntil > 0f && Time.unscaledTime > _statusUntil)
            _status = "准备就绪。";
        GUILayout.Label(_status, _statusStyle, GUILayout.Height(28f));
        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 65f, 48f));
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal(GUILayout.Height(46f));
        GUILayout.Label("单人作弊指令菜单", _headerStyle, GUILayout.ExpandWidth(false));
        GUILayout.Space(12f);
        GUILayout.Label("仅限 SINGLEPLAYER", _badgeStyle, GUILayout.Width(150f), GUILayout.Height(25f));
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{Provider.map}  ·  {_plugin.ShortcutLabel}", _mutedStyle, GUILayout.ExpandWidth(false));
        GUILayout.Space(12f);
        if (GUILayout.Button("×", _dangerButtonStyle, GUILayout.Width(42f), GUILayout.Height(34f)))
            _plugin.CloseMenu();
        GUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal(GUILayout.Height(42f));
        DrawTab(MenuTab.Character, "角色");
        DrawTab(MenuTab.Items, "物品");
        DrawTab(MenuTab.Vehicles, "车辆");
        DrawTab(MenuTab.Teleports, "传送");
        DrawTab(MenuTab.Other, "其他");
        GUILayout.EndHorizontal();
    }

    private void DrawTab(MenuTab tab, string label)
    {
        GUIStyle style = _activeTab == tab ? _tabActiveStyle : _tabStyle;
        if (GUILayout.Button(label, style, GUILayout.Height(38f)))
            _activeTab = tab;
    }

    private void DrawCharacterTab()
    {
        Player player = Player.LocalPlayer;
        PlayerLife life = player?.life;
        PlayerSkills skills = player?.skills;
        if (life == null || skills == null)
        {
            GUILayout.Label("玩家状态尚未准备完成。", _bodyStyle);
            return;
        }

        _characterScroll = GUILayout.BeginScrollView(_characterScroll);
        GUILayout.Label("生存状态", _subheaderStyle);
        GUILayout.Label(
            $"生命 {life.health}/100    饱食 {life.food}/100    水分 {life.water}/100    免疫 {life.virus}/100    体力 {life.stamina}/100    氧气 {life.oxygen}/100",
            _bodyStyle);
        GUILayout.Space(8f);

        GUILayout.BeginHorizontal();
        bool god = ToggleButton("无敌模式", _plugin.Actions.GodModeEnabled, "拦截本地玩家受到的伤害，并持续补满状态。");
        if (god != _plugin.Actions.GodModeEnabled)
        {
            _plugin.Actions.SetGodMode(god);
            SetStatus(god ? "无敌模式已开启。" : "无敌模式已关闭。");
        }

        bool needs = ToggleButton("无限生存状态", _plugin.Actions.InfiniteNeedsEnabled, "持续补满饱食、水分、免疫、体力和氧气。");
        if (needs != _plugin.Actions.InfiniteNeedsEnabled)
        {
            _plugin.Actions.SetInfiniteNeeds(needs);
            SetStatus(needs ? "无限生存状态已开启。" : "无限生存状态已关闭。");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.Label("设置指定数值（0–100；生命最低 1）", _mutedStyle);
        GUILayout.BeginHorizontal();
        DrawNumberInput("生命", ref _healthTarget, 58f);
        DrawNumberInput("饱食", ref _foodTarget, 58f);
        DrawNumberInput("水分", ref _waterTarget, 58f);
        DrawNumberInput("免疫", ref _virusTarget, 58f);
        DrawNumberInput("体力", ref _staminaTarget, 58f);
        DrawNumberInput("氧气", ref _oxygenTarget, 58f);
        if (GUILayout.Button("应用", _buttonStyle, GUILayout.Width(86f), GUILayout.Height(32f)))
        {
            bool success = _plugin.Actions.SetLifeStats(
                ParseClamped(_healthTarget, 1, 100, 100),
                ParseClamped(_foodTarget, 0, 100, 100),
                ParseClamped(_waterTarget, 0, 100, 100),
                ParseClamped(_virusTarget, 0, 100, 100),
                ParseClamped(_staminaTarget, 0, 100, 100),
                ParseClamped(_oxygenTarget, 0, 100, 100));
            SetStatus(success ? "角色生存数值已设置。" : "设置失败。");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (ActionButton("全部恢复", "补满生命和全部生存状态，治疗流血与骨折。"))
            SetStatus(_plugin.Actions.RestoreEverything() ? "角色状态已全部恢复。" : "恢复失败。");
        if (ActionButton("补满生存状态", "补满饱食、水分、免疫、体力和氧气。"))
            SetStatus(_plugin.Actions.RefillNeeds() ? "生存状态已补满。" : "操作失败。");
        if (ActionButton("治疗流血/骨折", "清除流血与骨折。"))
            SetStatus(_plugin.Actions.CureInjuries() ? "伤势已治疗。" : "操作失败。");
        GUILayout.EndHorizontal();

        GUILayout.Space(18f);
        GUILayout.Label("经验、声望与技能", _subheaderStyle);
        GUILayout.Label($"经验 {skills.experience:N0}    声望 {skills.reputation:N0}", _bodyStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("经验数量", _bodyStyle, GUILayout.Width(70f));
        _experienceAmount = GUILayout.TextField(_experienceAmount, _searchStyle, GUILayout.Width(105f), GUILayout.Height(32f));
        if (GUILayout.Button("增加经验", _buttonStyle, GUILayout.Width(110f), GUILayout.Height(32f)))
        {
            uint amount = (uint)ParseClamped(_experienceAmount, 1, 100000000, 1000);
            _plugin.Actions.AddExperience(amount);
            SetStatus($"已增加 {amount:N0} 经验。");
        }
        GUILayout.Space(16f);
        GUILayout.Label("声望数量", _bodyStyle, GUILayout.Width(70f));
        _reputationAmount = GUILayout.TextField(_reputationAmount, _searchStyle, GUILayout.Width(105f), GUILayout.Height(32f));
        if (GUILayout.Button("增加声望", _buttonStyle, GUILayout.Width(110f), GUILayout.Height(32f)))
        {
            int amount = ParseClamped(_reputationAmount, -1000000, 1000000, 100);
            _plugin.Actions.AddReputation(amount);
            SetStatus($"已变更 {amount:N0} 声望。");
        }
        GUILayout.FlexibleSpace();
        if (ActionButton("全部技能满级"))
        {
            int changed = _plugin.Actions.MaxAllSkills();
            SetStatus($"技能已处理，实际变更 {changed} 项。");
        }
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
    }

    private void DrawItemsTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索", _bodyStyle, GUILayout.Width(46f));
        string query = GUILayout.TextField(_itemQuery, _searchStyle, GUILayout.Height(32f));
        if (!string.Equals(query, _itemQuery, StringComparison.Ordinal))
        {
            _itemQuery = query;
            BuildItemResults();
        }
        GUILayout.Space(10f);
        GUILayout.Label("数量", _bodyStyle, GUILayout.Width(46f));
        if (GUILayout.Button("−", _buttonStyle, GUILayout.Width(34f), GUILayout.Height(32f)))
        {
            _giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
            _itemAmountText = _giveAmount.ToString();
        }
        _itemAmountText = GUILayout.TextField(_itemAmountText, _searchStyle, GUILayout.Width(55f), GUILayout.Height(32f));
        if (GUILayout.Button("+", _buttonStyle, GUILayout.Width(34f), GUILayout.Height(32f)))
        {
            _giveAmount = (byte)Math.Min(byte.MaxValue, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
            _itemAmountText = _giveAmount.ToString();
        }
        GUILayout.Space(8f);
        if (GUILayout.Button("重新扫描", _buttonStyle, GUILayout.Width(105f), GUILayout.Height(32f)))
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label($"显示 {_itemResults.Count} / {_plugin.Catalog.Items.Count}；已识别模组物品 {_plugin.Catalog.WorkshopItemCount}。搜索支持名称、ID、GUID 和来源。", _mutedStyle);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        DrawItemCategorySidebar();
        GUILayout.Space(10f);
        GUILayout.BeginVertical();
        DrawItemGrid();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawVehiclesTab()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索", _bodyStyle, GUILayout.Width(46f));
        string query = GUILayout.TextField(_vehicleQuery, _searchStyle, GUILayout.Height(32f));
        if (!string.Equals(query, _vehicleQuery, StringComparison.Ordinal))
        {
            _vehicleQuery = query;
            BuildVehicleResults();
        }
        GUILayout.Space(10f);
        GUILayout.Label("数量", _bodyStyle, GUILayout.Width(46f));
        if (GUILayout.Button("−", _buttonStyle, GUILayout.Width(34f), GUILayout.Height(32f)))
        {
            _spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
        }
        _vehicleAmountText = GUILayout.TextField(_vehicleAmountText, _searchStyle, GUILayout.Width(55f), GUILayout.Height(32f));
        if (GUILayout.Button("+", _buttonStyle, GUILayout.Width(34f), GUILayout.Height(32f)))
        {
            _spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
        }
        GUILayout.Space(8f);
        if (GUILayout.Button("重新扫描", _buttonStyle, GUILayout.Width(105f), GUILayout.Height(32f)))
        {
            _plugin.RefreshCatalog();
            SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label($"显示 {_vehicleResults.Count} / {_plugin.Catalog.Vehicles.Count}；已识别模组车辆 {_plugin.Catalog.WorkshopVehicleCount}。每次可生成 1–20 辆，自动排列在玩家前方。", _mutedStyle);
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        DrawVehicleCategorySidebar();
        GUILayout.Space(10f);
        GUILayout.BeginVertical();
        DrawVehicleGrid();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void DrawItemCategorySidebar()
    {
        GUILayout.BeginVertical(_cardStyle, GUILayout.Width(158f), GUILayout.ExpandHeight(true));
        GUILayout.Label("物品分类", _subheaderStyle);
        _itemCategoryScroll = GUILayout.BeginScrollView(_itemCategoryScroll);
        foreach (ItemPrimaryCategory category in ItemFilterService.Categories)
        {
            GUIStyle style = _itemFilter.Category == category ? _tabActiveStyle : _tabStyle;
            if (GUILayout.Button(ItemFilterService.GetPrimaryCategoryLabel(category), style, GUILayout.Height(34f)))
            {
                _itemFilter.Category = category;
                ItemFilterService.NormalizeForCategory(_itemFilter);
                BuildItemResults();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawVehicleCategorySidebar()
    {
        GUILayout.BeginVertical(_cardStyle, GUILayout.Width(158f), GUILayout.ExpandHeight(true));
        GUILayout.Label("车辆分类", _subheaderStyle);
        _vehicleCategoryScroll = GUILayout.BeginScrollView(_vehicleCategoryScroll);
        foreach (string category in VehicleCategories)
        {
            GUIStyle style = _vehicleCategory == category ? _tabActiveStyle : _tabStyle;
            if (GUILayout.Button(category, style, GUILayout.Height(34f)))
            {
                _vehicleCategory = category;
                BuildVehicleResults();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private void DrawItemGrid()
    {
        int pageCount = Math.Max(1, Mathf.CeilToInt(_itemResults.Count / (float)_plugin.PageSize));
        _itemPage = Mathf.Clamp(_itemPage, 0, pageCount - 1);
        DrawPagination(ref _itemPage, pageCount, _itemResults.Count);
        GUILayout.Space(6f);

        int start = _itemPage * _plugin.PageSize;
        int end = Math.Min(start + _plugin.PageSize, _itemResults.Count);
        _itemGridScroll = GUILayout.BeginScrollView(_itemGridScroll);
        DrawCardRows(start, end, index => DrawItemCard(_itemResults[index]));
        GUILayout.EndScrollView();
    }

    private void DrawVehicleGrid()
    {
        int pageCount = Math.Max(1, Mathf.CeilToInt(_vehicleResults.Count / (float)_plugin.PageSize));
        _vehiclePage = Mathf.Clamp(_vehiclePage, 0, pageCount - 1);
        DrawPagination(ref _vehiclePage, pageCount, _vehicleResults.Count);
        GUILayout.Space(6f);

        int start = _vehiclePage * _plugin.PageSize;
        int end = Math.Min(start + _plugin.PageSize, _vehicleResults.Count);
        _vehicleGridScroll = GUILayout.BeginScrollView(_vehicleGridScroll);
        DrawCardRows(start, end, index => DrawVehicleCard(_vehicleResults[index]));
        GUILayout.EndScrollView();
    }

    private void DrawCardRows(int start, int end, Action<int> drawCard)
    {
        const int columns = 5;
        for (int index = start; index < end; index += columns)
        {
            GUILayout.BeginHorizontal();
            for (int column = 0; column < columns; column++)
            {
                int cardIndex = index + column;
                if (cardIndex < end)
                    drawCard(cardIndex);
                else
                    GUILayout.Space(158f);
                if (column < columns - 1)
                    GUILayout.Space(7f);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(7f);
        }
    }

    private void DrawItemCard(ItemAsset asset)
    {
        Rect rect = GUILayoutUtility.GetRect(150f, 158f, GUILayout.Width(150f), GUILayout.Height(158f));
        bool clicked = GUI.Button(rect, GUIContent.none, _transparentButton);
        GUI.Box(rect, GUIContent.none, _cardStyle);
        Texture2D icon = _plugin.Icons.GetItemIcon(asset);
        Rect iconRect = new(rect.x + 27f, rect.y + 7f, 96f, 88f);
        if (icon != null)
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        else
            GUI.Label(iconRect, "生成图标…", _mutedStyle);
        GUI.Label(new Rect(rect.x + 7f, rect.y + 98f, rect.width - 14f, 36f), asset.FriendlyName, _cardNameStyle);
        GUI.Label(new Rect(rect.x + 7f, rect.y + 135f, rect.width - 14f, 18f), $"ID {asset.id}  ·  {AssetCatalog.GetOriginLabel(asset)}", _cardMetaStyle);

        if (clicked)
        {
            _giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
            _itemAmountText = _giveAmount.ToString();
            bool success = _plugin.Actions.GiveItem(asset, _giveAmount);
            SetStatus(success ? $"已给予 {_giveAmount} × {asset.FriendlyName}。" : $"无法给予 {asset.FriendlyName}。", 4f);
        }
    }

    private void DrawVehicleCard(VehicleAsset asset)
    {
        Rect rect = GUILayoutUtility.GetRect(150f, 158f, GUILayout.Width(150f), GUILayout.Height(158f));
        bool clicked = GUI.Button(rect, GUIContent.none, _transparentButton);
        GUI.Box(rect, GUIContent.none, _cardStyle);
        Texture2D icon = _plugin.Icons.GetVehicleIcon(asset);
        Rect iconRect = new(rect.x + 11f, rect.y + 7f, 128f, 88f);
        if (icon != null)
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        else
            GUI.Label(iconRect, "生成图标…", _mutedStyle);
        GUI.Label(new Rect(rect.x + 7f, rect.y + 98f, rect.width - 14f, 36f), asset.FriendlyName, _cardNameStyle);
        string id = asset.id == 0 ? "GUID" : $"ID {asset.id}";
        GUI.Label(new Rect(rect.x + 7f, rect.y + 135f, rect.width - 14f, 18f), $"{id}  ·  {AssetCatalog.GetOriginLabel(asset)}", _cardMetaStyle);

        if (clicked)
        {
            _spawnVehicleAmount = ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount);
            _vehicleAmountText = _spawnVehicleAmount.ToString();
            int spawned = _plugin.Actions.SpawnVehicles(asset, _spawnVehicleAmount);
            SetStatus(spawned > 0
                ? $"已在玩家前方生成 {spawned} × {asset.FriendlyName}。"
                : $"无法生成 {asset.FriendlyName}。", 4f);
        }
    }

    private void DrawTeleportsTab()
    {
        Player player = Player.LocalPlayer;
        if (player == null)
            return;

        Vector3 position = player.transform.position;
        GUILayout.Label("保存当前位置", _subheaderStyle);
        GUILayout.Label($"地图 {Provider.map}    X {position.x:F1}    Y {position.y:F1}    Z {position.z:F1}", _mutedStyle);
        GUILayout.BeginHorizontal();
        _teleportName = GUILayout.TextField(_teleportName, _searchStyle, GUILayout.Height(34f));
        if (GUILayout.Button("保存位置", _buttonStyle, GUILayout.Width(120f), GUILayout.Height(34f)))
        {
            TeleportPoint point = _plugin.Teleports.AddCurrent(_teleportName);
            if (point != null)
            {
                _teleportName = string.Empty;
                SetStatus($"已保存传送点：{point.Name}。", 4f);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);
        GUILayout.Label($"已保存位置（{_plugin.Teleports.Points.Count}）", _subheaderStyle);

        _teleportScroll = GUILayout.BeginScrollView(_teleportScroll);
        foreach (TeleportPoint point in _plugin.Teleports.Points.ToArray())
        {
            bool sameMap = string.Equals(point.Map, Provider.map, StringComparison.OrdinalIgnoreCase);
            GUILayout.BeginHorizontal(_cardStyle, GUILayout.Height(62f));
            GUILayout.BeginVertical();
            GUILayout.Label(point.Name, _bodyStyle);
            GUILayout.Label($"{point.Map}  ·  ({point.X:F1}, {point.Y:F1}, {point.Z:F1})", sameMap ? _mutedStyle : _cardMetaStyle);
            GUILayout.EndVertical();
            GUI.enabled = sameMap;
            if (GUILayout.Button(sameMap ? "传送" : "其他地图", _buttonStyle, GUILayout.Width(100f), GUILayout.Height(38f)))
            {
                bool success = _plugin.Actions.Teleport(point);
                SetStatus(success ? $"已传送到 {point.Name}。" : "传送失败：目标位置可能被阻挡，或玩家正在载具中。", 5f);
            }
            GUI.enabled = true;
            if (GUILayout.Button("×", _dangerButtonStyle, GUILayout.Width(42f), GUILayout.Height(38f)))
            {
                if (_plugin.Teleports.Remove(point.Id))
                    SetStatus($"已删除传送点：{point.Name}。", 4f);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }
        GUILayout.EndScrollView();
    }

    private void DrawOtherTab()
    {
        _otherScroll = GUILayout.BeginScrollView(_otherScroll);
        GUILayout.Label("时间", _subheaderStyle);
        uint cycle = Math.Max(1u, LightingManager.cycle);
        float normalized = LightingManager.time / (float)cycle;
        GUILayout.Label($"当前时间：{LightingManager.time:N0} / {cycle:N0}（{normalized:P0}）", _bodyStyle);
        float newNormalized = GUILayout.HorizontalSlider(normalized, 0f, 0.999f, GUILayout.Height(24f));
        if (Mathf.Abs(newNormalized - normalized) > 0.001f)
            _plugin.Actions.SetTime((uint)(newNormalized * cycle));

        GUILayout.BeginHorizontal();
        if (ActionButton("设为白天")) { _plugin.Actions.SetDay(); SetStatus("已切换到白天。"); }
        if (ActionButton("设为夜晚")) { _plugin.Actions.SetNight(); SetStatus("已切换到夜晚。"); }
        bool frozen = ToggleButton("冻结时间", _plugin.Actions.FreezeTimeEnabled);
        if (frozen != _plugin.Actions.FreezeTimeEnabled)
        {
            _plugin.Actions.SetFreezeTime(frozen);
            SetStatus(frozen ? "时间已冻结。" : "时间已恢复流动。");
        }
        GUILayout.EndHorizontal();

        bool fullMoon = ToggleButton("强制满月", LightingManager.isFullMoon, "切换当前世界的满月状态。");
        if (fullMoon != LightingManager.isFullMoon)
        {
            _plugin.Actions.SetFullMoon(fullMoon);
            SetStatus(fullMoon ? "已开启满月。" : "已关闭满月。");
        }

        GUILayout.Space(18f);
        GUILayout.Label("世界事件", _subheaderStyle);
        GUILayout.BeginHorizontal();
        if (ActionButton("立即呼叫空投", "使用当前地图配置的随机空投节点与货物表。"))
            SetStatus(_plugin.Actions.CallAirdrop() ? "空投已呼叫。" : "此地图没有可用的空投节点或货物表。", 5f);
        if (ActionButton("下雨"))
            SetStatus(_plugin.Actions.StartRain() ? "已触发雨天。" : "当前地图没有默认雨天资产。");
        if (ActionButton("暴雪"))
            SetStatus(_plugin.Actions.StartSnow() ? "已触发暴雪。" : "当前地图没有默认雪天资产。");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (ActionButton("清除当前天气")) { _plugin.Actions.ClearWeather(); SetStatus("当前天气已清除，后续仍可自然调度。"); }
        if (ActionButton("关闭天气调度")) { _plugin.Actions.DisableWeather(); SetStatus("当前天气和自然天气调度已关闭。"); }
        if (ActionButton("重新扫描模组资产")) { _plugin.RefreshCatalog(); SetStatus("资产目录已重新扫描。", 4f); }
        GUILayout.EndHorizontal();

        GUILayout.Space(18f);
        GUILayout.Label("扫描信息", _subheaderStyle);
        GUILayout.Label(
            $"物品：{_plugin.Catalog.Items.Count}（模组 {_plugin.Catalog.WorkshopItemCount}）    车辆：{_plugin.Catalog.Vehicles.Count}（模组 {_plugin.Catalog.WorkshopVehicleCount}）",
            _bodyStyle);
        GUILayout.Label("目录来源是 Unturned 当前资产映射：只有已被游戏成功加载的 Workshop、地图随附和原版资产才会显示。", _mutedStyle);
        GUILayout.EndScrollView();
    }

    private bool ToggleButton(string label, bool value, string tooltip = null)
    {
        GUIStyle style = value ? _toggleActiveStyle : _toggleStyle;
        string text = value ? $"✓ {label}" : label;
        return GUILayout.Toggle(value, new GUIContent(text, tooltip), style, GUILayout.Height(42f));
    }

    private bool ActionButton(string label, string tooltip = null)
    {
        return GUILayout.Button(new GUIContent(label, tooltip), _buttonStyle, GUILayout.Height(42f));
    }

    private void DrawNumberInput(string label, ref string value, float width)
    {
        GUILayout.Label(label, _mutedStyle, GUILayout.Width(34f));
        value = GUILayout.TextField(value, _searchStyle, GUILayout.Width(width), GUILayout.Height(32f));
    }

    private static int ParseClamped(string text, int min, int max, int fallback)
    {
        return int.TryParse(text, out int value) ? Mathf.Clamp(value, min, max) : Mathf.Clamp(fallback, min, max);
    }

    private void DrawPagination(ref int page, int pageCount, int resultCount)
    {
        GUILayout.BeginHorizontal();
        GUI.enabled = page > 0;
        if (GUILayout.Button("‹ 上一页", _buttonStyle, GUILayout.Width(96f), GUILayout.Height(30f))) page--;
        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        GUILayout.Label(resultCount == 0 ? "无结果" : $"第 {page + 1} / {pageCount} 页", _mutedStyle);
        GUILayout.FlexibleSpace();
        GUI.enabled = page < pageCount - 1;
        if (GUILayout.Button("下一页 ›", _buttonStyle, GUILayout.Width(96f), GUILayout.Height(30f))) page++;
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void BuildItemResults()
    {
        _itemFilter.SearchQuery = _itemQuery;
        _itemResults.Clear();
        _itemResults.AddRange(_plugin.Catalog.Items.Where(asset =>
            ItemFilterService.Matches(asset, _itemFilter)));
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

    private void SetStatus(string message, float seconds = 3f)
    {
        _status = message;
        _statusUntil = Time.unscaledTime + seconds;
    }

    private void EnsureWindowPosition()
    {
        float width = Mathf.Min(1080f, Screen.width / _plugin.UiScale - 36f);
        float height = Mathf.Min(760f, Screen.height / _plugin.UiScale - 36f);
        if (_windowRect.width <= 0f)
        {
            _windowRect = new Rect(
                (Screen.width / _plugin.UiScale - width) * 0.5f,
                (Screen.height / _plugin.UiScale - height) * 0.5f,
                width,
                height);
            return;
        }

        _windowRect.width = width;
        _windowRect.height = height;
        _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Math.Max(0f, Screen.width / _plugin.UiScale - width));
        _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Math.Max(0f, Screen.height / _plugin.UiScale - height));
    }

    private void EnsureStyles()
    {
        if (_stylesReady)
            return;

        Color panel = new(0.055f, 0.067f, 0.085f, 0.98f);
        Color surface = new(0.09f, 0.11f, 0.14f, 0.98f);
        Color surfaceHover = new(0.13f, 0.16f, 0.20f, 1f);
        Color accent = new(0.16f, 0.68f, 0.95f, 1f);
        Color accentDark = new(0.08f, 0.36f, 0.55f, 1f);
        Color danger = new(0.76f, 0.18f, 0.23f, 1f);
        Color text = new(0.94f, 0.96f, 0.98f, 1f);
        Color muted = new(0.62f, 0.68f, 0.74f, 1f);

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = MakeTexture(panel) },
            padding = new RectOffset(18, 18, 14, 14)
        };
        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, normal = { textColor = text }, alignment = TextAnchor.MiddleLeft };
        _subheaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = text } };
        _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = text }, wordWrap = true, alignment = TextAnchor.MiddleLeft };
        _mutedStyle = new GUIStyle(_bodyStyle) { fontSize = 12, normal = { textColor = muted } };

        _buttonStyle = CreateButtonStyle(surface, surfaceHover, accentDark, text);
        _dangerButtonStyle = CreateButtonStyle(danger, new Color(0.9f, 0.24f, 0.3f, 1f), danger, Color.white);
        _tabStyle = CreateButtonStyle(new Color(0f, 0f, 0f, 0f), surfaceHover, accentDark, muted);
        _tabActiveStyle = CreateButtonStyle(accentDark, accentDark, accent, Color.white);
        _toggleStyle = CreateButtonStyle(surface, surfaceHover, accentDark, text);
        _toggleActiveStyle = CreateButtonStyle(accentDark, accentDark, accent, Color.white);
        _cardStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTexture(surface), textColor = text },
            hover = { background = MakeTexture(surfaceHover), textColor = text },
            padding = new RectOffset(8, 8, 8, 8)
        };
        _cardNameStyle = new GUIStyle(_bodyStyle) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        _cardMetaStyle = new GUIStyle(_mutedStyle) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
        _searchStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 14,
            normal = { background = MakeTexture(surface), textColor = text },
            focused = { background = MakeTexture(surfaceHover), textColor = text },
            padding = new RectOffset(10, 10, 7, 7)
        };
        _statusStyle = new GUIStyle(_mutedStyle)
        {
            normal = { background = MakeTexture(new Color(0.035f, 0.043f, 0.055f, 1f)), textColor = muted },
            padding = new RectOffset(10, 10, 5, 5),
            alignment = TextAnchor.MiddleLeft
        };
        _badgeStyle = new GUIStyle(_bodyStyle)
        {
            normal = { background = MakeTexture(new Color(0.08f, 0.32f, 0.22f, 1f)), textColor = new Color(0.53f, 1f, 0.72f, 1f) },
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };
        _transparentButton = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTexture(Color.clear) },
            hover = { background = MakeTexture(new Color(1f, 1f, 1f, 0.03f)) },
            active = { background = MakeTexture(new Color(0.16f, 0.68f, 0.95f, 0.16f)) }
        };
        _stylesReady = true;
    }

    private GUIStyle CreateButtonStyle(Color normal, Color hover, Color active, Color textColor)
    {
        return new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = MakeTexture(normal), textColor = textColor },
            hover = { background = MakeTexture(hover), textColor = Color.white },
            active = { background = MakeTexture(active), textColor = Color.white },
            padding = new RectOffset(10, 10, 7, 7)
        };
    }

    private Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        _ownedTextures.Add(texture);
        return texture;
    }
}
