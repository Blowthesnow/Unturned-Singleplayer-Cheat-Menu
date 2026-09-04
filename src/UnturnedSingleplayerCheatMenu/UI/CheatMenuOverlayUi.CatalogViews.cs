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
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;
using InputField = UnityEngine.UI.InputField;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private void BuildItemsTab()
	{
		AddVerticalLayout(_contentHost, CheatMenuStyle.Theme.Space2, new RectOffset(12, 12, 8, 8));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		CreateLabel(gameObject.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		CreateSearchInput(gameObject.transform, _itemFilter.SearchQuery, delegate(string value)
		{
			_itemFilter.SearchQuery = value;
			RefreshItemResultsRegion();
		}, 280f);
		CreateLabel(gameObject.transform, "数量", 13, CheatMenuStyle.Text, CheatMenuStyle.ToolbarFieldLabelWidth);
		CreateButton(gameObject.transform, "−", delegate
		{
			_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "将每次给予的物品数量减少 1，最低为 1。", 36f, 34f);
		RegisterItemAmountInput(CreateInput(gameObject.transform, _itemAmountText, delegate(string value)
		{
			_itemAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
			}
		}, 62f));
		CreateButton(gameObject.transform, "+", delegate
		{
			_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "将每次给予的物品数量增加 1，最高为 255。", 36f, 34f);
		CreateButton(gameObject.transform, (_itemFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_itemFilter.ActiveFilterCount}", delegate
		{
			ShowItemFilterPanel(forFavorites: false);
		}, "打开物品类型、来源、稀有度和装备槽位筛选面板。", 92f, 34f);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "重新扫描", delegate
		{
			_catalogPresenter.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, "重新读取当前已加载的原版、地图和 Workshop 物品资产。", 105f, 34f);
		_itemSummaryHost = CreateObject("ItemSummary", _contentHost.transform);
		AddVerticalLayout(_itemSummaryHost, 4f, new RectOffset(0, 0, 0, 0));
		CreateItemFilterSummary(_itemSummaryHost.transform, _itemFilter, _itemResults.Count, _catalogPresenter.Items.Count);
		_itemResultsHost = CreateRow(_contentHost.transform, 0f, 10f);
		GameObject gameObject2 = _itemResultsHost;
		SetLayout(gameObject2, -1f, -1f, -1f, 1f);
		BuildItemCategorySidebar(gameObject2.transform, "物品分类", _itemFilter, delegate(ItemPrimaryCategory category)
		{
			_itemFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_itemFilter);
			RefreshItemResultsRegion();
		});
		BuildItemGrid(gameObject2.transform);
	}

	private void BuildVehiclesTab()
	{
		AddVerticalLayout(_contentHost, CheatMenuStyle.Theme.Space2, new RectOffset(12, 12, 8, 8));
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 7f);
		CreateLabel(gameObject.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		CreateSearchInput(gameObject.transform, _vehicleQuery, delegate(string value)
		{
			_vehicleQuery = value;
			RefreshVehicleResultsRegion();
		}, 280f);
		CreateLabel(gameObject.transform, "数量", 13, CheatMenuStyle.Text, CheatMenuStyle.ToolbarFieldLabelWidth);
		CreateButton(gameObject.transform, "−", delegate
		{
			_spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			UpdateVehicleAmountInput();
		}, "将每次生成的载具数量减少 1，最低为 1。", 36f, 34f);
		RegisterVehicleAmountInput(CreateInput(gameObject.transform, _vehicleAmountText, delegate(string value)
		{
			_vehicleAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_spawnVehicleAmount = Mathf.Clamp(result, 1, 20);
			}
		}, 62f));
		CreateButton(gameObject.transform, "+", delegate
		{
			_spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			UpdateVehicleAmountInput();
		}, "将每次生成的载具数量增加 1，最高为 20。", 36f, 34f);
		CreateButton(gameObject.transform, "⚙ 渲染设置", ShowVehicleThumbnailSettings, "设置车辆卡片缩略图的尺寸和取景倍率。", 116f, 34f);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "重新扫描", delegate
		{
			_catalogPresenter.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, "重新读取当前已加载的原版、地图和 Workshop 载具资产。", 105f, 34f);
		_vehicleSummaryText = CreateLabel(_contentHost.transform, string.Empty, 12, CheatMenuStyle.Muted);
		RefreshVehicleSummary();
		_vehicleResultsHost = CreateRow(_contentHost.transform, 0f, 10f);
		GameObject gameObject2 = _vehicleResultsHost;
		SetLayout(gameObject2, -1f, -1f, -1f, 1f);
		BuildCategorySidebar(gameObject2.transform, "车辆分类", VehicleCategories, _vehicleCategory, delegate(string category)
		{
			_vehicleCategory = category;
			RefreshVehicleResultsRegion();
		});
		BuildVehicleGrid(gameObject2.transform);
	}

	private void BuildFavoritesTab()
	{
		AddVerticalLayout(_contentHost, CheatMenuStyle.Theme.Space2, new RectOffset(12, 12, 8, 8));
		int num = _catalogPresenter.Items.Count(_catalogPresenter.IsItemFavorite);
		int num2 = _catalogPresenter.Vehicles.Count(_catalogPresenter.IsVehicleFavorite);
		GameObject gameObject = CreateRow(_contentHost.transform, 38f, 8f);
		_favoriteItemsButton = CreateButton(gameObject.transform, string.Format(PluginLocalization.Translate("物品收藏  {0}"), num), delegate
		{
			SwitchFavoriteKind(FavoriteKind.Items);
		}, "切换到已收藏物品列表。", 0f, 36f, (_favoriteKind == FavoriteKind.Items) ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 14);
		_favoriteVehiclesButton = CreateButton(gameObject.transform, string.Format(PluginLocalization.Translate("车辆收藏  {0}"), num2), delegate
		{
			SwitchFavoriteKind(FavoriteKind.Vehicles);
		}, "切换到已收藏载具列表。", 0f, 36f, (_favoriteKind == FavoriteKind.Vehicles) ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 14);
		foreach (Transform item in gameObject.transform)
		{
			SetLayout(item.gameObject, -1f, -1f, 1f);
		}
		_favoriteBodyHost = CreateObject("FavoriteBody", _contentHost.transform);
		AddVerticalLayout(_favoriteBodyHost, 8f, new RectOffset(0, 0, 0, 0));
		SetLayout(_favoriteBodyHost, -1f, -1f, -1f, 1f);
		BuildFavoriteBody(num);
	}

	private void BuildFavoriteBody(int itemFavoriteCount)
	{
		GameObject gameObject2 = CreateRow(_favoriteBodyHost.transform, 38f, 7f);
		CreateLabel(gameObject2.transform, "搜索", 13, CheatMenuStyle.Text, 48f);
		string value = ((_favoriteKind == FavoriteKind.Items) ? _favoriteItemFilter.SearchQuery : _favoriteQuery);
		CreateSearchInput(gameObject2.transform, value, delegate(string text)
		{
			if (_favoriteKind == FavoriteKind.Items)
			{
				_favoriteItemFilter.SearchQuery = text;
			}
			else
			{
				_favoriteQuery = text;
			}
			RefreshFavoriteResultsRegion();
		}, 280f);
		CreateLabel(gameObject2.transform, "数量", 13, CheatMenuStyle.Text, CheatMenuStyle.ToolbarFieldLabelWidth);
		if (_favoriteKind == FavoriteKind.Items)
		{
			CreateButton(gameObject2.transform, "−", delegate
			{
				_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
				_itemAmountText = _giveAmount.ToString();
				UpdateItemAmountInput();
			}, "将收藏物品每次给予的数量减少 1，最低为 1。", 36f, 34f);
			RegisterItemAmountInput(CreateInput(gameObject2.transform, _itemAmountText, delegate(string text)
			{
				_itemAmountText = text;
				if (int.TryParse(text, out var result))
				{
					_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
				}
			}, 62f));
			CreateButton(gameObject2.transform, "+", delegate
			{
				_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
				_itemAmountText = _giveAmount.ToString();
				UpdateItemAmountInput();
			}, "将收藏物品每次给予的数量增加 1，最高为 255。", 36f, 34f);
			CreateButton(gameObject2.transform, (_favoriteItemFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_favoriteItemFilter.ActiveFilterCount}", delegate
			{
				ShowItemFilterPanel(forFavorites: true);
			}, "打开收藏物品的类型、来源、稀有度和装备槽位筛选面板。", 92f, 34f);
		}
		else
		{
			CreateButton(gameObject2.transform, "−", delegate
			{
				_spawnVehicleAmount = Math.Max(1, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) - 1);
				_vehicleAmountText = _spawnVehicleAmount.ToString();
				UpdateVehicleAmountInput();
			}, "将收藏载具每次生成的数量减少 1，最低为 1。", 36f, 34f);
			RegisterVehicleAmountInput(CreateInput(gameObject2.transform, _vehicleAmountText, delegate(string text)
			{
				_vehicleAmountText = text;
				if (int.TryParse(text, out var result))
				{
					_spawnVehicleAmount = Mathf.Clamp(result, 1, 20);
				}
			}, 62f));
			CreateButton(gameObject2.transform, "+", delegate
			{
				_spawnVehicleAmount = Math.Min(20, ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount) + 1);
				_vehicleAmountText = _spawnVehicleAmount.ToString();
				UpdateVehicleAmountInput();
			}, "将收藏载具每次生成的数量增加 1，最高为 20。", 36f, 34f);
		}
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "重新扫描", delegate
		{
			_catalogPresenter.RefreshCatalog();
			SetStatus("已重新扫描当前加载的原版与模组资产。", 4f);
		}, "重新读取当前已加载资产，并刷新已收藏物品和载具。", 105f, 34f);
		_favoriteSummaryHost = CreateObject("FavoriteSummary", _favoriteBodyHost.transform);
		AddVerticalLayout(_favoriteSummaryHost, 4f, new RectOffset(0, 0, 0, 0));
		if (_favoriteKind == FavoriteKind.Items)
		{
			CreateItemFilterSummary(_favoriteSummaryHost.transform, _favoriteItemFilter, _favoriteItemResults.Count, itemFavoriteCount);
		}
		else
		{
			CreateLabel(_favoriteSummaryHost.transform, $"显示 {_favoriteVehicleResults.Count} 辆已加载收藏车辆。", 12, CheatMenuStyle.Muted);
		}
		_favoriteResultsHost = CreateRow(_favoriteBodyHost.transform, 0f, 10f);
		GameObject gameObject3 = _favoriteResultsHost;
		SetLayout(gameObject3, -1f, -1f, -1f, 1f);
		if (_favoriteKind == FavoriteKind.Items)
		{
			BuildItemCategorySidebar(gameObject3.transform, "收藏物品分类", _favoriteItemFilter, delegate(ItemPrimaryCategory category)
			{
				_favoriteItemFilter.Category = category;
				ItemFilterService.NormalizeForCategory(_favoriteItemFilter);
				RefreshFavoriteResultsRegion();
			}, _catalogPresenter.IsItemFavorite);
		}
		else
		{
			BuildCategorySidebar(gameObject3.transform, "收藏车辆分类", VehicleCategories, _favoriteVehicleCategory, delegate(string category)
			{
				_favoriteVehicleCategory = category;
				RefreshFavoriteResultsRegion();
			});
		}
		BuildFavoriteGrid(gameObject3.transform);
	}

	private void SwitchFavoriteKind(FavoriteKind kind)
	{
		if (_favoriteKind == kind || _favoriteBodyHost == null)
		{
			return;
		}
		_favoriteKind = kind;
		_favoritePage = 0;
		BuildFavoriteResults();
		ClearResultHost(_favoriteBodyHost);
		_favoriteResultsHost = null;
		_favoriteGridHost = null;
		int itemFavoriteCount = _catalogPresenter.Items.Count(_catalogPresenter.IsItemFavorite);
		BuildFavoriteBody(itemFavoriteCount);
		RefreshFavoriteKindButtons(itemFavoriteCount, _catalogPresenter.Vehicles.Count(_catalogPresenter.IsVehicleFavorite));
	}

	private void RefreshFavoriteKindButtons(int itemCount, int vehicleCount)
	{
		SetButtonTextAndColor(_favoriteItemsButton, string.Format(PluginLocalization.Translate("物品收藏  {0}"), itemCount), _favoriteKind == FavoriteKind.Items ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover);
		SetButtonTextAndColor(_favoriteVehiclesButton, string.Format(PluginLocalization.Translate("车辆收藏  {0}"), vehicleCount), _favoriteKind == FavoriteKind.Vehicles ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover);
	}

	private static void SetButtonTextAndColor(Button button, string label, Color color)
	{
		if (button == null)
		{
			return;
		}
		Text text = button.GetComponentInChildren<Text>(includeInactive: true);
		if (text != null)
		{
			text.text = PluginLocalization.Translate(label);
		}
		OverlaySelectableVisual visual = button.GetComponent<OverlaySelectableVisual>();
		if (visual != null)
		{
			visual.SetBaseColor(color);
		}
		else
		{
			button.GetComponent<Image>().color = color;
		}
	}

	private void BuildCategorySidebar(Transform parent, string title, IEnumerable<string> categories, string selected, Action<string> select)
	{
		GameObject gameObject = CreateObject("Categories", parent, typeof(Image));
		Image categoryImage = gameObject.GetComponent<Image>();
		categoryImage.color = CheatMenuStyle.Surface;
		_fluentControls.StyleSurface(gameObject, categoryImage, ControlLibrary.Core.FluentSurfaceRole.Navigation, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(gameObject, CheatMenuStyle.Theme.Space1, new RectOffset(8, 8, 8, 8));
		SetLayout(gameObject, CheatMenuStyle.CategorySidebarWidth, -1f, 0f);
		LayoutElement categoryLayout = gameObject.GetComponent<LayoutElement>();
		categoryLayout.minWidth = CheatMenuStyle.CategorySidebarWidth;
		categoryLayout.preferredWidth = CheatMenuStyle.CategorySidebarWidth;
		CreateLabel(gameObject.transform, title, 16, CheatMenuStyle.Text);
		foreach (string category in categories)
		{
			string captured = category;
			CreateButton(gameObject.transform, category, delegate
			{
				select(captured);
			}, string.Format(PluginLocalization.Translate("只显示“{0}”分类中的车辆。"), PluginLocalization.Translate(category)),
				0f, CheatMenuStyle.CategoryButtonHeight, (category == selected) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 13);
		}
	}

	private GameObject BuildItemCategorySidebar(Transform parent, string title, ItemFilterState filter, Action<ItemPrimaryCategory> select, Func<ItemAsset, bool> includeItem = null)
	{
		GameObject gameObject = CreateObject("Categories", parent, typeof(Image));
		Image categoryImage = gameObject.GetComponent<Image>();
		categoryImage.color = CheatMenuStyle.Surface;
		_fluentControls.StyleSurface(gameObject, categoryImage, ControlLibrary.Core.FluentSurfaceRole.Navigation, CheatMenuStyle.Theme.RadiusControl);
		AddVerticalLayout(gameObject, CheatMenuStyle.Theme.Space1, new RectOffset(8, 8, 8, 8));
		SetLayout(gameObject, CheatMenuStyle.CategorySidebarWidth, -1f, 0f);
		LayoutElement categoryLayout = gameObject.GetComponent<LayoutElement>();
		categoryLayout.minWidth = CheatMenuStyle.CategorySidebarWidth;
		categoryLayout.preferredWidth = CheatMenuStyle.CategorySidebarWidth;
		CreateLabel(gameObject.transform, title, 16, CheatMenuStyle.Text);
		foreach (ItemPrimaryCategory category in ItemFilterService.Categories)
		{
			ItemPrimaryCategory captured = category;
			ItemFilterState categoryFilter = filter.Clone();
			categoryFilter.Category = category;
			ItemFilterService.NormalizeForCategory(categoryFilter);
			int num = _catalogPresenter.Items.Count((ItemAsset asset) => (includeItem == null || includeItem(asset)) && ItemFilterService.Matches(asset, categoryFilter));
			string categoryLabel = PluginLocalization.Translate(ItemFilterService.GetPrimaryCategoryLabel(category));
			string label = $"{categoryLabel}  {num:N0}";
			CreateButton(gameObject.transform, label, delegate
			{
				select(captured);
			}, string.Format(PluginLocalization.Translate("只显示“{0}”分类中的物品。"), categoryLabel),
				0f, CheatMenuStyle.CategoryButtonHeight, (category == filter.Category) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 13);
		}
		return gameObject;
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
			}, "清除当前已应用的全部高级物品筛选条件。", 100f, 30f, CheatMenuStyle.SurfaceHover, 12);
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
			string filterLabel = string.Join(" / ", array);
			string text = filterLabel + " ×";
			CreateButton(parent, text, remove, string.Format(PluginLocalization.Translate("移除“{0}”筛选条件。"), filterLabel),
				Mathf.Clamp(48f + (float)text.Length * 11f, 92f, 280f), 30f, CheatMenuStyle.SurfaceHover, 11);
		}
	}

	private void ApplyItemFilterFromChip()
	{
		RefreshActiveCatalogResultsRegion();
	}

	private void ShowItemFilterPanel(bool forFavorites)
	{
		PrepareModalOpen();
		_itemFilterForInfiniteStorage = false;
		_itemFilterForFavorites = forFavorites;
		_itemFilterDraft = (_itemFilterForInfiniteStorage ? _infiniteStorageFilter : (forFavorites ? _favoriteItemFilter : _itemFilter)).Clone();
		BuildItemFilterPanel();
	}

	private void ShowInfiniteStorageFilterPanel()
	{
		PrepareModalOpen();
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
		_itemFilterRoot = CreateObject("ItemFilterOverlay", _uiRoot.ModalLayer, typeof(Image));
		_fluentHandles.RegisterScope(_itemFilterRoot);
		_uiRoot.RegisterModal(_itemFilterRoot, HideItemFilterPanel);
		Stretch(_itemFilterRoot.GetComponent<RectTransform>());
		_itemFilterRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.56f);
		GameObject gameObject = CreateObject("ItemFilterDialog", _itemFilterRoot.transform, typeof(Image));
		Image dialogImage = gameObject.GetComponent<Image>();
		dialogImage.color = CheatMenuStyle.Panel;
		_fluentControls.StyleSurface(gameObject, dialogImage, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusPanel);
		Outline outline = gameObject.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Vector2 anchorMin = (component.anchorMax = new Vector2(0.5f, 0.5f));
		component.anchorMin = anchorMin;
		component.pivot = new Vector2(0.5f, 0.5f);
		component.sizeDelta = new Vector2(860f, 610f);
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(24, 24, 16, 16);
		verticalLayoutGroup.spacing = 8f;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject2 = CreateRow(gameObject.transform, 42f, 8f);
		CreateText(gameObject2.transform, "筛选", 20, CheatMenuStyle.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateFlexibleSpacer(gameObject2.transform);
		CreateButton(gameObject2.transform, "×", HideItemFilterPanel, "关闭筛选面板并放弃尚未应用的更改。", 42f, 38f, CheatMenuStyle.Surface, 22);
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
		}, "清除筛选面板中的高级条件；点击应用后才会影响结果。", 90f);
		CreateButton(gameObject3.transform, "取消", HideItemFilterPanel, "放弃筛选面板中尚未应用的更改。", 90f);
		CreateButton(gameObject3.transform, "应用", ApplyItemFilterPanel, "应用面板中的筛选条件并刷新当前物品结果。", 90f, 36f, CheatMenuStyle.Accent);
		AnimateModalEnter(_itemFilterRoot, gameObject.transform);
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
		gridLayoutGroup.cellSize = new Vector2(num2, CheatMenuStyle.Theme.ControlCompact);
		gridLayoutGroup.spacing = new Vector2(4f, 5f);
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		int num3 = Mathf.CeilToInt((float)labels.Count / (float)num);
		SetLayout(gameObject, -1f, (float)num3 * (CheatMenuStyle.Theme.ControlCompact + CheatMenuStyle.Theme.Space1) - CheatMenuStyle.Theme.Space1);
		for (int i = 0; i < labels.Count; i++)
		{
			int captured = i;
			CreateButton(gameObject.transform, labels[i], delegate
			{
				select(captured);
			}, string.Format(PluginLocalization.Translate("切换“{0}”中的“{1}”筛选选项。"), PluginLocalization.Translate(title), PluginLocalization.Translate(labels[i])),
				num2, 32f, selected(i) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 11);
		}
	}

	private void RebuildItemFilterPanel()
	{
		if (!(_itemFilterRoot == null))
		{
			RunWithoutTransientMotion(delegate
			{
				_ = _itemFilterForFavorites;
				GameObject itemFilterRoot = _itemFilterRoot;
				_itemFilterRoot = null;
				QueueForDestroy(itemFilterRoot);
				BuildItemFilterPanel();
			});
		}
	}

	private void ApplyItemFilterPanel()
	{
		ItemFilterState obj = _itemFilterForInfiniteStorage ? _infiniteStorageFilter : (_itemFilterForFavorites ? _favoriteItemFilter : _itemFilter);
		obj.CopyFrom(_itemFilterDraft);
		ItemFilterService.NormalizeForCategory(obj);
		RunWithoutTransientMotion(HideItemFilterPanel);
		_itemFilterForInfiniteStorage = false;
		BuildItemResults();
		BuildFavoriteResults();
		if (_infiniteStorageRoot != null && _infiniteStorageRoot.activeSelf)
		{
			_infiniteStoragePage = 0;
			RefreshInfiniteStorageContentRegions();
		}
		else
		{
			RefreshActiveCatalogResultsRegion();
		}
	}

	private void HideItemFilterPanel()
	{
		if (!(_itemFilterRoot == null))
		{
			GameObject root = _itemFilterRoot;
			_itemFilterRoot = null;
			_itemFilterDraft = null;
			QueueModalExit(root, ResolveModalVisual(root));
		}
	}

	private void ClearResultHost(GameObject host)
	{
		if (host == null)
		{
			return;
		}
		for (int i = host.transform.childCount - 1; i >= 0; i--)
		{
			QueueForDestroy(host.transform.GetChild(i).gameObject);
		}
	}

	private void ResetCatalogPageViewReferences()
	{
		_itemResultsHost = null;
		_itemGridHost = null;
		_itemSummaryHost = null;
		_vehicleResultsHost = null;
		_vehicleGridHost = null;
		_vehicleSummaryText = null;
		_favoriteResultsHost = null;
		_favoriteGridHost = null;
		_favoriteBodyHost = null;
		_favoriteSummaryHost = null;
		_favoriteItemsButton = null;
		_favoriteVehiclesButton = null;
	}

	private void RefreshActiveCatalogResultsRegion()
	{
		switch (_activeTab)
		{
		case MenuTab.Items:
			RefreshItemResultsRegion();
			break;
		case MenuTab.Vehicles:
			RefreshVehicleResultsRegion();
			break;
		case MenuTab.Favorites:
			RefreshFavoriteResultsRegion();
			break;
		}
	}

	private void RefreshActiveCatalogGridRegion()
	{
		switch (_activeTab)
		{
		case MenuTab.Items:
			RefreshItemGridRegion();
			break;
		case MenuTab.Vehicles:
			RefreshVehicleGridRegion();
			break;
		case MenuTab.Favorites:
			RefreshFavoriteGridRegion();
			break;
		}
	}

	private void RefreshItemGridRegion()
	{
		if (_activeTab == MenuTab.Items && _itemGridHost != null)
		{
			ClearResultHost(_itemGridHost);
			PopulateItemGrid();
		}
	}

	private void RefreshVehicleGridRegion()
	{
		if (_activeTab == MenuTab.Vehicles && _vehicleGridHost != null)
		{
			ClearResultHost(_vehicleGridHost);
			PopulateVehicleGrid();
		}
	}

	private void RefreshFavoriteGridRegion()
	{
		if (_activeTab == MenuTab.Favorites && _favoriteGridHost != null)
		{
			ClearResultHost(_favoriteGridHost);
			int count = _favoriteKind == FavoriteKind.Items ? _favoriteItemResults.Count : _favoriteVehicleResults.Count;
			PopulateFavoriteGrid(count);
		}
	}

	private void RefreshItemResultsRegion()
	{
		if (_activeTab != MenuTab.Items || _itemResultsHost == null)
		{
			return;
		}
		BuildItemResults();
		if (_itemSummaryHost != null)
		{
			ClearResultHost(_itemSummaryHost);
			CreateItemFilterSummary(_itemSummaryHost.transform, _itemFilter, _itemResults.Count, _catalogPresenter.Items.Count);
		}
		ClearResultHost(_itemResultsHost);
		_itemGridHost = null;
		BuildItemCategorySidebar(_itemResultsHost.transform, "物品分类", _itemFilter, delegate(ItemPrimaryCategory category)
		{
			_itemFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_itemFilter);
			RefreshItemResultsRegion();
		});
		BuildItemGrid(_itemResultsHost.transform);
	}

	private void RefreshVehicleResultsRegion()
	{
		if (_activeTab != MenuTab.Vehicles || _vehicleResultsHost == null)
		{
			return;
		}
		BuildVehicleResults();
		RefreshVehicleSummary();
		ClearResultHost(_vehicleResultsHost);
		_vehicleGridHost = null;
		BuildCategorySidebar(_vehicleResultsHost.transform, "车辆分类", VehicleCategories, _vehicleCategory, delegate(string category)
		{
			_vehicleCategory = category;
			RefreshVehicleResultsRegion();
		});
		BuildVehicleGrid(_vehicleResultsHost.transform);
	}

	private void RefreshFavoriteResultsRegion()
	{
		if (_activeTab != MenuTab.Favorites || _favoriteResultsHost == null)
		{
			return;
		}
		BuildFavoriteResults();
		RefreshFavoriteSummary();
		ClearResultHost(_favoriteResultsHost);
		_favoriteGridHost = null;
		if (_favoriteKind == FavoriteKind.Items)
		{
			BuildItemCategorySidebar(_favoriteResultsHost.transform, "收藏物品分类", _favoriteItemFilter, delegate(ItemPrimaryCategory category)
			{
				_favoriteItemFilter.Category = category;
				ItemFilterService.NormalizeForCategory(_favoriteItemFilter);
				RefreshFavoriteResultsRegion();
			}, _catalogPresenter.IsItemFavorite);
		}
		else
		{
			BuildCategorySidebar(_favoriteResultsHost.transform, "收藏车辆分类", VehicleCategories, _favoriteVehicleCategory, delegate(string category)
			{
				_favoriteVehicleCategory = category;
				RefreshFavoriteResultsRegion();
			});
		}
		BuildFavoriteGrid(_favoriteResultsHost.transform);
		RefreshFavoriteKindButtons(
			_catalogPresenter.Items.Count(_catalogPresenter.IsItemFavorite),
			_catalogPresenter.Vehicles.Count(_catalogPresenter.IsVehicleFavorite));
	}

	private void RefreshVehicleSummary()
	{
		if (_vehicleSummaryText != null)
		{
			_vehicleSummaryText.text = PluginLocalization.Translate($"显示 {_vehicleResults.Count} / {_catalogPresenter.Vehicles.Count}；模组车辆 {_catalogPresenter.WorkshopVehicleCount}。每次生成 1–20 辆。");
		}
	}

	private void RefreshFavoriteSummary()
	{
		if (_favoriteSummaryHost == null)
		{
			return;
		}
		ClearResultHost(_favoriteSummaryHost);
		if (_favoriteKind == FavoriteKind.Items)
		{
			int itemCount = _catalogPresenter.Items.Count(_catalogPresenter.IsItemFavorite);
			CreateItemFilterSummary(_favoriteSummaryHost.transform, _favoriteItemFilter, _favoriteItemResults.Count, itemCount);
		}
		else
		{
			CreateLabel(_favoriteSummaryHost.transform, $"显示 {_favoriteVehicleResults.Count} 辆已加载收藏车辆。", 12, CheatMenuStyle.Muted);
		}
	}

	private int GetResponsiveCardColumnCount()
	{
		float windowWidth = _panel != null && _panel.rect.width > 0f ? _panel.rect.width : PreferredWindowWidth;
		float shellChrome = 32f;
		if (_wideShellLayout == true)
		{
			shellChrome += CheatMenuStyle.MainNavigationWidth + CheatMenuStyle.Theme.Space3;
		}
		float pagePadding = CheatMenuStyle.Theme.Space3 * 2f;
		float catalogSpacing = CheatMenuStyle.Theme.Space2;
		float availableGridWidth = Mathf.Max(0f,
			windowWidth - shellChrome - pagePadding - CheatMenuStyle.CategorySidebarWidth - catalogSpacing);
		float gridHorizontalPadding = 8f;
		float cardStride = CheatMenuStyle.CardWidth + CheatMenuStyle.RowSpacing;
		return Mathf.Clamp(Mathf.FloorToInt(
			(availableGridWidth - gridHorizontalPadding + CheatMenuStyle.RowSpacing) / cardStride), 3, 6);
	}

	private int GetResponsiveCardRowCount()
	{
		float windowHeight = _panel != null && _panel.rect.height > 0f ? _panel.rect.height : PreferredWindowHeight;
		return windowHeight < 740f ? 2 : 3;
	}

	private static float GetCardGridHeight(int rowCount)
	{
		return rowCount * CheatMenuStyle.CardHeight
			+ Mathf.Max(0, rowCount - 1) * CheatMenuStyle.RowSpacing
			+ 8f;
	}

	private void BuildItemGrid(Transform parent)
	{
		_itemGridHost = CreateObject("ItemGridColumn", parent);
		AddVerticalLayout(_itemGridHost, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(_itemGridHost, -1f, -1f, 1f, 1f);
		PopulateItemGrid();
	}

	private void PopulateItemGrid()
	{
		if (_itemGridHost == null)
		{
			return;
		}
		int columnCount = GetResponsiveCardColumnCount();
		int rowCount = GetResponsiveCardRowCount();
		int pageSize = columnCount * rowCount;
		int num = Math.Max(1, Mathf.CeilToInt((float)_itemResults.Count / pageSize));
		_itemPage = Mathf.Clamp(_itemPage, 0, num - 1);
		Action<int> changePage = delegate(int value)
		{
			_itemPage = value;
			RefreshItemGridRegion();
		};
		BuildPagination(_itemGridHost.transform, _itemPage, num, changePage);
		AddPageScrollHandler(_itemGridHost, _itemPage, num, changePage);
		if (_itemResults.Count == 0)
		{
			CreateEmptyState(_itemGridHost.transform, string.IsNullOrWhiteSpace(_itemFilter.SearchQuery) ? "当前分类没有可用物品。" : "没有匹配的物品。", string.IsNullOrWhiteSpace(_itemFilter.SearchQuery) ? "请选择其他分类，或重新扫描当前加载的资产。" : "请清空搜索或尝试其他关键词。");
			return;
		}
		GameObject gameObject2 = CreateObject("ItemGrid", _itemGridHost.transform);
		SetLayout(gameObject2, -1f, GetCardGridHeight(rowCount), -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(CheatMenuStyle.RowSpacing, CheatMenuStyle.RowSpacing);
		gridLayoutGroup.cellSize = new Vector2(CheatMenuStyle.CardWidth, CheatMenuStyle.CardHeight);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = columnCount;
		int num2 = _itemPage * pageSize;
		int num3 = Math.Min(num2 + pageSize, _itemResults.Count);
		for (int num4 = num2; num4 < num3; num4++)
		{
			CreateItemCard(gameObject2.transform, _itemResults[num4]);
		}
	}

	private void BuildVehicleGrid(Transform parent)
	{
		_vehicleGridHost = CreateObject("VehicleGridColumn", parent);
		AddVerticalLayout(_vehicleGridHost, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(_vehicleGridHost, -1f, -1f, 1f, 1f);
		PopulateVehicleGrid();
	}

	private void PopulateVehicleGrid()
	{
		if (_vehicleGridHost == null)
		{
			return;
		}
		int columnCount = GetResponsiveCardColumnCount();
		int rowCount = GetResponsiveCardRowCount();
		int pageSize = columnCount * rowCount;
		int num = Math.Max(1, Mathf.CeilToInt((float)_vehicleResults.Count / pageSize));
		_vehiclePage = Mathf.Clamp(_vehiclePage, 0, num - 1);
		Action<int> changePage = delegate(int value)
		{
			_vehiclePage = value;
			RefreshVehicleGridRegion();
		};
		BuildPagination(_vehicleGridHost.transform, _vehiclePage, num, changePage);
		AddPageScrollHandler(_vehicleGridHost, _vehiclePage, num, changePage);
		if (_vehicleResults.Count == 0)
		{
			CreateEmptyState(_vehicleGridHost.transform, string.IsNullOrWhiteSpace(_vehicleQuery) ? "当前分类没有可用车辆。" : "没有匹配的车辆。", string.IsNullOrWhiteSpace(_vehicleQuery) ? "请选择其他分类，或重新扫描当前加载的资产。" : "请清空搜索或尝试其他关键词。");
			return;
		}
		GameObject gameObject2 = CreateObject("VehicleGrid", _vehicleGridHost.transform);
		SetLayout(gameObject2, -1f, GetCardGridHeight(rowCount), -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(CheatMenuStyle.RowSpacing, CheatMenuStyle.RowSpacing);
		gridLayoutGroup.cellSize = new Vector2(CheatMenuStyle.CardWidth, CheatMenuStyle.CardHeight);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = columnCount;
		int num2 = _vehiclePage * pageSize;
		int num3 = Math.Min(num2 + pageSize, _vehicleResults.Count);
		for (int num4 = num2; num4 < num3; num4++)
		{
			CreateVehicleCard(gameObject2.transform, _vehicleResults[num4]);
		}
	}

	private void BuildFavoriteGrid(Transform parent)
	{
		int num = ((_favoriteKind == FavoriteKind.Items) ? _favoriteItemResults.Count : _favoriteVehicleResults.Count);
		_favoriteGridHost = CreateObject("FavoriteGridColumn", parent);
		AddVerticalLayout(_favoriteGridHost, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(_favoriteGridHost, -1f, -1f, 1f, 1f);
		PopulateFavoriteGrid(num);
	}

	private void PopulateFavoriteGrid(int num)
	{
		if (_favoriteGridHost == null)
		{
			return;
		}
		int columnCount = GetResponsiveCardColumnCount();
		int rowCount = GetResponsiveCardRowCount();
		int pageSize = columnCount * rowCount;
		int num2 = Math.Max(1, Mathf.CeilToInt((float)num / pageSize));
		_favoritePage = Mathf.Clamp(_favoritePage, 0, num2 - 1);
		Action<int> changePage = delegate(int value)
		{
			_favoritePage = value;
			RefreshFavoriteGridRegion();
		};
		BuildPagination(_favoriteGridHost.transform, _favoritePage, num2, changePage);
		AddPageScrollHandler(_favoriteGridHost, _favoritePage, num2, changePage);
		if (num == 0)
		{
			CreateEmptyState(_favoriteGridHost.transform, (_favoriteKind == FavoriteKind.Items) ? "当前分类没有已加载的收藏物品。" : "当前分类没有已加载的收藏车辆。", (_favoriteKind == FavoriteKind.Items) ? "请在物品卡片右上角点击 ☆ 收藏。" : "请在车辆卡片右上角点击 ☆ 收藏。");
			return;
		}
		GameObject gameObject2 = CreateObject("FavoriteGrid", _favoriteGridHost.transform);
		SetLayout(gameObject2, -1f, GetCardGridHeight(rowCount), -1f, 1f);
		GridLayoutGroup gridLayoutGroup = gameObject2.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.spacing = new Vector2(CheatMenuStyle.RowSpacing, CheatMenuStyle.RowSpacing);
		gridLayoutGroup.cellSize = new Vector2(CheatMenuStyle.CardWidth, CheatMenuStyle.CardHeight);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = columnCount;
		int num3 = _favoritePage * pageSize;
		int num4 = Math.Min(num3 + pageSize, num);
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
		string displayName = ((_translationPresenter.TryGetTranslation(asset, WorkshopTextField.ItemName, out var translation) && !string.IsNullOrWhiteSpace(translation)) ? translation : asset.FriendlyName);
		string tooltip = AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _catalogPresenter.Items);
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
			_itemAmountText = _giveAmount.ToString();
			bool flag = _catalogPresenter.GiveItem(asset, _giveAmount);
			SetStatus(flag ? $"已给予 {_giveAmount} × {displayName}。" : ("无法给予 " + displayName + "。"), 4f, flag ? StatusKind.Success : StatusKind.Error);
		}, tooltip + "\n\n" + PluginLocalization.Translate("点击卡片按当前数量给予此物品。"), 155f, 158f, CheatMenuStyle.Surface, delayTooltip: true);
		AddCardContents(button.transform, displayName, $"ID {asset.id}  ·  {AssetCatalog.GetOriginLabel(asset)}", () => _catalogPresenter.GetItemIcon(asset), isVehicle: false);
		AddFavoriteButton(button.transform, _catalogPresenter.IsItemFavorite(asset), delegate
		{
			ToggleItemFavorite(asset);
		});
	}

	private void CreateVehicleCard(Transform parent, VehicleAsset asset)
	{
		string displayName = ((_translationPresenter.TryGetTranslation(asset, WorkshopTextField.VehicleName, out var translation) && !string.IsNullOrWhiteSpace(translation)) ? translation : asset.FriendlyName);
		string tooltip = AssetHoverDetailsBuilder.BuildVehicle(asset, displayName);
		Button button = CreateButton(parent, string.Empty, delegate
		{
			_spawnVehicleAmount = ParseClamped(_vehicleAmountText, 1, 20, _spawnVehicleAmount);
			_vehicleAmountText = _spawnVehicleAmount.ToString();
			int num = _catalogPresenter.SpawnVehicles(asset, _spawnVehicleAmount);
			SetStatus((num > 0) ? $"已在玩家前方生成 {num} × {displayName}。" : ("无法生成 " + displayName + "。"), 4f, (num > 0) ? StatusKind.Success : StatusKind.Error);
		}, tooltip + "\n\n" + PluginLocalization.Translate("点击卡片按当前数量在玩家前方生成此载具。"), 155f, 158f, CheatMenuStyle.Surface, delayTooltip: true);
		string text = ((asset.id == 0) ? "GUID" : $"ID {asset.id}");
		AddCardContents(button.transform, displayName, text + "  ·  " + AssetCatalog.GetOriginLabel(asset), () => _catalogPresenter.GetVehicleIcon(asset), isVehicle: true);
		AddFavoriteButton(button.transform, _catalogPresenter.IsVehicleFavorite(asset), delegate
		{
			ToggleVehicleFavorite(asset);
		});
	}

	private void AddFavoriteButton(Transform parent, bool isFavorite, System.Action toggle)
	{
		Button button = CreateButton(parent, isFavorite ? "★" : "☆", toggle,
			isFavorite ? "从收藏中移除此条目。" : "将此条目添加到收藏。",
			0f, 0f, isFavorite ? CheatMenuStyle.Favorite : CheatMenuStyle.SurfaceHover, 18);
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
		bool flag = !_catalogPresenter.IsItemFavorite(asset);
		if (!_catalogPresenter.TrySetItemFavorite(asset, flag))
		{
			SetStatus("无法保存 " + asset.FriendlyName + " 的收藏状态。", 5f, StatusKind.Error);
			return;
		}
		MenuTab activeTab = _activeTab;
		BuildFavoriteResults();
		if (activeTab == MenuTab.Favorites)
		{
			RefreshFavoriteResultsRegion();
		}
		else if (activeTab == MenuTab.Items)
		{
			RefreshItemGridRegion();
		}
		SetStatus(flag ? ("已收藏物品：" + asset.FriendlyName + "。") : ("已取消收藏物品：" + asset.FriendlyName + "。"), 4f);
	}

	private void ToggleVehicleFavorite(VehicleAsset asset)
	{
		bool flag = !_catalogPresenter.IsVehicleFavorite(asset);
		if (!_catalogPresenter.TrySetVehicleFavorite(asset, flag))
		{
			SetStatus("无法保存 " + asset.FriendlyName + " 的收藏状态。", 5f, StatusKind.Error);
			return;
		}
		MenuTab activeTab = _activeTab;
		BuildFavoriteResults();
		if (activeTab == MenuTab.Favorites)
		{
			RefreshFavoriteResultsRegion();
		}
		else if (activeTab == MenuTab.Vehicles)
		{
			RefreshVehicleGridRegion();
		}
		SetStatus(flag ? ("已收藏车辆：" + asset.FriendlyName + "。") : ("已取消收藏车辆：" + asset.FriendlyName + "。"), 4f);
	}

	private void AddCardContents(Transform parent, string name, string meta, Func<Texture2D> iconLoader, bool isVehicle)
	{
		GameObject iconViewport = CreateObject("IconViewport", parent);
		RectTransform viewportRect = iconViewport.GetComponent<RectTransform>();
		viewportRect.anchorMin = new Vector2(0.06f, 0.28f);
		viewportRect.anchorMax = new Vector2(0.94f, 0.97f);
		Vector2 offsetMin = (viewportRect.offsetMax = Vector2.zero);
		viewportRect.offsetMin = offsetMin;
		GameObject gameObject = CreateObject("Icon", iconViewport.transform, typeof(RawImage), typeof(AspectRatioFitter), typeof(OverlayIconBinder));
		RectTransform component = gameObject.GetComponent<RectTransform>();
		Stretch(component);
		RawImage component2 = gameObject.GetComponent<RawImage>();
		component2.color = Color.white;
		component2.raycastTarget = false;
		AspectRatioFitter aspectRatioFitter = gameObject.GetComponent<AspectRatioFitter>();
		aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		aspectRatioFitter.aspectRatio = 1f;
		Text text = CreateText(gameObject.transform, "加载预览…", 11, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
		Stretch(text.rectTransform);
		gameObject.GetComponent<OverlayIconBinder>().Initialize(component2, aspectRatioFitter, text, iconLoader, isVehicle);
		Text text2 = CreateText(parent, name, 12, CheatMenuStyle.Text, TextAnchor.MiddleCenter, FontStyle.Bold, localize: false);
		text2.rectTransform.anchorMin = new Vector2(0.04f, 0.09f);
		text2.rectTransform.anchorMax = new Vector2(0.96f, 0.28f);
		RectTransform rectTransform = text2.rectTransform;
		offsetMin = (text2.rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		Text text3 = CreateText(parent, meta, 10, CheatMenuStyle.Muted, TextAnchor.MiddleCenter);
		text3.rectTransform.anchorMin = new Vector2(0.04f, 0.01f);
		text3.rectTransform.anchorMax = new Vector2(0.96f, 0.09f);
		RectTransform rectTransform2 = text3.rectTransform;
		offsetMin = (text3.rectTransform.offsetMax = Vector2.zero);
		rectTransform2.offsetMin = offsetMin;
	}

	private void AddPageScrollHandler(GameObject target, int page, int pageCount, Action<int> changePage)
	{
		Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
		image.color = Color.clear;
		PageScrollHandler handler = target.GetComponent<PageScrollHandler>() ?? target.AddComponent<PageScrollHandler>();
		handler.Initialize(page, pageCount,
			delegate
			{
				if (page > 0) { RunWithoutTransientMotion(HideHoverExplanation); changePage(page - 1); }
			},
			delegate
			{
				if (page < pageCount - 1) { RunWithoutTransientMotion(HideHoverExplanation); changePage(page + 1); }
			});
	}

	private void BuildPagination(Transform parent, int page, int pageCount, Action<int> changePage)
	{
		GameObject gameObject = CreateRow(parent, 34f, 8f);
		CreateButton(gameObject.transform, "‹ 上一页", delegate
		{
			changePage(page - 1);
		}, "切换到上一页结果。", 100f, 32f).interactable = page > 0;
		CreateFlexibleSpacer(gameObject.transform);
		CreateLabel(gameObject.transform, $"第 {page + 1} / {pageCount} 页", 12, CheatMenuStyle.Muted, 110f, TextAnchor.MiddleCenter);
		CreateFlexibleSpacer(gameObject.transform);
		CreateButton(gameObject.transform, "下一页 ›", delegate
		{
			changePage(page + 1);
		}, "切换到下一页结果。", 100f, 32f).interactable = page < pageCount - 1;
	}
}
