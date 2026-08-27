using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.AutoPickup;
using UnturnedSingleplayerCheatMenu.InventoryArrange;
using UnturnedSingleplayerCheatMenu.InfiniteStorage;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class CheatMenuOverlayUi : IDisposable
{
	private enum MenuTab
	{
		Character,
		Items,
		Vehicles,
		Favorites,
		Teleports,
		Tools,
		Other
	}

	private enum FavoriteKind
	{
		Items,
		Vehicles
	}

	private enum TeleportView
	{
		Map,
		Points
	}

	private enum StatusKind
	{
		Info,
		Success,
		Warning,
		Error
	}

	private sealed class ColorSwatchControl
	{
		public Color Color;

		public GameObject Check;
	}

	private enum ToolsTab
	{
		Interaction,
		Inventory,
		AutoPickup,
		Movement,
		Translation
	}

	private const int OverlaySortingOrder = 29900;

	private const int CardsPerOverlayPage = 15;

	private const int TeleportsPerOverlayPage = 6;

	private const string KeyboardGlyphLabel = "__keyboard_glyph__";

	private const string ProjectUrl = "https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu";

	private const string SponsorUrl = "https://afdian.com/a/Blowthesnow";

	private static readonly ItemOriginFilter[] ItemOriginFilters = new ItemOriginFilter[3]
	{
		ItemOriginFilter.Official,
		ItemOriginFilter.Workshop,
		ItemOriginFilter.MapOrOther
	};

	private static readonly EItemRarity[] ItemRarities = new EItemRarity[6]
	{
		EItemRarity.COMMON,
		EItemRarity.UNCOMMON,
		EItemRarity.RARE,
		EItemRarity.EPIC,
		EItemRarity.LEGENDARY,
		EItemRarity.MYTHICAL
	};

	private static readonly ESlotType[] ItemSlots = new ESlotType[4]
	{
		ESlotType.NONE,
		ESlotType.PRIMARY,
		ESlotType.SECONDARY,
		ESlotType.ANY
	};

	private static readonly EAction[] GunActions = new EAction[8]
	{
		EAction.Trigger,
		EAction.Bolt,
		EAction.Pump,
		EAction.Break,
		EAction.String,
		EAction.Rocket,
		EAction.Rail,
		EAction.Minigun
	};

	private static readonly GunFireModeFilter[] GunFireModes = new GunFireModeFilter[3]
	{
		GunFireModeFilter.Semi,
		GunFireModeFilter.Auto,
		GunFireModeFilter.Burst
	};

	private static readonly string[] VehicleCategories = new string[8] { "全部", "陆地车辆", "固定翼飞机", "直升机", "飞艇", "船只", "火车", "其他" };

	private static readonly TeleportMarkerKind[] MarkerKinds = new TeleportMarkerKind[4]
	{
		TeleportMarkerKind.Star,
		TeleportMarkerKind.Square,
		TeleportMarkerKind.Circle,
		TeleportMarkerKind.Diamond
	};

	private static readonly string[] TeleportMarkerColorPresets = new string[18]
	{
		"#252525", "#F7F5EF", "#1E6BB8", "#4BA3DF", "#2D8036", "#9AC65A", "#C27B10", "#F1BF45", "#AF3030", "#E94A4A",
		"#CB5A97", "#8659D4", "#D5D4CC", "#8C8D86", "#CDE5FA", "#F8ECD7", "#1C425A", "#4B3917"
	};

	private readonly CheatMenuPlugin _plugin;

	private readonly ItemFilterState _itemFilter = new ItemFilterState();

	private readonly ItemFilterState _favoriteItemFilter = new ItemFilterState();

	private readonly ItemFilterState _infiniteStorageFilter = new ItemFilterState();

	private readonly List<ItemAsset> _itemResults = new List<ItemAsset>();

	private readonly List<VehicleAsset> _vehicleResults = new List<VehicleAsset>();

	private readonly List<ItemAsset> _favoriteItemResults = new List<ItemAsset>();

	private readonly List<VehicleAsset> _favoriteVehicleResults = new List<VehicleAsset>();

	private readonly Dictionary<ushort, ItemAsset> _infiniteStorageAssetsById = new Dictionary<ushort, ItemAsset>();

	private readonly List<GameObject> _deferredDestroy = new List<GameObject>();

	private readonly Dictionary<MenuTab, Button> _tabButtons = new Dictionary<MenuTab, Button>();

	private readonly Dictionary<TeleportMarkerKind, Sprite> _teleportMarkerSprites = new Dictionary<TeleportMarkerKind, Sprite>();

	private readonly Dictionary<TeleportMarkerKind, Texture2D> _teleportMarkerTextures = new Dictionary<TeleportMarkerKind, Texture2D>();

	private readonly UiDebouncer _searchDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _timeApplyDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _autoPickupRuleSearchDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _autoPickupRangeDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _autoPickupFrequencyDebouncer = new UiDebouncer(0.15f);

	private readonly AmmoCompatibilityIndex _ammoCompatibilityIndex = new AmmoCompatibilityIndex();

	private GameObject _root;

	private GameObject _infiniteStorageRoot;

	private RectTransform _infiniteStoragePanel;

	private GameObject _infiniteStorageContentHost;

	private GameObject _hoverExplanationRoot;

	private Text _hoverExplanationText;

	private GameObject _ownedEventSystem;

	private RectTransform _panel;

	private GameObject _contentHost;

	private Text _mapAndShortcutText;

	private Text _statusText;

	private Image _statusAccent;

	private TeleportMapSurface _teleportMapSurface;

	private Text _timeCurrentText;

	private Text _timePreviewText;

	private Slider _timeSlider;

	private InputField _itemAmountInput;

	private InputField _vehicleAmountInput;

	private GameObject _confirmRoot;

	private GameObject _aboutRoot;

	private GameObject _shortcutRoot;

	private ScrollRect _shortcutScrollRect;

	private RectTransform _shortcutScrollContent;

	private float _shortcutScrollPosition = 1f;

	private Text _shortcutCaptureText;

	private readonly HashSet<KeyCode> _shortcutCaptureModifiers = new HashSet<KeyCode>();

	private bool _shortcutCaptureActive;

	private string _shortcutCaptureActionId = "menu.toggle";

	private GameObject _itemFilterRoot;

	private ItemFilterState _itemFilterDraft;

	private bool _itemFilterForFavorites;

	private bool _itemFilterForInfiniteStorage;

	private GameObject _vehicleThumbnailSettingsRoot;

	private Slider _vehicleThumbnailFramingSlider;

	private Text _vehicleThumbnailFramingText;

	private int _vehicleThumbnailDraftWidth;

	private float _vehicleThumbnailDraftFraming;

	private Font _font;

	private Texture2D _sliderGripTexture;

	private Texture2D _closeGlyphTexture;

	private Texture2D _keyboardGlyphTexture;

	private Texture2D _keyboardGlyphKeysTexture;

	private Texture2D _favoriteFilledTexture;

	private Texture2D _favoriteOutlineTexture;

	private Sprite _sliderGripSprite;

	private Sprite _closeGlyphSprite;

	private Sprite _keyboardGlyphSprite;

	private Sprite _keyboardGlyphKeysSprite;

	private Sprite _favoriteFilledSprite;

	private Sprite _favoriteOutlineSprite;

	private MenuTab _activeTab;

	private string _vehicleQuery = string.Empty;

	private string _vehicleCategory = "全部";

	private string _favoriteQuery = string.Empty;

	private string _favoriteVehicleCategory = "全部";

	private string _teleportName = string.Empty;

	private string _lastHeaderMap = string.Empty;

	private string _lastHeaderShortcut = string.Empty;

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

	private int _infiniteStoragePage;

	private int _vehiclePage;

	private int _favoritePage;

	private int _teleportPage;

	private FavoriteKind _favoriteKind;

	private TeleportView _teleportView;

	private TeleportMarkerKind _teleportMarkerKind;

	private Color _teleportMarkerColor = new Color(0.96f, 0.77f, 0.26f, 1f);

	private bool _teleportMarkerDrawerOpen;

	private GameObject _teleportMarkerDrawerRoot;

	private RectTransform _teleportMarkerDrawerPopup;

	private Button _teleportMarkerTriggerButton;

	private GameObject _teleportColorPickerRoot;

	private Color _teleportColorDraft;

	private Image _teleportColorPreview;

	private Text _teleportColorHexPreview;

	private InputField _teleportColorHexInput;

	private Slider _teleportColorRedSlider;

	private Slider _teleportColorGreenSlider;

	private Slider _teleportColorBlueSlider;

	private Text _teleportColorRedValue;

	private Text _teleportColorGreenValue;

	private Text _teleportColorBlueValue;

	private readonly List<ColorSwatchControl> _teleportColorSwatches = new List<ColorSwatchControl>();

	private byte _giveAmount = 1;

	private int _spawnVehicleAmount = 1;

	private float _statusUntil;

	private ToolsTab _activeToolsTab;

	private string _autoPickupRuleSearch = string.Empty;

	private int _autoPickupPage;

	private int _autoPickupBlacklistPickerPage;

	private string _autoPickupBlacklistPickerSearch = string.Empty;

	private GameObject _autoPickupBlacklistPickerRoot;

	private GameObject _autoPickupBlacklistPickerHost;

	private ItemFilterRuleAction _autoPickupPickerAction = ItemFilterRuleAction.Deny;

	private static readonly PointToolMode[] PointToolModes = new PointToolMode[6]
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

	private Button _inventoryPresetTriggerButton;

	private bool _inventoryScopeDrawerOpen;

	private GameObject _inventoryScopeDrawerRoot;

	private RectTransform _inventoryScopeDrawerPopup;

	private Button _inventoryScopeTriggerButton;

	private bool _workshopProtocolDrawerOpen;

	private GameObject _workshopProtocolDrawerRoot;

	private RectTransform _workshopProtocolDrawerPopup;

	private Button _workshopProtocolTriggerButton;

	private Text _workshopLocalizationStateText;

	private Text _workshopLocalizationSummaryText;

	private Text _workshopLocalizationApiText;

	internal bool IsCapturingShortcut => _shortcutCaptureActive;

	public CheatMenuOverlayUi(CheatMenuPlugin plugin)
	{
		_plugin = plugin;
		_activeTab = ParseMenuTab(plugin.LastMainTab);
		_teleportView = ParseTeleportView(plugin.LastTeleportView);
	}

	public void OnOpened()
	{
		BuildItemResults();
		BuildVehicleResults();
		BuildFavoriteResults();
		_ammoCompatibilityIndex.Rebuild(_plugin.Catalog.Items);
		EnsureShell();
		RefreshHeader();
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		_root.SetActive(value: true);
		ShowTab(_activeTab);
		SetStatus($"自动扫描完成：{_plugin.Catalog.Items.Count} 个物品、{_plugin.Catalog.Vehicles.Count} 辆载具。", 5f, StatusKind.Info);
	}

	internal void OpenInfiniteStorage()
	{
		_ammoCompatibilityIndex.Rebuild(_plugin.Catalog.Items);
		RebuildInfiniteStorageAssetIndex();
		EnsureInfiniteStorageShell();
		_infiniteStorageRoot.SetActive(value: true);
		RebuildInfiniteStoragePage();
	}

	internal void CloseInfiniteStorage()
	{
		HideItemFilterPanel();
		HideHoverExplanation();
		if (_infiniteStorageRoot != null)
		{
			_infiniteStorageRoot.SetActive(value: false);
		}
	}

	public void OnClosed()
	{
		HideConfirmDialog();
		HideAboutDialog();
		HideShortcutDialog();
		HideTeleportMarkerDrawer();
		HideTeleportColorPicker();
		HidePointToolModeDrawer();
		HideInventoryPresetDrawer();
		HideInventoryScopeDrawer();
		HideWorkshopProtocolDrawer();
		HideItemFilterPanel();
		HideAutoPickupBlacklistPicker();
		HideHoverExplanation();
		HideVehicleThumbnailSettings();
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
	}

	public void OnCatalogRefreshed()
	{
		BuildItemResults();
		BuildVehicleResults();
		BuildFavoriteResults();
		_ammoCompatibilityIndex.Rebuild(_plugin.Catalog.Items);
		RebuildInfiniteStorageAssetIndex();
		if (_root != null && _root.activeSelf && (_activeTab == MenuTab.Items || _activeTab == MenuTab.Vehicles || _activeTab == MenuTab.Favorites))
		{
			ShowTab(_activeTab);
		}
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			RebuildInfiniteStoragePage();
		}
	}

	public void OnLocalizedAssetViewsRefreshed()
	{
		BuildItemResults();
		BuildVehicleResults();
		BuildFavoriteResults();
		if (!(_root == null) && _root.activeSelf)
		{
			if (_activeTab == MenuTab.Items || _activeTab == MenuTab.Vehicles || _activeTab == MenuTab.Favorites)
			{
				ShowTab(_activeTab);
			}
			else if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
			{
				RefreshWorkshopLocalizationPage();
			}
		}
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			RebuildInfiniteStoragePage();
		}
	}

	internal void OnVehicleThumbnailSettingsApplied()
	{
		if (!(_root == null) && _root.activeSelf && (_activeTab == MenuTab.Vehicles || (_activeTab == MenuTab.Favorites && _favoriteKind == FavoriteKind.Vehicles)))
		{
			ShowTab(_activeTab);
		}
	}

	public void Maintain()
	{
		FlushDeferredDestroy();
		bool menuActive = _root != null && _root.activeSelf;
		bool infiniteStorageActive = _infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf;
		if (!menuActive && !infiniteStorageActive)
		{
			return;
		}
		_searchDebouncer.Tick();
		_timeApplyDebouncer.Tick();
		_autoPickupRuleSearchDebouncer.Tick();
		_autoPickupRangeDebouncer.Tick();
		_autoPickupFrequencyDebouncer.Tick();
		if (menuActive && _activeTab == MenuTab.Other)
		{
			RefreshOtherTime();
		}
		if (menuActive && _activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		Canvas component = menuActive ? _root.GetComponent<Canvas>() : null;
		if (component != null && component.sortingOrder != 29900)
		{
			component.sortingOrder = 29900;
		}
		if (menuActive && _panel != null)
		{
			_panel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);
		}
		if (infiniteStorageActive && _infiniteStoragePanel != null)
		{
			_infiniteStoragePanel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);
			Canvas storageCanvas = _infiniteStorageRoot.GetComponent<Canvas>();
			if (storageCanvas != null && storageCanvas.sortingOrder != OverlaySortingOrder + 1)
			{
				storageCanvas.sortingOrder = OverlaySortingOrder + 1;
			}
		}
		if (_teleportMarkerDrawerRoot != null)
		{
			PositionTeleportMarkerDrawer();
			if (Input.GetMouseButtonDown(0) && _teleportMarkerTriggerButton != null && _teleportMarkerDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_teleportMarkerDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_teleportMarkerTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideTeleportMarkerDrawer();
			}
		}
		if (menuActive)
		{
			RefreshHeader();
			if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
			{
				RefreshWorkshopLocalizationPageValues();
			}
		}
		if (_activeTab == MenuTab.Teleports && _teleportMapSurface != null && Player.LocalPlayer != null)
		{
			_teleportMapSurface.RefreshPlayerMarker(Player.LocalPlayer.transform.position);
		}
		MaintainPointToolModeDrawer();
		MaintainInventoryPresetDrawer();
		MaintainInventoryScopeDrawer();
		MaintainWorkshopProtocolDrawer();
		if (_shortcutCaptureActive)
		{
			CaptureShortcutInput();
		}
		if (infiniteStorageActive)
		{
			MaintainInfiniteStorage();
		}
		if (_statusUntil > 0f && Time.unscaledTime > _statusUntil)
		{
			_statusUntil = 0f;
			ResetStatus();
		}
	}

	public void Dispose()
	{
		_searchDebouncer.Dispose();
		_timeApplyDebouncer.Dispose();
		_autoPickupRuleSearchDebouncer.Dispose();
		_autoPickupRangeDebouncer.Dispose();
		_autoPickupFrequencyDebouncer.Dispose();
		FlushDeferredDestroy();
		HideHoverExplanation();
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
		}
		if (_infiniteStorageRoot != null)
		{
			UnityEngine.Object.Destroy(_infiniteStorageRoot);
		}
		if (_ownedEventSystem != null)
		{
			UnityEngine.Object.Destroy(_ownedEventSystem);
		}
		if (_font != null)
		{
			UnityEngine.Object.Destroy(_font);
		}
		DestroyGeneratedSprite(ref _sliderGripSprite, ref _sliderGripTexture);
		DestroyGeneratedSprite(ref _closeGlyphSprite, ref _closeGlyphTexture);
		DestroyGeneratedSprite(ref _keyboardGlyphSprite, ref _keyboardGlyphTexture);
		DestroyGeneratedSprite(ref _keyboardGlyphKeysSprite, ref _keyboardGlyphKeysTexture);
		DestroyGeneratedSprite(ref _favoriteFilledSprite, ref _favoriteFilledTexture);
		DestroyGeneratedSprite(ref _favoriteOutlineSprite, ref _favoriteOutlineTexture);
		foreach (Sprite value in _teleportMarkerSprites.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value);
			}
		}
		foreach (Texture2D value2 in _teleportMarkerTextures.Values)
		{
			if (value2 != null)
			{
				UnityEngine.Object.Destroy(value2);
			}
		}
		_teleportMarkerSprites.Clear();
		_teleportMarkerTextures.Clear();
		if (_confirmRoot != null)
		{
			UnityEngine.Object.Destroy(_confirmRoot);
		}
		if (_aboutRoot != null)
		{
			UnityEngine.Object.Destroy(_aboutRoot);
		}
		if (_shortcutRoot != null)
		{
			UnityEngine.Object.Destroy(_shortcutRoot);
		}
		if (_teleportMarkerDrawerRoot != null)
		{
			UnityEngine.Object.Destroy(_teleportMarkerDrawerRoot);
		}
		if (_pointToolModeDrawerRoot != null)
		{
			UnityEngine.Object.Destroy(_pointToolModeDrawerRoot);
		}
		if (_inventoryPresetDrawerRoot != null)
		{
			UnityEngine.Object.Destroy(_inventoryPresetDrawerRoot);
		}
		if (_inventoryScopeDrawerRoot != null)
		{
			UnityEngine.Object.Destroy(_inventoryScopeDrawerRoot);
		}
		if (_autoPickupBlacklistPickerRoot != null)
		{
			UnityEngine.Object.Destroy(_autoPickupBlacklistPickerRoot);
		}
		if (_teleportColorPickerRoot != null)
		{
			UnityEngine.Object.Destroy(_teleportColorPickerRoot);
		}
		_root = null;
		_infiniteStorageRoot = null;
		_infiniteStoragePanel = null;
		_infiniteStorageContentHost = null;
		_ownedEventSystem = null;
		_mapAndShortcutText = null;
		_statusText = null;
		_statusAccent = null;
		_teleportMapSurface = null;
		_timeCurrentText = null;
		_timePreviewText = null;
		_timeSlider = null;
		_itemAmountInput = null;
		_vehicleAmountInput = null;
		_confirmRoot = null;
		_aboutRoot = null;
		_shortcutRoot = null;
		_shortcutScrollRect = null;
		_shortcutScrollContent = null;
		_shortcutCaptureText = null;
		_shortcutCaptureModifiers.Clear();
		_shortcutCaptureActive = false;
		_teleportMarkerDrawerRoot = null;
		_teleportMarkerDrawerPopup = null;
		_teleportMarkerTriggerButton = null;
		_itemFilterRoot = null;
		_itemFilterDraft = null;
		_teleportColorPickerRoot = null;
		_pointToolModeDrawerRoot = null;
		_pointToolModeDrawerPopup = null;
		_pointToolModeTriggerButton = null;
		_inventoryPresetDrawerRoot = null;
		_inventoryPresetDrawerPopup = null;
		_inventoryPresetTriggerButton = null;
		_inventoryScopeDrawerRoot = null;
		_inventoryScopeDrawerPopup = null;
		_inventoryScopeTriggerButton = null;
		_autoPickupBlacklistPickerRoot = null;
		_autoPickupBlacklistPickerHost = null;
		_teleportColorPreview = null;
		_teleportColorHexPreview = null;
		_teleportColorHexInput = null;
		_teleportColorRedSlider = null;
		_teleportColorGreenSlider = null;
		_teleportColorBlueSlider = null;
		_teleportColorRedValue = null;
		_teleportColorGreenValue = null;
		_teleportColorBlueValue = null;
		_teleportColorSwatches.Clear();
		_lastHeaderMap = string.Empty;
		_lastHeaderShortcut = string.Empty;
		_font = null;
	}

	private void EnsureShell()
	{
		if (!(_root != null))
		{
			_font = Font.CreateDynamicFontFromOSFont(new string[4] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 18);
			_root = new GameObject("UnturnedSingleplayerCheatMenu.Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			UnityEngine.Object.DontDestroyOnLoad(_root);
			Canvas component = _root.GetComponent<Canvas>();
			component.renderMode = RenderMode.ScreenSpaceOverlay;
			component.overrideSorting = true;
			component.sortingOrder = 29900;
			CanvasScaler component2 = _root.GetComponent<CanvasScaler>();
			component2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			component2.referenceResolution = new Vector2(1920f, 1080f);
			component2.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			component2.matchWidthOrHeight = 0.5f;
			if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
			{
				_ownedEventSystem = new GameObject("UnturnedSingleplayerCheatMenu.EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
				UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
			}
			GameObject gameObject = CreateObject("Panel", _root.transform, typeof(Image));
			_panel = gameObject.GetComponent<RectTransform>();
			RectTransform panel = _panel;
			Vector2 anchorMin = (_panel.anchorMax = new Vector2(0.5f, 0.5f));
			panel.anchorMin = anchorMin;
			_panel.pivot = new Vector2(0.5f, 0.5f);
			_panel.sizeDelta = new Vector2(1120f, 790f);
			_panel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);
			gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(18, 18, 14, 14);
			verticalLayoutGroup.spacing = 9f;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			BuildHeader(gameObject.transform);
			BuildTabs(gameObject.transform);
			ReplaceContentHost();
			GameObject gameObject2 = CreateObject("Status", gameObject.transform, typeof(Image));
			gameObject2.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
			Outline outline = gameObject2.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.StatusBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			SetLayout(gameObject2, -1f, 34f, -1f, 0f);
			gameObject2.GetComponent<LayoutElement>().minHeight = 34f;
			HorizontalLayoutGroup horizontalLayoutGroup = gameObject2.AddComponent<HorizontalLayoutGroup>();
			horizontalLayoutGroup.padding = new RectOffset(11, 11, 0, 0);
			horizontalLayoutGroup.spacing = 9f;
			horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
			horizontalLayoutGroup.childControlWidth = true;
			horizontalLayoutGroup.childControlHeight = true;
			horizontalLayoutGroup.childForceExpandWidth = false;
			horizontalLayoutGroup.childForceExpandHeight = false;
			GameObject gameObject3 = CreateObject("StatusAccent", gameObject2.transform, typeof(Image));
			_statusAccent = gameObject3.GetComponent<Image>();
			_statusAccent.raycastTarget = false;
			SetLayout(gameObject3, 5f, 24f, 0f, 0f);
			_statusText = CreateText(gameObject2.transform, "准备就绪。", 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
			_statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
			_statusText.verticalOverflow = VerticalWrapMode.Truncate;
			SetLayout(_statusText.gameObject, -1f, 24f, 1f, 0f);
			CreateButton(gameObject2.transform, "__keyboard_glyph__", ShowShortcutDialog, 38f, 34f, CheatMenuStyle.Surface);
			CreateButton(gameObject2.transform, "?", ShowAboutDialog, 38f, 34f, CheatMenuStyle.Surface, 18);
			ResetStatus();
		}
	}

	private void EnsureInfiniteStorageShell()
	{
		if (_infiniteStorageRoot != null)
		{
			return;
		}
		if (_font == null)
		{
			_font = Font.CreateDynamicFontFromOSFont(new string[4] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 18);
		}
		if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
		{
			_ownedEventSystem = new GameObject("UnturnedSingleplayerCheatMenu.EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
		}
		_infiniteStorageRoot = new GameObject("UnturnedSingleplayerCheatMenu.InfiniteStorage", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		UnityEngine.Object.DontDestroyOnLoad(_infiniteStorageRoot);
		Canvas canvas = _infiniteStorageRoot.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = OverlaySortingOrder + 1;
		CanvasScaler scaler = _infiniteStorageRoot.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;
		GameObject panel = CreateObject("InfiniteStoragePanel", _infiniteStorageRoot.transform, typeof(Image));
		_infiniteStoragePanel = panel.GetComponent<RectTransform>();
		_infiniteStoragePanel.anchorMin = _infiniteStoragePanel.anchorMax = new Vector2(0.5f, 0.5f);
		_infiniteStoragePanel.pivot = new Vector2(0.5f, 0.5f);
		_infiniteStoragePanel.sizeDelta = new Vector2(1120f, 790f);
		_infiniteStoragePanel.localScale = Vector3.one * CalculateSafeScale(_plugin.UiScale);
		panel.GetComponent<Image>().color = CheatMenuStyle.Panel;
		VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(18, 18, 14, 14);
		layout.spacing = 9f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		GameObject header = CreateRow(panel.transform, 46f, 8f);
		CreateText(header.transform, "无限储存", 22, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(header.transform);
		CreateWindowCloseButton(header.transform, _plugin.CloseInfiniteStorage);
		_infiniteStorageContentHost = CreateObject("InfiniteStorageContentHost", panel.transform, typeof(Image));
		_infiniteStorageContentHost.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
		SetLayout(_infiniteStorageContentHost, -1f, -1f, -1f, 1f);
		AddVerticalLayout(_infiniteStorageContentHost, 9f, new RectOffset(12, 12, 10, 10)).childForceExpandHeight = false;
		_infiniteStorageRoot.SetActive(value: false);
	}

	private void RebuildInfiniteStoragePage()
	{
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf)
		{
			return;
		}
		HideHoverExplanation();
		HideItemFilterPanel();
		if (_infiniteStorageContentHost != null)
		{
			QueueForDestroy(_infiniteStorageContentHost);
		}
		_infiniteStorageContentHost = CreateObject("InfiniteStorageContentHost", _infiniteStoragePanel, typeof(Image));
		_infiniteStorageContentHost.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
		SetLayout(_infiniteStorageContentHost, -1f, -1f, -1f, 1f);
		AddVerticalLayout(_infiniteStorageContentHost, 9f, new RectOffset(12, 12, 10, 10)).childForceExpandHeight = false;
		BuildInfiniteStoragePanel(_infiniteStorageContentHost.transform);
	}

	private void MaintainInfiniteStorage()
	{
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf)
		{
			return;
		}
		if (_itemFilterRoot == null && _infiniteStorageContentHost == null)
		{
			RebuildInfiniteStoragePage();
		}
	}

	private GameObject GetActiveOverlayRoot()
	{
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			return _infiniteStorageRoot;
		}
		return _root;
	}

	private void BuildHeader(Transform parent)
	{
		GameObject gameObject = CreateRow(parent, 46f, 10f);
		SetLayout(CreateText(gameObject.transform, "单人作弊指令菜单", 25, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold).gameObject, (PluginLocalization.CurrentLanguage == PluginLanguage.English) ? 315f : 255f);
		GameObject gameObject2 = CreateObject("SingleplayerBadge", gameObject.transform, typeof(Image));
		gameObject2.GetComponent<Image>().color = new Color(0.06f, 0.3f, 0.19f, 1f);
		SetLayout(gameObject2, 165f, 28f);
		Stretch(CreateText(gameObject2.transform, "仅限 SINGLEPLAYER", 12, new Color(0.55f, 1f, 0.73f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, PluginLocalization.SwitchButtonLabel, delegate
		{
			bool persisted = _plugin.ToggleLanguageFromUi();
			RebuildForLanguageChange(persisted);
		}, 58f, 36f, CheatMenuStyle.Accent);
		_mapAndShortcutText = CreateText(gameObject.transform, string.Empty, 13, CheatMenuStyle.Muted, TextAnchor.MiddleRight);
		SetLayout(_mapAndShortcutText.gameObject, 250f);
		CreateWindowCloseButton(gameObject.transform, delegate
		{
			_plugin.CloseMenu();
		});
	}

	private Button CreateWindowCloseButton(Transform parent, System.Action action)
	{
		Button button = CreateButton(parent, "×", action, 44f, 36f, CheatMenuStyle.Danger, 23);
		OverlaySelectableVisual component = button.GetComponent<OverlaySelectableVisual>();
		component?.SetOutlineVisible(visible: false);
		if ((object)component != null)
		{
			component.SetFocusIndicatorVisible(visible: false);
			return button;
		}
		return button;
	}

	private void RebuildForLanguageChange(bool persisted)
	{
		GameObject root = _root;
		_root = null;
		_panel = null;
		_contentHost = null;
		_mapAndShortcutText = null;
		_statusText = null;
		_statusAccent = null;
		_aboutRoot = null;
		_shortcutRoot = null;
		_shortcutScrollRect = null;
		_shortcutScrollContent = null;
		_shortcutCaptureText = null;
		_shortcutCaptureModifiers.Clear();
		_shortcutCaptureActive = false;
		_lastHeaderMap = string.Empty;
		_lastHeaderShortcut = string.Empty;
		_tabButtons.Clear();
		if (root != null)
		{
			QueueForDestroy(root);
		}
		EnsureShell();
		RefreshHeader();
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		_root.SetActive(value: true);
		ShowTab(_activeTab);
		SetStatus(PluginLocalization.LanguageChangedMessage(persisted), 5f, persisted ? StatusKind.Success : StatusKind.Warning);
	}

	private void RefreshHeader()
	{
		if (!(_mapAndShortcutText == null))
		{
			string text = (string.IsNullOrWhiteSpace(Provider.map) ? PluginLocalization.Translate("未进入地图") : Provider.map);
			string shortcutLabel = _plugin.ShortcutLabel;
			if (!string.Equals(_lastHeaderMap, text, StringComparison.Ordinal) || !string.Equals(_lastHeaderShortcut, shortcutLabel, StringComparison.Ordinal))
			{
				_lastHeaderMap = text;
				_lastHeaderShortcut = shortcutLabel;
				_mapAndShortcutText.text = text + "  ·  " + shortcutLabel;
			}
		}
	}

	private void BuildTabs(Transform parent)
	{
		GameObject gameObject = CreateRow(parent, 43f, 7f);
		AddTab(gameObject.transform, MenuTab.Character, "角色");
		AddTab(gameObject.transform, MenuTab.Items, "物品");
		AddTab(gameObject.transform, MenuTab.Vehicles, "载具");
		AddTab(gameObject.transform, MenuTab.Favorites, "收藏");
		AddTab(gameObject.transform, MenuTab.Teleports, "传送");
		AddTab(gameObject.transform, MenuTab.Tools, "工具");
		AddTab(gameObject.transform, MenuTab.Other, "其他");
	}

	private void AddTab(Transform parent, MenuTab tab, string label)
	{
		Button button = CreateButton(parent, label, delegate
		{
			ShowTab(tab);
		}, 0f, 40f, CheatMenuStyle.Surface, 15);
		SetLayout(button.gameObject, -1f, -1f, 1f);
		_tabButtons[tab] = button;
	}

	private void ShowTab(MenuTab tab)
	{
		_activeTab = tab;
		_plugin.SetLastMainTab(tab.ToString());
		ReplaceContentHost();
		foreach (KeyValuePair<MenuTab, Button> tabButton in _tabButtons)
		{
			Color color = ((tabButton.Key == tab) ? CheatMenuStyle.Accent : CheatMenuStyle.Surface);
			OverlaySelectableVisual component = tabButton.Value.GetComponent<OverlaySelectableVisual>();
			if (component != null)
			{
				component.SetBaseColor(color);
			}
			else
			{
				tabButton.Value.GetComponent<Image>().color = color;
			}
		}
		switch (tab)
		{
		case MenuTab.Character:
			BuildCharacterTab();
			break;
		case MenuTab.Items:
			BuildItemsTab();
			break;
		case MenuTab.Vehicles:
			BuildVehiclesTab();
			break;
		case MenuTab.Favorites:
			BuildFavoritesTab();
			break;
		case MenuTab.Teleports:
			BuildTeleportsTab();
			break;
		case MenuTab.Tools:
			BuildToolsTab();
			break;
		case MenuTab.Other:
			BuildOtherTab();
			break;
		}
	}

	private void ReplaceContentHost()
	{
		HideHoverExplanation();
		HidePointToolModeDrawer();
		HideInventoryPresetDrawer();
		HideInventoryScopeDrawer();
		HideWorkshopProtocolDrawer();
		HideAutoPickupBlacklistPicker();
		if (_contentHost != null)
		{
			QueueForDestroy(_contentHost);
		}
		_contentHost = CreateObject("ContentHost", _panel, typeof(Image));
		_contentHost.transform.SetSiblingIndex(2);
		_contentHost.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
		SetLayout(_contentHost, -1f, -1f, -1f, 1f);
	}

	private void BuildCharacterTab()
	{
		Transform transform = _contentHost.transform;
		AddVerticalLayout(_contentHost, 9f, new RectOffset(12, 12, 10, 10)).childForceExpandHeight = false;
		Player localPlayer = Player.LocalPlayer;
		PlayerLife playerLife = localPlayer?.life;
		PlayerSkills playerSkills = localPlayer?.skills;
		if (playerLife == null || playerSkills == null)
		{
			CreateLabel(transform, "玩家状态尚未准备完成。", 17);
			return;
		}
		CreateSection(transform, "生存状态");
		CreateSummaryPanel(transform, $"生命 {playerLife.health}/100    饱食 {playerLife.food}/100    水分 {playerLife.water}/100    免疫 {playerLife.virus}/100    体力 {playerLife.stamina}/100    氧气 {playerLife.oxygen}/100");
		GameObject gameObject = CreateRow(transform, 42f, 8f);
		CreateToggleAction(gameObject.transform, "无敌模式", _plugin.Actions.GodModeEnabled, delegate(bool value)
		{
			_plugin.Actions.SetGodMode(value);
			SetStatus(value ? "无敌模式已开启。" : "无敌模式已关闭。");
		});
		CreateToggleAction(gameObject.transform, "无限生存状态", _plugin.Actions.InfiniteNeedsEnabled, delegate(bool value)
		{
			_plugin.Actions.SetInfiniteNeeds(value);
			SetStatus(value ? "无限生存状态已开启。" : "无限生存状态已关闭。");
		});
		CreateLabel(transform, "设置指定数值（0–100；生命最低 1）", 12, CheatMenuStyle.Muted);
		GameObject gameObject2 = CreateRow(transform, 36f, 6f);
		CreateLabeledInput(gameObject2.transform, "生命", _healthTarget, delegate(string value)
		{
			_healthTarget = value;
		});
		CreateLabeledInput(gameObject2.transform, "饱食", _foodTarget, delegate(string value)
		{
			_foodTarget = value;
		});
		CreateLabeledInput(gameObject2.transform, "水分", _waterTarget, delegate(string value)
		{
			_waterTarget = value;
		});
		CreateLabeledInput(gameObject2.transform, "免疫", _virusTarget, delegate(string value)
		{
			_virusTarget = value;
		});
		CreateLabeledInput(gameObject2.transform, "体力", _staminaTarget, delegate(string value)
		{
			_staminaTarget = value;
		});
		CreateLabeledInput(gameObject2.transform, "氧气", _oxygenTarget, delegate(string value)
		{
			_oxygenTarget = value;
		});
		CreateButton(gameObject2.transform, "应用", delegate
		{
			bool flag = _plugin.Actions.SetLifeStats(ParseClamped(_healthTarget, 1, 100, 100), ParseClamped(_foodTarget, 0, 100, 100), ParseClamped(_waterTarget, 0, 100, 100), ParseClamped(_virusTarget, 0, 100, 100), ParseClamped(_staminaTarget, 0, 100, 100), ParseClamped(_oxygenTarget, 0, 100, 100));
			SetStatus(flag ? "角色生存数值已设置。" : "设置失败。", 3f, flag ? StatusKind.Success : StatusKind.Error);
		}, 76f, 34f, CheatMenuStyle.Accent);
		GameObject gameObject3 = CreateRow(transform, 42f, 8f);
		CreateAction(gameObject3.transform, "全部恢复", delegate
		{
			SetStatus(_plugin.Actions.RestoreEverything() ? "角色状态已全部恢复。" : "恢复失败。");
		});
		CreateAction(gameObject3.transform, "补满生存状态", delegate
		{
			SetStatus(_plugin.Actions.RefillNeeds() ? "生存状态已补满。" : "操作失败。");
		});
		CreateAction(gameObject3.transform, "治疗流血/骨折", delegate
		{
			SetStatus(_plugin.Actions.CureInjuries() ? "伤势已治疗。" : "操作失败。");
		});
		CreateSection(transform, "经验、声望与技能");
		CreateLabel(transform, $"经验 {playerSkills.experience:N0}    声望 {playerSkills.reputation:N0}", 14);
		GameObject gameObject4 = CreateRow(transform, 38f, 8f);
		CreateLabeledInput(gameObject4.transform, "经验", _experienceAmount, delegate(string value)
		{
			_experienceAmount = value;
		}, 150f);
		CreateButton(gameObject4.transform, "增加经验", delegate
		{
			uint num = (uint)ParseClamped(_experienceAmount, 1, 100000000, 1000);
			SetStatus(_plugin.Actions.AddExperience(num) ? $"已增加 {num:N0} 经验。" : "增加经验失败。");
		}, 110f, 34f);
		CreateLabeledInput(gameObject4.transform, "声望", _reputationAmount, delegate(string value)
		{
			_reputationAmount = value;
		}, 150f);
		CreateButton(gameObject4.transform, "变更声望", delegate
		{
			int num = ParseClamped(_reputationAmount, -1000000, 1000000, 100);
			SetStatus(_plugin.Actions.AddReputation(num) ? $"已变更 {num:N0} 声望。" : "变更声望失败。");
		}, 110f, 34f);
		CreateAction(gameObject4.transform, "全部技能满级", delegate
		{
			int num = _plugin.Actions.MaxAllSkills();
			SetStatus($"技能已处理，实际变更 {num} 项。");
		});
	}

	private void BuildItemsTab()
	{
		AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		CreateLabel(gameObject.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		CreateInput(gameObject.transform, _itemFilter.SearchQuery, delegate(string value)
		{
			_itemFilter.SearchQuery = value;
			_searchDebouncer.Schedule(delegate
			{
				BuildItemResults();
				if (_activeTab == MenuTab.Items)
				{
					ShowTab(MenuTab.Items);
				}
			});
		}, 280f);
		CreateLabel(gameObject.transform, "数量", 13, CheatMenuStyle.Text, 48f);
		CreateButton(gameObject.transform, "−", delegate
		{
			_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, 36f, 34f);
		_itemAmountInput = CreateInput(gameObject.transform, _itemAmountText, delegate(string value)
		{
			_itemAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
			}
		}, 62f);
		CreateButton(gameObject.transform, "+", delegate
		{
			_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, 36f, 34f);
		CreateButton(gameObject.transform, (_itemFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_itemFilter.ActiveFilterCount}", delegate
		{
			ShowItemFilterPanel(forFavorites: false);
		}, 92f, 34f);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "重新扫描", delegate
		{
			_plugin.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, 105f, 34f);
		CreateItemFilterSummary(_contentHost.transform, _itemFilter, _itemResults.Count, _plugin.Catalog.Items.Count);
		GameObject gameObject2 = CreateRow(_contentHost.transform, 0f, 10f);
		SetLayout(gameObject2, -1f, -1f, -1f, 1f);
		BuildItemCategorySidebar(gameObject2.transform, "物品分类", _itemFilter, delegate(ItemPrimaryCategory category)
		{
			_itemFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_itemFilter);
			BuildItemResults();
			ShowTab(MenuTab.Items);
		});
		BuildItemGrid(gameObject2.transform);
	}

	private void BuildVehiclesTab()
	{
		AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		CreateLabel(gameObject.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		CreateInput(gameObject.transform, _vehicleQuery, delegate(string value)
		{
			_vehicleQuery = value;
			_searchDebouncer.Schedule(delegate
			{
				BuildVehicleResults();
				if (_activeTab == MenuTab.Vehicles)
				{
					ShowTab(MenuTab.Vehicles);
				}
			});
		}, 280f);
		CreateLabel(gameObject.transform, "数量", 13, CheatMenuStyle.Text, 48f);
		CreateButton(gameObject.transform, "−", delegate
		{
			_spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			UpdateVehicleAmountInput();
		}, 36f, 34f);
		_vehicleAmountInput = CreateInput(gameObject.transform, _vehicleAmountText, delegate(string value)
		{
			_vehicleAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_spawnVehicleAmount = Mathf.Clamp(result, 1, 20);
			}
		}, 62f);
		CreateButton(gameObject.transform, "+", delegate
		{
			_spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			UpdateVehicleAmountInput();
		}, 36f, 34f);
		CreateButton(gameObject.transform, "⚙ 渲染设置", ShowVehicleThumbnailSettings, 116f, 34f);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "重新扫描", delegate
		{
			_plugin.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, 105f, 34f);
		CreateLabel(_contentHost.transform, $"显示 {_vehicleResults.Count} / {_plugin.Catalog.Vehicles.Count}；模组车辆 {_plugin.Catalog.WorkshopVehicleCount}。每次生成 1–20 辆。", 12, CheatMenuStyle.Muted);
		GameObject gameObject2 = CreateRow(_contentHost.transform, 0f, 10f);
		SetLayout(gameObject2, -1f, -1f, -1f, 1f);
		BuildCategorySidebar(gameObject2.transform, "车辆分类", VehicleCategories, _vehicleCategory, delegate(string category)
		{
			_vehicleCategory = category;
			BuildVehicleResults();
			ShowTab(MenuTab.Vehicles);
		});
		BuildVehicleGrid(gameObject2.transform);
	}

	private void BuildFavoritesTab()
	{
		AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
		int num = _plugin.Catalog.Items.Count(_plugin.Favorites.IsItemFavorite);
		int num2 = _plugin.Catalog.Vehicles.Count(_plugin.Favorites.IsVehicleFavorite);
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 8f);
		CreateButton(gameObject.transform, $"物品收藏  {num}", delegate
		{
			_favoriteKind = FavoriteKind.Items;
			_favoritePage = 0;
			BuildFavoriteResults();
			ShowTab(MenuTab.Favorites);
		}, 0f, 36f, (_favoriteKind == FavoriteKind.Items) ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 14);
		CreateButton(gameObject.transform, $"车辆收藏  {num2}", delegate
		{
			_favoriteKind = FavoriteKind.Vehicles;
			_favoritePage = 0;
			BuildFavoriteResults();
			ShowTab(MenuTab.Favorites);
		}, 0f, 36f, (_favoriteKind == FavoriteKind.Vehicles) ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 14);
		foreach (Transform item in gameObject.transform)
		{
			SetLayout(item.gameObject, -1f, -1f, 1f);
		}
		GameObject gameObject2 = CreateRow(_contentHost.transform, 38f, 7f);
		CreateLabel(gameObject2.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		string value = ((_favoriteKind == FavoriteKind.Items) ? _favoriteItemFilter.SearchQuery : _favoriteQuery);
		CreateInput(gameObject2.transform, value, delegate(string text)
		{
			if (_favoriteKind == FavoriteKind.Items)
			{
				_favoriteItemFilter.SearchQuery = text;
			}
			else
			{
				_favoriteQuery = text;
			}
			_searchDebouncer.Schedule(delegate
			{
				BuildFavoriteResults();
				if (_activeTab == MenuTab.Favorites)
				{
					ShowTab(MenuTab.Favorites);
				}
			});
		}, 280f);
		CreateLabel(gameObject2.transform, "数量", 13, CheatMenuStyle.Text, 48f);
		if (_favoriteKind == FavoriteKind.Items)
		{
			CreateButton(gameObject2.transform, "−", delegate
			{
				_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
				_itemAmountText = _giveAmount.ToString();
				UpdateItemAmountInput();
			}, 36f, 34f);
			_itemAmountInput = CreateInput(gameObject2.transform, _itemAmountText, delegate(string text)
			{
				_itemAmountText = text;
				if (int.TryParse(text, out var result))
				{
					_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
				}
			}, 62f);
			CreateButton(gameObject2.transform, "+", delegate
			{
				_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
				_itemAmountText = _giveAmount.ToString();
				UpdateItemAmountInput();
			}, 36f, 34f);
			CreateButton(gameObject2.transform, (_favoriteItemFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_favoriteItemFilter.ActiveFilterCount}", delegate
			{
				ShowItemFilterPanel(forFavorites: true);
			}, 92f, 34f);
		}
		else
		{
			CreateButton(gameObject2.transform, "−", delegate
			{
				_spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
				_vehicleAmountText = _spawnVehicleAmount.ToString();
				UpdateVehicleAmountInput();
			}, 36f, 34f);
			_vehicleAmountInput = CreateInput(gameObject2.transform, _vehicleAmountText, delegate(string text)
			{
				_vehicleAmountText = text;
				if (int.TryParse(text, out var result))
				{
					_spawnVehicleAmount = Mathf.Clamp(result, 1, 20);
				}
			}, 62f);
			CreateButton(gameObject2.transform, "+", delegate
			{
				_spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
				_vehicleAmountText = _spawnVehicleAmount.ToString();
				UpdateVehicleAmountInput();
			}, 36f, 34f);
		}
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "重新扫描", delegate
		{
			_plugin.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, 105f, 34f);
		if (_favoriteKind == FavoriteKind.Items)
		{
			CreateItemFilterSummary(_contentHost.transform, _favoriteItemFilter, _favoriteItemResults.Count, num);
		}
		else
		{
			CreateLabel(_contentHost.transform, $"显示 {_favoriteVehicleResults.Count} 辆已加载收藏车辆。", 12, CheatMenuStyle.Muted);
		}
		GameObject gameObject3 = CreateRow(_contentHost.transform, 0f, 10f);
		SetLayout(gameObject3, -1f, -1f, -1f, 1f);
		if (_favoriteKind == FavoriteKind.Items)
		{
			BuildItemCategorySidebar(gameObject3.transform, "收藏物品分类", _favoriteItemFilter, delegate(ItemPrimaryCategory category)
			{
				_favoriteItemFilter.Category = category;
				ItemFilterService.NormalizeForCategory(_favoriteItemFilter);
				BuildFavoriteResults();
				ShowTab(MenuTab.Favorites);
			}, _plugin.Favorites.IsItemFavorite);
		}
		else
		{
			BuildCategorySidebar(gameObject3.transform, "收藏车辆分类", VehicleCategories, _favoriteVehicleCategory, delegate(string category)
			{
				_favoriteVehicleCategory = category;
				BuildFavoriteResults();
				ShowTab(MenuTab.Favorites);
			});
		}
		BuildFavoriteGrid(gameObject3.transform);
	}

	private void BuildCategorySidebar(Transform parent, string title, IEnumerable<string> categories, string selected, Action<string> select)
	{
		GameObject gameObject = CreateObject("Categories", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Surface;
		AddVerticalLayout(gameObject, 5f, new RectOffset(7, 7, 7, 7));
		SetLayout(gameObject, 165f, -1f, 0f);
		CreateLabel(gameObject.transform, title, 16, CheatMenuStyle.Text);
		foreach (string category in categories)
		{
			string captured = category;
			CreateButton(gameObject.transform, category, delegate
			{
				select(captured);
			}, 0f, 34f, (category == selected) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover);
		}
	}

	private void BuildItemCategorySidebar(Transform parent, string title, ItemFilterState filter, Action<ItemPrimaryCategory> select, Func<ItemAsset, bool> includeItem = null)
	{
		GameObject gameObject = CreateObject("Categories", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Surface;
		AddVerticalLayout(gameObject, 5f, new RectOffset(7, 7, 7, 7));
		SetLayout(gameObject, 165f, -1f, 0f);
		CreateLabel(gameObject.transform, title, 16, CheatMenuStyle.Text);
		foreach (ItemPrimaryCategory category in ItemFilterService.Categories)
		{
			ItemPrimaryCategory captured = category;
			ItemFilterState categoryFilter = filter.Clone();
			categoryFilter.Category = category;
			ItemFilterService.NormalizeForCategory(categoryFilter);
			int num = _plugin.Catalog.Items.Count((ItemAsset asset) => (includeItem == null || includeItem(asset)) && ItemFilterService.Matches(asset, categoryFilter));
			string label = $"{PluginLocalization.Translate(ItemFilterService.GetPrimaryCategoryLabel(category))}  {num:N0}";
			CreateButton(gameObject.transform, label, delegate
			{
				select(captured);
			}, 0f, 34f, (category == filter.Category) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12);
		}
	}

	private void CreateItemFilterSummary(Transform parent, ItemFilterState filter, int resultCount, int sourceCount)
	{
		CreateLabel(parent, $"显示 {resultCount:N0} / {sourceCount:N0}；搜索支持名称、ID、GUID 和来源。", 12, CheatMenuStyle.Muted);
		if (filter.ActiveFilterCount != 0)
		{
			GameObject gameObject = CreateRow(parent, 32f, 6f);
			AddItemFilterChip(gameObject.transform, filter.ItemType.HasValue ? ItemFilterService.GetItemTypeLabel(filter.ItemType.Value) : null, delegate
			{
				filter.ItemType = null;
				ItemFilterService.NormalizeForCategory(filter);
				ApplyItemFilterFromChip();
			});
			AddItemFilterChip(gameObject.transform, (filter.Origin != ItemOriginFilter.All) ? ItemFilterService.GetOriginLabel(filter.Origin) : null, delegate
			{
				filter.Origin = ItemOriginFilter.All;
				ApplyItemFilterFromChip();
			});
			AddItemFilterChip(gameObject.transform, filter.Rarity.HasValue ? ItemFilterService.GetRarityLabel(filter.Rarity.Value) : null, delegate
			{
				filter.Rarity = null;
				ApplyItemFilterFromChip();
			});
			AddItemFilterChip(gameObject.transform, filter.Slot.HasValue ? ItemFilterService.GetSlotLabel(filter.Slot.Value) : null, delegate
			{
				filter.Slot = null;
				ApplyItemFilterFromChip();
			});
			AddItemFilterChip(gameObject.transform, filter.GunAction.HasValue ? ItemFilterService.GetGunActionLabel(filter.GunAction.Value) : null, delegate
			{
				filter.GunAction = null;
				ApplyItemFilterFromChip();
			});
			AddItemFilterChip(gameObject.transform, (filter.FireModes != GunFireModeFilter.None) ? ItemFilterService.GetFireModeLabel(filter.FireModes) : null, delegate
			{
				filter.FireModes = GunFireModeFilter.None;
				ApplyItemFilterFromChip();
			});
			CreateFlexibleSpacer(gameObject.transform);
			CreateButton(gameObject.transform, "清除筛选", delegate
			{
				filter.ResetAdvanced();
				ApplyItemFilterFromChip();
			}, 100f, 30f, CheatMenuStyle.SurfaceHover, 12);
		}
	}

	private void AddItemFilterChip(Transform parent, string label, System.Action remove)
	{
		if (!string.IsNullOrEmpty(label))
		{
			string[] array = label.Split(new string[1] { " / " }, StringSplitOptions.None);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = PluginLocalization.Translate(array[i]);
			}
			string text = string.Join(" / ", array) + " ×";
			CreateButton(parent, text, remove, Mathf.Clamp(48f + (float)text.Length * 11f, 92f, 280f), 30f, CheatMenuStyle.SurfaceHover, 11);
		}
	}

	private void ApplyItemFilterFromChip()
	{
		BuildItemResults();
		BuildFavoriteResults();
		ShowTab(_activeTab);
	}

	private void ShowItemFilterPanel(bool forFavorites)
	{
		HideItemFilterPanel();
		_itemFilterForInfiniteStorage = false;
		_itemFilterForFavorites = forFavorites;
		_itemFilterDraft = (_itemFilterForInfiniteStorage ? _infiniteStorageFilter : (forFavorites ? _favoriteItemFilter : _itemFilter)).Clone();
		BuildItemFilterPanel();
	}

	private void ShowInfiniteStorageFilterPanel()
	{
		HideItemFilterPanel();
		_itemFilterForInfiniteStorage = true;
		_itemFilterDraft = _infiniteStorageFilter.Clone();
		BuildItemFilterPanel();
	}

	private void BuildItemFilterPanel()
	{
		if (_itemFilterDraft == null)
		{
			return;
		}
		GameObject activeRoot = GetActiveOverlayRoot();
		if (activeRoot == null)
		{
			return;
		}
		_itemFilterRoot = CreateObject("ItemFilterOverlay", activeRoot.transform, typeof(Image));
		Stretch(_itemFilterRoot.GetComponent<RectTransform>());
		_itemFilterRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("ItemFilterDialog", _itemFilterRoot.transform, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(860f, 610f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(24, 24, 18, 18);
		verticalLayoutGroup.spacing = 8f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject2 = CreateRow(gameObject.transform, 42f, 8f);
		CreateText(gameObject2.transform, "筛选", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "×", HideItemFilterPanel, 42f, 38f, CheatMenuStyle.Surface, 22);
		CreateLabel(gameObject.transform, "当前分类：" + PluginLocalization.Translate(ItemFilterService.GetPrimaryCategoryLabel(_itemFilterDraft.Category)), 12, CheatMenuStyle.Muted);
		IReadOnlyList<EItemType> itemTypes = ItemFilterService.GetItemTypes(_itemFilterDraft.Category);
		if (itemTypes.Count > 0)
		{
			List<string> list = new List<string> { "全部" };
			foreach (EItemType item in itemTypes)
			{
				list.Add(ItemFilterService.GetItemTypeLabel(item));
			}
			CreateFilterOptionGrid(gameObject.transform, "物品类型", list, 6, delegate(int index)
			{
				_itemFilterDraft.ItemType = ((index == 0) ? ((EItemType?)null) : new EItemType?(itemTypes[index - 1]));
				ItemFilterService.NormalizeForCategory(_itemFilterDraft);
				RebuildItemFilterPanel();
			}, (int index) => (index != 0) ? (_itemFilterDraft.ItemType == itemTypes[index - 1]) : (!_itemFilterDraft.ItemType.HasValue));
		}
		CreateFilterOptionGrid(gameObject.transform, "来源", new List<string>
		{
			"全部来源",
			ItemFilterService.GetOriginLabel(ItemOriginFilter.Official),
			ItemFilterService.GetOriginLabel(ItemOriginFilter.Workshop),
			ItemFilterService.GetOriginLabel(ItemOriginFilter.MapOrOther)
		}, 4, delegate(int index)
		{
			_itemFilterDraft.Origin = ((index != 0) ? ItemOriginFilters[index - 1] : ItemOriginFilter.All);
			RebuildItemFilterPanel();
		}, (int index) => (index != 0) ? (_itemFilterDraft.Origin == ItemOriginFilters[index - 1]) : (_itemFilterDraft.Origin == ItemOriginFilter.All));
		List<string> list2 = new List<string> { "全部" };
		EItemRarity[] itemRarities = ItemRarities;
		foreach (EItemRarity rarity in itemRarities)
		{
			list2.Add(ItemFilterService.GetRarityLabel(rarity));
		}
		CreateFilterOptionGrid(gameObject.transform, "稀有度", list2, 7, delegate(int index)
		{
			_itemFilterDraft.Rarity = ((index == 0) ? ((EItemRarity?)null) : new EItemRarity?(ItemRarities[index - 1]));
			RebuildItemFilterPanel();
		}, (int index) => (index != 0) ? (_itemFilterDraft.Rarity == ItemRarities[index - 1]) : (!_itemFilterDraft.Rarity.HasValue));
		List<string> list3 = new List<string> { "全部" };
		ESlotType[] itemSlots = ItemSlots;
		foreach (ESlotType slot in itemSlots)
		{
			list3.Add(ItemFilterService.GetSlotLabel(slot));
		}
		CreateFilterOptionGrid(gameObject.transform, "装备槽位", list3, 5, delegate(int index)
		{
			_itemFilterDraft.Slot = ((index == 0) ? ((ESlotType?)null) : new ESlotType?(ItemSlots[index - 1]));
			RebuildItemFilterPanel();
		}, (int index) => (index != 0) ? (_itemFilterDraft.Slot == ItemSlots[index - 1]) : (!_itemFilterDraft.Slot.HasValue));
		if (_itemFilterDraft.ItemType == EItemType.GUN)
		{
			List<string> list4 = new List<string> { "全部" };
			EAction[] gunActions = GunActions;
			foreach (EAction action in gunActions)
			{
				list4.Add(ItemFilterService.GetGunActionLabel(action));
			}
			CreateFilterOptionGrid(gameObject.transform, "射击机制", list4, 5, delegate(int index)
			{
				_itemFilterDraft.GunAction = ((index == 0) ? ((EAction?)null) : new EAction?(GunActions[index - 1]));
				RebuildItemFilterPanel();
			}, (int index) => (index != 0) ? (_itemFilterDraft.GunAction == GunActions[index - 1]) : (!_itemFilterDraft.GunAction.HasValue));
			List<string> labels = new List<string> { "全部", "半自动", "全自动", "点射" };
			CreateFilterOptionGrid(gameObject.transform, "射击模式（可多选）", labels, 4, delegate(int index)
			{
				if (index == 0)
				{
					_itemFilterDraft.FireModes = GunFireModeFilter.None;
				}
				else
				{
					GunFireModeFilter gunFireModeFilter = GunFireModes[index - 1];
					_itemFilterDraft.FireModes = (_itemFilterDraft.FireModes.HasFlag(gunFireModeFilter) ? (_itemFilterDraft.FireModes & ~gunFireModeFilter) : (_itemFilterDraft.FireModes | gunFireModeFilter));
				}
				RebuildItemFilterPanel();
			}, (int index) => (index != 0) ? _itemFilterDraft.FireModes.HasFlag(GunFireModes[index - 1]) : (_itemFilterDraft.FireModes == GunFireModeFilter.None));
		}
		CreateFlexibleSpacer(gameObject.transform);
		GameObject gameObject3 = CreateRow(gameObject.transform, 40f, 8f);
		CreateFlexibleSpacer(gameObject3.transform);
		CreateButton(gameObject3.transform, "重置", delegate
		{
			_itemFilterDraft.ResetAdvanced();
			RebuildItemFilterPanel();
		}, 90f);
		CreateButton(gameObject3.transform, "取消", HideItemFilterPanel, 90f);
		CreateButton(gameObject3.transform, "应用", ApplyItemFilterPanel, 90f, 36f, CheatMenuStyle.Accent);
	}

	private void CreateFilterOptionGrid(Transform parent, string title, IReadOnlyList<string> labels, int columns, Action<int> select, Func<int, bool> selected)
	{
		CreateLabel(parent, title, 13, CheatMenuStyle.Text);
		GameObject gameObject = CreateObject("FilterOptions", parent);
		GridLayoutGroup gridLayoutGroup = gameObject.AddComponent<GridLayoutGroup>();
		int num = Math.Max(1, columns);
		float num2 = Mathf.Min(112f, (812f - 4f * (float)(num - 1)) / (float)num);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = num;
		gridLayoutGroup.cellSize = new Vector2(num2, 32f);
		gridLayoutGroup.spacing = new Vector2(4f, 5f);
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		int num3 = Mathf.CeilToInt((float)labels.Count / (float)num);
		SetLayout(gameObject, -1f, (float)num3 * 37f - 5f);
		for (int i = 0; i < labels.Count; i++)
		{
			int captured = i;
			CreateButton(gameObject.transform, labels[i], delegate
			{
				select(captured);
			}, num2, 32f, selected(i) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 11);
		}
	}

	private void RebuildItemFilterPanel()
	{
		if (!(_itemFilterRoot == null))
		{
			_ = _itemFilterForFavorites;
			GameObject itemFilterRoot = _itemFilterRoot;
			_itemFilterRoot = null;
			QueueForDestroy(itemFilterRoot);
			BuildItemFilterPanel();
		}
	}

	private void ApplyItemFilterPanel()
	{
		ItemFilterState obj = _itemFilterForInfiniteStorage ? _infiniteStorageFilter : (_itemFilterForFavorites ? _favoriteItemFilter : _itemFilter);
		obj.CopyFrom(_itemFilterDraft);
		ItemFilterService.NormalizeForCategory(obj);
		HideItemFilterPanel();
		_itemFilterForInfiniteStorage = false;
		BuildItemResults();
		BuildFavoriteResults();
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			_infiniteStoragePage = 0;
			RebuildInfiniteStoragePage();
		}
		else
		{
			ShowTab(_activeTab);
		}
	}

	private void HideItemFilterPanel()
	{
		if (!(_itemFilterRoot == null))
		{
			QueueForDestroy(_itemFilterRoot);
			_itemFilterRoot = null;
			_itemFilterDraft = null;
		}
	}

	private void BuildItemGrid(Transform parent)
	{
		GameObject gameObject = CreateObject("ItemGridColumn", parent);
		AddVerticalLayout(gameObject, 7f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject, -1f, -1f, 1f, 1f);
		int num = Math.Max(1, Mathf.CeilToInt((float)_itemResults.Count / 15f));
		_itemPage = Mathf.Clamp(_itemPage, 0, num - 1);
		Action<int> changePage = delegate(int value)
		{
			_itemPage = value;
			ShowTab(MenuTab.Items);
		};
		BuildPagination(gameObject.transform, _itemPage, num, changePage);
		AddPageScrollHandler(gameObject, _itemPage, num, changePage);
		if (_itemResults.Count == 0)
		{
			CreateEmptyState(gameObject.transform, string.IsNullOrWhiteSpace(_itemFilter.SearchQuery) ? "当前分类没有可用物品。" : "没有匹配的物品。", string.IsNullOrWhiteSpace(_itemFilter.SearchQuery) ? "请选择其他分类，或重新扫描当前加载的资产。" : "请清空搜索或尝试其他关键词。");
			return;
		}
		GameObject gameObject2 = CreateObject("ItemGrid", gameObject.transform);
		SetLayout(gameObject2, -1f, 500f, -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(8f, 8f);
		gridLayoutGroup.cellSize = new Vector2(155f, 158f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 5;
		int num2 = _itemPage * 15;
		int num3 = Math.Min(num2 + 15, _itemResults.Count);
		for (int num4 = num2; num4 < num3; num4++)
		{
			CreateItemCard(gameObject2.transform, _itemResults[num4]);
		}
	}

	private void BuildVehicleGrid(Transform parent)
	{
		GameObject gameObject = CreateObject("VehicleGridColumn", parent);
		AddVerticalLayout(gameObject, 7f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject, -1f, -1f, 1f, 1f);
		int num = Math.Max(1, Mathf.CeilToInt((float)_vehicleResults.Count / 15f));
		_vehiclePage = Mathf.Clamp(_vehiclePage, 0, num - 1);
		Action<int> changePage = delegate(int value)
		{
			_vehiclePage = value;
			ShowTab(MenuTab.Vehicles);
		};
		BuildPagination(gameObject.transform, _vehiclePage, num, changePage);
		AddPageScrollHandler(gameObject, _vehiclePage, num, changePage);
		if (_vehicleResults.Count == 0)
		{
			CreateEmptyState(gameObject.transform, string.IsNullOrWhiteSpace(_vehicleQuery) ? "当前分类没有可用车辆。" : "没有匹配的车辆。", string.IsNullOrWhiteSpace(_vehicleQuery) ? "请选择其他分类，或重新扫描当前加载的资产。" : "请清空搜索或尝试其他关键词。");
			return;
		}
		GameObject gameObject2 = CreateObject("VehicleGrid", gameObject.transform);
		SetLayout(gameObject2, -1f, 500f, -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(8f, 8f);
		gridLayoutGroup.cellSize = new Vector2(155f, 158f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 5;
		int num2 = _vehiclePage * 15;
		int num3 = Math.Min(num2 + 15, _vehicleResults.Count);
		for (int num4 = num2; num4 < num3; num4++)
		{
			CreateVehicleCard(gameObject2.transform, _vehicleResults[num4]);
		}
	}

	private void BuildFavoriteGrid(Transform parent)
	{
		int num = ((_favoriteKind == FavoriteKind.Items) ? _favoriteItemResults.Count : _favoriteVehicleResults.Count);
		GameObject gameObject = CreateObject("FavoriteGridColumn", parent);
		AddVerticalLayout(gameObject, 7f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject, -1f, -1f, 1f, 1f);
		int num2 = Math.Max(1, Mathf.CeilToInt((float)num / 15f));
		_favoritePage = Mathf.Clamp(_favoritePage, 0, num2 - 1);
		Action<int> changePage = delegate(int value)
		{
			_favoritePage = value;
			ShowTab(MenuTab.Favorites);
		};
		BuildPagination(gameObject.transform, _favoritePage, num2, changePage);
		AddPageScrollHandler(gameObject, _favoritePage, num2, changePage);
		if (num == 0)
		{
			CreateEmptyState(gameObject.transform, (_favoriteKind == FavoriteKind.Items) ? "当前分类没有已加载的收藏物品。" : "当前分类没有已加载的收藏车辆。", (_favoriteKind == FavoriteKind.Items) ? "请在物品卡片右上角点击 ☆ 收藏。" : "请在车辆卡片右上角点击 ☆ 收藏。");
			return;
		}
		GameObject gameObject2 = CreateObject("FavoriteGrid", gameObject.transform);
		SetLayout(gameObject2, -1f, 500f, -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(8f, 8f);
		gridLayoutGroup.cellSize = new Vector2(155f, 158f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 5;
		int num3 = _favoritePage * 15;
		int num4 = Math.Min(num3 + 15, num);
		if (_favoriteKind == FavoriteKind.Items)
		{
			for (int num5 = num3; num5 < num4; num5++)
			{
				CreateItemCard(gameObject2.transform, _favoriteItemResults[num5]);
			}
		}
		else
		{
			for (int num6 = num3; num6 < num4; num6++)
			{
				CreateVehicleCard(gameObject2.transform, _favoriteVehicleResults[num6]);
			}
		}
	}

	private void CreateItemCard(Transform parent, ItemAsset asset)
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		string displayName = ((workshopLocalization != null && workshopLocalization.TryGetTranslation(asset, WorkshopTextField.ItemName, out var translation) && !string.IsNullOrWhiteSpace(translation)) ? translation : asset.FriendlyName);
		string tooltip = AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _plugin.Catalog.Items);
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
			_itemAmountText = _giveAmount.ToString();
			bool flag = _plugin.Actions.GiveItem(asset, _giveAmount);
			SetStatus(flag ? $"已给予 {_giveAmount} × {displayName}。" : ("无法给予 " + displayName + "。"), 4f, flag ? StatusKind.Success : StatusKind.Error);
		}, 155f, 158f, CheatMenuStyle.Surface, tooltip: tooltip, delayTooltip: true);
		AddCardContents(button.transform, displayName, $"ID {asset.id}  ·  {AssetCatalog.GetOriginLabel(asset)}", () => _plugin.Icons.GetItemIcon(asset), isVehicle: false);
		AddFavoriteButton(button.transform, _plugin.Favorites.IsItemFavorite(asset), delegate
		{
			ToggleItemFavorite(asset);
		});
	}

	private void CreateVehicleCard(Transform parent, VehicleAsset asset)
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		string displayName = ((workshopLocalization != null && workshopLocalization.TryGetTranslation(asset, WorkshopTextField.VehicleName, out var translation) && !string.IsNullOrWhiteSpace(translation)) ? translation : asset.FriendlyName);
		string tooltip = AssetHoverDetailsBuilder.BuildVehicle(asset, displayName);
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_spawnVehicleAmount = ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			int num = _plugin.Actions.SpawnVehicles(asset, _spawnVehicleAmount);
			SetStatus((num > 0) ? $"已在玩家前方生成 {num} × {displayName}。" : ("无法生成 " + displayName + "。"), 4f, (num > 0) ? StatusKind.Success : StatusKind.Error);
		}, 155f, 158f, CheatMenuStyle.Surface, tooltip: tooltip, delayTooltip: true);
		string text = ((asset.id == 0) ? "GUID" : $"ID {asset.id}");
		AddCardContents(button.transform, displayName, text + "  ·  " + AssetCatalog.GetOriginLabel(asset), () => _plugin.Icons.GetVehicleIcon(asset), isVehicle: true);
		AddFavoriteButton(button.transform, _plugin.Favorites.IsVehicleFavorite(asset), delegate
		{
			ToggleVehicleFavorite(asset);
		});
	}

	private void AddFavoriteButton(Transform parent, bool isFavorite, System.Action toggle)
	{
		Button button = CreateButton(parent, isFavorite ? "★" : "☆", toggle, 0f, 0f, isFavorite ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 18);
		RectTransform component = button.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(1f, 1f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(1f, 1f);
		component.anchoredPosition = new Vector2(-5f, -5f);
		component.sizeDelta = new Vector2(32f, 28f);
		button.transform.SetAsLastSibling();
	}

	private void ToggleItemFavorite(ItemAsset asset)
	{
		bool flag = !_plugin.Favorites.IsItemFavorite(asset);
		if (!_plugin.Favorites.TrySetItemFavorite(asset, flag))
		{
			SetStatus("无法保存 " + asset.FriendlyName + " 的收藏状态。", 5f, StatusKind.Error);
			return;
		}
		MenuTab activeTab = _activeTab;
		BuildFavoriteResults();
		ShowTab(activeTab);
		SetStatus(flag ? ("已收藏物品：" + asset.FriendlyName + "。") : ("已取消收藏物品：" + asset.FriendlyName + "。"), 4f);
	}

	private void ToggleVehicleFavorite(VehicleAsset asset)
	{
		bool flag = !_plugin.Favorites.IsVehicleFavorite(asset);
		if (!_plugin.Favorites.TrySetVehicleFavorite(asset, flag))
		{
			SetStatus("无法保存 " + asset.FriendlyName + " 的收藏状态。", 5f, StatusKind.Error);
			return;
		}
		MenuTab activeTab = _activeTab;
		BuildFavoriteResults();
		ShowTab(activeTab);
		SetStatus(flag ? ("已收藏车辆：" + asset.FriendlyName + "。") : ("已取消收藏车辆：" + asset.FriendlyName + "。"), 4f);
	}

	private void AddCardContents(Transform parent, string name, string meta, Func<Texture2D> iconLoader, bool isVehicle)
	{
		GameObject gameObject = CreateObject("Icon", parent, typeof(RawImage), typeof(OverlayIconBinder));
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.12f, 0.4f);
		component.anchorMax = new Vector2(0.88f, 0.95f);
		Vector2 offsetMin = (component.offsetMax = Vector2.zero);
		component.offsetMin = offsetMin;
		RawImage component2 = gameObject.GetComponent<RawImage>();
		component2.color = Color.white;
		component2.raycastTarget = false;
		Text text = CreateText(gameObject.transform, "加载预览…", 11, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
		Stretch(text.rectTransform);
		gameObject.GetComponent<OverlayIconBinder>().Initialize(component2, text, iconLoader, isVehicle);
		Text text2 = CreateText(parent, name, 12, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold, localize: false);
		text2.rectTransform.anchorMin = new Vector2(0.04f, 0.13f);
		text2.rectTransform.anchorMax = new Vector2(0.96f, 0.4f);
		RectTransform rectTransform = text2.rectTransform;
		offsetMin = (text2.rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		Text text3 = CreateText(parent, meta, 10, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
		text3.rectTransform.anchorMin = new Vector2(0.04f, 0.01f);
		text3.rectTransform.anchorMax = new Vector2(0.96f, 0.14f);
		RectTransform rectTransform2 = text3.rectTransform;
		offsetMin = (text3.rectTransform.offsetMax = Vector2.zero);
		rectTransform2.offsetMin = offsetMin;
	}

	private void AddPageScrollHandler(GameObject target, int page, int pageCount, Action<int> changePage)
	{
		Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
		image.color = Color.clear;
		PageScrollHandler handler = target.AddComponent<PageScrollHandler>();
		handler.Initialize(page, pageCount,
			delegate
			{
				if (page > 0) { HideHoverExplanation(); changePage(page - 1); }
			},
			delegate
			{
				if (page < pageCount - 1) { HideHoverExplanation(); changePage(page + 1); }
			});
	}

	private void BuildPagination(Transform parent, int page, int pageCount, Action<int> changePage)
	{
		GameObject gameObject = CreateRow(parent, 34f, 8f);
		CreateButton(gameObject.transform, "‹ 上一页", delegate
		{
			changePage(page - 1);
		}, 100f, 32f).interactable = page > 0;
		CreateFlexibleSpacer(gameObject.transform);
		CreateLabel(gameObject.transform, $"第 {page + 1} / {pageCount} 页", 12, CheatMenuStyle.Muted, 110f, TextAnchor.MiddleCenter);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "下一页 ›", delegate
		{
			changePage(page + 1);
		}, 100f, 32f).interactable = page < pageCount - 1;
	}

	private void BuildTeleportsTab()
	{
		Transform transform = _contentHost.transform;
		AddVerticalLayout(_contentHost, 8f, new RectOffset(12, 12, 10, 10));
		GameObject gameObject = CreateRow(transform, 38f, 8f);
		CreateButton(gameObject.transform, "地图", delegate
		{
			SwitchTeleportView(TeleportView.Map);
		}, 0f, 36f, (_teleportView == TeleportView.Map) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 14);
		CreateButton(gameObject.transform, "传送点", delegate
		{
			SwitchTeleportView(TeleportView.Points);
		}, 0f, 36f, (_teleportView == TeleportView.Points) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 14);
		foreach (Transform item in gameObject.transform)
		{
			SetLayout(item.gameObject, -1f, -1f, 1f);
		}
		if (_teleportView == TeleportView.Map)
		{
			BuildTeleportMapView(transform);
		}
		else
		{
			BuildTeleportPointsView(transform);
		}
	}

	private void BuildTeleportMapView(Transform content)
	{
		CreateSection(content, "当前地图 GPS");
		TeleportMapSnapshot teleportMapSnapshot = _plugin.TeleportMaps.ScanCurrentMap();
		string text = (teleportMapSnapshot.IsGps ? PluginLocalization.Translate("GPS/卫星图") : (teleportMapSnapshot.IsChart ? PluginLocalization.Translate("地形图") : PluginLocalization.Translate("地图预览")));
		GameObject gameObject = CreateRow(content, 34f, 8f);
		CreateLabel(gameObject.transform, teleportMapSnapshot.IsAvailable ? $"{text} · {teleportMapSnapshot.MapName} · {teleportMapSnapshot.Texture.width}×{teleportMapSnapshot.Texture.height}" : teleportMapSnapshot.Error, 12, teleportMapSnapshot.IsAvailable ? CheatMenuStyle.Muted : CheatMenuStyle.Warning);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "扫描地图", delegate
		{
			ShowTab(MenuTab.Teleports);
			SetStatus((_teleportMapSurface != null) ? ("已重新扫描地图：" + Provider.map + "。支持 Map.png / Chart.png。") : "当前地图没有可用的 Map.png / Chart.png。", 5f, (_teleportMapSurface != null) ? StatusKind.Success : StatusKind.Warning);
		}, 105f, 32f);
		if (!teleportMapSnapshot.IsAvailable)
		{
			CreateEmptyState(content, "当前地图没有可用 GPS 图层。", "请确认地图目录中存在 Map.png 或 Chart.png。");
			return;
		}
		string currentMap = Provider.map ?? string.Empty;
		TeleportPoint[] teleportPoints = _plugin.Teleports.Points.Where((TeleportPoint point) => string.Equals(point.Map, currentMap, StringComparison.OrdinalIgnoreCase)).ToArray();
		GameObject gameObject2 = CreateObject("TeleportMapSurface", content, typeof(Image));
		SetLayout(gameObject2, -1f, 480f, -1f, 1f);
		Outline outline = gameObject2.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.TeleportBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_teleportMapSurface = gameObject2.AddComponent<TeleportMapSurface>();
		_teleportMapSurface.Initialize(teleportMapSnapshot.Texture, delegate(Vector3 worldPosition)
		{
			bool flag = _plugin.Actions.TeleportToMapPosition(worldPosition);
			SetStatus(flag ? $"已传送到地图坐标 X {worldPosition.x:F1} · Z {worldPosition.z:F1}。" : "地图传送失败：未找到安全落点、目标超出地图边界或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
		}, teleportPoints, delegate(TeleportPoint point)
		{
			bool flag = _plugin.Actions.Teleport(point);
			SetStatus(flag ? ("已传送到 " + point.Name + "。") : "传送失败：目标位置有障碍物且无法放置在障碍物顶部，或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
		});
		GameObject gameObject3 = CreateRow(content, 34f, 8f);
		CreateFlexibleSpacer(gameObject3.transform);
		CreateButton(gameObject3.transform, "−", delegate
		{
			_teleportMapSurface?.ZoomOut();
		}, 42f, 32f);
		CreateButton(gameObject3.transform, "＋", delegate
		{
			_teleportMapSurface?.ZoomIn();
		}, 42f, 32f);
		CreateButton(gameObject3.transform, "居中", delegate
		{
			if (Player.LocalPlayer != null)
			{
				_teleportMapSurface?.CenterOnWorld(Player.LocalPlayer.transform.position);
			}
		}, 70f, 32f);
		CreateLabel(gameObject3.transform, "左键拖动 · 滚轮缩放 · 悬停标记查看信息 · 右键标记快速传送", 11, CheatMenuStyle.Muted, 0f, TextAnchor.MiddleRight);
	}

	private void BuildTeleportPointsView(Transform content)
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null)
		{
			CreateEmptyState(content, "玩家状态尚未准备完成。", "进入地图后即可保存和使用传送点。");
			return;
		}
		Vector3 position = localPlayer.transform.position;
		CreateSection(content, "保存当前位置");
		CreateLabel(content, $"地图 {Provider.map}    X {position.x:F1}    Y {position.y:F1}    Z {position.z:F1}", 12, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(content, 38f, 8f);
		CreateInput(gameObject.transform, _teleportName, delegate(string value)
		{
			_teleportName = value;
		}, 0f, 1f);
		Button markerPickerButton = null;
		markerPickerButton = CreateMarkerPickerButton(gameObject.transform, _teleportMarkerKind, delegate
		{
			ToggleTeleportMarkerDrawer(markerPickerButton);
		}, 108f, compact: true);
		CreateColorPickerButton(gameObject.transform);
		CreateButton(gameObject.transform, "保存位置", delegate
		{
			TeleportPoint teleportPoint = _plugin.Teleports.AddCurrent(_teleportName, _teleportMarkerKind, ColorUtility.ToHtmlStringRGB(_teleportMarkerColor).Insert(0, "#"));
			if (teleportPoint != null)
			{
				_teleportName = string.Empty;
				SetStatus("已保存传送点：" + teleportPoint.Name + "。", 4f);
				ShowTab(MenuTab.Teleports);
			}
		}, 120f, 35f);
		string currentMap = Provider.map ?? string.Empty;
		TeleportPoint[] array = _plugin.Teleports.Points.Where((TeleportPoint teleportPoint) => string.Equals(teleportPoint.Map, currentMap, StringComparison.OrdinalIgnoreCase)).ToArray();
		CreateSection(content, $"当前地图传送点（{array.Length}）");
		int num = Math.Max(1, Mathf.CeilToInt((float)array.Length / 6f));
		_teleportPage = Mathf.Clamp(_teleportPage, 0, num - 1);
		BuildPagination(content, _teleportPage, num, delegate(int value)
		{
			_teleportPage = value;
			ShowTab(MenuTab.Teleports);
		});
		if (array.Length == 0)
		{
			CreateEmptyState(content, "当前地图还没有保存的传送点。", "请先输入名称并保存当前位置；不同地图的传送点会自动隔离。");
			return;
		}
		int num2 = _teleportPage * 6;
		int num3 = Math.Min(num2 + 6, array.Length);
		for (int num4 = num2; num4 < num3; num4++)
		{
			TeleportPoint point = array[num4];
			GameObject gameObject2 = CreateRow(content, 62f, 8f);
			gameObject2.AddComponent<Image>().color = CheatMenuStyle.SurfaceInset;
			Outline outline = gameObject2.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.TeleportBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			HorizontalLayoutGroup component = gameObject2.GetComponent<HorizontalLayoutGroup>();
			component.padding = new RectOffset(13, 11, 7, 7);
			component.spacing = 10f;
			CreateTeleportMarkerImage(gameObject2.transform, point.MarkerKind, 28f, compact: false, ParseMarkerColor(point.MarkerColorHex));
			SetLayout(CreateText(gameObject2.transform, $"{point.Name}\n{point.Map}  ·  ({point.X:F1}, {point.Y:F1}, {point.Z:F1})", 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false).gameObject, -1f, -1f, 1f);
			CreateButton(gameObject2.transform, "传送", delegate
			{
				bool flag = _plugin.Actions.Teleport(point);
				SetStatus(flag ? ("已传送到 " + point.Name + "。") : "传送失败：目标被阻挡、地图不同或玩家正在载具中。", 5f, flag ? StatusKind.Success : StatusKind.Error);
			}, 105f, 38f);
			CreateButton(gameObject2.transform, "×", delegate
			{
				ShowTeleportDeleteConfirmation(point);
			}, 44f, 38f, CheatMenuStyle.Danger, 20);
		}
	}

	private void SwitchTeleportView(TeleportView view)
	{
		_teleportView = view;
		_plugin.SetLastTeleportView(view.ToString());
		ShowTab(MenuTab.Teleports);
		SetStatus((view == TeleportView.Map) ? "已切换到地图视图。" : "已切换到传送点视图。", 3f, StatusKind.Info);
	}

	private void ToggleTeleportMarkerDrawer(Button trigger)
	{
		if (_teleportMarkerDrawerOpen)
		{
			HideTeleportMarkerDrawer();
			return;
		}
		_teleportMarkerDrawerOpen = true;
		_teleportMarkerTriggerButton = trigger;
		BuildTeleportMarkerDrawer();
	}

	private void BuildTeleportMarkerDrawer()
	{
		if (_teleportMarkerTriggerButton == null || _root == null)
		{
			return;
		}
		if (_teleportMarkerDrawerRoot != null)
		{
			QueueForDestroy(_teleportMarkerDrawerRoot);
		}
		_teleportMarkerDrawerRoot = null;
		_teleportMarkerDrawerPopup = null;
		_teleportMarkerDrawerOpen = true;
		_teleportMarkerDrawerRoot = CreateObject("TeleportMarkerDrawer", _root.transform, typeof(Image));
		_teleportMarkerDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _teleportMarkerDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_teleportMarkerDrawerPopup = _teleportMarkerDrawerRoot.GetComponent<RectTransform>();
		RectTransform teleportMarkerDrawerPopup = _teleportMarkerDrawerPopup;
		Vector2 anchorMin = (_teleportMarkerDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		teleportMarkerDrawerPopup.anchorMin = anchorMin;
		_teleportMarkerDrawerPopup.pivot = new Vector2(0f, 1f);
		_teleportMarkerDrawerPopup.sizeDelta = new Vector2(108f, 184f);
		VerticalLayoutGroup verticalLayoutGroup = _teleportMarkerDrawerRoot.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		TeleportMarkerKind[] markerKinds = MarkerKinds;
		foreach (TeleportMarkerKind teleportMarkerKind in markerKinds)
		{
			TeleportMarkerKind capturedMarkerKind = teleportMarkerKind;
			SetLayout(CreateMarkerPickerButton(_teleportMarkerDrawerRoot.transform, capturedMarkerKind, delegate
			{
				_teleportMarkerKind = capturedMarkerKind;
				HideTeleportMarkerDrawer();
				ShowTab(MenuTab.Teleports);
			}, 0f, compact: true, 1f).gameObject, -1f, 42f, 1f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_teleportMarkerDrawerPopup);
		PositionTeleportMarkerDrawer();
	}

	private void PositionTeleportMarkerDrawer()
	{
		if (!(_teleportMarkerTriggerButton == null) && !(_teleportMarkerDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _teleportMarkerTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			Vector2 sizeDelta = _teleportMarkerDrawerPopup.sizeDelta;
			sizeDelta.x = component.rect.width;
			_teleportMarkerDrawerPopup.sizeDelta = sizeDelta;
			RectTransform teleportMarkerDrawerPopup = _teleportMarkerDrawerPopup;
			Vector2 anchorMin = (_teleportMarkerDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
			teleportMarkerDrawerPopup.anchorMin = anchorMin;
			bool flag = localPoint.y - component2.rect.yMin < _teleportMarkerDrawerPopup.rect.height + 8f;
			_teleportMarkerDrawerPopup.pivot = (flag ? new Vector2(0f, 0f) : new Vector2(0f, 1f));
			_teleportMarkerDrawerPopup.anchoredPosition = (flag ? localPoint2 : localPoint);
		}
	}

	private void HideTeleportMarkerDrawer()
	{
		_teleportMarkerDrawerOpen = false;
		if (_teleportMarkerDrawerRoot != null)
		{
			QueueForDestroy(_teleportMarkerDrawerRoot);
		}
		_teleportMarkerDrawerRoot = null;
		_teleportMarkerDrawerPopup = null;
		_teleportMarkerTriggerButton = null;
	}

	private static MenuTab ParseMenuTab(string value)
	{
		if (!Enum.TryParse<MenuTab>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(MenuTab), result))
		{
			return MenuTab.Character;
		}
		return result;
	}

	private static TeleportView ParseTeleportView(string value)
	{
		if (!Enum.TryParse<TeleportView>(value, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(TeleportView), result))
		{
			return TeleportView.Map;
		}
		return result;
	}

	private void ShowTeleportDeleteConfirmation(TeleportPoint point)
	{
		HideConfirmDialog();
		_confirmRoot = CreateObject("ConfirmOverlay", _root.transform, typeof(Image));
		Stretch(_confirmRoot.GetComponent<RectTransform>());
		_confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
		GameObject gameObject = CreateObject("ConfirmDialog", _confirmRoot.transform, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(470f, 190f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(22, 22, 18, 18);
		verticalLayoutGroup.spacing = 10f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		CreateText(gameObject.transform, "确认删除传送点？", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateText(gameObject.transform, "“" + point.Name + "”将从当前保存列表中删除。", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		GameObject gameObject2 = CreateRow(gameObject.transform, 38f, 8f);
		CreateFlexibleSpacer(gameObject2.transform);
		Button button = CreateButton(gameObject2.transform, "取消", HideConfirmDialog, 100f, 34f, CheatMenuStyle.Surface);
		CreateButton(gameObject2.transform, "删除", delegate
		{
			HideConfirmDialog();
			if (_plugin.Teleports.Remove(point.Id))
			{
				SetStatus("已删除传送点：" + point.Name + "。", 4f);
				ShowTab(MenuTab.Teleports);
			}
		}, 100f, 34f, CheatMenuStyle.Danger);
		if (EventSystem.current != null)
		{
			EventSystem.current.SetSelectedGameObject(button.gameObject);
		}
	}

	private void HideConfirmDialog()
	{
		if (!(_confirmRoot == null))
		{
			QueueForDestroy(_confirmRoot);
			_confirmRoot = null;
		}
	}

	private void ShowAboutDialog()
	{
		HideAboutDialog();
		HideShortcutDialog();
		if (!(_root == null))
		{
			_aboutRoot = CreateObject("AboutOverlay", _root.transform, typeof(Image));
			Stretch(_aboutRoot.GetComponent<RectTransform>());
			_aboutRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);
			GameObject gameObject = CreateObject("AboutDialog", _aboutRoot.transform, typeof(Image));
			gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.InputBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(760f, 390f);
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(26, 26, 20, 20);
			verticalLayoutGroup.spacing = 9f;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			GameObject gameObject2 = CreateRow(gameObject.transform, 40f, 8f);
			CreateText(gameObject2.transform, "关于此插件", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateFlexibleSpacer(gameObject2.transform);
			Button button = CreateWindowCloseButton(gameObject2.transform, HideAboutDialog);
			CreateText(gameObject.transform, "Unturned Singleplayer Cheat Menu v1.9.0", 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
			CreateText(gameObject.transform, "作者：Blowthesnow", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
			SetLayout(CreateText(gameObject.transform, "此插件完全免费。如果你在其他地方购买了此插件，请立刻申请退款。", 13, CheatMenuStyle.Warning, TextAnchor.MiddleLeft).gameObject, -1f, 46f);
			CreateText(gameObject.transform, "项目地址", 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateButton(gameObject.transform, "https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu", delegate
			{
				Application.OpenURL("https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu");
			}, 0f, 36f, CheatMenuStyle.Surface, 11);
			CreateText(gameObject.transform, "爱发电赞助地址", 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateButton(gameObject.transform, "https://afdian.com/a/Blowthesnow", delegate
			{
				Application.OpenURL("https://afdian.com/a/Blowthesnow");
			}, 0f, 36f, CheatMenuStyle.Surface, 11);
			if (EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(button.gameObject);
			}
		}
	}

	private void HideAboutDialog()
	{
		if (!(_aboutRoot == null))
		{
			QueueForDestroy(_aboutRoot);
			_aboutRoot = null;
		}
	}

	private void ShowShortcutDialog()
	{
		HideShortcutDialog();
		HideAboutDialog();
		if (!(_root == null))
		{
			_shortcutRoot = CreateObject("ShortcutOverlay", _root.transform, typeof(Image));
			Stretch(_shortcutRoot.GetComponent<RectTransform>());
			_shortcutRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);
			GameObject gameObject = CreateObject("ShortcutDialog", _shortcutRoot.transform, typeof(Image));
			gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.InputBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			RectTransform component = gameObject.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			component.sizeDelta = new Vector2(930f, 780f);
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(24, 24, 18, 18);
			verticalLayoutGroup.spacing = 10f;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			GameObject gameObject2 = CreateRow(gameObject.transform, 40f, 8f);
			CreateText(gameObject2.transform, "设置全局快捷键", 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
			CreateFlexibleSpacer(gameObject2.transform);
			Button button = CreateWindowCloseButton(gameObject2.transform, HideShortcutDialog);
			GameObject gameObject3 = CreateObject("ShortcutBody", gameObject.transform);
			SetLayout(gameObject3, -1f, -1f, 1f, 1f);
			GameObject gameObject4 = CreateObject("ShortcutNavigation", gameObject3.transform, typeof(Image));
			gameObject4.GetComponent<Image>().color = CheatMenuStyle.Surface;
			AddVerticalLayout(gameObject4, 6f, new RectOffset(8, 8, 8, 8));
			RectTransform component2 = gameObject4.GetComponent<RectTransform>();
			component2.anchorMin = new Vector2(0f, 0f);
			component2.anchorMax = new Vector2(0f, 1f);
			component2.offsetMin = Vector2.zero;
			component2.offsetMax = new Vector2(142f, 0f);
			GameObject gameObject5 = CreateObject("ShortcutContentPanel", gameObject3.transform, typeof(Image));
			gameObject5.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
			RectTransform component3 = gameObject5.GetComponent<RectTransform>();
			component3.anchorMin = new Vector2(0f, 0f);
			component3.anchorMax = new Vector2(1f, 1f);
			component3.offsetMin = new Vector2(152f, 0f);
			component3.offsetMax = Vector2.zero;
			_shortcutScrollContent = CreateShortcutScroll(gameObject5.transform);
			_shortcutScrollRect = _shortcutScrollContent.GetComponentInParent<ScrollRect>();
			_shortcutScrollRect.scrollSensitivity = 48f;
			AddShortcutScrollbar(_shortcutScrollRect);
			AddShortcutScrollLayout(_shortcutScrollContent.gameObject, 10f, new RectOffset(12, 18, 10, 10));
			RectTransform pluginHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "插件窗口");
			CreateShortcutNavigationButton(gameObject4.transform, "插件窗口", pluginHeader);
			BuildShortcutActionRow(_shortcutScrollContent, "menu.toggle");
			BuildShortcutActionRow(_shortcutScrollContent, "infinite-storage.toggle");
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform inventoryHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "背包整理");
			CreateShortcutNavigationButton(gameObject4.transform, "背包整理", inventoryHeader);
			BuildShortcutActionRow(_shortcutScrollContent, "inventory.arrange.current");
			BuildShortcutActionRow(_shortcutScrollContent, "inventory.arrange.all-carried");
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform characterHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "角色");
			CreateShortcutNavigationButton(gameObject4.transform, "角色", characterHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleGodMode);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleInfiniteNeeds);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.RestoreEverything);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.RefillNeeds);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform movementHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "移动");
			CreateShortcutNavigationButton(gameObject4.transform, "移动", movementHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFlight);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleNoclip);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform pointToolHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "准星");
			CreateShortcutNavigationButton(gameObject4.transform, "准星", pointToolHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.TogglePointToolEnabled);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.NextPointToolMode);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform healthBarsHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "生物生命条");
			CreateShortcutNavigationButton(gameObject4.transform, "生物生命条", healthBarsHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleCreatureHealthBars);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform automationHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "自动化");
			CreateShortcutNavigationButton(gameObject4.transform, "自动化", automationHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleAutoPickup);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleInfiniteStorageEnabled);
			CreateShortcutSectionSpacing(_shortcutScrollContent);
			RectTransform worldHeader = CreateShortcutSectionHeader(_shortcutScrollContent, "世界");
			CreateShortcutNavigationButton(gameObject4.transform, "世界", worldHeader);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFreezeTime);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.SetDay);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.SetNight);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleFullMoon);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ClearWeather);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.DisableWeather);
			BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.CallAirdrop);
			_shortcutCaptureText = CreateText(gameObject.transform, "点击某一行的设置后按下键盘键、鼠标中键或鼠标侧键；Escape 取消。", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
			SetLayout(_shortcutCaptureText.gameObject, -1f, 42f);
			GameObject gameObject6 = CreateRow(gameObject.transform, 38f, 8f);
			CreateFlexibleSpacer(gameObject6.transform);
			CreateButton(gameObject6.transform, "关闭", HideShortcutDialog, 104f, 34f, CheatMenuStyle.Surface);
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(component);
			LayoutRebuilder.ForceRebuildLayoutImmediate(gameObject3.GetComponent<RectTransform>());
			LayoutRebuilder.ForceRebuildLayoutImmediate(component3);
			RebuildShortcutScrollLayout();
			_shortcutScrollRect.verticalNormalizedPosition = Mathf.Clamp01(_shortcutScrollPosition);
			if (EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(button.gameObject);
			}
		}
	}

	private RectTransform CreateShortcutSectionHeader(Transform parent, string title)
	{
		CreateShortcutDivider(parent);
		RectTransform rectTransform = CreateText(parent, title, 13, CheatMenuStyle.Muted, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform;
		CreateShortcutDivider(parent);
		return rectTransform;
	}

	private void CreateShortcutNavigationButton(Transform parent, string title, RectTransform sectionHeader)
	{
		CreateButton(parent, title, delegate
		{
			ScrollShortcutSectionIntoView(sectionHeader);
		}, 0f, 34f, CheatMenuStyle.SurfaceHover);
	}

	private void ScrollShortcutSectionIntoView(RectTransform sectionHeader)
	{
		if (_shortcutScrollRect == null || _shortcutScrollContent == null || sectionHeader == null)
		{
			return;
		}
		RebuildShortcutScrollLayout();
		RectTransform viewport = _shortcutScrollRect.viewport;
		if (viewport == null)
		{
			return;
		}
		float scrollableHeight = _shortcutScrollContent.rect.height - viewport.rect.height;
		if (scrollableHeight <= 0.01f)
		{
			_shortcutScrollRect.verticalNormalizedPosition = 1f;
			return;
		}
		Vector3 worldPoint = sectionHeader.TransformPoint(new Vector3(sectionHeader.rect.center.x, sectionHeader.rect.yMax, 0f));
		float sectionOffset = 0f - _shortcutScrollContent.InverseTransformPoint(worldPoint).y;
		_shortcutScrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(sectionOffset / scrollableHeight);
	}

	private void RebuildShortcutScrollLayout()
	{
		if (_shortcutScrollContent == null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_shortcutScrollContent);
		float preferredHeight = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(_shortcutScrollContent));
		_shortcutScrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
		_shortcutScrollContent.anchoredPosition = new Vector2(_shortcutScrollContent.anchoredPosition.x, 0f);
		LayoutRebuilder.ForceRebuildLayoutImmediate(_shortcutScrollContent);
		Canvas.ForceUpdateCanvases();
	}

	private static VerticalLayoutGroup AddShortcutScrollLayout(GameObject target, float spacing, RectOffset padding)
	{
		VerticalLayoutGroup layout = target.AddComponent<VerticalLayoutGroup>();
		layout.padding = padding;
		layout.spacing = spacing;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		return layout;
	}

	private static RectTransform CreateShortcutScroll(Transform parent)
	{
		GameObject scrollObject = CreateObject("ShortcutScroll", parent, typeof(Image), typeof(ScrollRect));
		Stretch(scrollObject.GetComponent<RectTransform>());
		scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
		SetLayout(scrollObject, -1f, -1f, 1f, 1f);
		LayoutElement layoutElement = scrollObject.GetComponent<LayoutElement>();
		layoutElement.minWidth = 1f;
		layoutElement.minHeight = 1f;

		GameObject viewportObject = CreateObject("Viewport", scrollObject.transform, typeof(RectMask2D));
		RectTransform viewport = viewportObject.GetComponent<RectTransform>();
		Stretch(viewport);

		RectTransform content = CreateObject("ShortcutContent", viewport, typeof(RectTransform)).GetComponent<RectTransform>();
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = Vector2.zero;
		content.offsetMax = Vector2.zero;

		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		scrollRect.viewport = viewport;
		scrollRect.content = content;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 48f;
		return content;
	}

	private static void AddShortcutScrollbar(ScrollRect scrollRect)
	{
		if (scrollRect == null || scrollRect.viewport == null)
		{
			return;
		}
		scrollRect.viewport.offsetMax = new Vector2(-18f, 0f);
		GameObject scrollbarObject = CreateObject("ShortcutScrollbar", scrollRect.transform, typeof(Image), typeof(Scrollbar));
		RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
		scrollbarRect.anchorMin = new Vector2(1f, 0f);
		scrollbarRect.anchorMax = new Vector2(1f, 1f);
		scrollbarRect.pivot = new Vector2(1f, 0.5f);
		scrollbarRect.offsetMin = new Vector2(-14f, 4f);
		scrollbarRect.offsetMax = new Vector2(-2f, -4f);
		scrollbarObject.GetComponent<Image>().color = CheatMenuStyle.InputBorder;
		GameObject slidingArea = CreateObject("SlidingArea", scrollbarRect);
		RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
		Stretch(slidingRect);
		slidingRect.offsetMin = new Vector2(2f, 2f);
		slidingRect.offsetMax = new Vector2(-2f, -2f);
		GameObject handleObject = CreateObject("Handle", slidingRect, typeof(Image));
		RectTransform handleRect = handleObject.GetComponent<RectTransform>();
		Stretch(handleRect);
		Image handleImage = handleObject.GetComponent<Image>();
		handleImage.color = CheatMenuStyle.Accent;
		Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
		scrollbar.handleRect = handleRect;
		scrollbar.targetGraphic = handleImage;
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		scrollRect.verticalScrollbar = scrollbar;
		scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
		scrollRect.verticalScrollbarSpacing = 4f;
	}

	private static void CreateShortcutDivider(Transform parent)
	{
		GameObject gameObject = CreateObject("ShortcutDivider", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.InputBorder;
		SetLayout(gameObject, -1f, 1f, 1f);
	}

	private static void CreateShortcutSectionSpacing(Transform parent)
	{
		SetLayout(CreateObject("ShortcutSectionSpacing", parent), -1f, 24f, 1f);
	}

	private void BuildShortcutActionRow(Transform parent, string actionId)
	{
		GameObject gameObject = CreateRow(parent, 40f, 8f);
		Text actionText = CreateText(gameObject.transform, _plugin.GetShortcutDisplayName(actionId), 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft);
		actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(actionText.gameObject, 240f);
		Text shortcutText = CreateText(gameObject.transform, _plugin.GetShortcutLabel(actionId), 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		shortcutText.horizontalOverflow = HorizontalWrapMode.Overflow;
		SetLayout(shortcutText.gameObject, 160f, 40f);
		CreateButton(gameObject.transform, "设置", delegate
		{
			BeginShortcutCapture(actionId);
		}, 82f, 34f, CheatMenuStyle.Accent);
		CreateButton(gameObject.transform, "清除", delegate
		{
			ApplyShortcut(new KeyboardShortcut(KeyCode.None), actionId);
		}, 82f, 34f, CheatMenuStyle.Surface);
		CreateButton(gameObject.transform, "默认", delegate
		{
			ApplyShortcut(_plugin.GetShortcutDefault(actionId), actionId);
		}, 82f, 34f, CheatMenuStyle.Surface);
	}

	private void HideShortcutDialog()
	{
		_shortcutCaptureActive = false;
		_shortcutCaptureModifiers.Clear();
		if (_shortcutScrollRect != null)
		{
			_shortcutScrollPosition = _shortcutScrollRect.verticalNormalizedPosition;
		}
		_shortcutScrollRect = null;
		_shortcutScrollContent = null;
		_shortcutCaptureText = null;
		if (!(_shortcutRoot == null))
		{
			QueueForDestroy(_shortcutRoot);
			_shortcutRoot = null;
		}
	}

	private void BeginShortcutCapture(string actionId)
	{
		_shortcutCaptureModifiers.Clear();
		_shortcutCaptureActionId = actionId;
		_shortcutCaptureActive = true;
		if (_shortcutCaptureText != null)
		{
			_shortcutCaptureText.text = PluginLocalization.Translate("按键监听中，请按下键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，已按住的 Ctrl / Alt / Shift / Win 会作为组合键。");
		}
	}

	private void CaptureShortcutInput()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			HideShortcutDialog();
			return;
		}
		KeyCode[] modifierKeys = ShortcutInputPolicy.ModifierKeys;
		foreach (KeyCode key in modifierKeys)
		{
			if (Input.GetKey(key))
			{
				_shortcutCaptureModifiers.Add(ShortcutInputPolicy.NormalizeModifier(key));
			}
		}
		modifierKeys = ShortcutInputPolicy.KeyboardMainKeys;
		foreach (KeyCode keyCode in modifierKeys)
		{
			if (Input.GetKeyDown(keyCode))
			{
				ApplyShortcut(new KeyboardShortcut(keyCode, _shortcutCaptureModifiers.ToArray()), _shortcutCaptureActionId);
				return;
			}
		}
		modifierKeys = ShortcutInputPolicy.MouseKeys;
		foreach (KeyCode keyCode2 in modifierKeys)
		{
			int mouseButtonIndex = ShortcutInputPolicy.GetMouseButtonIndex(keyCode2);
			if (mouseButtonIndex >= 0 && Input.GetMouseButtonDown(mouseButtonIndex))
			{
				ApplyShortcut(new KeyboardShortcut(keyCode2, _shortcutCaptureModifiers.ToArray()), _shortcutCaptureActionId);
				break;
			}
		}
	}

	private void ApplyShortcut(KeyboardShortcut shortcut, string actionId)
	{
		bool flag = _plugin.SetShortcut(actionId, shortcut);
		_shortcutCaptureActive = false;
		_shortcutCaptureModifiers.Clear();
		ShowShortcutDialog();
		RefreshHeader();
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		SetStatus(flag ? "全局快捷键已更新。" : "全局快捷键已更新，但配置文件保存失败。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
	}

	private void BuildOtherTab()
	{
		Transform transform = _contentHost.transform;
		AddVerticalLayout(_contentHost, 9f, new RectOffset(12, 12, 10, 10));
		uint cycle = Math.Max(1u, LightingManager.cycle);
		float num = (float)LightingManager.time / (float)cycle;
		CreateSection(transform, "时间");
		_timeCurrentText = CreateLabel(transform, $"当前时间：{LightingManager.time:N0} / {cycle:N0}（{num:P0}）", 14);
		GameObject gameObject = CreateRow(transform, 38f, 10f);
		_timeSlider = CreateTimeSlider(gameObject.transform, num, delegate(float value)
		{
			UpdateTimePreview(value, cycle);
			int percent = Mathf.Clamp(Mathf.RoundToInt(value * 100f), 0, 99);
			_timeApplyDebouncer.Schedule(delegate
			{
				ApplyTimePercent(percent, cycle);
			});
		});
		SetLayout(_timeSlider.gameObject, 0f, -1f, 1f);
		CreateAction(gameObject.transform, "设为白天", delegate
		{
			_timeApplyDebouncer.Cancel();
			_plugin.Actions.SetDay();
			SetStatus("已切换到白天。");
		});
		CreateAction(gameObject.transform, "设为夜晚", delegate
		{
			_timeApplyDebouncer.Cancel();
			_plugin.Actions.SetNight();
			SetStatus("已切换到夜晚。");
		});
		_timePreviewText = CreateLabel(transform, $"预览：{Mathf.RoundToInt(num * 100f)}%", 12, CheatMenuStyle.Muted);
		GameObject gameObject2 = CreateRow(transform, 42f, 8f);
		CreateToggleAction(gameObject2.transform, "冻结时间", _plugin.Actions.FreezeTimeEnabled, delegate(bool value)
		{
			_plugin.Actions.SetFreezeTime(value);
			SetStatus(value ? "时间已冻结。" : "时间已恢复流动。");
		});
		CreateToggleAction(gameObject2.transform, "强制满月", LightingManager.isFullMoon, delegate(bool value)
		{
			_plugin.Actions.SetFullMoon(value);
			SetStatus(value ? "已开启满月。" : "已关闭满月。");
		});
		CreateSection(transform, "世界事件");
		GameObject gameObject3 = CreateRow(transform, 42f, 8f);
		CreateAction(gameObject3.transform, "立即呼叫空投", delegate
		{
			bool flag = _plugin.Actions.CallAirdrop();
			SetStatus(flag ? "空投已呼叫。" : "此地图没有可用的空投节点或货物表。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
		});
		CreateAction(gameObject3.transform, "下雨", delegate
		{
			bool flag = _plugin.Actions.StartRain();
			SetStatus(flag ? "已触发雨天。" : "当前地图没有默认雨天资产。", 3f, flag ? StatusKind.Success : StatusKind.Warning);
		});
		CreateAction(gameObject3.transform, "暴雪", delegate
		{
			bool flag = _plugin.Actions.StartSnow();
			SetStatus(flag ? "已触发暴雪。" : "当前地图没有默认雪天资产。", 3f, flag ? StatusKind.Success : StatusKind.Warning);
		});
		GameObject gameObject4 = CreateRow(transform, 42f, 8f);
		CreateAction(gameObject4.transform, "清除当前天气", delegate
		{
			_plugin.Actions.ClearWeather();
			SetStatus("当前天气已清除，后续仍可自然调度。");
		});
		CreateAction(gameObject4.transform, "关闭天气调度", delegate
		{
			_plugin.Actions.DisableWeather();
			SetStatus("当前天气和自然天气调度已关闭。", 3f, StatusKind.Warning);
		});
		CreateSection(transform, "扫描信息");
		CreateLabel(transform, $"物品：{_plugin.Catalog.Items.Count}（模组 {_plugin.Catalog.WorkshopItemCount}）    车辆：{_plugin.Catalog.Vehicles.Count}（模组 {_plugin.Catalog.WorkshopVehicleCount}）", 14);
		CreateLabel(transform, "只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。", 12, CheatMenuStyle.Muted);
		GameObject gameObject5 = CreateRow(transform, 40f, 8f);
		CreateButton(gameObject5.transform, "重新扫描模组资产", delegate
		{
			_plugin.RefreshCatalog();
			SetStatus("资产目录已重新扫描。", 4f, StatusKind.Info);
		}, 180f, 36f, CheatMenuStyle.SurfaceRaised);
	}

	private void CreateToggleAction(Transform parent, string label, bool current, Action<bool> change, string tooltip = null)
	{
		GameObject gameObject = CreateObject("Toggle", parent, typeof(Image), typeof(Toggle));
		Image component = gameObject.GetComponent<Image>();
		component.color = (current ? CheatMenuStyle.Accent : CheatMenuStyle.Surface);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.ToggleBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		Toggle component2 = gameObject.GetComponent<Toggle>();
		component2.targetGraphic = component;
		ColorBlock colors = component2.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
		colors.pressedColor = new Color(0.82f, 0.88f, 0.94f, 1f);
		colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
		component2.colors = colors;
		Text text = CreateText(gameObject.transform, "✓", 17, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
		text.rectTransform.anchorMin = new Vector2(0f, 0.1f);
		text.rectTransform.anchorMax = new Vector2(0f, 0.9f);
		text.rectTransform.pivot = new Vector2(0f, 0.5f);
		text.rectTransform.sizeDelta = new Vector2(30f, 0f);
		text.rectTransform.anchoredPosition = new Vector2(7f, 0f);
		component2.graphic = text;
		Text text2 = CreateText(gameObject.transform, label + "  " + (current ? "ON" : "OFF"), 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		SetOffsets(text2.rectTransform, 43f, 8f, 2f, 2f);
		GameObject gameObject2 = CreateObject("FocusIndicator", gameObject.transform, typeof(Image));
		Image component3 = gameObject2.GetComponent<Image>();
		component3.color = CheatMenuStyle.AccentBright;
		component3.raycastTarget = false;
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0f);
		component4.anchorMax = new Vector2(1f, 0f);
		component4.pivot = new Vector2(0.5f, 0f);
		component4.anchoredPosition = Vector2.zero;
		component4.sizeDelta = new Vector2(0f, 3f);
		gameObject2.SetActive(value: false);
		OverlaySelectableVisual toggleVisual = gameObject.AddComponent<OverlaySelectableVisual>();
		toggleVisual.Initialize(component2, component, text2, gameObject2, outline, component.color);
		toggleVisual.SetHoverExplanation(tooltip, ShowHoverExplanation, HideHoverExplanation);
		component2.isOn = current;
		component2.onValueChanged.AddListener(delegate(bool value)
		{
			toggleVisual.SetBaseColor(value ? CheatMenuStyle.Accent : CheatMenuStyle.Surface);
			text2.text = PluginLocalization.Translate(label) + "  " + (value ? "ON" : "OFF");
			change(value);
		});
		SetLayout(gameObject, -1f, 40f, 1f);
	}

	private void CreateAction(Transform parent, string label, System.Action action)
	{
		SetLayout(CreateButton(parent, label, action, 0f, 40f).gameObject, -1f, -1f, 1f);
	}

	private void CreatePrimaryAction(Transform parent, string label, System.Action action)
	{
		SetLayout(CreateButton(parent, label, action, 0f, 40f, CheatMenuStyle.Accent).gameObject, -1f, -1f, 1f);
	}

	private void CreateSection(Transform parent, string text)
	{
		GameObject gameObject = CreateRow(parent, 30f, 8f);
		GameObject gameObject2 = CreateObject("SectionMarker", gameObject.transform, typeof(Image));
		Image component = gameObject2.GetComponent<Image>();
		component.color = CheatMenuStyle.Accent;
		component.raycastTarget = false;
		SetLayout(gameObject2, 4f, 18f, 0f);
		SetLayout(CreateText(gameObject.transform, text, 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold).gameObject, -1f, -1f, 1f);
	}

	private void CreateSummaryPanel(Transform parent, string text)
	{
		GameObject gameObject = CreateObject("Summary", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.SurfaceRaised;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.SummaryBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		SetLayout(gameObject, -1f, 42f);
		SetOffsets(CreateText(gameObject.transform, text, 14, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform, 12f, 12f, 4f, 4f);
	}

	private void CreateEmptyState(Transform parent, string title, string hint)
	{
		GameObject gameObject = CreateObject("EmptyState", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.SurfaceRaised;
		AddVerticalLayout(gameObject, 4f, new RectOffset(18, 18, 14, 14)).childAlignment = TextAnchor.MiddleCenter;
		SetLayout(gameObject, -1f, 92f, 1f);
		SetLayout(CreateText(gameObject.transform, title, 16, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold).gameObject, -1f, 28f);
		SetLayout(CreateText(gameObject.transform, hint, 12, CheatMenuStyle.Muted, TextAnchor.MiddleCenter).gameObject, -1f, 24f);
	}

	private Text CreateLabel(Transform parent, string text, int size, Color? color = null, float width = 0f, TextAnchor alignment = TextAnchor.MiddleLeft)
	{
		Text text2 = CreateText(parent, text, size, color ?? CheatMenuStyle.Text, alignment);
		SetLayout(text2.gameObject, (width > 0f) ? width : (-1f), 27f);
		return text2;
	}

	private void CreateLabeledInput(Transform parent, string label, string value, Action<string> changed, float width = 120f, bool password = false, bool commitOnEndEdit = false, float labelWidth = 0f, string tooltip = null)
	{
		GameObject gameObject = CreateRow(parent, 38f, 6f);
		SetLayout(gameObject, width, -1f, (width <= 0f) ? 1f : 0f);
		string text = PluginLocalization.Translate(label);
		float width2 = ((labelWidth > 0f) ? labelWidth : Mathf.Clamp((float)text.Length * 12f + 12f, 86f, 150f));
		CreateLabel(gameObject.transform, text, 12, CheatMenuStyle.Muted, width2, TextAnchor.MiddleRight);
		CreateInput(gameObject.transform, value, changed, 0f, 1f, password, commitOnEndEdit, tooltip);
	}

	private InputField CreateInput(Transform parent, string value, Action<string> changed, float width = 0f, float flexibleWidth = 0f, bool password = false, bool commitOnEndEdit = false, string hoverExplanation = null)
	{
		GameObject gameObject = CreateObject("Input", parent, typeof(Image), typeof(InputField));
		Image component = gameObject.GetComponent<Image>();
		component.color = CheatMenuStyle.SurfaceInput;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		InputField component2 = gameObject.GetComponent<InputField>();
		component2.targetGraphic = component;
		ColorBlock colors = component2.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.05f, 1.08f, 1.12f, 1f);
		colors.pressedColor = new Color(0.9f, 0.94f, 1f, 1f);
		colors.selectedColor = new Color(0.95f, 1f, 1f, 1f);
		colors.disabledColor = new Color(0.45f, 0.48f, 0.52f, 0.55f);
		component2.colors = colors;
		component2.text = value ?? string.Empty;
		component2.lineType = InputField.LineType.SingleLine;
		component2.contentType = (password ? InputField.ContentType.Password : InputField.ContentType.Standard);
		Text text = CreateText(gameObject.transform, component2.text, 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		SetOffsets(text.rectTransform, 9f, 9f, 2f, 2f);
		component2.textComponent = text;
		Text text2 = CreateText(gameObject.transform, "输入…", 13, CheatMenuStyle.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
		SetOffsets(text2.rectTransform, 9f, 9f, 2f, 2f);
		component2.placeholder = text2;
		if (commitOnEndEdit)
		{
			component2.onEndEdit.AddListener(changed.Invoke);
		}
		else
		{
			component2.onValueChanged.AddListener(changed.Invoke);
		}
		GameObject gameObject2 = CreateObject("FocusIndicator", gameObject.transform, typeof(Image));
		Image component3 = gameObject2.GetComponent<Image>();
		component3.color = CheatMenuStyle.AccentBright;
		component3.raycastTarget = false;
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0f);
		component4.anchorMax = new Vector2(0f, 1f);
		component4.pivot = new Vector2(0f, 0.5f);
		component4.anchoredPosition = Vector2.zero;
		component4.sizeDelta = new Vector2(3f, 0f);
		gameObject2.SetActive(value: false);
		OverlaySelectableVisual overlaySelectableVisual = gameObject.AddComponent<OverlaySelectableVisual>();
		overlaySelectableVisual.Initialize(component2, component, text, gameObject2, outline, component.color);
		overlaySelectableVisual.SetHoverExplanation(hoverExplanation, ShowHoverExplanation, HideHoverExplanation);
		SetLayout(gameObject, (width > 0f) ? width : (-1f), 34f, flexibleWidth);
		return component2;
	}

	private Slider CreateTimeSlider(Transform parent, float normalized, Action<float> changed)
	{
		GameObject gameObject = CreateObject("TimeSlider", parent, typeof(Image), typeof(Slider));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.SliderBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		Slider component = gameObject.GetComponent<Slider>();
		component.minValue = 0f;
		component.maxValue = 1f;
		component.value = Mathf.Clamp01(normalized);
		component.direction = Slider.Direction.LeftToRight;
		SetLayout(gameObject, 170f, 22f, 0f, 0f);
		GameObject gameObject2 = CreateObject("FillArea", gameObject.transform, typeof(Image));
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		gameObject2.GetComponent<Image>().color = CheatMenuStyle.SliderTrack;
		Outline outline2 = gameObject2.AddComponent<Outline>();
		outline2.effectColor = CheatMenuStyle.InputBorder;
		outline2.effectDistance = Vector2.one;
		outline2.useGraphicAlpha = false;
		component2.anchorMin = new Vector2(0f, 0.34f);
		component2.anchorMax = new Vector2(1f, 0.66f);
		component2.offsetMin = new Vector2(7f, 0f);
		component2.offsetMax = new Vector2(-7f, 0f);
		GameObject gameObject3 = CreateObject("Fill", gameObject2.transform, typeof(Image));
		gameObject3.GetComponent<Image>().color = CheatMenuStyle.Accent;
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = new Vector2(0f, 1f);
		component3.offsetMin = Vector2.zero;
		component3.offsetMax = Vector2.zero;
		component.fillRect = component3;
		GameObject gameObject4 = CreateObject("Handle", gameObject.transform, typeof(Image));
		Image component4 = gameObject4.GetComponent<Image>();
		component4.sprite = GetSliderGripSprite();
		component4.color = Color.white;
		component4.preserveAspect = true;
		Outline outline3 = gameObject4.AddComponent<Outline>();
		outline3.effectColor = CheatMenuStyle.SliderBorder;
		outline3.effectDistance = Vector2.one;
		outline3.useGraphicAlpha = false;
		RectTransform component5 = gameObject4.GetComponent<RectTransform>();
		component5.sizeDelta = new Vector2(14f, 18f);
		component5.anchorMin = new Vector2(0f, 0.5f);
		component5.anchorMax = new Vector2(0f, 0.5f);
		component5.pivot = new Vector2(0.5f, 0.5f);
		component.handleRect = component5;
		component.onValueChanged.AddListener(changed.Invoke);
		return component;
	}

	private void RefreshOtherTime()
	{
		uint num = Math.Max(1u, LightingManager.cycle);
		float num2 = Mathf.Clamp01((float)LightingManager.time / (float)num);
		if (_timeCurrentText != null)
		{
			_timeCurrentText.text = $"当前时间：{LightingManager.time:N0} / {num:N0}（{num2:P0}）";
		}
		if (!(_timeSlider != null) || !Input.GetMouseButton(0) || !(EventSystem.current != null) || !(EventSystem.current.currentSelectedGameObject == _timeSlider.gameObject))
		{
			if (_timeSlider != null)
			{
				_timeSlider.SetValueWithoutNotify(num2);
			}
			UpdateTimePreview(num2, num);
		}
	}

	private void UpdateTimePreview(float normalized, uint cycle)
	{
		if (_timePreviewText != null)
		{
			_timePreviewText.text = $"预览：{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%";
		}
	}

	private void ApplyTimePercent(int percent, uint cycle)
	{
		percent = Mathf.Clamp(percent, 0, 99);
		_plugin.Actions.SetTime((uint)((float)cycle * ((float)percent / 100f)));
		if (_timeSlider != null)
		{
			_timeSlider.SetValueWithoutNotify((float)percent / 100f);
		}
		UpdateTimePreview((float)percent / 100f, cycle);
		SetStatus($"时间已设置为周期的 {percent}% 。");
	}

	private void UpdateItemAmountInput()
	{
		if (_itemAmountInput != null)
		{
			_itemAmountInput.SetTextWithoutNotify(_itemAmountText);
		}
	}

	private void UpdateVehicleAmountInput()
	{
		if (_vehicleAmountInput != null)
		{
			_vehicleAmountInput.SetTextWithoutNotify(_vehicleAmountText);
		}
	}

	private Button CreateMarkerPickerButton(Transform parent, TeleportMarkerKind markerKind, System.Action action, float width, bool compact, float flexibleWidth = 0f)
	{
		Color value = ((_teleportMarkerKind == markerKind) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover);
		float num = (compact ? 34f : 54f);
		Button button = CreateButton(parent, string.Empty, action, width, num, value, 11);
		SetLayout(button.gameObject, (width > 0f) ? width : (-1f), num, flexibleWidth);
		Text componentInChildren = button.GetComponentInChildren<Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: false);
		}
		CreateTeleportMarkerImage(button.transform, markerKind, compact ? 22f : 26f, compact, _teleportMarkerColor);
		if (compact)
		{
			Text text = CreateText(button.transform, PluginLocalization.Translate(MarkerLabel(markerKind)), 10, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
			text.rectTransform.anchorMin = new Vector2(0f, 0f);
			text.rectTransform.anchorMax = new Vector2(1f, 1f);
			text.rectTransform.offsetMin = new Vector2(34f, 2f);
			text.rectTransform.offsetMax = new Vector2(-4f, -2f);
		}
		else
		{
			Text text2 = CreateText(button.transform, PluginLocalization.Translate(MarkerLabel(markerKind)), 10, CheatMenuStyle.Text, TextAnchor.LowerCenter, FontStyle.Bold);
			text2.rectTransform.anchorMin = Vector2.zero;
			text2.rectTransform.anchorMax = Vector2.one;
			text2.rectTransform.offsetMin = new Vector2(2f, 1f);
			text2.rectTransform.offsetMax = new Vector2(-2f, 19f);
		}
		return button;
	}

	private Button CreateColorPickerButton(Transform parent)
	{
		Button button = CreateButton(parent, string.Empty, ShowTeleportColorPicker, 82f, 34f, CheatMenuStyle.SurfaceHover, 11);
		Text componentInChildren = button.GetComponentInChildren<Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: false);
		}
		GameObject gameObject = CreateObject("ColorSwatch", button.transform, typeof(Image));
		Image component = gameObject.GetComponent<Image>();
		component.color = _teleportMarkerColor;
		component.raycastTarget = false;
		RectTransform rectTransform = component.rectTransform;
		rectTransform.anchorMin = new Vector2(0f, 0.5f);
		rectTransform.anchorMax = new Vector2(0f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.anchoredPosition = new Vector2(17f, 0f);
		rectTransform.sizeDelta = new Vector2(20f, 20f);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = Color.white;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		Text text = CreateText(button.transform, "颜色", 11, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		text.rectTransform.anchorMin = Vector2.zero;
		text.rectTransform.anchorMax = Vector2.one;
		text.rectTransform.offsetMin = new Vector2(33f, 2f);
		text.rectTransform.offsetMax = new Vector2(-4f, -2f);
		return button;
	}

	private void ShowVehicleThumbnailSettings()
	{
		HideVehicleThumbnailSettings();
		VehicleThumbnailRenderSettings vehicleThumbnailSettings = _plugin.VehicleThumbnailSettings;
		_vehicleThumbnailDraftWidth = vehicleThumbnailSettings?.Width ?? 128;
		_vehicleThumbnailDraftFraming = vehicleThumbnailSettings?.Framing ?? 1f;
		_vehicleThumbnailSettingsRoot = CreateObject("VehicleThumbnailSettingsOverlay", _root.transform, typeof(Image));
		Stretch(_vehicleThumbnailSettingsRoot.GetComponent<RectTransform>());
		_vehicleThumbnailSettingsRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("VehicleThumbnailSettingsDialog", _vehicleThumbnailSettingsRoot.transform, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(680f, 560f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(28, 28, 20, 20);
		verticalLayoutGroup.spacing = 12f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject2 = CreateRow(gameObject.transform, 44f, 10f);
		CreateText(gameObject2.transform, "载具渲染设置", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "×", HideVehicleThumbnailSettings, 44f, 42f, CheatMenuStyle.Surface, 23);
		CreateLabel(gameObject.transform, "缩略图尺寸只影响车辆卡片，不会修改游戏全局画质。已生成的缩略图会进入缓存，重复打开不会重复实例化车辆。", 12, CheatMenuStyle.Muted);
		CreateLabel(gameObject.transform, "尺寸", 15, CheatMenuStyle.Text);
		GameObject gameObject3 = CreateRow(gameObject.transform, 38f, 8f);
		List<Button> resolutionButtons = new List<Button>();
		int[] resolutions = new int[3] { 128, 192, 256 };
		string[] array = new string[3] { "128 × 96（低开销）", "192 × 144（平衡）", "256 × 192（更清晰）" };
		for (int i = 0; i < resolutions.Length; i++)
		{
			int capturedResolution = resolutions[i];
			Button button = CreateButton(gameObject3.transform, array[i], delegate
			{
				_vehicleThumbnailDraftWidth = capturedResolution;
				for (int j = 0; j < resolutionButtons.Count; j++)
				{
					bool flag = resolutions[j] == _vehicleThumbnailDraftWidth;
					resolutionButtons[j].targetGraphic.color = (flag ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover);
				}
			}, 0f, 34f, (capturedResolution == _vehicleThumbnailDraftWidth) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12);
			SetLayout(button.gameObject, -1f, -1f, 1f);
			resolutionButtons.Add(button);
		}
		CreateLabel(gameObject.transform, "128 × 96 为默认，生成更快且占用更低；192 × 144 是清晰度与开销的折中；256 × 192 的输出像素约为默认的 4 倍，首次生成更慢。", 12, CheatMenuStyle.Muted);
		GameObject gameObject4 = CreateRow(gameObject.transform, 34f, 12f);
		_vehicleThumbnailFramingText = CreateText(gameObject4.transform, string.Empty, 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
		SetLayout(_vehicleThumbnailFramingText.gameObject, 125f);
		_vehicleThumbnailFramingSlider = CreateTimeSlider(gameObject4.transform, (VehicleThumbnailRenderSettings.NormalizeFraming(_vehicleThumbnailDraftFraming) - 0.5f) / 1f, delegate(float normalized)
		{
			_vehicleThumbnailDraftFraming = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(normalized));
			UpdateVehicleThumbnailFramingText();
		});
		UpdateVehicleThumbnailFramingText();
		CreateLabel(gameObject.transform, "取景倍率 0.5：主体更大、留白更少，可能接近裁切边界；1.0：默认平衡值；1.5：留白更多、主体更小，裁切风险更低。", 12, CheatMenuStyle.Muted);
		GameObject gameObject5 = CreateRow(gameObject.transform, 42f, 8f);
		CreateFlexibleSpacer(gameObject5.transform);
		CreateButton(gameObject5.transform, "取消", HideVehicleThumbnailSettings, 96f);
		CreateButton(gameObject5.transform, "应用", delegate
		{
			bool flag = _plugin.ApplyVehicleThumbnailSettings(_vehicleThumbnailDraftWidth, _vehicleThumbnailDraftFraming);
			HideVehicleThumbnailSettings();
			SetStatus(flag ? "车辆缩略图设置已应用并保存。" : "车辆缩略图设置已应用，但配置文件保存失败。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
		}, 96f, 36f, CheatMenuStyle.Accent);
	}

	private void UpdateVehicleThumbnailFramingText()
	{
		if (_vehicleThumbnailFramingText != null)
		{
			_vehicleThumbnailFramingText.text = string.Format("{0}：{1:0.00}", PluginLocalization.Translate("取景倍率"), _vehicleThumbnailDraftFraming);
		}
	}

	private void HideVehicleThumbnailSettings()
	{
		if (!(_vehicleThumbnailSettingsRoot == null))
		{
			QueueForDestroy(_vehicleThumbnailSettingsRoot);
			_vehicleThumbnailSettingsRoot = null;
			_vehicleThumbnailFramingSlider = null;
			_vehicleThumbnailFramingText = null;
		}
	}

	private void ShowTeleportColorPicker()
	{
		HideTeleportColorPicker();
		_teleportColorDraft = _teleportMarkerColor;
		_teleportColorPickerRoot = CreateObject("TeleportColorPickerOverlay", _root.transform, typeof(Image));
		Stretch(_teleportColorPickerRoot.GetComponent<RectTransform>());
		_teleportColorPickerRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("TeleportColorPickerDialog", _teleportColorPickerRoot.transform, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(780f, 620f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(28, 28, 20, 20);
		verticalLayoutGroup.spacing = 12f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject2 = CreateRow(gameObject.transform, 48f, 10f);
		GameObject gameObject3 = CreateObject("HeaderColor", gameObject2.transform, typeof(Image));
		gameObject3.GetComponent<Image>().color = _teleportColorDraft;
		gameObject3.GetComponent<Image>().raycastTarget = false;
		SetLayout(gameObject3, 34f, 34f);
		Outline outline2 = gameObject3.AddComponent<Outline>();
		outline2.effectColor = Color.white;
		outline2.effectDistance = Vector2.one;
		outline2.useGraphicAlpha = false;
		CreateText(gameObject2.transform, "选择颜色", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "×", HideTeleportColorPicker, 44f, 42f, CheatMenuStyle.Surface, 23);
		GameObject gameObject4 = CreateRow(gameObject.transform, 0f, 26f);
		SetLayout(gameObject4, -1f, -1f, -1f, 1f);
		GameObject gameObject5 = CreateObject("PreviewColumn", gameObject4.transform, typeof(Image));
		gameObject5.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
		AddVerticalLayout(gameObject5, 8f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject5, 326f, -1f, 0f, 1f);
		CreateLabel(gameObject5.transform, "预览", 16, CheatMenuStyle.Text);
		GameObject gameObject6 = CreateObject("Preview", gameObject5.transform, typeof(Image));
		_teleportColorPreview = gameObject6.GetComponent<Image>();
		_teleportColorPreview.raycastTarget = false;
		SetLayout(gameObject6, -1f, 158f, 1f);
		Outline outline3 = gameObject6.AddComponent<Outline>();
		outline3.effectColor = CheatMenuStyle.InputBorder;
		outline3.effectDistance = Vector2.one;
		outline3.useGraphicAlpha = false;
		GameObject gameObject7 = CreateObject("PreviewHex", gameObject6.transform, typeof(Image));
		gameObject7.GetComponent<Image>().color = new Color(0.02f, 0.12f, 0.22f, 0.76f);
		RectTransform component2 = gameObject7.GetComponent<RectTransform>();
		anchorMin = (component2.anchorMax = new Vector2(0.5f, 0.5f));
		component2.anchorMin = anchorMin;
		component2.pivot = new Vector2(0.5f, 0.5f);
		component2.sizeDelta = new Vector2(132f, 48f);
		_teleportColorHexPreview = CreateText(gameObject7.transform, string.Empty, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, localize: false);
		Stretch(_teleportColorHexPreview.rectTransform);
		CreateLabel(gameObject5.transform, "常用颜色", 16, CheatMenuStyle.Text);
		GameObject gameObject8 = CreateObject("PresetGrid", gameObject5.transform);
		GridLayoutGroup gridLayoutGroup = gameObject8.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 6;
		gridLayoutGroup.cellSize = new Vector2(48f, 42f);
		gridLayoutGroup.spacing = new Vector2(10f, 8f);
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		SetLayout(gameObject8, -1f, 142f, 1f);
		_teleportColorSwatches.Clear();
		string[] teleportMarkerColorPresets = TeleportMarkerColorPresets;
		for (int i = 0; i < teleportMarkerColorPresets.Length; i++)
		{
			if (ColorUtility.TryParseHtmlString(teleportMarkerColorPresets[i], out var color))
			{
				CreateColorSwatchButton(gameObject8.transform, color);
			}
		}
		GameObject gameObject9 = CreateObject("ColorControls", gameObject4.transform, typeof(Image));
		gameObject9.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
		AddVerticalLayout(gameObject9, 8f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject9, -1f, -1f, 1f, 1f);
		CreateLabel(gameObject9.transform, "颜色值", 16, CheatMenuStyle.Text);
		_teleportColorHexInput = CreateInput(gameObject9.transform, string.Empty, delegate(string value)
		{
			TryUpdateTeleportDraftFromHex(value);
		}, 0f, 1f);
		SetLayout(_teleportColorHexInput.gameObject, -1f, 38f, 1f);
		_teleportColorHexInput.onEndEdit.AddListener(TryUpdateTeleportDraftFromHex);
		CreateLabel(gameObject9.transform, "通道", 16, CheatMenuStyle.Text);
		GameObject gameObject10 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject10.transform, "红", 13, CheatMenuStyle.Text, 32f);
		_teleportColorRedSlider = CreateColorChannelSlider(gameObject10.transform, _teleportColorDraft.r * 255f, new Color(0.75f, 0.24f, 0.24f, 1f), delegate(int value)
		{
			_teleportColorDraft.r = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorRedValue);
		GameObject gameObject11 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject11.transform, "绿", 13, CheatMenuStyle.Text, 32f);
		_teleportColorGreenSlider = CreateColorChannelSlider(gameObject11.transform, _teleportColorDraft.g * 255f, new Color(0.25f, 0.68f, 0.3f, 1f), delegate(int value)
		{
			_teleportColorDraft.g = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorGreenValue);
		GameObject gameObject12 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject12.transform, "蓝", 13, CheatMenuStyle.Text, 32f);
		_teleportColorBlueSlider = CreateColorChannelSlider(gameObject12.transform, _teleportColorDraft.b * 255f, new Color(0.22f, 0.5f, 0.78f, 1f), delegate(int value)
		{
			_teleportColorDraft.b = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorBlueValue);
		GameObject gameObject13 = CreateRow(gameObject.transform, 48f, 10f);
		CreateFlexibleSpacer(gameObject13.transform);
		CreateButton(gameObject13.transform, "取消", HideTeleportColorPicker, 110f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject13.transform, "确定", delegate
		{
			_teleportMarkerColor = _teleportColorDraft;
			HideTeleportColorPicker();
			ShowTab(MenuTab.Teleports);
		}, 110f, 38f, CheatMenuStyle.Accent);
		UpdateTeleportColorPickerControls();
		if (EventSystem.current != null && _teleportColorHexInput != null)
		{
			EventSystem.current.SetSelectedGameObject(_teleportColorHexInput.gameObject);
		}
	}

	private void HideTeleportColorPicker()
	{
		if (_teleportColorPickerRoot != null)
		{
			QueueForDestroy(_teleportColorPickerRoot);
		}
		_teleportColorPickerRoot = null;
		_teleportColorPreview = null;
		_teleportColorHexPreview = null;
		_teleportColorHexInput = null;
		_teleportColorRedSlider = null;
		_teleportColorGreenSlider = null;
		_teleportColorBlueSlider = null;
		_teleportColorRedValue = null;
		_teleportColorGreenValue = null;
		_teleportColorBlueValue = null;
		_teleportColorSwatches.Clear();
	}

	private void UpdateTeleportColorPickerControls()
	{
		if (_teleportColorPreview != null)
		{
			_teleportColorPreview.color = _teleportColorDraft;
		}
		if (_teleportColorHexPreview != null)
		{
			_teleportColorHexPreview.text = ToMarkerColorHex(_teleportColorDraft);
		}
		if (_teleportColorHexInput != null)
		{
			_teleportColorHexInput.SetTextWithoutNotify(ToMarkerColorHex(_teleportColorDraft));
		}
		int num = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.r) * 255f);
		int num2 = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.g) * 255f);
		int num3 = Mathf.RoundToInt(Mathf.Clamp01(_teleportColorDraft.b) * 255f);
		if (_teleportColorRedSlider != null)
		{
			_teleportColorRedSlider.SetValueWithoutNotify(num);
		}
		if (_teleportColorGreenSlider != null)
		{
			_teleportColorGreenSlider.SetValueWithoutNotify(num2);
		}
		if (_teleportColorBlueSlider != null)
		{
			_teleportColorBlueSlider.SetValueWithoutNotify(num3);
		}
		if (_teleportColorRedValue != null)
		{
			_teleportColorRedValue.text = num.ToString();
		}
		if (_teleportColorGreenValue != null)
		{
			_teleportColorGreenValue.text = num2.ToString();
		}
		if (_teleportColorBlueValue != null)
		{
			_teleportColorBlueValue.text = num3.ToString();
		}
		foreach (ColorSwatchControl teleportColorSwatch in _teleportColorSwatches)
		{
			if (teleportColorSwatch?.Check != null)
			{
				teleportColorSwatch.Check.SetActive(ColorsMatch(teleportColorSwatch.Color, _teleportColorDraft));
			}
		}
	}

	private void TryUpdateTeleportDraftFromHex(string value)
	{
		string text = value?.Trim() ?? string.Empty;
		if (!text.StartsWith("#", StringComparison.Ordinal))
		{
			text = "#" + text;
		}
		if (text.Length == 7 && ColorUtility.TryParseHtmlString(text, out var color))
		{
			color.a = 1f;
			_teleportColorDraft = color;
			UpdateTeleportColorPickerControls();
		}
	}

	private void CreateColorSwatchButton(Transform parent, Color color)
	{
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_teleportColorDraft = color;
			UpdateTeleportColorPickerControls();
		}, 48f, 42f, CheatMenuStyle.SurfaceHover, 10);
		Text componentInChildren = button.GetComponentInChildren<Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.gameObject.SetActive(value: false);
		}
		GameObject gameObject = CreateObject("Swatch", button.transform, typeof(Image));
		Image component = gameObject.GetComponent<Image>();
		component.color = color;
		component.raycastTarget = false;
		SetOffsets(component.rectTransform, 3f, 3f, 3f, 3f);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(1f, 1f, 1f, 0.8f);
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		GameObject gameObject2 = CreateObject("Check", button.transform, typeof(Text));
		Text component2 = gameObject2.GetComponent<Text>();
		component2.font = _font;
		component2.text = "✓";
		component2.fontSize = 20;
		component2.fontStyle = FontStyle.Bold;
		component2.color = Color.white;
		component2.alignment = TextAnchor.MiddleCenter;
		component2.raycastTarget = false;
		Stretch(component2.rectTransform);
		ColorSwatchControl item = new ColorSwatchControl
		{
			Color = color,
			Check = gameObject2
		};
		_teleportColorSwatches.Add(item);
		gameObject2.SetActive(ColorsMatch(color, _teleportColorDraft));
	}

	private Slider CreateColorChannelSlider(Transform parent, float value, Color fillColor, Action<int> changed, out Text valueText)
	{
		GameObject gameObject = CreateObject("ColorChannelSlider", parent, typeof(Image), typeof(Slider));
		Image component = gameObject.GetComponent<Image>();
		component.color = CheatMenuStyle.SurfaceInset;
		component.raycastTarget = true;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.SliderBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		SetLayout(gameObject, -1f, 30f, 1f);
		Slider component2 = gameObject.GetComponent<Slider>();
		component2.minValue = 0f;
		component2.maxValue = 255f;
		component2.wholeNumbers = true;
		component2.value = Mathf.Clamp(value, 0f, 255f);
		component2.direction = Slider.Direction.LeftToRight;
		GameObject gameObject2 = CreateObject("FillArea", gameObject.transform, typeof(Image));
		Image component3 = gameObject2.GetComponent<Image>();
		component3.color = CheatMenuStyle.SliderTrack;
		component3.raycastTarget = false;
		RectTransform component4 = gameObject2.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0.34f);
		component4.anchorMax = new Vector2(1f, 0.66f);
		component4.offsetMin = new Vector2(7f, 0f);
		component4.offsetMax = new Vector2(-7f, 0f);
		GameObject gameObject3 = CreateObject("Fill", gameObject2.transform, typeof(Image));
		Image component5 = gameObject3.GetComponent<Image>();
		component5.color = fillColor;
		component5.raycastTarget = false;
		RectTransform component6 = gameObject3.GetComponent<RectTransform>();
		component6.anchorMin = Vector2.zero;
		component6.anchorMax = new Vector2(0f, 1f);
		component6.offsetMin = Vector2.zero;
		component6.offsetMax = Vector2.zero;
		component2.fillRect = component6;
		GameObject gameObject4 = CreateObject("Handle", gameObject.transform, typeof(Image));
		Image component7 = gameObject4.GetComponent<Image>();
		component7.sprite = GetSliderGripSprite();
		component7.color = Color.white;
		component7.preserveAspect = true;
		component7.raycastTarget = false;
		RectTransform component8 = gameObject4.GetComponent<RectTransform>();
		component8.sizeDelta = new Vector2(14f, 18f);
		component8.anchorMin = new Vector2(0f, 0.5f);
		component8.anchorMax = new Vector2(0f, 0.5f);
		component8.pivot = new Vector2(0.5f, 0.5f);
		component2.handleRect = component8;
		valueText = CreateText(parent, "0", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, FontStyle.Normal, localize: false);
		SetLayout(valueText.gameObject, 30f, 30f);
		component2.onValueChanged.AddListener(delegate(float channelValue)
		{
			changed(Mathf.RoundToInt(channelValue));
		});
		return component2;
	}

	private static string ToMarkerColorHex(Color color)
	{
		return "#" + ColorUtility.ToHtmlStringRGB(new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f));
	}

	private static bool ColorsMatch(Color left, Color right)
	{
		if (Mathf.Abs(left.r - right.r) < 0.002f && Mathf.Abs(left.g - right.g) < 0.002f)
		{
			return Mathf.Abs(left.b - right.b) < 0.002f;
		}
		return false;
	}

	private Image CreateTeleportMarkerImage(Transform parent, TeleportMarkerKind markerKind, float size, bool compact = false, Color? color = null)
	{
		GameObject gameObject = CreateObject("TeleportMarkerIcon", parent, typeof(Image));
		Image component = gameObject.GetComponent<Image>();
		component.sprite = GetTeleportMarkerSprite(markerKind);
		component.color = color ?? new Color(1f, 0.82f, 0.08f, 1f);
		component.preserveAspect = true;
		component.raycastTarget = false;
		RectTransform rectTransform = component.rectTransform;
		rectTransform.anchorMin = (compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f));
		rectTransform.anchorMax = (compact ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 1f));
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.anchoredPosition = (compact ? new Vector2(18f, 0f) : new Vector2(0f, -16f));
		rectTransform.sizeDelta = new Vector2(size, size);
		SetLayout(gameObject, size, size, 0f, 0f);
		return component;
	}

	private Sprite GetTeleportMarkerSprite(TeleportMarkerKind markerKind)
	{
		if (_teleportMarkerSprites.TryGetValue(markerKind, out var value) && value != null)
		{
			return value;
		}
		value = TeleportMarkerIconFactory.CreateSprite(markerKind, 32, out var texture);
		_teleportMarkerSprites[markerKind] = value;
		_teleportMarkerTextures[markerKind] = texture;
		return value;
	}

	private static string MarkerLabel(TeleportMarkerKind markerKind)
	{
		return markerKind switch
		{
			TeleportMarkerKind.Square => "正方形",
			TeleportMarkerKind.Circle => "圆形",
			TeleportMarkerKind.Diamond => "菱形",
			_ => "五角星",
		};
	}

	private static Color ParseMarkerColor(string value)
	{
		if (!ColorUtility.TryParseHtmlString(value, out var color))
		{
			return new Color(0.96f, 0.77f, 0.26f, 1f);
		}
		return color;
	}

	private static void CreateHoverExplanationBorderEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.offsetMin = offsetMin;
		component.offsetMax = offsetMax;
		Image component2 = gameObject.GetComponent<Image>();
		component2.color = CheatMenuStyle.Accent;
		component2.raycastTarget = false;
	}

	private void EnsureHoverExplanation()
	{
		GameObject activeRoot = GetActiveOverlayRoot();
		if (activeRoot == null)
		{
			return;
		}
		if (_hoverExplanationRoot != null && _hoverExplanationRoot.transform.parent != activeRoot.transform)
		{
			QueueForDestroy(_hoverExplanationRoot);
			_hoverExplanationRoot = null;
			_hoverExplanationText = null;
		}
		if (!(_hoverExplanationRoot != null))
		{
			_hoverExplanationRoot = CreateObject("OverlayHoverExplanation", activeRoot.transform, typeof(Image));
			RectTransform component = _hoverExplanationRoot.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			Image component2 = _hoverExplanationRoot.GetComponent<Image>();
			component2.color = Color.clear;
			component2.raycastTarget = false;
			Canvas component3 = activeRoot.GetComponent<Canvas>();
			float num = ((component3 != null) ? Mathf.Max(0.01f, component3.scaleFactor) : 1f);
			float num2 = 3f / num;
			CreateHoverExplanationBorderEdge(_hoverExplanationRoot.transform, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f - num2), Vector2.zero);
			CreateHoverExplanationBorderEdge(_hoverExplanationRoot.transform, "BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, num2));
			CreateHoverExplanationBorderEdge(_hoverExplanationRoot.transform, "BorderLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(num2, 0f));
			CreateHoverExplanationBorderEdge(_hoverExplanationRoot.transform, "BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(0f - num2, 0f), Vector2.zero);
			GameObject gameObject = CreateObject("Background", _hoverExplanationRoot.transform, typeof(Image));
			RectTransform component4 = gameObject.GetComponent<RectTransform>();
			component4.anchorMin = Vector2.zero;
			component4.anchorMax = Vector2.one;
			component4.offsetMin = new Vector2(num2, num2);
			component4.offsetMax = new Vector2(0f - num2, 0f - num2);
			Image component5 = gameObject.GetComponent<Image>();
			component5.color = new Color(0f, 0f, 0f, 0.8f);
			component5.raycastTarget = false;
			_hoverExplanationText = CreateText(_hoverExplanationRoot.transform, string.Empty, 12, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
			_hoverExplanationText.horizontalOverflow = HorizontalWrapMode.Wrap;
			_hoverExplanationText.verticalOverflow = VerticalWrapMode.Overflow;
			_hoverExplanationText.raycastTarget = false;
			_hoverExplanationRoot.SetActive(value: false);
		}
	}

	private void ShowHoverExplanation(string message, RectTransform target, Vector2 screenPosition)
	{
		GameObject activeRoot = GetActiveOverlayRoot();
		if (activeRoot == null || !activeRoot.activeSelf || string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		EnsureHoverExplanation();
		Canvas component = activeRoot.GetComponent<Canvas>();
		float num = ((component != null) ? Mathf.Max(1f, component.scaleFactor) : 1f);
		float num2 = 10f / num;
		float max = 420f / num;
		float min = 64f / num;
		_hoverExplanationText.text = PluginLocalization.Translate(message);
		_hoverExplanationText.horizontalOverflow = HorizontalWrapMode.Overflow;
		_hoverExplanationText.verticalOverflow = VerticalWrapMode.Overflow;
		RectTransform rectTransform = _hoverExplanationText.rectTransform;
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.sizeDelta = new Vector2(4096f, 32f);
		Canvas.ForceUpdateCanvases();
		float num3 = Mathf.Clamp(_hoverExplanationText.preferredWidth, min, max);
		rectTransform.sizeDelta = new Vector2(num3, 32f);
		_hoverExplanationText.horizontalOverflow = HorizontalWrapMode.Wrap;
		Canvas.ForceUpdateCanvases();
		float num4 = Mathf.Max(16f / num, _hoverExplanationText.preferredHeight);
		rectTransform.sizeDelta = new Vector2(num3, num4);
		float num5 = num3 + num2 * 2f;
		float num6 = num4 + num2 * 2f;
		RectTransform component2 = _hoverExplanationRoot.GetComponent<RectTransform>();
		component2.sizeDelta = new Vector2(num5, num6);
		Canvas.ForceUpdateCanvases();
		_hoverExplanationRoot.transform.SetAsLastSibling();
		float num7 = num5 * num;
		float num8 = num6 * num;
		float num9 = num7 * 0.5f;
		float num10 = num8 * 0.5f;
		Rect right = new Rect(screenPosition.x - 1f, screenPosition.y - 1f, 2f, 2f);
		if (target != null)
		{
			Vector3[] array = new Vector3[4];
			target.GetWorldCorners(array);
			Vector2 vector2 = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 vector3 = RectTransformUtility.WorldToScreenPoint(null, array[2]);
			right = Rect.MinMaxRect(vector2.x - 12f, vector2.y - 12f, vector3.x + 12f, vector3.y + 12f);
		}
		Vector2 center = right.center;
		Vector2[] obj = new Vector2[5]
		{
			new Vector2(center.x, right.yMax + 12f + num10),
			new Vector2(center.x, right.yMin - 12f - num10),
			new Vector2(right.xMax + 12f + num9, center.y),
			new Vector2(right.xMin - 12f - num9, center.y),
			screenPosition
		};
		Vector2 screenPoint = screenPosition;
		float num11 = float.MaxValue;
		Vector2[] array2 = obj;
		for (int i = 0; i < array2.Length; i++)
		{
			Vector2 vector4 = array2[i];
			Vector2 vector5 = new Vector2(Mathf.Clamp(vector4.x, num9 + 8f, (float)Screen.width - num9 - 8f), Mathf.Clamp(vector4.y, num10 + 8f, (float)Screen.height - num10 - 8f));
			Rect left = new Rect(vector5.x - num9, vector5.y - num10, num7, num8);
			bool num12 = left.xMin >= 8f && left.xMax <= (float)Screen.width - 8f && left.yMin >= 8f && left.yMax <= (float)Screen.height - 8f;
			float hoverExplanationOverlapArea = GetHoverExplanationOverlapArea(left, right);
			float num13 = (num12 ? 0f : 1000000f) + hoverExplanationOverlapArea;
			if (num13 < num11)
			{
				num11 = num13;
				screenPoint = vector5;
			}
		}
		RectTransformUtility.ScreenPointToLocalPointInRectangle(activeRoot.GetComponent<RectTransform>(), screenPoint, null, out var localPoint);
		component2.anchoredPosition = localPoint;
		_hoverExplanationRoot.SetActive(value: true);
	}

	private static float GetHoverExplanationOverlapArea(Rect left, Rect right)
	{
		float num = Mathf.Max(0f, Mathf.Min(left.xMax, right.xMax) - Mathf.Max(left.xMin, right.xMin));
		float num2 = Mathf.Max(0f, Mathf.Min(left.yMax, right.yMax) - Mathf.Max(left.yMin, right.yMin));
		return num * num2;
	}

	private void HideHoverExplanation()
	{
		if (_hoverExplanationRoot != null)
		{
			_hoverExplanationRoot.SetActive(value: false);
		}
	}

	private Button CreateButton(Transform parent, string label, System.Action action, float width = 0f, float height = 36f, Color? color = null, int fontSize = 13, string tooltip = null, bool delayTooltip = false)
	{
		GameObject gameObject = CreateObject("Button", parent, typeof(Image), typeof(Button));
		Image component = gameObject.GetComponent<Image>();
		component.color = color ?? CheatMenuStyle.Surface;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.ButtonBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		Button component2 = gameObject.GetComponent<Button>();
		component2.targetGraphic = component;
		ColorBlock colors = component2.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
		colors.pressedColor = new Color(0.78f, 0.84f, 0.9f, 1f);
		colors.disabledColor = new Color(0.42f, 0.45f, 0.49f, 0.55f);
		component2.colors = colors;
		if (action != null)
		{
			component2.onClick.AddListener(action.Invoke);
		}
		Text text = null;
		switch (label)
		{
		case "×":
		case "★":
		case "☆":
		case "__keyboard_glyph__":
		{
			GameObject gameObject2 = CreateObject("Glyph", gameObject.transform, typeof(Image));
			Image component3 = gameObject2.GetComponent<Image>();
			component3.sprite = label switch
			{
				"☆" => GetFavoriteOutlineSprite(),
				"★" => GetFavoriteFilledSprite(),
				"×" => GetCloseGlyphSprite(),
				_ => GetKeyboardGlyphSprite(),
			};
			component3.color = Color.white;
			component3.preserveAspect = true;
			component3.raycastTarget = false;
			RectTransform component4 = gameObject2.GetComponent<RectTransform>();
			Vector2 anchorMin = (component4.anchorMax = new Vector2(0.5f, 0.5f));
			component4.anchorMin = anchorMin;
			component4.pivot = new Vector2(0.5f, 0.5f);
			component4.anchoredPosition = Vector2.zero;
			component4.sizeDelta = ((label == "×") ? new Vector2(20f, 20f) : ((label == "__keyboard_glyph__") ? new Vector2(24f, 20f) : new Vector2(21f, 21f)));
			if (label == "__keyboard_glyph__")
			{
				GameObject keyboardKeysObject = CreateObject("KeyboardKeys", gameObject.transform, typeof(Image));
				Image keyboardKeysImage = keyboardKeysObject.GetComponent<Image>();
				keyboardKeysImage.sprite = GetKeyboardGlyphKeysSprite();
				keyboardKeysImage.color = Color.white;
				keyboardKeysImage.preserveAspect = true;
				keyboardKeysImage.raycastTarget = false;
				RectTransform keyboardKeysRect = keyboardKeysObject.GetComponent<RectTransform>();
				keyboardKeysRect.anchorMin = anchorMin;
				keyboardKeysRect.anchorMax = new Vector2(0.5f, 0.5f);
				keyboardKeysRect.pivot = new Vector2(0.5f, 0.5f);
				keyboardKeysRect.anchoredPosition = new Vector2(-0.5f, 0f);
				keyboardKeysRect.sizeDelta = new Vector2(24f, 20f);
			}
			break;
		}
		default:
			text = CreateText(gameObject.transform, label, fontSize, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
			SetOffsets(text.rectTransform, 4f, 4f, 2f, 2f);
			break;
		}
		GameObject gameObject3 = CreateObject("FocusIndicator", gameObject.transform, typeof(Image));
		Image component5 = gameObject3.GetComponent<Image>();
		component5.color = CheatMenuStyle.AccentBright;
		component5.raycastTarget = false;
		RectTransform component6 = gameObject3.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0f, 0f);
		component6.anchorMax = new Vector2(1f, 0f);
		component6.pivot = new Vector2(0.5f, 0f);
		component6.anchoredPosition = Vector2.zero;
		component6.sizeDelta = new Vector2(0f, 3f);
		gameObject3.SetActive(value: false);
		component2.transition = Selectable.Transition.None;
		OverlaySelectableVisual overlaySelectableVisual = gameObject.AddComponent<OverlaySelectableVisual>();
		overlaySelectableVisual.Initialize(component2, component, text, gameObject3, outline, component.color);
		overlaySelectableVisual.SetHoverExplanation(tooltip, ShowHoverExplanation, HideHoverExplanation, delayTooltip);
		SetLayout(gameObject, (width > 0f) ? width : (-1f), height);
		return component2;
	}

	private Text CreateText(Transform parent, string value, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal, bool localize = true)
	{
		Text component = CreateObject("Text", parent, typeof(Text)).GetComponent<Text>();
		component.font = _font;
		component.text = (localize ? PluginLocalization.Translate(value) : value);
		component.fontSize = size;
		component.fontStyle = style;
		component.color = color;
		component.alignment = alignment;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Truncate;
		component.raycastTarget = false;
		return component;
	}

	private RectTransform CreateScroll(Transform parent)
	{
		GameObject gameObject = CreateObject("Scroll", parent, typeof(Image), typeof(ScrollRect));
		Stretch(gameObject.GetComponent<RectTransform>());
		gameObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
		SetLayout(gameObject, -1f, -1f, 1f, 1f);
		LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();
		layoutElement.minWidth = 1f;
		layoutElement.minHeight = 1f;
		GameObject gameObject2 = CreateObject("Viewport", gameObject.transform, typeof(Image), typeof(Mask));
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		Stretch(component);
		gameObject2.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
		gameObject2.GetComponent<Mask>().showMaskGraphic = false;
		RectTransform component2 = CreateObject("Content", component, typeof(RectTransform)).GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(1f, 1f);
		component2.pivot = new Vector2(0.5f, 1f);
		component2.offsetMin = Vector2.zero;
		component2.offsetMax = Vector2.zero;
		ScrollRect component3 = gameObject.GetComponent<ScrollRect>();
		component3.viewport = component;
		component3.content = component2;
		component3.horizontal = false;
		component3.vertical = true;
		component3.movementType = ScrollRect.MovementType.Clamped;
		component3.scrollSensitivity = 28f;
		return component2;
	}

	private GameObject CreateRow(Transform parent, float height, float spacing)
	{
		GameObject gameObject = CreateObject("Row", parent);
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.spacing = spacing;
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
		if (height > 0f)
		{
			SetLayout(gameObject, -1f, height);
		}
		return gameObject;
	}

	private VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding)
	{
		VerticalLayoutGroup verticalLayoutGroup = target.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = padding;
		verticalLayoutGroup.spacing = spacing;
		verticalLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		if (target.GetComponent<ContentSizeFitter>() == null && target.name == "Content")
		{
			target.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		}
		return verticalLayoutGroup;
	}

	private static GameObject CreateObject(string name, Transform parent, params Type[] components)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		foreach (Type type in components)
		{
			if (type != typeof(RectTransform) && gameObject.GetComponent(type) == null)
			{
				gameObject.AddComponent(type);
			}
		}
		return gameObject;
	}

	private static void SetLayout(GameObject target, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
	{
		LayoutElement layoutElement = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
		if (preferredWidth >= 0f)
		{
			layoutElement.preferredWidth = preferredWidth;
		}
		if (preferredHeight >= 0f)
		{
			layoutElement.preferredHeight = preferredHeight;
		}
		if (flexibleWidth >= 0f)
		{
			layoutElement.flexibleWidth = flexibleWidth;
		}
		if (flexibleHeight >= 0f)
		{
			layoutElement.flexibleHeight = flexibleHeight;
		}
	}

	private static float CalculateSafeScale(float requestedScale)
	{
		float a = ((float)Screen.width - 32f) / 1120f;
		float b = ((float)Screen.height - 32f) / 790f;
		float b2 = Mathf.Min(a, b);
		return Mathf.Clamp(Mathf.Min(requestedScale, b2), 0.75f, 1.5f);
	}

	private static void CreateFlexibleSpacer(Transform parent)
	{
		SetLayout(CreateObject("Spacer", parent), -1f, -1f, 1f);
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static void SetOffsets(RectTransform rect, float left, float right, float bottom, float top)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(0f - right, 0f - top);
	}

	private void QueueForDestroy(GameObject target)
	{
		if (!(target == null))
		{
			target.SetActive(value: false);
			if (!_deferredDestroy.Contains(target))
			{
				_deferredDestroy.Add(target);
			}
		}
	}

	private void FlushDeferredDestroy()
	{
		if (_deferredDestroy.Count == 0)
		{
			return;
		}
		foreach (GameObject item in _deferredDestroy)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item);
			}
		}
		_deferredDestroy.Clear();
	}

	private void BuildItemResults()
	{
		_itemResults.Clear();
		_itemResults.AddRange(_plugin.Catalog.Items.Where((ItemAsset asset) => MatchesItemForFilter(asset, _itemFilter)));
		_itemPage = 0;
	}

	private void BuildVehicleResults()
	{
		_vehicleResults.Clear();
		_vehicleResults.AddRange(_plugin.Catalog.Vehicles.Where((VehicleAsset asset) => (_vehicleCategory == "全部" || AssetCatalog.GetVehicleCategory(asset) == _vehicleCategory) && AssetCatalog.Matches(asset, asset.FriendlyName, GetOriginalVehicleName(asset), _vehicleQuery)));
		_vehiclePage = 0;
	}

	private void BuildFavoriteResults()
	{
		_favoriteItemResults.Clear();
		_favoriteItemResults.AddRange(_plugin.Catalog.Items.Where((ItemAsset asset) => _plugin.Favorites.IsItemFavorite(asset) && MatchesItemForFilter(asset, _favoriteItemFilter)));
		_favoriteVehicleResults.Clear();
		_favoriteVehicleResults.AddRange(_plugin.Catalog.Vehicles.Where((VehicleAsset asset) => _plugin.Favorites.IsVehicleFavorite(asset) && (_favoriteVehicleCategory == "全部" || AssetCatalog.GetVehicleCategory(asset) == _favoriteVehicleCategory) && AssetCatalog.Matches(asset, asset.FriendlyName, GetOriginalVehicleName(asset), _favoriteQuery)));
		_favoritePage = 0;
	}

	private bool MatchesItemForFilter(ItemAsset asset, ItemFilterState filter)
	{
		string source = null;
		_plugin.WorkshopLocalization?.TryGetOriginal(asset, WorkshopTextField.ItemName, out source);
		return ItemFilterService.Matches(asset, filter, source);
	}

	private string GetOriginalVehicleName(VehicleAsset asset)
	{
		string source = null;
		_plugin.WorkshopLocalization?.TryGetOriginal(asset, WorkshopTextField.VehicleName, out source);
		return source;
	}

	private void ResetStatus()
	{
		if (_statusText != null)
		{
			_statusText.text = PluginLocalization.Translate("准备就绪。");
			_statusText.color = CheatMenuStyle.Muted;
		}
		if (_statusAccent != null)
		{
			_statusAccent.color = CheatMenuStyle.Accent;
		}
	}

	internal void ShowGlobalShortcutStatus(string message, bool warning = false)
	{
		SetStatus(message, 4f, warning ? StatusKind.Warning : null);
	}

	private void SetStatus(string message, float seconds = 3f, StatusKind? kind = null)
	{
		StatusKind statusKind = kind ?? InferStatusKind(message);
		if (_statusText != null)
		{
			string text = PluginLocalization.Translate(GetStatusPrefix(statusKind));
			string text2 = ((PluginLocalization.CurrentLanguage == PluginLanguage.English) ? ": " : "：");
			_statusText.text = text + text2 + PluginLocalization.Translate(message);
			_statusText.color = ((statusKind == StatusKind.Warning) ? CheatMenuStyle.Warning : CheatMenuStyle.Text);
		}
		if (_statusAccent != null)
		{
			_statusAccent.color = GetStatusColor(statusKind);
		}
		_statusUntil = Time.unscaledTime + seconds;
	}

	private static StatusKind InferStatusKind(string message)
	{
		if (message.IndexOf("失败", StringComparison.Ordinal) >= 0 || message.StartsWith("无法", StringComparison.Ordinal))
		{
			return StatusKind.Error;
		}
		if (message.IndexOf("没有可用", StringComparison.Ordinal) >= 0 || message.IndexOf("没有默认", StringComparison.Ordinal) >= 0)
		{
			return StatusKind.Warning;
		}
		return StatusKind.Success;
	}

	private static string GetStatusPrefix(StatusKind kind)
	{
		return kind switch
		{
			StatusKind.Success => "成功",
			StatusKind.Warning => "提示",
			StatusKind.Error => "失败",
			_ => "信息",
		};
	}

	private static Color GetStatusColor(StatusKind kind)
	{
		return kind switch
		{
			StatusKind.Success => CheatMenuStyle.Success,
			StatusKind.Warning => CheatMenuStyle.Warning,
			StatusKind.Error => CheatMenuStyle.Danger,
			_ => CheatMenuStyle.Accent,
		};
	}

	private static int ParseClamped(string text, int min, int max, int fallback)
	{
		if (!int.TryParse(text, out var result))
		{
			return Mathf.Clamp(fallback, min, max);
		}
		return Mathf.Clamp(result, min, max);
	}

	private Sprite GetSliderGripSprite()
	{
		if (_sliderGripSprite != null)
		{
			return _sliderGripSprite;
		}
		_sliderGripTexture = CreateSliderGripTexture(32);
		_sliderGripSprite = Sprite.Create(_sliderGripTexture, new Rect(0f, 0f, _sliderGripTexture.width, _sliderGripTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _sliderGripSprite;
	}

	private Sprite GetCloseGlyphSprite()
	{
		if (_closeGlyphSprite != null)
		{
			return _closeGlyphSprite;
		}
		_closeGlyphTexture = CreateCloseGlyphTexture(32);
		_closeGlyphSprite = Sprite.Create(_closeGlyphTexture, new Rect(0f, 0f, _closeGlyphTexture.width, _closeGlyphTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _closeGlyphSprite;
	}

	private Sprite GetKeyboardGlyphSprite()
	{
		if (_keyboardGlyphSprite != null)
		{
			return _keyboardGlyphSprite;
		}
		_keyboardGlyphTexture = CreateKeyboardGlyphTexture(32);
		_keyboardGlyphSprite = Sprite.Create(_keyboardGlyphTexture, new Rect(0f, 0f, _keyboardGlyphTexture.width, _keyboardGlyphTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _keyboardGlyphSprite;
	}

	private Sprite GetKeyboardGlyphKeysSprite()
	{
		if (_keyboardGlyphKeysSprite != null)
		{
			return _keyboardGlyphKeysSprite;
		}
		_keyboardGlyphKeysTexture = CreateKeyboardGlyphKeysTexture(32);
		_keyboardGlyphKeysSprite = Sprite.Create(_keyboardGlyphKeysTexture, new Rect(0f, 0f, _keyboardGlyphKeysTexture.width, _keyboardGlyphKeysTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _keyboardGlyphKeysSprite;
	}

	private Sprite GetFavoriteFilledSprite()
	{
		if (_favoriteFilledSprite != null)
		{
			return _favoriteFilledSprite;
		}
		_favoriteFilledTexture = CreateStarTexture(32, outlineOnly: false);
		_favoriteFilledSprite = Sprite.Create(_favoriteFilledTexture, new Rect(0f, 0f, _favoriteFilledTexture.width, _favoriteFilledTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _favoriteFilledSprite;
	}

	private Sprite GetFavoriteOutlineSprite()
	{
		if (_favoriteOutlineSprite != null)
		{
			return _favoriteOutlineSprite;
		}
		_favoriteOutlineTexture = CreateStarTexture(32, outlineOnly: true);
		_favoriteOutlineSprite = Sprite.Create(_favoriteOutlineTexture, new Rect(0f, 0f, _favoriteOutlineTexture.width, _favoriteOutlineTexture.height), new Vector2(0.5f, 0.5f), 32f);
		return _favoriteOutlineSprite;
	}

	private static Texture2D CreateSliderGripTexture(int size)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		Color color = new Color(0.03f, 0.18f, 0.28f, 1f);
		Color accentBright = CheatMenuStyle.AccentBright;
		Color sliderBorder = CheatMenuStyle.SliderBorder;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				bool flag = j < 2 || i < 2 || j >= size - 2 || i >= size - 2;
				bool flag2 = (j + i) % 9 < 2;
				texture2D.SetPixel(j, i, flag ? sliderBorder : (flag2 ? color : accentBright));
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private static Texture2D CreateCloseGlyphTexture(int size)
	{
		Texture2D texture2D = CreateTransparentTexture(size);
		float num = (float)(size - 1) * 0.5f;
		Color white = Color.white;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num2 = (float)j - num;
				float num3 = (float)i - num;
				bool flag = Mathf.Abs(num2 - num3) <= 1.8f;
				bool flag2 = Mathf.Abs(num2 + num3) <= 1.8f;
				if (flag | flag2)
				{
					texture2D.SetPixel(j, i, white);
				}
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private static Texture2D CreateKeyboardGlyphTexture(int size)
	{
		Texture2D texture2D = CreateTransparentTexture(size);
		Color white = Color.white;
		for (int i = 5; i <= 26; i++)
		{
			for (int j = 3; j <= 28; j++)
			{
				if (j <= 4 || j >= 27 || i <= 6 || i >= 25)
				{
					texture2D.SetPixel(j, i, white);
				}
			}
		}
		for (int num3 = 22; num3 <= 23; num3++)
		{
			for (int num4 = 11; num4 <= 21; num4++)
			{
				texture2D.SetPixel(num4, num3, white);
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private static Texture2D CreateKeyboardGlyphKeysTexture(int size)
	{
		Texture2D texture2D = CreateTransparentTexture(size);
		Color white = Color.white;
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < 6; j++)
			{
				int startX = 8 + j * 3;
				int startY = 10 + i * 6;
				for (int y = startY; y < startY + 3; y++)
				{
					for (int x = startX; x < startX + 2; x++)
					{
						texture2D.SetPixel(x, y, white);
					}
				}
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private static Texture2D CreateStarTexture(int size, bool outlineOnly)
	{
		Texture2D texture2D = CreateTransparentTexture(size);
		Vector2[] array = new Vector2[10];
		Vector2 vector = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		float num = (float)size * 0.43f;
		float num2 = (float)size * 0.19f;
		for (int i = 0; i < array.Length; i++)
		{
			float num3 = ((i % 2 == 0) ? num : num2);
			float f = MathF.PI / 2f + (float)i * MathF.PI / 5f;
			array[i] = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num3;
		}
		Color white = Color.white;
		for (int j = 0; j < size; j++)
		{
			for (int k = 0; k < size; k++)
			{
				Vector2 point = new Vector2(k, j);
				if (outlineOnly ? IsNearPolygonEdge(point, array, 1.45f) : IsInsidePolygon(point, array))
				{
					texture2D.SetPixel(k, j, white);
				}
			}
		}
		texture2D.Apply();
		return texture2D;
	}

	private static Texture2D CreateTransparentTexture(int size)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		Color color = new Color(0f, 0f, 0f, 0f);
		Color[] array = new Color[size * size];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = color;
		}
		texture2D.SetPixels(array);
		return texture2D;
	}

	private static bool IsInsidePolygon(Vector2 point, Vector2[] vertices)
	{
		bool flag = false;
		int num = 0;
		int num2 = vertices.Length - 1;
		while (num < vertices.Length)
		{
			Vector2 vector = vertices[num];
			Vector2 vector2 = vertices[num2];
			if (vector.y > point.y != vector2.y > point.y && point.x < (vector2.x - vector.x) * (point.y - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}

	private static bool IsNearPolygonEdge(Vector2 point, Vector2[] vertices, float thickness)
	{
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector2 vector = vertices[i];
			Vector2 vector2 = vertices[(i + 1) % vertices.Length] - vector;
			float sqrMagnitude = vector2.sqrMagnitude;
			float num = ((sqrMagnitude <= Mathf.Epsilon) ? 0f : Mathf.Clamp01(Vector2.Dot(point - vector, vector2) / sqrMagnitude));
			Vector2 b = vector + vector2 * num;
			if (Vector2.Distance(point, b) <= thickness)
			{
				return true;
			}
		}
		return false;
	}

	private static void DestroyGeneratedSprite(ref Sprite sprite, ref Texture2D texture)
	{
		if (sprite != null)
		{
			UnityEngine.Object.Destroy(sprite);
		}
		if (texture != null)
		{
			UnityEngine.Object.Destroy(texture);
		}
		sprite = null;
		texture = null;
	}

	private void BuildToolsTab()
	{
		AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		AddToolsTab(gameObject.transform, ToolsTab.Interaction, "交互");
		AddToolsTab(gameObject.transform, ToolsTab.Inventory, "背包整理");
		AddToolsTab(gameObject.transform, ToolsTab.AutoPickup, "自动拾取");
		AddToolsTab(gameObject.transform, ToolsTab.Movement, "移动");
		AddToolsTab(gameObject.transform, ToolsTab.Translation, "翻译");
		GameObject gameObject2 = CreateObject("ToolsPanel", _contentHost.transform, typeof(Image));
		gameObject2.GetComponent<Image>().color = CheatMenuStyle.Surface;
		AddVerticalLayout(gameObject2, 10f, new RectOffset(18, 18, 16, 16));
		SetLayout(gameObject2, -1f, -1f, 1f, 1f);
		CreateLabel(gameObject2.transform, GetToolsTabTitle(_activeToolsTab), 19, CheatMenuStyle.Text);
		if (_activeToolsTab == ToolsTab.Interaction)
		{
			BuildPointToolPanel(gameObject2.transform);
		}
		else if (_activeToolsTab == ToolsTab.Inventory)
		{
			BuildInventoryArrangePanel(gameObject2.transform);
		}
		else if (_activeToolsTab == ToolsTab.AutoPickup)
		{
			BuildAutoPickupPanel(gameObject2.transform);
		}
		else if (_activeToolsTab == ToolsTab.Movement)
		{
			BuildMovementPanel(gameObject2.transform);
		}
		else
		{
			BuildWorkshopLocalizationPanel(gameObject2.transform);
		}
	}

	private static string GetToolsTabTitle(ToolsTab tab)
	{
		return tab switch
		{
			ToolsTab.Interaction => "准星交互工具",
			ToolsTab.Inventory => "背包整理",
			ToolsTab.AutoPickup => "自动拾取",
			ToolsTab.Movement => "移动工具",
			_ => "翻译"
		};
	}

	private void AddToolsTab(Transform parent, ToolsTab tab, string label)
	{
		SetLayout(CreateButton(parent, label, delegate
		{
			_activeToolsTab = tab;
			ReplaceContentHost();
			BuildToolsTab();
		}, 0f, 40f, (_activeToolsTab == tab) ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 14).gameObject, -1f, -1f, 1f);
	}

	private void BuildInfiniteStoragePanel(Transform panel)
	{
		if (_plugin.InfiniteStorage == null)
		{
			CreateEmptyState(panel, "无限储存不可用", "储存运行时尚未初始化。");
			return;
		}
		GameObject row = CreateRow(panel, 40f, 7f);
		CreateToggleAction(row.transform, "启用无限储存", _plugin.InfiniteStorage.Enabled, delegate(bool value)
		{
			_plugin.SetInfiniteStorageEnabled(value);
			RebuildInfiniteStoragePage();
		});
		GameObject priorityRow = CreateRow(panel, 34f, 7f);
		CreateToggleAction(priorityRow.transform, "拾取优先存入无限储存", _plugin.IsInfiniteStoragePickupPriorityEnabled, delegate(bool value)
		{
			_plugin.SetInfiniteStoragePickupPriority(value);
			RebuildInfiniteStoragePage();
		});
		CreateLabel(row.transform, "搜索", 13, CheatMenuStyle.Text, 40f);
		CreateInput(row.transform, _infiniteStorageFilter.SearchQuery, delegate(string value)
		{
			_infiniteStorageFilter.SearchQuery = value;
			_infiniteStoragePage = 0;
			_searchDebouncer.Schedule(delegate { if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf) { RebuildInfiniteStoragePage(); } });
		}, 220f);
		CreateLabel(row.transform, "取出数量", 13, CheatMenuStyle.Text, 56f);
		CreateButton(row.transform, "−", delegate
		{
			_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, 36f, 34f);
		_itemAmountInput = CreateInput(row.transform, _itemAmountText, delegate(string value)
		{
			_itemAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
			}
		}, 62f);
		CreateButton(row.transform, "+", delegate
		{
			_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, 36f, 34f);
		CreateButton(row.transform, (_infiniteStorageFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_infiniteStorageFilter.ActiveFilterCount}", ShowInfiniteStorageFilterPanel, 92f, 34f, CheatMenuStyle.Surface, 12);
		long resourceUnits = _plugin.InfiniteStorage.Entries.Where(entry => entry.UnitKind == StackUnitKind.ResourceUnits).Sum(entry => entry.StoredUnitCount);
		long instances = _plugin.InfiniteStorage.Entries.Where(entry => entry.UnitKind != StackUnitKind.ResourceUnits).Sum(entry => entry.StoredUnitCount);
		IReadOnlyList<VirtualStorageEntry> filteredEntries = GetFilteredInfiniteStorageEntries();
		if (_plugin.InfiniteStorage.LegacyAmbiguousEntries.Count > 0)
		{
			GameObject legacyRow = CreateRow(panel, 34f, 7f);
			CreateLabel(legacyRow.transform, $"检测到 {_plugin.InfiniteStorage.LegacyAmbiguousEntries.Count:N0} 条待处理旧存档记录。", 13, CheatMenuStyle.Warning, 0f);
			CreateButton(legacyRow.transform, "导出原始备份", delegate
			{
				bool exported = _plugin.InfiniteStorage.TryExportLegacyAmbiguousEntries(out _, out _);
				SetStatus(exported ? "已导出待处理旧存档原始备份。" : "导出待处理旧存档失败。", 4f, exported ? StatusKind.Success : StatusKind.Warning);
			}, 118f, 30f, CheatMenuStyle.Surface, 12, "导出所有待处理旧存档的原始 JSON 备份。");
		}
		CreateButton(panel, "存储当前携带物品", delegate
		{
			int moved = _plugin.InfiniteStorage.StoreAllCarried();
			SetStatus(moved > 0 ? $"已存储 {moved} 个背包格。" : "没有可存储的物品，或无限储存未启用。", 4f, StatusKind.Info);
			RebuildInfiniteStoragePage();
		}, 180f, 40f, CheatMenuStyle.Success, 13, "将当前背包中的物品存入无限储存。");
		CreateLabel(panel, $"显示 {filteredEntries.Count:N0} / {_plugin.InfiniteStorage.Entries.Count:N0} 条目；资源 {resourceUnits:N0}，完整物品 {instances:N0} 个。", 14, CheatMenuStyle.Text);
		GameObject contentRow = CreateRow(panel, 0f, 10f);
		SetLayout(contentRow, -1f, -1f, -1f, 1f);
		HashSet<ushort> storedAssetIds = new HashSet<ushort>(_plugin.InfiniteStorage.Entries.Select(entry => entry.AssetId));
		BuildItemCategorySidebar(contentRow.transform, "物品分类", _infiniteStorageFilter, delegate(ItemPrimaryCategory category)
		{
			_infiniteStorageFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_infiniteStorageFilter);
			_infiniteStoragePage = 0;
			RebuildInfiniteStoragePage();
		}, asset => storedAssetIds.Contains(asset.id));
		BuildInfiniteStorageGrid(contentRow.transform, filteredEntries);
	}

	private void BuildInfiniteStorageGrid(Transform parent, IReadOnlyList<VirtualStorageEntry> entries)
	{
		GameObject column = CreateObject("InfiniteStorageGridColumn", parent);
		AddVerticalLayout(column, 7f, new RectOffset(0, 0, 0, 0));
		SetLayout(column, -1f, -1f, 1f, 1f);
		int pageCount = Math.Max(1, Mathf.CeilToInt(entries.Count / 15f));
		_infiniteStoragePage = Mathf.Clamp(_infiniteStoragePage, 0, pageCount - 1);
		Action<int> changePage = delegate(int value)
		{
			_infiniteStoragePage = value;
			RebuildInfiniteStoragePage();
		};
		BuildPagination(column.transform, _infiniteStoragePage, pageCount, changePage);
		AddPageScrollHandler(column, _infiniteStoragePage, pageCount, changePage);
		if (entries.Count == 0)
		{
			CreateEmptyState(column.transform, string.IsNullOrWhiteSpace(_infiniteStorageFilter.SearchQuery) ? "当前分类没有已存储物品。" : "没有匹配的物品。", string.IsNullOrWhiteSpace(_infiniteStorageFilter.SearchQuery) ? "存储物品后会显示在这里。" : "请清空搜索或尝试其他关键词。");
			return;
		}
		GameObject grid = CreateObject("InfiniteStorageGrid", column.transform);
		SetLayout(grid, -1f, 500f, -1f, 1f);
		GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
		layout.padding = new RectOffset(4, 4, 4, 4);
		layout.spacing = new Vector2(8f, 8f);
		layout.cellSize = new Vector2(155f, 158f);
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 5;
		int start = _infiniteStoragePage * 15;
		int end = Math.Min(start + 15, entries.Count);
		for (int i = start; i < end; i++)
		{
			VirtualStorageEntry entry = entries[i];
			_infiniteStorageAssetsById.TryGetValue(entry.AssetId, out ItemAsset asset);
			CreateInfiniteStorageCard(grid.transform, entry, asset);
		}
	}

	private void CreateInfiniteStorageCard(Transform parent, VirtualStorageEntry entry, ItemAsset asset)
	{
		string displayName = asset == null ? "未解析物品 ID " + entry.AssetId : GetItemDisplayName(asset);
		InfiniteStorageCardMetadata metadata = ItemInstanceSemantics.BuildCardMetadata(asset, entry);
		string tooltip = asset == null ? "当前资产未解析；条目保持保守状态隔离。" : AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _plugin.Catalog.Items);
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
			WithdrawInfiniteStorageEntry(entry, _giveAmount);
		}, 155f, 158f, CheatMenuStyle.Surface, tooltip: tooltip + "\n\n点击取出当前数量（" + metadata.WithdrawUnitLabel + "）。", delayTooltip: true);
		AddCardContents(button.transform, displayName, metadata.Text, () => asset == null ? null : _plugin.Icons.GetItemIcon(asset), isVehicle: false);
		Button withdrawAll = CreateButton(button.transform, "取全部", delegate
		{
			WithdrawInfiniteStorageEntry(entry, entry.StoredUnitCount);
		}, 62f, 26f, CheatMenuStyle.Accent, 10, "取出此条目的全部单位。", delayTooltip: true);
		RectTransform rect = withdrawAll.GetComponent<RectTransform>();
		Vector2 anchor = (rect.anchorMax = new Vector2(1f, 1f));
		rect.anchorMin = anchor;
		rect.pivot = new Vector2(1f, 1f);
		rect.anchoredPosition = new Vector2(-5f, -5f);
		rect.sizeDelta = new Vector2(62f, 26f);
		withdrawAll.transform.SetAsLastSibling();
	}

	private void WithdrawInfiniteStorageEntry(VirtualStorageEntry entry, long amount)
	{
		long moved = _plugin.InfiniteStorage.Withdraw(entry.EntryId, amount);
		string unit = entry.UnitKind == StackUnitKind.ResourceUnits ? "单位" : "个完整物品";
		SetStatus(moved > 0 ? $"已取出 {moved:N0} {unit}。" : "原生背包没有可用空间。", 3f, StatusKind.Info);
		RebuildInfiniteStoragePage();
	}

	private IReadOnlyList<VirtualStorageEntry> GetFilteredInfiniteStorageEntries()
	{
		List<VirtualStorageEntry> results = new List<VirtualStorageEntry>();
		foreach (VirtualStorageEntry entry in _plugin.InfiniteStorage.Entries)
		{
			if (_infiniteStorageAssetsById.TryGetValue(entry.AssetId, out ItemAsset asset) && MatchesItemForFilter(asset, _infiniteStorageFilter))
			{
				results.Add(entry);
			}
		}
		return results;
	}

	private void RebuildInfiniteStorageAssetIndex()
	{
		_infiniteStorageAssetsById.Clear();
		foreach (ItemAsset asset in _plugin.Catalog.Items)
		{
			if (asset != null && !_infiniteStorageAssetsById.ContainsKey(asset.id))
			{
				_infiniteStorageAssetsById.Add(asset.id, asset);
			}
		}
	}

	private void BuildWorkshopLocalizationPanel(Transform panel)
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		if (workshopLocalization == null)
		{
			CreateEmptyState(panel, "翻译服务不可用", "插件尚未完成 Workshop 翻译服务初始化。");
			return;
		}
		_ = workshopLocalization.Progress;
		CreateLabel(panel, "原版与 Workshop 资产翻译", 17, CheatMenuStyle.Text);
		CreateLabel(panel, "只读取当前已加载的原版与 Workshop 资产；不会修改原始文件、.dat 或 AssetBundle。已有其他汉化时会优先保留原生汉化。", 12, CheatMenuStyle.Muted);
		_workshopLocalizationStateText = CreateLabel(panel, string.Empty, 13, CheatMenuStyle.Text);
		_workshopLocalizationSummaryText = CreateLabel(panel, string.Empty, 13, CheatMenuStyle.Muted);
		_workshopLocalizationApiText = CreateLabel(panel, string.Empty, 12, CheatMenuStyle.Muted);
		RefreshWorkshopLocalizationPageValues();
		GameObject gameObject = CreateRow(panel, 40f, 7f);
		CreateLabel(gameObject.transform, "API 设置", 13, CheatMenuStyle.Text, 75f);
		Button apiButton = null;
		apiButton = CreateButton(gameObject.transform, MachineTranslationSettings.GetDisplayName(workshopLocalization.Settings.ApiProtocol) + " ▼", delegate
		{
			ToggleWorkshopProtocolDrawer(apiButton);
		}, 0f, 34f, CheatMenuStyle.Surface, 12, "选择协议并在下方输入 Endpoint、API Key 和 Model。");
		SetLayout(apiButton.gameObject, -1f, -1f, 1f);
		CreateButton(gameObject.transform, "开始翻译（未缓存）", StartWorkshopTranslationFromUi, 170f, 34f, CheatMenuStyle.Accent, 12, "扫描当前已加载资产后，翻译缓存中没有的文本；填写配置后点击这里开始。");
		CreateButton(gameObject.transform, "停止翻译", StopWorkshopTranslationFromUi, 105f, 34f, CheatMenuStyle.Danger, 12, "停止当前翻译任务；已经写入缓存的译文会保留。");
		bool isCloudProvider = workshopLocalization.Settings.IsCloudProvider;
		bool isLargeModelProvider = workshopLocalization.Settings.IsLargeModelProvider;
		bool num = string.Equals(workshopLocalization.Settings.ApiProtocol, "Ollama", StringComparison.OrdinalIgnoreCase);
		bool flag = true;
		bool flag2 = !num | isCloudProvider;
		bool requiresSecretKey = workshopLocalization.Settings.RequiresSecretKey;
		bool requiresRegion = workshopLocalization.Settings.RequiresRegion;
		bool flag3 = string.Equals(workshopLocalization.Settings.ApiProtocol, "TencentTmt", StringComparison.OrdinalIgnoreCase) || string.Equals(workshopLocalization.Settings.ApiProtocol, "GoogleCloud", StringComparison.OrdinalIgnoreCase);
		if (flag)
		{
			CreateLabeledInput(panel, "接口地址", workshopLocalization.Settings.Endpoint, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationEndpoint(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "请求发送到哪里。大模型通常是 Chat/Message 地址；云翻译使用供应商的 REST 地址。可保留默认值，也可以改成代理地址。");
		}
		if (flag2)
		{
			CreateLabeledInput(panel, isCloudProvider ? "API Key / 应用 ID" : "API Key", workshopLocalization.Settings.ApiKey, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationApiKey(value);
			}, 0f, password: true, commitOnEndEdit: true, 0f, isCloudProvider ? "云翻译供应商发给你的访问密钥或应用 ID。" : "大模型平台发给你的 API Key；不会写入日志。\nOllama 本地服务通常不需要填写。");
		}
		if (requiresSecretKey)
		{
			CreateLabeledInput(panel, "Secret Key / 密钥", workshopLocalization.Settings.SecretKey, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationSecretKey(value);
			}, 0f, password: true, commitOnEndEdit: true, 0f, "部分云翻译供应商需要的第二段密钥：腾讯云 SecretKey、百度 Secret Key 或有道密钥。");
		}
		if (requiresRegion)
		{
			CreateLabeledInput(panel, "区域", workshopLocalization.Settings.Region, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationRegion(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "云服务资源所在区域。例如腾讯云 ap-guangzhou、微软 Translator 的资源区域。");
		}
		if (flag3)
		{
			CreateLabeledInput(panel, "项目 ID", workshopLocalization.Settings.ProjectId, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationProjectId(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "云项目或资源的数字/字符串 ID；没有要求的供应商可以留空。");
		}
		if (isLargeModelProvider)
		{
			CreateLabeledInput(panel, "模型名称", workshopLocalization.Settings.Model, delegate(string value)
			{
				_plugin.SetWorkshopLocalizationModel(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "大模型平台上的模型名称，例如 deepseek-chat、gpt-4o-mini、claude-3-5-sonnet。云机器翻译通常不需要填写。");
		}
		CreateLabeledInput(panel, "每批翻译数量", workshopLocalization.Settings.BatchSize.ToString(), delegate(string value)
		{
			_plugin.SetWorkshopLocalizationBatchSize(value);
		}, 0f, password: false, commitOnEndEdit: true, 0f, "每次发送给翻译接口的文本数量，范围 1-50。数量越小越稳，但请求次数更多；修改后下次翻译任务生效。");
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "启用缓存译文", workshopLocalization.Enabled, delegate(bool value)
		{
			_plugin.SetWorkshopLocalizationEnabled(value);
		}, "关闭后立即回退到原始 Getter 文本。");
		CreateToggleAction(gameObject2.transform, "应用到原生显示", workshopLocalization.ApplyToNativeUi, delegate(bool value)
		{
			_plugin.SetWorkshopLocalizationApplyToNativeUi(value);
		}, "覆盖 ItemAsset、VehicleAsset 和 Workshop Action 的显示 Getter。");
		GameObject gameObject3 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject3.transform, "资产刷新后自动扫描", workshopLocalization.AutoScanOnAssetsRefreshed, delegate(bool value)
		{
			_plugin.SetWorkshopLocalizationAutoScan(value);
		});
		CreateToggleAction(gameObject3.transform, "自动翻译未缓存", workshopLocalization.AutoTranslateMissing, delegate(bool value)
		{
			_plugin.SetWorkshopLocalizationAutoTranslate(value);
		}, "需要已配置 API；默认关闭以避免意外费用。");
		GameObject gameObject4 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject4.transform, "扫描并应用缓存", delegate
		{
			_plugin.ScanWorkshopLocalization();
			RefreshWorkshopLocalizationPage();
		}, 0f, 38f, CheatMenuStyle.Accent);
		CreateButton(gameObject4.transform, "重新加载缓存", delegate
		{
			_plugin.ReloadWorkshopLocalizationCache();
			RefreshWorkshopLocalizationPage();
		}, 0f, 38f, CheatMenuStyle.Surface);
		GameObject gameObject5 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject5.transform, "翻译未缓存文本", delegate
		{
			bool flag4 = _plugin.TranslateWorkshopLocalization(force: false);
			RefreshWorkshopLocalizationPage();
			SetStatus(flag4 ? "Workshop 未缓存文本翻译已启动。" : "未能启动 Workshop 翻译，请检查 API 配置或当前任务状态。", 4f, (!flag4) ? StatusKind.Warning : StatusKind.Info);
		}, 0f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject5.transform, "强制重译当前文本", delegate
		{
			bool flag4 = _plugin.TranslateWorkshopLocalization(force: true);
			RefreshWorkshopLocalizationPage();
			SetStatus(flag4 ? "Workshop 强制重译已启动。" : "未能启动 Workshop 强制重译，请检查 API 配置或当前任务状态。", 4f, (!flag4) ? StatusKind.Warning : StatusKind.Info);
		}, 0f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject5.transform, "取消", delegate
		{
			_plugin.CancelWorkshopLocalization();
			RefreshWorkshopLocalizationPage();
		}, 0f, 38f, CheatMenuStyle.Danger);
		CreateLabel(panel, "缓存文件：" + workshopLocalization.CachePath, 11, CheatMenuStyle.Muted);
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
		{
			return;
		}
		if (_workshopProtocolDrawerRoot != null)
		{
			QueueForDestroy(_workshopProtocolDrawerRoot);
		}
		_workshopProtocolDrawerRoot = CreateObject("WorkshopProtocolDrawer", _root.transform, typeof(Image));
		_workshopProtocolDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _workshopProtocolDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_workshopProtocolDrawerPopup = _workshopProtocolDrawerRoot.GetComponent<RectTransform>();
		RectTransform workshopProtocolDrawerPopup = _workshopProtocolDrawerPopup;
		Vector2 anchorMin = (_workshopProtocolDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		workshopProtocolDrawerPopup.anchorMin = anchorMin;
		_workshopProtocolDrawerPopup.pivot = new Vector2(0.5f, 1f);
		string[] supportedProtocols = MachineTranslationSettings.SupportedProtocols;
		int num = Mathf.CeilToInt((float)supportedProtocols.Length / 3f);
		_workshopProtocolDrawerPopup.sizeDelta = new Vector2(356f, 12f + (float)num * 28f + (float)Math.Max(0, num - 1) * 4f);
		GridLayoutGroup gridLayoutGroup = _workshopProtocolDrawerRoot.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(6, 6, 6, 6);
		gridLayoutGroup.spacing = new Vector2(4f, 4f);
		gridLayoutGroup.cellSize = new Vector2(112f, 28f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 3;
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		string apiProtocol = _plugin.WorkshopLocalization.Settings.ApiProtocol;
		string[] array = supportedProtocols;
		foreach (string text in array)
		{
			string capturedProtocol = text;
			string label = PluginLocalization.Translate(MachineTranslationSettings.GetDisplayName(capturedProtocol));
			SetLayout(CreateButton(_workshopProtocolDrawerRoot.transform, label, delegate
			{
				_plugin.SetWorkshopLocalizationApiProtocol(capturedProtocol);
				HideWorkshopProtocolDrawer();
				RefreshWorkshopLocalizationPage();
			}, 0f, 28f, string.Equals(capturedProtocol, apiProtocol, StringComparison.OrdinalIgnoreCase) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 10, capturedProtocol).gameObject, -1f, 28f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_workshopProtocolDrawerPopup);
		_workshopProtocolDrawerRoot.transform.SetAsLastSibling();
		PositionWorkshopProtocolDrawer();
	}

	private void MaintainWorkshopProtocolDrawer()
	{
		if (!(_workshopProtocolDrawerRoot == null))
		{
			PositionWorkshopProtocolDrawer();
			if (Input.GetMouseButtonDown(0) && _workshopProtocolTriggerButton != null && _workshopProtocolDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_workshopProtocolDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_workshopProtocolTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideWorkshopProtocolDrawer();
			}
		}
	}

	private void PositionWorkshopProtocolDrawer()
	{
		if (!(_workshopProtocolTriggerButton == null) && !(_workshopProtocolDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _workshopProtocolTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			Vector2 screenPoint3 = RectTransformUtility.WorldToScreenPoint(null, array[2]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint3, null, out var localPoint3);
			Vector2 sizeDelta = _workshopProtocolDrawerPopup.sizeDelta;
			float value = (localPoint.x + localPoint3.x) * 0.5f;
			bool flag = localPoint.y - component2.rect.yMin < sizeDelta.y + 8f;
			_workshopProtocolDrawerPopup.pivot = (flag ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f));
			float min = component2.rect.xMin + sizeDelta.x * 0.5f + 8f;
			float max = component2.rect.xMax - sizeDelta.x * 0.5f - 8f;
			float x = Mathf.Clamp(value, min, max);
			float y = (flag ? localPoint2.y : localPoint.y);
			_workshopProtocolDrawerPopup.anchoredPosition = new Vector2(x, y);
		}
	}

	private void HideWorkshopProtocolDrawer()
	{
		_workshopProtocolDrawerOpen = false;
		if (_workshopProtocolDrawerRoot != null)
		{
			QueueForDestroy(_workshopProtocolDrawerRoot);
		}
		_workshopProtocolDrawerRoot = null;
		_workshopProtocolDrawerPopup = null;
		_workshopProtocolTriggerButton = null;
	}

	private void StartWorkshopTranslationFromUi()
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		if (workshopLocalization != null && workshopLocalization.IsTranslationRunning)
		{
			SetStatus("翻译正在进行中，请不要重复点击开始翻译。", 5f, StatusKind.Info);
			return;
		}
		_plugin.ScanWorkshopLocalization(allowAutoTranslate: false);
		bool num = _plugin.TranslateWorkshopLocalization(force: false);
		WorkshopLocalizationProgress workshopLocalizationProgress = _plugin.WorkshopLocalization?.Progress;
		bool flag = workshopLocalizationProgress != null && workshopLocalizationProgress.State == WorkshopLocalizationState.Translating;
		RefreshWorkshopLocalizationPage();
		if (num | flag)
		{
			SetStatus("已开始翻译未缓存文本。", 5f, StatusKind.Info);
			return;
		}
		string text = workshopLocalizationProgress?.Message;
		string text2 = ((text == "translation API is not configured") ? "未能开始翻译：当前供应商配置不完整。" : ((!(text == "translation cancelled")) ? (string.IsNullOrWhiteSpace(workshopLocalizationProgress?.Message) ? "未能开始翻译：请检查当前供应商配置或任务状态。" : ("未能开始翻译：" + workshopLocalizationProgress.Message)) : "翻译任务已取消。"));
		string message = text2;
		SetStatus(message, 5f, StatusKind.Warning);
	}

	private void StopWorkshopTranslationFromUi()
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		if (workshopLocalization == null || !workshopLocalization.IsTranslationRunning)
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
		if (_activeToolsTab == ToolsTab.Translation)
		{
			ReplaceContentHost();
			BuildToolsTab();
		}
	}

	private void RefreshWorkshopLocalizationPageValues()
	{
		WorkshopLocalizationService workshopLocalization = _plugin.WorkshopLocalization;
		if (workshopLocalization == null)
		{
			return;
		}
		WorkshopLocalizationProgress progress = workshopLocalization.Progress;
		string arg = progress.State switch
		{
			WorkshopLocalizationState.Scanning => "扫描中",
			WorkshopLocalizationState.Translating => "翻译中",
			WorkshopLocalizationState.Cancelled => "已取消",
			WorkshopLocalizationState.Error => "错误",
			_ => (!string.Equals(progress.Message, "translation complete", StringComparison.Ordinal)) ? ((!string.Equals(progress.Message, "no uncached text", StringComparison.Ordinal)) ? "空闲" : "完成") : "完成",
		};
		if (_workshopLocalizationStateText != null)
		{
			_workshopLocalizationStateText.text = PluginLocalization.Translate($"状态：{arg}；已扫描物品 {progress.ItemsScanned} / 载具 {progress.VehiclesScanned}");
			Text workshopLocalizationStateText = _workshopLocalizationStateText;
			Color color;
			switch (progress.State)
			{
			case WorkshopLocalizationState.Scanning:
			case WorkshopLocalizationState.Translating:
				color = CheatMenuStyle.Accent;
				break;
			case WorkshopLocalizationState.Error:
				color = CheatMenuStyle.Danger;
				break;
			case WorkshopLocalizationState.Cancelled:
				color = CheatMenuStyle.Warning;
				break;
			default:
				color = ((!string.Equals(progress.Message, "translation complete", StringComparison.Ordinal)) ? ((!string.Equals(progress.Message, "no uncached text", StringComparison.Ordinal)) ? CheatMenuStyle.Text : CheatMenuStyle.Success) : CheatMenuStyle.Success);
				break;
			}
			workshopLocalizationStateText.color = color;
		}
		if (_workshopLocalizationSummaryText != null)
		{
			_workshopLocalizationSummaryText.text = PluginLocalization.Translate($"文本总数 {progress.TotalTexts} · 缓存命中 {progress.CacheHits} · 中文跳过 {progress.SkippedTargetLanguageTexts} · 待翻译 {progress.PendingTranslations} · 完成 {progress.CompletedTranslations} · 失败 {progress.FailedTranslations}");
		}
		if (_workshopLocalizationApiText != null)
		{
			string text = (workshopLocalization.ApiConfigured ? "已配置" : "未配置");
			_workshopLocalizationApiText.text = PluginLocalization.Translate("翻译接口：" + text + "；供应商：" + MachineTranslationSettings.GetDisplayName(workshopLocalization.Settings.ApiProtocol) + "；目标语言：" + workshopLocalization.Settings.TargetLanguage + "；模型：" + (string.IsNullOrWhiteSpace(workshopLocalization.Settings.Model) ? "未填写" : workshopLocalization.Settings.Model));
		}
	}

	private void BuildInventoryArrangePanel(Transform panel)
	{
		InventoryArrangeSettings inventoryArrangeSettings = _plugin.GetInventoryArrangeSettings();
		string text = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(inventoryArrangeSettings.Preset));
		CreateLabel(panel, "安全背包整理", 17, CheatMenuStyle.Text);
		CreateLabel(panel, "排序预设：" + text + "；默认禁止自动掉落。", 13, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(panel, 44f, 8f);
		CreateLabel(gameObject.transform, "排序预设", 14, CheatMenuStyle.Text, 110f);
		Button presetButton = null;
		presetButton = CreateButton(gameObject.transform, text + " ▼", delegate
		{
			ToggleInventoryPresetDrawer(presetButton);
		}, 0f, 40f, CheatMenuStyle.Surface, 13, "点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。");
		SetLayout(presetButton.gameObject, -1f, -1f, 1f);
		GameObject scopeRow = CreateRow(panel, 44f, 8f);
		CreateLabel(scopeRow.transform, "整理范围", 14, CheatMenuStyle.Text, 110f);
		Button scopeButton = null;
		scopeButton = CreateButton(scopeRow.transform, "配置随身页 ▼", delegate
		{
			ToggleInventoryScopeDrawer(scopeButton);
		}, 0f, 40f, CheatMenuStyle.Surface, 13, "分别开启或关闭背包、背心、上衣和裤子储物页；关闭的页面不会参与整理。");
		SetLayout(scopeButton.gameObject, -1f, -1f, 1f);
		if (inventoryArrangeSettings.Preset == InventorySortPreset.Custom)
		{
			CreateLabel(panel, "自定义排序链（最多 5 项）：Key:Direction,Key:Direction", 12, CheatMenuStyle.Muted);
			CreateInput(panel, _plugin.InventoryArrangeCustomKeys, delegate(string value)
			{
				_plugin.SetInventoryArrangeCustomKeys(value);
			}, 0f, 1f);
		}
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "自动旋转", inventoryArrangeSettings.AutoRotate, delegate(bool value)
		{
			_plugin.SetInventoryArrangeAutoRotate(value);
			SetStatus(value ? "整理将允许自动旋转物品。" : "整理已禁止自动旋转物品。", 4f, StatusKind.Info);
		}, "允许物品在寻找位置时旋转 90°；关闭后保持原旋转。");
		CreateToggleAction(gameObject2.transform, "允许跨页", inventoryArrangeSettings.AllowCrossPage, delegate(bool value)
		{
			_plugin.SetInventoryArrangeCrossPage(value);
			SetStatus(value ? "整理将允许在当前范围内跨页安置。" : "整理将保持原页范围。", 4f, StatusKind.Info);
		}, "允许在当前整理范围的随身页之间寻找空位；关闭后保持原页范围。");
		GameObject gameObject3 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject3.transform, "保护装备", inventoryArrangeSettings.ProtectEquipped, delegate(bool value)
		{
			_plugin.SetInventoryArrangeProtectEquipped(value);
			SetStatus(value ? "已保护当前装备物品。" : "将允许整理当前装备物品。", 4f, StatusKind.Info);
		}, "保护当前装备物品；开启时不会移除或重排它。");
		CreateToggleAction(gameObject3.transform, "固定热键物品", inventoryArrangeSettings.FixHotkeyItems, delegate(bool value)
		{
			_plugin.SetInventoryArrangeFixHotkeyItems(value);
			SetStatus(value ? "热键绑定物品将保持在原坐标。" : "热键绑定物品会随整理移动并更新坐标。", 4f, StatusKind.Info);
		}, "开启时固定绑定热键的物品；关闭时整理会同步快捷键到新坐标。");
		CreateLabel(panel, "安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。", 12, CheatMenuStyle.Warning);
		CreateLabel(panel, "跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。", 12, CheatMenuStyle.Muted);
		GameObject gameObject4 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject4.transform, "预览当前", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, preview: true);
		}, 120f, 34f, CheatMenuStyle.Surface, 13, "只生成移动、归并和跨页摘要，不修改库存。");
		CreateButton(gameObject4.transform, "整理当前", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Arrange, preview: false);
		}, 120f, 34f, CheatMenuStyle.Accent, 13, "整理当前上下文：打开储物箱时整理储物箱，否则整理随身容器。");
		CreateButton(gameObject4.transform, "整理全部随身", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.AllCarried, InventoryArrangeAction.Arrange, preview: false);
		}, 150f, 34f, CheatMenuStyle.Accent, 13, "整理所有随身页，不处理当前储物箱；默认保护装备并固定热键物品。");
		GameObject gameObject5 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject5.transform, "仅压缩", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Compact, preview: false);
		}, 120f, 34f, CheatMenuStyle.Surface, 13, "只压缩空间并保持相对顺序；不按普通预设重排，自动补弹仍会执行。");
		CreateButton(gameObject5.transform, "撤销上次整理", ShowInventoryArrangeUndo, 150f, 34f, CheatMenuStyle.Surface, 13, "撤销最近一次成功整理；外部改变库存后撤销会失效。");
		CreateButton(gameObject5.transform, "仅归并", delegate
		{
			ShowInventoryArrangeResult(InventoryArrangeScope.CurrentContext, InventoryArrangeAction.Merge, preview: false);
		}, 120f, 34f, CheatMenuStyle.Surface, 13, "只归并兼容堆叠，不重排无关物品；弹匣和弹药箱会按兼容组重新分配总弹量。");
		CreateLabel(gameObject5.transform, "快捷键可在窗口右下角的全局快捷键中设置。", 12, CheatMenuStyle.Muted);
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
		{
			return;
		}
		if (_inventoryPresetDrawerRoot != null)
		{
			QueueForDestroy(_inventoryPresetDrawerRoot);
		}
		_inventoryPresetDrawerRoot = CreateObject("InventoryPresetDrawer", _root.transform, typeof(Image));
		_inventoryPresetDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _inventoryPresetDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_inventoryPresetDrawerPopup = _inventoryPresetDrawerRoot.GetComponent<RectTransform>();
		RectTransform inventoryPresetDrawerPopup = _inventoryPresetDrawerPopup;
		Vector2 anchorMin = (_inventoryPresetDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		inventoryPresetDrawerPopup.anchorMin = anchorMin;
		_inventoryPresetDrawerPopup.pivot = new Vector2(0.5f, 1f);
		InventorySortPreset[] availablePresets = InventorySortRegistry.AvailablePresets;
		int num = Mathf.CeilToInt((float)availablePresets.Length / 3f);
		_inventoryPresetDrawerPopup.sizeDelta = new Vector2(352f, 12f + (float)num * 28f + (float)Math.Max(0, num - 1) * 4f);
		GridLayoutGroup gridLayoutGroup = _inventoryPresetDrawerRoot.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(6, 6, 6, 6);
		gridLayoutGroup.spacing = new Vector2(4f, 4f);
		gridLayoutGroup.cellSize = new Vector2(112f, 28f);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 3;
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		InventoryArrangeSettings inventoryArrangeSettings = _plugin.GetInventoryArrangeSettings();
		InventorySortPreset[] array = availablePresets;
		foreach (InventorySortPreset inventorySortPreset in array)
		{
			InventorySortPreset capturedPreset = inventorySortPreset;
			string label = PluginLocalization.Translate(InventorySortRegistry.GetDisplayName(capturedPreset));
			SetLayout(CreateButton(_inventoryPresetDrawerRoot.transform, label, delegate
			{
				_plugin.SetInventoryArrangePreset(capturedPreset);
				HideInventoryPresetDrawer();
				ReplaceContentHost();
				BuildToolsTab();
			}, 0f, 28f, (capturedPreset == inventoryArrangeSettings.Preset) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 10, InventoryPresetTooltip(capturedPreset)).gameObject, -1f, 28f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryPresetDrawerPopup);
		_inventoryPresetDrawerRoot.transform.SetAsLastSibling();
		PositionInventoryPresetDrawer();
	}

	private void MaintainInventoryPresetDrawer()
	{
		if (!(_inventoryPresetDrawerRoot == null))
		{
			PositionInventoryPresetDrawer();
			if (Input.GetMouseButtonDown(0) && _inventoryPresetTriggerButton != null && _inventoryPresetDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryPresetDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryPresetTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideInventoryPresetDrawer();
			}
		}
	}

	private void PositionInventoryPresetDrawer()
	{
		if (!(_inventoryPresetTriggerButton == null) && !(_inventoryPresetDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _inventoryPresetTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			Vector2 screenPoint3 = RectTransformUtility.WorldToScreenPoint(null, array[2]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint3, null, out var localPoint3);
			Vector2 sizeDelta = _inventoryPresetDrawerPopup.sizeDelta;
			float value = (localPoint.x + localPoint3.x) * 0.5f;
			bool flag = localPoint.y - component2.rect.yMin < sizeDelta.y + 8f;
			_inventoryPresetDrawerPopup.pivot = (flag ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f));
			float min = component2.rect.xMin + sizeDelta.x * 0.5f + 8f;
			float max = component2.rect.xMax - sizeDelta.x * 0.5f - 8f;
			float x = Mathf.Clamp(value, min, max);
			float y = (flag ? localPoint2.y : localPoint.y);
			_inventoryPresetDrawerPopup.anchoredPosition = new Vector2(x, y);
		}
	}

	private void HideInventoryPresetDrawer()
	{
		_inventoryPresetDrawerOpen = false;
		if (_inventoryPresetDrawerRoot != null)
		{
			QueueForDestroy(_inventoryPresetDrawerRoot);
		}
		_inventoryPresetDrawerRoot = null;
		_inventoryPresetDrawerPopup = null;
		_inventoryPresetTriggerButton = null;
	}

	private void ToggleInventoryScopeDrawer(Button trigger)
	{
		if (_inventoryScopeDrawerOpen)
		{
			HideInventoryScopeDrawer();
			return;
		}
		_inventoryScopeDrawerOpen = true;
		_inventoryScopeTriggerButton = trigger;
		BuildInventoryScopeDrawer();
	}

	private void BuildInventoryScopeDrawer()
	{
		if (_inventoryScopeTriggerButton == null || _root == null)
		{
			return;
		}
		if (_inventoryScopeDrawerRoot != null)
		{
			QueueForDestroy(_inventoryScopeDrawerRoot);
		}
		_inventoryScopeDrawerRoot = CreateObject("InventoryScopeDrawer", _root.transform, typeof(Image));
		_inventoryScopeDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _inventoryScopeDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_inventoryScopeDrawerPopup = _inventoryScopeDrawerRoot.GetComponent<RectTransform>();
		_inventoryScopeDrawerPopup.anchorMin = new Vector2(0.5f, 0.5f);
		_inventoryScopeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
		_inventoryScopeDrawerPopup.pivot = new Vector2(0.5f, 1f);
		_inventoryScopeDrawerPopup.sizeDelta = new Vector2(248f, 218f);
		VerticalLayoutGroup layout = _inventoryScopeDrawerRoot.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(8, 8, 8, 8);
		layout.spacing = 4f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		Text title = CreateLabel(_inventoryScopeDrawerRoot.transform, "选择参与整理的随身页", 13, CheatMenuStyle.Text);
		SetLayout(title.gameObject, -1f, 24f, 1f);
		InventoryArrangeSettings settings = _plugin.GetInventoryArrangeSettings();
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "背包", settings.IncludeBackpackPage, delegate(bool value)
		{
			_plugin.SetInventoryArrangeIncludeBackpackPage(value);
			SetStatus(value ? "背包页已纳入整理范围。" : "背包页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理背包储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "背心", settings.IncludeVestPage, delegate(bool value)
		{
			_plugin.SetInventoryArrangeIncludeVestPage(value);
			SetStatus(value ? "背心页已纳入整理范围。" : "背心页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理背心储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "上衣", settings.IncludeShirtPage, delegate(bool value)
		{
			_plugin.SetInventoryArrangeIncludeShirtPage(value);
			SetStatus(value ? "上衣页已纳入整理范围。" : "上衣页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理上衣储物页；关闭后该页物品和位置保持不变。");
		CreateToggleAction(_inventoryScopeDrawerRoot.transform, "裤子", settings.IncludePantsPage, delegate(bool value)
		{
			_plugin.SetInventoryArrangeIncludePantsPage(value);
			SetStatus(value ? "裤子页已纳入整理范围。" : "裤子页已排除出整理范围。", 4f, StatusKind.Info);
		}, "开启后整理裤子储物页；关闭后该页物品和位置保持不变。");
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryScopeDrawerPopup);
		_inventoryScopeDrawerRoot.transform.SetAsLastSibling();
		PositionInventoryScopeDrawer();
	}

	private void MaintainInventoryScopeDrawer()
	{
		if (_inventoryScopeDrawerRoot != null)
		{
			PositionInventoryScopeDrawer();
			if (Input.GetMouseButtonDown(0) && _inventoryScopeTriggerButton != null && _inventoryScopeDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryScopeDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_inventoryScopeTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HideInventoryScopeDrawer();
			}
		}
	}

	private void PositionInventoryScopeDrawer()
	{
		if (_inventoryScopeTriggerButton != null && _inventoryScopeDrawerPopup != null && _root != null)
		{
			RectTransform trigger = _inventoryScopeTriggerButton.GetComponent<RectTransform>();
			RectTransform root = _root.GetComponent<RectTransform>();
			Vector3[] corners = new Vector3[4];
			trigger.GetWorldCorners(corners);
			Vector2 bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
			Vector2 top = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
			Vector2 rightTop = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, bottom, null, out var localBottom);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, top, null, out var localTop);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(root, rightTop, null, out var localRightTop);
			Vector2 size = _inventoryScopeDrawerPopup.sizeDelta;
			float centerX = (localBottom.x + localRightTop.x) * 0.5f;
			bool placeBelow = localBottom.y - root.rect.yMin < size.y + 8f;
			_inventoryScopeDrawerPopup.pivot = placeBelow ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
			float minX = root.rect.xMin + size.x * 0.5f + 8f;
			float maxX = root.rect.xMax - size.x * 0.5f - 8f;
			float x = Mathf.Clamp(centerX, minX, maxX);
			float y = placeBelow ? localTop.y : localBottom.y;
			_inventoryScopeDrawerPopup.anchoredPosition = new Vector2(x, y);
		}
	}

	private void HideInventoryScopeDrawer()
	{
		_inventoryScopeDrawerOpen = false;
		if (_inventoryScopeDrawerRoot != null)
		{
			QueueForDestroy(_inventoryScopeDrawerRoot);
		}
		_inventoryScopeDrawerRoot = null;
		_inventoryScopeDrawerPopup = null;
		_inventoryScopeTriggerButton = null;
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
			InventorySortPreset.Hotkey => "绑定快捷栏的物品优先；开启固定热键物品时，绑定物品不会被移动。",
			InventorySortPreset.LowMovement => "尽量保持原来的页、位置和旋转，只做必要的整理。",
			InventorySortPreset.Custom => "按下面输入框里的排序链执行，例如 Name:Descending,Area:Ascending。",
			_ => "按安全的默认规则整理。",
		};
	}

	private void ShowInventoryArrangeResult(InventoryArrangeScope scope, InventoryArrangeAction action, bool preview)
	{
		InventoryArrangeResult inventoryArrangeResult = ((!preview) ? _plugin.InventoryArrange?.Execute(scope, action, "工具页") : _plugin.InventoryArrange?.Preview(scope, action));
		SetStatus(inventoryArrangeResult?.Message ?? "背包整理尚未准备完成。", 6f, (inventoryArrangeResult != null && inventoryArrangeResult.Success) ? StatusKind.Success : StatusKind.Warning);
	}

	private void ShowInventoryArrangeUndo()
	{
		InventoryArrangeResult inventoryArrangeResult = _plugin.InventoryArrange?.Undo();
		SetStatus(inventoryArrangeResult?.Message ?? "没有可撤销的整理记录。", 6f, (inventoryArrangeResult != null && inventoryArrangeResult.Success) ? StatusKind.Success : StatusKind.Warning);
	}

	private const int AutoPickupCardPageSize = 10;

	private const int AutoPickupPickerPageSize = 15;

	private void BuildAutoPickupPanel(Transform panel)
	{
		AutoPickupFeature feature = _plugin.AutoPickup;
		if (feature == null)
		{
			CreateEmptyState(panel, "自动拾取不可用", "插件尚未完成自动拾取服务初始化。");
			return;
		}
		List<ItemAsset> whitelistAssets = GetFilteredAutoPickupRuleAssets(feature.Rules, ItemFilterRuleAction.Allow);
		List<ItemAsset> blacklistAssets = GetFilteredAutoPickupRuleAssets(feature.Rules, ItemFilterRuleAction.Deny);
		int whitelistPageCount = Math.Max(1, Mathf.CeilToInt((float)whitelistAssets.Count / AutoPickupCardPageSize));
		int blacklistPageCount = Math.Max(1, Mathf.CeilToInt((float)blacklistAssets.Count / AutoPickupCardPageSize));
		int pageCount = 1 + whitelistPageCount + blacklistPageCount;
		_autoPickupPage = Mathf.Clamp(_autoPickupPage, 0, pageCount - 1);
		GameObject pager = CreateRow(panel, 0f, 8f);
		SetLayout(pager, -1f, 510f, 1f, 0f);
		GameObject pageBody = CreateObject("AutoPickupPageBody", pager.transform, typeof(Image));
		pageBody.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		AddVerticalLayout(pageBody, 8f, new RectOffset(14, 14, 12, 12));
		SetLayout(pageBody, -1f, -1f, 1f, 1f);
		AddPageScrollHandler(pageBody, _autoPickupPage, pageCount, delegate(int page)
		{
			_autoPickupPage = Mathf.Clamp(page, 0, pageCount - 1);
			ShowTab(MenuTab.Tools);
		});
		pageBody.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		CreateAutoPickupPageDots(pager.transform, _autoPickupPage, pageCount);
		if (_autoPickupPage == 0)
		{
			BuildAutoPickupSettingsPage(pageBody.transform, feature);
		}
		else if (_autoPickupPage <= whitelistPageCount)
		{
			BuildAutoPickupRulePage(pageBody.transform, whitelistAssets, _autoPickupPage - 1, whitelistPageCount, ItemFilterRuleAction.Allow);
		}
		else
		{
			BuildAutoPickupRulePage(pageBody.transform, blacklistAssets, _autoPickupPage - 1 - whitelistPageCount, blacklistPageCount, ItemFilterRuleAction.Deny);
		}
	}

	private void CreateAutoPickupPageDots(Transform parent, int page, int pageCount)
	{
		GameObject dots = CreateObject("AutoPickupPageDots", parent, typeof(Image));
		dots.GetComponent<Image>().color = Color.clear;
		SetLayout(dots, 26f, -1f, 0f, 1f);
		VerticalLayoutGroup layout = dots.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 2f;
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		float dotSize = Mathf.Clamp((480f - Math.Max(0, pageCount - 1) * layout.spacing) / pageCount, 8f, 18f);
		int fontSize = dotSize >= 12f ? 12 : 8;
		for (int i = 0; i < pageCount; i++)
		{
			Text dot = CreateText(dots.transform, "●", fontSize, i == page ? CheatMenuStyle.AccentBright : CheatMenuStyle.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, localize: false);
			SetLayout(dot.gameObject, dotSize, dotSize, 0f, 0f);
		}
	}

	private void BuildAutoPickupSettingsPage(Transform page, AutoPickupFeature feature)
	{
		AutoPickupSettings settings = feature.Settings;
		AutoPickupRuleDocument document = feature.Rules;
		AutoPickupDiagnosticsSnapshot diagnostics = feature.Diagnostics;
		CreateSummaryPanel(page, $"{PluginLocalization.Translate(diagnostics.State)}  ·  候选 {diagnostics.CandidateCount}  ·  成功 {diagnostics.PickedUpCount}  ·  失败 {diagnostics.FailureCount}  ·  重试 {diagnostics.RetryCount}");
		GameObject switches = CreateRow(page, 40f, 7f);
		CreateToggleAction(switches.transform, "启用自动拾取", settings.Enabled, delegate(bool value)
		{
			_plugin.SetAutoPickupEnabled(value);
			SetStatus((_plugin.AutoPickup.Settings.Enabled == value) ? (value ? "自动拾取已启用；关闭菜单后开始扫描。" : "自动拾取已关闭。") : "只有进入真正的单人世界后才能启用自动拾取。", 5f, (_plugin.AutoPickup.Settings.Enabled == value) ? StatusKind.Info : StatusKind.Warning);
		});
		CreateToggleAction(switches.transform, "菜单打开时暂停", settings.PauseWhenMenuOpen, _plugin.SetAutoPickupPauseWhenMenuOpen);
		CreateToggleAction(switches.transform, "死亡时暂停", settings.PauseWhenDead, _plugin.SetAutoPickupPauseWhenDead);
		CreateAutoPickupSlider(page, "拾取距离", settings.Range, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange, "m", _autoPickupRangeDebouncer, _plugin.SetAutoPickupRange);
		GameObject scheduling = CreateRow(page, 40f, 7f);
		CreateAutoPickupSlider(scheduling.transform, "扫描频率", settings.FrequencyHz, AutoPickupSettings.MinFrequencyHz, AutoPickupSettings.MaxFrequencyHz, "Hz", _autoPickupFrequencyDebouncer, _plugin.SetAutoPickupFrequency);
		GameObject dropBlockRow = CreateRow(page, 40f, 8f);
		CreateLabel(dropBlockRow.transform, "丢弃后禁拾时长", 13, CheatMenuStyle.Text, 120f);
		InputField dropBlockInput = CreateInput(dropBlockRow.transform, settings.DropBlockMinutes.ToString(CultureInfo.InvariantCulture), delegate(string value)
		{
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
			{
				_plugin.SetAutoPickupDropBlockMinutes(minutes);
			}
			ShowTab(MenuTab.Tools);
		}, 92f, 0f, false, true, "玩家丢弃某个物品后，暂时禁止自动拾取同 ID 物品；再次丢弃同 ID 会重新计时。范围 1-1440 分钟，默认 1 分钟。");
		dropBlockInput.contentType = InputField.ContentType.IntegerNumber;
		dropBlockInput.characterLimit = 4;
		CreateLabel(dropBlockRow.transform, "分钟（1-1440）", 12, CheatMenuStyle.Muted, 110f);
		GameObject qualityFilterRow = CreateRow(page, 40f, 8f);
		CreateToggleAction(qualityFilterRow.transform, "启用品质过滤", settings.QualityFilterEnabled, _plugin.SetAutoPickupQualityFilterEnabled);
		CreateLabel(qualityFilterRow.transform, "最低品质", 13, CheatMenuStyle.Text, 72f);
		InputField qualityInput = CreateInput(qualityFilterRow.transform, settings.MinimumQuality.ToString(CultureInfo.InvariantCulture), delegate(string value)
		{
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quality))
			{
				_plugin.SetAutoPickupMinimumQuality(quality);
			}
			ShowTab(MenuTab.Tools);
		}, 72f, 0f, false, true, "启用后，自动拾取会忽略品质低于该数值的物品。范围 0-100。");
		qualityInput.contentType = InputField.ContentType.IntegerNumber;
		qualityInput.characterLimit = 3;
		CreateLabel(qualityFilterRow.transform, "品质（0-100）", 12, CheatMenuStyle.Muted, 100f);
		CreateButton(scheduling.transform, $"每轮 {settings.RequestsPerTick} 个请求", delegate
		{
			int value = settings.RequestsPerTick >= AutoPickupSettings.MaxRequestsPerTick ? AutoPickupSettings.MinRequestsPerTick : settings.RequestsPerTick + 1;
			_plugin.SetAutoPickupRequestsPerTick(value);
			ShowTab(MenuTab.Tools);
		}, 150f, 36f, CheatMenuStyle.SurfaceHover, 12, "每次扫描最多发送的原生拾取请求数，范围 1-16。");
		CreateButton(scheduling.transform, "诊断：" + AutoPickupDiagnosticsLabel(settings.Diagnostics), delegate
		{
			AutoPickupDiagnosticsLevel[] values = (AutoPickupDiagnosticsLevel[])Enum.GetValues(typeof(AutoPickupDiagnosticsLevel));
			int next = (Array.IndexOf(values, settings.Diagnostics) + 1) % values.Length;
			_plugin.SetAutoPickupDiagnostics(values[next]);
			ShowTab(MenuTab.Tools);
		}, 130f, 36f, CheatMenuStyle.SurfaceHover, 12);
		int whitelistCount = document.Rules.Count(rule => rule.Enabled && rule.Action == ItemFilterRuleAction.Allow && rule.Field == ItemFilterRuleField.AssetId);
		CreateSection(page, "白名单");
		GameObject whitelistSummary = CreateRow(page, 38f, 8f);
		CreateToggleAction(whitelistSummary.transform, "启用白名单", document.Enabled, delegate(bool value)
		{
			AutoPickupRuleLoadResult result = _plugin.SetAutoPickupWhitelistEnabled(value);
			SetStatus(result.Success ? (value ? "白名单已启用；现在只拾取白名单物品。" : "白名单已停用；黑名单仍然生效。") : "保存白名单状态失败：" + result.Error, 4f, result.Success ? StatusKind.Info : StatusKind.Error);
			ShowTab(MenuTab.Tools);
		});
		CreateLabel(whitelistSummary.transform, $"当前 {whitelistCount} 条白名单物品。", 12, CheatMenuStyle.Muted, 170f);
		CreateButton(whitelistSummary.transform, "管理白名单", delegate
		{
			_autoPickupPage = 1;
			ShowTab(MenuTab.Tools);
		}, 110f, 34f, CheatMenuStyle.SurfaceHover, 12);
		CreateButton(whitelistSummary.transform, "添加白名单", ShowAutoPickupWhitelistPicker, 110f, 34f, CheatMenuStyle.Success, 12);
		CreateSection(page, "黑名单");
		GameObject blacklistSummary = CreateRow(page, 38f, 8f);
		int blacklistCount = document.Rules.Count(rule => rule.Enabled && rule.Action == ItemFilterRuleAction.Deny && rule.Field == ItemFilterRuleField.AssetId);
		CreateLabel(blacklistSummary.transform, $"当前 {blacklistCount} 条黑名单物品；黑名单始终优先。", 12, CheatMenuStyle.Muted);
		CreateFlexibleSpacer(blacklistSummary.transform);
		CreateButton(blacklistSummary.transform, "管理黑名单", delegate
		{
			_autoPickupPage = GetAutoPickupRuleFirstPage(ItemFilterRuleAction.Deny);
			ShowTab(MenuTab.Tools);
		}, 110f, 34f, CheatMenuStyle.SurfaceHover, 12);
		CreateButton(blacklistSummary.transform, "添加黑名单", ShowAutoPickupBlacklistPicker, 110f, 34f, CheatMenuStyle.Danger, 12);
	}

	private void BuildAutoPickupRulePage(Transform page, List<ItemAsset> assets, int cardPage, int pageCount, ItemFilterRuleAction ruleAction)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		CreateSection(page, whitelist ? "白名单物品" : "黑名单物品");
		GameObject header = CreateRow(page, 38f, 8f);
		CreateLabel(header.transform, whitelist ? "点击物品卡片可取消白名单。" : "点击物品卡片可取消黑名单。", 12, CheatMenuStyle.Muted);
		CreateFlexibleSpacer(header.transform);
		CreateButton(header.transform, whitelist ? "添加白名单" : "添加黑名单", whitelist ? ShowAutoPickupWhitelistPicker : ShowAutoPickupBlacklistPicker, 110f, 34f, whitelist ? CheatMenuStyle.Success : CheatMenuStyle.Danger, 12);
		GameObject searchRow = CreateRow(page, 36f, 7f);
		CreateLabel(searchRow.transform, "搜索", 12, CheatMenuStyle.Muted, 42f);
		CreateInput(searchRow.transform, _autoPickupRuleSearch, delegate(string value)
		{
			_autoPickupRuleSearch = value ?? string.Empty;
			_autoPickupPage = GetAutoPickupRuleFirstPage(ruleAction);
			_autoPickupRuleSearchDebouncer.Schedule(delegate
			{
				if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.AutoPickup && _autoPickupPage > 0)
				{
					ShowTab(MenuTab.Tools);
				}
			});
		}, 0f, 1f);
		CreateLabel(searchRow.transform, $"{assets.Count} 件  ·  第 {cardPage + 1}/{pageCount} 页", 11, CheatMenuStyle.Muted, 150f, TextAnchor.MiddleRight);
		if (assets.Count == 0)
		{
			CreateEmptyState(page, whitelist ? "白名单为空" : "黑名单为空", whitelist ? "启用白名单并添加物品后，只会拾取白名单物品。" : "默认不限制物品；点击“添加黑名单”从当前背包选择物品。");
			return;
		}
		GameObject grid = CreateObject("AutoPickupBlacklistGrid", page);
		SetLayout(grid, -1f, 330f, 1f, 0f);
		GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
		layout.padding = new RectOffset(4, 4, 4, 4);
		layout.spacing = new Vector2(8f, 8f);
		layout.cellSize = new Vector2(155f, 158f);
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 5;
		int start = cardPage * AutoPickupCardPageSize;
		int end = Math.Min(start + AutoPickupCardPageSize, assets.Count);
		for (int i = start; i < end; i++)
		{
			ItemAsset asset = assets[i];
			CreateAutoPickupItemCard(grid.transform, asset, ruleAction, isListed: true, delegate { RemoveAutoPickupRule(ruleAction, asset.id); });
		}
	}

	private string GetItemDisplayName(ItemAsset asset)
	{
		if (asset == null)
		{
			return string.Empty;
		}
		WorkshopLocalizationService localization = _plugin.WorkshopLocalization;
		return localization != null && localization.TryGetTranslation(asset, WorkshopTextField.ItemName, out string translation) && !string.IsNullOrWhiteSpace(translation) ? translation : asset.FriendlyName;
	}

	private List<ItemAsset> GetAutoPickupRuleAssets(AutoPickupRuleDocument document, ItemFilterRuleAction ruleAction)
	{
		HashSet<ushort> ids = new HashSet<ushort>();
		foreach (ItemFilterRule rule in document?.Rules ?? Enumerable.Empty<ItemFilterRule>())
		{
			if (rule.Enabled && rule.Action == ruleAction && rule.Field == ItemFilterRuleField.AssetId && ushort.TryParse(rule.Value, NumberStyles.None, CultureInfo.InvariantCulture, out ushort id))
			{
				ids.Add(id);
			}
		}
		List<ItemAsset> assets = _plugin.Catalog.Items.Where(asset => asset != null && ids.Contains(asset.id)).ToList();
		assets.Sort((left, right) => string.Compare(GetItemDisplayName(left), GetItemDisplayName(right), StringComparison.CurrentCultureIgnoreCase));
		return assets;
	}

	private List<ItemAsset> GetFilteredAutoPickupRuleAssets(AutoPickupRuleDocument document, ItemFilterRuleAction ruleAction)
	{
		List<ItemAsset> assets = GetAutoPickupRuleAssets(document, ruleAction);
		string query = (_autoPickupRuleSearch ?? string.Empty).Trim();
		if (query.Length > 0)
		{
			assets = assets.Where(asset => AssetCatalog.Matches(asset, GetItemDisplayName(asset), query)).ToList();
		}
		return assets;
	}

	private HashSet<ushort> GetAutoPickupRuleIds(ItemFilterRuleAction ruleAction)
	{
		HashSet<ushort> ids = new HashSet<ushort>();
		foreach (ItemFilterRule rule in _plugin.AutoPickup.Rules.Rules)
		{
			if (rule.Enabled && rule.Action == ruleAction && rule.Field == ItemFilterRuleField.AssetId && ushort.TryParse(rule.Value, NumberStyles.None, CultureInfo.InvariantCulture, out ushort id))
			{
				ids.Add(id);
			}
		}
		return ids;
	}

	private int GetAutoPickupRuleFirstPage(ItemFilterRuleAction ruleAction)
	{
		if (ruleAction == ItemFilterRuleAction.Allow)
		{
			return 1;
		}
		int whitelistCount = GetFilteredAutoPickupRuleAssets(_plugin.AutoPickup.Rules, ItemFilterRuleAction.Allow).Count;
		return 1 + Math.Max(1, Mathf.CeilToInt((float)whitelistCount / AutoPickupCardPageSize));
	}

	private List<ItemAsset> GetCurrentBackpackAssets()
	{
		List<ItemAsset> result = new List<ItemAsset>();
		HashSet<ushort> seen = new HashSet<ushort>();
		PlayerInventory inventory = Player.LocalPlayer?.inventory;
		if (inventory?.items == null)
		{
			return result;
		}
		for (byte page = PlayerInventory.BACKPACK; page < PlayerInventory.STORAGE && page < inventory.items.Length; page++)
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
					result.Add(asset);
				}
			}
		}
		result.Sort((left, right) => string.Compare(GetItemDisplayName(left), GetItemDisplayName(right), StringComparison.CurrentCultureIgnoreCase));
		return result;
	}

	private void CreateAutoPickupItemCard(Transform parent, ItemAsset asset, ItemFilterRuleAction ruleAction, bool isListed, System.Action action)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		string displayName = GetItemDisplayName(asset);
		string tooltip = AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _plugin.Catalog.Items);
		Color color = !isListed ? CheatMenuStyle.Surface : (whitelist ? CheatMenuStyle.Success : CheatMenuStyle.Danger);
		Button button = CreateButton(parent, string.Empty, action, 155f, 158f, color, tooltip: tooltip, delayTooltip: true);
		string meta = whitelist
			? (isListed ? $"ID {asset.id}  ·  点击取消白名单" : $"ID {asset.id}  ·  点击加入白名单")
			: (isListed ? $"ID {asset.id}  ·  点击取消黑名单" : $"ID {asset.id}  ·  点击加入黑名单");
		AddCardContents(button.transform, displayName, meta, () => _plugin.Icons.GetItemIcon(asset), isVehicle: false);
	}

	private void AddAutoPickupRule(ItemFilterRuleAction ruleAction, ItemAsset asset)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		if (asset == null || GetAutoPickupRuleIds(ruleAction).Contains(asset.id))
		{
			SetStatus(whitelist ? "该物品已经在白名单中。" : "该物品已经在黑名单中。", 3f, StatusKind.Info);
			return;
		}
		AutoPickupRuleDocument document = _plugin.AutoPickup.Rules;
		List<ItemFilterRule> rules = document.Rules.Select(rule => rule.Clone()).ToList();
		rules.Add(new ItemFilterRule
		{
			Action = ruleAction,
			Field = ItemFilterRuleField.AssetId,
			Value = asset.id.ToString(CultureInfo.InvariantCulture)
		});
		bool whitelistEnabled = whitelist || document.Enabled;
		AutoPickupRuleLoadResult result = _plugin.SetAutoPickupRules(whitelistEnabled, whitelistEnabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly, rules);
		string success = whitelist ? $"已将 {GetItemDisplayName(asset)} 加入白名单。" : $"已将 {GetItemDisplayName(asset)} 加入黑名单。";
		string failure = (whitelist ? "保存白名单失败：" : "保存黑名单失败：") + result.Error;
		SetStatus(result.Success ? success : failure, 4f, result.Success ? StatusKind.Success : StatusKind.Error);
		if (result.Success)
		{
			RefreshAutoPickupPanel(ruleAction);
		}
	}

	private void RemoveAutoPickupRule(ItemFilterRuleAction ruleAction, ushort assetId)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		AutoPickupRuleDocument document = _plugin.AutoPickup.Rules;
		List<ItemFilterRule> rules = document.Rules.Where(rule => !(rule.Action == ruleAction && rule.Field == ItemFilterRuleField.AssetId && string.Equals(rule.Value, assetId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))).Select(rule => rule.Clone()).ToList();
		AutoPickupRuleLoadResult result = _plugin.SetAutoPickupRules(document.Enabled, document.Mode, rules);
		string success = whitelist ? $"已取消物品 ID {assetId} 的白名单。" : $"已取消物品 ID {assetId} 的黑名单。";
		string failure = (whitelist ? "保存白名单失败：" : "保存黑名单失败：") + result.Error;
		SetStatus(result.Success ? success : failure, 4f, result.Success ? StatusKind.Success : StatusKind.Error);
		if (result.Success)
		{
			RefreshAutoPickupPanel(ruleAction);
		}
	}

	private void RefreshAutoPickupPanel(ItemFilterRuleAction ruleAction)
	{
		_autoPickupPage = GetAutoPickupRuleFirstPage(ruleAction);
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.AutoPickup)
		{
			ShowTab(MenuTab.Tools);
		}
	}

	private void ShowAutoPickupWhitelistPicker()
	{
		ShowAutoPickupRulePicker(ItemFilterRuleAction.Allow);
	}

	private void ShowAutoPickupBlacklistPicker()
	{
		ShowAutoPickupRulePicker(ItemFilterRuleAction.Deny);
	}

	private void ShowAutoPickupRulePicker(ItemFilterRuleAction ruleAction)
	{
		_autoPickupPickerAction = ruleAction;
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		if (_autoPickupBlacklistPickerRoot != null)
		{
			_autoPickupBlacklistPickerRoot.SetActive(true);
			BuildAutoPickupBlacklistPickerContents();
			return;
		}
		HideHoverExplanation();
		_autoPickupBlacklistPickerRoot = CreateObject("AutoPickupBlacklistPicker", _root.transform, typeof(Image));
		_autoPickupBlacklistPickerRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
		RectTransform rootRect = _autoPickupBlacklistPickerRoot.GetComponent<RectTransform>();
		rootRect.anchorMin = Vector2.zero;
		rootRect.anchorMax = Vector2.one;
		rootRect.offsetMin = Vector2.zero;
		rootRect.offsetMax = Vector2.zero;
		GameObject popup = CreateObject("Popup", _autoPickupBlacklistPickerRoot.transform, typeof(Image));
		popup.GetComponent<Image>().color = CheatMenuStyle.Panel;
		RectTransform popupRect = popup.GetComponent<RectTransform>();
		popupRect.anchorMin = new Vector2(0.5f, 0.5f);
		popupRect.anchorMax = new Vector2(0.5f, 0.5f);
		popupRect.pivot = new Vector2(0.5f, 0.5f);
		popupRect.sizeDelta = new Vector2(980f, 760f);
		VerticalLayoutGroup layout = popup.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(18, 18, 16, 16);
		layout.spacing = 8f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		GameObject header = CreateRow(popup.transform, 38f, 8f);
		CreateLabel(header.transform, whitelist ? "从当前背包添加白名单" : "从当前背包添加黑名单", 18, CheatMenuStyle.Text);
		CreateFlexibleSpacer(header.transform);
		CreateButton(header.transform, "×", HideAutoPickupBlacklistPicker, 42f, 36f, CheatMenuStyle.Surface, 22);
		CreateLabel(popup.transform, whitelist ? "物品按 ID 自动去重；点击卡片加入白名单，悬停卡片可查看完整物品详情。" : "物品按 ID 自动去重；点击卡片加入黑名单，悬停卡片可查看完整物品详情。", 12, CheatMenuStyle.Muted);
		GameObject search = CreateRow(popup.transform, 36f, 7f);
		CreateLabel(search.transform, "搜索", 12, CheatMenuStyle.Muted, 42f);
		CreateInput(search.transform, _autoPickupBlacklistPickerSearch, delegate(string value)
		{
			_autoPickupBlacklistPickerSearch = value ?? string.Empty;
			_autoPickupBlacklistPickerPage = 0;
			BuildAutoPickupBlacklistPickerContents();
		}, 0f, 1f);
		_autoPickupBlacklistPickerHost = CreateObject("PickerHost", popup.transform, typeof(Image));
		_autoPickupBlacklistPickerHost.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		AddVerticalLayout(_autoPickupBlacklistPickerHost, 8f, new RectOffset(8, 8, 8, 8));
		SetLayout(_autoPickupBlacklistPickerHost, -1f, 560f, 1f, 0f);
		_autoPickupBlacklistPickerRoot.transform.SetAsLastSibling();
		BuildAutoPickupBlacklistPickerContents();
	}

	private void BuildAutoPickupBlacklistPickerContents()
	{
		if (_autoPickupBlacklistPickerHost == null || _plugin.AutoPickup == null)
		{
			return;
		}
		for (int i = _autoPickupBlacklistPickerHost.transform.childCount - 1; i >= 0; i--)
		{
			GameObject child = _autoPickupBlacklistPickerHost.transform.GetChild(i).gameObject;
			child.SetActive(false);
			QueueForDestroy(child);
		}
		List<ItemAsset> assets = GetCurrentBackpackAssets();
		ItemFilterRuleAction ruleAction = _autoPickupPickerAction;
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		string query = (_autoPickupBlacklistPickerSearch ?? string.Empty).Trim();
		if (query.Length > 0)
		{
			assets = assets.Where(asset => AssetCatalog.Matches(asset, GetItemDisplayName(asset), query)).ToList();
		}
		int pageCount = Math.Max(1, Mathf.CeilToInt((float)assets.Count / AutoPickupPickerPageSize));
		_autoPickupBlacklistPickerPage = Mathf.Clamp(_autoPickupBlacklistPickerPage, 0, pageCount - 1);
		GameObject header = CreateRow(_autoPickupBlacklistPickerHost.transform, 30f, 6f);
		CreateLabel(header.transform, $"当前背包去重后 {assets.Count} 件  ·  第 {_autoPickupBlacklistPickerPage + 1}/{pageCount} 页", 11, CheatMenuStyle.Muted);
		if (assets.Count == 0)
		{
			CreateEmptyState(_autoPickupBlacklistPickerHost.transform, "当前背包没有匹配物品。", "请先进入单人世界并确认物品在随身背包中。");
			return;
		}
		GameObject grid = CreateObject("PickerGrid", _autoPickupBlacklistPickerHost.transform);
		SetLayout(grid, -1f, 500f, 1f, 0f);
		GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
		layout.padding = new RectOffset(4, 4, 4, 4);
		layout.spacing = new Vector2(8f, 8f);
		layout.cellSize = new Vector2(155f, 158f);
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 5;
		AddPageScrollHandler(grid, _autoPickupBlacklistPickerPage, pageCount, delegate(int value)
		{
			_autoPickupBlacklistPickerPage = value;
			BuildAutoPickupBlacklistPickerContents();
		});
		HashSet<ushort> listedIds = GetAutoPickupRuleIds(ruleAction);
		int start = _autoPickupBlacklistPickerPage * AutoPickupPickerPageSize;
		int end = Math.Min(start + AutoPickupPickerPageSize, assets.Count);
		for (int i = start; i < end; i++)
		{
			ItemAsset asset = assets[i];
			bool isListed = listedIds.Contains(asset.id);
			CreateAutoPickupItemCard(grid.transform, asset, ruleAction, isListed, isListed ? delegate { SetStatus(whitelist ? "该物品已经在白名单中。" : "该物品已经在黑名单中。", 3f, StatusKind.Info); } : delegate { AddAutoPickupRule(ruleAction, asset); });
		}
	}

	private void HideAutoPickupBlacklistPicker()
	{
		if (_autoPickupBlacklistPickerRoot != null)
		{
			QueueForDestroy(_autoPickupBlacklistPickerRoot);
		}
		_autoPickupBlacklistPickerRoot = null;
		_autoPickupBlacklistPickerHost = null;
	}


	private void CreateAutoPickupSlider(Transform parent, string label, float value, float min, float max, string suffix, UiDebouncer debouncer, Action<float> changed)
	{
		GameObject row = CreateRow(parent, 40f, 8f);
		CreateLabel(row.transform, label, 13, CheatMenuStyle.Text, 92f);
		Text valueText = CreateText(row.transform, $"{value:0}{suffix}", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, FontStyle.Normal, localize: false);
		SetLayout(valueText.gameObject, 68f, 30f);
		SetLayout(CreateTimeSlider(row.transform, Mathf.InverseLerp(min, max, value), delegate(float normalized)
		{
			float selected = Mathf.Clamp(Mathf.Round(Mathf.Lerp(min, max, Mathf.Clamp01(normalized))), min, max);
			valueText.text = $"{selected:0}{suffix}";
			debouncer.Schedule(delegate { changed(selected); });
		}).gameObject, -1f, -1f, 1f);
	}


	private static string AutoPickupDiagnosticsLabel(AutoPickupDiagnosticsLevel level)
	{
		return level switch
		{
			AutoPickupDiagnosticsLevel.Off => "关闭",
			AutoPickupDiagnosticsLevel.Failures => "失败",
			AutoPickupDiagnosticsLevel.Summary => "摘要",
			_ => "逐项"
		};
	}

	private void BuildPointToolPanel(Transform panel)
	{
		GameObject creatureHealthRow = CreateRow(panel, 40f, 7f);
		CreateToggleAction(creatureHealthRow.transform, "生物头顶血条", _plugin.CreatureHealthBarEnabled, delegate(bool value)
		{
			_plugin.SetCreatureHealthBarEnabled(value);
			SetStatus(value ? "生物头顶血条已开启。" : "生物头顶血条已关闭。", 5f, StatusKind.Info);
		});
		CreateLabel(panel, "准心目标或实际受伤后的生物会显示当前生命 / 生命上限。", 12, CheatMenuStyle.Muted);
		GameObject gameObject = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject.transform, "启用准星交互工具", _plugin.PointTool.Enabled, delegate(bool value)
		{
			_plugin.SetPointToolEnabled(value);
			SetStatus(value ? "准星交互工具已开启。" : "准星交互工具已关闭。", 5f, StatusKind.Info);
		});
		Button modeTrigger = null;
		modeTrigger = CreatePointToolModeButton(gameObject.transform, _plugin.PointTool.Mode, delegate
		{
			TogglePointToolModeDrawer(modeTrigger);
		});
		CreateLabel(panel, "中键触发；删除必须同时按住 Shift。", 13, CheatMenuStyle.Muted);
		CreateButton(panel, $"最大距离 {_plugin.PointTool.Range:0}m", delegate
		{
			float pointToolRange = ((_plugin.PointTool.Range >= 250f) ? 25f : (_plugin.PointTool.Range + 25f));
			_plugin.SetPointToolRange(pointToolRange);
			ReplaceContentHost();
			BuildToolsTab();
		}, 220f);
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "名称", _plugin.PointTool.ShowTargetName, delegate(bool value)
		{
			_plugin.SetPointToolShowTargetName(value);
		});
		CreateToggleAction(gameObject2.transform, "ID / GUID", _plugin.PointTool.ShowId, delegate(bool value)
		{
			_plugin.SetPointToolShowId(value);
		});
		CreateToggleAction(gameObject2.transform, "生命 / 耐久", _plugin.PointTool.ShowHealth, delegate(bool value)
		{
			_plugin.SetPointToolShowHealth(value);
		});
		BuildPointToolDescriptions(panel);
	}

	private void BuildPointToolDescriptions(Transform parent)
	{
		GameObject gameObject = CreateObject("PointToolDescriptions", parent, typeof(Image));
		gameObject.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		AddVerticalLayout(gameObject, 5f, new RectOffset(12, 12, 10, 10));
		SetLayout(gameObject, -1f, 286f, 1f, 1f);
		CreateLabel(gameObject.transform, "各模式功能介绍", 14, CheatMenuStyle.Text);
		AddPointToolDescription(gameObject.transform, PointToolMode.Smart, "根据准星目标自动选择单一动作：维修、实用、准星传送、检查或 Shift + 中键删除。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Inspect, "查看准星指向对象的名称、ID / GUID、生命 / 耐久和位置等信息。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Repair, "维修车辆耐久、电瓶、油量、轮胎并扶正翻倒车辆，也可修复路障和建筑。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Teleport, "将玩家传送到准星指向的世界表面，并自动寻找安全落点。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Utility, "执行准星指向对象的原生交互，例如上车、打开容器或切换设备。");
		AddPointToolDescription(gameObject.transform, PointToolMode.Delete, "删除准星指向的可删除对象；必须同时按住 Shift 再点击中键。");
	}

	private void AddPointToolDescription(Transform parent, PointToolMode mode, string description)
	{
		GameObject gameObject = CreateRow(parent, 32f, 10f);
		CreateLabel(gameObject.transform, PointToolModeLabel(mode), 12, CheatMenuStyle.Accent, 72f);
		SetLayout(CreateText(gameObject.transform, description, 12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft).gameObject, -1f, 32f, 1f);
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
			_ => "智能",
		};
	}

	private Button CreatePointToolModeButton(Transform parent, PointToolMode mode, System.Action action)
	{
		return CreateButton(parent, PointToolModeLabel(mode), action, 150f, 40f, (_plugin.PointTool.Mode == mode) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12);
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
		{
			return;
		}
		if (_pointToolModeDrawerRoot != null)
		{
			QueueForDestroy(_pointToolModeDrawerRoot);
		}
		_pointToolModeDrawerRoot = CreateObject("PointToolModeDrawer", _root.transform, typeof(Image));
		_pointToolModeDrawerRoot.GetComponent<Image>().color = CheatMenuStyle.Panel;
		Outline outline = _pointToolModeDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		_pointToolModeDrawerPopup = _pointToolModeDrawerRoot.GetComponent<RectTransform>();
		RectTransform pointToolModeDrawerPopup = _pointToolModeDrawerPopup;
		Vector2 anchorMin = (pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		pointToolModeDrawerPopup.anchorMin = anchorMin;
		pointToolModeDrawerPopup.pivot = new Vector2(0f, 1f);
		pointToolModeDrawerPopup.sizeDelta = new Vector2(150f, 280f);
		VerticalLayoutGroup verticalLayoutGroup = _pointToolModeDrawerRoot.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		PointToolMode[] pointToolModes = PointToolModes;
		foreach (PointToolMode pointToolMode in pointToolModes)
		{
			PointToolMode capturedMode = pointToolMode;
			SetLayout(CreateButton(_pointToolModeDrawerRoot.transform, PointToolModeLabel(capturedMode), delegate
			{
				_plugin.SetPointToolMode(capturedMode);
				HidePointToolModeDrawer();
				ReplaceContentHost();
				BuildToolsTab();
			}, 0f, 42f, (_plugin.PointTool.Mode == capturedMode) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12).gameObject, -1f, 42f, 1f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_pointToolModeDrawerPopup);
		PositionPointToolModeDrawer();
	}

	private void MaintainPointToolModeDrawer()
	{
		if (!(_pointToolModeDrawerRoot == null))
		{
			PositionPointToolModeDrawer();
			if (Input.GetMouseButtonDown(0) && _pointToolModeTriggerButton != null && _pointToolModeDrawerPopup != null && !RectTransformUtility.RectangleContainsScreenPoint(_pointToolModeDrawerPopup, Input.mousePosition, null) && !RectTransformUtility.RectangleContainsScreenPoint(_pointToolModeTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
			{
				HidePointToolModeDrawer();
			}
		}
	}

	private void PositionPointToolModeDrawer()
	{
		if (!(_pointToolModeTriggerButton == null) && !(_pointToolModeDrawerPopup == null) && !(_root == null))
		{
			RectTransform component = _pointToolModeTriggerButton.GetComponent<RectTransform>();
			RectTransform component2 = _root.GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, array[0]);
			Vector2 screenPoint2 = RectTransformUtility.WorldToScreenPoint(null, array[1]);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint, null, out var localPoint);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(component2, screenPoint2, null, out var localPoint2);
			Vector2 sizeDelta = _pointToolModeDrawerPopup.sizeDelta;
			sizeDelta.x = component.rect.width;
			_pointToolModeDrawerPopup.sizeDelta = sizeDelta;
			RectTransform pointToolModeDrawerPopup = _pointToolModeDrawerPopup;
			Vector2 anchorMin = (_pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
			pointToolModeDrawerPopup.anchorMin = anchorMin;
			bool flag = localPoint.y - component2.rect.yMin < _pointToolModeDrawerPopup.rect.height + 8f;
			_pointToolModeDrawerPopup.pivot = (flag ? new Vector2(0f, 0f) : new Vector2(0f, 1f));
			_pointToolModeDrawerPopup.anchoredPosition = (flag ? localPoint2 : localPoint);
		}
	}

	private void HidePointToolModeDrawer()
	{
		_pointToolModeDrawerOpen = false;
		if (_pointToolModeDrawerRoot != null)
		{
			QueueForDestroy(_pointToolModeDrawerRoot);
		}
		_pointToolModeDrawerRoot = null;
		_pointToolModeDrawerPopup = null;
		_pointToolModeTriggerButton = null;
	}

	private void BuildMovementPanel(Transform panel)
	{
		GameObject modeRow = CreateRow(panel, 40f, 8f);
		CreateLabel(modeRow.transform, $"飞行模式：{_plugin.Movement.Mode.ToDisplayName()}", 13, CheatMenuStyle.Text);
		CreateFlexibleSpacer(modeRow.transform);
		CreateButton(modeRow.transform, "⇆", delegate
		{
			FlightMode nextMode = _plugin.Movement.Mode == FlightMode.WorldAxis ? FlightMode.ViewDirection : FlightMode.WorldAxis;
			_plugin.SetFlightMode(nextMode);
			SetStatus($"飞行模式已切换为 {nextMode.ToDisplayName()}。", 5f, StatusKind.Info);
			ReplaceContentHost();
			BuildToolsTab();
		}, 44f, 36f, CheatMenuStyle.Accent, 18, "切换飞行模式：在世界轴与视角指向之间切换。", delayTooltip: false);
		GameObject gameObject = CreateRow(panel, 42f, 8f);
		CreateToggleAction(gameObject.transform, "飞行", _plugin.Movement.FlightEnabled, delegate(bool value)
		{
			_plugin.Movement.SetFlight(value);
			SetStatus(value ? "飞行已开启。" : "飞行已关闭。", 5f, StatusKind.Info);
		});
		CreateToggleAction(gameObject.transform, "穿墙", _plugin.Movement.NoclipEnabled, delegate(bool value)
		{
			_plugin.Movement.SetNoclip(value);
			SetStatus(value ? "穿墙已开启。" : "穿墙已关闭。", 5f, StatusKind.Info);
		});
		CreateMovementSpeedSlider(panel, "水平速度", _plugin.Movement.FlightSpeed, _plugin.SetFlightSpeed);
		CreateMovementSpeedSlider(panel, "垂直速度", _plugin.Movement.VerticalSpeed, _plugin.SetFlightVerticalSpeed);
		CreateToggleAction(panel, "关闭穿墙后寻找安全位置", _plugin.Movement.SafeExit, delegate(bool value)
		{
			_plugin.SetNoclipSafeExit(value);
		});
		CreateLabel(panel, "WASD 移动，Space 上升，Ctrl 下降。", 13, CheatMenuStyle.Muted);
	}

	private void CreateMovementSpeedSlider(Transform parent, string label, float value, Action<float> changed)
	{
		GameObject gameObject = CreateRow(parent, 40f, 8f);
		CreateLabel(gameObject.transform, label, 13, CheatMenuStyle.Text, 92f);
		Text valueText = CreateText(gameObject.transform, $"{value:0}x", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, FontStyle.Normal, localize: false);
		SetLayout(valueText.gameObject, 42f, 30f);
		SetLayout(CreateTimeSlider(gameObject.transform, Mathf.InverseLerp(1f, 10f, value), delegate(float normalized)
		{
			float num = Mathf.Clamp(Mathf.Round(Mathf.Lerp(1f, 10f, Mathf.Clamp01(normalized))), 1f, 10f);
			changed(num);
			valueText.text = $"{num:0}x";
		}).gameObject, -1f, -1f, 1f);
	}
}
