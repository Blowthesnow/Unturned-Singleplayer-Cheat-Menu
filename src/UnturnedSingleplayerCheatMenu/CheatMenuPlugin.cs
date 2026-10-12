using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.AmmoRefill;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Decomposition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Patches;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu;

[BepInPlugin("com.codex.unturned.singleplayer-cheat-menu", "Unturned Singleplayer Cheat Menu", "2.1.0")]
[BepInProcess("Unturned.exe")]
public sealed class CheatMenuPlugin : BaseUnityPlugin
{
	public const string PluginGuid = "com.codex.unturned.singleplayer-cheat-menu";

	public const string PluginName = "Unturned Singleplayer Cheat Menu";

	public const string PluginVersion = "2.1.0";
	private const int CurrentMinimapSchemaVersion = 5;

	private ConfigEntry<string> _language;

	private ConfigEntry<string> _fontFamily;

	private ConfigEntry<KeyboardShortcut> _toggleShortcut;

	private ConfigEntry<KeyboardShortcut> _infiniteStorageShortcut;

	private ConfigEntry<KeyboardShortcut> _arrangeCurrentShortcut;

	private ConfigEntry<KeyboardShortcut> _arrangeAllCarriedShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleGodModeShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleInfiniteNeedsShortcut;

	private ConfigEntry<KeyboardShortcut> _restoreEverythingShortcut;

	private ConfigEntry<KeyboardShortcut> _refillNeedsShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleFlightShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleNoclipShortcut;

	private ConfigEntry<KeyboardShortcut> _togglePointToolEnabledShortcut;

	private ConfigEntry<KeyboardShortcut> _nextPointToolModeShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleCreatureHealthBarsShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleAutoPickupShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleMinimapShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleInfiniteStorageEnabledShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleFreezeTimeShortcut;

	private ConfigEntry<KeyboardShortcut> _setDayShortcut;

	private ConfigEntry<KeyboardShortcut> _setNightShortcut;

	private ConfigEntry<KeyboardShortcut> _toggleFullMoonShortcut;

	private ConfigEntry<KeyboardShortcut> _clearWeatherShortcut;

	private ConfigEntry<KeyboardShortcut> _disableWeatherShortcut;

	private ConfigEntry<KeyboardShortcut> _callAirdropShortcut;

	private ConfigEntry<string> _inventoryArrangeCustomKeys;

	private ConfigEntry<string> _inventoryArrangePreset;

	private ConfigEntry<bool> _inventoryArrangeAutoRotate;

	private ConfigEntry<bool> _inventoryArrangeCrossPage;

	private ConfigEntry<bool> _inventoryArrangeIncludeBackpackPage;

	private ConfigEntry<bool> _inventoryArrangeIncludeVestPage;

	private ConfigEntry<bool> _inventoryArrangeIncludeShirtPage;

	private ConfigEntry<bool> _inventoryArrangeIncludePantsPage;

	private ConfigEntry<bool> _inventoryArrangeProtectEquipped;

	private ConfigEntry<bool> _inventoryArrangeFixHotkeyItems;

	private ConfigEntry<float> _uiScale;

	private ConfigEntry<int> _pageSize;

	private ConfigEntry<int> _vehicleIconResolution;

	private ConfigEntry<float> _vehicleIconFraming;

	private ConfigEntry<string> _lastMainTab;

	private ConfigEntry<string> _lastTeleportView;

	private ConfigEntry<float> _windowPositionX;

	private ConfigEntry<float> _windowPositionY;

	private ConfigEntry<bool> _pointToolEnabled;

	private ConfigEntry<string> _pointToolMode;

	private ConfigEntry<float> _pointToolRange;

	private ConfigEntry<bool> _pointToolShowTargetName;

	private ConfigEntry<bool> _pointToolShowId;

	private ConfigEntry<bool> _pointToolShowHealth;

	private ConfigEntry<bool> _pointToolShowHud;

	private ConfigEntry<bool> _creatureHealthBarEnabled;

	private ConfigEntry<bool> _autoPickupEnabled;
	private ConfigEntry<bool> _infiniteStorageEnabled;
	private ConfigEntry<bool> _infiniteStoragePickupPriority;
	private ConfigEntry<bool> _infiniteStorageCraftingEnabled;
	private ConfigEntry<bool> _infiniteStorageAmmoRefillEnabled;
	private ConfigEntry<bool> _infiniteStorageDecompositionEnabled;
	private ConfigEntry<bool> _infiniteStorageDestructionWarningAcknowledged;

	private ConfigEntry<float> _autoPickupRange;

	private ConfigEntry<float> _autoPickupFrequencyHz;

	private ConfigEntry<int> _autoPickupRequestsPerTick;

	private ConfigEntry<int> _autoPickupDropBlockMinutes;

	private ConfigEntry<bool> _autoPickupQualityFilterEnabled;

	private ConfigEntry<int> _autoPickupMinimumQuality;

	private ConfigEntry<int> _autoPickupAllowedRarityMask;

	private ConfigEntry<string> _autoPickupFilterMode;

	private ConfigEntry<string> _autoPickupRulesPath;

	private ConfigEntry<bool> _autoPickupPauseWhenMenuOpen;

	private ConfigEntry<bool> _autoPickupPauseWhenDead;

	private ConfigEntry<string> _autoPickupDiagnostics;
	private ConfigEntry<bool> _autoPickupUseMinimapRange;
	private ConfigEntry<bool> _autoPickupUseMinimapRarityFilter;

	private ConfigEntry<int> _minimapSchemaVersion;
	private ConfigEntry<bool> _minimapEnabled;
	private ConfigEntry<string> _minimapShape;
	private ConfigEntry<string> _minimapAnchor;
	private ConfigEntry<float> _minimapOffsetX;
	private ConfigEntry<float> _minimapOffsetY;
	private ConfigEntry<float> _minimapSize;
	private ConfigEntry<float> _minimapVisibleRadius;
	private ConfigEntry<string> _minimapBackgroundMode;
	private ConfigEntry<float> _minimapBackgroundRenderHz;
	private ConfigEntry<float> _minimapWorldScanHz;
	private ConfigEntry<bool> _minimapShowLoadedBuildings;
	private ConfigEntry<bool> _minimapRotateWithView;
	private ConfigEntry<float> _minimapOcclusionProbeHz;
	private ConfigEntry<float> _minimapOcclusionEnterThickness;
	private ConfigEntry<float> _minimapOcclusionExitThickness;
	private ConfigEntry<float> _minimapOcclusionEnterDelay;
	private ConfigEntry<float> _minimapOcclusionExitDelay;
	private ConfigEntry<float> _minimapOcclusionMergeGap;
	private ConfigEntry<float> _minimapFloorExclusionMaxThickness;
	private ConfigEntry<bool> _minimapShowCompass;
	private ConfigEntry<bool> _minimapShowCoordinates;
	private ConfigEntry<bool> _minimapShowNativeMapMarkers;
	private ConfigEntry<bool> _minimapShowTeleportMarkers;
	private ConfigEntry<bool> _minimapShowZombies;
	private ConfigEntry<bool> _minimapShowAnimals;
	private ConfigEntry<bool> _minimapShowPlayers;
	private ConfigEntry<bool> _minimapShowItems;
	private ConfigEntry<string> _minimapItemRarityMask;
	private ConfigEntry<float> _minimapItemMergePixels;
	private ConfigEntry<float> _minimapUndergroundSliceAbove;
	private ConfigEntry<float> _minimapUndergroundSliceBelow;
	private ConfigEntry<float> _minimapSameFloorTolerance;
	private ConfigEntry<bool> _minimapShowOtherFloors;
	private ConfigEntry<bool> _minimapLayoutEditMode;
	private bool _minimapConfigurationWarningLogged;

	private ConfigEntry<float> _flightSpeed;

	private ConfigEntry<float> _flightVerticalSpeed;

	private ConfigEntry<string> _flightMode;

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

	private PluginUiRoot _uiRoot;

	private PluginRuntimeContext _runtimeContext;
	private readonly MultiplayerRiskAcknowledgement _multiplayerRiskAcknowledgement = new MultiplayerRiskAcknowledgement();
	private RuntimeSessionState _runtimeSession = new RuntimeSessionState(RuntimeSessionKind.Frontend, string.Empty, false);

	private VehicleIconRenderer _vehicleIconRenderer;

	private PointToolHud _pointToolHud;

	private CreatureHealthBarHud _creatureHealthBarHud;
	private NearbyItemIndex _nearbyItemIndex;

	private readonly UiInputCaptureController _inputCapture = new UiInputCaptureController();

	private GlobalShortcutManager _shortcutManager;

	private bool _menuOpen;

	private bool _infiniteStorageOpen;

	private bool _hasLoggedVehicleIconFallback;

	private bool _hasLoggedVehicleIconRequest;

	private bool _hasLoggedVehicleIconResult;

	private bool _hasLoggedVehicleIconCache;

	private bool _hasLoggedVehicleIconBound;

	private bool _hasLoggedVehicleIconCacheWriteFailure;

	private static readonly PointToolMode[] ShortcutPointToolModes = new PointToolMode[6]
	{
		PointToolMode.Smart,
		PointToolMode.Inspect,
		PointToolMode.Repair,
		PointToolMode.Teleport,
		PointToolMode.Utility,
		PointToolMode.Delete
	};

	internal static CheatMenuPlugin Instance { get; private set; }
	internal RuntimeSessionState CurrentSessionState => _runtimeSession;
	internal bool IsMultiplayerRiskAcknowledged => _multiplayerRiskAcknowledgement.IsAccepted;
	internal GameplayFeatureAccess CurrentGameplayAccess =>
		GameplayFeatureAccessPolicy.Evaluate(_runtimeSession, _multiplayerRiskAcknowledgement.IsAccepted);
	internal bool CanExecuteGameplay
	{
		get
		{
			ObserveRuntimeSession();
			return GameplayFeatureAccessPolicy.IsAllowed(CurrentGameplayAccess);
		}
	}
	internal bool CanOpenClientMenu => _runtimeSession.CanOpenClientMenu;
	internal bool CanUseSnapshotStorage => _runtimeSession.CanUseSnapshotStorage;

	internal ManualLogSource PluginLogger => Logger;

	internal AssetCatalog Catalog { get; private set; }

	internal IconCache Icons { get; private set; }

	internal FavoriteStore Favorites { get; private set; }

	internal TeleportStore Teleports { get; private set; }

	internal TeleportMapService TeleportMaps { get; private set; }

	internal CheatActions Actions { get; private set; }

	internal WorldMaintenanceService WorldMaintenance { get; private set; }

	internal MovementCheatService Movement { get; private set; }

	internal PointToolService PointTool { get; private set; }

	internal InventoryArrangeFeature InventoryArrange { get; private set; }

	internal AutoPickupFeature AutoPickup { get; private set; }

	internal MinimapFeature Minimap { get; private set; }

	internal SnapshotInfiniteStorageRuntime InfiniteStorage { get; private set; }
	private SnapshotModuleRegistry _snapshotStorageModules;
	private SnapshotAmmoRefillModule _snapshotAmmoRefillModule;

	internal CreatureHealthBarService CreatureHealthBars { get; private set; }

	internal float UiScale => Mathf.Clamp(_uiScale.Value, 0.75f, 1.5f);

	internal int PageSize => Mathf.Clamp(_pageSize.Value, 12, 80);

	internal string ShortcutLabel => GetShortcutLabel("menu.toggle");

	internal bool IsMenuOpen => _menuOpen;
	internal bool IsMinimapLayoutEditActive => CanExecuteGameplay && (Minimap?.Settings.LayoutEditMode ?? false);

	internal bool IsInfiniteStorageOpen => _infiniteStorageOpen;

	internal bool IsInfiniteStoragePickupPriorityEnabled => _infiniteStoragePickupPriority?.Value == true;
	internal bool IsInfiniteStorageDestructionWarningAcknowledged =>
		_infiniteStorageDestructionWarningAcknowledged?.Value == true;

	internal string LastMainTab => _lastMainTab?.Value ?? "Character";

	internal string LastTeleportView => _lastTeleportView?.Value ?? "Map";

	internal Vector2 NormalizedWindowPosition => new Vector2(_windowPositionX?.Value ?? 0f, _windowPositionY?.Value ?? 0f);

	internal string SelectedFontFamily => SystemFontProvider.SelectedSystemFont;

	internal VehicleThumbnailRenderSettings VehicleThumbnailSettings { get; private set; }

	internal WorkshopLocalizationService WorkshopLocalization { get; private set; }

	internal string InventoryArrangeCustomKeys => _inventoryArrangeCustomKeys?.Value ?? "Type:Ascending,Area:Descending,Name:Ascending";

	internal IReadOnlyList<SystemFontFamilyOption> GetInstalledChineseFontFamilies()
	{
		return SystemFontProvider.GetInstalledChineseFontFamilies(base.Logger);
	}

	internal bool TrySetFontFamily(string fontFamily, out bool persisted, out string error)
	{
		persisted = false;
		if (!SystemFontProvider.TrySelectSystemFont(fontFamily, base.Logger, out error))
		{
			return false;
		}
		persisted = PersistInterfaceState(_fontFamily, SystemFontProvider.SelectedSystemFont);
		if (!persisted)
		{
			error = "界面字体已切换，但配置文件保存失败。";
		}
		return true;
	}

	internal bool SetLastMainTab(string value)
	{
		return PersistInterfaceState(_lastMainTab, value);
	}

	internal bool SetLastTeleportView(string value)
	{
		return PersistInterfaceState(_lastTeleportView, value);
	}

	internal void SetWindowPosition(Vector2 normalizedPosition)
	{
		if (_windowPositionX == null || _windowPositionY == null)
		{
			return;
		}
		normalizedPosition.x = float.IsNaN(normalizedPosition.x) || float.IsInfinity(normalizedPosition.x) ? 0f : Mathf.Clamp(normalizedPosition.x, -1f, 1f);
		normalizedPosition.y = float.IsNaN(normalizedPosition.y) || float.IsInfinity(normalizedPosition.y) ? 0f : Mathf.Clamp(normalizedPosition.y, -1f, 1f);
		bool saveOnConfigSet = base.Config.SaveOnConfigSet;
		try
		{
			base.Config.SaveOnConfigSet = false;
			_windowPositionX.Value = normalizedPosition.x;
			_windowPositionY.Value = normalizedPosition.y;
			base.Config.Save();
		}
		catch (Exception exception)
		{
			base.Logger.LogWarning($"窗口位置已在本次运行中更新，但配置文件保存失败。\n{exception}");
		}
		finally
		{
			base.Config.SaveOnConfigSet = saveOnConfigSet;
		}
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
		}
	}

	private void NormalizeConfiguredInventoryShortcut(ConfigEntry<KeyboardShortcut> entry)
	{
		if (entry == null || ShortcutInputPolicy.IsAllowedShortcut(entry.Value))
		{
			return;
		}
		entry.Value = ((entry == _arrangeAllCarriedShortcut) ? new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift) : new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl));
	}

	private void HandleArrangeCurrentShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess("inventory.arrange.current", inputSource)) return;
		InventoryArrangeResult obj = InventoryArrange?.Execute(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, inputSource);
		base.Logger.LogInfo(obj?.Message ?? "Inventory arrange is not ready.");
	}

	private void HandleArrangeAllCarriedShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess("inventory.arrange.all-carried", inputSource)) return;
		InventoryArrangeResult obj = InventoryArrange?.Execute(InventoryArrangeScope.AllCarried, InventoryArrangeAction.Arrange, inputSource);
		base.Logger.LogInfo(obj?.Message ?? "Inventory arrange is not ready.");
	}

	internal bool ObserveRuntimeSession()
	{
		RuntimeSessionState state = RuntimeSessionBoundary.CaptureCurrent();
		_runtimeSession = state;
		bool changed = _multiplayerRiskAcknowledgement.Observe(RuntimeSessionBoundary.CaptureConnectionKey(state));
		if (changed)
		{
			_ui?.OnSessionStateChanged();
		}
		return changed;
	}

	internal bool EnsureGameplayAccess(string actionId, string inputSource)
	{
		ObserveRuntimeSession();
		GameplayFeatureAccess access = CurrentGameplayAccess;
		if (GameplayFeatureAccessPolicy.IsAllowed(access))
			return true;
		if (access == GameplayFeatureAccess.RequiresMultiplayerAcknowledgement)
		{
			base.Logger.LogWarning($"全局快捷键 {actionId} ({inputSource}) 等待本次多人会话风险确认。");
			_ui?.ShowMultiplayerRiskConfirmation();
			return false;
		}
		string text = GetGameplayAccessFailure(access);
		base.Logger.LogWarning($"全局快捷键 {actionId} ({inputSource}) 被拒绝：{text}");
		_ui?.ShowGlobalShortcutStatus(text, warning: true);
		return false;
	}

	internal void AcknowledgeMultiplayerRisk()
	{
		ObserveRuntimeSession();
		if (_runtimeSession.IsUnsupportedClientSession)
		{
			_multiplayerRiskAcknowledgement.AcceptCurrent();
			_ui?.OnSessionStateChanged();
			base.Logger.LogWarning("Multiplayer gameplay risk acknowledged for this connection only; gameplay remains unsupported.");
		}
	}

	private static string GetGameplayAccessFailure(GameplayFeatureAccess access)
	{
		return access switch
		{
			GameplayFeatureAccess.DeniedNotReady => PluginLocalization.Translate("本地玩家尚未生成。"),
			GameplayFeatureAccess.DeniedDedicatedServer => PluginLocalization.Translate("独立专用服务器不运行客户端玩法功能。"),
			GameplayFeatureAccess.DeniedUnsupportedSession => PluginLocalization.Translate("当前会话不支持玩法功能。"),
			_ => PluginLocalization.Translate("无法识别当前会话，玩法功能已停止。")
		};
	}

	private void ReportShortcut(string actionId, string inputSource, string message, bool warning = false)
	{
		base.Logger.LogInfo($"全局快捷键 {actionId} ({inputSource})：{message}");
		_ui?.ShowGlobalShortcutStatus(message, warning);
	}

	private void HandleGodModeShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleGodMode, inputSource)) return;
		Actions.SetGodMode(!Actions.GodModeEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleGodMode, inputSource, Actions.GodModeEnabled ? "无敌模式已开启。" : "无敌模式已关闭。");
	}

	private void HandleInfiniteNeedsShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleInfiniteNeeds, inputSource)) return;
		Actions.SetInfiniteNeeds(!Actions.InfiniteNeedsEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleInfiniteNeeds, inputSource, Actions.InfiniteNeedsEnabled ? "无限生存状态已开启。" : "无限生存状态已关闭。");
	}

	private void HandleRestoreEverythingShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.RestoreEverything, inputSource)) return;
		bool result = Actions.RestoreEverything();
		ReportShortcut(GlobalShortcutActions.RestoreEverything, inputSource, result ? "角色状态已全部恢复。" : "恢复失败。", !result);
	}

	private void HandleRefillNeedsShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.RefillNeeds, inputSource)) return;
		bool result = Actions.RefillNeeds();
		ReportShortcut(GlobalShortcutActions.RefillNeeds, inputSource, result ? "生存状态已补满。" : "补满失败。", !result);
	}

	private void HandleFlightShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleFlight, inputSource)) return;
		Movement.SetFlight(!Movement.FlightEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleFlight, inputSource, Movement.FlightEnabled ? "飞行已开启。" : "飞行已关闭。");
	}

	private void HandleNoclipShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleNoclip, inputSource)) return;
		Movement.SetNoclip(!Movement.NoclipEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleNoclip, inputSource, Movement.NoclipEnabled ? "穿墙已开启。" : "穿墙已关闭。");
	}

	private void HandlePointToolEnabledShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.TogglePointToolEnabled, inputSource)) return;
		SetPointToolEnabled(!PointTool.Enabled);
		ReportShortcut(GlobalShortcutActions.TogglePointToolEnabled, inputSource, PointTool.Enabled ? "准星工具已开启。" : "准星工具已关闭。");
	}

	private void HandleNextPointToolModeShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.NextPointToolMode, inputSource)) return;
		int index = Array.IndexOf(ShortcutPointToolModes, PointTool.Mode);
		PointToolMode nextMode = ShortcutPointToolModes[(index + 1 + ShortcutPointToolModes.Length) % ShortcutPointToolModes.Length];
		SetPointToolMode(nextMode);
		ReportShortcut(GlobalShortcutActions.NextPointToolMode, inputSource, "准星工具模式已切换为 " + nextMode + "。");
	}

	private void HandleCreatureHealthBarsShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleCreatureHealthBars, inputSource)) return;
		SetCreatureHealthBarEnabled(!CreatureHealthBarEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleCreatureHealthBars, inputSource, CreatureHealthBarEnabled ? "生物生命条已开启。" : "生物生命条已关闭。");
	}

	private void HandleAutoPickupShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleAutoPickup, inputSource)) return;
		SetAutoPickupEnabled(!(_autoPickupEnabled?.Value ?? false));
		ReportShortcut(GlobalShortcutActions.ToggleAutoPickup, inputSource, (_autoPickupEnabled?.Value ?? false) ? "自动拾取已开启。" : "自动拾取已关闭。");
	}

	private void HandleMinimapToggleShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleMinimap, inputSource)) return;
		bool enabled = false;
		UpdateMinimapSettings(settings =>
		{
			settings.Enabled = !settings.Enabled;
			if (!settings.Enabled) settings.LayoutEditMode = false;
			enabled = settings.Enabled;
		});
		ReportShortcut(GlobalShortcutActions.ToggleMinimap, inputSource,
			PluginLocalization.Translate(enabled ? "小地图已开启。" : "小地图已关闭。"));
	}

	private void HandleMinimapZoomInShortcut(string inputSource, float detents)
	{
		AdjustMinimapZoom(inputSource, MinimapZoomDirection.In, detents);
	}

	private void HandleMinimapZoomOutShortcut(string inputSource, float detents)
	{
		AdjustMinimapZoom(inputSource, MinimapZoomDirection.Out, detents);
	}

	private void AdjustMinimapZoom(string inputSource, MinimapZoomDirection direction, float detents)
	{
		string actionId = direction == MinimapZoomDirection.In
			? GlobalShortcutActions.ZoomMinimapIn
			: GlobalShortcutActions.ZoomMinimapOut;
		if (!EnsureGameplayAccess(actionId, inputSource)) return;
		float radius = 100f;
		UpdateMinimapSettings(settings =>
		{
			settings.VisibleRadiusMeters = MinimapZoomPolicy.Apply(settings.VisibleRadiusMeters, direction, detents);
			radius = settings.VisibleRadiusMeters;
		});
		string template = direction == MinimapZoomDirection.In
			? "小地图已放大，可见半径 {0:0} 米。"
			: "小地图已缩小，可见半径 {0:0} 米。";
		ReportShortcut(actionId, inputSource, string.Format(PluginLocalization.Translate(template), radius));
	}

	private void HandleInfiniteStorageEnabledShortcut(string inputSource)
	{
		ObserveRuntimeSession();
		if (!CanUseSnapshotStorage)
		{
			ReportShortcut(GlobalShortcutActions.ToggleInfiniteStorageEnabled, inputSource,
				PluginLocalization.Translate("Snapshot 无限储存仅在真正的单人世界中可用。"), warning: true);
			return;
		}
		SetInfiniteStorageEnabled(!(_infiniteStorageEnabled?.Value ?? false));
		ReportShortcut(GlobalShortcutActions.ToggleInfiniteStorageEnabled, inputSource, (_infiniteStorageEnabled?.Value ?? false) ? "无限储存功能已开启。" : "无限储存功能已关闭。");
	}

	private void HandleFreezeTimeShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleFreezeTime, inputSource)) return;
		Actions.SetFreezeTime(!Actions.FreezeTimeEnabled);
		ReportShortcut(GlobalShortcutActions.ToggleFreezeTime, inputSource, Actions.FreezeTimeEnabled ? "时间已冻结。" : "时间已恢复流动。");
	}

	private void HandleSetDayShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.SetDay, inputSource)) return;
		Actions.SetDay();
		ReportShortcut(GlobalShortcutActions.SetDay, inputSource, "已切换到白天。");
	}

	private void HandleSetNightShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.SetNight, inputSource)) return;
		Actions.SetNight();
		ReportShortcut(GlobalShortcutActions.SetNight, inputSource, "已切换到夜晚。");
	}

	private void HandleFullMoonShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ToggleFullMoon, inputSource)) return;
		Actions.SetFullMoon(!LightingManager.isFullMoon);
		ReportShortcut(GlobalShortcutActions.ToggleFullMoon, inputSource, LightingManager.isFullMoon ? "已开启满月。" : "已关闭满月。");
	}

	private void HandleClearWeatherShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.ClearWeather, inputSource)) return;
		Actions.ClearWeather();
		ReportShortcut(GlobalShortcutActions.ClearWeather, inputSource, "当前天气已清除，后续仍可自然调度。");
	}

	private void HandleDisableWeatherShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.DisableWeather, inputSource)) return;
		Actions.DisableWeather();
		ReportShortcut(GlobalShortcutActions.DisableWeather, inputSource, "当前天气和自然天气调度已关闭。", warning: true);
	}

	private void HandleCallAirdropShortcut(string inputSource)
	{
		if (!EnsureGameplayAccess(GlobalShortcutActions.CallAirdrop, inputSource)) return;
		bool result = Actions.CallAirdrop();
		ReportShortcut(GlobalShortcutActions.CallAirdrop, inputSource, result ? "空投已呼叫。" : "空投呼叫失败。", !result);
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
			IncludeBackpackPage = (_inventoryArrangeIncludeBackpackPage?.Value ?? true),
			IncludeVestPage = (_inventoryArrangeIncludeVestPage?.Value ?? true),
			IncludeShirtPage = (_inventoryArrangeIncludeShirtPage?.Value ?? true),
			IncludePantsPage = (_inventoryArrangeIncludePantsPage?.Value ?? true),
			ProtectEquipped = (_inventoryArrangeProtectEquipped?.Value ?? true),
			CustomKeys = (keys ?? new List<InventorySortKeySpec>()),
			FixHotkeyItems = (_inventoryArrangeFixHotkeyItems?.Value ?? true)
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

	private void NormalizeConfiguredInfiniteStorageShortcut()
	{
		if (_infiniteStorageShortcut == null || ShortcutInputPolicy.IsAllowedShortcut(_infiniteStorageShortcut.Value))
		{
			return;
		}
		_infiniteStorageShortcut.Value = new KeyboardShortcut(KeyCode.Keypad0, KeyCode.LeftControl);
	}

	internal void SetInventoryArrangeIncludeBackpackPage(bool value)
	{
		PersistInterfaceState(_inventoryArrangeIncludeBackpackPage, value);
	}

	internal void SetInventoryArrangeIncludeVestPage(bool value)
	{
		PersistInterfaceState(_inventoryArrangeIncludeVestPage, value);
	}

	internal void SetInventoryArrangeIncludeShirtPage(bool value)
	{
		PersistInterfaceState(_inventoryArrangeIncludeShirtPage, value);
	}

	internal void SetInventoryArrangeIncludePantsPage(bool value)
	{
		PersistInterfaceState(_inventoryArrangeIncludePantsPage, value);
	}

	internal void SetInventoryArrangeProtectEquipped(bool value)
	{
		PersistInterfaceState(_inventoryArrangeProtectEquipped, value);
	}

	internal void SetInventoryArrangeFixHotkeyItems(bool value)
	{
		PersistInterfaceState(_inventoryArrangeFixHotkeyItems, value);
	}

	internal KeyboardShortcut GetShortcut(string actionId)
	{
		return _shortcutManager?.GetShortcut(actionId) ?? new KeyboardShortcut(KeyCode.End);
	}

	internal string GetShortcutLabel(string actionId)
	{
		if (_shortcutManager?.IsFixedGesture(actionId) ?? false)
		{
			return _shortcutManager.GetLabel(actionId);
		}
		KeyboardShortcut shortcut = GetShortcut(actionId);
		if (shortcut.MainKey == KeyCode.None)
		{
			return PluginLocalization.CurrentLanguage == PluginLanguage.English ? "Unbound" : "未设置";
		}
		return _shortcutManager?.GetLabel(actionId) ?? shortcut.ToString();
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
		return _shortcutManager?.GetDefaultShortcut(actionId) ?? new KeyboardShortcut(KeyCode.End);
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

	private void Awake()
	{
		Instance = this;
		if (Dedicator.isStandaloneDedicatedServer)
		{
			base.Logger.LogWarning("Client UI/gameplay features are inactive on a standalone dedicated server.");
			return;
		}
		_runtimeContext = new PluginRuntimeContext(this, base.Logger, _inputCapture);
		try
		{
			Initialize();
		}
		catch (Exception exception)
		{
			base.Logger.LogError($"插件初始化失败，正在执行统一回滚。\n{exception}");
			TransferRuntimeOwnership();
			Teardown("Awake initialization failed");
			throw;
		}
	}

	private void Initialize()
	{
		PluginConfigInitializationTransaction.Run(base.Config, base.Logger, InitializeProtected);
	}

	private void InitializeProtected()
	{
		_language = base.Config.Bind("Interface", "Language", "Auto", "UI language. Auto follows Unturned; supported values: Auto, English, Chinese. / 界面语言：Auto 跟随 Unturned，也可填写 English 或 Chinese。");
		PluginLocalization.Initialize(_language.Value, Provider.language);
		_fontFamily = base.Config.Bind("Interface", "FontFamily", "Microsoft YaHei UI", "Installed system font used by the plugin UI. Only Chinese-capable fonts can be selected. / 插件界面使用的系统字体；只能选择支持中文的字体。");
		SystemFontProvider.ConfigurePreferredFamily(_fontFamily.Value, base.Logger);
		_toggleShortcut = base.Config.Bind("General", "ToggleShortcut", new KeyboardShortcut(KeyCode.End), PluginLocalization.Translate("打开或关闭菜单。默认 End；本地玩家生成后可用。"));
		NormalizeConfiguredToggleShortcut();
		_shortcutManager = new GlobalShortcutManager(base.Logger);
		_infiniteStorageShortcut = base.Config.Bind("General", "InfiniteStorageShortcut", new KeyboardShortcut(KeyCode.Keypad0, KeyCode.LeftControl), "打开或关闭独立的无限储存页面。默认左 Ctrl + 数字小键盘 0。");
		NormalizeConfiguredInfiniteStorageShortcut();
		_shortcutManager.Register("infinite-storage.toggle", "无限储存", _infiniteStorageShortcut, new KeyboardShortcut(KeyCode.Keypad0, KeyCode.LeftControl), HandleInfiniteStorageShortcut);
		_shortcutManager.Register("menu.toggle", "打开或关闭菜单", _toggleShortcut, new KeyboardShortcut(KeyCode.End), HandleToggleShortcut);
		_arrangeCurrentShortcut = base.Config.Bind("General", "ArrangeCurrentShortcut", new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl), "整理当前容器；默认 Ctrl+I。单人受支持，多人需按本次连接确认且不受支持。");
		_arrangeAllCarriedShortcut = base.Config.Bind("General", "ArrangeAllCarriedShortcut", new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift), "整理全部随身容器；默认 Ctrl+Shift+I。单人受支持，多人需按本次连接确认且不受支持。");
		_inventoryArrangeCustomKeys = base.Config.Bind("InventoryArrange", "CustomKeys", "Type:Ascending,Area:Descending,Name:Ascending", "自定义排序链，最多 5 项，例如 Type:Ascending,Quality:Descending,Area:Descending。");
		_inventoryArrangePreset = base.Config.Bind("InventoryArrange", "Preset", "Compatibility", "Compatibility, Compact, Type, Name, LargeFirst, SmallFirst, Quality, Rarity, StackFill, Ammo, Hotkey, LowMovement, Custom.");
		_inventoryArrangeAutoRotate = base.Config.Bind("InventoryArrange", "AutoRotate", defaultValue: true, "允许整理时自动旋转物品。");
		_inventoryArrangeCrossPage = base.Config.Bind("InventoryArrange", "AllowCrossPage", defaultValue: true, "允许在整理范围内跨随身页安置。");
		_inventoryArrangeIncludeBackpackPage = base.Config.Bind("InventoryArrange", "IncludeBackpackPage", defaultValue: true, "整理时包含背包页。");
		_inventoryArrangeIncludeVestPage = base.Config.Bind("InventoryArrange", "IncludeVestPage", defaultValue: true, "整理时包含背心页。");
		_inventoryArrangeIncludeShirtPage = base.Config.Bind("InventoryArrange", "IncludeShirtPage", defaultValue: true, "整理时包含上衣页。");
		_inventoryArrangeIncludePantsPage = base.Config.Bind("InventoryArrange", "IncludePantsPage", defaultValue: true, "整理时包含裤子页。");
		_inventoryArrangeProtectEquipped = base.Config.Bind("InventoryArrange", "ProtectEquipped", defaultValue: true, "保护当前装备物品。");
		ConfigDefinition configDefinition = new ConfigDefinition("InventoryArrange", "FixHotkeyItems");
		_inventoryArrangeFixHotkeyItems = base.Config.Bind(configDefinition, defaultValue: true, new ConfigDescription("固定热键绑定物品；关闭时整理会更新热键坐标。"));
		NormalizeConfiguredInventoryShortcut(_arrangeCurrentShortcut);
		NormalizeConfiguredInventoryShortcut(_arrangeAllCarriedShortcut);
		_shortcutManager.Register("inventory.arrange.current", "整理当前容器", _arrangeCurrentShortcut, new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl), HandleArrangeCurrentShortcut);
		_shortcutManager.Register("inventory.arrange.all-carried", "整理全部随身容器", _arrangeAllCarriedShortcut, new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl, KeyCode.LeftShift), HandleArrangeAllCarriedShortcut);
		_toggleGodModeShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleGodMode, new KeyboardShortcut(KeyCode.None), "Toggle god mode.");
		_toggleInfiniteNeedsShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleInfiniteNeeds, new KeyboardShortcut(KeyCode.None), "Toggle infinite needs.");
		_restoreEverythingShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.RestoreEverything, new KeyboardShortcut(KeyCode.None), "Restore health and injuries.");
		_refillNeedsShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.RefillNeeds, new KeyboardShortcut(KeyCode.None), "Refill survival needs.");
		_toggleFlightShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleFlight, new KeyboardShortcut(KeyCode.None), "Toggle flight.");
		_toggleNoclipShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleNoclip, new KeyboardShortcut(KeyCode.None), "Toggle noclip.");
		_togglePointToolEnabledShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.TogglePointToolEnabled, new KeyboardShortcut(KeyCode.None), "Toggle point tool.");
		_nextPointToolModeShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.NextPointToolMode, new KeyboardShortcut(KeyCode.None), "Cycle point tool mode.");
		_toggleCreatureHealthBarsShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleCreatureHealthBars, new KeyboardShortcut(KeyCode.None), "Toggle creature health bars.");
		_toggleAutoPickupShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleAutoPickup, new KeyboardShortcut(KeyCode.None), "Toggle automatic pickup.");
		_toggleMinimapShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleMinimap, new KeyboardShortcut(KeyCode.None), "Toggle the minimap HUD.");
		_toggleInfiniteStorageEnabledShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleInfiniteStorageEnabled, new KeyboardShortcut(KeyCode.None), "Toggle infinite storage feature.");
		_toggleFreezeTimeShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleFreezeTime, new KeyboardShortcut(KeyCode.None), "Toggle frozen time.");
		_setDayShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.SetDay, new KeyboardShortcut(KeyCode.None), "Set daytime.");
		_setNightShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.SetNight, new KeyboardShortcut(KeyCode.None), "Set nighttime.");
		_toggleFullMoonShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ToggleFullMoon, new KeyboardShortcut(KeyCode.None), "Toggle full moon.");
		_clearWeatherShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.ClearWeather, new KeyboardShortcut(KeyCode.None), "Clear current weather.");
		_disableWeatherShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.DisableWeather, new KeyboardShortcut(KeyCode.None), "Disable weather scheduling.");
		_callAirdropShortcut = base.Config.Bind("Shortcuts", GlobalShortcutActions.CallAirdrop, new KeyboardShortcut(KeyCode.None), "Call an airdrop.");
		_shortcutManager.Register(GlobalShortcutActions.ToggleGodMode, "无敌模式", _toggleGodModeShortcut, new KeyboardShortcut(KeyCode.None), HandleGodModeShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleInfiniteNeeds, "无限生存状态", _toggleInfiniteNeedsShortcut, new KeyboardShortcut(KeyCode.None), HandleInfiniteNeedsShortcut);
		_shortcutManager.Register(GlobalShortcutActions.RestoreEverything, "恢复角色状态", _restoreEverythingShortcut, new KeyboardShortcut(KeyCode.None), HandleRestoreEverythingShortcut);
		_shortcutManager.Register(GlobalShortcutActions.RefillNeeds, "补满生存状态", _refillNeedsShortcut, new KeyboardShortcut(KeyCode.None), HandleRefillNeedsShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleFlight, "飞行", _toggleFlightShortcut, new KeyboardShortcut(KeyCode.None), HandleFlightShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleNoclip, "穿墙", _toggleNoclipShortcut, new KeyboardShortcut(KeyCode.None), HandleNoclipShortcut);
		_shortcutManager.Register(GlobalShortcutActions.TogglePointToolEnabled, "准星工具", _togglePointToolEnabledShortcut, new KeyboardShortcut(KeyCode.None), HandlePointToolEnabledShortcut);
		_shortcutManager.Register(GlobalShortcutActions.NextPointToolMode, "准星工具模式", _nextPointToolModeShortcut, new KeyboardShortcut(KeyCode.None), HandleNextPointToolModeShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleCreatureHealthBars, "生物生命条", _toggleCreatureHealthBarsShortcut, new KeyboardShortcut(KeyCode.None), HandleCreatureHealthBarsShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleAutoPickup, "自动拾取", _toggleAutoPickupShortcut, new KeyboardShortcut(KeyCode.None), HandleAutoPickupShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleMinimap, "小地图开关", _toggleMinimapShortcut, new KeyboardShortcut(KeyCode.None), HandleMinimapToggleShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleInfiniteStorageEnabled, "无限储存功能", _toggleInfiniteStorageEnabledShortcut, new KeyboardShortcut(KeyCode.None), HandleInfiniteStorageEnabledShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleFreezeTime, "冻结时间", _toggleFreezeTimeShortcut, new KeyboardShortcut(KeyCode.None), HandleFreezeTimeShortcut);
		_shortcutManager.Register(GlobalShortcutActions.SetDay, "切换为白天", _setDayShortcut, new KeyboardShortcut(KeyCode.None), HandleSetDayShortcut);
		_shortcutManager.Register(GlobalShortcutActions.SetNight, "切换为夜晚", _setNightShortcut, new KeyboardShortcut(KeyCode.None), HandleSetNightShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ToggleFullMoon, "强制满月", _toggleFullMoonShortcut, new KeyboardShortcut(KeyCode.None), HandleFullMoonShortcut);
		_shortcutManager.Register(GlobalShortcutActions.ClearWeather, "清除当前天气", _clearWeatherShortcut, new KeyboardShortcut(KeyCode.None), HandleClearWeatherShortcut);
		_shortcutManager.Register(GlobalShortcutActions.DisableWeather, "关闭天气调度", _disableWeatherShortcut, new KeyboardShortcut(KeyCode.None), HandleDisableWeatherShortcut);
		_shortcutManager.Register(GlobalShortcutActions.CallAirdrop, "呼叫空投", _callAirdropShortcut, new KeyboardShortcut(KeyCode.None), HandleCallAirdropShortcut);
		_shortcutManager.RegisterMouseWheel(GlobalShortcutActions.ZoomMinimapIn, "小地图放大",
			ShortcutMouseWheelDirection.Up, KeyCode.LeftAlt, "Alt + 鼠标滚轮上", HandleMinimapZoomInShortcut);
		_shortcutManager.RegisterMouseWheel(GlobalShortcutActions.ZoomMinimapOut, "小地图缩小",
			ShortcutMouseWheelDirection.Down, KeyCode.LeftAlt, "Alt + 鼠标滚轮下", HandleMinimapZoomOutShortcut);
		_uiScale = base.Config.Bind("Interface", "Scale", 1f, PluginLocalization.Translate("菜单缩放，范围 0.75 到 1.5。"));
		_pageSize = base.Config.Bind("Interface", "CardsPerPage", 32, PluginLocalization.Translate("物品/车辆每页卡片数量，范围 12 到 80。"));
		_vehicleIconResolution = base.Config.Bind("Interface", "VehicleIconResolution", 128, PluginLocalization.Translate("车辆缩略图宽度：128、192 或 256；高度自动使用 4:3 比例。"));
		_vehicleIconFraming = base.Config.Bind("Interface", "VehicleIconFraming", 1f, PluginLocalization.Translate("自动取景倍率，范围 0.5 到 1.5，默认 1.0。"));
		_lastMainTab = base.Config.Bind("Interface", "LastMainTab", "Character", "Last selected main menu tab.");
		_lastTeleportView = base.Config.Bind("Interface", "LastTeleportView", "Map", "Last selected teleport subview: Map or Points.");
		ConfigDefinition normalizedWindowXDefinition = new ConfigDefinition("Interface", "WindowPositionNormalizedX");
		ConfigDefinition normalizedWindowYDefinition = new ConfigDefinition("Interface", "WindowPositionNormalizedY");
		_windowPositionX = base.Config.Bind(normalizedWindowXDefinition, 0f, new ConfigDescription("Normalized main window horizontal position, clamped to -1..1."));
		_windowPositionY = base.Config.Bind(normalizedWindowYDefinition, 0f, new ConfigDescription("Normalized main window vertical position, clamped to -1..1."));
		_pointToolEnabled = base.Config.Bind("PointTool", "Enabled", defaultValue: false, "Enable the crosshair interaction tool.");
		_pointToolMode = base.Config.Bind("PointTool", "Mode", "Smart", "Inspect, Repair, Teleport, Utility, Delete, or Smart.");
		_pointToolRange = base.Config.Bind("PointTool", "Range", 100f, "Maximum crosshair interaction range in meters.");
		_pointToolShowTargetName = base.Config.Bind("PointTool", "ShowTargetName", defaultValue: true, "Show the target name in the point HUD.");
		InventoryArrange = new InventoryArrangeFeature(base.Logger, GetInventoryArrangeSettings,
			() => CanExecuteGameplay, () => RuntimeSessionBoundary.CaptureConnectionKey(CurrentSessionState));
		_pointToolShowId = base.Config.Bind("PointTool", "ShowId", defaultValue: true, "Show target ID and GUID in the point HUD.");
		_pointToolShowHealth = base.Config.Bind("PointTool", "ShowHealth", defaultValue: true, "Show target health or durability in the point HUD.");
		_pointToolShowHud = base.Config.Bind("Interface", "ShowPointToolHud", defaultValue: true, "Show the point-tool HUD.");
		_creatureHealthBarEnabled = base.Config.Bind("CreatureHealthBar", "Enabled", defaultValue: false, "Show zombie and animal health bars when gameplay authorization is active; supported only in singleplayer.");
		_autoPickupEnabled = base.Config.Bind("AutoPickup", "Enabled", defaultValue: false, "Request automatic pickup; effective only while gameplay authorization is active and supported only in singleplayer.");
		_infiniteStorageEnabled = base.Config.Bind("InfiniteStorage", "Enabled", defaultValue: false, "Request persistent Snapshot infinite storage; effective only in a true singleplayer world. Disabled by default.");
		_infiniteStoragePickupPriority = base.Config.Bind("InfiniteStorage", "PickupPriority", defaultValue: false, "Store automatically picked-up items in infinite storage before trying the carried inventory.");
		_infiniteStorageCraftingEnabled = base.Config.Bind("InfiniteStorage", "CraftingAdapterEnabled", defaultValue: true, "Enable the same-DLL crafting adapter. Snapshot Core remains independent.");
		_infiniteStorageAmmoRefillEnabled = base.Config.Bind("InfiniteStorage", "AmmoRefillAdapterEnabled", defaultValue: true, "Enable the same-DLL ammo refill adapter. Snapshot Core remains independent.");
		_infiniteStorageDecompositionEnabled = base.Config.Bind("InfiniteStorage", "DecompositionAdapterEnabled", defaultValue: true, "Enable the same-DLL decomposition adapter. Snapshot Core remains independent.");
		_infiniteStorageDestructionWarningAcknowledged = base.Config.Bind("InfiniteStorage", "DestructionWarningAcknowledged", defaultValue: false, "Remember that the one-time permanent-destruction warning was acknowledged.");
		ConfigDefinition rangeDefinition = new ConfigDefinition("AutoPickup", "Range");
		_autoPickupRange = base.Config.Bind(rangeDefinition, 40f, new ConfigDescription("Automatic pickup distance in meters, clamped to 1-1000."));
		_autoPickupFrequencyHz = base.Config.Bind("AutoPickup", "FrequencyHz", 10f, "Drop scan frequency, clamped to 1-20 Hz.");
		_autoPickupRequestsPerTick = base.Config.Bind("AutoPickup", "RequestsPerTick", 4, "Maximum native pickup requests per scan, clamped to 1-16.");
		_autoPickupDropBlockMinutes = base.Config.Bind("AutoPickup", "DropBlockMinutes", 1, "Temporarily block automatic pickup for an item ID after the player drops it, clamped to 1-1440 minutes.");
		_autoPickupQualityFilterEnabled = base.Config.Bind("AutoPickup", "QualityFilterEnabled", defaultValue: false, "Ignore automatically picked-up items below the configured minimum quality.");
		_autoPickupMinimumQuality = base.Config.Bind("AutoPickup", "MinimumQuality", 0, "Minimum automatic-pickup item quality, clamped to 0-100 when quality filtering is enabled.");
		_autoPickupAllowedRarityMask = base.Config.Bind("AutoPickup", "AllowedRarityMask", AutoPickupSettings.AllRaritiesMask, "Bit mask of item rarities allowed by automatic pickup; bits 0-5 map from Common through Mythical.");
		_autoPickupFilterMode = base.Config.Bind("AutoPickup", "FilterMode", "BlacklistOnly", "BlacklistOnly or WhitelistThenBlacklist; whitelist state is also persisted in the rules document.");
		_autoPickupRulesPath = base.Config.Bind("AutoPickup", "RulesPath", "AutoPickup.rules.json", "Relative paths are resolved under BepInEx/config.");
		_autoPickupPauseWhenMenuOpen = base.Config.Bind("AutoPickup", "PauseWhenMenuOpen", defaultValue: true, "Pause new pickup scans while the cheat menu is open.");
		_autoPickupPauseWhenDead = base.Config.Bind("AutoPickup", "PauseWhenDead", defaultValue: true, "Pause and clear pending pickup work while the player is dead.");
		_autoPickupDiagnostics = base.Config.Bind("AutoPickup", "Diagnostics", "Failures", "Off, Failures, Summary, or Verbose.");
		_autoPickupUseMinimapRange = base.Config.Bind("AutoPickup", "UseMinimapRange", defaultValue: false, "When explicitly enabled, automatic pickup range follows the minimap visible radius.");
		_autoPickupUseMinimapRarityFilter = base.Config.Bind("AutoPickup", "UseMinimapRarityFilter", defaultValue: false, "When explicitly enabled, the minimap rarity mask also limits automatic pickup.");
		_minimapSchemaVersion = base.Config.Bind("Minimap", "SchemaVersion", 0, "Internal minimap settings migration version.");
		_minimapEnabled = base.Config.Bind("Minimap", "Enabled", defaultValue: false, "Request the minimap HUD; effective only while gameplay authorization is active and supported only in singleplayer.");
		_minimapShape = base.Config.Bind("Minimap", "Shape", "Circle", "Circle or Square.");
		_minimapAnchor = base.Config.Bind("Minimap", "Anchor", "TopLeft", "TopLeft, TopRight, BottomLeft, or BottomRight.");
		_minimapOffsetX = base.Config.Bind("Minimap", "OffsetX", 20f, "Horizontal distance from the selected screen anchor.");
		_minimapOffsetY = base.Config.Bind("Minimap", "OffsetY", 20f, "Vertical distance from the selected screen anchor.");
		_minimapSize = base.Config.Bind("Minimap", "Size", 260f, "Minimap size in UI pixels, clamped to 160-520.");
		_minimapVisibleRadius = base.Config.Bind("Minimap", "VisibleRadiusMeters", 100f, "World radius represented by the minimap, clamped to 20-500 meters.");
		_minimapBackgroundMode = base.Config.Bind("Minimap", "BackgroundMode", "AutoNativeFirst", "AutoNativeFirst, NativeMapOnly, RealtimeSurface, RealtimeHeightSlice, or Disabled.");
		_minimapBackgroundRenderHz = base.Config.Bind("Minimap", "BackgroundRenderHz", 5f, "Explicit minimap camera render frequency, clamped to 2-10 Hz.");
		_minimapWorldScanHz = base.Config.Bind("Minimap", "WorldScanHz", 5f, "Radar scan frequency, clamped to 2-10 Hz.");
		_minimapShowLoadedBuildings = base.Config.Bind("Minimap", "ShowLoadedBuildings", defaultValue: true, "Overlay client-loaded buildings in realtime camera backgrounds.");
		_minimapRotateWithView = base.Config.Bind("Minimap", "RotateWithView", defaultValue: false, "Keep the existing north-up minimap when disabled; rotate map content and compass with the final camera heading when enabled.");
		_minimapOcclusionProbeHz = base.Config.Bind("Minimap", "OcclusionProbeHz", 5f, "Overhead occlusion probe frequency.");
		_minimapOcclusionEnterThickness = base.Config.Bind("Minimap", "OcclusionEnterThickness", 2.5f, "Continuous overhead thickness required to enter height-slice mode.");
		_minimapOcclusionExitThickness = base.Config.Bind("Minimap", "OcclusionExitThickness", 1.5f, "Thickness threshold used to leave height-slice mode.");
		_minimapOcclusionEnterDelay = base.Config.Bind("Minimap", "OcclusionEnterDelaySeconds", 0.75f, "Occlusion enter hysteresis delay.");
		_minimapOcclusionExitDelay = base.Config.Bind("Minimap", "OcclusionExitDelaySeconds", 0.25f, "Occlusion exit hysteresis delay.");
		_minimapOcclusionMergeGap = base.Config.Bind("Minimap", "OcclusionMergeGap", 0.25f, "Maximum gap merged between adjacent solid intervals.");
		_minimapFloorExclusionMaxThickness = base.Config.Bind("Minimap", "FloorExclusionMaxThickness", 1.25f, "Thin floors at or below this thickness do not count as cave roofs.");
		_minimapShowCompass = base.Config.Bind("Minimap", "ShowCompass", defaultValue: true, "Show north/east/south/west labels for the selected orientation mode.");
		_minimapShowCoordinates = base.Config.Bind("Minimap", "ShowCoordinates", defaultValue: true, "Show player XYZ below the clipped viewport.");
		_minimapShowNativeMapMarkers = base.Config.Bind("Minimap", "ShowNativeMapMarkers", defaultValue: true, "Show local and same-group native map markers.");
		_minimapShowTeleportMarkers = base.Config.Bind("Minimap", "ShowTeleportMarkers", defaultValue: true, "Show saved teleport points for the current map.");
		_minimapShowZombies = base.Config.Bind("Minimap", "ShowZombies", defaultValue: true, "Show living zombies in the visible radius.");
		_minimapShowAnimals = base.Config.Bind("Minimap", "ShowAnimals", defaultValue: true, "Show living animals in the visible radius.");
		_minimapShowPlayers = base.Config.Bind("Minimap", "ShowPlayers", defaultValue: true, "Show other client-known players in the visible radius.");
		_minimapShowItems = base.Config.Bind("Minimap", "ShowItems", defaultValue: false, "Show raw nearby ground items independently of automatic pickup.");
		_minimapItemRarityMask = base.Config.Bind("Minimap", "ItemRarityMask", "All", "Enabled item rarities: Common, Uncommon, Rare, Epic, Legendary, Mythical, or All.");
		_minimapItemMergePixels = base.Config.Bind("Minimap", "ItemMergePixels", 8f, "Pixel distance used to merge overlapping item stars.");
		_minimapUndergroundSliceAbove = base.Config.Bind("Minimap", "UndergroundSliceAbove", MinimapSettings.DefaultUndergroundSliceAbove, "Height rendered above the player in height-slice mode.");
		_minimapUndergroundSliceBelow = base.Config.Bind("Minimap", "UndergroundSliceBelow", MinimapSettings.DefaultUndergroundSliceBelow, "Depth rendered below the player in height-slice mode.");
		_minimapSameFloorTolerance = base.Config.Bind("Minimap", "SameFloorTolerance", 6f, "Vertical distance treated as the current floor.");
		_minimapShowOtherFloors = base.Config.Bind("Minimap", "ShowOtherFloors", defaultValue: true, "Show dimmed graphical height indicators for other floors.");
		_minimapLayoutEditMode = base.Config.Bind("Minimap", "LayoutEditMode", defaultValue: false, "Allow dragging and resizing the minimap HUD.");
		int minimapSchemaVersion = _minimapSchemaVersion.Value;
		if (minimapSchemaVersion < 2)
		{
			_minimapShowOtherFloors.Value = true;
		}
		if (minimapSchemaVersion < 3)
		{
			if (_minimapOcclusionProbeHz.Value == 2f) _minimapOcclusionProbeHz.Value = 5f;
			if (_minimapOcclusionExitDelay.Value == 1.5f) _minimapOcclusionExitDelay.Value = 0.25f;
		}
		if (minimapSchemaVersion < 4)
		{
			if (_minimapUndergroundSliceBelow.Value == 6f)
				_minimapUndergroundSliceBelow.Value = 48f;
		}
		if (minimapSchemaVersion < 5)
		{
			if (_minimapUndergroundSliceBelow.Value == 48f)
				_minimapUndergroundSliceBelow.Value = MinimapSettings.DefaultUndergroundSliceBelow;
		}
		if (minimapSchemaVersion < CurrentMinimapSchemaVersion)
		{
			_minimapSchemaVersion.Value = CurrentMinimapSchemaVersion;
		}
		_flightSpeed = base.Config.Bind("Movement", "FlightSpeed", 3f, "Horizontal flight speed multiplier.");
		_flightVerticalSpeed = base.Config.Bind("Movement", "FlightVerticalSpeed", 2f, "Vertical flight speed multiplier.");
		_flightMode = base.Config.Bind("Movement", "FlightMode", "WorldAxis", "WorldAxis or ViewDirection.");
		_noclipSafeExit = base.Config.Bind("Movement", "NoClipSafeExit", defaultValue: true, "Search for a safe position when noclip is disabled.");
		_workshopLocalizationEnabled = base.Config.Bind("WorkshopLocalization", "Enabled", defaultValue: false, "Apply cached translations to loaded Workshop assets.");
		_workshopLocalizationApiProtocol = base.Config.Bind("WorkshopLocalization", "ApiProtocol", "OpenAICompatible", "Translation API protocol. Configure it in the in-game Translation page.");
		_workshopLocalizationAutoScan = base.Config.Bind("WorkshopLocalization", "AutoScanOnAssetsRefreshed", defaultValue: true, "Rescan loaded Workshop assets after an asset refresh.");
		_workshopLocalizationAutoTranslate = base.Config.Bind("WorkshopLocalization", "AutoTranslateMissing", defaultValue: false, "Automatically translate uncached text; disabled by default to avoid unexpected API charges.");
		_workshopLocalizationApplyToNativeUi = base.Config.Bind("WorkshopLocalization", "ApplyToNativeUi", defaultValue: true, "Apply cached translations to Unturned Getter display paths.");
		_workshopLocalizationSourceLanguage = base.Config.Bind("WorkshopLocalization", "SourceLanguage", "English", "Source language sent to the translation provider.");
		_workshopLocalizationTargetLanguage = base.Config.Bind("WorkshopLocalization", "TargetLanguage", "zh-CN", "Target language used by the cache and translation provider.");
		_workshopLocalizationEndpoint = base.Config.Bind("WorkshopLocalization", "Endpoint", string.Empty, "OpenAI-compatible Chat Completions endpoint or base URL.");
		_workshopLocalizationApiKey = base.Config.Bind("WorkshopLocalization", "ApiKey", string.Empty, "Translation API keys (up to two, separated by new lines); they are never written to logs.");
		_workshopLocalizationSecretKey = base.Config.Bind("WorkshopLocalization", "SecretKey", string.Empty, "Cloud translation secret key; it is never written to logs.");
		_workshopLocalizationRegion = base.Config.Bind("WorkshopLocalization", "Region", string.Empty, "Cloud provider region when required.");
		_workshopLocalizationProjectId = base.Config.Bind("WorkshopLocalization", "ProjectId", string.Empty, "Cloud provider project ID when required.");
		_workshopLocalizationModel = base.Config.Bind("WorkshopLocalization", "Model", string.Empty, "Translation model name.");
		_workshopLocalizationBatchSize = base.Config.Bind("WorkshopLocalization", "BatchSize", 50, "Translation batch size, clamped to 1-100.");
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
		Actions = new CheatActions(base.Logger, () => CanExecuteGameplay);
		WorldMaintenance = new WorldMaintenanceService(base.Logger, () => CurrentSessionState.IsSupportedGameplay);
		Movement = new MovementCheatService(base.Logger, () => CanExecuteGameplay)
		{
			FlightSpeed = Mathf.Clamp(_flightSpeed.Value, 1f, 10f),
			VerticalSpeed = Mathf.Clamp(_flightVerticalSpeed.Value, 1f, 10f),
			SafeExit = _noclipSafeExit.Value
		};
		Movement.SetFlightMode(ParseFlightMode(_flightMode.Value, base.Logger));
		_uiRoot = PluginUiRoot.Create(base.Logger);
		_pointToolHud = new PointToolHud(base.Logger, _uiRoot);
		_creatureHealthBarHud = new CreatureHealthBarHud(base.Logger);
		CreatureHealthBars = new CreatureHealthBarService(base.Logger, new CreatureHealthBarSettings
		{
			Enabled = _creatureHealthBarEnabled.Value,
			DamageDisplayDuration = ReadConfigFloat("CreatureHealthBar", "DamageDisplayDuration", 10f, "Seconds to show a creature after confirmed damage."),
			CrosshairRange = ReadConfigFloat("CreatureHealthBar", "CrosshairRange", 100f, "Maximum crosshair target range in meters."),
			DisplayRange = ReadConfigFloat("CreatureHealthBar", "DisplayRange", 100f, "Maximum health bar display range in meters."),
			MaxVisibleBars = ReadConfigInt("CreatureHealthBar", "MaxVisibleBars", 12, "Maximum number of visible creature health bars."),
			ShowAnimals = ReadConfigBool("CreatureHealthBar", "ShowAnimals", true, "Show animal health bars."),
			ShowZombies = ReadConfigBool("CreatureHealthBar", "ShowZombies", true, "Show zombie health bars."),
			OcclusionCheckHz = ReadConfigFloat("CreatureHealthBar", "OcclusionCheckHz", 10f, "Health bar occlusion checks per second."),
			HealthRefreshHz = ReadConfigFloat("CreatureHealthBar", "HealthRefreshHz", 20f, "Health value refreshes per second."),
			BarScaleReferenceDistance = ReadConfigFloat("CreatureHealthBar", "BarScaleReferenceDistance", 18f, "Distance in meters at which the health bar uses its base size."),
			MaxBarScale = ReadConfigFloat("CreatureHealthBar", "MaxBarScale", 1.5f, "Maximum near-distance health bar scale."),
			Diagnostics = ReadConfigBool("CreatureHealthBar", "Diagnostics", false, "Enable health bar diagnostics.")
		}, _creatureHealthBarHud, () => CanExecuteGameplay);
		PointTool = new PointToolService(base.Logger, _pointToolHud, () => CanExecuteGameplay)
		{
			Enabled = _pointToolEnabled.Value,
			Mode = ParsePointToolMode(_pointToolMode.Value),
			Range = Mathf.Clamp(_pointToolRange.Value, 5f, 250f),
			ShowTargetName = _pointToolShowTargetName.Value,
			ShowId = _pointToolShowId.Value,
			ShowHealth = _pointToolShowHealth.Value,
			ShowHud = _pointToolShowHud.Value
		};
		_nearbyItemIndex = new NearbyItemIndex();
		AutoPickup = new AutoPickupFeature(base.Logger, GetAutoPickupSettings(), _autoPickupRulesPath.Value,
			_nearbyItemIndex, () => CanExecuteGameplay);
		Minimap = new MinimapFeature(base.Logger, _uiRoot, Teleports, _nearbyItemIndex,
			GetMinimapSettings(), HandleMinimapSettingsChanged, () => CanExecuteGameplay);
		_snapshotStorageModules = new SnapshotModuleRegistry();
		InfiniteStorage = new SnapshotInfiniteStorageRuntime(base.Logger, _snapshotStorageModules)
		{
			Enabled = _infiniteStorageEnabled.Value
		};
		SnapshotCraftingModule craftingModule = new SnapshotCraftingModule(
			InfiniteStorage, _infiniteStorageCraftingEnabled.Value);
		_snapshotAmmoRefillModule = new SnapshotAmmoRefillModule(
			InfiniteStorage, _infiniteStorageAmmoRefillEnabled.Value, base.Logger);
		SnapshotDecompositionModule decompositionModule = new SnapshotDecompositionModule(
			InfiniteStorage, _infiniteStorageDecompositionEnabled.Value);
		_snapshotStorageModules.Register(craftingModule);
		_snapshotStorageModules.Register(_snapshotAmmoRefillModule);
		_snapshotStorageModules.Register(decompositionModule);
		SnapshotCraftingPatch.Configure(craftingModule);
		AutoPickupRuleDocument autoPickupRules = AutoPickup.Rules;
		_autoPickupFilterMode.Value = autoPickupRules.Mode.ToString();
		_ui = new CheatMenuOverlayUi(this, _uiRoot);
		TransferRuntimeOwnership();
		_runtimeContext.InstallRuntimePatches();
		Favorites.Load();
		Teleports.Load();
		_runtimeContext.SubscribeLifecycle();
		base.Logger.LogInfo("Unturned Singleplayer Cheat Menu " + PluginVersion + " 已加载。快捷键：" + ShortcutLabel + "；玩法支持范围：单人；多人需按连接确认且不受支持；Snapshot 仅限单人。");
		base.Logger.LogInfo("Plugin UI language: " + PluginLocalization.CurrentLanguageName + "; configured=" + _language.Value + "; Unturned=" + Provider.language + ".");
		Version version = typeof(BaseUnityPlugin).Assembly.GetName().Version;
		base.Logger.LogInfo($"检测环境：Unturned {Provider.APP_VERSION}，Unity {Application.unityVersion}，BepInEx {version}。");
	}

	private void TransferRuntimeOwnership()
	{
		_runtimeContext?.OwnRuntime(new PluginRuntimeComposition(
			_uiRoot,
			_ui,
			_vehicleIconRenderer,
			_pointToolHud,
			_creatureHealthBarHud,
			_shortcutManager,
			Catalog,
			Icons,
			TeleportMaps,
			Actions,
			Movement,
			PointTool,
			InventoryArrange,
			_nearbyItemIndex,
			AutoPickup,
			Minimap,
			WorldMaintenance,
			InfiniteStorage,
			CreatureHealthBars,
			WorkshopLocalization));
	}

	private float ReadConfigFloat(string section, string key, float value, string description)
	{
		return base.Config.Bind(section, key, value, description).Value;
	}

	private int ReadConfigInt(string section, string key, int value, string description)
	{
		return base.Config.Bind(section, key, value, description).Value;
	}

	private bool ReadConfigBool(string section, string key, bool value, string description)
	{
		return base.Config.Bind(section, key, value, description).Value;
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

	private void HandleToggleShortcut(string inputSource)
	{
		ObserveRuntimeSession();
		if (!CanOpenClientMenu)
		{
			base.Logger.LogWarning("未打开菜单：" + GetGameplayAccessFailure(CurrentGameplayAccess));
			return;
		}
		SetMenuOpen(!_menuOpen);
		base.Logger.LogInfo("快捷键 " + ShortcutLabel + " 已通过" + inputSource + "触发：菜单" + (_menuOpen ? "已打开" : "已关闭") + "。");
	}

	private bool _isTornDown;

	private void OnDestroy()
	{
		base.Logger.LogInfo("[Runtime] BepInEx bootstrap component destroyed; static Update bridge and runtime context remain active until application quit.");
	}

	private void HandleInfiniteStorageShortcut(string inputSource)
	{
		ObserveRuntimeSession();
		if (!CanUseSnapshotStorage)
		{
			base.Logger.LogWarning("Cannot open infinite storage: " + PluginLocalization.Translate("Snapshot 无限储存仅在真正的单人世界中可用。"));
			return;
		}
		SetInfiniteStorageOpen(!_infiniteStorageOpen);
		base.Logger.LogInfo("Infinite storage shortcut triggered via " + inputSource + ": " + (_infiniteStorageOpen ? "opened" : "closed") + ".");
	}

	private void OnApplicationQuit()
	{
		Teardown("application quit");
	}

	private void Teardown(string reason)
	{
		if (_isTornDown)
		{
			return;
		}
		_isTornDown = true;
		PluginRuntimeContext runtimeContext = _runtimeContext;
		_runtimeContext = null;
		runtimeContext?.Dispose(reason);
		_ui = null;
		_uiRoot = null;
		if (ReferenceEquals(Instance, this))
		{
			Instance = null;
		}
	}

	internal void CloseMenu()
	{
		SetMenuOpen(open: false);
	}

	internal void CloseInfiniteStorage()
	{
		SetInfiniteStorageOpen(open: false);
	}

	private void NotifyWorkshopLocalizationSettingsChanged(bool refreshLocalizedAssetViews = false)
	{
		WorkshopLocalization?.NotifySettingsChanged();
		if (refreshLocalizedAssetViews)
		{
			_runtimeContext?.SynchronizeWorkshopVersions();
			_ui?.OnLocalizedAssetViewsRefreshed();
		}
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
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationEndpoint(string endpoint)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Endpoint = endpoint?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationEndpoint, WorkshopLocalization.Settings.Endpoint);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationSecretKey(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.SecretKey = value ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationSecretKey, WorkshopLocalization.Settings.SecretKey);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationRegion(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Region = value?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationRegion, WorkshopLocalization.Settings.Region);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationProjectId(string value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.ProjectId = value?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationProjectId, WorkshopLocalization.Settings.ProjectId);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationApiKey(string apiKey)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.ApiKey = apiKey ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationApiKey, WorkshopLocalization.Settings.ApiKey);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationModel(string model)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Settings.Model = model?.Trim() ?? string.Empty;
			PersistInterfaceState(_workshopLocalizationModel, WorkshopLocalization.Settings.Model);
			NotifyWorkshopLocalizationSettingsChanged();
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
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationEnabled(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.Enabled = value;
			PersistInterfaceState(_workshopLocalizationEnabled, value);
			NotifyWorkshopLocalizationSettingsChanged(refreshLocalizedAssetViews: true);
		}
	}

	internal void SetWorkshopLocalizationApplyToNativeUi(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.ApplyToNativeUi = value;
			PersistInterfaceState(_workshopLocalizationApplyToNativeUi, value);
			NotifyWorkshopLocalizationSettingsChanged(refreshLocalizedAssetViews: true);
		}
	}

	internal void SetWorkshopLocalizationAutoScan(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.AutoScanOnAssetsRefreshed = value;
			PersistInterfaceState(_workshopLocalizationAutoScan, value);
			NotifyWorkshopLocalizationSettingsChanged();
		}
	}

	internal void SetWorkshopLocalizationAutoTranslate(bool value)
	{
		if (WorkshopLocalization != null)
		{
			WorkshopLocalization.AutoTranslateMissing = value;
			PersistInterfaceState(_workshopLocalizationAutoTranslate, value);
			NotifyWorkshopLocalizationSettingsChanged();
			if (value && WorkshopLocalization.ApiConfigured && !WorkshopLocalization.IsTranslationRunning)
			{
				WorkshopLocalization.ScanCurrentAssets();
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
		PointTool.Enabled = value;
		PersistInterfaceState(_pointToolEnabled, value);
		if (!value)
		{
			_pointToolHud.ClearPointIfCreated();
		}
	}

	internal bool CreatureHealthBarEnabled => _creatureHealthBarEnabled?.Value ?? false;

	internal void SetCreatureHealthBarEnabled(bool value)
	{
		PersistInterfaceState(_creatureHealthBarEnabled, value);
		if (CreatureHealthBars != null)
		{
			CreatureHealthBars.SetEnabled(value);
		}
	}

	internal void SetAutoPickupEnabled(bool value)
	{
		PersistInterfaceState(_autoPickupEnabled, value);
		RefreshAutoPickupSettings();
	}

	internal void SetInfiniteStorageEnabled(bool value)
	{
		PersistInterfaceState(_infiniteStorageEnabled, value);
		if (InfiniteStorage != null)
		{
			InfiniteStorage.Enabled = value;
			if (!value) InfiniteStorage.Flush();
		}
	}

	internal void SetInfiniteStoragePickupPriority(bool value)
	{
		PersistInterfaceState(_infiniteStoragePickupPriority, value);
	}

	internal void SetAutoPickupRange(float value)
	{
		float normalized = Mathf.Clamp(value, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange);
		PersistInterfaceState(_autoPickupRange, normalized);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupFrequency(float value)
	{
		PersistInterfaceState(_autoPickupFrequencyHz, Mathf.Clamp(value, AutoPickupSettings.MinFrequencyHz, AutoPickupSettings.MaxFrequencyHz));
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupRequestsPerTick(int value)
	{
		PersistInterfaceState(_autoPickupRequestsPerTick, Mathf.Clamp(value, AutoPickupSettings.MinRequestsPerTick, AutoPickupSettings.MaxRequestsPerTick));
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupDropBlockMinutes(int value)
	{
		PersistInterfaceState(_autoPickupDropBlockMinutes, Mathf.Clamp(value, AutoPickupSettings.MinDropBlockMinutes, AutoPickupSettings.MaxDropBlockMinutes));
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupQualityFilterEnabled(bool value)
	{
		PersistInterfaceState(_autoPickupQualityFilterEnabled, value);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupMinimumQuality(int value)
	{
		PersistInterfaceState(_autoPickupMinimumQuality, Mathf.Clamp(value, AutoPickupSettings.MinQuality, AutoPickupSettings.MaxQuality));
		RefreshAutoPickupSettings();
	}

	internal bool AcknowledgeInfiniteStorageDestructionWarning() =>
		PersistInterfaceState(_infiniteStorageDestructionWarningAcknowledged, true);

	internal void SetAutoPickupAllowedRarityMask(int value)
	{
		PersistInterfaceState(_autoPickupAllowedRarityMask, value & AutoPickupSettings.AllRaritiesMask);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupPauseWhenMenuOpen(bool value)
	{
		PersistInterfaceState(_autoPickupPauseWhenMenuOpen, value);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupPauseWhenDead(bool value)
	{
		PersistInterfaceState(_autoPickupPauseWhenDead, value);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupDiagnostics(AutoPickupDiagnosticsLevel value)
	{
		PersistInterfaceState(_autoPickupDiagnostics, value.ToString());
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupUseMinimapRange(bool value)
	{
		PersistInterfaceState(_autoPickupUseMinimapRange, value);
		RefreshAutoPickupSettings();
	}

	internal void SetAutoPickupUseMinimapRarityFilter(bool value)
	{
		PersistInterfaceState(_autoPickupUseMinimapRarityFilter, value);
		RefreshAutoPickupSettings();
	}

	internal AutoPickupRuleLoadResult SetAutoPickupRules(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules)
	{
		AutoPickupFilterMode normalizedMode = enabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly;
		PersistInterfaceState(_autoPickupFilterMode, normalizedMode.ToString());
		return AutoPickup.SaveRules(enabled, normalizedMode, rules);
	}

	internal AutoPickupRuleLoadResult SetAutoPickupWhitelistEnabled(bool enabled)
	{
		AutoPickupRuleDocument document = AutoPickup.Rules;
		return SetAutoPickupRules(enabled, enabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly, document.Rules);
	}

	private AutoPickupSettings GetAutoPickupSettings()
	{
		AutoPickupDiagnosticsLevel diagnostics = Enum.TryParse(_autoPickupDiagnostics?.Value, ignoreCase: true, out AutoPickupDiagnosticsLevel parsedDiagnostics) ? parsedDiagnostics : AutoPickupDiagnosticsLevel.Failures;
		return new AutoPickupSettings
		{
			Enabled = _autoPickupEnabled?.Value ?? false,
			Range = _autoPickupRange?.Value ?? 40f,
			FrequencyHz = _autoPickupFrequencyHz?.Value ?? 10f,
			RequestsPerTick = _autoPickupRequestsPerTick?.Value ?? 4,
			DropBlockMinutes = _autoPickupDropBlockMinutes?.Value ?? AutoPickupSettings.MinDropBlockMinutes,
			QualityFilterEnabled = _autoPickupQualityFilterEnabled?.Value ?? false,
			MinimumQuality = _autoPickupMinimumQuality?.Value ?? AutoPickupSettings.MinQuality,
			AllowedRarityMask = _autoPickupAllowedRarityMask?.Value ?? AutoPickupSettings.AllRaritiesMask,
			PauseWhenMenuOpen = _autoPickupPauseWhenMenuOpen?.Value ?? true,
			PauseWhenDead = _autoPickupPauseWhenDead?.Value ?? true,
			UseMinimapRange = _autoPickupUseMinimapRange?.Value ?? false,
			UseMinimapRarityFilter = _autoPickupUseMinimapRarityFilter?.Value ?? false,
			Diagnostics = diagnostics
		}.Normalize();
	}

	private void RefreshAutoPickupSettings()
	{
		AutoPickup?.UpdateSettings(GetAutoPickupSettings());
	}

	internal MinimapSettings GetMinimapSettings()
	{
		try
		{
			return GetMinimapSettingsCore();
		}
		catch (Exception exception)
		{
			WarnInvalidMinimapConfiguration("[Minimap] configuration could not be read; safe defaults are active.\n" + exception);
			return new MinimapSettings().Normalize();
		}
	}

	private MinimapSettings GetMinimapSettingsCore()
	{
		string shapeValue = _minimapShape?.Value ?? "Circle";
		string anchorValue = _minimapAnchor?.Value ?? "TopLeft";
		string backgroundValue = _minimapBackgroundMode?.Value ?? "AutoNativeFirst";
		string rarityValue = _minimapItemRarityMask?.Value ?? "All";
		bool shapeValid = Enum.TryParse(shapeValue, true, out MinimapShape shape);
		bool anchorValid = Enum.TryParse(anchorValue, true, out MinimapAnchor anchor);
		bool backgroundValid = Enum.TryParse(backgroundValue, true, out MinimapBackgroundMode background);
		bool rarityValid = Enum.TryParse(rarityValue, true, out ItemRarityMask rarityMask);
		if (!shapeValid) shape = MinimapShape.Circle;
		if (!anchorValid) anchor = MinimapAnchor.TopLeft;
		if (!backgroundValid) background = MinimapBackgroundMode.AutoNativeFirst;
		if (!rarityValid) rarityMask = ItemRarityMask.All;
		MinimapSettings raw = new MinimapSettings
		{
			Enabled = _minimapEnabled?.Value ?? false,
			Shape = shape,
			Anchor = anchor,
			OffsetX = _minimapOffsetX?.Value ?? 20f,
			OffsetY = _minimapOffsetY?.Value ?? 20f,
			Size = _minimapSize?.Value ?? 260f,
			VisibleRadiusMeters = _minimapVisibleRadius?.Value ?? 100f,
			BackgroundMode = background,
			BackgroundRenderHz = _minimapBackgroundRenderHz?.Value ?? 5f,
			WorldScanHz = _minimapWorldScanHz?.Value ?? 5f,
			ShowLoadedBuildings = _minimapShowLoadedBuildings?.Value ?? true,
			RotateWithView = _minimapRotateWithView?.Value ?? false,
			OcclusionProbeHz = _minimapOcclusionProbeHz?.Value ?? 5f,
			OcclusionEnterThickness = _minimapOcclusionEnterThickness?.Value ?? 2.5f,
			OcclusionExitThickness = _minimapOcclusionExitThickness?.Value ?? 1.5f,
			OcclusionEnterDelaySeconds = _minimapOcclusionEnterDelay?.Value ?? 0.75f,
			OcclusionExitDelaySeconds = _minimapOcclusionExitDelay?.Value ?? 0.25f,
			OcclusionMergeGap = _minimapOcclusionMergeGap?.Value ?? 0.25f,
			FloorExclusionMaxThickness = _minimapFloorExclusionMaxThickness?.Value ?? 1.25f,
			ShowCompass = _minimapShowCompass?.Value ?? true,
			ShowCoordinates = _minimapShowCoordinates?.Value ?? true,
			ShowNativeMapMarkers = _minimapShowNativeMapMarkers?.Value ?? true,
			ShowTeleportMarkers = _minimapShowTeleportMarkers?.Value ?? true,
			ShowZombies = _minimapShowZombies?.Value ?? true,
			ShowAnimals = _minimapShowAnimals?.Value ?? true,
			ShowPlayers = _minimapShowPlayers?.Value ?? true,
			ShowItems = _minimapShowItems?.Value ?? false,
			ItemRarityMask = rarityMask,
			ItemMergePixels = _minimapItemMergePixels?.Value ?? 8f,
			UndergroundSliceAbove = _minimapUndergroundSliceAbove?.Value ?? MinimapSettings.DefaultUndergroundSliceAbove,
			UndergroundSliceBelow = _minimapUndergroundSliceBelow?.Value ?? MinimapSettings.DefaultUndergroundSliceBelow,
			SameFloorTolerance = _minimapSameFloorTolerance?.Value ?? 6f,
			ShowOtherFloors = _minimapShowOtherFloors?.Value ?? true,
			LayoutEditMode = _minimapLayoutEditMode?.Value ?? false
		};
		MinimapSettings normalized = raw.Clone().Normalize();
		if (!shapeValid || !anchorValid || !backgroundValid || !rarityValid || MinimapSettingsChangedByNormalization(raw, normalized))
		{
			WarnInvalidMinimapConfiguration(
				"[Minimap] invalid configuration value(s) were normalized; unsafe floor/exit/enter thresholds use safe defaults.");
		}
		return normalized;
	}

	internal void UpdateMinimapSettings(Action<MinimapSettings> change)
	{
		MinimapSettings settings = Minimap?.Settings ?? GetMinimapSettings();
		bool wasEnabled = settings.Enabled;
		change?.Invoke(settings);
		settings.Normalize();
		if (settings.Enabled && !wasEnabled && !EnsureGameplayAccess("minimap.enable", "UI"))
			return;
		PersistMinimapSettings(settings);
		Minimap?.ApplySettings(settings);
		SynchronizeMinimapLayoutInput(settings.LayoutEditMode);
	}

	private void HandleMinimapSettingsChanged(MinimapSettings settings)
	{
		if (settings == null) return;
		PersistMinimapSettings(settings.Normalize());
		Minimap?.ApplySettings(settings);
		SynchronizeMinimapLayoutInput(settings.LayoutEditMode);
	}

	private void SynchronizeMinimapLayoutInput(bool layoutEditMode)
	{
		if (layoutEditMode)
		{
			CaptureMenuInput();
		}
		else if (!_menuOpen && !_infiniteStorageOpen)
		{
			ReleaseMenuInput();
		}
	}

	private void PersistMinimapSettings(MinimapSettings settings)
	{
		bool saveOnConfigSet = base.Config.SaveOnConfigSet;
		try
		{
			base.Config.SaveOnConfigSet = false;
			_minimapEnabled.Value = settings.Enabled;
			_minimapShape.Value = settings.Shape.ToString();
			_minimapAnchor.Value = settings.Anchor.ToString();
			_minimapOffsetX.Value = settings.OffsetX;
			_minimapOffsetY.Value = settings.OffsetY;
			_minimapSize.Value = settings.Size;
			_minimapVisibleRadius.Value = settings.VisibleRadiusMeters;
			_minimapBackgroundMode.Value = settings.BackgroundMode.ToString();
			_minimapBackgroundRenderHz.Value = settings.BackgroundRenderHz;
			_minimapWorldScanHz.Value = settings.WorldScanHz;
			_minimapShowLoadedBuildings.Value = settings.ShowLoadedBuildings;
			_minimapRotateWithView.Value = settings.RotateWithView;
			_minimapOcclusionProbeHz.Value = settings.OcclusionProbeHz;
			_minimapOcclusionEnterThickness.Value = settings.OcclusionEnterThickness;
			_minimapOcclusionExitThickness.Value = settings.OcclusionExitThickness;
			_minimapOcclusionEnterDelay.Value = settings.OcclusionEnterDelaySeconds;
			_minimapOcclusionExitDelay.Value = settings.OcclusionExitDelaySeconds;
			_minimapOcclusionMergeGap.Value = settings.OcclusionMergeGap;
			_minimapFloorExclusionMaxThickness.Value = settings.FloorExclusionMaxThickness;
			_minimapShowCompass.Value = settings.ShowCompass;
			_minimapShowCoordinates.Value = settings.ShowCoordinates;
			_minimapShowNativeMapMarkers.Value = settings.ShowNativeMapMarkers;
			_minimapShowTeleportMarkers.Value = settings.ShowTeleportMarkers;
			_minimapShowZombies.Value = settings.ShowZombies;
			_minimapShowAnimals.Value = settings.ShowAnimals;
			_minimapShowPlayers.Value = settings.ShowPlayers;
			_minimapShowItems.Value = settings.ShowItems;
			_minimapItemRarityMask.Value = settings.ItemRarityMask.ToString();
			_minimapItemMergePixels.Value = settings.ItemMergePixels;
			_minimapUndergroundSliceAbove.Value = settings.UndergroundSliceAbove;
			_minimapUndergroundSliceBelow.Value = settings.UndergroundSliceBelow;
			_minimapSameFloorTolerance.Value = settings.SameFloorTolerance;
			_minimapShowOtherFloors.Value = settings.ShowOtherFloors;
			_minimapLayoutEditMode.Value = settings.LayoutEditMode;
			base.Config.Save();
		}
		catch (Exception exception)
		{
			base.Logger.LogWarning("[Minimap] settings are active for this session, but the config file could not be saved.\n" + exception);
		}
		finally
		{
			base.Config.SaveOnConfigSet = saveOnConfigSet;
		}
	}

	private static bool MinimapSettingsChangedByNormalization(MinimapSettings raw, MinimapSettings normalized)
	{
		return raw.OffsetX != normalized.OffsetX || raw.OffsetY != normalized.OffsetY ||
			raw.Size != normalized.Size || raw.VisibleRadiusMeters != normalized.VisibleRadiusMeters ||
			raw.BackgroundRenderHz != normalized.BackgroundRenderHz || raw.WorldScanHz != normalized.WorldScanHz ||
			raw.OcclusionProbeHz != normalized.OcclusionProbeHz ||
			raw.OcclusionEnterThickness != normalized.OcclusionEnterThickness ||
			raw.OcclusionExitThickness != normalized.OcclusionExitThickness ||
			raw.OcclusionEnterDelaySeconds != normalized.OcclusionEnterDelaySeconds ||
			raw.OcclusionExitDelaySeconds != normalized.OcclusionExitDelaySeconds ||
			raw.OcclusionMergeGap != normalized.OcclusionMergeGap ||
			raw.FloorExclusionMaxThickness != normalized.FloorExclusionMaxThickness ||
			raw.ItemRarityMask != normalized.ItemRarityMask || raw.ItemMergePixels != normalized.ItemMergePixels ||
			raw.UndergroundSliceAbove != normalized.UndergroundSliceAbove ||
			raw.UndergroundSliceBelow != normalized.UndergroundSliceBelow ||
			raw.SameFloorTolerance != normalized.SameFloorTolerance;
	}

	private void WarnInvalidMinimapConfiguration(string message)
	{
		if (_minimapConfigurationWarningLogged) return;
		_minimapConfigurationWarningLogged = true;
		base.Logger.LogWarning(message);
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

	internal void SetFlightMode(FlightMode mode)
	{
		Movement.SetFlightMode(mode);
		PersistInterfaceState(_flightMode, mode.ToString());
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

	private static FlightMode ParseFlightMode(string value, ManualLogSource log)
	{
		if (!Enum.TryParse<FlightMode>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(FlightMode), result))
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				log?.LogWarning($"飞行模式配置无效：{value}，已回退为 WorldAxis。" );
			}
			return FlightMode.WorldAxis;
		}
		return result;
	}

	internal void RefreshCatalog(bool scanWorkshopLocalization = true)
	{
		Catalog.Refresh();
		Icons.Clear();
		if (scanWorkshopLocalization)
		{
			WorkshopLocalizationService workshopLocalization = WorkshopLocalization;
			if (workshopLocalization == null || !workshopLocalization.IsTranslationRunning)
			{
				WorkshopLocalizationService workshopLocalization2 = WorkshopLocalization;
				if (workshopLocalization2 == null || workshopLocalization2.SnapshotVersion == 0)
				{
					WorkshopLocalization?.ScanCurrentAssets();
				}
			}
		}
		_runtimeContext?.SynchronizeWorkshopVersions();
		_ui.OnCatalogRefreshed();
		base.Logger.LogInfo($"资产扫描完成：物品 {Catalog.Items.Count}（模组 {Catalog.WorkshopItemCount}），车辆 {Catalog.Vehicles.Count}（模组 {Catalog.WorkshopVehicleCount}）。");
	}

	internal void SetMenuOpen(bool open)
	{
		ObserveRuntimeSession();
		if (open && (!CanOpenClientMenu || !_uiRoot.CanOpenWindow("the main menu")))
		{
			return;
		}
		if (open && _infiniteStorageOpen)
		{
			SetInfiniteStorageOpen(open: false);
		}
		if (_menuOpen != open)
		{
			_menuOpen = open;
			if (open)
			{
				CaptureMenuInput();
			}
			else
			{
				_ui.OnClosed();
				if (!IsMinimapLayoutEditActive) ReleaseMenuInput();
			}
			if (open)
			{
				PluginLocalization.Initialize(_language.Value, Provider.language);
				RefreshCatalog();
				_ui.OnOpened();
			}
		}
	}

	internal void SetInfiniteStorageOpen(bool open)
	{
		ObserveRuntimeSession();
		if (open && (!CanUseSnapshotStorage || !_uiRoot.CanOpenWindow("infinite storage")))
		{
			return;
		}
		if (_infiniteStorageOpen == open)
		{
			return;
		}
		_infiniteStorageOpen = open;
		if (open)
		{
			if (_menuOpen)
			{
				SetMenuOpen(open: false);
			}
			CaptureMenuInput();
			PluginLocalization.Initialize(_language.Value, Provider.language);
			_ui.OpenInfiniteStorage();
			_runtimeContext?.ScheduleInfiniteStorageRefresh(Catalog.IsDirty);
		}
		else
		{
			_runtimeContext?.CancelInfiniteStorageRefresh();
			_ui.CloseInfiniteStorage();
			if (!_menuOpen)
			{
				ReleaseMenuInput();
			}
		}
	}

	private void CaptureMenuInput()
	{
		_inputCapture.Capture();
	}

	private void ReleaseMenuInput()
	{
		_inputCapture.Release();
	}

	internal void HandleAssetsRefreshed()
	{
		InventoryStateReader.InvalidateAmmoCompatibility();
		InfiniteStorage?.RefreshAssetMetadata();
		_snapshotAmmoRefillModule?.RefreshAssets();
		Catalog.MarkDirty();
		Icons.Clear();
		WorkshopLocalization?.OnAssetsRefreshed();
		if (_menuOpen || _infiniteStorageOpen)
		{
			RefreshCatalog(scanWorkshopLocalization: false);
		}
	}

}
