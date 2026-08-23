using System;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
    private enum ToolsTab
    {
        Interaction,
        Inventory,
        Movement,
        Translation
    }

    private ToolsTab _activeToolsTab = ToolsTab.Interaction;
    private static readonly PointToolMode[] PointToolModes =
    {
        PointToolMode.Smart,
        PointToolMode.Inspect,
        PointToolMode.Repair,
        PointToolMode.Teleport,
        PointToolMode.Utility,
        PointToolMode.Delete
    };
    private bool _pointToolModeDrawerOpen;
    private GameObject _pointToolModeDrawerRoot;
    private RectTransform _pointToolModeDrawerPopup;
    private Button _pointToolModeTriggerButton;
    private bool _inventoryPresetDrawerOpen;
    private GameObject _inventoryPresetDrawerRoot;
    private RectTransform _inventoryPresetDrawerPopup;
    private Button _inventoryPresetTriggerButton;    private bool _workshopProtocolDrawerOpen;    private GameObject _workshopProtocolDrawerRoot;    private RectTransform _workshopProtocolDrawerPopup;    private Button _workshopProtocolTriggerButton;

    private Text _workshopLocalizationStateText;
    private Text _workshopLocalizationSummaryText;
    private Text _workshopLocalizationApiText;

    private void BuildToolsTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
        GameObject tabs = CreateRow(_contentHost.transform, 38f, 7f);
        AddToolsTab(tabs.transform, ToolsTab.Interaction, "交互");
        AddToolsTab(tabs.transform, ToolsTab.Inventory, "背包整理");
        AddToolsTab(tabs.transform, ToolsTab.Movement, "移动");
        AddToolsTab(tabs.transform, ToolsTab.Translation, "翻译");

        GameObject panel = CreateObject("ToolsPanel", _contentHost.transform, typeof(Image));
        panel.GetComponent<Image>().color = CheatMenuStyle.Surface;
        AddVerticalLayout(panel, 10f, new RectOffset(18, 18, 16, 16));
        SetLayout(panel, flexibleWidth: 1f, flexibleHeight: 1f);
        CreateLabel(panel.transform, _activeToolsTab == ToolsTab.Interaction ? "准星交互工具" : _activeToolsTab == ToolsTab.Inventory ? "背包整理" : _activeToolsTab == ToolsTab.Movement ? "移动工具" : "翻译", 19, CheatMenuStyle.Text);

        if (_activeToolsTab == ToolsTab.Interaction)
            BuildPointToolPanel(panel.transform);
        else if (_activeToolsTab == ToolsTab.Inventory)
            BuildInventoryArrangePanel(panel.transform);
        else if (_activeToolsTab == ToolsTab.Movement)
            BuildMovementPanel(panel.transform);
        else
            BuildWorkshopLocalizationPanel(panel.transform);
    }

    private void AddToolsTab(Transform parent, ToolsTab tab, string label)
    {
        Button button = CreateButton(parent, label, () =>
        {
            _activeToolsTab = tab;
            ReplaceContentHost();
            BuildToolsTab();
        }, 0f, CheatMenuStyle.TabHeight, _activeToolsTab == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 14);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void BuildWorkshopLocalizationPanel(Transform panel)
    {
        WorkshopLocalizationService service = _plugin.WorkshopLocalization;
        if (service == null)
        {
            CreateEmptyState(panel, "翻译服务不可用", "插件尚未完成 Workshop 翻译服务初始化。");
            return;
        }

        WorkshopLocalizationProgress progress = service.Progress;
        CreateLabel(panel, "原版与 Workshop 资产翻译", 17, CheatMenuStyle.Text);
        CreateLabel(panel, "只读取当前已加载的原版与 Workshop 资产；不会修改原始文件、.dat 或 AssetBundle。已有其他汉化时会优先保留原生汉化。", 12, CheatMenuStyle.Muted);
        _workshopLocalizationStateText = CreateLabel(panel, string.Empty, 13, CheatMenuStyle.Text);
        _workshopLocalizationSummaryText = CreateLabel(panel, string.Empty, 13, CheatMenuStyle.Muted);
        _workshopLocalizationApiText = CreateLabel(panel, string.Empty, 12, CheatMenuStyle.Muted);
        RefreshWorkshopLocalizationPageValues();

        GameObject apiRow = CreateRow(panel, 40f, 7f);
        CreateLabel(apiRow.transform, "API 设置", 13, CheatMenuStyle.Text, 75f, TextAnchor.MiddleLeft);
        Button apiButton = null;
        apiButton = CreateButton(
            apiRow.transform,
            MachineTranslationSettings.GetDisplayName(service.Settings.ApiProtocol) + " ▼",
            () => ToggleWorkshopProtocolDrawer(apiButton),
            0f,
            34f,
            CheatMenuStyle.Surface,
            12,
            "选择协议并在下方输入 Endpoint、API Key 和 Model。");
        SetLayout(apiButton.gameObject, flexibleWidth: 1f);
        CreateButton(apiRow.transform, "开始翻译（未缓存）", StartWorkshopTranslationFromUi, 170f, 34f, CheatMenuStyle.Accent, 12, "扫描当前已加载资产后，翻译缓存中没有的文本；填写配置后点击这里开始。" );
        CreateButton(apiRow.transform, "停止翻译", StopWorkshopTranslationFromUi, 105f, 34f, CheatMenuStyle.Danger, 12, "停止当前翻译任务；已经写入缓存的译文会保留。" );

        bool isCloud = service.Settings.IsCloudProvider;
        bool isLargeModel = service.Settings.IsLargeModelProvider;
        bool isOllama = string.Equals(service.Settings.ApiProtocol, MachineTranslationSettings.OllamaProvider, StringComparison.OrdinalIgnoreCase);
        bool showEndpoint = true;
        bool showApiKey = !isOllama || isCloud;
        bool showSecret = service.Settings.RequiresSecretKey;
        bool showRegion = service.Settings.RequiresRegion;
        bool showProject = string.Equals(service.Settings.ApiProtocol, MachineTranslationSettings.TencentTmtProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(service.Settings.ApiProtocol, MachineTranslationSettings.GoogleCloudProvider, StringComparison.OrdinalIgnoreCase);
        bool showModel = isLargeModel;

        if (showEndpoint)
            CreateLabeledInput(panel, "接口地址", service.Settings.Endpoint, value => _plugin.SetWorkshopLocalizationEndpoint(value), 0f, commitOnEndEdit: true, tooltip: "请求发送到哪里。大模型通常是 Chat/Message 地址；云翻译使用供应商的 REST 地址。可保留默认值，也可以改成代理地址。");
        if (showApiKey)
            CreateLabeledInput(panel, isCloud ? "API Key / 应用 ID" : "API Key", service.Settings.ApiKey, value => _plugin.SetWorkshopLocalizationApiKey(value), 0f, password: true, commitOnEndEdit: true, tooltip: isCloud ? "云翻译供应商发给你的访问密钥或应用 ID。" : "大模型平台发给你的 API Key；不会写入日志。\nOllama 本地服务通常不需要填写。" );
        if (showSecret)
            CreateLabeledInput(panel, "Secret Key / 密钥", service.Settings.SecretKey, value => _plugin.SetWorkshopLocalizationSecretKey(value), 0f, password: true, commitOnEndEdit: true, tooltip: "部分云翻译供应商需要的第二段密钥：腾讯云 SecretKey、百度 Secret Key 或有道密钥。" );
        if (showRegion)
            CreateLabeledInput(panel, "区域", service.Settings.Region, value => _plugin.SetWorkshopLocalizationRegion(value), 0f, commitOnEndEdit: true, tooltip: "云服务资源所在区域。例如腾讯云 ap-guangzhou、微软 Translator 的资源区域。" );
        if (showProject)
            CreateLabeledInput(panel, "项目 ID", service.Settings.ProjectId, value => _plugin.SetWorkshopLocalizationProjectId(value), 0f, commitOnEndEdit: true, tooltip: "云项目或资源的数字/字符串 ID；没有要求的供应商可以留空。" );
        if (showModel)
            CreateLabeledInput(panel, "模型名称", service.Settings.Model, value => _plugin.SetWorkshopLocalizationModel(value), 0f, commitOnEndEdit: true, tooltip: "大模型平台上的模型名称，例如 deepseek-chat、gpt-4o-mini、claude-3-5-sonnet。云机器翻译通常不需要填写。" );

        CreateLabeledInput(panel, "每批翻译数量", service.Settings.BatchSize.ToString(), value => _plugin.SetWorkshopLocalizationBatchSize(value), 0f, commitOnEndEdit: true, tooltip: "每次发送给翻译接口的文本数量，范围 1-50。数量越小越稳，但请求次数更多；修改后下次翻译任务生效。" );

        GameObject toggles = CreateRow(panel, 40f, 7f);
        CreateToggleAction(toggles.transform, "启用缓存译文", service.Enabled, value => _plugin.SetWorkshopLocalizationEnabled(value), "关闭后立即回退到原始 Getter 文本。");
        CreateToggleAction(toggles.transform, "应用到原生显示", service.ApplyToNativeUi, value => _plugin.SetWorkshopLocalizationApplyToNativeUi(value), "覆盖 ItemAsset、VehicleAsset 和 Workshop Action 的显示 Getter。");
        GameObject toggles2 = CreateRow(panel, 40f, 7f);
        CreateToggleAction(toggles2.transform, "资产刷新后自动扫描", service.AutoScanOnAssetsRefreshed, value => _plugin.SetWorkshopLocalizationAutoScan(value));
        CreateToggleAction(toggles2.transform, "自动翻译未缓存", service.AutoTranslateMissing, value => _plugin.SetWorkshopLocalizationAutoTranslate(value), "需要已配置 API；默认关闭以避免意外费用。");

        GameObject firstActions = CreateRow(panel, 40f, 7f);
        CreateButton(firstActions.transform, "扫描并应用缓存", () =>
        {
            _plugin.ScanWorkshopLocalization();
            RefreshWorkshopLocalizationPage();
        }, 0f, 38f, CheatMenuStyle.Accent, 13);
        CreateButton(firstActions.transform, "重新加载缓存", () =>
        {
            _plugin.ReloadWorkshopLocalizationCache();
            RefreshWorkshopLocalizationPage();
        }, 0f, 38f, CheatMenuStyle.Surface, 13);

        GameObject secondActions = CreateRow(panel, 40f, 7f);
        CreateButton(secondActions.transform, "翻译未缓存文本", () =>
        {
            bool started = _plugin.TranslateWorkshopLocalization(false);
            RefreshWorkshopLocalizationPage();
            SetStatus(started ? "Workshop 未缓存文本翻译已启动。" : "未能启动 Workshop 翻译，请检查 API 配置或当前任务状态。", 4f, started ? StatusKind.Info : StatusKind.Warning);
        }, 0f, 38f, CheatMenuStyle.Surface, 13);
        CreateButton(secondActions.transform, "强制重译当前文本", () =>
        {
            bool started = _plugin.TranslateWorkshopLocalization(true);
            RefreshWorkshopLocalizationPage();
            SetStatus(started ? "Workshop 强制重译已启动。" : "未能启动 Workshop 强制重译，请检查 API 配置或当前任务状态。", 4f, started ? StatusKind.Info : StatusKind.Warning);
        }, 0f, 38f, CheatMenuStyle.Surface, 13);
        CreateButton(secondActions.transform, "取消", () =>
        {
            _plugin.CancelWorkshopLocalization();
            RefreshWorkshopLocalizationPage();
        }, 0f, 38f, CheatMenuStyle.Danger, 13);

        CreateLabel(panel, $"缓存文件：{service.CachePath}", 11, CheatMenuStyle.Muted);
    }
    private void ToggleWorkshopProtocolDrawer(Button trigger)
    {
        if (_workshopProtocolDrawerOpen)
        {
            HideWorkshopProtocolDrawer();
            return;
        }

        _workshopProtocolDrawerOpen = true;
        _workshopProtocolTriggerButton = trigger;
        BuildWorkshopProtocolDrawer();
    }

    private void BuildWorkshopProtocolDrawer()
    {
        if (_workshopProtocolTriggerButton == null || _root == null || _plugin.WorkshopLocalization == null)
            return;

        if (_workshopProtocolDrawerRoot != null)
            QueueForDestroy(_workshopProtocolDrawerRoot);

        _workshopProtocolDrawerRoot = CreateObject("WorkshopProtocolDrawer", _root.transform, typeof(Image));
        Image popupImage = _workshopProtocolDrawerRoot.GetComponent<Image>();
        popupImage.color = CheatMenuStyle.Panel;
        Outline popupOutline = _workshopProtocolDrawerRoot.AddComponent<Outline>();
        popupOutline.effectColor = CheatMenuStyle.InputBorder;
        popupOutline.effectDistance = Vector2.one;
        popupOutline.useGraphicAlpha = false;
        _workshopProtocolDrawerPopup = _workshopProtocolDrawerRoot.GetComponent<RectTransform>();
        _workshopProtocolDrawerPopup.anchorMin = _workshopProtocolDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
        _workshopProtocolDrawerPopup.pivot = new Vector2(0.5f, 1f);

        string[] protocols = MachineTranslationSettings.SupportedProtocols;
        const int columns = 3;
        const float cellWidth = 112f;
        const float cellHeight = 28f;
        const float spacing = 4f;
        const float horizontalPadding = 6f;
        const float verticalPadding = 6f;
        int rows = Mathf.CeilToInt(protocols.Length / (float)columns);
        _workshopProtocolDrawerPopup.sizeDelta = new Vector2(
            horizontalPadding * 2f + columns * cellWidth + (columns - 1) * spacing,
            verticalPadding * 2f + rows * cellHeight + Math.Max(0, rows - 1) * spacing);

        GridLayoutGroup grid = _workshopProtocolDrawerRoot.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset((int)horizontalPadding, (int)horizontalPadding, (int)verticalPadding, (int)verticalPadding);
        grid.spacing = new Vector2(spacing, spacing);
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;

        string currentProtocol = _plugin.WorkshopLocalization.Settings.ApiProtocol;
        foreach (string protocol in protocols)
        {
            string capturedProtocol = protocol;
            string label = PluginLocalization.Translate(MachineTranslationSettings.GetDisplayName(capturedProtocol));
            Button option = CreateButton(
                _workshopProtocolDrawerRoot.transform,
                label,
                () =>
                {
                    _plugin.SetWorkshopLocalizationApiProtocol(capturedProtocol);
                    HideWorkshopProtocolDrawer();
                    RefreshWorkshopLocalizationPage();
                },
                0f,
                cellHeight,
                string.Equals(capturedProtocol, currentProtocol, StringComparison.OrdinalIgnoreCase)
                    ? CheatMenuStyle.Accent
                    : CheatMenuStyle.SurfaceHover,
                10,
                capturedProtocol);
            SetLayout(option.gameObject, preferredHeight: cellHeight);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_workshopProtocolDrawerPopup);
        _workshopProtocolDrawerRoot.transform.SetAsLastSibling();
        PositionWorkshopProtocolDrawer();
    }

    private void MaintainWorkshopProtocolDrawer()
    {
        if (_workshopProtocolDrawerRoot == null)
            return;

        PositionWorkshopProtocolDrawer();
        if (Input.GetMouseButtonDown(0)
            && _workshopProtocolTriggerButton != null
            && _workshopProtocolDrawerPopup != null
            && !RectTransformUtility.RectangleContainsScreenPoint(_workshopProtocolDrawerPopup, Input.mousePosition, null)
            && !RectTransformUtility.RectangleContainsScreenPoint(_workshopProtocolTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
        {
            HideWorkshopProtocolDrawer();
        }
    }

    private void PositionWorkshopProtocolDrawer()
    {
        if (_workshopProtocolTriggerButton == null || _workshopProtocolDrawerPopup == null || _root == null)
            return;

        RectTransform triggerRect = _workshopProtocolTriggerButton.GetComponent<RectTransform>();
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        triggerRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, bottomLeft, null, out Vector2 bottomLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topLeft, null, out Vector2 topLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topRight, null, out Vector2 topRightLocal);

        Vector2 popupSize = _workshopProtocolDrawerPopup.sizeDelta;
        float centerX = (bottomLeftLocal.x + topRightLocal.x) * 0.5f;
        float spaceBelow = bottomLeftLocal.y - rootRect.rect.yMin;
        bool openAbove = spaceBelow < popupSize.y + 8f;
        _workshopProtocolDrawerPopup.pivot = openAbove ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        float minX = rootRect.rect.xMin + popupSize.x * 0.5f + 8f;
        float maxX = rootRect.rect.xMax - popupSize.x * 0.5f - 8f;
        float x = Mathf.Clamp(centerX, minX, maxX);
        float y = openAbove ? topLeftLocal.y : bottomLeftLocal.y;
        _workshopProtocolDrawerPopup.anchoredPosition = new Vector2(x, y);
    }

    private void HideWorkshopProtocolDrawer()
    {
        _workshopProtocolDrawerOpen = false;
        if (_workshopProtocolDrawerRoot != null)
            QueueForDestroy(_workshopProtocolDrawerRoot);
        _workshopProtocolDrawerRoot = null;
        _workshopProtocolDrawerPopup = null;
        _workshopProtocolTriggerButton = null;
    }
    private void StartWorkshopTranslationFromUi()
    {
        if (_plugin.WorkshopLocalization?.IsTranslationRunning == true)
        {
            SetStatus("翻译正在进行中，请不要重复点击开始翻译。", 5f, StatusKind.Info);
            return;
        }

        _plugin.ScanWorkshopLocalization(allowAutoTranslate: false);
        bool started = _plugin.TranslateWorkshopLocalization(false);
        WorkshopLocalizationProgress progress = _plugin.WorkshopLocalization?.Progress;
        bool running = progress?.State == WorkshopLocalizationState.Translating;
        RefreshWorkshopLocalizationPage();
        if (started || running)
        {
            SetStatus("已开始翻译未缓存文本。", 5f, StatusKind.Info);
            return;
        }

        string message = progress?.Message switch
        {
            "translation API is not configured" => "未能开始翻译：当前供应商配置不完整。",
            "translation cancelled" => "翻译任务已取消。",
            _ when !string.IsNullOrWhiteSpace(progress?.Message) => $"未能开始翻译：{progress.Message}",
            _ => "未能开始翻译：请检查当前供应商配置或任务状态。"
        };
        SetStatus(message, 5f, StatusKind.Warning);
    }
    private void StopWorkshopTranslationFromUi()
    {
        if (_plugin.WorkshopLocalization?.IsTranslationRunning != true)
        {
            SetStatus("当前没有正在运行的翻译任务。", 5f, StatusKind.Warning);
            return;
        }

        _plugin.CancelWorkshopLocalization();
        RefreshWorkshopLocalizationPage();
        SetStatus("已请求停止翻译；已完成并写入缓存的译文会保留。", 5f, StatusKind.Warning);
    }
    private void RefreshWorkshopLocalizationPage()
    {
        if (_activeToolsTab != ToolsTab.Translation)
            return;
        ReplaceContentHost();
        BuildToolsTab();
    }

    private void RefreshWorkshopLocalizationPageValues()
    {
        WorkshopLocalizationService service = _plugin.WorkshopLocalization;
        if (service == null)
            return;
        WorkshopLocalizationProgress progress = service.Progress;
        string state = progress.State switch
        {
            WorkshopLocalizationState.Scanning => "扫描中",
            WorkshopLocalizationState.Translating => "翻译中",
            WorkshopLocalizationState.Cancelled => "已取消",
            WorkshopLocalizationState.Error => "错误",
            _ when string.Equals(progress.Message, "translation complete", StringComparison.Ordinal) => "完成",
            _ when string.Equals(progress.Message, "no uncached text", StringComparison.Ordinal) => "完成",
            _ => "空闲"
        };
        if (_workshopLocalizationStateText != null)
        {
            _workshopLocalizationStateText.text = PluginLocalization.Translate($"状态：{state}；已扫描物品 {progress.ItemsScanned} / 载具 {progress.VehiclesScanned}");
            _workshopLocalizationStateText.color = progress.State switch
            {
                WorkshopLocalizationState.Translating or WorkshopLocalizationState.Scanning => CheatMenuStyle.Accent,
                WorkshopLocalizationState.Error => CheatMenuStyle.Danger,
                WorkshopLocalizationState.Cancelled => CheatMenuStyle.Warning,
                _ when string.Equals(progress.Message, "translation complete", StringComparison.Ordinal) => CheatMenuStyle.Success,
                _ when string.Equals(progress.Message, "no uncached text", StringComparison.Ordinal) => CheatMenuStyle.Success,
                _ => CheatMenuStyle.Text
            };
        }
        if (_workshopLocalizationSummaryText != null)
            _workshopLocalizationSummaryText.text = PluginLocalization.Translate($"文本总数 {progress.TotalTexts} · 缓存命中 {progress.CacheHits} · 中文跳过 {progress.SkippedTargetLanguageTexts} · 待翻译 {progress.PendingTranslations} · 完成 {progress.CompletedTranslations} · 失败 {progress.FailedTranslations}");
        if (_workshopLocalizationApiText != null)
        {
            string apiState = service.ApiConfigured ? "已配置" : "未配置";
            _workshopLocalizationApiText.text = PluginLocalization.Translate($"翻译接口：{apiState}；供应商：{MachineTranslationSettings.GetDisplayName(service.Settings.ApiProtocol)}；目标语言：{service.Settings.TargetLanguage}；模型：{(string.IsNullOrWhiteSpace(service.Settings.Model) ? "未填写" : service.Settings.Model)}");
        }
    }
    private void BuildInventoryArrangePanel(Transform panel)
    {
        InventoryArrangeSettings settings = _plugin.GetInventoryArrangeSettings();
        string presetName = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(settings.Preset));
        CreateLabel(panel, "安全背包整理", 17, CheatMenuStyle.Text);
        CreateLabel(panel, $"排序预设：{presetName}；默认禁止自动掉落。", 13, CheatMenuStyle.Muted);

        GameObject presetRow = CreateRow(panel, 44f, 8f);
        CreateLabel(presetRow.transform, "排序预设", 14, CheatMenuStyle.Text, 110f, TextAnchor.MiddleLeft);
        Button presetButton = null;
        presetButton = CreateButton(
            presetRow.transform,
            $"{presetName} ▼",
            () => ToggleInventoryPresetDrawer(presetButton),
            0f,
            40f,
            CheatMenuStyle.Surface,
            13,
            "点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。");
        SetLayout(presetButton.gameObject, flexibleWidth: 1f);
        if (settings.Preset == InventorySortPreset.Custom)
        {
            CreateLabel(panel, "自定义排序链（最多 5 项）：Key:Direction,Key:Direction", 12, CheatMenuStyle.Muted);
            CreateInput(panel, _plugin.InventoryArrangeCustomKeys, value => _plugin.SetInventoryArrangeCustomKeys(value), 0f, 1f);
        }

        GameObject options = CreateRow(panel, 40f, 7f);
        CreateToggleAction(options.transform, "自动旋转", settings.AutoRotate, value =>
        {
            _plugin.SetInventoryArrangeAutoRotate(value);
            SetStatus(value ? "整理将允许自动旋转物品。" : "整理已禁止自动旋转物品。", 4f, StatusKind.Info);
        }, "允许物品在寻找位置时旋转 90°；关闭后保持原旋转。");
        CreateToggleAction(options.transform, "允许跨页", settings.AllowCrossPage, value =>
        {
            _plugin.SetInventoryArrangeCrossPage(value);
            SetStatus(value ? "整理将允许在当前范围内跨页安置。" : "整理将保持原页范围。", 4f, StatusKind.Info);
        }, "允许在当前整理范围的随身页之间寻找空位；关闭后保持原页范围。");

        GameObject protection = CreateRow(panel, 40f, 7f);
        CreateToggleAction(protection.transform, "保护装备", settings.ProtectEquipped, value =>
        {
            _plugin.SetInventoryArrangeProtectEquipped(value);
            SetStatus(value ? "已保护当前装备物品。" : "将允许整理当前装备物品。", 4f, StatusKind.Info);
        }, "保护当前装备物品；开启时不会移除或重排它。");
        CreateToggleAction(protection.transform, "保留热键", settings.PreserveHotkeys, value =>
        {
            _plugin.SetInventoryArrangePreserveHotkeys(value);
            SetStatus(value ? "已保护热键绑定物品。" : "将允许移动热键绑定物品。", 4f, StatusKind.Info);
        }, "保护绑定热键的物品，避免整理后快捷栏失效。");

        CreateLabel(panel, "安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。", 12, CheatMenuStyle.Warning);
        CreateLabel(panel, "跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。", 12, CheatMenuStyle.Muted);
        GameObject primary = CreateRow(panel, 40f, 7f);
        CreateButton(primary.transform, "预览当前", () => ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, true), 120f, 34f, CheatMenuStyle.Surface, 13, "只生成移动、归并和跨页摘要，不修改库存。");
        CreateButton(primary.transform, "整理当前", () => ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, false), 120f, 34f, CheatMenuStyle.Accent, 13, "整理当前上下文：打开储物箱时整理储物箱，否则整理随身容器。");
        CreateButton(primary.transform, "整理全部随身", () => ShowInventoryArrangeResult(InventoryArrangeScope.AllCarried, InventoryArrangeAction.Arrange, false), 150f, 34f, CheatMenuStyle.Accent, 13, "整理所有随身页，不处理当前储物箱；默认保护装备和热键。");

        GameObject secondary = CreateRow(panel, 40f, 7f);
        CreateButton(secondary.transform, "仅压缩", () => ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Compact, false), 120f, 34f, CheatMenuStyle.Surface, 13, "只压缩空间并保持相对顺序；不按普通预设重排，自动补弹仍会执行。");
        CreateButton(secondary.transform, "撤销上次整理", ShowInventoryArrangeUndo, 150f, 34f, CheatMenuStyle.Surface, 13, "撤销最近一次成功整理；外部改变库存后撤销会失效。");
        CreateButton(secondary.transform, "仅归并", () => ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Merge, false), 120f, 34f, CheatMenuStyle.Surface, 13, "只归并兼容堆叠，不重排无关物品；弹匣和弹药箱会按兼容组重新分配总弹量。");
        CreateLabel(secondary.transform, "快捷键可在窗口右下角的全局快捷键中设置。", 12, CheatMenuStyle.Muted, 0f, TextAnchor.MiddleLeft);
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
            return;

        if (_inventoryPresetDrawerRoot != null)
            QueueForDestroy(_inventoryPresetDrawerRoot);

        _inventoryPresetDrawerRoot = CreateObject("InventoryPresetDrawer", _root.transform, typeof(Image));
        Image popupImage = _inventoryPresetDrawerRoot.GetComponent<Image>();
        popupImage.color = CheatMenuStyle.Panel;
        Outline popupOutline = _inventoryPresetDrawerRoot.AddComponent<Outline>();
        popupOutline.effectColor = CheatMenuStyle.InputBorder;
        popupOutline.effectDistance = Vector2.one;
        popupOutline.useGraphicAlpha = false;
        _inventoryPresetDrawerPopup = _inventoryPresetDrawerRoot.GetComponent<RectTransform>();
        _inventoryPresetDrawerPopup.anchorMin = _inventoryPresetDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
        _inventoryPresetDrawerPopup.pivot = new Vector2(0.5f, 1f);

        InventorySortPreset[] presets = InventorySortRegistry.AvailablePresets;
        const int columns = 3;
        const float cellWidth = 112f;
        const float cellHeight = 28f;
        const float spacing = 4f;
        const float horizontalPadding = 6f;
        const float verticalPadding = 6f;
        int rows = Mathf.CeilToInt(presets.Length / (float)columns);
        _inventoryPresetDrawerPopup.sizeDelta = new Vector2(
            horizontalPadding * 2f + columns * cellWidth + spacing,
            verticalPadding * 2f + rows * cellHeight + Math.Max(0, rows - 1) * spacing);

        GridLayoutGroup grid = _inventoryPresetDrawerRoot.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset((int)horizontalPadding, (int)horizontalPadding, (int)verticalPadding, (int)verticalPadding);
        grid.spacing = new Vector2(spacing, spacing);
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;

        InventoryArrangeSettings currentSettings = _plugin.GetInventoryArrangeSettings();
        foreach (InventorySortPreset preset in presets)
        {
            InventorySortPreset capturedPreset = preset;
            string displayName = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(capturedPreset));
            Button option = CreateButton(
                _inventoryPresetDrawerRoot.transform,
                displayName,
                () =>
                {
                    _plugin.SetInventoryArrangePreset(capturedPreset);
                    HideInventoryPresetDrawer();
                    ReplaceContentHost();
                    BuildToolsTab();
                },
                0f,
                cellHeight,
                capturedPreset == currentSettings.Preset ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
                10,
                InventoryPresetTooltip(capturedPreset));
            SetLayout(option.gameObject, preferredHeight: cellHeight);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryPresetDrawerPopup);
        _inventoryPresetDrawerRoot.transform.SetAsLastSibling();
        PositionInventoryPresetDrawer();
    }

    private void MaintainInventoryPresetDrawer()
    {
        if (_inventoryPresetDrawerRoot == null)
            return;

        PositionInventoryPresetDrawer();
        if (Input.GetMouseButtonDown(0)
            && _inventoryPresetTriggerButton != null
            && _inventoryPresetDrawerPopup != null
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _inventoryPresetDrawerPopup,
                Input.mousePosition,
                null)
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _inventoryPresetTriggerButton.GetComponent<RectTransform>(),
                Input.mousePosition,
                null))
        {
            HideInventoryPresetDrawer();
        }
    }

    private void PositionInventoryPresetDrawer()
    {
        if (_inventoryPresetTriggerButton == null
            || _inventoryPresetDrawerPopup == null
            || _root == null)
            return;

        RectTransform triggerRect = _inventoryPresetTriggerButton.GetComponent<RectTransform>();
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        triggerRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, bottomLeft, null, out Vector2 bottomLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topLeft, null, out Vector2 topLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topRight, null, out Vector2 topRightLocal);

        Vector2 popupSize = _inventoryPresetDrawerPopup.sizeDelta;
        float centerX = (bottomLeftLocal.x + topRightLocal.x) * 0.5f;
        float spaceBelow = bottomLeftLocal.y - rootRect.rect.yMin;
        bool openAbove = spaceBelow < popupSize.y + 8f;
        _inventoryPresetDrawerPopup.pivot = openAbove ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        float minX = rootRect.rect.xMin + popupSize.x * 0.5f + 8f;
        float maxX = rootRect.rect.xMax - popupSize.x * 0.5f - 8f;
        float x = Mathf.Clamp(centerX, minX, maxX);
        float y = openAbove ? topLeftLocal.y : bottomLeftLocal.y;
        _inventoryPresetDrawerPopup.anchoredPosition = new Vector2(x, y);
    }

    private void HideInventoryPresetDrawer()
    {
        _inventoryPresetDrawerOpen = false;
        if (_inventoryPresetDrawerRoot != null)
            QueueForDestroy(_inventoryPresetDrawerRoot);
        _inventoryPresetDrawerRoot = null;
        _inventoryPresetDrawerPopup = null;
        _inventoryPresetTriggerButton = null;
    }
    private static string InventoryPresetTooltip(InventorySortPreset preset)
    {
        return preset switch
        {
            InventorySortPreset.Compatibility => "按类型、占用格数和名称整理，适合平时的一键整理。",
            InventorySortPreset.Compact => "尽量把物品挤到前面，保持原来的先后顺序；每次整理仍会自动补齐兼容弹匣和弹药箱。",
            InventorySortPreset.Type => "把同类物品放在一起，例如武器、衣服、食物和工具。",
            InventorySortPreset.Name => "按物品名称从 A 到 Z 排列，方便按名字找东西。",
            InventorySortPreset.LargeFirst => "先放大件，再用小件填空位，适合整理占格较大的物品。",
            InventorySortPreset.SmallFirst => "先放小件，尽量留下连续的大空位。",
            InventorySortPreset.Quality => "耐久或品质高的物品排在前面。",
            InventorySortPreset.Rarity => "按稀有度从神话、传奇、史诗、稀有、罕见到普通排列。",
            InventorySortPreset.StackFill => "相同物品会自动合堆；兼容的半弹匣或弹药箱会重新分配成满的和空的。",
            InventorySortPreset.Ammo => "剩余弹量多的弹匣或弹药箱排在前面；读不到弹量的物品不会乱改。",
            InventorySortPreset.Hotkey => "绑定快捷栏的物品优先；开启保留热键时，绑定物品不会被移动。",
            InventorySortPreset.LowMovement => "尽量保持原来的页、位置和旋转，只做必要的整理。",
            InventorySortPreset.Custom => "按下面输入框里的排序链执行，例如 Name:Descending,Area:Ascending。",
            _ => "按安全的默认规则整理。"
        };
    }

    private void ShowInventoryArrangeResult(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview)
    {
        InventoryArrangeResult result = preview
            ? _plugin.InventoryArrange?.Preview(scope, action)
            : _plugin.InventoryArrange?.Execute(scope, action, "工具页");
        SetStatus(
            result?.Message ?? "背包整理尚未准备完成。",
            6f,
            result?.Success == true ? StatusKind.Success : StatusKind.Warning);
    }

    private void ShowInventoryArrangeUndo()
    {
        InventoryArrangeResult result = _plugin.InventoryArrange?.Undo();
        SetStatus(
            result?.Message ?? "没有可撤销的整理记录。",
            6f,
            result?.Success == true ? StatusKind.Success : StatusKind.Warning);
    }

    private void BuildPointToolPanel(Transform panel)
    {
        GameObject header = CreateRow(panel, 40f, 7f);
        CreateToggleAction(header.transform, "启用准星交互工具", _plugin.PointTool.Enabled, value =>
        {
            _plugin.SetPointToolEnabled(value);
            SetStatus(value ? "准星交互工具已开启。" : "准星交互工具已关闭。", 5f, StatusKind.Info);
        });
        Button modeTrigger = null;
        modeTrigger = CreatePointToolModeButton(
            header.transform,
            _plugin.PointTool.Mode,
            () => TogglePointToolModeDrawer(modeTrigger));
        CreateLabel(panel, "中键触发；删除必须同时按住 Shift。", 13, CheatMenuStyle.Muted);
        CreateButton(panel, $"最大距离 {_plugin.PointTool.Range:0}m", () =>
        {
            float next = _plugin.PointTool.Range >= 250f ? 25f : _plugin.PointTool.Range + 25f;
            _plugin.SetPointToolRange(next);
            ReplaceContentHost();
            BuildToolsTab();
        }, 220f, 36f);
        GameObject display = CreateRow(panel, 40f, 7f);
        CreateToggleAction(display.transform, "名称", _plugin.PointTool.ShowTargetName, value => _plugin.SetPointToolShowTargetName(value));
        CreateToggleAction(display.transform, "ID / GUID", _plugin.PointTool.ShowId, value => _plugin.SetPointToolShowId(value));
        CreateToggleAction(display.transform, "生命 / 耐久", _plugin.PointTool.ShowHealth, value => _plugin.SetPointToolShowHealth(value));
        BuildPointToolDescriptions(panel);
    }

    private void BuildPointToolDescriptions(Transform parent)
    {
        GameObject descriptions = CreateObject("PointToolDescriptions", parent, typeof(Image));
        descriptions.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
        Outline outline = descriptions.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.InputBorder;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        AddVerticalLayout(descriptions, 5f, new RectOffset(12, 12, 10, 10));
        SetLayout(descriptions, preferredHeight: 286f, flexibleWidth: 1f, flexibleHeight: 1f);

        CreateLabel(descriptions.transform, "各模式功能介绍", 14, CheatMenuStyle.Text);
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Smart,
            "根据准星目标自动选择单一动作：维修、实用、准星传送、检查或 Shift + 中键删除。");
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Inspect,
            "查看准星指向对象的名称、ID / GUID、生命 / 耐久和位置等信息。");
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Repair,
            "维修车辆耐久、电瓶、油量、轮胎并扶正翻倒车辆，也可修复路障和建筑。");
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Teleport,
            "将玩家传送到准星指向的世界表面，并自动寻找安全落点。");
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Utility,
            "执行准星指向对象的原生交互，例如上车、打开容器或切换设备。");
        AddPointToolDescription(
            descriptions.transform,
            PointToolMode.Delete,
            "删除准星指向的可删除对象；必须同时按住 Shift 再点击中键。");
    }

    private void AddPointToolDescription(Transform parent, PointToolMode mode, string description)
    {
        GameObject row = CreateRow(parent, 32f, 10f);
        CreateLabel(row.transform, PointToolModeLabel(mode), 12, CheatMenuStyle.Accent, 72f, TextAnchor.MiddleLeft);
        Text detail = CreateText(row.transform, description, 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
        SetLayout(detail.gameObject, flexibleWidth: 1f, preferredHeight: 32f);
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
            _ => "智能"
        };
    }
    private Button CreatePointToolModeButton(Transform parent, PointToolMode mode, Action action)
    {
        return CreateButton(
            parent,
            PointToolModeLabel(mode),
            action,
            150f,
            40f,
            _plugin.PointTool.Mode == mode ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
            12);
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
            return;
        if (_pointToolModeDrawerRoot != null)
            QueueForDestroy(_pointToolModeDrawerRoot);
        _pointToolModeDrawerRoot = CreateObject("PointToolModeDrawer", _root.transform, typeof(Image));
        Image popupImage = _pointToolModeDrawerRoot.GetComponent<Image>();
        popupImage.color = CheatMenuStyle.Panel;
        Outline popupOutline = _pointToolModeDrawerRoot.AddComponent<Outline>();
        popupOutline.effectColor = CheatMenuStyle.InputBorder;
        popupOutline.effectDistance = Vector2.one;
        popupOutline.useGraphicAlpha = false;
        _pointToolModeDrawerPopup = _pointToolModeDrawerRoot.GetComponent<RectTransform>();
        RectTransform popup = _pointToolModeDrawerPopup;
        popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
        popup.pivot = new Vector2(0f, 1f);
        popup.sizeDelta = new Vector2(150f, 280f);

        VerticalLayoutGroup popupLayout = _pointToolModeDrawerRoot.AddComponent<VerticalLayoutGroup>();
        popupLayout.padding = new RectOffset(4, 4, 4, 4);
        popupLayout.spacing = 4f;
        popupLayout.childControlWidth = true;
        popupLayout.childControlHeight = true;
        popupLayout.childForceExpandWidth = true;
        popupLayout.childForceExpandHeight = false;

        foreach (PointToolMode mode in PointToolModes)
        {
            PointToolMode capturedMode = mode;
            Button option = CreateButton(
                _pointToolModeDrawerRoot.transform,
                PointToolModeLabel(capturedMode),
                () =>
                {
                    _plugin.SetPointToolMode(capturedMode);
                    HidePointToolModeDrawer();
                    ReplaceContentHost();
                    BuildToolsTab();
                },
                0f,
                42f,
                _plugin.PointTool.Mode == capturedMode ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
                12);
            SetLayout(option.gameObject, preferredHeight: 42f, flexibleWidth: 1f);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_pointToolModeDrawerPopup);
        PositionPointToolModeDrawer();
    }
    private void MaintainPointToolModeDrawer()
    {
        if (_pointToolModeDrawerRoot == null)
            return;
        PositionPointToolModeDrawer();
        if (Input.GetMouseButtonDown(0)
            && _pointToolModeTriggerButton != null
            && _pointToolModeDrawerPopup != null
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _pointToolModeDrawerPopup,
                Input.mousePosition,
                null)
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _pointToolModeTriggerButton.GetComponent<RectTransform>(),
                Input.mousePosition,
                null))
        {
            HidePointToolModeDrawer();
        }
    }
    private void PositionPointToolModeDrawer()
    {
        if (_pointToolModeTriggerButton == null
            || _pointToolModeDrawerPopup == null
            || _root == null)
            return;

        RectTransform triggerRect = _pointToolModeTriggerButton.GetComponent<RectTransform>();
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        triggerRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, bottomLeft, null, out Vector2 bottomLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topLeft, null, out Vector2 topLeftLocal);

        Vector2 popupSize = _pointToolModeDrawerPopup.sizeDelta;
        popupSize.x = triggerRect.rect.width;
        _pointToolModeDrawerPopup.sizeDelta = popupSize;
        _pointToolModeDrawerPopup.anchorMin = _pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
        float spaceBelow = bottomLeftLocal.y - rootRect.rect.yMin;
        bool openAbove = spaceBelow < _pointToolModeDrawerPopup.rect.height + 8f;
        _pointToolModeDrawerPopup.pivot = openAbove ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        _pointToolModeDrawerPopup.anchoredPosition = openAbove ? topLeftLocal : bottomLeftLocal;
    }
    private void HidePointToolModeDrawer()
    {
        _pointToolModeDrawerOpen = false;
        if (_pointToolModeDrawerRoot != null)
            QueueForDestroy(_pointToolModeDrawerRoot);
        _pointToolModeDrawerRoot = null;
        _pointToolModeDrawerPopup = null;
        _pointToolModeTriggerButton = null;
    }
    private void BuildMovementPanel(Transform panel)
    {
        GameObject toggles = CreateRow(panel, 42f, 8f);
        CreateToggleAction(toggles.transform, "飞行", _plugin.Movement.FlightEnabled, value =>
        {
            _plugin.Movement.SetFlight(value);
            SetStatus(value ? "飞行已开启。" : "飞行已关闭。", 5f, StatusKind.Info);
        });
        CreateToggleAction(toggles.transform, "穿墙", _plugin.Movement.NoclipEnabled, value =>
        {
            _plugin.Movement.SetNoclip(value);
            SetStatus(value ? "穿墙已开启。" : "穿墙已关闭。", 5f, StatusKind.Info);
        });
        CreateMovementSpeedSlider(panel, "水平速度", _plugin.Movement.FlightSpeed, _plugin.SetFlightSpeed);
        CreateMovementSpeedSlider(panel, "垂直速度", _plugin.Movement.VerticalSpeed, _plugin.SetFlightVerticalSpeed);
        CreateToggleAction(panel, "关闭穿墙后寻找安全位置", _plugin.Movement.SafeExit, value => _plugin.SetNoclipSafeExit(value));
        CreateLabel(panel, "WASD 移动，Space 上升，Ctrl 下降。", 13, CheatMenuStyle.Muted);
    }

    private void CreateMovementSpeedSlider(Transform parent, string label, float value, Action<float> changed)
    {
        GameObject row = CreateRow(parent, 40f, 8f);
        CreateLabel(row.transform, label, 13, CheatMenuStyle.Text, 92f, TextAnchor.MiddleLeft);
        Text valueText = CreateText(row.transform, $"{value:0}x", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, localize: false);
        SetLayout(valueText.gameObject, preferredWidth: 42f, preferredHeight: 30f);
        Slider slider = CreateTimeSlider(row.transform, Mathf.InverseLerp(1f, 10f, value), normalized =>
        {
            float snapped = Mathf.Clamp(Mathf.Round(Mathf.Lerp(1f, 10f, Mathf.Clamp01(normalized))), 1f, 10f);
            changed(snapped);
            valueText.text = $"{snapped:0}x";
        });
        SetLayout(slider.gameObject, flexibleWidth: 1f);
    }
}
