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
	private static readonly InfiniteStorageDecomposeScope[] InfiniteStorageDecomposeScopes =
	{
		InfiniteStorageDecomposeScope.SingleItem,
		InfiniteStorageDecomposeScope.AssetId,
		InfiniteStorageDecomposeScope.Stack
	};

	private bool _infiniteStorageDecomposeMode;
	private InfiniteStorageDecomposeScope _infiniteStorageDecomposeScope = InfiniteStorageDecomposeScope.SingleItem;
	private bool _infiniteStorageDecomposeScopeDrawerOpen;
	private GameObject _infiniteStorageDecomposeScopeDrawerRoot;
	private RectTransform _infiniteStorageDecomposeScopeDrawerPopup;
	private Button _infiniteStorageDecomposeScopeTriggerButton;

	private void BuildInfiniteStoragePanel(Transform panel)
	{
		InfiniteStoragePageSnapshot snapshot = _infiniteStoragePresenter.CaptureSnapshot();
		if (!snapshot.IsAvailable)
		{
			CreateEmptyState(panel, "无限储存不可用", "储存运行时尚未初始化。");
			return;
		}
		GameObject row = CreateRow(panel, 40f, 7f);
		CreateToggleAction(row.transform, "启用无限储存", snapshot.Enabled, delegate(bool value)
		{
			_infiniteStoragePresenter.SetEnabled(value);
		});
		GameObject priorityRow = CreateRow(panel, 34f, 7f);
		CreateToggleAction(priorityRow.transform, "拾取优先存入无限储存", snapshot.PickupPriorityEnabled, delegate(bool value)
		{
			_infiniteStoragePresenter.SetPickupPriority(value);
		});
		CreateButton(priorityRow.transform, PluginLocalization.Translate("分解模式") + (_infiniteStorageDecomposeMode ? " ON" : " OFF"), delegate
		{
			_infiniteStorageDecomposeMode = !_infiniteStorageDecomposeMode;
			HideInfiniteStorageDecomposeScopeDrawer();
			SetStatus(_infiniteStorageDecomposeMode ? "分解模式已开启；只有可分解的非材料物品会在悬停时变红。" : "分解模式已关闭。", 4f, StatusKind.Info);
			RebuildInfiniteStoragePage();
		}, "开启后只有带原生分解配方的非材料物品卡片会在悬停时变红；点击卡片按所选范围分解，分解产物直接存回无限储存。",
			132f, 34f, _infiniteStorageDecomposeMode ? CheatMenuStyle.Danger : CheatMenuStyle.Surface, 12);
		Button decomposeScopeTrigger = null;
		decomposeScopeTrigger = CreateButton(priorityRow.transform,
			PluginLocalization.Translate("分解范围") + "：" + PluginLocalization.Translate(InfiniteStorageDecomposeScopeLabel(_infiniteStorageDecomposeScope)) + " ▼",
			delegate { ToggleInfiniteStorageDecomposeScopeDrawer(decomposeScopeTrigger); },
			"选择分解范围：单物品只分解一个；ID 分解所有同 ID 物品；堆叠分解当前卡片的全部数量。",
			170f, 34f, CheatMenuStyle.Surface, 11);
		CreateLabel(row.transform, "搜索", 13, CheatMenuStyle.Text, 64f);
		CreateSearchInput(row.transform, _infiniteStorageFilter.SearchQuery, delegate(string value)
		{
			_infiniteStorageFilter.SearchQuery = value;
			_infiniteStoragePage = 0;
			RefreshInfiniteStorageResultsRegion();
		}, 220f);
		CreateLabel(row.transform, "取出数量", 13, CheatMenuStyle.Text, CheatMenuStyle.InfiniteStorageWithdrawLabelWidth);
		CreateButton(row.transform, "−", delegate
		{
			_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "将每次从无限储存取出的数量减少 1，最低为 1。", 36f, 34f);
		RegisterItemAmountInput(CreateInput(row.transform, _itemAmountText, delegate(string value)
		{
			_itemAmountText = value;
			if (int.TryParse(value, out var result))
			{
				_giveAmount = (byte)Mathf.Clamp(result, 1, 255);
			}
		}, 62f));
		CreateButton(row.transform, "+", delegate
		{
			_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "将每次从无限储存取出的数量增加 1，最高为 255。", 36f, 34f);
		Button filterButton = CreateButton(row.transform, GetInfiniteStorageFilterButtonLabel(), ShowInfiniteStorageFilterPanel, "打开无限储存物品的详细筛选面板。", 92f, 34f, CheatMenuStyle.Surface, 12);
		_infiniteStorageFilterButtonText = filterButton.GetComponentInChildren<Text>(true);
		if (snapshot.LegacyAmbiguousEntryCount > 0)
		{
			GameObject legacyRow = CreateRow(panel, 34f, 7f);
			CreateLabel(legacyRow.transform, $"检测到 {snapshot.LegacyAmbiguousEntryCount:N0} 条待处理旧存档记录。", 13, CheatMenuStyle.Warning, 0f);
			CreateButton(legacyRow.transform, "导出原始备份", delegate
			{
				bool exported = _infiniteStoragePresenter.TryExportLegacyAmbiguousEntries();
				SetStatus(exported ? "已导出待处理旧存档原始备份。" : "导出待处理旧存档失败。", 4f, exported ? StatusKind.Success : StatusKind.Warning);
			}, "导出所有待处理旧存档的原始 JSON 备份。", 118f, 30f, CheatMenuStyle.Surface, 12);
		}
		CreateButton(panel, "存储当前携带物品", delegate
		{
			int moved = _infiniteStoragePresenter.StoreAllCarried();
			SetStatus(moved > 0 ? $"已存储 {moved} 个背包格。" : "没有可存储的物品，或无限储存未启用。", 4f, StatusKind.Info);
			RefreshInfiniteStorageContentRegions();
		}, "将当前背包中的物品存入无限储存。", 180f, 40f, CheatMenuStyle.Success, 13);
		_infiniteStorageSummaryText = CreateLabel(panel, string.Empty, 14, CheatMenuStyle.Text);
		_infiniteStorageContentRow = CreateRow(panel, 0f, 10f);
		SetLayout(_infiniteStorageContentRow, -1f, -1f, -1f, 1f);
		HashSet<ushort> storedAssetIds = new HashSet<ushort>(snapshot.Entries.Select(entry => entry.AssetId));
		_infiniteStorageCategoryHost = BuildItemCategorySidebar(_infiniteStorageContentRow.transform, "物品分类", _infiniteStorageFilter, delegate(ItemPrimaryCategory category)
		{
			_infiniteStorageFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_infiniteStorageFilter);
			_infiniteStoragePage = 0;
			RefreshInfiniteStorageContentRegions();
		}, asset => storedAssetIds.Contains(asset.id));
		_infiniteStorageResultsHost = CreateObject("InfiniteStorageResultsHost", _infiniteStorageContentRow.transform, typeof(Image));
		_infiniteStorageResultsHost.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		AddVerticalLayout(_infiniteStorageResultsHost, CheatMenuStyle.Theme.Space2, new RectOffset(8, 8, 8, 8));
		SetLayout(_infiniteStorageResultsHost, -1f, -1f, 1f, 1f);
		RefreshInfiniteStorageResultsRegion();
	}

	private string GetInfiniteStorageFilterButtonLabel()
	{
		return (_infiniteStorageFilter.ActiveFilterCount == 0) ? "筛选" : $"筛选 {_infiniteStorageFilter.ActiveFilterCount}";
	}

	private void RefreshInfiniteStorageContentRegions()
	{
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf || !_infiniteStoragePresenter.CaptureSnapshot().IsAvailable)
		{
			return;
		}
		if (_infiniteStorageFilterButtonText != null)
		{
			_infiniteStorageFilterButtonText.text = GetInfiniteStorageFilterButtonLabel();
		}
		RefreshInfiniteStorageCategoryRegion();
		RefreshInfiniteStorageResultsRegion();
	}

	private void RefreshInfiniteStorageCategoryRegion()
	{
		InfiniteStoragePageSnapshot snapshot = _infiniteStoragePresenter.CaptureSnapshot();
		if (_infiniteStorageContentRow == null || !snapshot.IsAvailable)
		{
			return;
		}
		if (_infiniteStorageCategoryHost != null)
		{
			QueueForDestroy(_infiniteStorageCategoryHost);
		}
		HashSet<ushort> storedAssetIds = new HashSet<ushort>(snapshot.Entries.Select(entry => entry.AssetId));
		_infiniteStorageCategoryHost = BuildItemCategorySidebar(_infiniteStorageContentRow.transform, "物品分类", _infiniteStorageFilter, delegate(ItemPrimaryCategory category)
		{
			_infiniteStorageFilter.Category = category;
			ItemFilterService.NormalizeForCategory(_infiniteStorageFilter);
			_infiniteStoragePage = 0;
			RefreshInfiniteStorageContentRegions();
		}, asset => storedAssetIds.Contains(asset.id));
		_infiniteStorageCategoryHost.transform.SetSiblingIndex(0);
	}

	private void RefreshInfiniteStorageResultsRegion()
	{
		InfiniteStoragePageSnapshot snapshot = _infiniteStoragePresenter.CaptureSnapshot();
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf || _infiniteStorageResultsHost == null || !snapshot.IsAvailable)
		{
			return;
		}
		IReadOnlyList<VirtualStorageEntry> filteredEntries = GetFilteredInfiniteStorageEntries(snapshot.Entries);
		long resourceUnits = snapshot.Entries.Where(entry => entry.UnitKind == StackUnitKind.ResourceUnits).Sum(entry => entry.StoredUnitCount);
		long instances = snapshot.Entries.Where(entry => entry.UnitKind != StackUnitKind.ResourceUnits).Sum(entry => entry.StoredUnitCount);
		if (_infiniteStorageSummaryText != null)
		{
			_infiniteStorageSummaryText.text = PluginLocalization.Translate($"显示 {filteredEntries.Count:N0} / {snapshot.Entries.Count:N0} 条目；资源 {resourceUnits:N0}，完整物品 {instances:N0} 个。");
		}
		ClearResultHost(_infiniteStorageResultsHost);
		BuildInfiniteStorageGrid(_infiniteStorageResultsHost.transform, filteredEntries);
	}

	private void BuildInfiniteStorageGrid(Transform parent, IReadOnlyList<VirtualStorageEntry> entries)
	{
		GameObject column = CreateObject("InfiniteStorageGridColumn", parent);
		AddVerticalLayout(column, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(column, -1f, -1f, 1f, 1f);
		int pageCount = Math.Max(1, Mathf.CeilToInt(entries.Count / 15f));
		_infiniteStoragePage = Mathf.Clamp(_infiniteStoragePage, 0, pageCount - 1);
		Action<int> changePage = delegate(int value)
		{
			_infiniteStoragePage = value;
			RefreshInfiniteStorageResultsRegion();
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
		string tooltip = asset == null ? "当前资产未解析；条目保持保守状态隔离。" : AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _catalogPresenter.Items);
		bool canDecompose = _infiniteStoragePresenter.CanDecompose(entry.EntryId);
		string interactionTooltip = !_infiniteStorageDecomposeMode
			? string.Format(PluginLocalization.Translate("点击取出当前数量（{0}）。"), PluginLocalization.Translate(metadata.WithdrawUnitLabel))
			: canDecompose
				? string.Format(PluginLocalization.Translate("点击按“{0}”范围分解；产物会直接存回无限储存。"), PluginLocalization.Translate(InfiniteStorageDecomposeScopeLabel(_infiniteStorageDecomposeScope)))
				: entry.UnitKind == StackUnitKind.ResourceUnits || InfiniteStorageDecomposeEligibility.IsMaterialAsset(asset)
					? "材料无法继续分解；关闭分解模式后可正常取出。"
					: "该物品没有可用的原生分解配方；分解模式不会处理它。";
		Button button = CreateButton(parent, string.Empty, delegate
		{
			if (_infiniteStorageDecomposeMode)
			{
				if (!canDecompose) return;
				InfiniteStorageDecomposeResult result = _infiniteStoragePresenter.Decompose(entry.EntryId, _infiniteStorageDecomposeScope);
				if (result.Success)
				{
					SetStatus($"已分解 {result.DecomposedUnitCount:N0} 个物品；{result.ProducedUnitCount:N0} 个产物单位（{result.ProducedAssetCount:N0} 种）已存入无限储存。", 5f, StatusKind.Success);
				}
				else
				{
					SetStatus(InfiniteStorageDecomposeFailureText(result.Failure), 5f, StatusKind.Warning);
				}
				RefreshInfiniteStorageContentRegions();
				return;
			}
			_giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
			WithdrawInfiniteStorageEntry(entry, _giveAmount);
		}, tooltip + "\n\n" + interactionTooltip, 155f, 158f, CheatMenuStyle.Surface, delayTooltip: true);
		if (_infiniteStorageDecomposeMode)
		{
			button.interactable = canDecompose;
			if (canDecompose)
			{
				_fluentControls.MarkDestructiveHover(button);
			}
		}
		AddCardContents(button.transform, displayName, metadata.Text, () => asset == null ? null : _catalogPresenter.GetItemIcon(asset), isVehicle: false);
		if (!_infiniteStorageDecomposeMode)
		{
			Button withdrawAll = CreateButton(button.transform, "取全部", delegate
			{
				WithdrawInfiniteStorageEntry(entry, entry.StoredUnitCount);
			}, "取出此条目的全部单位。", 62f, 26f, CheatMenuStyle.Accent, 10, delayTooltip: true);
			RectTransform rect = withdrawAll.GetComponent<RectTransform>();
			Vector2 anchor = (rect.anchorMax = new Vector2(1f, 1f));
			rect.anchorMin = anchor;
			rect.pivot = new Vector2(1f, 1f);
			rect.anchoredPosition = new Vector2(-5f, -5f);
			rect.sizeDelta = new Vector2(62f, 26f);
			withdrawAll.transform.SetAsLastSibling();
		}
	}

	private static string InfiniteStorageDecomposeScopeLabel(InfiniteStorageDecomposeScope scope)
	{
		return scope switch
		{
			InfiniteStorageDecomposeScope.AssetId => "ID",
			InfiniteStorageDecomposeScope.Stack => "堆叠",
			_ => "单物品"
		};
	}

	private static string InfiniteStorageDecomposeScopeTooltip(InfiniteStorageDecomposeScope scope)
	{
		return scope switch
		{
			InfiniteStorageDecomposeScope.AssetId => "分解无限储存中所有同 ID 的可分解物品，包括不同品质或状态的卡片。",
			InfiniteStorageDecomposeScope.Stack => "分解鼠标指向卡片中的全部数量；不会处理其他同 ID 卡片。",
			_ => "只从鼠标指向的卡片分解一个物品。"
		};
	}

	private static string InfiniteStorageDecomposeFailureText(InfiniteStorageDecomposeFailure failure)
	{
		return failure switch
		{
			InfiniteStorageDecomposeFailure.StorageUnavailable => "无限储存尚未启用或加载完成。",
			InfiniteStorageDecomposeFailure.EntryNotFound => "物品卡片已经失效，请刷新后重试。",
			InfiniteStorageDecomposeFailure.MaterialNotDecomposable => "材料无法继续分解。",
			InfiniteStorageDecomposeFailure.AssetNotFound => "无法解析该物品资产。",
			InfiniteStorageDecomposeFailure.NoSalvageRecipe => "该物品没有原生分解配方。",
			InfiniteStorageDecomposeFailure.UnsupportedSalvageRecipe => "该物品的分解配方不是受支持的单物品分解配方。",
			InfiniteStorageDecomposeFailure.OutputAssetNotFound => "分解产物中存在无法解析的物品。",
			InfiniteStorageDecomposeFailure.PersistenceFailed => "分解结果保存失败，原物品和分解产物已回滚。",
			InfiniteStorageDecomposeFailure.ArithmeticOverflow => "分解数量过大，操作已取消。",
			_ => "分解失败，原物品未被扣除。"
		};
	}

	private void ToggleInfiniteStorageDecomposeScopeDrawer(Button trigger)
	{
		if (_infiniteStorageDecomposeScopeDrawerOpen)
		{
			HideInfiniteStorageDecomposeScopeDrawer();
			return;
		}
		_infiniteStorageDecomposeScopeDrawerOpen = true;
		_infiniteStorageDecomposeScopeTriggerButton = trigger;
		BuildInfiniteStorageDecomposeScopeDrawer();
	}

	private void BuildInfiniteStorageDecomposeScopeDrawer()
	{
		if (_infiniteStorageDecomposeScopeTriggerButton == null || _uiRoot.PopupLayer == null)
		{
			return;
		}
		if (_infiniteStorageDecomposeScopeDrawerRoot != null)
		{
			QueueForDestroy(_infiniteStorageDecomposeScopeDrawerRoot);
		}
		_infiniteStorageDecomposeScopeDrawerRoot = CreateObject("InfiniteStorageDecomposeScopeDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_infiniteStorageDecomposeScopeDrawerRoot, HideInfiniteStorageDecomposeScopeDrawer);
		Image drawerImage = _infiniteStorageDecomposeScopeDrawerRoot.GetComponent<Image>();
		drawerImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(_infiniteStorageDecomposeScopeDrawerRoot);
		_fluentControls.StyleSurface(_infiniteStorageDecomposeScopeDrawerRoot, drawerImage,
			ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		Outline outline = _infiniteStorageDecomposeScopeDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_infiniteStorageDecomposeScopeDrawerPopup = _infiniteStorageDecomposeScopeDrawerRoot.GetComponent<RectTransform>();
		_infiniteStorageDecomposeScopeDrawerPopup.anchorMin = _infiniteStorageDecomposeScopeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
		_infiniteStorageDecomposeScopeDrawerPopup.pivot = new Vector2(0f, 1f);
		_infiniteStorageDecomposeScopeDrawerPopup.sizeDelta = new Vector2(184f, 144f);
		VerticalLayoutGroup layout = _infiniteStorageDecomposeScopeDrawerRoot.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(8, 8, 8, 8);
		layout.spacing = CheatMenuStyle.Theme.Space1;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		foreach (InfiniteStorageDecomposeScope scope in InfiniteStorageDecomposeScopes)
		{
			InfiniteStorageDecomposeScope capturedScope = scope;
			SetLayout(CreateButton(_infiniteStorageDecomposeScopeDrawerRoot.transform,
				InfiniteStorageDecomposeScopeLabel(capturedScope), delegate
				{
					_infiniteStorageDecomposeScope = capturedScope;
					HideInfiniteStorageDecomposeScopeDrawer();
					RebuildInfiniteStoragePage();
				}, InfiniteStorageDecomposeScopeTooltip(capturedScope), 0f, 40f,
				capturedScope == _infiniteStorageDecomposeScope ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 12).gameObject,
				-1f, 40f, 1f);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_infiniteStorageDecomposeScopeDrawerPopup);
		_infiniteStorageDecomposeScopeDrawerRoot.transform.SetAsLastSibling();
		PositionInfiniteStorageDecomposeScopeDrawer();
	}

	private void MaintainInfiniteStorageDecomposeScopeDrawer()
	{
		if (_infiniteStorageDecomposeScopeDrawerRoot == null)
		{
			return;
		}
		PositionInfiniteStorageDecomposeScopeDrawer();
		if (Input.GetMouseButtonDown(0) && _infiniteStorageDecomposeScopeTriggerButton != null &&
			_infiniteStorageDecomposeScopeDrawerPopup != null &&
			!RectTransformUtility.RectangleContainsScreenPoint(_infiniteStorageDecomposeScopeDrawerPopup, Input.mousePosition, null) &&
			!RectTransformUtility.RectangleContainsScreenPoint(_infiniteStorageDecomposeScopeTriggerButton.GetComponent<RectTransform>(), Input.mousePosition, null))
		{
			HideInfiniteStorageDecomposeScopeDrawer();
		}
	}

	private void PositionInfiniteStorageDecomposeScopeDrawer()
	{
		if (_infiniteStorageDecomposeScopeTriggerButton == null || _infiniteStorageDecomposeScopeDrawerPopup == null || _uiRoot.PopupLayer == null)
		{
			return;
		}
		RectTransform trigger = _infiniteStorageDecomposeScopeTriggerButton.GetComponent<RectTransform>();
		RectTransform bounds = _uiRoot.PopupLayer;
		Vector3[] corners = new Vector3[4];
		trigger.GetWorldCorners(corners);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, RectTransformUtility.WorldToScreenPoint(null, corners[0]), null, out Vector2 bottomLeft);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, RectTransformUtility.WorldToScreenPoint(null, corners[1]), null, out Vector2 topLeft);
		Vector2 size = _infiniteStorageDecomposeScopeDrawerPopup.sizeDelta;
		bool openAbove = bottomLeft.y - bounds.rect.yMin < size.y + 8f;
		float x = Mathf.Clamp(bottomLeft.x, bounds.rect.xMin + 8f, bounds.rect.xMax - size.x - 8f);
		_infiniteStorageDecomposeScopeDrawerPopup.pivot = openAbove ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
		_infiniteStorageDecomposeScopeDrawerPopup.anchoredPosition = new Vector2(x, openAbove ? topLeft.y : bottomLeft.y);
	}

	private void HideInfiniteStorageDecomposeScopeDrawer()
	{
		_infiniteStorageDecomposeScopeDrawerOpen = false;
		if (_infiniteStorageDecomposeScopeDrawerRoot != null)
		{
			QueueForDestroy(_infiniteStorageDecomposeScopeDrawerRoot);
		}
		_infiniteStorageDecomposeScopeDrawerRoot = null;
		_infiniteStorageDecomposeScopeDrawerPopup = null;
		_infiniteStorageDecomposeScopeTriggerButton = null;
	}

	private void DisposeInfiniteStorageDecomposeScopeDrawer()
	{
		_infiniteStorageDecomposeScopeDrawerOpen = false;
		if (_infiniteStorageDecomposeScopeDrawerRoot != null)
		{
			UnityEngine.Object.Destroy(_infiniteStorageDecomposeScopeDrawerRoot);
		}
		_infiniteStorageDecomposeScopeDrawerRoot = null;
		_infiniteStorageDecomposeScopeDrawerPopup = null;
		_infiniteStorageDecomposeScopeTriggerButton = null;
	}

	private void WithdrawInfiniteStorageEntry(VirtualStorageEntry entry, long amount)
	{
		long moved = _infiniteStoragePresenter.Withdraw(entry.EntryId, amount);
		string message = entry.UnitKind == StackUnitKind.ResourceUnits
			? $"已取出 {moved:N0} 单位。"
			: $"已取出 {moved:N0} 个完整物品。";
		SetStatus(moved > 0 ? message : "原生背包没有可用空间。", 3f, StatusKind.Info);
		RefreshInfiniteStorageContentRegions();
	}

	private IReadOnlyList<VirtualStorageEntry> GetFilteredInfiniteStorageEntries(IReadOnlyList<VirtualStorageEntry> entries)
	{
		List<VirtualStorageEntry> results = new List<VirtualStorageEntry>();
		foreach (VirtualStorageEntry entry in entries)
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
		foreach (ItemAsset asset in _catalogPresenter.Items)
		{
			if (asset != null && !_infiniteStorageAssetsById.ContainsKey(asset.id))
			{
				_infiniteStorageAssetsById.Add(asset.id, asset);
			}
		}
	}
}
