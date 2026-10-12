using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal interface IShellUiFeature
{
	float UiScale { get; }
	string ShortcutLabel { get; }
	string CurrentMap { get; }
	RuntimeSessionKind SessionKind { get; }
	bool MultiplayerRiskAcknowledged { get; }
	string SelectedFontFamily { get; }
	Vector2 NormalizedWindowPosition { get; }
	Font ResolveFont();
	IReadOnlyList<SystemFontFamilyOption> GetInstalledChineseFontFamilies();
	bool TrySetFontFamily(string fontFamily, out bool persisted, out string error);
	void CloseMenu();
	void CloseInfiniteStorage();
	void AcknowledgeMultiplayerRisk();
	void SetWindowPosition(Vector2 position);
	bool SetLastMainTab(string value);
	bool SetLastTeleportView(string value);
	bool ToggleLanguage();
	string GetShortcutLabel(string actionId);
	string GetShortcutDisplayName(string actionId);
	KeyboardShortcut GetShortcutDefault(string actionId);
	bool SetShortcut(string actionId, KeyboardShortcut shortcut);
}

internal interface ICatalogUiFeature
{
	IReadOnlyList<ItemAsset> Items { get; }
	IReadOnlyList<VehicleAsset> Vehicles { get; }
	int WorkshopVehicleCount { get; }
	VehicleThumbnailRenderSettings VehicleThumbnailSettings { get; }
	Texture2D GetItemIcon(ItemAsset asset);
	Texture2D GetVehicleIcon(VehicleAsset asset);
	bool IsItemFavorite(ItemAsset asset);
	bool IsVehicleFavorite(VehicleAsset asset);
	bool TrySetItemFavorite(ItemAsset asset, bool value);
	bool TrySetVehicleFavorite(VehicleAsset asset, bool value);
	bool GiveItem(ItemAsset asset, byte amount);
	int SpawnVehicles(VehicleAsset asset, int amount);
	bool ApplyVehicleThumbnailSettings(int width, float framing);
	void RefreshCatalog();
	bool TryGetOriginal(object instance, WorkshopTextField field, out string source);
}

internal interface ITeleportUiFeature
{
	string CurrentMap { get; }
	IReadOnlyList<TeleportPoint> Points { get; }
	TeleportMapSnapshot ScanCurrentMap();
	TeleportPoint AddCurrent(string name, TeleportMarkerKind markerKind, string color);
	bool Remove(string id);
	bool TryGetLocalPlayerPosition(out Vector3 position);
	bool TeleportToMapPosition(Vector3 position);
	bool Teleport(TeleportPoint point);
}

internal interface IWorldUiFeature
{
	uint Cycle { get; }
	uint Time { get; }
	bool FreezeTimeEnabled { get; }
	bool FullMoonEnabled { get; }
	string ActiveWeatherName { get; }
	int ItemCount { get; }
	int WorkshopItemCount { get; }
	int VehicleCount { get; }
	int WorkshopVehicleCount { get; }
	void SetTime(uint value);
	void SetDay();
	void SetNight();
	void SetFreezeTime(bool value);
	void SetFullMoon(bool value);
	bool CallAirdrop();
	bool StartRain();
	bool StartSnow();
	void ClearWeather();
	void DisableWeather();
	void RefreshCatalog();
	bool ApplyScenario(WorldScenarioKind scenario);
}

internal interface IWorldMaintenanceUiFeature
{
	int SnapshotVersion { get; }
	WorldMaintenanceDashboardSnapshot CaptureDashboard(float radius);
	WorldMaintenanceProgress CaptureProgress();
	bool TryCreatePreview(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		float radius, bool includeSpecialZombies, out WorldMaintenancePreview preview, out string message);
	bool TryStart(WorldMaintenancePreview preview, out WorldMaintenancePreview currentPreview, out string message);
	void Stop();
}

internal interface IInfiniteStorageUiFeature
{
	bool IsAvailable { get; }
	bool Enabled { get; }
	bool PickupPriorityEnabled { get; }
	bool DestructionWarningAcknowledged { get; }
	IReadOnlyList<ItemGroupView> Groups { get; }
	IReadOnlyList<SnapshotModuleAction> ModuleActions { get; }
	void SetEnabled(bool value);
	void SetPickupPriority(bool value);
	int StoreAllCarried();
	long Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long count);
	bool CanWithdraw(ItemGroupIdentity identity);
	bool AcknowledgeDestructionWarning();
	bool CanDestroy(ItemGroupIdentity identity);
	InfiniteStorageDestructionResult Destroy(ItemGroupIdentity identity, ItemSnapshot snapshot,
		SnapshotDestructionScope scope);
	bool CanExecuteModule(string id, SnapshotModuleRequest request);
	SnapshotModuleExecutionResult ExecuteModule(string id, SnapshotModuleRequest request);
}

internal interface ITranslationUiFeature
{
	string ApiProtocol { get; }
	string Endpoint { get; }
	string ApiKey { get; }
	string SecretKey { get; }
	string Region { get; }
	string ProjectId { get; }
	string Model { get; }
	string TargetLanguage { get; }
	int BatchSize { get; }
	bool IsCloudProvider { get; }
	bool IsLargeModelProvider { get; }
	bool RequiresSecretKey { get; }
	bool RequiresRegion { get; }
	WorkshopLocalizationProgress Progress { get; }
	bool IsAvailable { get; }
	bool Enabled { get; }
	bool ApplyToNativeUi { get; }
	bool AutoScanOnAssetsRefreshed { get; }
	bool AutoTranslateMissing { get; }
	bool IsTranslationRunning { get; }
	bool ApiConfigured { get; }
	string CachePath { get; }
	bool TryGetOriginal(object instance, WorkshopTextField field, out string source);
	bool TryGetTranslation(object instance, WorkshopTextField field, out string translation);
	void SetApiProtocol(string value);
	void SetEndpoint(string value);
	void SetApiKey(string value);
	void SetSecretKey(string value);
	void SetRegion(string value);
	void SetProjectId(string value);
	void SetModel(string value);
	void SetBatchSize(string value);
	void SetEnabled(bool value);
	void SetApplyToNativeUi(bool value);
	void SetAutoScan(bool value);
	void SetAutoTranslate(bool value);
	void Scan(bool allowAutoTranslate);
	bool Translate(bool force);
	void Cancel();
	void ReloadCache();
}

internal interface IInventoryUiFeature
{
	string CustomKeys { get; }
	InventoryArrangeSettings CaptureSettings();
	void SetPreset(InventorySortPreset value);
	void SetCustomKeys(string value);
	void SetAutoRotate(bool value);
	void SetCrossPage(bool value);
	void SetIncludeBackpackPage(bool value);
	void SetIncludeVestPage(bool value);
	void SetIncludeShirtPage(bool value);
	void SetIncludePantsPage(bool value);
	void SetProtectEquipped(bool value);
	void SetFixHotkeyItems(bool value);
	InventoryArrangeResult Execute(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview);
	InventoryArrangeResult Undo();
}

internal interface IAutoPickupUiFeature
{
	bool IsAvailable { get; }
	AutoPickupSettings Settings { get; }
	AutoPickupRuleDocument Rules { get; }
	AutoPickupDiagnosticsSnapshot Diagnostics { get; }
	void SetEnabled(bool value);
	void SetRange(float value);
	void SetFrequency(float value);
	void SetRequestsPerTick(int value);
	void SetDropBlockMinutes(int value);
	void SetQualityFilterEnabled(bool value);
	void SetMinimumQuality(int value);
	void SetAllowedRarityMask(int value);
	void SetPauseWhenMenuOpen(bool value);
	void SetPauseWhenDead(bool value);
	void SetDiagnostics(AutoPickupDiagnosticsLevel value);
	void SetUseMinimapRange(bool value);
	void SetUseMinimapRarityFilter(bool value);
	AutoPickupRuleLoadResult SetRules(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules);
	AutoPickupRuleLoadResult SetWhitelistEnabled(bool enabled);
	List<ItemAsset> GetCurrentBackpackAssets();
	string GetItemDisplayName(ItemAsset asset);
}

internal interface IMinimapUiFeature
{
	MinimapSettings Settings { get; }
	MinimapBackgroundState BackgroundState { get; }
	OverheadOcclusionDiagnostics OcclusionDiagnostics { get; }
	void UpdateSettings(Action<MinimapSettings> change);
}

internal interface IInteractionUiFeature
{
	bool IsAvailable { get; }
	bool Enabled { get; }
	PointToolMode Mode { get; }
	float Range { get; }
	bool ShowTargetName { get; }
	bool ShowId { get; }
	bool ShowHealth { get; }
	bool CreatureHealthBarEnabled { get; }
	void SetPointToolEnabled(bool value);
	void SetMode(PointToolMode value);
	void SetRange(float value);
	void SetShowTargetName(bool value);
	void SetShowId(bool value);
	void SetShowHealth(bool value);
	void SetCreatureHealthBarEnabled(bool value);
}

internal interface IMovementUiFeature
{
	bool IsAvailable { get; }
	FlightMode Mode { get; }
	bool FlightEnabled { get; }
	bool NoclipEnabled { get; }
	float FlightSpeed { get; }
	float VerticalSpeed { get; }
	bool SafeExit { get; }
	void SetFlight(bool value);
	void SetNoclip(bool value);
	void SetFlightSpeed(float value);
	void SetFlightVerticalSpeed(float value);
	void SetFlightMode(FlightMode value);
	void SetNoclipSafeExit(bool value);
}

/// <summary>
/// Adapts the plugin bootstrap facade to feature-sized UI seams. Presenters never receive the
/// plugin itself and tests can replace any one seam without constructing the full runtime graph.
/// </summary>
internal sealed class PluginUiFeatureAdapter :
	IShellUiFeature,
	ICatalogUiFeature,
	ITeleportUiFeature,
	IWorldUiFeature,
	IWorldMaintenanceUiFeature,
	IInfiniteStorageUiFeature,
	ITranslationUiFeature,
	IInventoryUiFeature,
	IAutoPickupUiFeature,
	IMinimapUiFeature,
	IInteractionUiFeature,
	IMovementUiFeature
{
	private readonly CheatMenuPlugin _plugin;

	internal PluginUiFeatureAdapter(CheatMenuPlugin plugin) =>
		_plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

	private bool EnsureGameplayAccess(string actionId) => _plugin.EnsureGameplayAccess(actionId, "UI");

	float IShellUiFeature.UiScale => _plugin.UiScale;
	string IShellUiFeature.ShortcutLabel => _plugin.ShortcutLabel;
	string IShellUiFeature.CurrentMap => Provider.map ?? string.Empty;
	RuntimeSessionKind IShellUiFeature.SessionKind => _plugin.CurrentSessionState.Kind;
	bool IShellUiFeature.MultiplayerRiskAcknowledged => _plugin.IsMultiplayerRiskAcknowledged;
	string IShellUiFeature.SelectedFontFamily => _plugin.SelectedFontFamily;
	Vector2 IShellUiFeature.NormalizedWindowPosition => _plugin.NormalizedWindowPosition;
	Font IShellUiFeature.ResolveFont() => SystemFontProvider.Resolve(_plugin.PluginLogger);
	IReadOnlyList<SystemFontFamilyOption> IShellUiFeature.GetInstalledChineseFontFamilies() => _plugin.GetInstalledChineseFontFamilies();
	bool IShellUiFeature.TrySetFontFamily(string fontFamily, out bool persisted, out string error) =>
		_plugin.TrySetFontFamily(fontFamily, out persisted, out error);
	void IShellUiFeature.CloseMenu() => _plugin.CloseMenu();
	void IShellUiFeature.CloseInfiniteStorage() => _plugin.CloseInfiniteStorage();
	void IShellUiFeature.AcknowledgeMultiplayerRisk() => _plugin.AcknowledgeMultiplayerRisk();
	void IShellUiFeature.SetWindowPosition(Vector2 position) => _plugin.SetWindowPosition(position);
	bool IShellUiFeature.SetLastMainTab(string value) => _plugin.SetLastMainTab(value);
	bool IShellUiFeature.SetLastTeleportView(string value) => _plugin.SetLastTeleportView(value);
	bool IShellUiFeature.ToggleLanguage() => _plugin.ToggleLanguageFromUi();
	string IShellUiFeature.GetShortcutLabel(string actionId) => _plugin.GetShortcutLabel(actionId);
	string IShellUiFeature.GetShortcutDisplayName(string actionId) => _plugin.GetShortcutDisplayName(actionId);
	KeyboardShortcut IShellUiFeature.GetShortcutDefault(string actionId) => _plugin.GetShortcutDefault(actionId);
	bool IShellUiFeature.SetShortcut(string actionId, KeyboardShortcut shortcut) => _plugin.SetShortcut(actionId, shortcut);

	IReadOnlyList<ItemAsset> ICatalogUiFeature.Items => _plugin.Catalog?.Items?.ToArray() ?? Array.Empty<ItemAsset>();
	IReadOnlyList<VehicleAsset> ICatalogUiFeature.Vehicles => _plugin.Catalog?.Vehicles?.ToArray() ?? Array.Empty<VehicleAsset>();
	int ICatalogUiFeature.WorkshopVehicleCount => _plugin.Catalog?.WorkshopVehicleCount ?? 0;
	VehicleThumbnailRenderSettings ICatalogUiFeature.VehicleThumbnailSettings => _plugin.VehicleThumbnailSettings;
	Texture2D ICatalogUiFeature.GetItemIcon(ItemAsset asset) => _plugin.Icons?.GetItemIcon(asset);
	Texture2D ICatalogUiFeature.GetVehicleIcon(VehicleAsset asset) => _plugin.Icons?.GetVehicleIcon(asset);
	bool ICatalogUiFeature.IsItemFavorite(ItemAsset asset) => _plugin.Favorites?.IsItemFavorite(asset) ?? false;
	bool ICatalogUiFeature.IsVehicleFavorite(VehicleAsset asset) => _plugin.Favorites?.IsVehicleFavorite(asset) ?? false;
	bool ICatalogUiFeature.TrySetItemFavorite(ItemAsset asset, bool value) => _plugin.Favorites?.TrySetItemFavorite(asset, value) ?? false;
	bool ICatalogUiFeature.TrySetVehicleFavorite(VehicleAsset asset, bool value) => _plugin.Favorites?.TrySetVehicleFavorite(asset, value) ?? false;
	bool ICatalogUiFeature.GiveItem(ItemAsset asset, byte amount) =>
		EnsureGameplayAccess("catalog.give-item") && _plugin.Actions.GiveItem(asset, amount);
	int ICatalogUiFeature.SpawnVehicles(VehicleAsset asset, int amount) =>
		EnsureGameplayAccess("catalog.spawn-vehicle") ? _plugin.Actions.SpawnVehicles(asset, amount) : 0;
	bool ICatalogUiFeature.ApplyVehicleThumbnailSettings(int width, float framing) => _plugin.ApplyVehicleThumbnailSettings(width, framing);
	void ICatalogUiFeature.RefreshCatalog() => _plugin.RefreshCatalog();
	bool ICatalogUiFeature.TryGetOriginal(object instance, WorkshopTextField field, out string source)
	{
		source = null;
		return _plugin.WorkshopLocalization?.TryGetOriginal(instance, field, out source) ?? false;
	}

	string ITeleportUiFeature.CurrentMap => Provider.map ?? string.Empty;
	IReadOnlyList<TeleportPoint> ITeleportUiFeature.Points => _plugin.Teleports?.Points?.ToArray() ?? Array.Empty<TeleportPoint>();
	TeleportMapSnapshot ITeleportUiFeature.ScanCurrentMap() => _plugin.TeleportMaps?.ScanCurrentMap();
	TeleportPoint ITeleportUiFeature.AddCurrent(string name, TeleportMarkerKind markerKind, string color) => _plugin.Teleports?.AddCurrent(name, markerKind, color);
	bool ITeleportUiFeature.Remove(string id) => _plugin.Teleports?.Remove(id) ?? false;
	bool ITeleportUiFeature.TryGetLocalPlayerPosition(out Vector3 position)
	{
		Player player = Player.LocalPlayer;
		if (player == null)
		{
			position = default;
			return false;
		}
		position = player.transform.position;
		return true;
	}
	bool ITeleportUiFeature.TeleportToMapPosition(Vector3 position) =>
		EnsureGameplayAccess("teleport.map") && _plugin.Actions.TeleportToMapPosition(position);
	bool ITeleportUiFeature.Teleport(TeleportPoint point) =>
		EnsureGameplayAccess("teleport.saved") && _plugin.Actions.Teleport(point);

	uint IWorldUiFeature.Cycle => Math.Max(1u, LightingManager.cycle);
	uint IWorldUiFeature.Time => LightingManager.time;
	bool IWorldUiFeature.FreezeTimeEnabled => _plugin.Actions.FreezeTimeEnabled;
	bool IWorldUiFeature.FullMoonEnabled => LightingManager.isFullMoon;
	string IWorldUiFeature.ActiveWeatherName => LevelLighting.GetActiveWeatherAsset()?.FriendlyName ?? "无活动天气";
	int IWorldUiFeature.ItemCount => _plugin.Catalog?.Items.Count ?? 0;
	int IWorldUiFeature.WorkshopItemCount => _plugin.Catalog?.WorkshopItemCount ?? 0;
	int IWorldUiFeature.VehicleCount => _plugin.Catalog?.Vehicles.Count ?? 0;
	int IWorldUiFeature.WorkshopVehicleCount => _plugin.Catalog?.WorkshopVehicleCount ?? 0;
	void IWorldUiFeature.SetTime(uint value) { if (EnsureGameplayAccess("world.set-time")) _plugin.Actions.SetTime(value); }
	void IWorldUiFeature.SetDay() { if (EnsureGameplayAccess(GlobalShortcutActions.SetDay)) _plugin.Actions.SetDay(); }
	void IWorldUiFeature.SetNight() { if (EnsureGameplayAccess(GlobalShortcutActions.SetNight)) _plugin.Actions.SetNight(); }
	void IWorldUiFeature.SetFreezeTime(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.ToggleFreezeTime)) _plugin.Actions.SetFreezeTime(value);
	}
	void IWorldUiFeature.SetFullMoon(bool value) { if (EnsureGameplayAccess(GlobalShortcutActions.ToggleFullMoon)) _plugin.Actions.SetFullMoon(value); }
	bool IWorldUiFeature.CallAirdrop() => EnsureGameplayAccess(GlobalShortcutActions.CallAirdrop) && _plugin.Actions.CallAirdrop();
	bool IWorldUiFeature.StartRain() => EnsureGameplayAccess("world.start-rain") && _plugin.Actions.StartRain();
	bool IWorldUiFeature.StartSnow() => EnsureGameplayAccess("world.start-snow") && _plugin.Actions.StartSnow();
	void IWorldUiFeature.ClearWeather() { if (EnsureGameplayAccess(GlobalShortcutActions.ClearWeather)) _plugin.Actions.ClearWeather(); }
	void IWorldUiFeature.DisableWeather() { if (EnsureGameplayAccess(GlobalShortcutActions.DisableWeather)) _plugin.Actions.DisableWeather(); }
	void IWorldUiFeature.RefreshCatalog() => _plugin.RefreshCatalog();
	bool IWorldUiFeature.ApplyScenario(WorldScenarioKind scenario)
	{
		if (!_plugin.CurrentSessionState.IsSupportedGameplay) return false;
		switch (scenario)
		{
			case WorldScenarioKind.ClearNoon:
				_plugin.Actions.SetDay();
				_plugin.Actions.ClearWeather();
				_plugin.Actions.SetFullMoon(false);
				return true;
			case WorldScenarioKind.FullMoonNight:
				_plugin.Actions.SetNight();
				_plugin.Actions.SetFullMoon(true);
				_plugin.Actions.ClearWeather();
				return true;
			case WorldScenarioKind.SnowstormNight:
				_plugin.Actions.SetNight();
				return _plugin.Actions.StartSnow();
			case WorldScenarioKind.RestoreNatural:
				_plugin.Actions.SetFreezeTime(false);
				_plugin.Actions.SetFullMoon(false);
				_plugin.Actions.RestoreNaturalWeather();
				return true;
			default:
				return false;
		}
	}

	WorldMaintenanceDashboardSnapshot IWorldMaintenanceUiFeature.CaptureDashboard(float radius) =>
		_plugin.WorldMaintenance?.CaptureDashboard(radius);
	int IWorldMaintenanceUiFeature.SnapshotVersion => _plugin.WorldMaintenance?.SnapshotVersion ?? 0;
	WorldMaintenanceProgress IWorldMaintenanceUiFeature.CaptureProgress() =>
		_plugin.WorldMaintenance?.CaptureProgress();
	bool IWorldMaintenanceUiFeature.TryCreatePreview(WorldMaintenanceActionKind action,
		WorldMaintenanceScopeKind scope, float radius, bool includeSpecialZombies,
		out WorldMaintenancePreview preview, out string message)
	{
		preview = null;
		message = "世界维护服务不可用。";
		return _plugin.WorldMaintenance?.TryCreatePreview(action, scope, radius, includeSpecialZombies,
			out preview, out message) == true;
	}
	bool IWorldMaintenanceUiFeature.TryStart(WorldMaintenancePreview preview,
		out WorldMaintenancePreview currentPreview, out string message)
	{
		currentPreview = null;
		message = "世界维护服务不可用。";
		return _plugin.WorldMaintenance?.TryStart(preview, out currentPreview, out message) == true;
	}
	void IWorldMaintenanceUiFeature.Stop() => _plugin.WorldMaintenance?.Stop("用户已停止任务；已完成的世界修改不会回滚。");

	bool IInfiniteStorageUiFeature.IsAvailable => _plugin.InfiniteStorage != null && _plugin.CanUseSnapshotStorage;
	bool IInfiniteStorageUiFeature.Enabled => _plugin.InfiniteStorage?.Enabled ?? false;
	bool IInfiniteStorageUiFeature.PickupPriorityEnabled => _plugin.IsInfiniteStoragePickupPriorityEnabled;
	bool IInfiniteStorageUiFeature.DestructionWarningAcknowledged =>
		_plugin.IsInfiniteStorageDestructionWarningAcknowledged;
	IReadOnlyList<ItemGroupView> IInfiniteStorageUiFeature.Groups => _plugin.InfiniteStorage?.Groups?.ToArray() ?? Array.Empty<ItemGroupView>();
	IReadOnlyList<SnapshotModuleAction> IInfiniteStorageUiFeature.ModuleActions => _plugin.InfiniteStorage?.ModuleActions?.ToArray() ?? Array.Empty<SnapshotModuleAction>();
	void IInfiniteStorageUiFeature.SetEnabled(bool value) => _plugin.SetInfiniteStorageEnabled(value);
	void IInfiniteStorageUiFeature.SetPickupPriority(bool value) => _plugin.SetInfiniteStoragePickupPriority(value);
	int IInfiniteStorageUiFeature.StoreAllCarried() => _plugin.InfiniteStorage?.StoreAllCarried() ?? 0;
	long IInfiniteStorageUiFeature.Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long count) =>
		_plugin.InfiniteStorage?.Withdraw(identity, snapshot, count) ?? 0;
	bool IInfiniteStorageUiFeature.CanWithdraw(ItemGroupIdentity identity) =>
		_plugin.InfiniteStorage?.CanWithdraw(identity) ?? false;
	bool IInfiniteStorageUiFeature.AcknowledgeDestructionWarning() =>
		_plugin.AcknowledgeInfiniteStorageDestructionWarning();
	bool IInfiniteStorageUiFeature.CanDestroy(ItemGroupIdentity identity) =>
		_plugin.InfiniteStorage?.CanDestroy(identity) ?? false;
	InfiniteStorageDestructionResult IInfiniteStorageUiFeature.Destroy(ItemGroupIdentity identity,
		ItemSnapshot snapshot, SnapshotDestructionScope scope)
	{
		if (_plugin.InfiniteStorage == null || _plugin.AutoPickup == null)
			return InfiniteStorageDestructionResult.Failed("destruction-service-unavailable");
		return InfiniteStorageDestructionCoordinator.Execute(identity.AssetId, _plugin.AutoPickup.Rules,
			_plugin.SetAutoPickupRules,
			() => _plugin.InfiniteStorage.Destroy(identity, snapshot, scope));
	}
	bool IInfiniteStorageUiFeature.CanExecuteModule(string id, SnapshotModuleRequest request) =>
		_plugin.InfiniteStorage?.CanExecuteModule(id, request) ?? false;
	SnapshotModuleExecutionResult IInfiniteStorageUiFeature.ExecuteModule(string id, SnapshotModuleRequest request) =>
		_plugin.InfiniteStorage?.ExecuteModule(id, request) ?? SnapshotModuleExecutionResult.Failed("storage-unavailable");

	private WorkshopLocalizationService Translation => _plugin.WorkshopLocalization;
	string ITranslationUiFeature.ApiProtocol => Translation?.Settings?.ApiProtocol ?? string.Empty;
	string ITranslationUiFeature.Endpoint => Translation?.Settings?.Endpoint ?? string.Empty;
	string ITranslationUiFeature.ApiKey => Translation?.Settings?.ApiKey ?? string.Empty;
	string ITranslationUiFeature.SecretKey => Translation?.Settings?.SecretKey ?? string.Empty;
	string ITranslationUiFeature.Region => Translation?.Settings?.Region ?? string.Empty;
	string ITranslationUiFeature.ProjectId => Translation?.Settings?.ProjectId ?? string.Empty;
	string ITranslationUiFeature.Model => Translation?.Settings?.Model ?? string.Empty;
	string ITranslationUiFeature.TargetLanguage => Translation?.Settings?.TargetLanguage ?? string.Empty;
	int ITranslationUiFeature.BatchSize => Translation?.Settings?.BatchSize ?? 0;
	bool ITranslationUiFeature.IsCloudProvider => Translation?.Settings?.IsCloudProvider ?? false;
	bool ITranslationUiFeature.IsLargeModelProvider => Translation?.Settings?.IsLargeModelProvider ?? false;
	bool ITranslationUiFeature.RequiresSecretKey => Translation?.Settings?.RequiresSecretKey ?? false;
	bool ITranslationUiFeature.RequiresRegion => Translation?.Settings?.RequiresRegion ?? false;
	WorkshopLocalizationProgress ITranslationUiFeature.Progress => Translation?.Progress ?? new WorkshopLocalizationProgress();
	bool ITranslationUiFeature.IsAvailable => Translation != null;
	bool ITranslationUiFeature.Enabled => Translation?.Enabled ?? false;
	bool ITranslationUiFeature.ApplyToNativeUi => Translation?.ApplyToNativeUi ?? false;
	bool ITranslationUiFeature.AutoScanOnAssetsRefreshed => Translation?.AutoScanOnAssetsRefreshed ?? false;
	bool ITranslationUiFeature.AutoTranslateMissing => Translation?.AutoTranslateMissing ?? false;
	bool ITranslationUiFeature.IsTranslationRunning => Translation?.IsTranslationRunning ?? false;
	bool ITranslationUiFeature.ApiConfigured => Translation?.ApiConfigured ?? false;
	string ITranslationUiFeature.CachePath => Translation?.CachePath ?? string.Empty;
	bool ITranslationUiFeature.TryGetOriginal(object instance, WorkshopTextField field, out string source)
	{
		source = null;
		return Translation?.TryGetOriginal(instance, field, out source) ?? false;
	}
	bool ITranslationUiFeature.TryGetTranslation(object instance, WorkshopTextField field, out string translation)
	{
		translation = null;
		return Translation?.TryGetTranslation(instance, field, out translation) ?? false;
	}
	void ITranslationUiFeature.SetApiProtocol(string value) => _plugin.SetWorkshopLocalizationApiProtocol(value);
	void ITranslationUiFeature.SetEndpoint(string value) => _plugin.SetWorkshopLocalizationEndpoint(value);
	void ITranslationUiFeature.SetApiKey(string value) => _plugin.SetWorkshopLocalizationApiKey(value);
	void ITranslationUiFeature.SetSecretKey(string value) => _plugin.SetWorkshopLocalizationSecretKey(value);
	void ITranslationUiFeature.SetRegion(string value) => _plugin.SetWorkshopLocalizationRegion(value);
	void ITranslationUiFeature.SetProjectId(string value) => _plugin.SetWorkshopLocalizationProjectId(value);
	void ITranslationUiFeature.SetModel(string value) => _plugin.SetWorkshopLocalizationModel(value);
	void ITranslationUiFeature.SetBatchSize(string value) => _plugin.SetWorkshopLocalizationBatchSize(value);
	void ITranslationUiFeature.SetEnabled(bool value) => _plugin.SetWorkshopLocalizationEnabled(value);
	void ITranslationUiFeature.SetApplyToNativeUi(bool value) => _plugin.SetWorkshopLocalizationApplyToNativeUi(value);
	void ITranslationUiFeature.SetAutoScan(bool value) => _plugin.SetWorkshopLocalizationAutoScan(value);
	void ITranslationUiFeature.SetAutoTranslate(bool value) => _plugin.SetWorkshopLocalizationAutoTranslate(value);
	void ITranslationUiFeature.Scan(bool allowAutoTranslate) => _plugin.ScanWorkshopLocalization(allowAutoTranslate);
	bool ITranslationUiFeature.Translate(bool force) => _plugin.TranslateWorkshopLocalization(force);
	void ITranslationUiFeature.Cancel() => _plugin.CancelWorkshopLocalization();
	void ITranslationUiFeature.ReloadCache() => _plugin.ReloadWorkshopLocalizationCache();

	string IInventoryUiFeature.CustomKeys => _plugin.InventoryArrangeCustomKeys;
	InventoryArrangeSettings IInventoryUiFeature.CaptureSettings() => _plugin.GetInventoryArrangeSettings();
	void IInventoryUiFeature.SetPreset(InventorySortPreset value) => _plugin.SetInventoryArrangePreset(value);
	void IInventoryUiFeature.SetCustomKeys(string value) => _plugin.SetInventoryArrangeCustomKeys(value);
	void IInventoryUiFeature.SetAutoRotate(bool value) => _plugin.SetInventoryArrangeAutoRotate(value);
	void IInventoryUiFeature.SetCrossPage(bool value) => _plugin.SetInventoryArrangeCrossPage(value);
	void IInventoryUiFeature.SetIncludeBackpackPage(bool value) => _plugin.SetInventoryArrangeIncludeBackpackPage(value);
	void IInventoryUiFeature.SetIncludeVestPage(bool value) => _plugin.SetInventoryArrangeIncludeVestPage(value);
	void IInventoryUiFeature.SetIncludeShirtPage(bool value) => _plugin.SetInventoryArrangeIncludeShirtPage(value);
	void IInventoryUiFeature.SetIncludePantsPage(bool value) => _plugin.SetInventoryArrangeIncludePantsPage(value);
	void IInventoryUiFeature.SetProtectEquipped(bool value) => _plugin.SetInventoryArrangeProtectEquipped(value);
	void IInventoryUiFeature.SetFixHotkeyItems(bool value) => _plugin.SetInventoryArrangeFixHotkeyItems(value);
	InventoryArrangeResult IInventoryUiFeature.Execute(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview) =>
		preview ? _plugin.InventoryArrange?.Preview(scope, action) :
		(EnsureGameplayAccess("inventory.arrange") ? _plugin.InventoryArrange?.Execute(scope, action, "工具页") : null);
	InventoryArrangeResult IInventoryUiFeature.Undo() =>
		EnsureGameplayAccess("inventory.undo") ? _plugin.InventoryArrange?.Undo() : null;

	bool IAutoPickupUiFeature.IsAvailable => _plugin.AutoPickup != null;
	AutoPickupSettings IAutoPickupUiFeature.Settings => _plugin.AutoPickup?.Settings ?? new AutoPickupSettings();
	AutoPickupRuleDocument IAutoPickupUiFeature.Rules => _plugin.AutoPickup?.Rules ?? new AutoPickupRuleDocument();
	AutoPickupDiagnosticsSnapshot IAutoPickupUiFeature.Diagnostics => _plugin.AutoPickup?.Diagnostics ?? new AutoPickupDiagnosticsSnapshot();
	void IAutoPickupUiFeature.SetEnabled(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.ToggleAutoPickup)) _plugin.SetAutoPickupEnabled(value);
	}
	void IAutoPickupUiFeature.SetRange(float value) => _plugin.SetAutoPickupRange(value);
	void IAutoPickupUiFeature.SetFrequency(float value) => _plugin.SetAutoPickupFrequency(value);
	void IAutoPickupUiFeature.SetRequestsPerTick(int value) => _plugin.SetAutoPickupRequestsPerTick(value);
	void IAutoPickupUiFeature.SetDropBlockMinutes(int value) => _plugin.SetAutoPickupDropBlockMinutes(value);
	void IAutoPickupUiFeature.SetQualityFilterEnabled(bool value) => _plugin.SetAutoPickupQualityFilterEnabled(value);
	void IAutoPickupUiFeature.SetMinimumQuality(int value) => _plugin.SetAutoPickupMinimumQuality(value);
	void IAutoPickupUiFeature.SetAllowedRarityMask(int value) => _plugin.SetAutoPickupAllowedRarityMask(value);
	void IAutoPickupUiFeature.SetPauseWhenMenuOpen(bool value) => _plugin.SetAutoPickupPauseWhenMenuOpen(value);
	void IAutoPickupUiFeature.SetPauseWhenDead(bool value) => _plugin.SetAutoPickupPauseWhenDead(value);
	void IAutoPickupUiFeature.SetDiagnostics(AutoPickupDiagnosticsLevel value) => _plugin.SetAutoPickupDiagnostics(value);
	void IAutoPickupUiFeature.SetUseMinimapRange(bool value) => _plugin.SetAutoPickupUseMinimapRange(value);
	void IAutoPickupUiFeature.SetUseMinimapRarityFilter(bool value) => _plugin.SetAutoPickupUseMinimapRarityFilter(value);
	AutoPickupRuleLoadResult IAutoPickupUiFeature.SetRules(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules) =>
		_plugin.SetAutoPickupRules(enabled, mode, rules);
	AutoPickupRuleLoadResult IAutoPickupUiFeature.SetWhitelistEnabled(bool enabled) => _plugin.SetAutoPickupWhitelistEnabled(enabled);
	List<ItemAsset> IAutoPickupUiFeature.GetCurrentBackpackAssets()
	{
		List<ItemAsset> assets = new List<ItemAsset>();
		PlayerInventory inventory = Player.LocalPlayer?.inventory;
		if (inventory?.items == null)
		{
			return assets;
		}
		HashSet<ushort> seen = new HashSet<ushort>();
		// Unturned's carried inventory starts at SLOTS (the Hands grid). Native
		// auto-pickup fills this page before backpack/clothing pages, so starting
		// at BACKPACK makes the rule picker look empty for those items.
		for (byte page = PlayerInventory.SLOTS; page < PlayerInventory.STORAGE && page < inventory.items.Length; page++)
		{
			SDG.Unturned.Items items = inventory.items[page];
			if (items == null)
			{
				continue;
			}
			for (byte index = 0; index < items.getItemCount(); index++)
			{
				ItemAsset asset = items.getItem(index)?.GetAsset();
				if (asset != null && seen.Add(asset.id))
				{
					assets.Add(asset);
				}
			}
		}
		assets.Sort((left, right) => string.Compare(GetItemDisplayName(left), GetItemDisplayName(right), StringComparison.CurrentCultureIgnoreCase));
		return assets;
	}
	string IAutoPickupUiFeature.GetItemDisplayName(ItemAsset asset) => GetItemDisplayName(asset);

	MinimapSettings IMinimapUiFeature.Settings => _plugin.Minimap?.Settings ?? _plugin.GetMinimapSettings();
	MinimapBackgroundState IMinimapUiFeature.BackgroundState => _plugin.Minimap?.BackgroundState ?? MinimapBackgroundState.Disabled;
	OverheadOcclusionDiagnostics IMinimapUiFeature.OcclusionDiagnostics => _plugin.Minimap?.OcclusionDiagnostics ?? default;
	void IMinimapUiFeature.UpdateSettings(Action<MinimapSettings> change)
	{
		_plugin.UpdateMinimapSettings(change);
	}
	private string GetItemDisplayName(ItemAsset asset)
	{
		return Translation != null && Translation.TryGetTranslation(asset, WorkshopTextField.ItemName, out string translated) &&
			!string.IsNullOrWhiteSpace(translated) ? translated : asset?.FriendlyName ?? string.Empty;
	}

	bool IInteractionUiFeature.IsAvailable => _plugin.PointTool != null;
	bool IInteractionUiFeature.Enabled => _plugin.PointTool?.Enabled ?? false;
	PointToolMode IInteractionUiFeature.Mode => _plugin.PointTool?.Mode ?? PointToolMode.Smart;
	float IInteractionUiFeature.Range => _plugin.PointTool?.Range ?? 100f;
	bool IInteractionUiFeature.ShowTargetName => _plugin.PointTool?.ShowTargetName ?? false;
	bool IInteractionUiFeature.ShowId => _plugin.PointTool?.ShowId ?? false;
	bool IInteractionUiFeature.ShowHealth => _plugin.PointTool?.ShowHealth ?? false;
	bool IInteractionUiFeature.CreatureHealthBarEnabled => _plugin.CreatureHealthBarEnabled;
	void IInteractionUiFeature.SetPointToolEnabled(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.TogglePointToolEnabled)) _plugin.SetPointToolEnabled(value);
	}
	void IInteractionUiFeature.SetMode(PointToolMode value) => _plugin.SetPointToolMode(value);
	void IInteractionUiFeature.SetRange(float value) => _plugin.SetPointToolRange(value);
	void IInteractionUiFeature.SetShowTargetName(bool value) => _plugin.SetPointToolShowTargetName(value);
	void IInteractionUiFeature.SetShowId(bool value) => _plugin.SetPointToolShowId(value);
	void IInteractionUiFeature.SetShowHealth(bool value) => _plugin.SetPointToolShowHealth(value);
	void IInteractionUiFeature.SetCreatureHealthBarEnabled(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.ToggleCreatureHealthBars)) _plugin.SetCreatureHealthBarEnabled(value);
	}

	bool IMovementUiFeature.IsAvailable => _plugin.Movement != null;
	FlightMode IMovementUiFeature.Mode => _plugin.Movement?.Mode ?? FlightMode.WorldAxis;
	bool IMovementUiFeature.FlightEnabled => _plugin.Movement?.FlightEnabled ?? false;
	bool IMovementUiFeature.NoclipEnabled => _plugin.Movement?.NoclipEnabled ?? false;
	float IMovementUiFeature.FlightSpeed => _plugin.Movement?.FlightSpeed ?? 1f;
	float IMovementUiFeature.VerticalSpeed => _plugin.Movement?.VerticalSpeed ?? 1f;
	bool IMovementUiFeature.SafeExit => _plugin.Movement?.SafeExit ?? false;
	void IMovementUiFeature.SetFlight(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.ToggleFlight)) _plugin.Movement?.SetFlight(value);
	}
	void IMovementUiFeature.SetNoclip(bool value)
	{
		if (!value || EnsureGameplayAccess(GlobalShortcutActions.ToggleNoclip)) _plugin.Movement?.SetNoclip(value);
	}
	void IMovementUiFeature.SetFlightSpeed(float value) => _plugin.SetFlightSpeed(value);
	void IMovementUiFeature.SetFlightVerticalSpeed(float value) => _plugin.SetFlightVerticalSpeed(value);
	void IMovementUiFeature.SetFlightMode(FlightMode value) => _plugin.SetFlightMode(value);
	void IMovementUiFeature.SetNoclipSafeExit(bool value) => _plugin.SetNoclipSafeExit(value);
}
