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
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;
using InputField = UnityEngine.UI.InputField;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi : IDisposable
{
	internal enum MenuTab
	{
		Character,
		Items,
		Vehicles,
		Favorites,
		Teleports,
		Tools,
		Other
	}

	internal enum FavoriteKind
	{
		Items,
		Vehicles
	}

	internal enum TeleportView
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

	private enum ColorPickerPurpose
	{
		TeleportMarker,
		Accent
	}

	private sealed class ColorSwatchControl
	{
		public Color Color;

		public GameObject Check;
	}

	internal enum ToolsTab
	{
		Interaction,
		Inventory,
		AutoPickup,
		Movement,
		Minimap,
		Translation
	}

	private const int OverlaySortingOrder = 29900;

	private const float PreferredWindowWidth = 1280f;
	private const float PreferredInfiniteStorageWindowWidth = 1120f;
	private const float PreferredWindowHeight = 790f;
	private const float MinimumWindowWidth = 900f;
	private const float MinimumWindowHeight = 620f;
	private const float WindowMargin = 16f;

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
		"#252525", "#F7F5EF", "#0078D4", "#4BA3DF", "#2D8036", "#9AC65A", "#C27B10", "#F1BF45", "#AF3030", "#E94A4A",
		"#CB5A97", "#8659D4", "#D5D4CC", "#8C8D86", "#CDE5FA", "#F8ECD7", "#1C425A", "#4B3917"
	};

	private readonly PluginUiRoot _uiRoot;
	private readonly UiStateStore _state;
	private readonly ShellPresenter _shellPresenter;
	private readonly CharacterPagePresenter _characterPresenter;
	private readonly CatalogPagePresenter _catalogPresenter;
	private readonly TeleportPagePresenter _teleportPresenter;
	private readonly WorldPagePresenter _worldPresenter;
	private readonly InfiniteStoragePagePresenter _infiniteStoragePresenter;
	private readonly InteractionToolsPresenter _interactionPresenter;
	private readonly InventoryToolsPresenter _inventoryPresenter;
	private readonly AutoPickupToolsPresenter _autoPickupPresenter;
	private readonly MovementToolsPresenter _movementPresenter;
	private readonly MinimapToolsPresenter _minimapPresenter;
	private readonly TranslationToolsPresenter _translationPresenter;
	private readonly LegacyFluentUiContext _fluentContext;
	private readonly LegacyFluentHandleRegistry _fluentHandles;
	private readonly LegacyFluentTooltipBridge _fluentTooltips;
	private readonly LegacyFluentControlAdapter _fluentControls;

	private ItemFilterState _itemFilter => _state.ItemFilter;

	private ItemFilterState _favoriteItemFilter => _state.FavoriteItemFilter;

	private ItemFilterState _infiniteStorageFilter => _state.InfiniteStorageFilter;

	private IReadOnlyList<ItemAsset> _itemResults => _catalogPresenter.ItemResults;

	private IReadOnlyList<VehicleAsset> _vehicleResults => _catalogPresenter.VehicleResults;

	private IReadOnlyList<ItemAsset> _favoriteItemResults => _catalogPresenter.FavoriteItemResults;

	private IReadOnlyList<VehicleAsset> _favoriteVehicleResults => _catalogPresenter.FavoriteVehicleResults;

	private readonly Dictionary<ushort, ItemAsset> _infiniteStorageAssetsById = new Dictionary<ushort, ItemAsset>();

	private readonly DeferredUiDestroyQueue<GameObject> _deferredDestroy = new DeferredUiDestroyQueue<GameObject>();

	private readonly Dictionary<MenuTab, Button> _tabButtons = new Dictionary<MenuTab, Button>();
	private readonly Dictionary<ToolsTab, Button> _toolsTabButtons = new Dictionary<ToolsTab, Button>();
	private CheatMenuToolsPageViewHost _toolsPageViewHost;
	private GameObject _toolsPageHost;
	private GameObject _toolsPageContentHost;

	private readonly Dictionary<TeleportMarkerKind, Sprite> _teleportMarkerSprites = new Dictionary<TeleportMarkerKind, Sprite>();

	private readonly Dictionary<TeleportMarkerKind, Texture2D> _teleportMarkerTextures = new Dictionary<TeleportMarkerKind, Texture2D>();

	private readonly UiDebouncer _timeApplyDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _autoPickupRangeDebouncer = new UiDebouncer(0.15f);

	private readonly UiDebouncer _autoPickupFrequencyDebouncer = new UiDebouncer(0.15f);
	private readonly UiDebouncer _minimapSettingsDebouncer = new UiDebouncer(0.15f);

	private readonly List<UiSearchInputController> _searchInputs = new List<UiSearchInputController>();

	private readonly AmmoCompatibilityIndex _ammoCompatibilityIndex = new AmmoCompatibilityIndex();

	private GameObject _root;

	private GameObject _infiniteStorageRoot;

	private RectTransform _infiniteStoragePanel;

	private GameObject _infiniteStorageContentHost;

	private GameObject _infiniteStorageContentRow;

	private GameObject _infiniteStorageCategoryHost;

	private GameObject _infiniteStorageResultsHost;

	private Text _infiniteStorageFilterButtonText;

	private Text _infiniteStorageSummaryText;

	private GameObject _hoverExplanationRoot;

	private Text _hoverExplanationText;

	private RectTransform _panel;
	private GameObject _shellBody;
	private GameObject _narrowShellBody;
	private GameObject _pageRegion;
	private GameObject _navigationViewport;
	private RectTransform _navigationContent;
	private RectTransform _narrowNavigationContent;
	private ScrollRect _navigationScroll;
	private HorizontalLayoutGroup _wideBodyLayout;
	private VerticalLayoutGroup _narrowBodyLayout;
	private VerticalLayoutGroup _wideNavigationLayout;
	private HorizontalLayoutGroup _narrowNavigationLayout;
	private ContentSizeFitter _navigationFitter;
	private ContentSizeFitter _narrowNavigationFitter;
	private bool? _wideShellLayout;

	private GameObject _contentHost;
	private CheatMenuPageViewHost _pageViewHost;

	private GameObject _itemResultsHost;
	private GameObject _itemGridHost;
	private GameObject _itemSummaryHost;

	private GameObject _vehicleResultsHost;
	private GameObject _vehicleGridHost;
	private Text _vehicleSummaryText;

	private GameObject _favoriteResultsHost;
	private GameObject _favoriteGridHost;
	private GameObject _favoriteBodyHost;
	private GameObject _favoriteSummaryHost;
	private Button _favoriteItemsButton;
	private Button _favoriteVehiclesButton;

	private Text _mapAndShortcutText;

	private Text _statusText;

	private Image _statusAccent;

	private TeleportMapSurface _teleportMapSurface;

	private Text _timeCurrentText;

	private Text _timePreviewText;

	private LegacyFluentTimeRingControl _timeRing;

	private readonly List<InputField> _itemAmountInputs = new List<InputField>();

	private readonly List<InputField> _vehicleAmountInputs = new List<InputField>();

	private GameObject _confirmRoot;

	private GameObject _aboutRoot;

	private GameObject _fontPickerRoot;

	private GameObject _shortcutRoot;

	private ScrollRect _shortcutScrollRect;

	private RectTransform _shortcutScrollContent;

	private float _shortcutScrollPosition { get => _state.ShortcutScrollPosition; set => _state.ShortcutScrollPosition = value; }

	private Text _shortcutCaptureText;

	private HashSet<KeyCode> _shortcutCaptureModifiers => _state.ShortcutCaptureModifiers;

	private bool _shortcutCaptureActive { get => _state.ShortcutCaptureActive; set => _state.ShortcutCaptureActive = value; }

	private string _shortcutCaptureActionId { get => _state.ShortcutCaptureActionId; set => _state.ShortcutCaptureActionId = value; }

	private GameObject _itemFilterRoot;

	private ItemFilterState _itemFilterDraft { get => _state.ItemFilterDraft; set => _state.ItemFilterDraft = value; }

	private bool _itemFilterForFavorites { get => _state.ItemFilterForFavorites; set => _state.ItemFilterForFavorites = value; }

	private bool _itemFilterForInfiniteStorage { get => _state.ItemFilterForInfiniteStorage; set => _state.ItemFilterForInfiniteStorage = value; }

	private GameObject _vehicleThumbnailSettingsRoot;

	private Slider _vehicleThumbnailFramingSlider;

	private Text _vehicleThumbnailFramingText;

	private int _vehicleThumbnailDraftWidth { get => _state.VehicleThumbnailDraftWidth; set => _state.VehicleThumbnailDraftWidth = value; }

	private float _vehicleThumbnailDraftFraming { get => _state.VehicleThumbnailDraftFraming; set => _state.VehicleThumbnailDraftFraming = value; }

	private Font _font;
	private int _lastMainLayoutScreenWidth = -1;
	private int _lastMainLayoutScreenHeight = -1;
	private float _lastMainLayoutScale = -1f;
	private float _lastMainLayoutRequestedScale = -1f;
	private int _lastInfiniteLayoutScreenWidth = -1;
	private int _lastInfiniteLayoutScreenHeight = -1;
	private float _lastInfiniteLayoutRequestedScale = -1f;
	private int _lastGridPageSize = -1;

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

	private MenuTab _activeTab { get => _state.ActiveTab; set => _state.ActiveTab = value; }

	private string _vehicleQuery { get => _state.VehicleQuery; set => _state.VehicleQuery = value; }

	private string _vehicleCategory { get => _state.VehicleCategory; set => _state.VehicleCategory = value; }

	private string _favoriteQuery { get => _state.FavoriteQuery; set => _state.FavoriteQuery = value; }

	private string _favoriteVehicleCategory { get => _state.FavoriteVehicleCategory; set => _state.FavoriteVehicleCategory = value; }

	private string _teleportName { get => _state.TeleportName; set => _state.TeleportName = value; }

	private string _lastHeaderMap = string.Empty;

	private string _lastHeaderShortcut = string.Empty;

	private string _healthTarget { get => _state.HealthTarget; set => _state.HealthTarget = value; }

	private string _foodTarget { get => _state.FoodTarget; set => _state.FoodTarget = value; }

	private string _waterTarget { get => _state.WaterTarget; set => _state.WaterTarget = value; }

	private string _virusTarget { get => _state.VirusTarget; set => _state.VirusTarget = value; }

	private string _staminaTarget { get => _state.StaminaTarget; set => _state.StaminaTarget = value; }

	private string _oxygenTarget { get => _state.OxygenTarget; set => _state.OxygenTarget = value; }

	private string _experienceAmount { get => _state.ExperienceAmount; set => _state.ExperienceAmount = value; }

	private string _reputationAmount { get => _state.ReputationAmount; set => _state.ReputationAmount = value; }

	private string _itemAmountText { get => _state.ItemAmountText; set => _state.ItemAmountText = value; }

	private string _vehicleAmountText { get => _state.VehicleAmountText; set => _state.VehicleAmountText = value; }

	private int _itemPage { get => _state.ItemPage; set => _state.ItemPage = value; }

	private int _infiniteStoragePage { get => _state.InfiniteStoragePage; set => _state.InfiniteStoragePage = value; }

	private int _vehiclePage { get => _state.VehiclePage; set => _state.VehiclePage = value; }

	private int _favoritePage { get => _state.FavoritePage; set => _state.FavoritePage = value; }

	private int _teleportPage { get => _state.TeleportPage; set => _state.TeleportPage = value; }

	private FavoriteKind _favoriteKind { get => _state.FavoriteKind; set => _state.FavoriteKind = value; }

	private TeleportView _teleportView { get => _state.TeleportView; set => _state.TeleportView = value; }

	private TeleportMarkerKind _teleportMarkerKind { get => _state.TeleportMarkerKind; set => _state.TeleportMarkerKind = value; }

	private Color _teleportMarkerColor { get => _state.TeleportMarkerColor; set => _state.TeleportMarkerColor = value; }

	private bool _teleportMarkerDrawerOpen { get => _state.TeleportMarkerDrawerOpen; set => _state.TeleportMarkerDrawerOpen = value; }

	private GameObject _teleportMarkerDrawerRoot;

	private RectTransform _teleportMarkerDrawerPopup;

	private Button _teleportMarkerTriggerButton;

	private GameObject _teleportColorPickerRoot;

	private Color _teleportColorDraft { get => _state.TeleportColorDraft; set => _state.TeleportColorDraft = value; }

	private Color _accentColor { get => _state.AccentColor; set => _state.AccentColor = value; }

	private ColorPickerPurpose _colorPickerPurpose;

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

	private byte _giveAmount { get => _state.GiveAmount; set => _state.GiveAmount = value; }

	private int _spawnVehicleAmount { get => _state.SpawnVehicleAmount; set => _state.SpawnVehicleAmount = value; }

	private float _statusUntil { get => _state.StatusUntil; set => _state.StatusUntil = value; }

	private ToolsTab _activeToolsTab { get => _state.ActiveToolsTab; set => _state.ActiveToolsTab = value; }

	private string _autoPickupRuleSearch { get => _state.AutoPickupRuleSearch; set => _state.AutoPickupRuleSearch = value; }

	private int _autoPickupPage { get => _state.AutoPickupPage; set => _state.AutoPickupPage = value; }
	private GameObject _autoPickupContentHost;

	private int _autoPickupBlacklistPickerPage { get => _state.AutoPickupBlacklistPickerPage; set => _state.AutoPickupBlacklistPickerPage = value; }

	private string _autoPickupBlacklistPickerSearch { get => _state.AutoPickupBlacklistPickerSearch; set => _state.AutoPickupBlacklistPickerSearch = value; }

	private GameObject _autoPickupBlacklistPickerRoot;

	private GameObject _autoPickupBlacklistPickerHost;

	private GameObject _autoPickupRarityPickerRoot;

	private GameObject _autoPickupRuleResultsHost;

	private ItemFilterRuleAction _autoPickupPickerAction { get => _state.AutoPickupPickerAction; set => _state.AutoPickupPickerAction = value; }

	private static readonly PointToolMode[] PointToolModes = new PointToolMode[6]
	{
		PointToolMode.Smart,
		PointToolMode.Inspect,
		PointToolMode.Repair,
		PointToolMode.Teleport,
		PointToolMode.Utility,
		PointToolMode.Delete
	};

	private bool _pointToolModeDrawerOpen { get => _state.PointToolModeDrawerOpen; set => _state.PointToolModeDrawerOpen = value; }

	private GameObject _pointToolModeDrawerRoot;

	private RectTransform _pointToolModeDrawerPopup;

	private Button _pointToolModeTriggerButton;

	private bool _inventoryPresetDrawerOpen { get => _state.InventoryPresetDrawerOpen; set => _state.InventoryPresetDrawerOpen = value; }

	private GameObject _inventoryPresetDrawerRoot;

	private RectTransform _inventoryPresetDrawerPopup;

	private Button _inventoryPresetTriggerButton;

	private bool _inventoryScopeDrawerOpen { get => _state.InventoryScopeDrawerOpen; set => _state.InventoryScopeDrawerOpen = value; }

	private GameObject _inventoryScopeDrawerRoot;

	private RectTransform _inventoryScopeDrawerPopup;

	private Button _inventoryScopeTriggerButton;

	private bool _workshopProtocolDrawerOpen { get => _state.WorkshopProtocolDrawerOpen; set => _state.WorkshopProtocolDrawerOpen = value; }

	private GameObject _workshopProtocolDrawerRoot;

	private RectTransform _workshopProtocolDrawerPopup;

	private Button _workshopProtocolTriggerButton;

	private Text _workshopLocalizationStateText;

	private Text _workshopLocalizationSummaryText;

	private Text _workshopLocalizationApiText;

	private Text _workshopLocalizationProtocolButtonText;

	private GameObject _workshopLocalizationSettingsHost;

	private ScrollRect _workshopLocalizationScrollRect;

	private RectTransform _workshopLocalizationScrollContent;

	private readonly List<string> _workshopLocalizationApiKeyDrafts = new List<string>();

	internal bool IsCapturingShortcut => _shortcutCaptureActive;
	internal bool IsCapturingTextInput
	{
		get
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			InputField input = selected != null ? selected.GetComponent<InputField>() : null;
			return input != null && input.isFocused;
		}
	}
	internal bool IsCapturingKeyboardInput => IsCapturingShortcut || IsCapturingTextInput;

	public CheatMenuOverlayUi(CheatMenuPlugin plugin, PluginUiRoot uiRoot)
	{
		_uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
		_state = new UiStateStore();
		CheatMenuStyle.ApplyAccent(_state.AccentColor);
		PluginUiFeatureAdapter features = new PluginUiFeatureAdapter(plugin);
		_shellPresenter = new ShellPresenter(features);
		_characterPresenter = new CharacterPagePresenter(plugin.Actions);
		_catalogPresenter = new CatalogPagePresenter(features, _state);
		_teleportPresenter = new TeleportPagePresenter(features);
		_worldPresenter = new WorldPagePresenter(features);
		_infiniteStoragePresenter = new InfiniteStoragePagePresenter(features);
		_interactionPresenter = new InteractionToolsPresenter(features);
		_inventoryPresenter = new InventoryToolsPresenter(features);
		_autoPickupPresenter = new AutoPickupToolsPresenter(features);
		_movementPresenter = new MovementToolsPresenter(features);
		_minimapPresenter = new MinimapToolsPresenter(features);
		_translationPresenter = new TranslationToolsPresenter(features);
		_fluentContext = new LegacyFluentUiContext(CheatMenuStyle.Theme, () => _font ?? _shellPresenter.ResolveFont());
		_fluentHandles = new LegacyFluentHandleRegistry();
		_fluentTooltips = new LegacyFluentTooltipBridge(ShowHoverExplanation, HideHoverExplanation);
		_fluentControls = new LegacyFluentControlAdapter(_fluentContext, _fluentHandles, _fluentTooltips);
		_activeTab = ParseMenuTab(plugin.LastMainTab);
		_teleportView = ParseTeleportView(plugin.LastTeleportView);
	}

	public void OnOpened()
	{
		BuildItemResults();
		BuildVehicleResults();
		BuildFavoriteResults();
		_ammoCompatibilityIndex.Rebuild(_catalogPresenter.Items);
		EnsureShell();
		RefreshHeader();
		if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
		_root.SetActive(value: true);
		RefreshWindowGeometry(restoreNormalizedPosition: true);
		ShowTab(_activeTab);
		SetStatus($"自动扫描完成：{_catalogPresenter.Items.Count} 个物品、{_catalogPresenter.Vehicles.Count} 辆载具。", 5f, StatusKind.Info);
	}

	internal void OpenInfiniteStorage()
	{
		_ammoCompatibilityIndex.Rebuild(_catalogPresenter.Items);
		RebuildInfiniteStorageAssetIndex();
		EnsureInfiniteStorageShell();
		_infiniteStorageRoot.SetActive(value: true);
		RefreshInfiniteStorageGeometry();
		RebuildInfiniteStoragePage();
	}

	internal void CloseInfiniteStorage()
	{
		HideInfiniteStorageDecomposeScopeDrawer();
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
		HideInfiniteStorageDecomposeScopeDrawer();
		HideItemFilterPanel();
		HideAutoPickupBlacklistPicker();
		HideAutoPickupRarityPicker();
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
		_ammoCompatibilityIndex.Rebuild(_catalogPresenter.Items);
		RebuildInfiniteStorageAssetIndex();
		if (_root != null && _root.activeSelf && (_activeTab == MenuTab.Items || _activeTab == MenuTab.Vehicles || _activeTab == MenuTab.Favorites))
		{
			RefreshActiveCatalogResultsRegion();
		}
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			RefreshInfiniteStorageContentRegions();
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
				RefreshActiveCatalogResultsRegion();
			}
			else if (_activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
			{
				RefreshWorkshopLocalizationPage();
			}
		}
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			RefreshInfiniteStorageContentRegions();
		}
	}

	public void OnWorkshopLocalizationPresentationChanged()
	{
		if (_root != null && _root.activeSelf && _activeTab == MenuTab.Tools && _activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
	}

	internal void OnVehicleThumbnailSettingsApplied()
	{
		if (!(_root == null) && _root.activeSelf && (_activeTab == MenuTab.Vehicles || (_activeTab == MenuTab.Favorites && _favoriteKind == FavoriteKind.Vehicles)))
		{
			RefreshActiveCatalogGridRegion();
		}
	}

	public void Maintain()
	{
		bool menuActive = _root != null && _root.activeSelf;
		bool infiniteStorageActive = _infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf;
		if (!menuActive && !infiniteStorageActive)
		{
			return;
		}
		_timeApplyDebouncer.Tick();
		_autoPickupRangeDebouncer.Tick();
		_autoPickupFrequencyDebouncer.Tick();
		_minimapSettingsDebouncer.Tick();
		bool cancelConsumedByTextInput = false;
		for (int i = _searchInputs.Count - 1; i >= 0; i--)
		{
			UiSearchInputController searchInput = _searchInputs[i];
			if (!searchInput.IsAlive)
			{
				searchInput.Dispose();
				_searchInputs.RemoveAt(i);
				continue;
			}
			cancelConsumedByTextInput |= searchInput.Pump();
		}
		if (!cancelConsumedByTextInput && !_shortcutCaptureActive && !IsCapturingTextInput && Input.GetKeyDown(KeyCode.Escape))
		{
			if (!_uiRoot.HandleCancel())
			{
				if (infiniteStorageActive)
				{
					_shellPresenter.CloseInfiniteStorage();
				}
				else if (menuActive)
				{
					_shellPresenter.CloseMenu();
				}
			}
		}
		if (menuActive && _activeTab == MenuTab.Other)
		{
			RefreshOtherTime();
		}
		Canvas component = menuActive ? _uiRoot.Canvas : null;
		if (component != null && component.sortingOrder != 29900)
		{
			component.sortingOrder = 29900;
		}
		if (menuActive && _panel != null &&
			(_lastMainLayoutScreenWidth != Screen.width || _lastMainLayoutScreenHeight != Screen.height ||
			Mathf.Abs(_lastMainLayoutRequestedScale - _shellPresenter.UiScale) > 0.0001f))
		{
			RefreshWindowGeometry(restoreNormalizedPosition: true);
		}
		if (infiniteStorageActive && _infiniteStoragePanel != null &&
			(_lastInfiniteLayoutScreenWidth != Screen.width || _lastInfiniteLayoutScreenHeight != Screen.height ||
			Mathf.Abs(_lastInfiniteLayoutRequestedScale - _shellPresenter.UiScale) > 0.0001f))
		{
			RefreshInfiniteStorageGeometry();
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
		}
		if (_activeTab == MenuTab.Teleports && _teleportMapSurface != null && _teleportPresenter.TryGetLocalPlayerPosition(out Vector3 playerPosition))
		{
			_teleportMapSurface.RefreshPlayerMarker(playerPosition);
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

	internal void HandleSceneChanged()
	{
		if (_root != null)
		{
			RebuildShellVisualTree(_root.activeSelf);
		}
		if (_infiniteStorageRoot != null)
		{
			RebuildInfiniteStorageVisualTree(_infiniteStorageRoot.activeSelf);
		}
	}

	private void RefreshWindowGeometry(bool restoreNormalizedPosition)
	{
		if (_panel == null || _root == null)
		{
			return;
		}
		RectTransform bounds = _root.GetComponent<RectTransform>();
		Vector2 boundsSize = bounds != null ? bounds.rect.size : new Vector2(Screen.width, Screen.height);
		Vector2 minimumSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
		float scale = WindowGeometry.ResolveScale(_shellPresenter.UiScale, boundsSize, minimumSize, WindowMargin);
		_panel.sizeDelta = WindowGeometry.ResolveSize(
			boundsSize,
			scale,
			minimumSize,
			new Vector2(PreferredWindowWidth, PreferredWindowHeight),
			WindowMargin);
		_panel.localScale = Vector3.one * scale;
		Canvas.ForceUpdateCanvases();
		RefreshShellLayout();
		Canvas.ForceUpdateCanvases();
		_panel.anchoredPosition = restoreNormalizedPosition
			? WindowGeometry.RestoreNormalized(_shellPresenter.NormalizedWindowPosition, boundsSize, _panel.rect.size, scale)
			: WindowGeometry.Clamp(_panel.anchoredPosition, boundsSize, _panel.rect.size, scale);
		_lastMainLayoutScreenWidth = Screen.width;
		_lastMainLayoutScreenHeight = Screen.height;
		_lastMainLayoutScale = scale;
		_lastMainLayoutRequestedScale = _shellPresenter.UiScale;
		int gridPageSize = GetResponsiveCardColumnCount() * GetResponsiveCardRowCount();
		if (_lastGridPageSize > 0 && gridPageSize != _lastGridPageSize)
		{
			_lastGridPageSize = gridPageSize;
			RefreshActiveCatalogGridRegion();
		}
		else
		{
			_lastGridPageSize = gridPageSize;
		}
	}

	private void RefreshInfiniteStorageGeometry()
	{
		if (_infiniteStoragePanel == null || _infiniteStorageRoot == null)
		{
			return;
		}
		RectTransform bounds = _infiniteStorageRoot.GetComponent<RectTransform>();
		Vector2 boundsSize = bounds != null ? bounds.rect.size : new Vector2(Screen.width, Screen.height);
		Vector2 minimumSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
		float scale = WindowGeometry.ResolveScale(_shellPresenter.UiScale, boundsSize, minimumSize, WindowMargin);
		_infiniteStoragePanel.sizeDelta = WindowGeometry.ResolveSize(
			boundsSize,
			scale,
			minimumSize,
			new Vector2(PreferredInfiniteStorageWindowWidth, PreferredWindowHeight),
			WindowMargin);
		_infiniteStoragePanel.localScale = Vector3.one * scale;
		_lastInfiniteLayoutScreenWidth = Screen.width;
		_lastInfiniteLayoutScreenHeight = Screen.height;
		_lastInfiniteLayoutRequestedScale = _shellPresenter.UiScale;
	}

	private void CommitWindowPosition(Vector2 position)
	{
		if (_panel == null || _root == null)
		{
			return;
		}
		_panel.anchoredPosition = position;
		RefreshWindowGeometry(restoreNormalizedPosition: false);
		RectTransform bounds = _root.GetComponent<RectTransform>();
		Vector2 boundsSize = bounds != null ? bounds.rect.size : new Vector2(Screen.width, Screen.height);
		_shellPresenter.SetWindowPosition(WindowGeometry.Normalize(
			_panel.anchoredPosition,
			boundsSize,
			_panel.rect.size,
			_lastMainLayoutScale));
	}

	public void Dispose()
	{
		_timeApplyDebouncer.Dispose();
		_autoPickupRangeDebouncer.Dispose();
		_autoPickupFrequencyDebouncer.Dispose();
		_minimapSettingsDebouncer.Dispose();
		DisposeSearchInputs();
		PumpDeferredDestroy();
		HideHoverExplanation();
		DisposeInfiniteStorageDecomposeScopeDrawer();
		_pageViewHost?.DetachForAncestorDestroy();
		_pageViewHost = null;
		_toolsPageViewHost?.DetachForAncestorDestroy();
		_toolsPageViewHost = null;
		_fluentHandles.Clear();
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
		}
		if (_infiniteStorageRoot != null)
		{
			UnityEngine.Object.Destroy(_infiniteStorageRoot);
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
		if (_fontPickerRoot != null)
		{
			UnityEngine.Object.Destroy(_fontPickerRoot);
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
		if (_autoPickupRarityPickerRoot != null)
		{
			UnityEngine.Object.Destroy(_autoPickupRarityPickerRoot);
		}
		if (_teleportColorPickerRoot != null)
		{
			UnityEngine.Object.Destroy(_teleportColorPickerRoot);
		}
		_root = null;
		_shellBody = null;
		_narrowShellBody = null;
		_pageRegion = null;
		_navigationViewport = null;
		_navigationContent = null;
		_narrowNavigationContent = null;
		_navigationScroll = null;
		_wideBodyLayout = null;
		_narrowBodyLayout = null;
		_wideNavigationLayout = null;
		_narrowNavigationLayout = null;
		_navigationFitter = null;
		_narrowNavigationFitter = null;
		_wideShellLayout = null;
		_infiniteStorageRoot = null;
		_infiniteStoragePanel = null;
		_infiniteStorageContentHost = null;
		_infiniteStorageContentRow = null;
		_infiniteStorageCategoryHost = null;
		_infiniteStorageResultsHost = null;
		_infiniteStorageFilterButtonText = null;
		_infiniteStorageSummaryText = null;
		_mapAndShortcutText = null;
		_statusText = null;
		_statusAccent = null;
		_teleportMapSurface = null;
		_timeCurrentText = null;
		_timePreviewText = null;
		_timeRing = null;
		_itemAmountInputs.Clear();
		_vehicleAmountInputs.Clear();
		_confirmRoot = null;
		_aboutRoot = null;
		_fontPickerRoot = null;
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
		_autoPickupRarityPickerRoot = null;
		_autoPickupRuleResultsHost = null;
		_workshopLocalizationStateText = null;
		_workshopLocalizationSummaryText = null;
		_workshopLocalizationApiText = null;
		_workshopLocalizationProtocolButtonText = null;
		_workshopLocalizationSettingsHost = null;
		_workshopLocalizationScrollRect = null;
		_workshopLocalizationScrollContent = null;
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
			_font = _shellPresenter.ResolveFont();
			_root = new GameObject("MainMenuWindow", typeof(RectTransform));
			_root.transform.SetParent(_uiRoot.WindowLayer, false);
			RectTransform rootRect = _root.GetComponent<RectTransform>();
			rootRect.anchorMin = Vector2.zero;
			rootRect.anchorMax = Vector2.one;
			rootRect.offsetMin = Vector2.zero;
			rootRect.offsetMax = Vector2.zero;
			GameObject gameObject = CreateObject("Panel", _root.transform, typeof(Image));
			_panel = gameObject.GetComponent<RectTransform>();
			RectTransform panel = _panel;
			Vector2 anchorMin = (_panel.anchorMax = new Vector2(0.5f, 0.5f));
			panel.anchorMin = anchorMin;
			_panel.pivot = new Vector2(0.5f, 0.5f);
			_panel.sizeDelta = Vector2.zero;
			_panel.anchoredPosition = Vector2.zero;
			_panel.localScale = Vector3.one;
			Image panelImage = gameObject.GetComponent<Image>();
			panelImage.color = CheatMenuStyle.Panel;
			_fluentHandles.RegisterScope(gameObject);
			_fluentControls.StyleSurface(gameObject, panelImage, FluentSurfaceRole.Panel, CheatMenuStyle.Theme.RadiusPanel);
			VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.padding = new RectOffset(16, 16, 12, 12);
			verticalLayoutGroup.spacing = CheatMenuStyle.Theme.Space3;
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.childForceExpandHeight = false;
			BuildHeader(gameObject.transform);
			_shellBody = CreateObject("ShellBody", gameObject.transform);
			SetLayout(_shellBody, -1f, -1f, 1f, 1f);
			_wideBodyLayout = _shellBody.AddComponent<HorizontalLayoutGroup>();
			_wideBodyLayout.spacing = CheatMenuStyle.Theme.Space3;
			_wideBodyLayout.childAlignment = TextAnchor.UpperLeft;
			_wideBodyLayout.childControlWidth = true;
			_wideBodyLayout.childControlHeight = true;
			_wideBodyLayout.childForceExpandWidth = false;
			_wideBodyLayout.childForceExpandHeight = true;
			_narrowShellBody = CreateObject("NarrowShellBody", gameObject.transform);
			SetLayout(_narrowShellBody, -1f, -1f, 1f, 1f);
			_narrowBodyLayout = _narrowShellBody.AddComponent<VerticalLayoutGroup>();
			_narrowBodyLayout.spacing = CheatMenuStyle.Theme.Space2;
			_narrowBodyLayout.childAlignment = TextAnchor.UpperLeft;
			_narrowBodyLayout.childControlWidth = true;
			_narrowBodyLayout.childControlHeight = true;
			_narrowBodyLayout.childForceExpandWidth = true;
			_narrowBodyLayout.childForceExpandHeight = false;
			_narrowShellBody.SetActive(value: false);
			BuildTabs(_shellBody.transform);
			_pageRegion = CreateObject("PageRegion", _shellBody.transform);
			SetLayout(_pageRegion, -1f, -1f, 1f, 1f);
			_pageViewHost = new CheatMenuPageViewHost(CreatePageView);
			GameObject gameObject2 = CreateObject("Status", gameObject.transform, typeof(Image));
			Image statusImage = gameObject2.GetComponent<Image>();
			statusImage.color = CheatMenuStyle.SurfaceInset;
			Outline outline = gameObject2.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.StatusBorder;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			SetLayout(gameObject2, -1f, CheatMenuStyle.Theme.ControlDefault, -1f, 0f);
			gameObject2.GetComponent<LayoutElement>().minHeight = CheatMenuStyle.Theme.ControlDefault;
			_fluentControls.StyleSurface(gameObject2, statusImage, FluentSurfaceRole.Muted, CheatMenuStyle.Theme.RadiusField);
			HorizontalLayoutGroup horizontalLayoutGroup = gameObject2.AddComponent<HorizontalLayoutGroup>();
			horizontalLayoutGroup.padding = new RectOffset(12, 12, 0, 0);
			horizontalLayoutGroup.spacing = CheatMenuStyle.Theme.Space3;
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
			CreateButton(gameObject2.transform, "__keyboard_glyph__", ShowShortcutDialog, "查看并修改全局快捷键绑定。", 38f, 34f, CheatMenuStyle.Surface);
			CreateButton(gameObject2.transform, "?", ShowAboutDialog, "查看插件版本、项目主页和支持链接。", 38f, 34f, CheatMenuStyle.Surface, 18);
			ResetStatus();
			RefreshWindowGeometry(restoreNormalizedPosition: true);
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
			_font = _shellPresenter.ResolveFont();
		}
		_infiniteStorageRoot = new GameObject("InfiniteStorageWindow", typeof(RectTransform));
		_infiniteStorageRoot.transform.SetParent(_uiRoot.WindowLayer, false);
		RectTransform storageRect = _infiniteStorageRoot.GetComponent<RectTransform>();
		storageRect.anchorMin = Vector2.zero;
		storageRect.anchorMax = Vector2.one;
		storageRect.offsetMin = Vector2.zero;
		storageRect.offsetMax = Vector2.zero;
		GameObject panel = CreateObject("InfiniteStoragePanel", _infiniteStorageRoot.transform, typeof(Image));
		_infiniteStoragePanel = panel.GetComponent<RectTransform>();
		_infiniteStoragePanel.anchorMin = _infiniteStoragePanel.anchorMax = new Vector2(0.5f, 0.5f);
		_infiniteStoragePanel.pivot = new Vector2(0.5f, 0.5f);
		_infiniteStoragePanel.sizeDelta = Vector2.zero;
		_infiniteStoragePanel.localScale = Vector3.one;
		Image panelImage = panel.GetComponent<Image>();
		panelImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(panel);
		_fluentControls.StyleSurface(panel, panelImage, FluentSurfaceRole.Panel, CheatMenuStyle.Theme.RadiusPanel);
		VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(16, 16, 12, 12);
		layout.spacing = CheatMenuStyle.Theme.Space3;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		GameObject header = CreateRow(panel.transform, 46f, 8f);
		CreateText(header.transform, "无限储存", 22, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(header.transform);
		CreateWindowCloseButton(header.transform, _shellPresenter.CloseInfiniteStorage, "关闭无限储存窗口。");
		_infiniteStorageContentHost = CreateObject("InfiniteStorageContentHost", panel.transform, typeof(Image));
		_fluentHandles.RegisterScope(_infiniteStorageContentHost);
		Image contentImage = _infiniteStorageContentHost.GetComponent<Image>();
		contentImage.color = CheatMenuStyle.Surface;
		SetLayout(_infiniteStorageContentHost, -1f, -1f, -1f, 1f);
		_fluentControls.StyleSurface(_infiniteStorageContentHost, contentImage, FluentSurfaceRole.Surface, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(_infiniteStorageContentHost, CheatMenuStyle.Theme.Space2, new RectOffset(12, 12, 8, 8)).childForceExpandHeight = false;
		_infiniteStorageRoot.SetActive(value: false);
		RefreshInfiniteStorageGeometry();
	}

	private void RebuildInfiniteStoragePage()
	{
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf)
		{
			return;
		}
		HideHoverExplanation();
		HideItemFilterPanel();
		HideInfiniteStorageDecomposeScopeDrawer();
		if (_infiniteStorageContentHost != null)
		{
			QueueForDestroy(_infiniteStorageContentHost);
		}
		_infiniteStorageContentRow = null;
		_infiniteStorageCategoryHost = null;
		_infiniteStorageResultsHost = null;
		_infiniteStorageFilterButtonText = null;
		_infiniteStorageSummaryText = null;
		_infiniteStorageContentHost = CreateObject("InfiniteStorageContentHost", _infiniteStoragePanel, typeof(Image));
		_fluentHandles.RegisterScope(_infiniteStorageContentHost);
		Image contentImage = _infiniteStorageContentHost.GetComponent<Image>();
		contentImage.color = CheatMenuStyle.Surface;
		SetLayout(_infiniteStorageContentHost, -1f, -1f, -1f, 1f);
		_fluentControls.StyleSurface(_infiniteStorageContentHost, contentImage, FluentSurfaceRole.Surface, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(_infiniteStorageContentHost, CheatMenuStyle.Theme.Space2, new RectOffset(12, 12, 8, 8)).childForceExpandHeight = false;
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
		MaintainInfiniteStorageDecomposeScopeDrawer();
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
		GameObject gameObject = CreateRow(parent, 56f, 10f);
		GameObject dragArea = CreateRow(gameObject.transform, 56f, 10f);
		SetLayout(dragArea, -1f, 56f, 1f);
		Image dragSurface = dragArea.AddComponent<Image>();
		dragSurface.color = new Color(0f, 0f, 0f, 0f);
		dragSurface.raycastTarget = true;
		WindowDragHandle dragHandle = dragArea.AddComponent<WindowDragHandle>();
		dragHandle.Initialize(_panel, _root.GetComponent<RectTransform>(), CommitWindowPosition);
		SetLayout(CreateText(dragArea.transform, "单人作弊指令菜单", 25, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold).gameObject, (PluginLocalization.CurrentLanguage == PluginLanguage.English) ? 315f : 255f);
		GameObject gameObject2 = CreateObject("SingleplayerBadge", dragArea.transform, typeof(Image));
		gameObject2.GetComponent<Image>().color = new Color(0.06f, 0.3f, 0.19f, 1f);
		SetLayout(gameObject2, 165f, 28f);
		Stretch(CreateText(gameObject2.transform, "仅限 SINGLEPLAYER", 12, new Color(0.55f, 1f, 0.73f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform);
		CreateFlexibleSpacer(dragArea.transform);
		CreateButton(gameObject.transform, PluginLocalization.SwitchButtonLabel, delegate
		{
			bool persisted = _shellPresenter.ToggleLanguage();
			RebuildForLanguageChange(persisted);
		}, "切换界面语言并立即重建当前菜单。", 58f, 40f, CheatMenuStyle.Accent);
		CreateButton(gameObject.transform, "字体", ShowFontPicker, "选择此菜单使用的界面字体。", 58f, 40f, CheatMenuStyle.Surface);
		CreateAccentPickerButton(gameObject.transform);
		_mapAndShortcutText = CreateText(gameObject.transform, string.Empty, 13, CheatMenuStyle.Muted, TextAnchor.MiddleRight);
		SetLayout(_mapAndShortcutText.gameObject, 220f);
		CreateWindowCloseButton(gameObject.transform, delegate
		{
			_shellPresenter.CloseMenu();
		}, "关闭作弊菜单。");
	}

	private Button CreateWindowCloseButton(Transform parent, System.Action action, string tooltip)
	{
		Button button = CreateButton(parent, "×", action, tooltip, 44f, 36f, CheatMenuStyle.Danger, 23);
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
		RebuildVisualTreesPreservingVisibility();
		SetStatus(PluginLocalization.LanguageChangedMessage(persisted), 5f, persisted ? StatusKind.Success : StatusKind.Warning);
	}

	private void RefreshHeader()
	{
		if (!(_mapAndShortcutText == null))
		{
			string text = (string.IsNullOrWhiteSpace(_shellPresenter.CurrentMap) ? PluginLocalization.Translate("未进入地图") : _shellPresenter.CurrentMap);
			string shortcutLabel = _shellPresenter.ShortcutLabel;
			if (!string.Equals(_lastHeaderMap, text, StringComparison.Ordinal) || !string.Equals(_lastHeaderShortcut, shortcutLabel, StringComparison.Ordinal))
			{
				_lastHeaderMap = text;
				_lastHeaderShortcut = shortcutLabel;
				_mapAndShortcutText.text = text + "  ·  " + shortcutLabel;
			}
		}
	}

	private void BuildPageView(MenuTab tab, GameObject host)
	{
		_contentHost = host;
		BuildPage(tab);
	}

	private UiPageView<MenuTab> CreatePageView(MenuTab tab)
	{
		GameObject host = CreatePageHost(tab);
		BuildPageView(tab, host);
		return new UiPageView<MenuTab>(tab, host, QueueForDestroy);
	}

	private void BuildPage(MenuTab tab)
	{
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

	private GameObject CreatePageHost(MenuTab tab)
	{
		GameObject host = CreateObject(tab + "PageView", _pageRegion.transform, typeof(Image));
		RectTransform hostRect = host.GetComponent<RectTransform>();
		Stretch(hostRect);
		Image hostImage = host.GetComponent<Image>();
		hostImage.color = CheatMenuStyle.Surface;
		_fluentHandles.RegisterScope(host);
		_fluentControls.StyleSurface(host, hostImage, FluentSurfaceRole.Surface, CheatMenuStyle.Theme.RadiusControl);
		return host;
	}

	private void DisposeSearchInputs()
	{
		foreach (UiSearchInputController searchInput in _searchInputs)
		{
			searchInput.Dispose();
		}
		_searchInputs.Clear();
	}






	private void CreateToggleAction(Transform parent, string label, bool current, Action<bool> change, string tooltip = null)
	{
		GameObject gameObject = CreateObject("Toggle", parent, typeof(Image), typeof(Toggle));
		Image component = gameObject.GetComponent<Image>();
		Color baseColor = current ? CheatMenuStyle.Accent : CheatMenuStyle.Surface;
		component.color = baseColor;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.ToggleBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
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
		string localizedLabel = PluginLocalization.Translate(label);
		Text text2 = CreateText(gameObject.transform, localizedLabel + "  " + (current ? "ON" : "OFF"), 13, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold, localize: false);
		SetOffsets(text2.rectTransform, 12f, 64f, 2f, 2f);
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
		component2.isOn = current;
		OverlaySelectableVisual toggleVisual = null;
		_fluentControls.AdaptToggle(gameObject, component2, component, text2, text.gameObject, gameObject2, outline, baseColor, delegate(bool value)
		{
			toggleVisual?.SetBaseColor(value ? CheatMenuStyle.Accent : CheatMenuStyle.Surface);
			text2.text = PluginLocalization.Translate(label) + "  " + (value ? "ON" : "OFF");
			change?.Invoke(value);
		}, tooltip);
		toggleVisual = gameObject.GetComponent<OverlaySelectableVisual>();
		SetLayout(gameObject, -1f, 40f, 1f);
	}

	private void CreateAction(Transform parent, string label, System.Action action, string tooltip)
	{
		SetLayout(CreateButton(parent, label, action, tooltip, 0f, 40f).gameObject, -1f, -1f, 1f);
	}

	private void CreatePrimaryAction(Transform parent, string label, System.Action action, string tooltip)
	{
		SetLayout(CreateButton(parent, label, action, tooltip, 0f, 40f, CheatMenuStyle.Accent).gameObject, -1f, -1f, 1f);
	}

	private void CreateSection(Transform parent, string text)
	{
		GameObject gameObject = CreateRow(parent, CheatMenuStyle.Theme.ControlCompact, CheatMenuStyle.Theme.Space2);
		GameObject gameObject2 = CreateObject("SectionMarker", gameObject.transform, typeof(Image));
		Image component = gameObject2.GetComponent<Image>();
		component.color = CheatMenuStyle.Accent;
		component.raycastTarget = false;
		_fluentControls.StyleSurface(gameObject2, component, FluentSurfaceRole.Accent, CheatMenuStyle.Theme.RadiusField);
		SetLayout(gameObject2, 4f, 18f, 0f);
		SetLayout(CreateText(gameObject.transform, text, 17, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold).gameObject, -1f, -1f, 1f);
	}

	private void CreateSummaryPanel(Transform parent, string text)
	{
		GameObject gameObject = CreateObject("Summary", parent, typeof(Image));
		Image image = gameObject.GetComponent<Image>();
		image.color = CheatMenuStyle.SurfaceRaised;
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.SummaryBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_fluentControls.StyleSurface(gameObject, image, FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		SetLayout(gameObject, -1f, CheatMenuStyle.Theme.ControlDefault);
		SetOffsets(CreateText(gameObject.transform, text, 14, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform, 12f, 12f, 4f, 4f);
	}

	private void CreateEmptyState(Transform parent, string title, string hint)
	{
		GameObject gameObject = CreateObject("EmptyState", parent, typeof(Image));
		Image image = gameObject.GetComponent<Image>();
		image.color = CheatMenuStyle.SurfaceRaised;
		_fluentControls.StyleSurface(gameObject, image, FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(gameObject, CheatMenuStyle.Theme.Space1, new RectOffset(16, 16, 12, 12)).childAlignment = TextAnchor.MiddleCenter;
		SetLayout(gameObject, -1f, 92f, 1f);
		SetLayout(CreateText(gameObject.transform, title, 16, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold).gameObject, -1f, 28f);
		SetLayout(CreateText(gameObject.transform, hint, 12, CheatMenuStyle.Muted, TextAnchor.MiddleCenter).gameObject, -1f, 24f);
	}

	private Text CreateLabel(Transform parent, string text, int size, Color? color = null, float width = 0f, TextAnchor alignment = TextAnchor.MiddleLeft, bool localize = true)
	{
		Text text2 = CreateText(parent, text, size, color ?? CheatMenuStyle.Text, alignment, FontStyle.Normal, localize);
		SetLayout(text2.gameObject, (width > 0f) ? width : (-1f), 27f);
		return text2;
	}

	private void CreateLabeledInput(Transform parent, string label, string value, Action<string> changed, float width = 120f, bool password = false, bool commitOnEndEdit = false, float labelWidth = 0f, string tooltip = null)
	{
		GameObject gameObject = CreateRow(parent, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
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
		_fluentControls.AdaptInput(gameObject, component2, component, text, gameObject2, outline, changed, commitOnEndEdit, hoverExplanation);
		SetLayout(gameObject, (width > 0f) ? width : (-1f), 34f, flexibleWidth);
		return component2;
	}

	private InputField CreateSearchInput(Transform parent, string value, Action<string> changed, float width = 0f, float flexibleWidth = 0f, string hoverExplanation = null)
	{
		InputField input = CreateInput(parent, value, null, width, flexibleWidth, password: false, commitOnEndEdit: false, hoverExplanation: hoverExplanation);
		UiSearchInputController controller = new UiSearchInputController(input, changed);
		_searchInputs.Add(controller);
		_fluentControls.TrackLifetime(input.gameObject, controller);
		return input;
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
		SetLayout(gameObject, 170f, CheatMenuStyle.Theme.ControlCompact, 0f, 0f);
		GameObject gameObject2 = CreateObject("FillArea", gameObject.transform, typeof(Image));
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		Image trackImage = gameObject2.GetComponent<Image>();
		trackImage.color = CheatMenuStyle.SliderTrack;
		Outline outline2 = gameObject2.AddComponent<Outline>();
		outline2.effectColor = CheatMenuStyle.InputBorder;
		outline2.effectDistance = Vector2.one;
		outline2.useGraphicAlpha = false;
		component2.anchorMin = new Vector2(0f, 0.34f);
		component2.anchorMax = new Vector2(1f, 0.66f);
		component2.offsetMin = new Vector2(7f, 0f);
		component2.offsetMax = new Vector2(-7f, 0f);
		GameObject gameObject3 = CreateObject("Fill", gameObject2.transform, typeof(Image));
		Image fillImage = gameObject3.GetComponent<Image>();
		fillImage.color = CheatMenuStyle.Accent;
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
		_fluentControls.AdaptSlider(gameObject, component, gameObject.GetComponent<Image>(), trackImage, fillImage, changed);
		return component;
	}

	private void RefreshOtherTime()
	{
		WorldPageSnapshot snapshot = _worldPresenter.CaptureSnapshot();
		uint num = snapshot.Cycle;
		float num2 = Mathf.Clamp01(snapshot.NormalizedTime);
		if (_timeCurrentText != null)
		{
			_timeCurrentText.text = PluginLocalization.Translate($"当前时间：{snapshot.Time:N0} / {num:N0}（{num2:P0}）");
		}
		if (_timeRing != null && !_timeRing.IsInteracting && !_timeApplyDebouncer.HasPending)
		{
			_timeRing.SetTime(WorldTimeClockMapping.GameNormalizedToClockHours(num2), notify: false);
			UpdateTimePreview(num2, num);
		}
	}

	private void UpdateTimePreview(float normalized, uint cycle)
	{
		if (_timePreviewText != null)
		{
			_timePreviewText.text = PluginLocalization.Translate($"预览：{Mathf.RoundToInt(Mathf.Clamp01(normalized) * 100f)}%");
		}
	}

	private void ApplyTimeNormalized(float normalized, uint cycle)
	{
		normalized = Mathf.Repeat(normalized, 1f);
		int maximum = cycle > 0u ? Mathf.Max(0, (int)cycle - 1) : 0;
		uint targetTime = (uint)Mathf.Clamp(Mathf.RoundToInt(cycle * normalized), 0, maximum);
		_worldPresenter.SetTime(targetTime);
		if (_timeRing != null)
		{
			_timeRing.SetTime(WorldTimeClockMapping.GameNormalizedToClockHours(normalized), notify: false);
		}
		int percent = Mathf.Clamp(Mathf.RoundToInt(normalized * 100f), 0, 99);
		UpdateTimePreview(normalized, cycle);
		SetStatus($"时间已设置为周期的 {percent}% 。");
	}

	private void UpdateItemAmountInput()
	{
		for (int i = _itemAmountInputs.Count - 1; i >= 0; i--)
		{
			InputField input = _itemAmountInputs[i];
			if (input == null)
			{
				_itemAmountInputs.RemoveAt(i);
			}
			else
			{
				input.SetTextWithoutNotify(_itemAmountText);
			}
		}
	}

	private void UpdateVehicleAmountInput()
	{
		for (int i = _vehicleAmountInputs.Count - 1; i >= 0; i--)
		{
			InputField input = _vehicleAmountInputs[i];
			if (input == null)
			{
				_vehicleAmountInputs.RemoveAt(i);
			}
			else
			{
				input.SetTextWithoutNotify(_vehicleAmountText);
			}
		}
	}

	private InputField RegisterItemAmountInput(InputField input)
	{
		if (input != null)
		{
			_itemAmountInputs.Add(input);
		}
		return input;
	}

	private InputField RegisterVehicleAmountInput(InputField input)
	{
		if (input != null)
		{
			_vehicleAmountInputs.Add(input);
		}
		return input;
	}

	private Button CreateMarkerPickerButton(Transform parent, TeleportMarkerKind markerKind, System.Action action, float width, bool compact, float flexibleWidth = 0f)
	{
		Color value = ((_teleportMarkerKind == markerKind) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover);
		float num = (compact ? 34f : 54f);
		string tooltip = string.Format(PluginLocalization.Translate("选择“{0}”传送标记样式。"), PluginLocalization.Translate(MarkerLabel(markerKind)));
		Button button = CreateButton(parent, string.Empty, action, tooltip, width, num, value, 11);
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
		Button button = CreateButton(parent, string.Empty, ShowTeleportColorPicker, "打开传送标记颜色选择器。", 82f, 34f, CheatMenuStyle.SurfaceHover, 11);
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
		VehicleThumbnailRenderSettings vehicleThumbnailSettings = _catalogPresenter.VehicleThumbnailSettings;
		_vehicleThumbnailDraftWidth = vehicleThumbnailSettings?.Width ?? 128;
		_vehicleThumbnailDraftFraming = vehicleThumbnailSettings?.Framing ?? 1f;
		_vehicleThumbnailSettingsRoot = CreateObject("VehicleThumbnailSettingsOverlay", _uiRoot.ModalLayer, typeof(Image));
		_fluentHandles.RegisterScope(_vehicleThumbnailSettingsRoot);
		_uiRoot.RegisterModal(_vehicleThumbnailSettingsRoot, HideVehicleThumbnailSettings);
		Stretch(_vehicleThumbnailSettingsRoot.GetComponent<RectTransform>());
		_vehicleThumbnailSettingsRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("VehicleThumbnailSettingsDialog", _vehicleThumbnailSettingsRoot.transform, typeof(Image));
		Image dialogImage = gameObject.GetComponent<Image>();
		dialogImage.color = CheatMenuStyle.Panel;
		_fluentControls.StyleSurface(gameObject, dialogImage, FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusPanel);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(680f, 560f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(24, 24, 16, 16);
		verticalLayoutGroup.spacing = 12f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject2 = CreateRow(gameObject.transform, 44f, 10f);
		CreateText(gameObject2.transform, "载具渲染设置", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "×", HideVehicleThumbnailSettings, "关闭载具缩略图设置且不保存本次更改。", 44f, 42f, CheatMenuStyle.Surface, 23);
		CreateLabel(gameObject.transform, "缩略图尺寸只影响车辆卡片，不会修改游戏全局画质。已生成的缩略图会进入缓存，重复打开不会重复实例化车辆。", 12, CheatMenuStyle.Muted);
		CreateLabel(gameObject.transform, "尺寸", 15, CheatMenuStyle.Text);
		GameObject gameObject3 = CreateRow(gameObject.transform, 38f, 8f);
		List<Button> resolutionButtons = new List<Button>();
		int[] resolutions = new int[3] { 128, 192, 256 };
		string[] array = new string[3] { "128 × 96（低开销）", "192 × 144（平衡）", "256 × 192（更清晰）" };
		string[] resolutionTooltips = new string[3]
		{
			"选择 128 × 96 缩略图：生成最快、缓存占用最低。",
			"选择 192 × 144 缩略图：在清晰度与生成开销之间平衡。",
			"选择 256 × 192 缩略图：更清晰，但首次生成更慢。"
		};
		for (int i = 0; i < resolutions.Length; i++)
		{
			int capturedResolution = resolutions[i];
			Button button = CreateButton(gameObject3.transform, array[i], delegate
			{
				_vehicleThumbnailDraftWidth = capturedResolution;
				for (int j = 0; j < resolutionButtons.Count; j++)
				{
					bool flag = resolutions[j] == _vehicleThumbnailDraftWidth;
					Color selectionColor = flag ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover;
					OverlaySelectableVisual selectionVisual = resolutionButtons[j].GetComponent<OverlaySelectableVisual>();
					if (selectionVisual != null)
					{
						selectionVisual.SetBaseColor(selectionColor);
					}
				}
			}, resolutionTooltips[i], 0f, 34f, (capturedResolution == _vehicleThumbnailDraftWidth) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12);
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
		CreateButton(gameObject5.transform, "取消", HideVehicleThumbnailSettings, "放弃本次缩略图尺寸和取景倍率更改。", 96f);
		CreateButton(gameObject5.transform, "应用", delegate
		{
			bool flag = _catalogPresenter.ApplyVehicleThumbnailSettings(_vehicleThumbnailDraftWidth, _vehicleThumbnailDraftFraming);
			HideVehicleThumbnailSettings();
			SetStatus(flag ? "车辆缩略图设置已应用并保存。" : "车辆缩略图设置已应用，但配置文件保存失败。", 5f, flag ? StatusKind.Success : StatusKind.Warning);
		}, "保存缩略图尺寸和取景倍率；后续车辆卡片使用新设置。", 96f, 36f, CheatMenuStyle.Accent);
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
		ShowColorPicker(ColorPickerPurpose.TeleportMarker);
	}

	private void ShowColorPicker(ColorPickerPurpose purpose)
	{
		HideTeleportColorPicker();
		_colorPickerPurpose = purpose;
		bool accentPicker = purpose == ColorPickerPurpose.Accent;
		_teleportColorDraft = accentPicker ? _accentColor : _teleportMarkerColor;
		_teleportColorPickerRoot = CreateObject("SharedColorPickerOverlay", _uiRoot.ModalLayer, typeof(Image));
		_fluentHandles.RegisterScope(_teleportColorPickerRoot);
		_uiRoot.RegisterModal(_teleportColorPickerRoot, HideTeleportColorPicker);
		Stretch(_teleportColorPickerRoot.GetComponent<RectTransform>());
		_teleportColorPickerRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("SharedColorPickerDialog", _teleportColorPickerRoot.transform, typeof(Image));
		Image dialogImage = gameObject.GetComponent<Image>();
		dialogImage.color = CheatMenuStyle.Panel;
		_fluentControls.StyleSurface(gameObject, dialogImage, FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusPanel);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(780f, 620f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(24, 24, 16, 16);
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
		string pickerTitle = accentPicker
			? (PluginLocalization.CurrentLanguage == PluginLanguage.English ? "Accent Color" : "强调色")
			: PluginLocalization.Translate("选择颜色");
		CreateText(gameObject2.transform, pickerTitle, 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold, localize: false);
		CreateFlexibleSpacer(gameObject2.transform);
		string closePickerTooltip = accentPicker && PluginLocalization.CurrentLanguage == PluginLanguage.English
			? "Close the accent picker and discard this draft."
			: accentPicker ? "关闭强调色选择器并放弃本次更改。" : "关闭颜色选择器并放弃本次颜色更改。";
		CreateButton(gameObject2.transform, "×", HideTeleportColorPicker, closePickerTooltip, 44f, 42f, CheatMenuStyle.Surface, 23);
		GameObject gameObject4 = CreateRow(gameObject.transform, 0f, 26f);
		SetLayout(gameObject4, -1f, -1f, -1f, 1f);
		GameObject gameObject5 = CreateObject("PreviewColumn", gameObject4.transform, typeof(Image));
		gameObject5.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
		AddVerticalLayout(gameObject5, 8f, new RectOffset(0, 0, 0, 0));
		SetLayout(gameObject5, 326f, -1f, 0f, 1f);
		CreateLabel(gameObject5.transform, "预览", 16, CheatMenuStyle.Text);
		if (accentPicker)
		{
			CreateLabel(gameObject5.transform,
				PluginLocalization.CurrentLanguage == PluginLanguage.English
					? "Session only · teleport marker color stays independent"
					: "仅本次游戏会话生效 · 不影响传送点颜色",
				12, CheatMenuStyle.Muted);
		}
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
		gridLayoutGroup.cellSize = new Vector2(48f, CheatMenuStyle.Theme.ControlDefault);
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
		SetLayout(_teleportColorHexInput.gameObject, -1f, CheatMenuStyle.Theme.ControlDefault, 1f);
		_fluentControls.TrackInputEndEdit(_teleportColorHexInput.gameObject, _teleportColorHexInput, TryUpdateTeleportDraftFromHex);
		CreateLabel(gameObject9.transform, "通道", 16, CheatMenuStyle.Text);
		GameObject gameObject10 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject10.transform, "红", 13, CheatMenuStyle.Text, CheatMenuStyle.ColorChannelLabelWidth);
		_teleportColorRedSlider = CreateColorChannelSlider(gameObject10.transform, _teleportColorDraft.r * 255f, new Color(0.75f, 0.24f, 0.24f, 1f), delegate(int value)
		{
			_state.TeleportColorDraft.r = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorRedValue);
		GameObject gameObject11 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject11.transform, "绿", 13, CheatMenuStyle.Text, CheatMenuStyle.ColorChannelLabelWidth);
		_teleportColorGreenSlider = CreateColorChannelSlider(gameObject11.transform, _teleportColorDraft.g * 255f, new Color(0.25f, 0.68f, 0.3f, 1f), delegate(int value)
		{
			_state.TeleportColorDraft.g = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorGreenValue);
		GameObject gameObject12 = CreateRow(gameObject9.transform, 36f, 8f);
		CreateLabel(gameObject12.transform, "蓝", 13, CheatMenuStyle.Text, CheatMenuStyle.ColorChannelLabelWidth);
		_teleportColorBlueSlider = CreateColorChannelSlider(gameObject12.transform, _teleportColorDraft.b * 255f, new Color(0.22f, 0.5f, 0.78f, 1f), delegate(int value)
		{
			_state.TeleportColorDraft.b = (float)value / 255f;
			UpdateTeleportColorPickerControls();
		}, out _teleportColorBlueValue);
		GameObject gameObject13 = CreateRow(gameObject.transform, 48f, 10f);
		if (accentPicker)
		{
			CreateButton(gameObject13.transform,
				PluginLocalization.CurrentLanguage == PluginLanguage.English ? "Default Blue" : "默认蓝色",
				delegate
				{
					_teleportColorDraft = CheatMenuStyle.DefaultAccent;
					UpdateTeleportColorPickerControls();
				},
				PluginLocalization.CurrentLanguage == PluginLanguage.English
					? "Restore the Fluent blue accent draft."
					: "把强调色草稿恢复为默认 Fluent 蓝。",
				120f, 38f, CheatMenuStyle.Surface);
		}
		CreateFlexibleSpacer(gameObject13.transform);
		string cancelPickerTooltip = accentPicker && PluginLocalization.CurrentLanguage == PluginLanguage.English
			? "Discard this session accent draft."
			: accentPicker ? "放弃本次强调色更改。" : "放弃本次传送标记颜色更改。";
		CreateButton(gameObject13.transform, "取消", HideTeleportColorPicker, cancelPickerTooltip, 110f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject13.transform, "确定", delegate
		{
			if (accentPicker)
			{
				ApplyAccentColorDraft();
			}
			else
			{
				ApplyTeleportMarkerColorDraft();
			}
		}, accentPicker
			? (PluginLocalization.CurrentLanguage == PluginLanguage.English
				? "Apply this color to accent, focus, navigation selection, and sliders for this session."
				: "把当前颜色用于本次会话的强调、焦点、导航选中和滑块。")
			: "将当前预览颜色用于传送标记。", 110f, 38f, CheatMenuStyle.Accent);
		UpdateTeleportColorPickerControls();
		if (EventSystem.current != null && _teleportColorHexInput != null)
		{
			EventSystem.current.SetSelectedGameObject(_teleportColorHexInput.gameObject);
		}
	}

	private void HideTeleportColorPicker()
	{
		GameObject pickerRoot = _teleportColorPickerRoot;
		if (pickerRoot != null)
		{
			if (EventSystem.current != null && SelectionBelongsToRoot(EventSystem.current.currentSelectedGameObject, pickerRoot))
			{
				EventSystem.current.SetSelectedGameObject(null);
			}
			QueueForDestroy(pickerRoot);
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
		string tooltip = _colorPickerPurpose == ColorPickerPurpose.Accent
			? (PluginLocalization.CurrentLanguage == PluginLanguage.English
				? "Load this preset into the session accent preview."
				: "把此预设颜色载入强调色预览。")
			: "将此预设颜色载入当前传送标记颜色预览。";
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_teleportColorDraft = color;
			UpdateTeleportColorPickerControls();
		}, tooltip, 48f, 42f, CheatMenuStyle.SurfaceHover, 10);
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
		SetLayout(gameObject, -1f, CheatMenuStyle.Theme.ControlCompact, 1f);
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
		SetLayout(valueText.gameObject, 30f, CheatMenuStyle.Theme.ControlCompact);
		_fluentControls.AdaptSlider(gameObject, component2, component, component3, component5, delegate(float channelValue)
		{
			changed?.Invoke(Mathf.RoundToInt(channelValue));
		});
		component5.color = fillColor;
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

	private void EnsureHoverExplanation()
	{
		GameObject activeRoot = GetActiveOverlayRoot();
		if (activeRoot == null)
		{
			return;
		}
		if (_hoverExplanationRoot != null && _hoverExplanationRoot.transform.parent != _uiRoot.TooltipLayer)
		{
			QueueForDestroy(_hoverExplanationRoot);
			_hoverExplanationRoot = null;
			_hoverExplanationText = null;
		}
		if (!(_hoverExplanationRoot != null))
		{
			_hoverExplanationRoot = CreateObject("OverlayHoverExplanation", _uiRoot.TooltipLayer, typeof(Image));
			_fluentHandles.RegisterScope(_hoverExplanationRoot);
			_uiRoot.AttachTooltip(_hoverExplanationRoot);
			RectTransform component = _hoverExplanationRoot.GetComponent<RectTransform>();
			Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
			component.anchorMin = anchorMin;
			component.pivot = new Vector2(0.5f, 0.5f);
			Image component2 = _hoverExplanationRoot.GetComponent<Image>();
			component2.color = CheatMenuStyle.SurfaceRaised;
			component2.raycastTarget = false;
			_fluentControls.StyleTooltipSurface(_hoverExplanationRoot, component2, CheatMenuStyle.Theme.RadiusField);
			_hoverExplanationText = CreateText(_hoverExplanationRoot.transform, string.Empty, 12, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Normal, localize: false);
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
		Canvas component = _uiRoot.Canvas;
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

	private Button CreateButton(Transform parent, string label, System.Action action, string tooltip, float width = 0f, float height = 36f, Color? color = null, int fontSize = 13, bool delayTooltip = false)
	{
		height = CheatMenuStyle.Theme.NormalizeControlHeight(height);
		GameObject gameObject = CreateObject("Button", parent, typeof(Image), typeof(Button));
		Image component = gameObject.GetComponent<Image>();
		Color baseColor = color ?? CheatMenuStyle.Surface;
		component.color = baseColor;
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
		tooltip = ButtonTooltipText.Require(tooltip);
		_fluentControls.AdaptButton(gameObject, component2, component, text, gameObject3, outline, baseColor, action, tooltip, delayTooltip);
		SetLayout(gameObject, (width > 0f) ? width : (-1f), height);
		return component2;
	}

	private Text CreateText(Transform parent, string value, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal, bool localize = true)
	{
		Text component = CreateObject("Text", parent, typeof(Text)).GetComponent<Text>();
		string resolvedText = localize ? PluginLocalization.Translate(value) : value;
		component.font = _font;
		component.text = resolvedText;
		component.fontSize = size;
		component.fontStyle = style;
		component.color = color;
		component.alignment = alignment;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Truncate;
		component.raycastTarget = false;
		return component;
	}

	private GameObject CreateRow(Transform parent, float height, float spacing)
	{
		height = CheatMenuStyle.Theme.NormalizeControlHeight(height);
		spacing = CheatMenuStyle.Theme.NormalizeSpacing(spacing);
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
		verticalLayoutGroup.padding = CheatMenuStyle.Theme.NormalizePadding(padding);
		verticalLayoutGroup.spacing = CheatMenuStyle.Theme.NormalizeSpacing(spacing);
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
			_fluentHandles.DisposeSubtree(target);
			_uiRoot.UnregisterLayerSurface(target);
			_deferredDestroy.Enqueue(target, Time.frameCount, item => item.SetActive(value: false));
		}
	}

	internal void PumpDeferredDestroy()
	{
		bool canvasRebuilding = CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics();
		while (_deferredDestroy.TryDequeueReady(Time.frameCount, canvasRebuilding, out GameObject item))
		{
			if (item != null)
			{
				item.SetActive(value: false);
				UnityEngine.Object.Destroy(item);
			}
		}
	}

	private void BuildItemResults()
	{
		_catalogPresenter.ProjectItems();
	}

	private void BuildVehicleResults()
	{
		_catalogPresenter.ProjectVehicles();
	}

	private void BuildFavoriteResults()
	{
		_catalogPresenter.ProjectFavorites();
	}

	private bool MatchesItemForFilter(ItemAsset asset, ItemFilterState filter)
	{
		return _catalogPresenter.MatchesItem(asset, filter);
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
		if (statusKind != StatusKind.Info)
		{
			_uiRoot.Toasts?.Show(PluginLocalization.Translate(message), seconds, GetStatusColor(statusKind));
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





	private const int AutoPickupCardPageSize = 10;

	private const int AutoPickupPickerPageSize = 15;



}
