using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Adapters.Crafting;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Patches;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.Services;

/// <summary>
/// Owns the plugin runtime graph, Unity/game subscriptions, Harmony instance, and the idempotent
/// teardown sequence. Static game Update patches keep dispatch alive after bootstrap components die.
/// </summary>
public sealed class PluginRuntimeContext : IDisposable
{
	private readonly CheatMenuPlugin _plugin;
	private readonly ManualLogSource _log;
	private readonly UiInputCaptureController _inputCapture;
	private PluginUiRoot _uiRoot;
	private CheatMenuOverlayUi _ui;
	private VehicleIconRenderer _vehicleIconRenderer;
	private PointToolHud _pointToolHud;
	private CreatureHealthBarHud _creatureHealthBarHud;
	private GlobalShortcutManager _shortcutManager;
	private AssetCatalog _catalog;
	private IconCache _icons;
	private TeleportMapService _teleportMaps;
	private CheatActions _actions;
	private MovementCheatService _movement;
	private PointToolService _pointTool;
	private InventoryArrangeFeature _inventoryArrange;
	private NearbyItemIndex _nearbyItemIndex;
	private AutoPickupFeature _autoPickup;
	private MinimapFeature _minimap;
	private NearbyItemSnapshot _sharedItemSnapshot;
	private float _nextSharedItemScanTime;
	private int _sharedItemSnapshotVersion;
	private SnapshotInfiniteStorageRuntime _infiniteStorage;
	private CreatureHealthBarService _creatureHealthBars;
	private WorkshopLocalizationService _workshopLocalization;
	private Harmony _harmony;
	private readonly IdempotentTeardownGate _teardownGate = new IdempotentTeardownGate();
	private bool _subscriptionsInstalled;
	private bool _disposed;
	private bool _hasLoggedUpdateCallback;
	private bool _hasLoggedWorkshopPumpFailure;
	private bool _hasLoggedInfiniteStoragePumpFailure;
	private int _lastRuntimeUpdateFrame = -1;
	private int _lastRuntimeLateUpdateFrame = -1;
	private int _lastWorkshopSnapshotVersion = -1;
	private int _lastWorkshopPresentationVersion = -1;
	private int _infiniteStorageRefreshFrame = -1;
	private float _nextMaintenanceTime;

	internal PluginRuntimeContext(CheatMenuPlugin plugin, ManualLogSource log, UiInputCaptureController inputCapture)
	{
		_plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
		_log = log;
		_inputCapture = inputCapture ?? throw new ArgumentNullException(nameof(inputCapture));
	}

	internal bool IsDisposed => _disposed;

	internal void OwnRuntime(PluginRuntimeComposition composition)
	{
		ThrowIfDisposed();
		if (composition == null)
		{
			throw new ArgumentNullException(nameof(composition));
		}
		_uiRoot = composition.UiRoot;
		_ui = composition.Ui;
		_vehicleIconRenderer = composition.VehicleIconRenderer;
		_pointToolHud = composition.PointToolHud;
		_creatureHealthBarHud = composition.CreatureHealthBarHud;
		_shortcutManager = composition.ShortcutManager;
		_catalog = composition.Catalog;
		_icons = composition.Icons;
		_teleportMaps = composition.TeleportMaps;
		_actions = composition.Actions;
		_movement = composition.Movement;
		_pointTool = composition.PointTool;
		_inventoryArrange = composition.InventoryArrange;
		_nearbyItemIndex = composition.NearbyItemIndex;
		_autoPickup = composition.AutoPickup;
		_minimap = composition.Minimap;
		_infiniteStorage = composition.InfiniteStorage;
		_creatureHealthBars = composition.CreatureHealthBars;
		_workshopLocalization = composition.WorkshopLocalization;
		_lastWorkshopSnapshotVersion = composition.WorkshopLocalization?.SnapshotVersion ?? -1;
		_lastWorkshopPresentationVersion = composition.WorkshopLocalization?.PresentationVersion ?? -1;
	}

	internal void InstallRuntimePatches()
	{
		ThrowIfDisposed();
		if (_harmony != null)
		{
			return;
		}
		_harmony = new Harmony(CheatMenuPlugin.PluginGuid);
		RuntimeUpdatePatch.Install(_harmony, _log, this);
		MinimapLayoutInputPatch.Install(_harmony, _log);
		MovementSimulationPatch.Install(_harmony, _log);
		FlightViewmodelPatch.Install(_harmony, _log);
		InventoryArrangePatch.Install(_harmony, _log, _inventoryArrange);
		PointToolInteractionPatch.Install(_harmony, _log);
		WorkshopLocalizationPatch.Install(_harmony, _log);
		SnapshotCraftingPatch.Install(_harmony, _log);
		ZombieRegionRetentionPatch.Install(_harmony, _log);
		try
		{
			MethodInfo target = (from method in AccessTools.GetDeclaredMethods(typeof(PlayerLife))
				where method.Name == "doDamage" && method.ReturnType == typeof(void)
				orderby method.GetParameters().Length descending
				select method).FirstOrDefault();
			if (target == null)
			{
				throw new MissingMethodException(typeof(PlayerLife).FullName, "doDamage");
			}
			MethodInfo prefix = AccessTools.Method(typeof(GodModePatch), "Prefix");
			_harmony.Patch(target, new HarmonyMethod(prefix));
			_log?.LogInfo($"无敌补丁已注册：{target.DeclaringType?.FullName}.{target.Name}({target.GetParameters().Length} 参数)。");
		}
		catch (Exception exception)
		{
			_log?.LogError($"无敌补丁注册失败；其他菜单功能仍可使用。\n{exception}");
		}
	}

	internal void SubscribeLifecycle()
	{
		ThrowIfDisposed();
		if (_subscriptionsInstalled)
		{
			return;
		}
		Assets.onAssetsRefreshed = (AssetsRefreshed)Delegate.Combine(
			Assets.onAssetsRefreshed, new AssetsRefreshed(HandleAssetsRefreshed));
		SceneManager.sceneLoaded += HandleSceneLoaded;
		SceneManager.activeSceneChanged += HandleActiveSceneChanged;
		_inputCapture.NativeUiChanged += HandleNativeUiChanged;
		_inputCapture.StateChanged += HandleInputOwnershipStateChanged;
		Application.quitting += HandleApplicationQuitting;
		_subscriptionsInstalled = true;
	}

	internal void Update()
	{
		if (_disposed || _lastRuntimeUpdateFrame == Time.frameCount)
		{
			return;
		}
		_lastRuntimeUpdateFrame = Time.frameCount;
		try
		{
			_workshopLocalization?.Pump();
			_hasLoggedWorkshopPumpFailure = false;
		}
		catch (Exception exception)
		{
			if (!_hasLoggedWorkshopPumpFailure)
			{
				_hasLoggedWorkshopPumpFailure = true;
				_log?.LogError("[Runtime] Workshop localization Pump failed; the remaining runtime graph will continue.\n" + exception);
			}
		}
		if (!_hasLoggedUpdateCallback)
		{
			_hasLoggedUpdateCallback = true;
			_log?.LogInfo("[Runtime] static game Update bridge has run; shortcut polling is enabled.");
		}
		_plugin.ObserveRuntimeSession();
		bool gameplayAllowed = _plugin.CanExecuteGameplay;

		if (_icons == null || !_icons.PumpOne())
		{
			_vehicleIconRenderer?.PumpOne();
		}
		OverlayIconBinder.PumpPending();
		_uiRoot?.Pump();
		_ui?.PumpDeferredDestroy();
		if (_workshopLocalization != null)
		{
			int snapshotVersion = _workshopLocalization.SnapshotVersion;
			int presentationVersion = _workshopLocalization.PresentationVersion;
			if (snapshotVersion != _lastWorkshopSnapshotVersion)
			{
				_lastWorkshopSnapshotVersion = snapshotVersion;
				_lastWorkshopPresentationVersion = presentationVersion;
				_ui?.OnLocalizedAssetViewsRefreshed();
			}
			else if (presentationVersion != _lastWorkshopPresentationVersion)
			{
				_lastWorkshopPresentationVersion = presentationVersion;
				_ui?.OnWorkshopLocalizationPresentationChanged();
			}
		}
		bool pluginWindowOpen = _plugin.IsMenuOpen || _plugin.IsInfiniteStorageOpen || _plugin.IsMinimapLayoutEditActive;
		bool windowAllowed = (_plugin.IsMenuOpen && _plugin.CanOpenClientMenu) ||
			(_plugin.IsInfiniteStorageOpen && _plugin.CanUseSnapshotStorage) ||
			(_plugin.IsMinimapLayoutEditActive && gameplayAllowed);
		if (pluginWindowOpen && windowAllowed)
		{
			_inputCapture.Maintain(!(_ui?.IsCapturingKeyboardInput ?? false));
		}
		bool nativeUiCoexistence = _inputCapture.State == UiInputOwnershipState.NativeUiCoexistence;
		_shortcutManager?.ProcessUpdate(
			Time.frameCount,
			Time.realtimeSinceStartup,
			nativeUiCoexistence || (_ui?.IsCapturingKeyboardInput ?? false));
		if (_plugin.IsInfiniteStorageOpen && _infiniteStorageRefreshFrame >= 0 && Time.frameCount >= _infiniteStorageRefreshFrame)
		{
			_infiniteStorageRefreshFrame = -1;
			if (_catalog?.IsDirty == true)
			{
				_plugin.RefreshCatalog();
			}
		}
		_inventoryArrange?.OnPlayerInitialized();
		_inventoryArrange?.PumpQuickTransfer();
		NearbyItemSnapshot sharedItemSnapshot = RefreshSharedItemSnapshot(
			_plugin.IsMenuOpen || _plugin.IsInfiniteStorageOpen, Time.realtimeSinceStartup);
		_minimap?.Update(Time.realtimeSinceStartup, sharedItemSnapshot);
		try
		{
			_infiniteStorage?.Pump();
			_hasLoggedInfiniteStoragePumpFailure = false;
		}
		catch (Exception exception)
		{
			if (!_hasLoggedInfiniteStoragePumpFailure)
			{
				_hasLoggedInfiniteStoragePumpFailure = true;
				_log?.LogError("[Runtime] InfiniteStorage Pump failed; downstream runtime features remain active.\n" + exception);
			}
		}
		_autoPickup?.Pump(_plugin.IsMenuOpen || _plugin.IsInfiniteStorageOpen, sharedItemSnapshot);

		if (_plugin.IsMenuOpen && !_plugin.CanOpenClientMenu)
		{
			_plugin.SetMenuOpen(false);
		}
		if (_plugin.IsInfiniteStorageOpen && !_plugin.CanUseSnapshotStorage)
		{
			_plugin.SetInfiniteStorageOpen(false);
		}
		if (_plugin.IsMenuOpen || _plugin.IsInfiniteStorageOpen)
		{
			if (!nativeUiCoexistence)
			{
				_ui?.Maintain();
			}
		}

		if (!gameplayAllowed)
		{
			_movement?.Restore();
			_pointTool?.Pump();
			_creatureHealthBars?.Pump();
		}
		else
		{
			_movement?.Pump();
			_pointTool?.Pump();
			_creatureHealthBars?.Pump();
			_pointToolHud?.SetMovement(_movement?.FlightEnabled ?? false, _movement?.NoclipEnabled ?? false, _movement?.FlightSpeed ?? 0f);
		}
		if (Time.unscaledTime >= _nextMaintenanceTime)
		{
			_nextMaintenanceTime = Time.unscaledTime + 0.2f;
			ZombieRegionRetentionPatch.Maintain();
			_actions?.MaintainEnabledCheats();
		}
	}

	internal void LateUpdate()
	{
		if (_disposed || _lastRuntimeLateUpdateFrame == Time.frameCount)
		{
			return;
		}
		_lastRuntimeLateUpdateFrame = Time.frameCount;
		_creatureHealthBars?.RenderLate();
		_minimap?.LateUpdate();
	}

	private NearbyItemSnapshot RefreshSharedItemSnapshot(bool pluginWindowOpen, float now)
	{
		Player player = Player.LocalPlayer;
		if (_nearbyItemIndex == null || player == null || !_plugin.CanExecuteGameplay)
		{
			_sharedItemSnapshot = null;
			return null;
		}
		bool minimapDemand = _minimap?.NeedsItemScan == true;
		float autoRadius = 0f;
		float autoFrequency = 0f;
		bool autoDemand = _autoPickup?.TryGetItemScanDemand(pluginWindowOpen, out autoRadius, out autoFrequency) == true;
		if (!minimapDemand && !autoDemand)
		{
			_sharedItemSnapshot = null;
			return null;
		}
		float radius = 0f;
		float frequency = 0f;
		if (minimapDemand)
		{
			radius = _minimap.ItemScanRadius;
			frequency = _minimap.ItemScanFrequencyHz;
		}
		if (autoDemand)
		{
			radius = Mathf.Max(radius, autoRadius);
			frequency = Mathf.Max(frequency, autoFrequency);
		}
		bool insufficientRadius = _sharedItemSnapshot == null || _sharedItemSnapshot.Radius + 0.001f < radius;
		bool movedOutsideEnvelope = _sharedItemSnapshot != null &&
			(player.transform.position - _sharedItemSnapshot.Center).sqrMagnitude > 1f;
		if (now >= _nextSharedItemScanTime || insufficientRadius || movedOutsideEnvelope)
		{
			_sharedItemSnapshot = _nearbyItemIndex.Refresh(player.transform.position, radius, ++_sharedItemSnapshotVersion);
			_nextSharedItemScanTime = now + 1f / Mathf.Max(1f, frequency);
		}
		return _sharedItemSnapshot;
	}

	internal void ScheduleInfiniteStorageRefresh(bool catalogIsDirty)
	{
		_infiniteStorageRefreshFrame = catalogIsDirty ? Time.frameCount + 1 : -1;
	}

	internal void CancelInfiniteStorageRefresh()
	{
		_infiniteStorageRefreshFrame = -1;
	}

	internal void SynchronizeWorkshopVersions()
	{
		_lastWorkshopSnapshotVersion = _workshopLocalization?.SnapshotVersion ?? _lastWorkshopSnapshotVersion;
		_lastWorkshopPresentationVersion = _workshopLocalization?.PresentationVersion ?? _lastWorkshopPresentationVersion;
	}

	private void HandleAssetsRefreshed()
	{
		if (!_disposed)
		{
			_plugin.HandleAssetsRefreshed();
		}
	}

	private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		HandleSceneTransition($"sceneLoaded:{scene.name}:{mode}");
	}

	private void HandleActiveSceneChanged(Scene previous, Scene current)
	{
		HandleSceneTransition($"activeSceneChanged:{previous.name}->{current.name}");
	}

	private void HandleNativeUiChanged()
	{
		if (!_disposed)
		{
			_uiRoot?.HandleNativeUiChanged();
		}
	}

	private void HandleInputOwnershipStateChanged(UiInputOwnershipState state)
	{
		if (!_disposed)
		{
			_uiRoot?.SetNativeUiCoexistence(state == UiInputOwnershipState.NativeUiCoexistence);
		}
	}

	private void HandleSceneTransition(string source)
	{
		if (_disposed)
		{
			return;
		}
		_uiRoot?.HandleSceneChanged();
		_ui?.HandleSceneChanged();
		ZombieRegionRetentionPatch.ResetForSceneTransition();
		_minimap?.HandleSceneChanged();
		_sharedItemSnapshot = null;
		_nextSharedItemScanTime = 0f;
		if (_plugin.IsMenuOpen || _plugin.IsInfiniteStorageOpen)
		{
			_inputCapture.Maintain();
		}
		_log?.LogInfo("[Runtime] UI scene references refreshed via " + source + ".");
	}

	internal void Dispose(string reason)
	{
		if (!_teardownGate.TryBegin())
		{
			return;
		}
		_disposed = true;

		RunTeardownStep("scene/assets events", RemoveSubscriptions);
		RunTeardownStep("runtime Update bridge", () => RuntimeUpdatePatch.Reset(this));
		RunTeardownStep("minimap layout input", MinimapLayoutInputPatch.Reset);
		RunTeardownStep("input capture", _inputCapture.Release);
		RunTeardownStep("movement restore", () => _movement?.Restore());
		RunTeardownStep("zombie retention cleanup", ZombieRegionRetentionPatch.ReleaseAll);
		RunTeardownStep("ui", () => _ui?.Dispose());
		_ui = null;
		RunTeardownStep("minimap", () => _minimap?.Dispose());
		_minimap = null;
		RunTeardownStep("ui root", () => _uiRoot?.Dispose());
		_uiRoot = null;
		RunTeardownStep("creature health bars", () => _creatureHealthBars?.Dispose());
		RunTeardownStep("auto pickup", () => _autoPickup?.Dispose());
		RunTeardownStep("infinite storage", () => _infiniteStorage?.Dispose());
		RunTeardownStep("teleport maps", () => _teleportMaps?.Dispose());
		RunTeardownStep("icons", () => _icons?.Clear());
		RunTeardownStep("vehicle icon queue", () => _vehicleIconRenderer?.CancelPending());
		RunTeardownStep("workshop localization", () => _workshopLocalization?.Dispose());
		RunTeardownStep("harmony", () => _harmony?.UnpatchSelf());
		_harmony = null;
		SystemFontProvider.Reset();
		_log?.LogInfo("Runtime teardown completed: " + reason + ".");
	}

	public void Dispose()
	{
		Dispose("runtime context disposed");
	}

	private void RemoveSubscriptions()
	{
		if (!_subscriptionsInstalled)
		{
			return;
		}
		Assets.onAssetsRefreshed = (AssetsRefreshed)Delegate.Remove(
			Assets.onAssetsRefreshed, new AssetsRefreshed(HandleAssetsRefreshed));
		SceneManager.sceneLoaded -= HandleSceneLoaded;
		SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
		_inputCapture.NativeUiChanged -= HandleNativeUiChanged;
		_inputCapture.StateChanged -= HandleInputOwnershipStateChanged;
		Application.quitting -= HandleApplicationQuitting;
		_subscriptionsInstalled = false;
	}

	private void HandleApplicationQuitting()
	{
		Dispose("application quitting");
	}

	private void RunTeardownStep(string name, System.Action action)
	{
		try
		{
			action();
		}
		catch (Exception exception)
		{
			_log?.LogWarning("Teardown step failed (" + name + "): " + exception);
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(PluginRuntimeContext));
		}
	}
}
