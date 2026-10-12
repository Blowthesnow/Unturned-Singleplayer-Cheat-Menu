using System.Collections.Generic;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>
/// Long-lived, Unity-object-free UI state. Page Views may be destroyed and recreated without losing
/// drafts, filters, pagination, selection, or dialog state; persisted navigation is committed by
/// the shell presenter when a value changes.
/// </summary>
internal sealed class UiStateStore
{
	internal readonly ItemFilterState ItemFilter = new ItemFilterState();
	internal readonly ItemFilterState FavoriteItemFilter = new ItemFilterState();
	internal readonly ItemFilterState InfiniteStorageFilter = new ItemFilterState();
	internal readonly HashSet<KeyCode> ShortcutCaptureModifiers = new HashSet<KeyCode>();

	internal CheatMenuOverlayUi.MenuTab ActiveTab;
	internal CheatMenuOverlayUi.ToolsTab ActiveToolsTab;
	internal CheatMenuOverlayUi.FavoriteKind FavoriteKind;
	internal CheatMenuOverlayUi.TeleportView TeleportView;
	internal TeleportMarkerKind TeleportMarkerKind;
	internal Color TeleportMarkerColor = new Color(0.96f, 0.77f, 0.26f, 1f);
	internal Color TeleportColorDraft;
	internal Color AccentColor = CheatMenuStyle.DefaultAccent;

	internal string VehicleQuery = string.Empty;
	internal string VehicleCategory = "全部";
	internal string FavoriteQuery = string.Empty;
	internal string FavoriteVehicleCategory = "全部";
	internal string TeleportName = string.Empty;
	internal string HealthTarget = "100";
	internal string FoodTarget = "100";
	internal string WaterTarget = "100";
	internal string VirusTarget = "100";
	internal string StaminaTarget = "100";
	internal string OxygenTarget = "100";
	internal string ExperienceAmount = "1000";
	internal string ReputationAmount = "100";
	internal string ItemAmountText = "1";
	internal string VehicleAmountText = "1";
	internal string AutoPickupRuleSearch = string.Empty;
	internal string AutoPickupBlacklistPickerSearch = string.Empty;
	internal string ShortcutCaptureActionId = "menu.toggle";

	internal int ItemPage;
	internal int InfiniteStoragePage;
	internal int VehiclePage;
	internal int FavoritePage;
	internal int TeleportPage;
	internal int AutoPickupPage;
	internal int AutoPickupBlacklistPickerPage;
	internal int VehicleThumbnailDraftWidth;
	internal byte GiveAmount = 1;
	internal int SpawnVehicleAmount = 1;
	internal float VehicleThumbnailDraftFraming;
	internal float ShortcutScrollPosition = 1f;
	internal float StatusUntil;
	internal float WorldMaintenanceRadius = WorldMaintenancePolicy.DefaultRadius;

	internal bool TeleportMarkerDrawerOpen;
	internal bool PointToolModeDrawerOpen;
	internal bool InventoryPresetDrawerOpen;
	internal bool InventoryScopeDrawerOpen;
	internal bool WorkshopProtocolDrawerOpen;
	internal bool ShortcutCaptureActive;
	internal bool ItemFilterForFavorites;
	internal bool ItemFilterForInfiniteStorage;
	internal bool WorldMaintenanceIncludeSpecialZombies;
	internal ItemFilterState ItemFilterDraft;
	internal ItemFilterRuleAction AutoPickupPickerAction = ItemFilterRuleAction.Deny;
}
