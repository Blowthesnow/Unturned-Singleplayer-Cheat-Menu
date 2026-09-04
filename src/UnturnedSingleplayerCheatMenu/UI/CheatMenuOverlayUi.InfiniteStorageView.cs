using System;
using System.Collections.Generic;
using System.Linq;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.Composition;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using UnturnedSingleplayerCheatMenu.Services;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private bool _infiniteStorageDestructionMode;
	private bool _infiniteStorageDecomposeMode;
	private string _snapshotDecompositionScope = "single";

	private sealed class InfiniteStorageCardEntry
	{
		internal InfiniteStorageCardEntry(ItemGroupView group, SnapshotBucketDisplayGroup displayGroup)
		{
			Group = group;
			DisplayGroup = displayGroup;
		}

		internal ItemGroupView Group { get; }
		internal SnapshotBucketDisplayGroup DisplayGroup { get; }
	}

	private void BuildInfiniteStoragePanel(Transform panel)
	{
		InfiniteStoragePageSnapshot snapshot = _infiniteStoragePresenter.CaptureSnapshot();
		if (!snapshot.IsAvailable)
		{
			CreateEmptyState(panel, "无限储存不可用", "快照运行时尚未初始化。");
			return;
		}

		GameObject topRow = CreateRow(panel, 40f, 7f);
		CreateToggleAction(topRow.transform, "启用无限储存", snapshot.Enabled,
			value => _infiniteStoragePresenter.SetEnabled(value));
		CreateLabel(topRow.transform, "搜索", 13, CheatMenuStyle.Text, 64f);
		CreateSearchInput(topRow.transform, _infiniteStorageFilter.SearchQuery, value =>
		{
			_infiniteStorageFilter.SearchQuery = value;
			_infiniteStoragePage = 0;
			RefreshInfiniteStorageResultsRegion();
		}, 220f);
		CreateLabel(topRow.transform, "取出实例数", 13, CheatMenuStyle.Text,
			CheatMenuStyle.InfiniteStorageWithdrawLabelWidth);
		CreateButton(topRow.transform, "−", delegate
		{
			_giveAmount = (byte)Math.Max(1, ParseClamped(_itemAmountText, 1, 255, _giveAmount) - 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "每次少取一个完全相同的原生快照实例。", 36f, 34f);
		RegisterItemAmountInput(CreateInput(topRow.transform, _itemAmountText, value =>
		{
			_itemAmountText = value;
			if (int.TryParse(value, out int parsed)) _giveAmount = (byte)Mathf.Clamp(parsed, 1, 255);
		}, 62f));
		CreateButton(topRow.transform, "+", delegate
		{
			_giveAmount = (byte)Math.Min(255, ParseClamped(_itemAmountText, 1, 255, _giveAmount) + 1);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
		}, "每次多取一个完全相同的原生快照实例。", 36f, 34f);
		Button filterButton = CreateButton(topRow.transform, GetInfiniteStorageFilterButtonLabel(),
			ShowInfiniteStorageFilterPanel, "筛选物品组。", 92f, 34f, CheatMenuStyle.Surface, 12);
		_infiniteStorageFilterButtonText = filterButton.GetComponentInChildren<Text>(true);

		GameObject optionRow = CreateRow(panel, 40f, 7f);
		CreateToggleAction(optionRow.transform, "自动拾取优先存入", snapshot.PickupPriorityEnabled,
			value => _infiniteStoragePresenter.SetPickupPriority(value));
		CreateInfiniteStorageDestructionModeToggle(optionRow.transform);
		if (FindInfiniteStorageDecompositionAction(snapshot.ModuleActions) != null)
		{
			CreateInfiniteStorageDecompositionModeToggle(optionRow.transform);
		}
		if (FindInfiniteStorageDecompositionAction(snapshot.ModuleActions) != null)
		{
			CreateButton(optionRow.transform, PluginLocalization.Translate("分解范围：") +
				PluginLocalization.Translate(ScopeLabel(_snapshotDecompositionScope)), delegate
			{
				_snapshotDecompositionScope = _snapshotDecompositionScope == "single" ? "bucket" :
					_snapshotDecompositionScope == "bucket" ? "asset" : "single";
				RebuildInfiniteStoragePage();
			}, "单实例 → 当前快照桶 → 当前物品组循环。", 168f, 34f, CheatMenuStyle.Surface, 11);
		}

		GameObject actionRow = CreateRow(panel, 40f, 7f);
		CreateButton(actionRow.transform, "存储当前携带物品", delegate
		{
			int moved = _infiniteStoragePresenter.StoreAllCarried();
			SetStatus(moved > 0 ? $"已存储 {moved:N0} 个原生物品快照。" :
				"没有可存储物品、保存失败，或快照运行时未就绪。", 5f,
				moved > 0 ? StatusKind.Success : StatusKind.Info);
			RefreshInfiniteStorageContentRegions();
		}, "每个背包物品均按原生编号、数量、品质和状态字节完整存储。",
			184f, 40f, CheatMenuStyle.Success, 13);
		foreach (SnapshotModuleAction action in snapshot.ModuleActions
			.Where(value => value.Kind == SnapshotModuleKind.GlobalAction))
		{
			SnapshotModuleAction captured = action;
			CreateButton(actionRow.transform, captured.Label, delegate
			{
				SnapshotModuleExecutionResult result = _infiniteStoragePresenter.ExecuteModule(
					captured.Id, SnapshotModuleRequest.Empty);
				SetStatus(result.Success ? result.Message :
					PluginLocalization.SnapshotModuleFailureMessage(result.ErrorCode), 5f,
					result.Success ? StatusKind.Success : StatusKind.Warning);
				RefreshInfiniteStorageContentRegions();
			}, captured.Tooltip, 190f, 40f, CheatMenuStyle.Accent, 12);
		}

		_infiniteStorageSummaryText = CreateLabel(panel, string.Empty, 14, CheatMenuStyle.Text);
		_infiniteStorageContentRow = CreateRow(panel, 0f, 10f);
		SetLayout(_infiniteStorageContentRow, -1f, -1f, -1f, 1f);
		HashSet<ushort> storedAssetIds = new HashSet<ushort>(snapshot.Groups.Select(group => group.AssetId));
		_infiniteStorageCategoryHost = BuildItemCategorySidebar(_infiniteStorageContentRow.transform,
			"物品分类", _infiniteStorageFilter, category =>
			{
				_infiniteStorageFilter.Category = category;
				ItemFilterService.NormalizeForCategory(_infiniteStorageFilter);
				_infiniteStoragePage = 0;
				RefreshInfiniteStorageContentRegions();
			}, asset => storedAssetIds.Contains(asset.id));
		_infiniteStorageResultsHost = CreateObject("InfiniteStorageResultsHost",
			_infiniteStorageContentRow.transform, typeof(Image));
		_infiniteStorageResultsHost.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		AddVerticalLayout(_infiniteStorageResultsHost, CheatMenuStyle.Theme.Space2,
			new RectOffset(8, 8, 8, 8));
		SetLayout(_infiniteStorageResultsHost, -1f, -1f, 1f, 1f);
		RefreshInfiniteStorageResultsRegion();
	}

	private void CreateInfiniteStorageDestructionModeToggle(Transform parent)
	{
		CreateToggleAction(parent, "破坏模式", _infiniteStorageDestructionMode, value =>
		{
			if (value)
			{
				ShowInfiniteStorageDestructionWarning();
				return;
			}
			_infiniteStorageDestructionMode = false;
			SetStatus("破坏模式已关闭。", 4f, StatusKind.Info);
			RebuildInfiniteStoragePage();
		}, "开启后点击物品卡片会永久破坏同一 ID 的全部精确快照，并把该物品 ID 加入自动拾取黑名单。",
			CheatMenuStyle.Danger);
	}

	private void CreateInfiniteStorageDecompositionModeToggle(Transform parent)
	{
		CreateToggleAction(parent, "分解模式", _infiniteStorageDecomposeMode, value =>
		{
			_infiniteStorageDecomposeMode = value;
			if (value) _infiniteStorageDestructionMode = false;
			SetStatus(value
				? "分解模式已开启；只有可分解的非材料物品会在悬停时变红。"
				: "分解模式已关闭。", 4f, StatusKind.Info);
			RebuildInfiniteStoragePage();
		}, "开启后只有带原生分解配方的非材料物品卡片会在悬停时变红；点击卡片按所选范围分解，分解产物直接存回无限储存。");
	}

	private void EnableInfiniteStorageDestructionMode()
	{
		_infiniteStorageDestructionMode = true;
		_infiniteStorageDecomposeMode = false;
		SetStatus("破坏模式已开启；点击红色悬停卡片将永久破坏快照并自动加入黑名单。",
			5f, StatusKind.Warning);
		RebuildInfiniteStoragePage();
	}

	private void ShowInfiniteStorageDestructionWarning()
	{
		if (_infiniteStoragePresenter.DestructionWarningAcknowledged)
		{
			EnableInfiniteStorageDestructionMode();
			return;
		}
		PrepareModalOpen();
		HideConfirmDialog();
		_confirmRoot = CreateObject("DestructionWarningOverlay", _uiRoot.ModalLayer, typeof(Image));
		_fluentHandles.RegisterScope(_confirmRoot);
		Stretch(_confirmRoot.GetComponent<RectTransform>());
		_confirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);
		GameObject dialog = CreateObject("DestructionWarningDialog", _confirmRoot.transform, typeof(Image));
		Image dialogImage = dialog.GetComponent<Image>();
		dialogImage.color = CheatMenuStyle.Panel;
		_fluentControls.StyleSurface(dialog, dialogImage,
			ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusPanel);
		RectTransform dialogRect = dialog.GetComponent<RectTransform>();
		dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
		dialogRect.pivot = new Vector2(0.5f, 0.5f);
		dialogRect.sizeDelta = new Vector2(560f, 250f);
		VerticalLayoutGroup layout = dialog.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(24, 24, 16, 16);
		layout.spacing = CheatMenuStyle.Theme.Space3;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		CreateText(dialog.transform, "确认开启破坏模式？", 18, CheatMenuStyle.Text,
			TextAnchor.MiddleLeft, FontStyle.Bold);
		CreateText(dialog.transform,
			"破坏会永久删除无限储存中同一物品 ID 的全部精确快照，无法撤销。破坏范围固定为 ID。",
			13, CheatMenuStyle.Danger, TextAnchor.MiddleLeft);
		CreateText(dialog.transform,
			"破坏成功后，该物品 ID 会自动加入自动拾取黑名单。此警告确认一次后不再弹出。",
			12, CheatMenuStyle.Muted, TextAnchor.MiddleLeft);
		GameObject actions = CreateRow(dialog.transform, 40f, CheatMenuStyle.Theme.Space2);
		CreateFlexibleSpacer(actions.transform);
		Button cancel = CreateButton(actions.transform, "取消", CancelInfiniteStorageDestructionWarning,
			"保持破坏模式关闭并关闭警告。", 104f, 36f, CheatMenuStyle.Surface);
		CreateButton(actions.transform, "确认开启", delegate
		{
			bool persisted = _infiniteStoragePresenter.AcknowledgeDestructionWarning();
			HideConfirmDialog();
			if (persisted)
				EnableInfiniteStorageDestructionMode();
			else
			{
				SetStatus("破坏模式警告确认状态保存失败；破坏模式仍保持关闭。", 5f, StatusKind.Warning);
				RebuildInfiniteStoragePage();
			}
		}, "确认理解永久删除和自动加入黑名单的行为，并开启破坏模式。",
			112f, 36f, CheatMenuStyle.Danger);
		_uiRoot.RegisterModal(_confirmRoot, CancelInfiniteStorageDestructionWarning, cancel.gameObject);
		AnimateModalEnter(_confirmRoot, dialog.transform);
	}

	private void CancelInfiniteStorageDestructionWarning()
	{
		HideConfirmDialog();
		RebuildInfiniteStoragePage();
	}

	private string GetInfiniteStorageFilterButtonLabel() =>
		_infiniteStorageFilter.ActiveFilterCount == 0 ? "筛选" :
			"筛选 " + _infiniteStorageFilter.ActiveFilterCount;

	private void RefreshInfiniteStorageContentRegions()
	{
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf ||
			!_infiniteStoragePresenter.CaptureSnapshot().IsAvailable) return;
		if (_infiniteStorageFilterButtonText != null)
			_infiniteStorageFilterButtonText.text = PluginLocalization.Translate(GetInfiniteStorageFilterButtonLabel());
		RefreshInfiniteStorageCategoryRegion();
		RefreshInfiniteStorageResultsRegion();
	}

	private void RefreshInfiniteStorageCategoryRegion()
	{
		InfiniteStoragePageSnapshot snapshot = _infiniteStoragePresenter.CaptureSnapshot();
		if (_infiniteStorageContentRow == null || !snapshot.IsAvailable) return;
		if (_infiniteStorageCategoryHost != null) QueueForDestroy(_infiniteStorageCategoryHost);
		HashSet<ushort> storedAssetIds = new HashSet<ushort>(snapshot.Groups.Select(group => group.AssetId));
		_infiniteStorageCategoryHost = BuildItemCategorySidebar(_infiniteStorageContentRow.transform,
			"物品分类", _infiniteStorageFilter, category =>
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
		if (_infiniteStorageRoot == null || !_infiniteStorageRoot.activeSelf ||
			_infiniteStorageResultsHost == null || !snapshot.IsAvailable) return;
		IReadOnlyList<ItemGroupView> groups = FilterGroups(snapshot.Groups);
		long instanceCount = SaturatingInstanceCount(snapshot.Groups);
		if (_infiniteStorageSummaryText != null)
			_infiniteStorageSummaryText.text = PluginLocalization.Translate(
				$"物品组 {groups.Count:N0} / {snapshot.Groups.Count:N0}；快照实例 {instanceCount:N0}。原生数量字段始终保持不透明，内核不会拆分。");
		ClearResultHost(_infiniteStorageResultsHost);
		BuildInfiniteStorageGrid(_infiniteStorageResultsHost.transform, groups, snapshot.ModuleActions);
	}

	private void BuildInfiniteStorageGrid(Transform parent, IReadOnlyList<ItemGroupView> groups,
		IReadOnlyList<SnapshotModuleAction> actions)
	{
		List<InfiniteStorageCardEntry> cards = new List<InfiniteStorageCardEntry>();
		foreach (ItemGroupView group in groups)
		{
			_infiniteStorageAssetsById.TryGetValue(group.AssetId, out ItemAsset asset);
			bool resolved = asset != null && asset.GUID == group.AssetGuid;
			bool mergeHiddenQuality = resolved && !asset.showQuality && !_infiniteStorageDecomposeMode;
			foreach (SnapshotBucketDisplayGroup displayGroup in
				SnapshotBucketDisplayAggregator.Build(group, mergeHiddenQuality))
				cards.Add(new InfiniteStorageCardEntry(group, displayGroup));
		}

		GameObject column = CreateObject("InfiniteStorageGridColumn", parent);
		AddVerticalLayout(column, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0));
		SetLayout(column, -1f, -1f, 1f, 1f);
		const int columnCount = 5;
		const int rowCount = 3;
		const int pageSize = 15;
		int pageCount = Math.Max(1, Mathf.CeilToInt((float)cards.Count / pageSize));
		_infiniteStoragePage = Mathf.Clamp(_infiniteStoragePage, 0, pageCount - 1);
		Action<int> changePage = value =>
		{
			_infiniteStoragePage = value;
			RefreshInfiniteStorageResultsRegion();
		};
		BuildPagination(column.transform, _infiniteStoragePage, pageCount, changePage);
		AddPageScrollHandler(column, _infiniteStoragePage, pageCount, changePage);
		if (cards.Count == 0)
		{
			CreateEmptyState(column.transform,
				string.IsNullOrWhiteSpace(_infiniteStorageFilter.SearchQuery) ? "当前分类没有已存储物品。" : "没有匹配的物品。",
				string.IsNullOrWhiteSpace(_infiniteStorageFilter.SearchQuery) ? "存储物品后会显示在这里。" : "请清空搜索或尝试其他关键词。");
			return;
		}

		GameObject grid = CreateObject("InfiniteStorageGrid", column.transform);
		SetLayout(grid, -1f, GetCardGridHeight(rowCount), -1f, 1f);
		GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
		layout.padding = new RectOffset(4, 4, 4, 4);
		layout.spacing = new Vector2(CheatMenuStyle.RowSpacing, CheatMenuStyle.RowSpacing);
		layout.cellSize = new Vector2(CheatMenuStyle.CardWidth, CheatMenuStyle.CardHeight);
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = columnCount;
		layout.childAlignment = TextAnchor.UpperCenter;
		int start = _infiniteStoragePage * pageSize;
		int end = Math.Min(start + pageSize, cards.Count);
		for (int index = start; index < end; index++)
		{
			InfiniteStorageCardEntry entry = cards[index];
			CreateInfiniteStorageBucketCard(grid.transform, entry.Group, entry.DisplayGroup, actions);
		}
	}

	private void CreateInfiniteStorageBucketCard(Transform parent, ItemGroupView group,
		SnapshotBucketDisplayGroup displayGroup, IReadOnlyList<SnapshotModuleAction> actions)
	{
		SnapshotBucketView bucket = displayGroup.Representative;
		_infiniteStorageAssetsById.TryGetValue(group.AssetId, out ItemAsset asset);
		bool resolved = asset != null && asset.GUID == group.AssetGuid;
		string displayName = ResolveInfiniteStorageGroupDisplayName(
			resolved ? GetItemDisplayName(asset) : string.Empty, asset != null, resolved);
		SnapshotBucketDisplayFacts displayFacts = resolved &&
			_ammoCompatibilityIndex.TryGetAmmoContainerCapacity(asset, out int ammoCapacity)
			? SnapshotBucketDisplayFacts.AmmoContainer(ammoCapacity)
			: SnapshotBucketDisplayFacts.NativeAmount;
		SnapshotBucketMetadata metadata = SnapshotBucketMetadata.Build(bucket.Snapshot, displayGroup.TotalCount,
			resolved && asset.showQuality, displayFacts);
		string tooltip = resolved
			? AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription,
				_ammoCompatibilityIndex, _catalogPresenter.Items)
			: PluginLocalization.Translate("资产缺失或编号到资产标识的映射不匹配；物品组被保留为只读且禁止取出。");
		string marker = displayGroup.IsDefaultTemplate ? PluginLocalization.Translate(" · 默认满状态") : string.Empty;
		bool canWithdraw = resolved && _infiniteStoragePresenter.CanWithdraw(group.Identity);
		SnapshotModuleAction decompositionAction = FindInfiniteStorageDecompositionAction(actions);
		SnapshotModuleRequest decompositionRequest = new SnapshotModuleRequest(group.Identity, bucket.Snapshot,
			bucket.Count, _snapshotDecompositionScope);
		bool canDecompose = decompositionAction != null &&
			_infiniteStoragePresenter.CanExecuteModule(decompositionAction.Id, decompositionRequest);
		bool canDestroy = resolved && _infiniteStoragePresenter.CanDestroy(group.Identity);
		string preservedQualityTooltip = displayGroup.Buckets.Count > 1
			? "\n\n" + PluginLocalization.Translate(
				$"品质分别按 {displayGroup.Buckets.Count:N0} 个精确快照桶保留。")
			: string.Empty;
		string inventoryCountTooltip = group.TotalCount > displayGroup.TotalCount
			? "\n\n" + PluginLocalization.Translate(
				$"当前卡片显示实例 ×{displayGroup.TotalCount:N0}；同 ID 总库存 ×{group.TotalCount:N0}。")
			: string.Empty;
		string interactionTooltip = _infiniteStorageDestructionMode
			? PluginLocalization.Translate(
				"点击卡片会永久破坏同一物品 ID 的全部精确快照；成功后该 ID 会自动加入自动拾取黑名单。")
			: !_infiniteStorageDecomposeMode
			? PluginLocalization.Translate("取出所选精确快照桶。")
			: canDecompose
				? string.Format(PluginLocalization.Translate("点击按“{0}”范围分解；产物会直接存回无限储存。"),
					PluginLocalization.Translate(ScopeLabel(_snapshotDecompositionScope)))
				: PluginLocalization.Translate("该物品没有可用的原生分解配方；分解模式不会处理它。");
		Button card = CreateButton(parent, string.Empty, delegate
		{
			if (_infiniteStorageDestructionMode)
			{
				if (!canDestroy) return;
				DestroyInfiniteStorageBucket(group.Identity, bucket.Snapshot);
				return;
			}
			if (_infiniteStorageDecomposeMode)
			{
				if (!canDecompose) return;
				SnapshotModuleExecutionResult result = _infiniteStoragePresenter.ExecuteModule(
					decompositionAction.Id, decompositionRequest);
				SetStatus(result.Success ? result.Message :
					PluginLocalization.SnapshotModuleFailureMessage(result.ErrorCode), 5f,
					result.Success ? StatusKind.Success : StatusKind.Warning);
				RefreshInfiniteStorageContentRegions();
				return;
			}
			_giveAmount = (byte)ParseClamped(_itemAmountText, 1, 255, _giveAmount);
			_itemAmountText = _giveAmount.ToString();
			UpdateItemAmountInput();
			WithdrawInfiniteStorageBuckets(group.Identity, displayGroup.Buckets, _giveAmount);
		}, tooltip + "\n\n" + PluginLocalization.Translate(metadata.Text) + marker +
			preservedQualityTooltip + inventoryCountTooltip + "\n\n" + interactionTooltip,
			CheatMenuStyle.CardWidth, CheatMenuStyle.CardHeight,
			resolved ? CheatMenuStyle.Surface : CheatMenuStyle.SurfaceInset, delayTooltip: true);
		card.interactable = _infiniteStorageDestructionMode ? canDestroy :
			_infiniteStorageDecomposeMode ? canDecompose : canWithdraw;
		if ((_infiniteStorageDestructionMode && canDestroy) ||
			(_infiniteStorageDecomposeMode && canDecompose)) _fluentControls.MarkDestructiveHover(card);
		AddCardContents(card.transform, displayName,
			BuildInfiniteStorageCardMeta(group.AssetId, displayGroup.TotalCount, group.TotalCount),
			() => resolved ? _catalogPresenter.GetItemIcon(asset) : null, isVehicle: false);

		if (_infiniteStorageDestructionMode || _infiniteStorageDecomposeMode) return;
		Button takeAll = CreateButton(card.transform, "取全部", delegate
		{
			WithdrawInfiniteStorageBuckets(group.Identity, displayGroup.Buckets, displayGroup.TotalCount);
		}, "取出此卡片中全部原始精确快照实例。", 92f, 25f, CheatMenuStyle.Accent, 10, delayTooltip: true);
		RectTransform takeAllRect = takeAll.GetComponent<RectTransform>();
		takeAllRect.anchorMin = takeAllRect.anchorMax = new Vector2(1f, 1f);
		takeAllRect.pivot = new Vector2(1f, 1f);
		takeAllRect.anchoredPosition = new Vector2(-5f, -5f);
		takeAllRect.sizeDelta = new Vector2(92f, 25f);
		takeAll.interactable = canWithdraw;
		takeAll.transform.SetAsLastSibling();
	}

	private static string BuildInfiniteStorageCardMeta(ushort assetId, long bucketCount, long groupTotal)
	{
		return groupTotal > bucketCount
			? $"ID {assetId}  ·  ×{bucketCount:N0}/总{groupTotal:N0}"
			: $"ID {assetId}  ·  ×{bucketCount:N0}";
	}

	private void DestroyInfiniteStorageBucket(ItemGroupIdentity identity, ItemSnapshot snapshot)
	{
		InfiniteStorageDestructionResult result = _infiniteStoragePresenter.Destroy(identity, snapshot,
			SnapshotDestructionScope.AssetId);
		string message;
		StatusKind statusKind;
		if (result.Success)
		{
			message = result.BlacklistAdded
				? $"已破坏 {result.DestroyedCount:N0} 个快照实例；物品 ID {identity.AssetId} 已加入自动拾取黑名单。"
				: $"已破坏 {result.DestroyedCount:N0} 个快照实例；物品 ID {identity.AssetId} 已在自动拾取黑名单中。";
			statusKind = StatusKind.Success;
		}
		else if (result.StorageStateUncertain)
		{
			message = "破坏结果需要对账；自动拾取黑名单已保留，无限储存已停止继续变更。";
			statusKind = StatusKind.Error;
		}
		else if (!result.BlacklistRollbackSucceeded)
		{
			message = "破坏失败且自动拾取黑名单回滚失败；仓库物品仍保留，请手动检查黑名单。";
			statusKind = StatusKind.Error;
		}
		else
		{
			message = "破坏失败；仓库快照和自动拾取黑名单均保持原状。";
			statusKind = StatusKind.Warning;
		}
		SetStatus(message, 6f, statusKind);
		RefreshInfiniteStorageContentRegions();
	}

	private static SnapshotModuleAction FindInfiniteStorageDecompositionAction(
		IEnumerable<SnapshotModuleAction> actions) =>
		(actions ?? Array.Empty<SnapshotModuleAction>()).FirstOrDefault(action =>
			action.Kind == SnapshotModuleKind.BucketAction &&
			string.Equals(action.Id, "decomposition", StringComparison.Ordinal));

	private IReadOnlyList<ItemGroupView> FilterGroups(IReadOnlyList<ItemGroupView> groups)
	{
		List<ItemGroupView> result = new List<ItemGroupView>();
		foreach (ItemGroupView group in groups ?? Array.Empty<ItemGroupView>())
		{
			if (_infiniteStorageAssetsById.TryGetValue(group.AssetId, out ItemAsset asset))
			{
				if (MatchesItemForFilter(asset, _infiniteStorageFilter)) result.Add(group);
			}
			else if (_infiniteStorageFilter.ActiveFilterCount == 0 ||
				group.AssetId.ToString().IndexOf(_infiniteStorageFilter.SearchQuery ?? string.Empty,
					StringComparison.OrdinalIgnoreCase) >= 0)
			{
				result.Add(group);
			}
		}
		return result;
	}

	private void WithdrawInfiniteStorageBuckets(ItemGroupIdentity identity,
		IReadOnlyList<SnapshotBucketView> buckets, long requested)
	{
		long moved = 0;
		long remaining = Math.Max(0, requested);
		foreach (SnapshotBucketView bucket in buckets ?? Array.Empty<SnapshotBucketView>())
		{
			if (remaining <= 0 || bucket?.Snapshot == null || bucket.Count <= 0) break;
			long target = Math.Min(remaining, bucket.Count);
			long current = _infiniteStoragePresenter.Withdraw(identity, bucket.Snapshot, target);
			moved += current;
			remaining -= current;
			if (current < target) break;
		}
		SetStatus(moved > 0 ? $"已取出 {moved:N0} 个精确快照实例。" :
			"未取出：背包空间不足、资产冲突或保存失败。", 4f,
			moved > 0 ? StatusKind.Success : StatusKind.Warning);
		RefreshInfiniteStorageContentRegions();
	}

	private static long SaturatingInstanceCount(IEnumerable<ItemGroupView> groups)
	{
		long total = 0;
		foreach (ItemGroupView group in groups ?? Array.Empty<ItemGroupView>())
		{
			try { total = checked(total + group.TotalCount); }
			catch (OverflowException) { return long.MaxValue; }
		}
		return total;
	}

	private static string ScopeLabel(string scope) => scope == "asset" ? "物品组" :
		scope == "bucket" ? "当前快照桶" : "单实例";

	private static string ResolveInfiniteStorageGroupDisplayName(string resolvedAssetName,
		bool assetExists, bool identityMatches)
	{
		if (!assetExists) return PluginLocalization.Translate("未解析物品组（只读）");
		if (!identityMatches) return PluginLocalization.Translate("资产标识冲突的物品组（只读）");
		return resolvedAssetName ?? string.Empty;
	}

	private void RebuildInfiniteStorageAssetIndex()
	{
		_infiniteStorageAssetsById.Clear();
		foreach (ItemAsset asset in _catalogPresenter.Items)
			if (asset != null && !_infiniteStorageAssetsById.ContainsKey(asset.id))
				_infiniteStorageAssetsById.Add(asset.id, asset);
	}

}
