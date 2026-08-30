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
	private void BuildAutoPickupPanel(Transform panel)
	{
		_autoPickupContentHost = CreateObject("AutoPickupContentHost", panel);
		AddVerticalLayout(_autoPickupContentHost, 8f, new RectOffset(0, 0, 0, 0));
		SetLayout(_autoPickupContentHost, -1f, -1f, 1f, 1f);
		PopulateAutoPickupPanel();
	}

	private void RefreshAutoPickupPanel()
	{
		if (_activeTab != MenuTab.Tools || _activeToolsTab != ToolsTab.AutoPickup || _autoPickupContentHost == null)
		{
			return;
		}
		HideAutoPickupBlacklistPicker();
		ClearResultHost(_autoPickupContentHost);
		PopulateAutoPickupPanel();
	}

	private void PopulateAutoPickupPanel()
	{
		Transform panel = _autoPickupContentHost.transform;
		AutoPickupToolsSnapshot snapshot = _autoPickupPresenter.CaptureSnapshot();
		if (!snapshot.IsAvailable)
		{
			CreateEmptyState(panel, "自动拾取不可用", "插件尚未完成自动拾取服务初始化。");
			return;
		}
		List<ItemAsset> whitelistAssets = GetFilteredAutoPickupRuleAssets(snapshot.Rules, ItemFilterRuleAction.Allow);
		List<ItemAsset> blacklistAssets = GetFilteredAutoPickupRuleAssets(snapshot.Rules, ItemFilterRuleAction.Deny);
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
			RefreshAutoPickupPanel();
		});
		pageBody.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		CreateAutoPickupPageDots(pager.transform, _autoPickupPage, pageCount);
		if (_autoPickupPage == 0)
		{
			BuildAutoPickupSettingsPage(pageBody.transform, snapshot);
		}
		else if (_autoPickupPage <= whitelistPageCount)
		{
			BuildAutoPickupRulePage(pageBody.transform, ItemFilterRuleAction.Allow);
		}
		else
		{
			BuildAutoPickupRulePage(pageBody.transform, ItemFilterRuleAction.Deny);
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

	private void BuildAutoPickupSettingsPage(Transform page, AutoPickupToolsSnapshot snapshot)
	{
		AutoPickupSettings settings = snapshot.Settings;
		AutoPickupRuleDocument document = snapshot.Rules;
		AutoPickupDiagnosticsSnapshot diagnostics = snapshot.Diagnostics;
		CreateSummaryPanel(page, $"{PluginLocalization.Translate(diagnostics.State)}  ·  候选 {diagnostics.CandidateCount}  ·  成功 {diagnostics.PickedUpCount}  ·  失败 {diagnostics.FailureCount}  ·  重试 {diagnostics.RetryCount}");
		GameObject switches = CreateRow(page, 40f, 7f);
		CreateToggleAction(switches.transform, "启用自动拾取", settings.Enabled, delegate(bool value)
		{
			_autoPickupPresenter.SetEnabled(value);
			bool applied = _autoPickupPresenter.CaptureSnapshot().Settings.Enabled == value;
			SetStatus(applied ? (value ? "自动拾取已启用；关闭菜单后开始扫描。" : "自动拾取已关闭。") : "只有进入真正的单人世界后才能启用自动拾取。", 5f, applied ? StatusKind.Info : StatusKind.Warning);
		});
		CreateToggleAction(switches.transform, "菜单打开时暂停", settings.PauseWhenMenuOpen, _autoPickupPresenter.SetPauseWhenMenuOpen);
		CreateToggleAction(switches.transform, "死亡时暂停", settings.PauseWhenDead, _autoPickupPresenter.SetPauseWhenDead);
		GameObject minimapLinks = CreateRow(page, 40f, 7f);
		CreateToggleAction(minimapLinks.transform, "跟随小地图范围", settings.UseMinimapRange, _autoPickupPresenter.SetUseMinimapRange,
			"默认关闭；开启后只由小地图可见半径单向约束自动拾取距离。");
		CreateToggleAction(minimapLinks.transform, "跟随小地图稀有度", settings.UseMinimapRarityFilter, _autoPickupPresenter.SetUseMinimapRarityFilter,
			"默认关闭；开启后小地图稀有度掩码会额外约束自动拾取，不会反向影响雷达显示。");
		CreateAutoPickupSlider(page, "拾取距离", settings.Range, AutoPickupSettings.MinRange, AutoPickupSettings.MaxRange, "m", _autoPickupRangeDebouncer, _autoPickupPresenter.SetRange);
		GameObject scheduling = CreateRow(page, 40f, 7f);
		CreateAutoPickupSlider(scheduling.transform, "扫描频率", settings.FrequencyHz, AutoPickupSettings.MinFrequencyHz, AutoPickupSettings.MaxFrequencyHz, "Hz", _autoPickupFrequencyDebouncer, _autoPickupPresenter.SetFrequency);
		GameObject dropBlockRow = CreateRow(page, 40f, 8f);
		CreateLabel(dropBlockRow.transform, "丢弃后禁拾时长", 13, CheatMenuStyle.Text, 120f);
		InputField dropBlockInput = CreateInput(dropBlockRow.transform, settings.DropBlockMinutes.ToString(CultureInfo.InvariantCulture), delegate(string value)
		{
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
			{
				_autoPickupPresenter.SetDropBlockMinutes(minutes);
			}
		RefreshAutoPickupPanel();
		}, 92f, 0f, false, true, "玩家丢弃某个物品后，暂时禁止自动拾取同 ID 物品；再次丢弃同 ID 会重新计时。范围 1-1440 分钟，默认 1 分钟。");
		dropBlockInput.contentType = InputField.ContentType.IntegerNumber;
		dropBlockInput.characterLimit = 4;
		CreateLabel(dropBlockRow.transform, "分钟（1-1440）", 12, CheatMenuStyle.Muted, 110f);
		GameObject qualityFilterRow = CreateRow(page, 40f, 8f);
		CreateToggleAction(qualityFilterRow.transform, "启用品质过滤", settings.QualityFilterEnabled, _autoPickupPresenter.SetQualityFilterEnabled);
		CreateLabel(qualityFilterRow.transform, "最低品质", 13, CheatMenuStyle.Text, 72f);
		InputField qualityInput = CreateInput(qualityFilterRow.transform, settings.MinimumQuality.ToString(CultureInfo.InvariantCulture), delegate(string value)
		{
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int quality))
			{
				_autoPickupPresenter.SetMinimumQuality(quality);
			}
		RefreshAutoPickupPanel();
		}, 72f, 0f, false, true, "启用后，自动拾取会忽略品质低于该数值的物品。范围 0-100。");
		qualityInput.contentType = InputField.ContentType.IntegerNumber;
		qualityInput.characterLimit = 3;
		CreateLabel(qualityFilterRow.transform, "品质（0-100）", 12, CheatMenuStyle.Muted, 100f);
		CreateButton(scheduling.transform, $"每轮 {settings.RequestsPerTick} 个请求", delegate
		{
			int value = settings.RequestsPerTick >= AutoPickupSettings.MaxRequestsPerTick ? AutoPickupSettings.MinRequestsPerTick : settings.RequestsPerTick + 1;
			_autoPickupPresenter.SetRequestsPerTick(value);
			RefreshAutoPickupPanel();
		}, "将每轮原生拾取请求上限增加 1；到 16 后循环回 1。", 150f, 36f, CheatMenuStyle.SurfaceHover, 12);
		CreateButton(scheduling.transform, "诊断：" + AutoPickupDiagnosticsLabel(settings.Diagnostics), delegate
		{
			AutoPickupDiagnosticsLevel[] values = (AutoPickupDiagnosticsLevel[])Enum.GetValues(typeof(AutoPickupDiagnosticsLevel));
			int next = (Array.IndexOf(values, settings.Diagnostics) + 1) % values.Length;
			_autoPickupPresenter.SetDiagnostics(values[next]);
			RefreshAutoPickupPanel();
		}, "在关闭、摘要和逐项诊断日志之间循环切换。", 130f, 36f, CheatMenuStyle.SurfaceHover, 12);
		int whitelistCount = document.Rules.Count(rule => rule.Enabled && rule.Action == ItemFilterRuleAction.Allow && rule.Field == ItemFilterRuleField.AssetId);
		CreateSection(page, "白名单");
		GameObject whitelistSummary = CreateRow(page, 38f, 8f);
		CreateToggleAction(whitelistSummary.transform, "启用白名单", document.Enabled, delegate(bool value)
		{
			AutoPickupRuleLoadResult result = _autoPickupPresenter.SetWhitelistEnabled(value);
			SetStatus(result.Success ? (value ? "白名单已启用；现在只拾取白名单物品。" : "白名单已停用；黑名单仍然生效。") : "保存白名单状态失败：" + result.Error, 4f, result.Success ? StatusKind.Info : StatusKind.Error);
			RefreshAutoPickupPanel();
		});
		CreateLabel(whitelistSummary.transform, $"当前 {whitelistCount} 条白名单物品。", 12, CheatMenuStyle.Muted, 170f);
		CreateButton(whitelistSummary.transform, "管理白名单", delegate
		{
			_autoPickupPage = 1;
			RefreshAutoPickupPanel();
		}, "打开白名单管理页，查看或移除已加入的物品。", 110f, 34f, CheatMenuStyle.SurfaceHover, 12);
		CreateButton(whitelistSummary.transform, "添加白名单", ShowAutoPickupWhitelistPicker, "从当前背包选择要加入自动拾取白名单的物品。", 110f, 34f, CheatMenuStyle.Success, 12);
		CreateSection(page, "黑名单");
		GameObject blacklistSummary = CreateRow(page, 38f, 8f);
		int blacklistCount = document.Rules.Count(rule => rule.Enabled && rule.Action == ItemFilterRuleAction.Deny && rule.Field == ItemFilterRuleField.AssetId);
		CreateLabel(blacklistSummary.transform, $"当前 {blacklistCount} 条黑名单物品；黑名单始终优先。", 12, CheatMenuStyle.Muted);
		CreateFlexibleSpacer(blacklistSummary.transform);
		CreateButton(blacklistSummary.transform, "管理黑名单", delegate
		{
			_autoPickupPage = GetAutoPickupRuleFirstPage(ItemFilterRuleAction.Deny);
			RefreshAutoPickupPanel();
		}, "打开黑名单管理页，查看或移除已加入的物品。", 110f, 34f, CheatMenuStyle.SurfaceHover, 12);
		CreateButton(blacklistSummary.transform, "添加黑名单", ShowAutoPickupBlacklistPicker, "从当前背包选择要加入自动拾取黑名单的物品。", 110f, 34f, CheatMenuStyle.Danger, 12);
	}

	private void BuildAutoPickupRulePage(Transform page, ItemFilterRuleAction ruleAction)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		CreateSection(page, whitelist ? "白名单物品" : "黑名单物品");
		GameObject header = CreateRow(page, 38f, 8f);
		CreateLabel(header.transform, whitelist ? "点击物品卡片可取消白名单。" : "点击物品卡片可取消黑名单。", 12, CheatMenuStyle.Muted);
		CreateFlexibleSpacer(header.transform);
		CreateButton(header.transform, whitelist ? "添加白名单" : "添加黑名单", whitelist ? ShowAutoPickupWhitelistPicker : ShowAutoPickupBlacklistPicker,
			whitelist ? "从当前背包继续选择要加入白名单的物品。" : "从当前背包继续选择要加入黑名单的物品。",
			110f, 34f, whitelist ? CheatMenuStyle.Success : CheatMenuStyle.Danger, 12);
		GameObject searchRow = CreateRow(page, 36f, 7f);
		CreateLabel(searchRow.transform, "搜索", 12, CheatMenuStyle.Muted, 42f);
		CreateSearchInput(searchRow.transform, _autoPickupRuleSearch, delegate(string value)
		{
			_autoPickupRuleSearch = value ?? string.Empty;
			_autoPickupPage = GetAutoPickupRuleFirstPage(ruleAction);
			RefreshAutoPickupRuleResults(ruleAction);
		}, 0f, 1f);
		_autoPickupRuleResultsHost = CreateObject("AutoPickupRuleResultsHost", page, typeof(Image));
		_autoPickupRuleResultsHost.GetComponent<Image>().color = CheatMenuStyle.SurfaceInset;
		AddVerticalLayout(_autoPickupRuleResultsHost, 7f, new RectOffset(8, 8, 8, 8));
		SetLayout(_autoPickupRuleResultsHost, -1f, 560f, 1f, 0f);
		RefreshAutoPickupRuleResults(ruleAction);
	}

	private void RefreshAutoPickupRuleResults(ItemFilterRuleAction ruleAction)
	{
		if (_autoPickupRuleResultsHost == null || !_autoPickupPresenter.CaptureSnapshot().IsAvailable || _activeTab != MenuTab.Tools || _activeToolsTab != ToolsTab.AutoPickup)
		{
			return;
		}
		List<ItemAsset> assets = GetFilteredAutoPickupRuleAssets(_autoPickupPresenter.CaptureSnapshot().Rules, ruleAction);
		int whitelistPageCount = Math.Max(1, Mathf.CeilToInt((float)GetFilteredAutoPickupRuleAssets(_autoPickupPresenter.CaptureSnapshot().Rules, ItemFilterRuleAction.Allow).Count / AutoPickupCardPageSize));
		int pageCount = Math.Max(1, Mathf.CeilToInt((float)assets.Count / AutoPickupCardPageSize));
		int cardPage = ruleAction == ItemFilterRuleAction.Allow ? _autoPickupPage - 1 : _autoPickupPage - 1 - whitelistPageCount;
		cardPage = Mathf.Clamp(cardPage, 0, pageCount - 1);
		ClearResultHost(_autoPickupRuleResultsHost);
		CreateLabel(_autoPickupRuleResultsHost.transform, $"{assets.Count} 件  ·  第 {cardPage + 1}/{pageCount} 页", 11, CheatMenuStyle.Muted, 150f, TextAnchor.MiddleRight);
		if (assets.Count == 0)
		{
			CreateEmptyState(_autoPickupRuleResultsHost.transform, ruleAction == ItemFilterRuleAction.Allow ? "白名单为空" : "黑名单为空", ruleAction == ItemFilterRuleAction.Allow ? "启用白名单并添加物品后，只会拾取白名单物品。" : "默认不限制物品；点击“添加黑名单”从当前背包选择物品。");
			return;
		}
		GameObject grid = CreateObject("AutoPickupBlacklistGrid", _autoPickupRuleResultsHost.transform);
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
		return _translationPresenter.TryGetTranslation(asset, WorkshopTextField.ItemName, out string translation) && !string.IsNullOrWhiteSpace(translation) ? translation : asset.FriendlyName;
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
		List<ItemAsset> assets = _catalogPresenter.Items.Where(asset => asset != null && ids.Contains(asset.id)).ToList();
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
		foreach (ItemFilterRule rule in _autoPickupPresenter.CaptureSnapshot().Rules.Rules)
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
		int whitelistCount = GetFilteredAutoPickupRuleAssets(_autoPickupPresenter.CaptureSnapshot().Rules, ItemFilterRuleAction.Allow).Count;
		return 1 + Math.Max(1, Mathf.CeilToInt((float)whitelistCount / AutoPickupCardPageSize));
	}

	private List<ItemAsset> GetCurrentBackpackAssets()
	{
		return _autoPickupPresenter.GetCurrentBackpackAssets();
	}

	private void CreateAutoPickupItemCard(Transform parent, ItemAsset asset, ItemFilterRuleAction ruleAction, bool isListed, System.Action action)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		string displayName = GetItemDisplayName(asset);
		string tooltip = AssetHoverDetailsBuilder.BuildItem(asset, displayName, asset.itemDescription, _ammoCompatibilityIndex, _catalogPresenter.Items);
		string actionTooltip = whitelist
			? (isListed ? "点击将此物品从自动拾取白名单移除。" : "点击将此物品加入自动拾取白名单。")
			: (isListed ? "点击将此物品从自动拾取黑名单移除。" : "点击将此物品加入自动拾取黑名单。");
		Color color = !isListed ? CheatMenuStyle.Surface : (whitelist ? CheatMenuStyle.Success : CheatMenuStyle.Danger);
		Button button = CreateButton(parent, string.Empty, action, tooltip + "\n\n" + actionTooltip, 155f, 158f, color, delayTooltip: true);
		string meta = whitelist
			? (isListed ? $"ID {asset.id}  ·  点击取消白名单" : $"ID {asset.id}  ·  点击加入白名单")
			: (isListed ? $"ID {asset.id}  ·  点击取消黑名单" : $"ID {asset.id}  ·  点击加入黑名单");
		AddCardContents(button.transform, displayName, meta, () => _catalogPresenter.GetItemIcon(asset), isVehicle: false);
	}

	private void AddAutoPickupRule(ItemFilterRuleAction ruleAction, ItemAsset asset)
	{
		bool whitelist = ruleAction == ItemFilterRuleAction.Allow;
		if (asset == null || GetAutoPickupRuleIds(ruleAction).Contains(asset.id))
		{
			SetStatus(whitelist ? "该物品已经在白名单中。" : "该物品已经在黑名单中。", 3f, StatusKind.Info);
			return;
		}
		AutoPickupRuleDocument document = _autoPickupPresenter.CaptureSnapshot().Rules;
		List<ItemFilterRule> rules = document.Rules.Select(rule => rule.Clone()).ToList();
		rules.Add(new ItemFilterRule
		{
			Action = ruleAction,
			Field = ItemFilterRuleField.AssetId,
			Value = asset.id.ToString(CultureInfo.InvariantCulture)
		});
		bool whitelistEnabled = whitelist || document.Enabled;
		AutoPickupRuleLoadResult result = _autoPickupPresenter.SetRules(whitelistEnabled, whitelistEnabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly, rules);
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
		AutoPickupRuleDocument document = _autoPickupPresenter.CaptureSnapshot().Rules;
		List<ItemFilterRule> rules = document.Rules.Where(rule => !(rule.Action == ruleAction && rule.Field == ItemFilterRuleField.AssetId && string.Equals(rule.Value, assetId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))).Select(rule => rule.Clone()).ToList();
		AutoPickupRuleLoadResult result = _autoPickupPresenter.SetRules(document.Enabled, document.Mode, rules);
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
			RefreshAutoPickupPanel();
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
		_autoPickupBlacklistPickerRoot = CreateObject("AutoPickupBlacklistPicker", _uiRoot.ModalLayer, typeof(Image));
		_uiRoot.RegisterModal(_autoPickupBlacklistPickerRoot, HideAutoPickupBlacklistPicker);
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
		CreateButton(header.transform, "×", HideAutoPickupBlacklistPicker,
			whitelist ? "关闭白名单物品选择窗口。" : "关闭黑名单物品选择窗口。",
			42f, 36f, CheatMenuStyle.Surface, 22);
		CreateLabel(popup.transform, whitelist ? "物品按 ID 自动去重；点击卡片加入白名单，悬停卡片可查看完整物品详情。" : "物品按 ID 自动去重；点击卡片加入黑名单，悬停卡片可查看完整物品详情。", 12, CheatMenuStyle.Muted);
		GameObject search = CreateRow(popup.transform, 36f, 7f);
		CreateLabel(search.transform, "搜索", 12, CheatMenuStyle.Muted, 42f);
		CreateSearchInput(search.transform, _autoPickupBlacklistPickerSearch, delegate(string value)
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
		if (_autoPickupBlacklistPickerHost == null || !_autoPickupPresenter.CaptureSnapshot().IsAvailable)
		{
			return;
		}
		for (int i = _autoPickupBlacklistPickerHost.transform.childCount - 1; i >= 0; i--)
		{
			GameObject child = _autoPickupBlacklistPickerHost.transform.GetChild(i).gameObject;
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
}
