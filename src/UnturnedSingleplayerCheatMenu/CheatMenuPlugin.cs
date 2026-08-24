using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Patches;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu;

[BepInPlugin("com.codex.unturned.singleplayer-cheat-menu", "Unturned Singleplayer Cheat Menu", "1.7.0")]
[BepInProcess("Unturned.exe")]
public sealed class CheatMenuPlugin : BaseUnityPlugin
{
	public const string PluginGuid = "com.codex.unturned.singleplayer-cheat-menu";

	public const string PluginName = "Unturned Singleplayer Cheat Menu";

	public const string PluginVersion = "1.7.0";

	private Harmony _harmony;

	private ConfigEntry<string> _language;

	private ConfigEntry<KeyboardShortcut> _toggleShortcut;

	private ConfigEntry<KeyboardShortcut> _arrangeCurrentShortcut;

	private ConfigEntry<KeyboardShortcut> _arrangeAllCarriedShortcut;

	private ConfigEntry<string> _inventoryArrangeCustomKeys;

	private ConfigEntry<string> _inventoryArrangePreset;

	private ConfigEntry<bool> _inventoryArrangeAutoRotate;

	private ConfigEntry<bool> _inventoryArrangeCrossPage;

	private ConfigEntry<bool> _inventoryArrangeProtectEquipped;

	private ConfigEntry<bool> _inventoryArrangePreserveHotkeys;

	private ConfigEntry<float> _uiScale;

	private ConfigEntry<int> _pageSize;

	private ConfigEntry<int> _vehicleIconResolution;

	private ConfigEntry<float> _vehicleIconFraming;

	private ConfigEntry<string> _lastMainTab;

	private ConfigEntry<string> _lastTeleportView;

	private ConfigEntry<bool> _pointToolEnabled;

	private ConfigEntry<string> _pointToolMode;

	private ConfigEntry<float> _pointToolRange;

	private ConfigEntry<bool> _pointToolShowTargetName;

	private ConfigEntry<bool> _pointToolShowId;

	private ConfigEntry<bool> _pointToolShowHealth;

	private ConfigEntry<bool> _pointToolShowHud;

	private ConfigEntry<float> _flightSpeed;

	private ConfigEntry<float> _flightVerticalSpeed;

	private ConfigEntry<bool> _noclipSafeExit;

	private ConfigEntry<bool> _workshopLocalizationEnabled;

	private ConfigEntry<string> _workshopLocalizationApiProtocol;

	private ConfigEntry<bool> _workshopLocalizationAutoScan;

	private ConfigEntry<bool> _workshopLocalizationAutoTranslate;

	private ConfigEntry<bool> _workshopLocalizationApplyToNativeUi;

	private ConfigEntry<string> _workshopLocalizationSourceLanguage;

	private ConfigEntry<string> _workshopLocalizationTargetLanguage;

	private ConfigEntry<string> _workshopLocalizationEndpoint;

	private ConfigEntry<string> _workshopLocalizationApiKey;

	private ConfigEntry<string> _workshopLocalizationSecretKey;

	private ConfigEntry<string> _workshopLocalizationRegion;

	private ConfigEntry<string> _workshopLocalizationProjectId;

	private ConfigEntry<string> _workshopLocalizationModel;

	private ConfigEntry<int> _workshopLocalizationBatchSize;

	private ConfigEntry<int> _workshopLocalizationTimeoutSeconds;

	private ConfigEntry<int> _workshopLocalizationMaxRetries;

	private ConfigEntry<bool> _workshopLocalizationSkipTargetLanguage;

	private CheatMenuOverlayUi _ui;

	private PluginRuntimeHost _runtimeHost;

	private VehicleIconRenderer _vehicleIconRenderer;

	private PointToolHud _pointToolHud;

	private GlobalShortcutManager _shortcutManager;

	private float _nextMaintenanceTime;

	private int _lastRuntimeUpdateFrame = -1;

	private int _lastWorkshopSnapshotVersion = -1;

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

	private bool _hasLoggedVehicleIconCacheWriteFailure;

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

	internal TeleportMapService TeleportMaps { get; private set; }

	internal CheatActions Actions { get; private set; }

	internal MovementCheatService Movement { get; private set; }

	internal PointToolService PointTool { get; private set; }

	internal InventoryArrangeFeature InventoryArrange { get; private set; }

	internal float UiScale => Mathf.Clamp(_uiScale.Value, 0.75f, 1.5f);

	internal int PageSize => Mathf.Clamp(_pageSize.Value, 12, 80);

	internal string ShortcutLabel => GetShortcutLabel("menu.toggle");

	internal bool IsMenuOpen => _menuOpen;

	internal string LastMainTab => _lastMainTab?.Value ?? "Character";

	internal string LastTeleportView => _lastTeleportView?.Value ?? "Map";

	internal VehicleThumbnailRenderSettings VehicleThumbnailSettings { get; private set; }

	internal WorkshopLocalizationService WorkshopLocalization { get; private set; }

	internal string InventoryArrangeCustomKeys => _inventoryArrangeCustomKeys?.Value ?? "Type:Ascending,Area:Descending,Name:Ascending";

	internal bool SetLastMainTab(string value)
	{
		return PersistInterfaceState(_lastMainTab, value);
	}

	internal bool SetLastTeleportView(string value)
	{
		return PersistInterfaceState(_lastTeleportView, value);
	}

	internal bool ApplyVehicleThumbnailSettings(int width, float framing)
	{
		VehicleThumbnailRenderSettings vehicleThumbnailRenderSettings = VehicleThumbnailRenderSettings.Normalize(width, framing);
		Icons?.ClearVehicleMemory();
		VehicleThumbnailSettings = vehicleThumbnailRenderSettings;
		bool result = true;
		bool saveOnConfigSet = base.Config.SaveOnConfigSet;
		try
		{
			base.Config.SaveOnConfigSet = false;
			_vehicleIconResolution.Value = vehicleThumbnailRenderSettings.Width;
			_vehicleIconFraming.Value = vehicleThumbnailRenderSettings.Framing;
			base.Config.Save();
		}
		catch (Exception arg)
		{
			result = false;
			base.Logger.LogWarning($"车辆缩略图设置已在本次运行中更新，但配置文件保存失败。\n{arg}");
		}
		finally
		{
			base.Config.SaveOnConfigSet = saveOnConfigSet;
		}
		_ui?.OnVehicleThumbnailSettingsApplied();
		return result;
	}

	internal bool ToggleLanguageFromUi()
	{
		string text = ((PluginLocalization.CurrentLanguage != PluginLanguage.English) ? "English" : "Chinese");
		bool saveOnConfigSet = base.Config.SaveOnConfigSet;
		bool flag = true;
		try
		{
			base.Config.SaveOnConfigSet = false;
			_language.Value = text;
			base.Config.Save();
		}
		catch (Exception arg)
		{
			flag = false;
			base.Logger.LogWarning($"Language changed for this session, but the config file could not be saved.\n{arg}");
		}
		finally
		{
			base.Config.SaveOnConfigSet = saveOnConfigSet;
		}
		PluginLocalization.Initialize(text, Provider.language);
		base.Logger.LogInfo($"Plugin UI language changed to {PluginLocalization.CurrentLanguageName}; persisted={flag}.");
		return flag;
	}

	private void NormalizeConfiguredToggleShortcut()
	{
		if (!ShortcutInputPolicy.IsAllowedShortcut(_toggleShortcut.Value))
		{
			_toggleShortcut.Value = new KeyboardShortcut(KeyCode.End);
			try
			{
				base.Config.Save();
			}
			catch (Exception arg)
			{
				base.Logger.LogWarning($"快捷键配置无效，已在本次运行中恢复为 End，但配置保存失败：{arg}");
			}
		}
	}

	private void NormalizeConfiguredInventoryShortcut(ConfigEntry<KeyboardShortcut> entry)
	{
		if (entry == null || ShortcutInputPolicy.IsAllowedShortcut(entry.Value))
		{
			return;
		}
		entry.Value = ((entry == _arrangeAllCarriedShortcut) ? new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift) : new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl));
		try
		{
			base.Config.Save();
		}
		catch (Exception arg)
		{
			base.Logger.LogWarning($"Inventory shortcut was normalized for this session but could not be saved: {arg}");
		}
	}

	private void HandleArrangeCurrentShortcut(string inputSource)
	{
		InventoryArrangeResult obj = InventoryArrange?.Execute(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, inputSource);
		base.Logger.LogInfo(obj?.Message ?? "Inventory arrange is not ready.");
	}

	private void HandleArrangeAllCarriedShortcut(string inputSource)
	{
		InventoryArrangeResult obj = InventoryArrange?.Execute(InventoryArrangeScope.AllCarried, InventoryArrangeAction.Arrange, inputSource);
		base.Logger.LogInfo(obj?.Message ?? "Inventory arrange is not ready.");
	}

	internal InventoryArrangeSettings GetInventoryArrangeSettings()
	{
		InventorySortRegistry.TryParseCustom(_inventoryArrangeCustomKeys?.Value, out var keys);
		InventorySortPreset preset = (Enum.TryParse<InventorySortPreset>(_inventoryArrangePreset?.Value, ignoreCase: true, out var result) ? result : InventorySortPreset.Compatibility);
		return new InventoryArrangeSettings
		{
			Preset = preset,
			AutoRotate = (_inventoryArrangeAutoRotate?.Value ?? true),
			AllowCrossPage = (_inventoryArrangeCrossPage?.Value ?? true),
			ProtectEquipped = (_inventoryArrangeProtectEquipped?.Value ?? true),
			CustomKeys = (keys ?? new List<InventorySortKeySpec>()),
			PreserveHotkeys = (_inventoryArrangePreserveHotkeys?.Value ?? true)
		};
	}

	internal void SetInventoryArrangePreset(InventorySortPreset preset)
	{
		PersistInterfaceState(_inventoryArrangePreset, preset.ToString());
	}

	internal void SetInventoryArrangeCustomKeys(string value)
	{
		PersistInterfaceState(_inventoryArrangeCustomKeys, value ?? string.Empty);
	}

	internal void SetInventoryArrangeAutoRotate(bool value)
	{
		PersistInterfaceState(_inventoryArrangeAutoRotate, value);
	}

	internal void SetInventoryArrangeCrossPage(bool value)
	{
		PersistInterfaceState(_inventoryArrangeCrossPage, value);
	}

	internal void SetInventoryArrangeProtectEquipped(bool value)
	{
		PersistInterfaceState(_inventoryArrangeProtectEquipped, value);
	}

	internal void SetInventoryArrangePreserveHotkeys(bool value)
	{
		PersistInterfaceState(_inventoryArrangePreserveHotkeys, value);
	}

	internal KeyboardShortcut GetShortcut(string actionId)
	{
		return _shortcutManager?.GetShortcut(actionId) ?? new KeyboardShortcut(KeyCode.End);
	}

	internal string GetShortcutLabel(string actionId)
	{
		return _shortcutManager?.GetLabel(actionId) ?? GetShortcut(actionId).ToString();
	}

	internal string GetShortcutDisplayName(string actionId)
	{
		return _shortcutManager?.GetDisplayName(actionId) ?? actionId ?? string.Empty;
	}

	internal bool SetShortcut(string actionId, KeyboardShortcut shortcut)
	{
		return _shortcutManager?.TrySetShortcut(actionId, shortcut, base.Config) ?? false;
	}

	internal KeyboardShortcut GetShortcutDefault(string actionId)
	{
		if (!(actionId == "inventory.arrange.current"))
		{
			if (!(actionId == "inventory.arrange.all-carried"))
			{
				return new KeyboardShortcut(KeyCode.End);
			}
			return new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift);
		}
		return new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl);
	}

	internal void LogRuntimeHostCreated(PluginRuntimeHost host)
	{
		base.Logger.LogInfo($"独立运行宿主已创建：activeInHierarchy={host.gameObject.activeInHierarchy}，enabled={host.enabled}，" + $"vehicleIconService={_vehicleIconRenderer != null}。");
	}

	internal void LogVehicleIconFallback(VehicleAsset asset, string mode)
	{
		if (!_hasLoggedVehicleIconFallback)
		{
			_hasLoggedVehicleIconFallback = true;
			base.Logger.LogInfo($"车辆资产的预设缩略图取景不可用，已启用{mode}。示例：{asset.vehicleName} ({asset.GUID})。");
		}
	}

	internal void LogVehicleIconFailure(VehicleAsset asset, Exception exception)
	{
		base.Logger.LogWarning(string.Format("车辆缩略图生成失败：{0} ({1})。{2}", asset?.vehicleName ?? "未知车辆", asset?.GUID.ToString() ?? "无 GUID", exception));
	}

	internal void LogVehicleIconRequest(VehicleAsset asset, string route)
	{
		if (!_hasLoggedVehicleIconRequest)
		{
			_hasLoggedVehicleIconRequest = true;
			base.Logger.LogInfo("车辆缩略图请求已提交：" + (asset?.vehicleName ?? "未知车辆") + "；路径=" + route + "。");
		}
	}

	internal void LogVehicleIconResult(VehicleAsset asset, Texture2D texture, string route)
	{
		if (!_hasLoggedVehicleIconResult)
		{
			_hasLoggedVehicleIconResult = true;
			if (texture == null)
			{
				base.Logger.LogWarning("车辆缩略图生成返回空纹理：" + (asset?.vehicleName ?? "未知车辆") + "；路径=" + route + "。");
			}
			else
			{
				base.Logger.LogInfo("车辆缩略图已生成：" + (asset?.vehicleName ?? "未知车辆") + "；路径=" + route + "；" + $"尺寸={texture.width}x{texture.height}。");
			}
		}
	}

	internal void LogVehicleIconCached(VehicleAsset asset, VehicleThumbnailRenderSettings settings, string path)
	{
		if (!_hasLoggedVehicleIconCache)
		{
			_hasLoggedVehicleIconCache = true;
			base.Logger.LogInfo("车辆缩略图已写入缓存：" + (asset?.vehicleName ?? "未知车辆") + "；" + $"配置={settings?.Width ?? 0}x{settings?.Height ?? 0}/f{settings?.GetFramingMilli() ?? 0}；" + "路径=" + path + "。");
		}
	}

	internal void LogVehicleIconCacheWriteFailure(VehicleAsset asset, VehicleThumbnailRenderSettings settings, string failure)
	{
		if (!_hasLoggedVehicleIconCacheWriteFailure)
		{
			_hasLoggedVehicleIconCacheWriteFailure = true;
			base.Logger.LogWarning("车辆缩略图磁盘缓存写入失败，将继续使用内存缓存。示例：" + (asset?.vehicleName ?? "未知车辆") + "；" + $"配置={settings?.Width ?? 0}x{settings?.Height ?? 0}/f{settings?.GetFramingMilli() ?? 0}；" + "原因=" + (failure ?? "未知原因") + "。");
		}
	}

	internal void LogVehicleIconBound(Texture2D texture)
	{
		if (!_hasLoggedVehicleIconBound)
		{
			_hasLoggedVehicleIconBound = true;
			base.Logger.LogInfo($"车辆缩略图已绑定到卡片：尺寸={texture?.width ?? 0}x{texture?.height ?? 0}。");
		}
	}

	internal void LogRuntimeHostStarted()
	{
		base.Logger.LogInfo("独立运行宿主 Start 已执行。");
	}

	private void Awake()
	{
		Instance = this;
		_language = base.Config.Bind("Interface", "Language", "Auto", "UI language. Auto follows Unturned; supported values: Auto, English, Chinese. / 界面语言：Auto 跟随 Unturned，也可填写 English 或 Chinese。");
		PluginLocalization.Initialize(_language.Value, Provider.language);
		_toggleShortcut = base.Config.Bind("General", "ToggleShortcut", new KeyboardShortcut(KeyCode.End), PluginLocalization.Translate("打开或关闭单人作弊菜单。默认 End，可自由修改。仅在真正的单人世界中生效。"));
		NormalizeConfiguredToggleShortcut();
		_shortcutManager = new GlobalShortcutManager(base.Logger);
		_shortcutManager.Register("menu.toggle", "打开或关闭菜单", _toggleShortcut, new KeyboardShortcut(KeyCode.End), HandleToggleShortcut);
		_arrangeCurrentShortcut = base.Config.Bind("General", "ArrangeCurrentShortcut", new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl), "整理当前容器；可在右下角全局快捷键窗口中修改。默认 Ctrl+I。仅在单人模式生效。");
		_arrangeAllCarriedShortcut = base.Config.Bind("General", "ArrangeAllCarriedShortcut", new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift), "整理全部随身容器；可在右下角全局快捷键窗口中修改。默认 Ctrl+Shift+I。仅在单人模式生效。");
		_inventoryArrangeCustomKeys = base.Config.Bind("InventoryArrange", "CustomKeys", "Type:Ascending,Area:Descending,Name:Ascending", "自定义排序链，最多 5 项，例如 Type:Ascending,Quality:Descending,Area:Descending。");
		_inventoryArrangePreset = base.Config.Bind("InventoryArrange", "Preset", "Compatibility", "Compatibility, Compact, Type, Name, LargeFirst, SmallFirst, Quality, Rarity, StackFill, Ammo, Hotkey, LowMovement, Custom.");
		_inventoryArrangeAutoRotate = base.Config.Bind("InventoryArrange", "AutoRotate", defaultValue: true, "允许整理时自动旋转物品。");
		_inventoryArrangeCrossPage = base.Config.Bind("InventoryArrange", "AllowCrossPage", defaultValue: true, "允许在整理范围内跨随身页安置。");
		_inventoryArrangeProtectEquipped = base.Config.Bind("InventoryArrange", "ProtectEquipped", defaultValue: true, "保护当前装备物品。");
		_inventoryArrangePreserveHotkeys = base.Config.Bind("InventoryArrange", "PreserveHotkeys", defaultValue: true, "保护热键绑定物品。");
		NormalizeConfiguredInventoryShortcut(_arrangeCurrentShortcut);
		NormalizeConfiguredInventoryShortcut(_arrangeAllCarriedShortcut);
		_shortcutManager.Register("inventory.arrange.current", "整理当前容器", _arrangeCurrentShortcut, new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl), HandleArrangeCurrentShortcut);
		_shortcutManager.Register("inventory.arrange.all-carried", "整理全部随身容器", _arrangeAllCarriedShortcut, new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift), HandleArrangeAllCarriedShortcut);
		_uiScale = base.Config.Bind("Interface", "Scale", 1f, PluginLocalization.Translate("菜单缩放，范围 0.75 到 1.5。"));
		_pageSize = base.Config.Bind("Interface", "CardsPerPage", 32, PluginLocalization.Translate("物品/车辆每页卡片数量，范围 12 到 80。"));
		_vehicleIconResolution = base.Config.Bind("Interface", "VehicleIconResolution", 128, PluginLocalization.Translate("车辆缩略图宽度：128、192 或 256；高度自动使用 4:3 比例。"));
		_vehicleIconFraming = base.Config.Bind("Interface", "VehicleIconFraming", 1f, PluginLocalization.Translate("自动取景倍率，范围 0.5 到 1.5，默认 1.0。"));
		_lastMainTab = base.Config.Bind("Interface", "LastMainTab", "Character", "Last selected main menu tab.");
		_lastTeleportView = base.Config.Bind("Interface", "LastTeleportView", "Map", "Last selected teleport subview: Map or Points.");
		_pointToolEnabled = base.Config.Bind("PointTool", "Enabled", defaultValue: false, "Enable the crosshair interaction tool.");
		_pointToolMode = base.Config.Bind("PointTool", "Mode", "Smart", "Inspect, Repair, Teleport, Utility, Delete, or Smart.");
		_pointToolRange = base.Config.Bind("PointTool", "Range", 100f, "Maximum crosshair interaction range in meters.");
		_pointToolShowTargetName = base.Config.Bind("PointTool", "ShowTargetName", defaultValue: true, "Show the target name in the point HUD.");
		InventoryArrange = new InventoryArrangeFeature(base.Logger, GetInventoryArrangeSettings);
		_pointToolShowId = base.Config.Bind("PointTool", "ShowId", defaultValue: true, "Show target ID and GUID in the point HUD.");
		_pointToolShowHealth = base.Config.Bind("PointTool", "ShowHealth", defaultValue: true, "Show target health or durability in the point HUD.");
		_pointToolShowHud = base.Config.Bind("Interface", "ShowPointToolHud", defaultValue: true, "Show the point-tool HUD.");
		_flightSpeed = base.Config.Bind("Movement", "FlightSpeed", 3f, "Horizontal flight speed multiplier.");
		_flightVerticalSpeed = base.Config.Bind("Movement", "FlightVerticalSpeed", 2f, "Vertical flight speed multiplier.");
		_noclipSafeExit = base.Config.Bind("Movement", "NoClipSafeExit", defaultValue: true, "Search for a safe position when noclip is disabled.");
		_workshopLocalizationEnabled = base.Config.Bind("WorkshopLocalization", "Enabled", defaultValue: false, "Apply cached translations to loaded Workshop assets.");
		_workshopLocalizationApiProtocol = base.Config.Bind("WorkshopLocalization", "ApiProtocol", "OpenAICompatible", "Translation API protocol. Configure it in the in-game Translation page.");
		_workshopLocalizationAutoScan = base.Config.Bind("WorkshopLocalization", "AutoScanOnAssetsRefreshed", defaultValue: true, "Rescan loaded Workshop assets after an asset refresh.");
		_workshopLocalizationAutoTranslate = base.Config.Bind("WorkshopLocalization", "AutoTranslateMissing", defaultValue: false, "Automatically translate uncached text; disabled by default to avoid unexpected API charges.");
		_workshopLocalizationApplyToNativeUi = base.Config.Bind("WorkshopLocalization", "ApplyToNativeUi", defaultValue: true, "Apply cached translations to Unturned Getter display paths.");
		_workshopLocalizationSourceLanguage = base.Config.Bind("WorkshopLocalization", "SourceLanguage", "English", "Source language sent to the translation provider.");
		_workshopLocalizationTargetLanguage = base.Config.Bind("WorkshopLocalization", "TargetLanguage", "zh-CN", "Target language used by the cache and translation provider.");
		_workshopLocalizationEndpoint = base.Config.Bind("WorkshopLocalization", "Endpoint", string.Empty, "OpenAI-compatible Chat Completions endpoint or base URL.");
		_workshopLocalizationApiKey = base.Config.Bind("WorkshopLocalization", "ApiKey", string.Empty, "Translation API key; it is never written to logs.");
		_workshopLocalizationSecretKey = base.Config.Bind("WorkshopLocalization", "SecretKey", string.Empty, "Cloud translation secret key; it is never written to logs.");
		_workshopLocalizationRegion = base.Config.Bind("WorkshopLocalization", "Region", string.Empty, "Cloud provider region when required.");
		_workshopLocalizationProjectId = base.Config.Bind("WorkshopLocalization", "ProjectId", string.Empty, "Cloud provider project ID when required.");
		_workshopLocalizationModel = base.Config.Bind("WorkshopLocalization", "Model", string.Empty, "Translation model name.");
		_workshopLocalizationBatchSize = base.Config.Bind("WorkshopLocalization", "BatchSize", 50, "Translation batch size, clamped to 1-50.");
		_workshopLocalizationTimeoutSeconds = base.Config.Bind("WorkshopLocalization", "TimeoutSeconds", 30, "Translation request timeout in seconds, clamped to 5-120.");
		_workshopLocalizationMaxRetries = base.Config.Bind("WorkshopLocalization", "MaxRetries", 2, "Maximum retries for timeout, rate-limit, and server errors, clamped to 0-5.");
		_workshopLocalizationSkipTargetLanguage = base.Config.Bind("WorkshopLocalization", "SkipLikelyTargetLanguageText", defaultValue: true, "Skip obvious target-language source text during automatic translation.");
		VehicleThumbnailSettings = VehicleThumbnailRenderSettings.Normalize(_vehicleIconResolution.Value, _vehicleIconFraming.Value);
		if (VehicleThumbnailSettings.Width != _vehicleIconResolution.Value || Math.Abs(VehicleThumbnailSettings.Framing - _vehicleIconFraming.Value) > 0.0001f)
		{
			base.Logger.LogWarning($"车辆缩略图配置无效，当前运行已规范化为 {VehicleThumbnailSettings.Width}x{VehicleThumbnailSettings.Height}，" + $"取景倍率 {VehicleThumbnailSettings.Framing:0.00}。");
		}
		Catalog = new AssetCatalog();
		WorkshopLocalization = new WorkshopLocalizationService(GetWorkshopTranslationCachePath(_workshopLocalizationTargetLanguage.Value), new MachineTranslationSettings
		{
			ApiProtocol = _workshopLocalizationApiProtocol.Value,
			Endpoint = _workshopLocalizationEndpoint.Value,
			ApiKey = _workshopLocalizationApiKey.Value,
			SecretKey = _workshopLocalizationSecretKey.Value,
			Region = _workshopLocalizationRegion.Value,
			ProjectId = _workshopLocalizationProjectId.Value,
			Model = _workshopLocalizationModel.Value,
			SourceLanguage = _workshopLocalizationSourceLanguage.Value,
			TargetLanguage = _workshopLocalizationTargetLanguage.Value,
			BatchSize = _workshopLocalizationBatchSize.Value,
			TimeoutSeconds = _workshopLocalizationTimeoutSeconds.Value,
			MaxRetries = _workshopLocalizationMaxRetries.Value
		}, delegate(string message)
		{
			base.Logger.LogInfo(message);
		}, delegate(string message)
		{
			base.Logger.LogWarning(message);
		});
		WorkshopLocalization.Enabled = _workshopLocalizationEnabled.Value;
		WorkshopLocalization.AutoScanOnAssetsRefreshed = _workshopLocalizationAutoScan.Value;
		WorkshopLocalization.AutoTranslateMissing = _workshopLocalizationAutoTranslate.Value;
		WorkshopLocalization.ApplyToNativeUi = _workshopLocalizationApplyToNativeUi.Value;
		WorkshopLocalization.SkipLikelyTargetLanguageText = _workshopLocalizationSkipTargetLanguage.Value;
		_vehicleIconRenderer = new VehicleIconRenderer(this);
		Icons = new IconCache(_vehicleIconRenderer, new VehicleIconDiskCache(), () => VehicleThumbnailSettings);
		Favorites = new FavoriteStore(base.Logger);
		Teleports = new TeleportStore(base.Logger);
		TeleportMaps = new TeleportMapService(base.Logger);
		Actions = new CheatActions(base.Logger);
		Movement = new MovementCheatService(base.Logger)
		{
			FlightSpeed = Mathf.Clamp(_flightSpeed.Value, 1f, 10f),
			VerticalSpeed = Mathf.Clamp(_flightVerticalSpeed.Value, 1f, 10f),
			SafeExit = _noclipSafeExit.Value
		};
		_pointToolHud = new PointToolHud(base.Logger);
		PointTool = new PointToolService(base.Logger, _pointToolHud)
		{
			Enabled = _pointToolEnabled.Value,
			Mode = ParsePointToolMode(_pointToolMode.Value),
			Range = Mathf.Clamp(_pointToolRange.Value, 5f, 250f),
			ShowTargetName = _pointToolShowTargetName.Value,
			ShowId = _pointToolShowId.Value,
			ShowHealth = _pointToolShowHealth.Value,
			ShowHud = _pointToolShowHud.Value
		};
		_ui = new CheatMenuOverlayUi(this);
		_runtimeHost = PluginRuntimeHost.Create(this);
		Favorites.Load();
		Teleports.Load();
		Assets.onAssetsRefreshed = (AssetsRefreshed)Delegate.Combine(Assets.onAssetsRefreshed, new AssetsRefreshed(OnAssetsRefreshed));
		InstallRuntimePatches();
		base.Logger.LogInfo("Unturned Singleplayer Cheat Menu 1.7.0 已加载。快捷键：" + ShortcutLabel + "；严格限制：单人模式。");
		base.Logger.LogInfo("Plugin UI language: " + PluginLocalization.CurrentLanguageName + "; configured=" + _language.Value + "; Unturned=" + Provider.language + ".");
		Version version = typeof(BaseUnityPlugin).Assembly.GetName().Version;
		base.Logger.LogInfo($"检测环境：Unturned {Provider.APP_VERSION}，Unity {Application.unityVersion}，BepInEx {version}。");
	}

	private bool PersistInterfaceState<T>(ConfigEntry<T> entry, T value)
	{
		if (entry == null)
		{
			return false;
		}
		if (EqualityComparer<T>.Default.Equals(entry.Value, value))
		{
			return true;
		}
		bool saveOnConfigSet = base.Config.SaveOnConfigSet;
		try
		{
			base.Config.SaveOnConfigSet = false;
			entry.Value = value;
			base.Config.Save();
			return true;
		}
		catch (Exception arg)
		{
			base.Logger.LogWarning($"界面状态已在本次运行中更新，但配置文件保存失败：{entry.Definition.Key}。\n{arg}");
			return false;
		}
		finally
		{
			base.Config.SaveOnConfigSet = saveOnConfigSet;
		}
	}

	private void Update()
	{
		RunUpdateCallback("Unity Update");
	}

	internal void RunUpdateCallback(string callbackSource)
	{
		if (_lastRuntimeUpdateFrame == Time.frameCount)
		{
			return;
		}
		_lastRuntimeUpdateFrame = Time.frameCount;
		if (!_hasLoggedUpdateCallback)
		{
			_hasLoggedUpdateCallback = true;
			base.Logger.LogInfo(callbackSource + " 回调已运行，快捷键轮询已启用。");
		}
		IconCache icons = Icons;
		if (icons == null || !icons.PumpOne())
		{
			_vehicleIconRenderer?.PumpOne();
		}
		OverlayIconBinder.PumpPending();
		WorkshopLocalization?.Pump();
		if (WorkshopLocalization != null && WorkshopLocalization.SnapshotVersion != _lastWorkshopSnapshotVersion)
		{
			_lastWorkshopSnapshotVersion = WorkshopLocalization.SnapshotVersion;
			_ui?.OnLocalizedAssetViewsRefreshed();
		}
		_shortcutManager?.ProcessUpdate(Time.frameCount, Time.realtimeSinceStartup, _ui?.IsCapturingShortcut ?? false);
		InventoryArrange?.OnPlayerInitialized();
		InventoryArrange?.PumpQuickTransfer();
		if (_menuOpen && !SingleplayerGuard.IsReady)
		{
			SetMenuOpen(open: false);
		}
		else if (_menuOpen)
		{
			MaintainMenuInputCapture();
			_ui.Maintain();
		}
		if (!SingleplayerGuard.IsReady)
		{
			WorkshopLocalization?.CancelTranslation();
			Movement?.Restore();
			if (PointTool != null)
			{
				PointTool.Enabled = false;
			}
			_pointToolHud?.Hide();
		}
		else
		{
			Movement?.Pump();
			PointTool?.Pump();
			_pointToolHud?.SetMovement(Movement?.FlightEnabled ?? false, Movement?.NoclipEnabled ?? false, Movement?.FlightSpeed ?? 0f);
		}
		if (Time.unscaledTime >= _nextMaintenanceTime)
		{
			_nextMaintenanceTime = Time.unscaledTime + 0.2f;
			Actions.MaintainEnabledCheats();
		}
	}

	private void OnGUI()
	{
		RunGuiCallback("Unity OnGUI");
	}

	internal void RunGuiCallback(string callbackSource)
	{
		Event current = Event.current;
		EventType eventType = current?.type ?? EventType.Ignore;
		if (_lastGuiCallbackFrame == Time.frameCount && _lastGuiEventType == eventType)
		{
			return;
		}
		_lastGuiCallbackFrame = Time.frameCount;
		_lastGuiEventType = eventType;
		if (!_hasLoggedOnGuiCallback)
		{
			_hasLoggedOnGuiCallback = true;
			base.Logger.LogInfo(callbackSource + " 回调已运行，菜单绘制通道已启用。");
		}
		CheatMenuOverlayUi ui = _ui;
		if (ui != null && ui.IsCapturingShortcut)
		{
			if (current != null && current.type == EventType.KeyDown)
			{
				current.Use();
			}
			return;
		}
		GlobalShortcutManager shortcutManager = _shortcutManager;
		if (shortcutManager != null && shortcutManager.ProcessGuiEvent(current, Time.frameCount, Time.realtimeSinceStartup, _ui?.IsCapturingShortcut ?? false))
		{
			current.Use();
		}
	}

	private void HandleToggleShortcut(string inputSource)
	{
		if (!SingleplayerGuard.IsReady)
		{
			base.Logger.LogWarning("未打开菜单：" + SingleplayerGuard.RejectionReason);
			return;
		}
		SetMenuOpen(!_menuOpen);
		base.Logger.LogInfo("快捷键 " + ShortcutLabel + " 已通过" + inputSource + "触发：菜单" + (_menuOpen ? "已打开" : "已关闭") + "。");
	}

	private void OnDestroy()
	{
		base.Logger.LogWarning("BepInEx 插件宿主 OnDestroy 已触发；保留跨场景运行宿主与补丁直到进程退出。");
	}

	private void OnApplicationQuit()
	{
		try
		{
			Assets.onAssetsRefreshed = (AssetsRefreshed)Delegate.Remove(Assets.onAssetsRefreshed, new AssetsRefreshed(OnAssetsRefreshed));
			WorkshopLocalization?.Dispose();
			_harmony?.UnpatchSelf();
		}
		catch (Exception arg)
		{
			base.Logger.LogWarning($"退出时清理 Workshop 翻译资源失败；进程仍将退出。\n{arg}");
		}
	}

	internal void CloseMenu()
	{
		SetMenuOpen(open: false);
	}

	internal void SetWorkshopLocalizationApiProtocol(string protocol)
	{
		if (WorkshopLocalization != null && !string.IsNullOrWhiteSpace(protocol) && MachineTranslationSettings.SupportedProtocols.Any((string value) => string.Equals(value, protocol, StringComparison.OrdinalIgnoreCase)))
		{
			string apiProtocol = WorkshopLocalization.Settings.ApiProtocol;
			WorkshopLocalization.Settings.ApiProtocol = protocol;
			PersistInterfaceState(_workshopLocalizationApiProtocol, protocol);
			string defaultEndpoint = MachineTranslationSettings.GetDefaultEndpoint(protocol);
			if (string.IsNullOrWhiteSpace(WorkshopLocalization.Settings.Endpoint) || string.Equals(WorkshopLocalization.Settings.Endpoint, MachineTranslationSettings.GetDefaultEndpoint(apiProtocol), StringComparison.OrdinalIgnoreCase))
			{
				SetWorkshopLocalizationEndpoint(defaultEndpoint);
			}
			_ui?.OnLocalizedAssetViewsRefreshed();
		}
	}

	internal void SetWorkshopLocalizationEndpoint(string endpoint)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Endpoint = endpoint?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationEndpoint, WorkshopLocalization.Settings.Endpoint);
		}
	}

	internal void SetWorkshopLocalizationSecretKey(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.SecretKey = value ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationSecretKey, WorkshopLocalization.Settings.SecretKey);
		}
	}

	internal void SetWorkshopLocalizationRegion(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Region = value?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationRegion, WorkshopLocalization.Settings.Region);
		}
	}

	internal void SetWorkshopLocalizationProjectId(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.ProjectId = value?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationProjectId, WorkshopLocalization.Settings.ProjectId);
		}
	}

	internal void SetWorkshopLocalizationApiKey(string apiKey)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.ApiKey = apiKey ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationApiKey, WorkshopLocalization.Settings.ApiKey);
		}
	}

	internal void SetWorkshopLocalizationModel(string model)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Model = model?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationModel, WorkshopLocalization.Settings.Model);
		}
	}

	internal void SetWorkshopLocalizationBatchSize(string value)
	{
		if (WorkshopLocalization != null)
		{
			int batchSize = WorkshopLocalization.Settings.BatchSize;
			batchSize = ((!int.TryParse(value, out var result)) ? MachineTranslationSettings.NormalizeBatchSize(batchSize) : MachineTranslationSettings.NormalizeBatchSize(result));
			WorkshopLocalization.Settings.BatchSize = batchSize;
			PersistInterfaceState(_workshopLocalizationBatchSize, batchSize);
		}
	}

	internal void SetWorkshopLocalizationEnabled(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Enabled = value;
			PersistInterfaceState(_workshopLocalizationEnabled, value);
			_ui?.OnLocalizedAssetViewsRefreshed();
		}
	}

	internal void SetWorkshopLocalizationApplyToNativeUi(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.ApplyToNativeUi = value;
			PersistInterfaceState(_workshopLocalizationApplyToNativeUi, value);
			_ui?.OnLocalizedAssetViewsRefreshed();
		}
	}

	internal void SetWorkshopLocalizationAutoScan(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.AutoScanOnAssetsRefreshed = value;
			PersistInterfaceState(_workshopLocalizationAutoScan, value);
		}
	}

	internal void SetWorkshopLocalizationAutoTranslate(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.AutoTranslateMissing = value;
			PersistInterfaceState(_workshopLocalizationAutoTranslate, value);
			if (value && WorkshopLocalization.ApiConfigured && !WorkshopLocalization.IsTranslationRunning)
			{
				WorkshopLocalization.ScanCurrentAssets();
				_ui?.OnLocalizedAssetViewsRefreshed();
			}
		}
	}

	internal void ScanWorkshopLocalization(bool allowAutoTranslate = true)
	{
		WorkshopLocalization?.ScanCurrentAssets(allowAutoTranslate);
	}

	internal bool TranslateWorkshopLocalization(bool force)
	{
		return WorkshopLocalization?.StartTranslation(force) ?? false;
	}

	internal void CancelWorkshopLocalization()
	{
		WorkshopLocalization?.CancelTranslation();
	}

	internal void ReloadWorkshopLocalizationCache()
	{
		WorkshopLocalization?.ReloadCache();
	}

	internal void SetPointToolEnabled(bool value)
	{
		PointTool.Enabled = value && SingleplayerGuard.IsReady;
		PersistInterfaceState(_pointToolEnabled, PointTool.Enabled);
		if (!PointTool.Enabled)
		{
			_pointToolHud.ClearPointIfCreated();
		}
	}

	internal void SetPointToolMode(PointToolMode value)
	{
		PointTool.Mode = value;
		PersistInterfaceState(_pointToolMode, value.ToString());
	}

	internal void SetPointToolRange(float value)
	{
		PointTool.Range = Mathf.Clamp(value, 5f, 250f);
		PersistInterfaceState(_pointToolRange, PointTool.Range);
	}

	internal void SetPointToolShowTargetName(bool value)
	{
		PointTool.ShowTargetName = value;
		PersistInterfaceState(_pointToolShowTargetName, value);
	}

	internal void SetPointToolShowId(bool value)
	{
		PointTool.ShowId = value;
		PersistInterfaceState(_pointToolShowId, value);
	}

	internal void SetPointToolShowHealth(bool value)
	{
		PointTool.ShowHealth = value;
		PersistInterfaceState(_pointToolShowHealth, value);
	}

	internal void SetFlightSpeed(float value)
	{
		Movement.FlightSpeed = Mathf.Clamp(value, 1f, 10f);
		PersistInterfaceState(_flightSpeed, Movement.FlightSpeed);
	}

	internal void SetFlightVerticalSpeed(float value)
	{
		Movement.VerticalSpeed = Mathf.Clamp(value, 1f, 10f);
		PersistInterfaceState(_flightVerticalSpeed, Movement.VerticalSpeed);
	}

	internal void SetNoclipSafeExit(bool value)
	{
		Movement.SafeExit = value;
		PersistInterfaceState(_noclipSafeExit, value);
	}

	private static string GetWorkshopTranslationCachePath(string targetLanguage)
	{
		string text = (string.IsNullOrWhiteSpace(targetLanguage) ? "zh-CN" : targetLanguage.Trim());
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		return Path.Combine(Paths.CachePath, "UnturnedSingleplayerCheatMenu", "WorkshopTranslations", text + ".json");
	}

	private static PointToolMode ParsePointToolMode(string value)
	{
		if (!Enum.TryParse<PointToolMode>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(PointToolMode), result))
		{
			return PointToolMode.Smart;
		}
		return result;
	}

	internal void RefreshCatalog()
	{
		Catalog.Refresh();
		Icons.Clear();
		WorkshopLocalizationService workshopLocalization = WorkshopLocalization;
		if (workshopLocalization == null || !workshopLocalization.IsTranslationRunning)
		{
			WorkshopLocalizationService workshopLocalization2 = WorkshopLocalization;
			if (workshopLocalization2 == null || workshopLocalization2.SnapshotVersion == 0)
			{
				WorkshopLocalization?.ScanCurrentAssets();
			}
		}
		_lastWorkshopSnapshotVersion = WorkshopLocalization?.SnapshotVersion ?? _lastWorkshopSnapshotVersion;
		_ui.OnCatalogRefreshed();
		base.Logger.LogInfo($"资产扫描完成：物品 {Catalog.Items.Count}（模组 {Catalog.WorkshopItemCount}），车辆 {Catalog.Vehicles.Count}（模组 {Catalog.WorkshopVehicleCount}）。");
	}

	private void SetMenuOpen(bool open)
	{
		if (_menuOpen != open && (!open || SingleplayerGuard.IsReady))
		{
			_menuOpen = open;
			if (open)
			{
				CaptureMenuInput();
			}
			else
			{
				ReleaseMenuInput();
				_ui.OnClosed();
			}
			if (open)
			{
				PluginLocalization.Initialize(_language.Value, Provider.language);
				RefreshCatalog();
				_ui.OnOpened();
			}
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
			Cursor.visible = false;
		}
		SleekWindow window = PlayerUI.window;
		if (window != null)
		{
			if (_capturedWindow != window)
			{
				ReleaseMenuInput();
				_capturedWindow = window;
				_windowWasEnabled = window.isEnabled;
				_windowWasShowingCursor = window.showCursor;
			}
			window.isEnabled = true;
			window.showCursor = true;
		}
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
		{
			RefreshCatalog();
		}
		else
		{
			WorkshopLocalization?.OnAssetsRefreshed();
		}
	}

	private static void GameUpdatePostfix(MethodBase __originalMethod)
	{
		CheatMenuPlugin instance = Instance;
		instance?.RunUpdateCallback("游戏更新 Harmony Postfix");
		if (__originalMethod?.DeclaringType == typeof(PlayerUI) && (object)instance != null && instance._menuOpen && SingleplayerGuard.IsReady)
		{
			instance.MaintainMenuInputCapture();
			instance._ui.Maintain();
		}
	}

	private static void GameOnGuiPostfix()
	{
		Instance?.RunGuiCallback("游戏 OnGUI Harmony Postfix");
	}

	private void InstallRuntimePatches()
	{
		_harmony = new Harmony("com.codex.unturned.singleplayer-cheat-menu");
		MovementSimulationPatch.Install(_harmony, base.Logger);
		InventoryArrangePatch.Install(_harmony, base.Logger, InventoryArrange);
		PointToolInteractionPatch.Install(_harmony, base.Logger);
		WorkshopLocalizationPatch.Install(_harmony, base.Logger);
		PatchUpdateLoop(typeof(Provider));
		PatchUpdateLoop(typeof(MenuUI));
		PatchUpdateLoop(typeof(PlayerUI));
		PatchOnGuiLoop(typeof(MenuUI));
		PatchOnGuiLoop(typeof(PlayerUI));
		try
		{
			MethodInfo methodInfo = (from methodInfo2 in AccessTools.GetDeclaredMethods(typeof(PlayerLife))
				where methodInfo2.Name == "doDamage" && methodInfo2.ReturnType == typeof(void)
				orderby methodInfo2.GetParameters().Length descending
				select methodInfo2).FirstOrDefault();
			if (methodInfo == null)
			{
				throw new MissingMethodException(typeof(PlayerLife).FullName, "doDamage");
			}
			MethodInfo method = AccessTools.Method(typeof(GodModePatch), "Prefix");
			_harmony.Patch(methodInfo, new HarmonyMethod(method));
			base.Logger.LogInfo($"无敌补丁已注册：{methodInfo.DeclaringType?.FullName}.{methodInfo.Name}({methodInfo.GetParameters().Length} 参数)。");
		}
		catch (Exception arg)
		{
			base.Logger.LogError($"无敌补丁注册失败；其他菜单功能仍可使用。\n{arg}");
		}
	}

	private void PatchUpdateLoop(Type targetType)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(targetType, "Update", Type.EmptyTypes);
			if (methodInfo == null)
			{
				throw new MissingMethodException(targetType.FullName, "Update");
			}
			MethodInfo method = AccessTools.Method(typeof(CheatMenuPlugin), "GameUpdatePostfix");
			_harmony.Patch(methodInfo, null, new HarmonyMethod(method));
			base.Logger.LogInfo("更新回调补丁已注册：" + targetType.FullName + ".Update。");
		}
		catch (Exception arg)
		{
			base.Logger.LogWarning($"无法挂接 {targetType.FullName}.Update；将继续尝试其他更新入口。\n{arg}");
		}
	}

	private void PatchOnGuiLoop(Type targetType)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(targetType, "OnGUI", Type.EmptyTypes);
			if (methodInfo == null)
			{
				throw new MissingMethodException(targetType.FullName, "OnGUI");
			}
			MethodInfo method = AccessTools.Method(typeof(CheatMenuPlugin), "GameOnGuiPostfix");
			_harmony.Patch(methodInfo, null, new HarmonyMethod(method));
			base.Logger.LogInfo("绘制回调补丁已注册：" + targetType.FullName + ".OnGUI。");
		}
		catch (Exception arg)
		{
			base.Logger.LogWarning($"无法挂接 {targetType.FullName}.OnGUI；将继续尝试其他绘制入口。\n{arg}");
		}
	}
}
