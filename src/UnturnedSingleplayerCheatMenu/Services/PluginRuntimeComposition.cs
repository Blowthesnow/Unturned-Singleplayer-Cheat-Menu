using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Runtime;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.Services;

/// <summary>
/// Immutable ownership boundary for the objects created while bootstrapping the plugin runtime.
/// Members may be null when initialization failed partway through; the runtime context still owns
/// and tears down every object that was successfully created before the failure.
/// </summary>
internal sealed class PluginRuntimeComposition
{
	internal PluginRuntimeComposition(
		PluginUiRoot uiRoot,
		CheatMenuOverlayUi ui,
		VehicleIconRenderer vehicleIconRenderer,
		PointToolHud pointToolHud,
		CreatureHealthBarHud creatureHealthBarHud,
		GlobalShortcutManager shortcutManager,
		AssetCatalog catalog,
		IconCache icons,
		TeleportMapService teleportMaps,
		CheatActions actions,
		MovementCheatService movement,
		PointToolService pointTool,
		InventoryArrangeFeature inventoryArrange,
		NearbyItemIndex nearbyItemIndex,
		AutoPickupFeature autoPickup,
		MinimapFeature minimap,
		WorldMaintenanceService worldMaintenance,
		SnapshotInfiniteStorageRuntime infiniteStorage,
		CreatureHealthBarService creatureHealthBars,
		WorkshopLocalizationService workshopLocalization)
	{
		UiRoot = uiRoot;
		Ui = ui;
		VehicleIconRenderer = vehicleIconRenderer;
		PointToolHud = pointToolHud;
		CreatureHealthBarHud = creatureHealthBarHud;
		ShortcutManager = shortcutManager;
		Catalog = catalog;
		Icons = icons;
		TeleportMaps = teleportMaps;
		Actions = actions;
		Movement = movement;
		PointTool = pointTool;
		InventoryArrange = inventoryArrange;
		NearbyItemIndex = nearbyItemIndex;
		AutoPickup = autoPickup;
		Minimap = minimap;
		WorldMaintenance = worldMaintenance;
		InfiniteStorage = infiniteStorage;
		CreatureHealthBars = creatureHealthBars;
		WorkshopLocalization = workshopLocalization;
	}

	internal PluginUiRoot UiRoot { get; }
	internal CheatMenuOverlayUi Ui { get; }
	internal VehicleIconRenderer VehicleIconRenderer { get; }
	internal PointToolHud PointToolHud { get; }
	internal CreatureHealthBarHud CreatureHealthBarHud { get; }
	internal GlobalShortcutManager ShortcutManager { get; }
	internal AssetCatalog Catalog { get; }
	internal IconCache Icons { get; }
	internal TeleportMapService TeleportMaps { get; }
	internal CheatActions Actions { get; }
	internal MovementCheatService Movement { get; }
	internal PointToolService PointTool { get; }
	internal InventoryArrangeFeature InventoryArrange { get; }
	internal NearbyItemIndex NearbyItemIndex { get; }
	internal AutoPickupFeature AutoPickup { get; }
	internal MinimapFeature Minimap { get; }
	internal WorldMaintenanceService WorldMaintenance { get; }
	internal SnapshotInfiniteStorageRuntime InfiniteStorage { get; }
	internal CreatureHealthBarService CreatureHealthBars { get; }
	internal WorkshopLocalizationService WorkshopLocalization { get; }
}
