using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Patches;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Unturned.exe")]
public sealed class CheatMenuPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.codex.unturned.singleplayer-cheat-menu";
    public const string PluginName = "Unturned Singleplayer Cheat Menu";
    public const string PluginVersion = "1.1.0";

    private Harmony _harmony;
    private ConfigEntry<KeyboardShortcut> _toggleShortcut;
    private ConfigEntry<float> _uiScale;
    private ConfigEntry<int> _pageSize;
    private CheatMenuOverlayUi _ui;
    private PluginRuntimeHost _runtimeHost;
    private VehicleIconRenderer _vehicleIconRenderer;
    private readonly NativeShortcutDetector _nativeShortcut = new();
    private float _nextMaintenanceTime;
    private int _lastRuntimeUpdateFrame = -1;
    private int _lastToggleFrame = -1;
    private int _lastGuiCallbackFrame = -1;
    private EventType _lastGuiEventType = EventType.Ignore;
    private bool _menuOpen;
    private bool _hasLoggedUpdateCallback;
    private bool _hasLoggedOnGuiCallback;
    private bool _hasLoggedVehicleIconFallback;
    private bool _hasLoggedVehicleIconRequest;
    private bool _hasLoggedVehicleIconResult;
    private bool _hasLoggedVehicleIconCache;
    private bool _hasLoggedVehicleIconBound;
    private SleekWindow _capturedWindow;
    private bool _windowWasEnabled;
    private bool _windowWasShowingCursor;
    private bool _hasCapturedUnityCursor;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorWasLocked;

    internal static CheatMenuPlugin Instance { get; private set; }
    internal AssetCatalog Catalog { get; private set; }
    internal IconCache Icons { get; private set; }
    internal FavoriteStore Favorites { get; private set; }
    internal TeleportStore Teleports { get; private set; }
    internal CheatActions Actions { get; private set; }
    internal float UiScale => Mathf.Clamp(_uiScale.Value, 0.75f, 1.5f);
    internal int PageSize => Mathf.Clamp(_pageSize.Value, 12, 80);
    internal string ShortcutLabel => _toggleShortcut.Value.ToString();
    internal bool IsMenuOpen => _menuOpen;

    internal void LogRuntimeHostCreated(PluginRuntimeHost host)
    {
        Logger.LogInfo(
            $"独立运行宿主已创建：activeInHierarchy={host.gameObject.activeInHierarchy}，enabled={host.enabled}，" +
            $"vehicleIconService={_vehicleIconRenderer != null}。");
    }

    internal void LogVehicleIconFallback(VehicleAsset asset, string mode)
    {
        if (_hasLoggedVehicleIconFallback)
            return;

        _hasLoggedVehicleIconFallback = true;
        Logger.LogInfo(
            $"车辆资产的预设缩略图取景不可用，已启用{mode}。示例：{asset.vehicleName} ({asset.GUID})。");
    }

    internal void LogVehicleIconFailure(VehicleAsset asset, Exception exception)
    {
        Logger.LogWarning(
            $"车辆缩略图生成失败：{asset?.vehicleName ?? "未知车辆"} ({asset?.GUID.ToString() ?? "无 GUID"})。{exception}");
    }

    internal void LogVehicleIconRequest(VehicleAsset asset, string route)
    {
        if (_hasLoggedVehicleIconRequest)
            return;

        _hasLoggedVehicleIconRequest = true;
        Logger.LogInfo(
            $"车辆缩略图请求已提交：{asset?.vehicleName ?? "未知车辆"}；路径={route}。");
    }

    internal void LogVehicleIconResult(VehicleAsset asset, Texture2D texture, string route)
    {
        if (_hasLoggedVehicleIconResult)
            return;

        _hasLoggedVehicleIconResult = true;
        if (texture == null)
        {
            Logger.LogWarning(
                $"车辆缩略图生成返回空纹理：{asset?.vehicleName ?? "未知车辆"}；路径={route}。");
            return;
        }

        Logger.LogInfo(
            $"车辆缩略图已生成：{asset?.vehicleName ?? "未知车辆"}；路径={route}；" +
            $"尺寸={texture.width}x{texture.height}。");
    }

    internal void LogVehicleIconCached(VehicleAsset asset, Texture2D texture)
    {
        if (_hasLoggedVehicleIconCache)
            return;

        _hasLoggedVehicleIconCache = true;
        Logger.LogInfo(
            $"车辆缩略图已写入缓存：{asset?.vehicleName ?? "未知车辆"}；" +
            $"尺寸={texture?.width ?? 0}x{texture?.height ?? 0}。");
    }

    internal void LogVehicleIconBound(Texture2D texture)
    {
        if (_hasLoggedVehicleIconBound)
            return;

        _hasLoggedVehicleIconBound = true;
        Logger.LogInfo($"车辆缩略图已绑定到卡片：尺寸={texture?.width ?? 0}x{texture?.height ?? 0}。");
    }

    internal void LogRuntimeHostStarted()
    {
        Logger.LogInfo("独立运行宿主 Start 已执行。");
    }

    private void Awake()
    {
        Instance = this;
        _toggleShortcut = Config.Bind(
            "General",
            "ToggleShortcut",
            new KeyboardShortcut(KeyCode.End),
            "打开或关闭单人作弊菜单。默认 End，可自由修改。仅在真正的单人世界中生效。");
        _uiScale = Config.Bind(
            "Interface",
            "Scale",
            1.0f,
            "菜单缩放，范围 0.75 到 1.5。");
        _pageSize = Config.Bind(
            "Interface",
            "CardsPerPage",
            32,
            "物品/车辆每页卡片数量，范围 12 到 80。");

        Catalog = new AssetCatalog();
        _vehicleIconRenderer = new VehicleIconRenderer(this);
        Icons = new IconCache(_vehicleIconRenderer);
        Favorites = new FavoriteStore(Logger);
        Teleports = new TeleportStore(Logger);
        Actions = new CheatActions(Logger);
        _ui = new CheatMenuOverlayUi(this);
        _runtimeHost = PluginRuntimeHost.Create(this);
        Favorites.Load();
        Teleports.Load();

        Assets.onAssetsRefreshed += OnAssetsRefreshed;
        InstallRuntimePatches();
        Logger.LogInfo($"{PluginName} {PluginVersion} 已加载。快捷键：{ShortcutLabel}；严格限制：单人模式。");
        Version bepinExVersion = typeof(BaseUnityPlugin).Assembly.GetName().Version;
        Logger.LogInfo($"检测环境：Unturned {Provider.APP_VERSION}，Unity {Application.unityVersion}，BepInEx {bepinExVersion}。");
    }

    private void Update()
    {
        RunUpdateCallback("Unity Update");
    }

    internal void RunUpdateCallback(string callbackSource)
    {
        if (_lastRuntimeUpdateFrame == Time.frameCount)
            return;
        _lastRuntimeUpdateFrame = Time.frameCount;

        if (!_hasLoggedUpdateCallback)
        {
            _hasLoggedUpdateCallback = true;
            Logger.LogInfo($"{callbackSource} 回调已运行，快捷键轮询已启用。");
        }

        // Unturned destroys BepInEx's original Unity host during its bootstrap
        // scene transition on current builds. Harmony update callbacks remain
        // reliable, so pump both the vehicle icon queue and the UI binders from
        // this path rather than relying only on MonoBehaviour.Update.
        _vehicleIconRenderer?.PumpOne();
        OverlayIconBinder.PumpPending();

        bool unityShortcutPressed = IsToggleShortcutDown();
        bool nativeShortcutPressed = _nativeShortcut.IsPressed(_toggleShortcut.Value);
        if (unityShortcutPressed || nativeShortcutPressed)
            HandleToggleShortcut(nativeShortcutPressed && !unityShortcutPressed
                ? "Windows 原生输入"
                : "Unity 输入");

        if (_menuOpen && !SingleplayerGuard.IsReady)
            SetMenuOpen(false);
        else if (_menuOpen)
        {
            MaintainMenuInputCapture();
            _ui.Maintain();
        }

        if (Time.unscaledTime >= _nextMaintenanceTime)
        {
            _nextMaintenanceTime = Time.unscaledTime + 0.2f;
            Actions.MaintainEnabledCheats();
        }
    }

    private bool IsToggleShortcutDown()
    {
        KeyboardShortcut shortcut = _toggleShortcut.Value;
        if (shortcut.IsDown())
            return true;

        // BepInEx 5 requires every other supported key to be released. Unturned can
        // keep an internal input key active while playing, so accept the configured
        // main key plus required modifiers without rejecting unrelated held keys.
        return shortcut.MainKey != KeyCode.None
            && Input.GetKeyDown(shortcut.MainKey)
            && shortcut.Modifiers.All(Input.GetKey);
    }

    private void OnGUI()
    {
        RunGuiCallback("Unity OnGUI");
    }

    internal void RunGuiCallback(string callbackSource)
    {
        Event current = Event.current;
        EventType currentEventType = current?.type ?? EventType.Ignore;
        if (_lastGuiCallbackFrame == Time.frameCount && _lastGuiEventType == currentEventType)
            return;
        _lastGuiCallbackFrame = Time.frameCount;
        _lastGuiEventType = currentEventType;

        if (!_hasLoggedOnGuiCallback)
        {
            _hasLoggedOnGuiCallback = true;
            Logger.LogInfo($"{callbackSource} 回调已运行，菜单绘制通道已启用。");
        }

        if (IsToggleShortcutEvent(current))
        {
            HandleToggleShortcut("IMGUI 事件");
            current.Use();
        }

        // The menu itself is rendered by a top-sorted Screen Space Overlay
        // Canvas. Keep this callback only as an additional shortcut source.
    }

    private bool IsToggleShortcutEvent(Event current)
    {
        if (current == null || current.type != EventType.KeyDown)
            return false;

        KeyboardShortcut shortcut = _toggleShortcut.Value;
        return shortcut.MainKey != KeyCode.None
            && current.keyCode == shortcut.MainKey
            && shortcut.Modifiers.All(modifier => IsModifierPressed(current, modifier));
    }

    private static bool IsModifierPressed(Event current, KeyCode modifier)
    {
        return modifier switch
        {
            KeyCode.LeftShift or KeyCode.RightShift => current.shift,
            KeyCode.LeftControl or KeyCode.RightControl => current.control,
            KeyCode.LeftAlt or KeyCode.RightAlt => current.alt,
            KeyCode.LeftCommand or KeyCode.RightCommand => current.command,
            _ => Input.GetKey(modifier)
        };
    }

    private void HandleToggleShortcut(string inputSource)
    {
        if (_lastToggleFrame == Time.frameCount)
            return;

        _lastToggleFrame = Time.frameCount;
        if (!SingleplayerGuard.IsReady)
        {
            Logger.LogWarning($"未打开菜单：{SingleplayerGuard.RejectionReason}");
            return;
        }

        SetMenuOpen(!_menuOpen);
        Logger.LogInfo(
            $"快捷键 {ShortcutLabel} 已通过{inputSource}触发：菜单{(_menuOpen ? "已打开" : "已关闭")}。");
    }

    private void OnDestroy()
    {
        // Unturned replaces its bootstrap scene before the first rendered frame.
        // On current Unity builds that can destroy BepInEx's original component
        // even though the managed plugin state and our DontDestroyOnLoad bridge
        // are still valid. Removing patches here would disable the menu before it
        // ever gets an Update/OnGUI callback. The process owns these resources, so
        // preserve them until process exit instead of tearing them down on a scene
        // transition.
        Logger.LogWarning(
            "BepInEx 插件宿主 OnDestroy 已触发；保留跨场景运行宿主与补丁直到进程退出。");
    }

    internal void CloseMenu() => SetMenuOpen(false);

    internal void RefreshCatalog()
    {
        Catalog.Refresh();
        Icons.Clear();
        _ui.OnCatalogRefreshed();
        Logger.LogInfo(
            $"资产扫描完成：物品 {Catalog.Items.Count}（模组 {Catalog.WorkshopItemCount}），车辆 {Catalog.Vehicles.Count}（模组 {Catalog.WorkshopVehicleCount}）。");
    }

    private void SetMenuOpen(bool open)
    {
        if (_menuOpen == open)
            return;

        if (open && !SingleplayerGuard.IsReady)
            return;

        _menuOpen = open;
        if (open)
            CaptureMenuInput();
        else
        {
            ReleaseMenuInput();
            _ui.OnClosed();
        }

        if (open)
        {
            RefreshCatalog();
            _ui.OnOpened();
        }
    }

    private void CaptureMenuInput()
    {
        if (!_hasCapturedUnityCursor)
        {
            _cursorWasVisible = Cursor.visible;
            _cursorWasLocked = Cursor.lockState;
            _hasCapturedUnityCursor = true;
        }

        SleekWindow window = PlayerUI.window;
        if (window == null)
        {
            MaintainMenuInputCapture();
            return;
        }

        _capturedWindow = window;
        _windowWasEnabled = window.isEnabled;
        _windowWasShowingCursor = window.showCursor;
        MaintainMenuInputCapture();
    }

    private void MaintainMenuInputCapture()
    {
        if (_hasCapturedUnityCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            // SleekWindow hides the operating-system cursor and Glazier draws
            // Unturned's native cursor on its own higher-sorted canvas.
            Cursor.visible = false;
        }

        SleekWindow window = PlayerUI.window;
        if (window == null)
            return;

        if (!ReferenceEquals(_capturedWindow, window))
        {
            ReleaseMenuInput();
            _capturedWindow = window;
            _windowWasEnabled = window.isEnabled;
            _windowWasShowingCursor = window.showCursor;
        }

        window.isEnabled = true;
        // Unturned uses showCursor as part of its gameplay-input gate, not
        // merely to draw the Sleek cursor. Keep it enabled so mouse movement
        // stops controlling the camera and Glazier draws the native cursor.
        window.showCursor = true;
    }

    private void ReleaseMenuInput()
    {
        if (_capturedWindow != null)
        {
            _capturedWindow.isEnabled = _windowWasEnabled;
            _capturedWindow.showCursor = _windowWasShowingCursor;
        }

        _capturedWindow = null;
        if (_hasCapturedUnityCursor)
        {
            Cursor.lockState = _cursorWasLocked;
            Cursor.visible = _cursorWasVisible;
            _hasCapturedUnityCursor = false;
        }
    }

    private void OnAssetsRefreshed()
    {
        Catalog.MarkDirty();
        Icons.Clear();
        if (_menuOpen)
            RefreshCatalog();
    }

    private static void GameUpdatePostfix(MethodBase __originalMethod)
    {
        CheatMenuPlugin plugin = Instance;
        plugin?.RunUpdateCallback("游戏更新 Harmony Postfix");

        // PlayerUI.Update recomputes showCursor from Unturned's built-in modal
        // flags. The generic callback can already have run earlier this frame
        // from Provider.Update, so reassert our external overlay's input gate
        // specifically after PlayerUI.Update.
        if (__originalMethod?.DeclaringType == typeof(PlayerUI)
            && plugin?._menuOpen == true
            && SingleplayerGuard.IsReady)
        {
            plugin.MaintainMenuInputCapture();
            plugin._ui.Maintain();
        }
    }

    private static void GameOnGuiPostfix()
    {
        Instance?.RunGuiCallback("游戏 OnGUI Harmony Postfix");
    }

    private void InstallRuntimePatches()
    {
        _harmony = new Harmony(PluginGuid);
        PatchUpdateLoop(typeof(Provider));
        PatchUpdateLoop(typeof(MenuUI));
        PatchUpdateLoop(typeof(PlayerUI));
        PatchOnGuiLoop(typeof(MenuUI));
        PatchOnGuiLoop(typeof(PlayerUI));

        try
        {
            MethodInfo target = AccessTools.GetDeclaredMethods(typeof(PlayerLife))
                .Where(method => method.Name == "doDamage" && method.ReturnType == typeof(void))
                .OrderByDescending(method => method.GetParameters().Length)
                .FirstOrDefault();
            if (target == null)
                throw new MissingMethodException(typeof(PlayerLife).FullName, "doDamage");

            MethodInfo prefix = AccessTools.Method(typeof(GodModePatch), nameof(GodModePatch.Prefix));
            _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            Logger.LogInfo($"无敌补丁已注册：{target.DeclaringType?.FullName}.{target.Name}({target.GetParameters().Length} 参数)。");
        }
        catch (Exception ex)
        {
            Logger.LogError($"无敌补丁注册失败；其他菜单功能仍可使用。\n{ex}");
        }
    }

    private void PatchUpdateLoop(Type targetType)
    {
        try
        {
            MethodInfo target = AccessTools.DeclaredMethod(targetType, "Update", Type.EmptyTypes);
            if (target == null)
                throw new MissingMethodException(targetType.FullName, "Update");

            MethodInfo postfix = AccessTools.Method(typeof(CheatMenuPlugin), nameof(GameUpdatePostfix));
            _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Logger.LogInfo($"更新回调补丁已注册：{targetType.FullName}.Update。");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"无法挂接 {targetType.FullName}.Update；将继续尝试其他更新入口。\n{ex}");
        }
    }

    private void PatchOnGuiLoop(Type targetType)
    {
        try
        {
            MethodInfo target = AccessTools.DeclaredMethod(targetType, "OnGUI", Type.EmptyTypes);
            if (target == null)
                throw new MissingMethodException(targetType.FullName, "OnGUI");

            MethodInfo postfix = AccessTools.Method(typeof(CheatMenuPlugin), nameof(GameOnGuiPostfix));
            _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Logger.LogInfo($"绘制回调补丁已注册：{targetType.FullName}.OnGUI。");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"无法挂接 {targetType.FullName}.OnGUI；将继续尝试其他绘制入口。\n{ex}");
        }
    }
}
