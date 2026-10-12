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

internal sealed class ShellPresenter
{
	private readonly IShellUiFeature _feature;

	internal ShellPresenter(IShellUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal float UiScale => _feature.UiScale;
	internal string ShortcutLabel => _feature.ShortcutLabel;
	internal string CurrentMap => _feature.CurrentMap;
	internal RuntimeSessionKind SessionKind => _feature.SessionKind;
	internal bool MultiplayerRiskAcknowledged => _feature.MultiplayerRiskAcknowledged;
	internal string SelectedFontFamily => _feature.SelectedFontFamily;
	internal Vector2 NormalizedWindowPosition => _feature.NormalizedWindowPosition;
	internal Font ResolveFont() => _feature.ResolveFont();
	internal IReadOnlyList<SystemFontFamilyOption> GetInstalledChineseFontFamilies() => _feature.GetInstalledChineseFontFamilies();
	internal bool TrySetFontFamily(string fontFamily, out bool persisted, out string error) =>
		_feature.TrySetFontFamily(fontFamily, out persisted, out error);

	internal void CloseMenu() => _feature.CloseMenu();
	internal void CloseInfiniteStorage() => _feature.CloseInfiniteStorage();
	internal void AcknowledgeMultiplayerRisk() => _feature.AcknowledgeMultiplayerRisk();
	internal void SetWindowPosition(Vector2 position) => _feature.SetWindowPosition(position);
	internal bool SetLastMainTab(string value) => _feature.SetLastMainTab(value);
	internal bool SetLastTeleportView(string value) => _feature.SetLastTeleportView(value);
	internal bool ToggleLanguage() => _feature.ToggleLanguage();
	internal string GetShortcutLabel(string actionId) => _feature.GetShortcutLabel(actionId);
	internal string GetShortcutDisplayName(string actionId) => _feature.GetShortcutDisplayName(actionId);
	internal KeyboardShortcut GetShortcutDefault(string actionId) => _feature.GetShortcutDefault(actionId);
	internal bool SetShortcut(string actionId, KeyboardShortcut shortcut) => _feature.SetShortcut(actionId, shortcut);
}

internal sealed class WorldMaintenancePresenter
{
	private readonly IWorldMaintenanceUiFeature _feature;

	internal WorldMaintenancePresenter(IWorldMaintenanceUiFeature feature) =>
		_feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal WorldMaintenanceDashboardSnapshot CaptureDashboard(float radius) => _feature.CaptureDashboard(radius);
	internal int SnapshotVersion => _feature.SnapshotVersion;
	internal WorldMaintenanceProgress CaptureProgress() => _feature.CaptureProgress();
	internal bool TryCreatePreview(WorldMaintenanceActionKind action, WorldMaintenanceScopeKind scope,
		float radius, bool includeSpecialZombies, out WorldMaintenancePreview preview, out string message) =>
		_feature.TryCreatePreview(action, scope, radius, includeSpecialZombies, out preview, out message);
	internal bool TryStart(WorldMaintenancePreview preview,
		out WorldMaintenancePreview currentPreview, out string message) =>
		_feature.TryStart(preview, out currentPreview, out message);
	internal void Stop() => _feature.Stop();
}

internal sealed class CatalogPagePresenter
{
	private readonly ICatalogUiFeature _feature;
	private readonly UiStateStore _state;
	private IReadOnlyList<ItemAsset> _itemResults = Array.Empty<ItemAsset>();
	private IReadOnlyList<VehicleAsset> _vehicleResults = Array.Empty<VehicleAsset>();
	private IReadOnlyList<ItemAsset> _favoriteItemResults = Array.Empty<ItemAsset>();
	private IReadOnlyList<VehicleAsset> _favoriteVehicleResults = Array.Empty<VehicleAsset>();

	internal CatalogPagePresenter(ICatalogUiFeature feature, UiStateStore state)
	{
		_feature = feature ?? throw new ArgumentNullException(nameof(feature));
		_state = state ?? throw new ArgumentNullException(nameof(state));
	}

	internal IReadOnlyList<ItemAsset> Items => _feature.Items;
	internal IReadOnlyList<VehicleAsset> Vehicles => _feature.Vehicles;
	internal IReadOnlyList<ItemAsset> ItemResults => _itemResults;
	internal IReadOnlyList<VehicleAsset> VehicleResults => _vehicleResults;
	internal IReadOnlyList<ItemAsset> FavoriteItemResults => _favoriteItemResults;
	internal IReadOnlyList<VehicleAsset> FavoriteVehicleResults => _favoriteVehicleResults;
	internal int WorkshopVehicleCount => _feature.WorkshopVehicleCount;
	internal VehicleThumbnailRenderSettings VehicleThumbnailSettings => _feature.VehicleThumbnailSettings;

	internal Texture2D GetItemIcon(ItemAsset asset) => _feature.GetItemIcon(asset);
	internal Texture2D GetVehicleIcon(VehicleAsset asset) => _feature.GetVehicleIcon(asset);
	internal bool IsItemFavorite(ItemAsset asset) => _feature.IsItemFavorite(asset);
	internal bool IsVehicleFavorite(VehicleAsset asset) => _feature.IsVehicleFavorite(asset);
	internal bool TrySetItemFavorite(ItemAsset asset, bool value) => _feature.TrySetItemFavorite(asset, value);
	internal bool TrySetVehicleFavorite(VehicleAsset asset, bool value) => _feature.TrySetVehicleFavorite(asset, value);
	internal bool GiveItem(ItemAsset asset, byte amount) => _feature.GiveItem(asset, amount);
	internal int SpawnVehicles(VehicleAsset asset, int amount) => _feature.SpawnVehicles(asset, amount);
	internal bool ApplyVehicleThumbnailSettings(int width, float framing) => _feature.ApplyVehicleThumbnailSettings(width, framing);
	internal void RefreshCatalog() => _feature.RefreshCatalog();

	internal void ProjectItems()
	{
		_itemResults = _feature.Items.Where(asset => MatchesItem(asset, _state.ItemFilter)).ToArray();
		_state.ItemPage = 0;
	}

	internal void ProjectVehicles()
	{
		_vehicleResults = _feature.Vehicles.Where(asset => MatchesVehicle(asset, _state.VehicleCategory, _state.VehicleQuery)).ToArray();
		_state.VehiclePage = 0;
	}

	internal void ProjectFavorites()
	{
		_favoriteItemResults = _feature.Items.Where(asset => _feature.IsItemFavorite(asset) && MatchesItem(asset, _state.FavoriteItemFilter)).ToArray();
		_favoriteVehicleResults = _feature.Vehicles.Where(asset => _feature.IsVehicleFavorite(asset) && MatchesVehicle(asset, _state.FavoriteVehicleCategory, _state.FavoriteQuery)).ToArray();
		_state.FavoritePage = 0;
	}

	internal bool MatchesItem(ItemAsset asset, ItemFilterState filter)
	{
		_feature.TryGetOriginal(asset, WorkshopTextField.ItemName, out string originalName);
		return ItemFilterService.Matches(asset, filter, originalName);
	}

	private bool MatchesVehicle(VehicleAsset asset, string category, string query)
	{
		_feature.TryGetOriginal(asset, WorkshopTextField.VehicleName, out string originalName);
		return (category == "全部" || AssetCatalog.GetVehicleCategory(asset) == category) &&
			AssetCatalog.Matches(asset, asset.FriendlyName, originalName, query);
	}
}

internal sealed class TeleportPagePresenter
{
	private readonly ITeleportUiFeature _feature;

	internal TeleportPagePresenter(ITeleportUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal string CurrentMap => _feature.CurrentMap;
	internal IReadOnlyList<TeleportPoint> Points => _feature.Points;
	internal TeleportMapSnapshot ScanCurrentMap() => _feature.ScanCurrentMap();
	internal TeleportPoint AddCurrent(string name, TeleportMarkerKind markerKind, string color) => _feature.AddCurrent(name, markerKind, color);
	internal bool Remove(string id) => _feature.Remove(id);
	internal bool TryGetLocalPlayerPosition(out Vector3 position) => _feature.TryGetLocalPlayerPosition(out position);
	internal bool TeleportToMapPosition(Vector3 position) => _feature.TeleportToMapPosition(position);
	internal bool Teleport(TeleportPoint point) => _feature.Teleport(point);
}

internal sealed class InfiniteStoragePageSnapshot
{
	internal InfiniteStoragePageSnapshot(IInfiniteStorageUiFeature feature)
	{
		IsAvailable = feature?.IsAvailable ?? false;
		Enabled = feature?.Enabled ?? false;
		PickupPriorityEnabled = feature?.PickupPriorityEnabled ?? false;
		DestructionWarningAcknowledged = feature?.DestructionWarningAcknowledged ?? false;
		Groups = feature?.Groups ?? Array.Empty<ItemGroupView>();
		ModuleActions = feature?.ModuleActions ?? Array.Empty<SnapshotModuleAction>();
	}

	internal bool IsAvailable { get; }
	internal bool Enabled { get; }
	internal bool PickupPriorityEnabled { get; }
	internal bool DestructionWarningAcknowledged { get; }
	internal IReadOnlyList<ItemGroupView> Groups { get; }
	internal IReadOnlyList<SnapshotModuleAction> ModuleActions { get; }
}

internal sealed class InfiniteStoragePagePresenter
{
	private readonly IInfiniteStorageUiFeature _feature;

	internal InfiniteStoragePagePresenter(IInfiniteStorageUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal InfiniteStoragePageSnapshot CaptureSnapshot() => new InfiniteStoragePageSnapshot(_feature);
	internal void SetEnabled(bool value) => _feature.SetEnabled(value);
	internal void SetPickupPriority(bool value) => _feature.SetPickupPriority(value);
	internal int StoreAllCarried() => _feature.StoreAllCarried();
	internal long Withdraw(ItemGroupIdentity identity, ItemSnapshot snapshot, long count) =>
		_feature.Withdraw(identity, snapshot, count);
	internal bool CanWithdraw(ItemGroupIdentity identity) => _feature.CanWithdraw(identity);
	internal bool DestructionWarningAcknowledged => _feature.DestructionWarningAcknowledged;
	internal bool AcknowledgeDestructionWarning() => _feature.AcknowledgeDestructionWarning();
	internal bool CanDestroy(ItemGroupIdentity identity) => _feature.CanDestroy(identity);
	internal InfiniteStorageDestructionResult Destroy(ItemGroupIdentity identity, ItemSnapshot snapshot,
		SnapshotDestructionScope scope) => _feature.Destroy(identity, snapshot, scope);
	internal bool CanExecuteModule(string id, SnapshotModuleRequest request) => _feature.CanExecuteModule(id, request);
	internal SnapshotModuleExecutionResult ExecuteModule(string id, SnapshotModuleRequest request) =>
		_feature.ExecuteModule(id, request);
}

internal sealed class TranslationSettingsSnapshot
{
	internal TranslationSettingsSnapshot(ITranslationUiFeature feature)
	{
		ApiProtocol = feature?.ApiProtocol ?? string.Empty;
		Endpoint = feature?.Endpoint ?? string.Empty;
		ApiKey = feature?.ApiKey ?? string.Empty;
		MachineTranslationSettings parsedSettings = new MachineTranslationSettings { ApiKey = ApiKey };
		ApiKeys = parsedSettings.ApiKeys.ToArray();
		ApiKeyCount = parsedSettings.ApiKeyCount;
		ParallelWorkerCount = parsedSettings.ParallelWorkerCount;
		SecretKey = feature?.SecretKey ?? string.Empty;
		Region = feature?.Region ?? string.Empty;
		ProjectId = feature?.ProjectId ?? string.Empty;
		Model = feature?.Model ?? string.Empty;
		TargetLanguage = feature?.TargetLanguage ?? string.Empty;
		BatchSize = feature?.BatchSize ?? 0;
		IsCloudProvider = feature?.IsCloudProvider ?? false;
		IsLargeModelProvider = feature?.IsLargeModelProvider ?? false;
		RequiresSecretKey = feature?.RequiresSecretKey ?? false;
		RequiresRegion = feature?.RequiresRegion ?? false;
	}

	internal string ApiProtocol { get; }
	internal string Endpoint { get; }
	internal string ApiKey { get; }
	internal IReadOnlyList<string> ApiKeys { get; }
	internal int ApiKeyCount { get; }
	internal int ParallelWorkerCount { get; }
	internal string SecretKey { get; }
	internal string Region { get; }
	internal string ProjectId { get; }
	internal string Model { get; }
	internal string TargetLanguage { get; }
	internal int BatchSize { get; }
	internal bool IsCloudProvider { get; }
	internal bool IsLargeModelProvider { get; }
	internal bool RequiresSecretKey { get; }
	internal bool RequiresRegion { get; }
}

internal sealed class TranslationToolsSnapshot
{
	internal TranslationToolsSnapshot(ITranslationUiFeature feature)
	{
		IsAvailable = feature?.IsAvailable ?? false;
		Settings = new TranslationSettingsSnapshot(feature);
		Progress = feature?.Progress ?? new WorkshopLocalizationProgress();
		Enabled = feature?.Enabled ?? false;
		ApplyToNativeUi = feature?.ApplyToNativeUi ?? false;
		AutoScanOnAssetsRefreshed = feature?.AutoScanOnAssetsRefreshed ?? false;
		AutoTranslateMissing = feature?.AutoTranslateMissing ?? false;
		IsTranslationRunning = feature?.IsTranslationRunning ?? false;
		ApiConfigured = feature?.ApiConfigured ?? false;
		CachePath = feature?.CachePath ?? string.Empty;
	}

	internal bool IsAvailable { get; }
	internal TranslationSettingsSnapshot Settings { get; }
	internal WorkshopLocalizationProgress Progress { get; }
	internal bool Enabled { get; }
	internal bool ApplyToNativeUi { get; }
	internal bool AutoScanOnAssetsRefreshed { get; }
	internal bool AutoTranslateMissing { get; }
	internal bool IsTranslationRunning { get; }
	internal bool ApiConfigured { get; }
	internal string CachePath { get; }
}

internal sealed class TranslationToolsPresenter
{
	private readonly ITranslationUiFeature _feature;

	internal TranslationToolsPresenter(ITranslationUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal TranslationToolsSnapshot CaptureSnapshot() => new TranslationToolsSnapshot(_feature);
	internal bool TryGetOriginal(object instance, WorkshopTextField field, out string source)
	{
		source = null;
		return _feature.TryGetOriginal(instance, field, out source);
	}
	internal bool TryGetTranslation(object instance, WorkshopTextField field, out string translation)
	{
		translation = null;
		return _feature.TryGetTranslation(instance, field, out translation);
	}
	internal void SetApiProtocol(string value) => _feature.SetApiProtocol(value);
	internal void SetEndpoint(string value) => _feature.SetEndpoint(value);
	internal void SetApiKey(string value) => _feature.SetApiKey(value);
	internal void SetSecretKey(string value) => _feature.SetSecretKey(value);
	internal void SetRegion(string value) => _feature.SetRegion(value);
	internal void SetProjectId(string value) => _feature.SetProjectId(value);
	internal void SetModel(string value) => _feature.SetModel(value);
	internal void SetBatchSize(string value) => _feature.SetBatchSize(value);
	internal void SetEnabled(bool value) => _feature.SetEnabled(value);
	internal void SetApplyToNativeUi(bool value) => _feature.SetApplyToNativeUi(value);
	internal void SetAutoScan(bool value) => _feature.SetAutoScan(value);
	internal void SetAutoTranslate(bool value) => _feature.SetAutoTranslate(value);
	internal void Scan(bool allowAutoTranslate = true) => _feature.Scan(allowAutoTranslate);
	internal bool Translate(bool force) => _feature.Translate(force);
	internal void Cancel() => _feature.Cancel();
	internal void ReloadCache() => _feature.ReloadCache();
}

internal sealed class InventoryToolsPresenter
{
	private readonly IInventoryUiFeature _feature;

	internal InventoryToolsPresenter(IInventoryUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal string CustomKeys => _feature.CustomKeys;
	internal InventoryArrangeSettings CaptureSettings() => _feature.CaptureSettings();
	internal void SetPreset(InventorySortPreset value) => _feature.SetPreset(value);
	internal void SetCustomKeys(string value) => _feature.SetCustomKeys(value);
	internal void SetAutoRotate(bool value) => _feature.SetAutoRotate(value);
	internal void SetCrossPage(bool value) => _feature.SetCrossPage(value);
	internal void SetIncludeBackpackPage(bool value) => _feature.SetIncludeBackpackPage(value);
	internal void SetIncludeVestPage(bool value) => _feature.SetIncludeVestPage(value);
	internal void SetIncludeShirtPage(bool value) => _feature.SetIncludeShirtPage(value);
	internal void SetIncludePantsPage(bool value) => _feature.SetIncludePantsPage(value);
	internal void SetProtectEquipped(bool value) => _feature.SetProtectEquipped(value);
	internal void SetFixHotkeyItems(bool value) => _feature.SetFixHotkeyItems(value);
	internal InventoryArrangeResult Execute(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview) => _feature.Execute(scope, action, preview);
	internal InventoryArrangeResult Undo() => _feature.Undo();
}

internal sealed class AutoPickupToolsSnapshot
{
	internal AutoPickupToolsSnapshot(IAutoPickupUiFeature feature)
	{
		IsAvailable = feature?.IsAvailable ?? false;
		Settings = feature?.Settings ?? new AutoPickupSettings();
		Rules = feature?.Rules ?? new AutoPickupRuleDocument();
		Diagnostics = feature?.Diagnostics ?? new AutoPickupDiagnosticsSnapshot();
	}

	internal bool IsAvailable { get; }
	internal AutoPickupSettings Settings { get; }
	internal AutoPickupRuleDocument Rules { get; }
	internal AutoPickupDiagnosticsSnapshot Diagnostics { get; }
}

internal sealed class AutoPickupToolsPresenter
{
	private readonly IAutoPickupUiFeature _feature;

	internal AutoPickupToolsPresenter(IAutoPickupUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal AutoPickupToolsSnapshot CaptureSnapshot() => new AutoPickupToolsSnapshot(_feature);
	internal void SetEnabled(bool value) => _feature.SetEnabled(value);
	internal void SetRange(float value) => _feature.SetRange(value);
	internal void SetFrequency(float value) => _feature.SetFrequency(value);
	internal void SetRequestsPerTick(int value) => _feature.SetRequestsPerTick(value);
	internal void SetDropBlockMinutes(int value) => _feature.SetDropBlockMinutes(value);
	internal void SetQualityFilterEnabled(bool value) => _feature.SetQualityFilterEnabled(value);
	internal void SetMinimumQuality(int value) => _feature.SetMinimumQuality(value);
	internal void SetAllowedRarityMask(int value) => _feature.SetAllowedRarityMask(value);
	internal void SetPauseWhenMenuOpen(bool value) => _feature.SetPauseWhenMenuOpen(value);
	internal void SetPauseWhenDead(bool value) => _feature.SetPauseWhenDead(value);
	internal void SetDiagnostics(AutoPickupDiagnosticsLevel value) => _feature.SetDiagnostics(value);
	internal void SetUseMinimapRange(bool value) => _feature.SetUseMinimapRange(value);
	internal void SetUseMinimapRarityFilter(bool value) => _feature.SetUseMinimapRarityFilter(value);
	internal AutoPickupRuleLoadResult SetRules(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules) =>
		_feature.SetRules(enabled, mode, rules);
	internal AutoPickupRuleLoadResult SetWhitelistEnabled(bool enabled) => _feature.SetWhitelistEnabled(enabled);
	internal List<ItemAsset> GetCurrentBackpackAssets() => _feature.GetCurrentBackpackAssets();
	internal string GetItemDisplayName(ItemAsset asset) => _feature.GetItemDisplayName(asset);
}

internal sealed class MinimapToolsPresenter
{
	private readonly IMinimapUiFeature _feature;

	internal MinimapToolsPresenter(IMinimapUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));
	internal MinimapSettings CaptureSettings() => _feature.Settings;
	internal MinimapBackgroundState BackgroundState => _feature.BackgroundState;
	internal OverheadOcclusionDiagnostics OcclusionDiagnostics => _feature.OcclusionDiagnostics;
	internal void UpdateSettings(Action<MinimapSettings> change) => _feature.UpdateSettings(change);
}

internal sealed class InteractionToolsSnapshot
{
	internal InteractionToolsSnapshot(IInteractionUiFeature feature)
	{
		IsAvailable = feature?.IsAvailable ?? false;
		Enabled = feature?.Enabled ?? false;
		Mode = feature?.Mode ?? PointToolMode.Smart;
		Range = feature?.Range ?? 100f;
		ShowTargetName = feature?.ShowTargetName ?? false;
		ShowId = feature?.ShowId ?? false;
		ShowHealth = feature?.ShowHealth ?? false;
		CreatureHealthBarEnabled = feature?.CreatureHealthBarEnabled ?? false;
	}

	internal bool IsAvailable { get; }
	internal bool Enabled { get; }
	internal PointToolMode Mode { get; }
	internal float Range { get; }
	internal bool ShowTargetName { get; }
	internal bool ShowId { get; }
	internal bool ShowHealth { get; }
	internal bool CreatureHealthBarEnabled { get; }
}

internal sealed class InteractionToolsPresenter
{
	private readonly IInteractionUiFeature _feature;

	internal InteractionToolsPresenter(IInteractionUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal InteractionToolsSnapshot CaptureSnapshot() => new InteractionToolsSnapshot(_feature);
	internal void SetPointToolEnabled(bool value) => _feature.SetPointToolEnabled(value);
	internal void SetMode(PointToolMode value) => _feature.SetMode(value);
	internal void SetRange(float value) => _feature.SetRange(value);
	internal void SetShowTargetName(bool value) => _feature.SetShowTargetName(value);
	internal void SetShowId(bool value) => _feature.SetShowId(value);
	internal void SetShowHealth(bool value) => _feature.SetShowHealth(value);
	internal void SetCreatureHealthBarEnabled(bool value) => _feature.SetCreatureHealthBarEnabled(value);
}

internal sealed class MovementToolsSnapshot
{
	internal MovementToolsSnapshot(IMovementUiFeature feature)
	{
		IsAvailable = feature?.IsAvailable ?? false;
		Mode = feature?.Mode ?? FlightMode.WorldAxis;
		FlightEnabled = feature?.FlightEnabled ?? false;
		NoclipEnabled = feature?.NoclipEnabled ?? false;
		FlightSpeed = feature?.FlightSpeed ?? 1f;
		VerticalSpeed = feature?.VerticalSpeed ?? 1f;
		SafeExit = feature?.SafeExit ?? false;
	}

	internal bool IsAvailable { get; }
	internal FlightMode Mode { get; }
	internal bool FlightEnabled { get; }
	internal bool NoclipEnabled { get; }
	internal float FlightSpeed { get; }
	internal float VerticalSpeed { get; }
	internal bool SafeExit { get; }
}

internal sealed class MovementToolsPresenter
{
	private readonly IMovementUiFeature _feature;

	internal MovementToolsPresenter(IMovementUiFeature feature) => _feature = feature ?? throw new ArgumentNullException(nameof(feature));

	internal MovementToolsSnapshot CaptureSnapshot() => new MovementToolsSnapshot(_feature);
	internal void SetFlight(bool value) => _feature.SetFlight(value);
	internal void SetNoclip(bool value) => _feature.SetNoclip(value);
	internal void SetFlightSpeed(float value) => _feature.SetFlightSpeed(value);
	internal void SetFlightVerticalSpeed(float value) => _feature.SetFlightVerticalSpeed(value);
	internal void SetFlightMode(FlightMode value) => _feature.SetFlightMode(value);
	internal void SetNoclipSafeExit(bool value) => _feature.SetNoclipSafeExit(value);
}
