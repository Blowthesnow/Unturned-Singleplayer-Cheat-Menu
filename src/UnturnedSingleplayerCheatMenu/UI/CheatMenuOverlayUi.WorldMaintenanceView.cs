using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private Transform _worldMaintenanceParent;
	private GameObject _worldMaintenanceRoot;
	private GameObject _worldMaintenanceConfirmRoot;
	private WorldMaintenancePreview _worldMaintenancePendingPreview;
	private Text _worldMaintenanceConfirmSummary;
	private Text _worldMaintenanceProgressText;
	private Image _worldMaintenanceProgressFill;
	private Button _worldMaintenanceStopButton;
	private int _worldMaintenanceRenderedVersion = -1;

	private float WorldMaintenanceRadius
	{
		get => WorldMaintenancePolicy.NormalizeRadius(_state.WorldMaintenanceRadius);
		set => _state.WorldMaintenanceRadius = WorldMaintenancePolicy.NormalizeRadius(value);
	}

	private void BuildWorldMaintenanceView(Transform parent)
	{
		CreateSection(parent, "世界维护");
		_worldMaintenanceParent = parent;
		BuildWorldMaintenanceRoot();
	}

	private void BuildWorldMaintenanceRoot()
	{
		if (_worldMaintenanceParent == null) return;
		_worldMaintenanceRoot = CreateObject("WorldMaintenanceRoot", _worldMaintenanceParent);
		_fluentHandles.RegisterScope(_worldMaintenanceRoot);
		AddVerticalLayout(_worldMaintenanceRoot, CheatMenuStyle.Theme.Space3, new RectOffset(0, 0, 0, 0));
		WorldMaintenanceDashboardSnapshot snapshot = _worldMaintenancePresenter.CaptureDashboard(WorldMaintenanceRadius);
		if (snapshot == null)
		{
			CreateEmptyState(_worldMaintenanceRoot.transform, "世界维护不可用", "运行时服务尚未完成初始化。");
			_worldMaintenanceRenderedVersion = _worldMaintenancePresenter.SnapshotVersion;
			return;
		}
		bool running = snapshot.Progress?.State == WorldMaintenanceRunState.Running;
		BuildMaintenanceRangeRow(_worldMaintenanceRoot.transform, running);

		GameObject columns = CreateRow(_worldMaintenanceRoot.transform, 0f, CheatMenuStyle.Theme.Space3);
		columns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
		Transform resourceColumn = CreateMaintenanceColumn(columns.transform, "WorldMaintenanceResourceColumn");
		Transform cleanupColumn = CreateMaintenanceColumn(columns.transform, "WorldMaintenanceCleanupColumn");
		BuildResourceMaintenanceCard(resourceColumn, snapshot, running);
		BuildItemRestockCard(resourceColumn, snapshot, running);
		BuildZombieMaintenanceCard(cleanupColumn, snapshot, running);
		BuildGroundItemMaintenanceCard(cleanupColumn, snapshot, running);
		BuildMaintenanceProgress(_worldMaintenanceRoot.transform, snapshot.Progress);
		_worldMaintenanceRenderedVersion = snapshot.SnapshotVersion;
	}

	private Transform CreateMaintenanceColumn(Transform parent, string name)
	{
		GameObject column = CreateObject(name, parent);
		AddVerticalLayout(column, CheatMenuStyle.Theme.Space3, new RectOffset(0, 0, 0, 0));
		SetLayout(column, 360f, -1f, 1f, 0f);
		column.GetComponent<LayoutElement>().minWidth = 360f;
		return column.transform;
	}

	private void BuildMaintenanceRangeRow(Transform parent, bool running)
	{
		GameObject row = CreateRow(parent, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		CreateLabel(row.transform, "附近范围", 13, CheatMenuStyle.Muted, 88f);
		foreach (float radius in new[] { 100f, 250f, 500f })
		{
			float captured = radius;
			bool selected = Math.Abs(WorldMaintenanceRadius - radius) < 0.1f;
			Button button = CreateButton(row.transform, selected ? $"[{radius:0}m]" : $"{radius:0}m", delegate
			{
				WorldMaintenanceRadius = captured;
				RebuildWorldMaintenanceRoot();
			}, $"将附近维护操作的水平范围设置为 {radius:0} 米。", 86f,
				CheatMenuStyle.Theme.ControlDefault, selected ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceRaised);
			button.interactable = !running;
		}
		CreateFlexibleSpacer(row.transform);
	}

	private void BuildResourceMaintenanceCard(Transform parent, WorldMaintenanceDashboardSnapshot snapshot, bool running)
	{
		GameObject card = CreateWorldCard(parent, "NaturalResourceMaintenanceCard", -1f);
		CreateLabel(card.transform, "自然资源刷新", 17, CheatMenuStyle.Text);
		if (!snapshot.ResourceRefreshSupported)
		{
			CreateFlowLabel(card.transform, snapshot.ResourceCapabilityMessage, CheatMenuStyle.Warning);
		}
		else
		{
			CreateFlowLabel(card.transform,
				$"附近：枯竭 {snapshot.NearbyDeadResources} / 可恢复 {snapshot.NearbyRecoverableResources} / 被阻挡 {snapshot.NearbyBlockedResources}",
				CheatMenuStyle.Text);
			CreateFlowLabel(card.transform,
				$"全图：枯竭 {snapshot.WholeDeadResources} / 可恢复 {snapshot.WholeRecoverableResources} / 被阻挡 {snapshot.WholeBlockedResources}",
				CheatMenuStyle.Muted);
		}
		GameObject actions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		Button nearby = CreateMaintenanceButton(actions.transform, "刷新附近资源", CheatMenuStyle.Success,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.RefreshNaturalResources,
				WorldMaintenanceScopeKind.Nearby, requiresConfirmation: false),
			"立即恢复附近枯竭且未被玩家、敌人或建筑阻挡的树木与岩石。");
		Button all = CreateMaintenanceButton(actions.transform, "刷新全图资源", CheatMenuStyle.Accent,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.RefreshNaturalResources,
				WorldMaintenanceScopeKind.WholeMap, requiresConfirmation: true),
			"预览并确认恢复全图可安全恢复的枯竭自然资源。");
		nearby.interactable = all.interactable = snapshot.ResourceRefreshSupported && !running;
	}

	private void BuildZombieMaintenanceCard(Transform parent, WorldMaintenanceDashboardSnapshot snapshot, bool running)
	{
		GameObject card = CreateWorldCard(parent, "ZombieMaintenanceCard", -1f);
		CreateLabel(card.transform, "丧尸清除", 17, CheatMenuStyle.Text);
		CreateFlowLabel(card.transform, $"附近普通存活：{snapshot.NearbyZombies}", CheatMenuStyle.Text);
		CreateFlowLabel(card.transform,
			$"已加载普通存活：{snapshot.LoadedZombies}    特殊保护：{snapshot.ProtectedZombies}", CheatMenuStyle.Muted);
		CreateFlowLabel(card.transform,
			$"附近生成空缺：{snapshot.NearbyDeadZombieSlots}    已加载生成空缺：{snapshot.LoadedDeadZombieSlots}",
			CheatMenuStyle.Muted);
		GameObject actions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		Button nearby = CreateMaintenanceButton(actions.transform, "清除附近丧尸", CheatMenuStyle.Warning,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.ClearZombies,
				WorldMaintenanceScopeKind.Nearby, requiresConfirmation: true),
			"预览并确认清除附近存活丧尸；默认保护 Boss、Mega 和信标事件丧尸。");
		Button loaded = CreateMaintenanceButton(actions.transform, "清除已加载丧尸", CheatMenuStyle.Danger,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.ClearZombies,
				WorldMaintenanceScopeKind.LoadedWorld, requiresConfirmation: true),
			"预览并确认清除当前已经加载的存活丧尸，不宣称清除未加载区域。");
		nearby.interactable = loaded.interactable = !running;
		GameObject generateActions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		Button generateNearby = CreateMaintenanceButton(generateActions.transform, "生成附近丧尸", CheatMenuStyle.Warning,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.GenerateZombies,
				WorldMaintenanceScopeKind.Nearby, requiresConfirmation: true),
			"预览并确认在附近可用的已加载丧尸槽位中生成普通丧尸。");
		Button generateLoaded = CreateMaintenanceButton(generateActions.transform, "生成已加载丧尸", CheatMenuStyle.Danger,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.GenerateZombies,
				WorldMaintenanceScopeKind.LoadedWorld, requiresConfirmation: true),
			"预览并确认在当前已加载区域的死丧尸槽位中生成丧尸。");
		generateNearby.interactable = generateLoaded.interactable = !running;
	}

	private void BuildGroundItemMaintenanceCard(Transform parent, WorldMaintenanceDashboardSnapshot snapshot, bool running)
	{
		GameObject card = CreateWorldCard(parent, "GroundItemMaintenanceCard", -1f);
		CreateLabel(card.transform, "地面掉落物清理", 17, CheatMenuStyle.Text);
		CreateFlowLabel(card.transform,
			$"附近掉落物：{snapshot.NearbyGroundItems}    全部掉落物：{snapshot.AllGroundItems}    当前范围：{WorldMaintenanceRadius:0}m",
			CheatMenuStyle.Text);
		CreateFlowLabel(card.transform, "只处理 ItemManager 管理的地面掉落物，不触碰背包、衣物容器、储物箱或无限仓库。", CheatMenuStyle.Muted);
		GameObject actions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		Button nearby = CreateMaintenanceButton(actions.transform, "清理附近掉落物", CheatMenuStyle.Warning,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.ClearGroundItems,
				WorldMaintenanceScopeKind.Nearby, requiresConfirmation: true),
			"预览并确认清理选定半径内的地面掉落物。");
		Button all = CreateMaintenanceButton(actions.transform, "清理全部掉落物", CheatMenuStyle.Danger,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.ClearGroundItems,
				WorldMaintenanceScopeKind.WholeMap, requiresConfirmation: true),
			"预览并确认按区域分帧清理当前地图全部地面掉落物。");
		nearby.interactable = all.interactable = !running;
	}

	private void BuildItemRestockCard(Transform parent, WorldMaintenanceDashboardSnapshot snapshot, bool running)
	{
		GameObject card = CreateWorldCard(parent, "ItemRestockMaintenanceCard", -1f);
		CreateLabel(card.transform, "物资点补货", 17, CheatMenuStyle.Text);
		CreateFlowLabel(card.transform, string.Format(
			PluginLocalization.Translate("附近可补货：{0}    全图可补货：{1}"),
			snapshot.NearbyRestockableItems, snapshot.AllRestockableItems), CheatMenuStyle.Text);
		CreateFlowLabel(card.transform,
			"数量为预计可补充件数；附近按覆盖区域执行，保留原生密度、安全区和 2m 占位检查。",
			CheatMenuStyle.Muted);
		GameObject actions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlDefault, CheatMenuStyle.Theme.Space2);
		Button nearby = CreateMaintenanceButton(actions.transform, "补充附近物资", CheatMenuStyle.Warning,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.RestockGroundItems,
				WorldMaintenanceScopeKind.Nearby, requiresConfirmation: true),
			"预览并确认补充当前范围覆盖的物资区域，保留已有物品。");
		Button all = CreateMaintenanceButton(actions.transform, "补充全图物资", CheatMenuStyle.Danger,
			() => RequestWorldMaintenance(WorldMaintenanceActionKind.RestockGroundItems,
				WorldMaintenanceScopeKind.WholeMap, requiresConfirmation: true),
			"预览并确认按区域分帧补充当前地图缺失的自然物资点物品。");
		nearby.interactable = all.interactable = !running;
	}

	private void BuildMaintenanceProgress(Transform parent, WorldMaintenanceProgress progress)
	{
		GameObject card = CreateWorldCard(parent, "WorldMaintenanceProgressCard", -1f);
		_worldMaintenanceProgressText = CreateFlowLabel(card.transform, FormatMaintenanceProgress(progress), CheatMenuStyle.Muted);
		GameObject track = CreateObject("WorldMaintenanceProgressTrack", card.transform, typeof(Image));
		Image trackImage = track.GetComponent<Image>();
		trackImage.color = CheatMenuStyle.SliderTrack;
		trackImage.raycastTarget = false;
		_fluentControls.StyleSurface(track, trackImage, FluentSurfaceRole.Navigation, CheatMenuStyle.Theme.RadiusField);
		SetLayout(track, -1f, 8f, 1f);
		GameObject fill = CreateObject("WorldMaintenanceProgressFill", track.transform, typeof(Image));
		_worldMaintenanceProgressFill = fill.GetComponent<Image>();
		_worldMaintenanceProgressFill.color = CheatMenuStyle.Accent;
		_worldMaintenanceProgressFill.raycastTarget = false;
		RectTransform fillRect = fill.GetComponent<RectTransform>();
		fillRect.anchorMin = Vector2.zero;
		fillRect.anchorMax = new Vector2(progress?.Normalized ?? 0f, 1f);
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;
		GameObject actions = CreateRow(card.transform, CheatMenuStyle.Theme.ControlCompact, CheatMenuStyle.Theme.Space2);
		CreateFlexibleSpacer(actions.transform);
		_worldMaintenanceStopButton = CreateButton(actions.transform, "停止未处理项", delegate
		{
			_worldMaintenancePresenter.Stop();
			SetStatus("维护任务已停止；已完成的世界修改不会回滚。", 5f, StatusKind.Warning);
			RebuildWorldMaintenanceRoot();
		}, "停止尚未处理的候选；已经恢复或清除的对象不会回滚。", 150f,
			CheatMenuStyle.Theme.ControlCompact, CheatMenuStyle.Warning);
		_worldMaintenanceStopButton.interactable = progress?.CanStop == true;
	}

	private Button CreateMaintenanceButton(Transform parent, string label, Color color,
		System.Action action, string tooltip)
	{
		Button button = CreateButton(parent, label, action, tooltip, 0f,
			CheatMenuStyle.Theme.ControlDefault, color);
		SetLayout(button.gameObject, -1f, -1f, 1f);
		return button;
	}

	private void RequestWorldMaintenance(WorldMaintenanceActionKind action,
		WorldMaintenanceScopeKind scope, bool requiresConfirmation)
	{
		bool includeSpecial = (action == WorldMaintenanceActionKind.ClearZombies ||
			action == WorldMaintenanceActionKind.GenerateZombies) &&
			_state.WorldMaintenanceIncludeSpecialZombies;
		if (!_worldMaintenancePresenter.TryCreatePreview(action, scope, WorldMaintenanceRadius,
			includeSpecial, out WorldMaintenancePreview preview, out string message))
		{
			SetStatus(message, 5f, StatusKind.Warning);
			RebuildWorldMaintenanceRoot();
			return;
		}
		if (preview == null || preview.CandidateCount <= 0)
		{
			SetStatus(string.IsNullOrWhiteSpace(message) ? "没有符合条件的目标。" : message,
				4f, StatusKind.Info);
			RebuildWorldMaintenanceRoot();
			return;
		}
		if (requiresConfirmation)
		{
			ShowWorldMaintenanceConfirmation(preview);
			return;
		}
		StartWorldMaintenance(preview);
	}

	private void ShowWorldMaintenanceConfirmation(WorldMaintenancePreview preview)
	{
		PrepareModalOpen();
		_worldMaintenancePendingPreview = preview;
		_worldMaintenanceConfirmRoot = CreateObject("WorldMaintenanceConfirmOverlay", _uiRoot.ModalLayer, typeof(Image));
		_fluentHandles.RegisterScope(_worldMaintenanceConfirmRoot);
		Stretch(_worldMaintenanceConfirmRoot.GetComponent<RectTransform>());
		_worldMaintenanceConfirmRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
		GameObject dialog = CreateObject("WorldMaintenanceConfirmDialog", _worldMaintenanceConfirmRoot.transform, typeof(Image));
		Image dialogImage = dialog.GetComponent<Image>();
		dialogImage.color = CheatMenuStyle.Panel;
		_fluentControls.StyleSurface(dialog, dialogImage, FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusPanel);
		RectTransform dialogRect = dialog.GetComponent<RectTransform>();
		dialogRect.anchorMin = dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
		dialogRect.pivot = new Vector2(0.5f, 0.5f);
		dialogRect.sizeDelta = new Vector2(580f,
			preview.Action == WorldMaintenanceActionKind.ClearZombies || preview.Action == WorldMaintenanceActionKind.GenerateZombies
				? 340f : 290f);
		VerticalLayoutGroup layout = dialog.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(24, 24, 16, 16);
		layout.spacing = CheatMenuStyle.Theme.Space3;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		CreateText(dialog.transform, "确认执行世界维护？", 17, CheatMenuStyle.Text,
			TextAnchor.MiddleLeft, FontStyle.Bold);
		_worldMaintenanceConfirmSummary = CreateText(dialog.transform, FormatMaintenancePreview(preview), 13,
			CheatMenuStyle.Muted, TextAnchor.UpperLeft, FontStyle.Normal, localize: false);
		SetLayout(_worldMaintenanceConfirmSummary.gameObject, -1f, 78f, 1f);
		if (preview.Action == WorldMaintenanceActionKind.ClearZombies ||
			preview.Action == WorldMaintenanceActionKind.GenerateZombies)
		{
			GameObject optionRow = CreateRow(dialog.transform, CheatMenuStyle.Theme.ControlDefault,
				CheatMenuStyle.Theme.Space2);
			CreateToggleAction(optionRow.transform, "包括特殊丧尸",
				_state.WorldMaintenanceIncludeSpecialZombies, UpdateWorldMaintenanceSpecialOption,
				"显式纳入 Boss、Mega 和信标事件区域中的丧尸；默认关闭。");
		}
		CreateFlowLabel(dialog.transform,
			"此操作会真实修改测试存档。停止任务或回滚 DLL 都不会撤销已经完成的恢复与清除。",
			CheatMenuStyle.Warning);
		GameObject actions = CreateRow(dialog.transform, 38f, CheatMenuStyle.Theme.Space2);
		CreateFlexibleSpacer(actions.transform);
		Button cancel = CreateButton(actions.transform, "取消", HideWorldMaintenanceConfirmation,
			"取消本次预览，不修改世界。", 100f, 34f, CheatMenuStyle.Surface);
		CreateButton(actions.transform, "确认执行", ConfirmWorldMaintenance,
			"按当前预览开始分帧维护；已完成的修改不可撤销。", 120f, 34f, CheatMenuStyle.Danger);
		_uiRoot.RegisterModal(_worldMaintenanceConfirmRoot, HideWorldMaintenanceConfirmation, cancel.gameObject);
		AnimateModalEnter(_worldMaintenanceConfirmRoot, dialog.transform);
	}

	private void UpdateWorldMaintenanceSpecialOption(bool includeSpecial)
	{
		_state.WorldMaintenanceIncludeSpecialZombies = includeSpecial;
		WorldMaintenancePreview pending = _worldMaintenancePendingPreview;
		if (pending == null || (pending.Action != WorldMaintenanceActionKind.ClearZombies &&
			pending.Action != WorldMaintenanceActionKind.GenerateZombies)) return;
		if (_worldMaintenancePresenter.TryCreatePreview(pending.Action, pending.Scope,
			pending.RadiusMeters, includeSpecial, out WorldMaintenancePreview refreshed, out string message) && refreshed != null)
		{
			_worldMaintenancePendingPreview = refreshed;
			if (_worldMaintenanceConfirmSummary != null)
			{
				_worldMaintenanceConfirmSummary.text = FormatMaintenancePreview(refreshed);
			}
		}
		else
		{
			SetStatus(message, 5f, StatusKind.Warning);
		}
	}

	private void ConfirmWorldMaintenance()
	{
		WorldMaintenancePreview preview = _worldMaintenancePendingPreview;
		if (preview == null) return;
		StartWorldMaintenance(preview);
	}

	private void StartWorldMaintenance(WorldMaintenancePreview preview)
	{
		if (_worldMaintenancePresenter.TryStart(preview, out WorldMaintenancePreview currentPreview, out string message))
		{
			HideWorldMaintenanceConfirmation();
			SetStatus("世界维护任务已开始，将按帧处理候选。", 4f, StatusKind.Info);
			RebuildWorldMaintenanceRoot();
			return;
		}
		if (currentPreview != null && currentPreview.CandidateCount > 0 &&
			_worldMaintenanceConfirmRoot != null)
		{
			_worldMaintenancePendingPreview = currentPreview;
			if (_worldMaintenanceConfirmSummary != null)
			{
				_worldMaintenanceConfirmSummary.text = FormatMaintenancePreview(currentPreview);
			}
			SetStatus(message, 5f, StatusKind.Warning);
			return;
		}
		HideWorldMaintenanceConfirmation();
		SetStatus(message, 5f, StatusKind.Warning);
		RebuildWorldMaintenanceRoot();
	}

	private void HideWorldMaintenanceConfirmation()
	{
		_worldMaintenancePendingPreview = null;
		_worldMaintenanceConfirmSummary = null;
		if (_worldMaintenanceConfirmRoot == null) return;
		GameObject root = _worldMaintenanceConfirmRoot;
		_worldMaintenanceConfirmRoot = null;
		QueueModalExit(root, ResolveModalVisual(root));
	}

	private void RefreshWorldMaintenanceView()
	{
		if (_worldMaintenanceRoot == null) return;
		if (_worldMaintenanceRenderedVersion != _worldMaintenancePresenter.SnapshotVersion)
		{
			RebuildWorldMaintenanceRoot();
			return;
		}
		WorldMaintenanceProgress progress = _worldMaintenancePresenter.CaptureProgress();
		if (_worldMaintenanceProgressText != null)
		{
			_worldMaintenanceProgressText.text = FormatMaintenanceProgress(progress);
		}
		if (_worldMaintenanceProgressFill != null)
		{
			RectTransform fill = _worldMaintenanceProgressFill.rectTransform;
			fill.anchorMax = new Vector2(progress?.Normalized ?? 0f, 1f);
		}
		if (_worldMaintenanceStopButton != null)
		{
			_worldMaintenanceStopButton.interactable = progress?.CanStop == true;
		}
	}

	private void RebuildWorldMaintenanceRoot()
	{
		if (_worldMaintenanceRoot != null)
		{
			GameObject old = _worldMaintenanceRoot;
			_worldMaintenanceRoot = null;
			_fluentHandles.DisposeSubtree(old);
			QueueForDestroy(old);
		}
		ResetWorldMaintenanceCardReferences();
		BuildWorldMaintenanceRoot();
		Canvas.ForceUpdateCanvases();
	}

	private void ResetWorldMaintenanceVisualReferences()
	{
		_worldEnvironmentWeatherText = null;
		_worldEnvironmentStateText = null;
		_worldMaintenanceParent = null;
		_worldMaintenanceRoot = null;
		_worldMaintenanceRenderedVersion = -1;
		ResetWorldMaintenanceCardReferences();
	}

	private void ResetWorldMaintenanceCardReferences()
	{
		_worldMaintenanceProgressText = null;
		_worldMaintenanceProgressFill = null;
		_worldMaintenanceStopButton = null;
	}

	private static string FormatMaintenanceProgress(WorldMaintenanceProgress progress)
	{
		if (progress == null) return PluginLocalization.Translate("状态：不可用");
		string message = PluginLocalization.Translate(progress.Message);
		if (progress.State == WorldMaintenanceRunState.Running)
		{
			return string.Format(PluginLocalization.Translate("处理中 {0} / {1}。{2}"),
				progress.ProcessedCount, progress.TotalCount, message);
		}
		return string.Format(PluginLocalization.Translate("状态：{0}"), message);
	}

	private static string FormatMaintenancePreview(WorldMaintenancePreview preview)
	{
		if (preview == null) return string.Empty;
		return string.Format(PluginLocalization.Translate(
				"动作：{0}\n范围：{1}\n可执行候选：{2}    特殊保护：{3}    碰撞阻挡：{4}"),
			PluginLocalization.Translate(MaintenanceActionLabel(preview.Action)),
			MaintenanceScopeLabel(preview.Scope, preview.RadiusMeters),
			preview.CandidateCount, preview.ProtectedCount, preview.BlockedCount);
	}

	private static string MaintenanceActionLabel(WorldMaintenanceActionKind action)
	{
		return action switch
		{
			WorldMaintenanceActionKind.RefreshNaturalResources => "自然资源刷新",
			WorldMaintenanceActionKind.ClearZombies => "丧尸清除",
			WorldMaintenanceActionKind.GenerateZombies => "丧尸生成",
			WorldMaintenanceActionKind.RestockGroundItems => "物资点补货",
			_ => "地面掉落物清理"
		};
	}

	private static string MaintenanceScopeLabel(WorldMaintenanceScopeKind scope, float radius)
	{
		return scope switch
		{
			WorldMaintenanceScopeKind.Nearby => string.Format(
				PluginLocalization.Translate("附近 {0}m"), radius.ToString("0")),
			WorldMaintenanceScopeKind.LoadedWorld => PluginLocalization.Translate("当前已加载对象"),
			_ => PluginLocalization.Translate("当前地图全部区域")
		};
	}
}
