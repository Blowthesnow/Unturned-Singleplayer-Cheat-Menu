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
	private void BuildWorkshopLocalizationPanel(Transform panel)
	{
		TranslationToolsSnapshot workshopLocalization = _translationPresenter.CaptureSnapshot();
		if (!workshopLocalization.IsAvailable)
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
		}, "打开翻译 API 协议列表；选择后可配置对应的 Endpoint、API Key 和 Model。", 0f, 34f, CheatMenuStyle.Surface, 12);
		_workshopLocalizationProtocolButtonText = apiButton.GetComponentInChildren<Text>(true);
		SetLayout(apiButton.gameObject, -1f, -1f, 1f);
		CreateButton(gameObject.transform, "开始翻译（未缓存）", StartWorkshopTranslationFromUi, "扫描当前已加载资产后，翻译缓存中没有的文本；填写配置后点击这里开始。", 170f, 34f, CheatMenuStyle.Accent, 12);
		CreateButton(gameObject.transform, "停止翻译", StopWorkshopTranslationFromUi, "停止当前翻译任务；已经写入缓存的译文会保留。", 105f, 34f, CheatMenuStyle.Danger, 12);
		_workshopLocalizationSettingsHost = CreateObject("WorkshopLocalizationSettingsHost", panel);
		AddVerticalLayout(_workshopLocalizationSettingsHost, CheatMenuStyle.Theme.Space2, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = false;
		SetLayout(_workshopLocalizationSettingsHost, -1f, -1f, 1f);
		ResetWorkshopApiKeyDrafts(workshopLocalization.Settings.ApiKeys);
		BuildWorkshopLocalizationSettingsRegion();
		GameObject gameObject2 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject2.transform, "启用缓存译文", workshopLocalization.Enabled, delegate(bool value)
		{
			_translationPresenter.SetEnabled(value);
		}, "关闭后立即回退到原始 Getter 文本。");
		CreateToggleAction(gameObject2.transform, "应用到原生显示", workshopLocalization.ApplyToNativeUi, delegate(bool value)
		{
			_translationPresenter.SetApplyToNativeUi(value);
		}, "覆盖 ItemAsset、VehicleAsset 和 Workshop Action 的显示 Getter。");
		GameObject gameObject3 = CreateRow(panel, 40f, 7f);
		CreateToggleAction(gameObject3.transform, "资产刷新后自动扫描", workshopLocalization.AutoScanOnAssetsRefreshed, delegate(bool value)
		{
			_translationPresenter.SetAutoScan(value);
		});
		CreateToggleAction(gameObject3.transform, "自动翻译未缓存", workshopLocalization.AutoTranslateMissing, delegate(bool value)
		{
			_translationPresenter.SetAutoTranslate(value);
		}, "需要已配置 API；默认关闭以避免意外费用。");
		GameObject gameObject4 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject4.transform, "扫描并应用缓存", delegate
		{
			_translationPresenter.Scan();
			RefreshWorkshopLocalizationPage();
		}, "重新扫描已加载资产，并立即把已有缓存译文应用到显示 Getter。", 0f, 38f, CheatMenuStyle.Accent);
		CreateButton(gameObject4.transform, "重新加载缓存", delegate
		{
			_translationPresenter.ReloadCache();
			RefreshWorkshopLocalizationPage();
		}, "从磁盘重新读取翻译缓存，替换当前内存中的缓存内容。", 0f, 38f, CheatMenuStyle.Surface);
		GameObject gameObject5 = CreateRow(panel, 40f, 7f);
		CreateButton(gameObject5.transform, "翻译未缓存文本", delegate
		{
			bool flag4 = _translationPresenter.Translate(force: false);
			RefreshWorkshopLocalizationPage();
			SetStatus(flag4 ? "Workshop 未缓存文本翻译已启动。" : "未能启动 Workshop 翻译，请检查 API 配置或当前任务状态。", 4f, (!flag4) ? StatusKind.Warning : StatusKind.Info);
		}, "仅向翻译服务提交缓存中尚不存在的当前资产文本。", 0f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject5.transform, "强制重译当前文本", delegate
		{
			bool flag4 = _translationPresenter.Translate(force: true);
			RefreshWorkshopLocalizationPage();
			SetStatus(flag4 ? "Workshop 强制重译已启动。" : "未能启动 Workshop 强制重译，请检查 API 配置或当前任务状态。", 4f, (!flag4) ? StatusKind.Warning : StatusKind.Info);
		}, "忽略现有缓存，重新提交当前资产文本并覆盖对应缓存译文。", 0f, 38f, CheatMenuStyle.Surface);
		CreateButton(gameObject5.transform, "取消", delegate
		{
			_translationPresenter.Cancel();
			RefreshWorkshopLocalizationPage();
		}, "取消当前正在运行的扫描或翻译任务；已写入缓存的结果会保留。", 0f, 38f, CheatMenuStyle.Danger);
		CreateLabel(panel, "缓存文件：" + workshopLocalization.CachePath, 11, CheatMenuStyle.Muted);
	}

	private void BuildWorkshopLocalizationSettingsRegion()
	{
		TranslationToolsSnapshot workshopLocalization = _translationPresenter.CaptureSnapshot();
		if (_workshopLocalizationSettingsHost == null || !workshopLocalization.IsAvailable)
		{
			return;
		}
		Transform panel = _workshopLocalizationSettingsHost.transform;
		bool isCloudProvider = workshopLocalization.Settings.IsCloudProvider;
		bool isLargeModelProvider = workshopLocalization.Settings.IsLargeModelProvider;
		bool isOllama = string.Equals(workshopLocalization.Settings.ApiProtocol, "Ollama", StringComparison.OrdinalIgnoreCase);
		bool requiresApiKey = !isOllama || isCloudProvider;
		bool requiresProjectId = string.Equals(workshopLocalization.Settings.ApiProtocol, "TencentTmt", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(workshopLocalization.Settings.ApiProtocol, "GoogleCloud", StringComparison.OrdinalIgnoreCase);
		CreateLabeledInput(panel, "接口地址", workshopLocalization.Settings.Endpoint, delegate(string value)
		{
			_translationPresenter.SetEndpoint(value);
		}, 0f, password: false, commitOnEndEdit: true, 0f, "请求发送到哪里。大模型通常是 Chat/Message 地址；云翻译使用供应商的 REST 地址。可保留默认值，也可以改成代理地址。");
		if (requiresApiKey)
		{
			CreateWorkshopApiKeyRows(panel, isCloudProvider ? "API Key / 应用 ID" : "API Key", isCloudProvider ? "云翻译供应商发给你的访问密钥或应用 ID。最多添加 2 个，并按密钥并行翻译。" : "大模型平台发给你的 API Key；不会写入日志。最多添加 2 个，并按密钥并行翻译。\nOllama 本地服务通常不需要填写。");
		}
		if (workshopLocalization.Settings.RequiresSecretKey)
		{
			CreateLabeledInput(panel, "Secret Key / 密钥", workshopLocalization.Settings.SecretKey, delegate(string value)
			{
				_translationPresenter.SetSecretKey(value);
			}, 0f, password: true, commitOnEndEdit: true, 0f, "部分云翻译供应商需要的第二段密钥：腾讯云 SecretKey、百度 Secret Key 或有道密钥。");
		}
		if (workshopLocalization.Settings.RequiresRegion)
		{
			CreateLabeledInput(panel, "区域", workshopLocalization.Settings.Region, delegate(string value)
			{
				_translationPresenter.SetRegion(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "云服务资源所在区域。例如腾讯云 ap-guangzhou、微软 Translator 的资源区域。");
		}
		if (requiresProjectId)
		{
			CreateLabeledInput(panel, "项目 ID", workshopLocalization.Settings.ProjectId, delegate(string value)
			{
				_translationPresenter.SetProjectId(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "云项目或资源的数字/字符串 ID；没有要求的供应商可以留空。");
		}
		if (isLargeModelProvider)
		{
			CreateLabeledInput(panel, "模型名称", workshopLocalization.Settings.Model, delegate(string value)
			{
				_translationPresenter.SetModel(value);
			}, 0f, password: false, commitOnEndEdit: true, 0f, "大模型平台上的模型名称，例如 deepseek-chat、gpt-4o-mini、claude-3-5-sonnet。云机器翻译通常不需要填写。");
		}
		CreateLabeledInput(panel, "每批翻译数量", workshopLocalization.Settings.BatchSize.ToString(), delegate(string value)
		{
			_translationPresenter.SetBatchSize(value);
		}, 0f, password: false, commitOnEndEdit: true, 0f, "每次发送给翻译接口的文本数量，范围 1-100。数量越小越稳，但请求次数更多；修改后下次翻译任务生效。");
	}

	private void CreateWorkshopApiKeyRows(Transform panel, string primaryLabel, string tooltip)
	{
		if (_workshopLocalizationApiKeyDrafts.Count == 0)
		{
			_workshopLocalizationApiKeyDrafts.Add(string.Empty);
		}
		for (int index = 0; index < _workshopLocalizationApiKeyDrafts.Count && index < MachineTranslationSettings.MaxApiKeyCount; index++)
		{
			int capturedIndex = index;
			GameObject row = CreateRow(panel, 38f, 6f);
			SetLayout(row, -1f, -1f, 1f);
			CreateLabel(row.transform, index == 0 ? primaryLabel : "API Key " + (index + 1), 12, CheatMenuStyle.Muted, 86f, TextAnchor.MiddleRight);
			InputField input = CreateInput(row.transform, _workshopLocalizationApiKeyDrafts[index], delegate(string value)
			{
				_workshopLocalizationApiKeyDrafts[capturedIndex] = value ?? string.Empty;
			}, 0f, 1f, password: true, commitOnEndEdit: false, tooltip);
			_fluentControls.TrackInputEndEdit(input.gameObject, input, delegate
			{
				CommitWorkshopApiKeyDrafts();
			});
			if (index == 0)
			{
				Button addButton = CreateButton(row.transform, "+", AddWorkshopApiKeyDraft, "增加一个并行翻译密钥输入框，最多 2 个。", 36f, 34f, CheatMenuStyle.SurfaceHover, 17);
				addButton.interactable = _workshopLocalizationApiKeyDrafts.Count < MachineTranslationSettings.MaxApiKeyCount;
			}
			else
			{
				CreateButton(row.transform, "×", delegate
				{
					RemoveWorkshopApiKeyDraft(capturedIndex);
				}, "删除此翻译密钥输入框，并保存其余密钥。", 36f, 34f, CheatMenuStyle.Danger, 16);
			}
		}
	}

	private void ResetWorkshopApiKeyDrafts(IReadOnlyList<string> apiKeys)
	{
		_workshopLocalizationApiKeyDrafts.Clear();
		if (apiKeys != null)
		{
			_workshopLocalizationApiKeyDrafts.AddRange(apiKeys.Take(MachineTranslationSettings.MaxApiKeyCount));
		}
		if (_workshopLocalizationApiKeyDrafts.Count == 0)
		{
			_workshopLocalizationApiKeyDrafts.Add(string.Empty);
		}
	}

	private void AddWorkshopApiKeyDraft()
	{
		if (_workshopLocalizationApiKeyDrafts.Count >= MachineTranslationSettings.MaxApiKeyCount)
		{
			return;
		}
		_workshopLocalizationApiKeyDrafts.Add(string.Empty);
		RefreshWorkshopLocalizationSettingsRegion();
	}

	private void RemoveWorkshopApiKeyDraft(int index)
	{
		if (index <= 0 || index >= _workshopLocalizationApiKeyDrafts.Count)
		{
			return;
		}
		_workshopLocalizationApiKeyDrafts.RemoveAt(index);
		CommitWorkshopApiKeyDrafts();
		RefreshWorkshopLocalizationSettingsRegion();
	}

	private void CommitWorkshopApiKeyDrafts()
	{
		_translationPresenter.SetApiKey(MachineTranslationSettings.SerializeApiKeys(_workshopLocalizationApiKeyDrafts));
		RefreshWorkshopLocalizationPageValues();
	}

	private void RebuildWorkshopLocalizationScrollLayout(bool resetToTop = false)
	{
		if (_workshopLocalizationScrollContent == null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_workshopLocalizationScrollContent);
		float preferredHeight = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(_workshopLocalizationScrollContent));
		_workshopLocalizationScrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
		LayoutRebuilder.ForceRebuildLayoutImmediate(_workshopLocalizationScrollContent);
		Canvas.ForceUpdateCanvases();
		if (resetToTop && _workshopLocalizationScrollRect != null)
		{
			_workshopLocalizationScrollRect.verticalNormalizedPosition = 1f;
		}
	}

	private void RefreshWorkshopLocalizationSettingsRegion()
	{
		if (_workshopLocalizationSettingsHost == null)
		{
			return;
		}
		ClearResultHost(_workshopLocalizationSettingsHost);
		BuildWorkshopLocalizationSettingsRegion();
		RebuildWorkshopLocalizationScrollLayout();
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
		if (_workshopProtocolTriggerButton == null || _root == null || !_translationPresenter.CaptureSnapshot().IsAvailable)
		{
			return;
		}
		if (_workshopProtocolDrawerRoot != null)
		{
			QueueForDestroy(_workshopProtocolDrawerRoot);
		}
		_workshopProtocolDrawerRoot = CreateObject("WorkshopProtocolDrawer", _uiRoot.PopupLayer, typeof(Image));
		_uiRoot.RegisterPopup(_workshopProtocolDrawerRoot, HideWorkshopProtocolDrawer);
		Image drawerImage = _workshopProtocolDrawerRoot.GetComponent<Image>();
		drawerImage.color = CheatMenuStyle.Panel;
		_fluentHandles.RegisterScope(_workshopProtocolDrawerRoot);
		_fluentControls.StyleSurface(_workshopProtocolDrawerRoot, drawerImage, ControlLibrary.Core.FluentSurfaceRole.Raised, CheatMenuStyle.Theme.RadiusControl);
		Outline outline = _workshopProtocolDrawerRoot.AddComponent<Outline>();
		outline.effectColor = CheatMenuStyle.InputBorder;
		outline.effectDistance = Vector2.one;
		outline.useGraphicAlpha = false;
		outline.enabled = false;
		_workshopProtocolDrawerPopup = _workshopProtocolDrawerRoot.GetComponent<RectTransform>();
		RectTransform workshopProtocolDrawerPopup = _workshopProtocolDrawerPopup;
		Vector2 anchorMin = (_workshopProtocolDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f));
		workshopProtocolDrawerPopup.anchorMin = anchorMin;
		_workshopProtocolDrawerPopup.pivot = new Vector2(0.5f, 1f);
		string[] supportedProtocols = MachineTranslationSettings.SupportedProtocols;
		int num = Mathf.CeilToInt((float)supportedProtocols.Length / 3f);
		_workshopProtocolDrawerPopup.sizeDelta = new Vector2(356f, CheatMenuStyle.Theme.Space4 + (float)num * CheatMenuStyle.Theme.ControlCompact + (float)Math.Max(0, num - 1) * CheatMenuStyle.Theme.Space1);
		GridLayoutGroup gridLayoutGroup = _workshopProtocolDrawerRoot.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.padding = new RectOffset(8, 8, 8, 8);
		gridLayoutGroup.spacing = new Vector2(4f, 4f);
		gridLayoutGroup.cellSize = new Vector2(112f, CheatMenuStyle.Theme.ControlCompact);
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 3;
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		string apiProtocol = _translationPresenter.CaptureSnapshot().Settings.ApiProtocol;
		string[] array = supportedProtocols;
		foreach (string text in array)
		{
			string capturedProtocol = text;
			string label = PluginLocalization.Translate(MachineTranslationSettings.GetDisplayName(capturedProtocol));
			SetLayout(CreateButton(_workshopProtocolDrawerRoot.transform, label, delegate
			{
				_translationPresenter.SetApiProtocol(capturedProtocol);
				RunWithoutTransientMotion(HideWorkshopProtocolDrawer);
				RefreshWorkshopLocalizationSettingsRegion();
				RefreshWorkshopLocalizationPageValues();
			}, string.Format(PluginLocalization.Translate("使用“{0}”协议连接翻译服务。"), label),
				0f, CheatMenuStyle.Theme.ControlCompact, string.Equals(capturedProtocol, apiProtocol, StringComparison.OrdinalIgnoreCase) ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover, 10).gameObject, -1f, CheatMenuStyle.Theme.ControlCompact);
		}
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(_workshopProtocolDrawerPopup);
		_workshopProtocolDrawerRoot.transform.SetAsLastSibling();
		PositionWorkshopProtocolDrawer();
		AnimatePopupEnter(_workshopProtocolDrawerRoot, _workshopProtocolDrawerPopup);
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
			SetPopupMotionPosition(_workshopProtocolDrawerPopup, new Vector2(x, y));
		}
	}

	private void HideWorkshopProtocolDrawer()
	{
		_workshopProtocolDrawerOpen = false;
		if (_workshopProtocolDrawerRoot != null)
		{
			GameObject root = _workshopProtocolDrawerRoot;
			_workshopProtocolDrawerRoot = null;
			QueuePopupExit(root, root.transform);
		}
		_workshopProtocolDrawerPopup = null;
		_workshopProtocolTriggerButton = null;
	}

	private void StartWorkshopTranslationFromUi()
	{
		TranslationToolsSnapshot workshopLocalization = _translationPresenter.CaptureSnapshot();
		if (workshopLocalization.IsAvailable && workshopLocalization.IsTranslationRunning)
		{
			SetStatus("翻译正在进行中，请不要重复点击开始翻译。", 5f, StatusKind.Info);
			return;
		}
		_translationPresenter.Scan(allowAutoTranslate: false);
		bool num = _translationPresenter.Translate(force: false);
		WorkshopLocalizationProgress workshopLocalizationProgress = _translationPresenter.CaptureSnapshot().Progress;
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
		TranslationToolsSnapshot workshopLocalization = _translationPresenter.CaptureSnapshot();
		if (!workshopLocalization.IsAvailable || !workshopLocalization.IsTranslationRunning)
		{
			SetStatus("当前没有正在运行的翻译任务。", 5f, StatusKind.Warning);
			return;
		}
		_translationPresenter.Cancel();
		RefreshWorkshopLocalizationPage();
		SetStatus("已请求停止翻译；已完成并写入缓存的译文会保留。", 5f, StatusKind.Warning);
	}

	private void RefreshWorkshopLocalizationPage()
	{
		if (_activeToolsTab == ToolsTab.Translation)
		{
			RefreshWorkshopLocalizationPageValues();
		}
	}

	private void RefreshWorkshopLocalizationPageValues()
	{
		TranslationToolsSnapshot workshopLocalization = _translationPresenter.CaptureSnapshot();
		if (!workshopLocalization.IsAvailable)
		{
			return;
		}
		WorkshopLocalizationProgress progress = workshopLocalization.Progress;
		string arg = PluginLocalization.Translate(progress.State switch
		{
			WorkshopLocalizationState.Scanning => "扫描中",
			WorkshopLocalizationState.Translating => "翻译中",
			WorkshopLocalizationState.Cancelled => "已取消",
			WorkshopLocalizationState.Error => "错误",
			_ => (!string.Equals(progress.Message, "translation complete", StringComparison.Ordinal)) ? ((!string.Equals(progress.Message, "no uncached text", StringComparison.Ordinal)) ? "空闲" : "完成") : "完成",
		});
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
			string text = PluginLocalization.Translate(workshopLocalization.ApiConfigured ? "已配置" : "未配置");
			string model = string.IsNullOrWhiteSpace(workshopLocalization.Settings.Model)
				? PluginLocalization.Translate("未填写")
				: workshopLocalization.Settings.Model;
			_workshopLocalizationApiText.text = PluginLocalization.Translate("翻译接口：" + text + "；供应商：" + MachineTranslationSettings.GetDisplayName(workshopLocalization.Settings.ApiProtocol) + "；密钥：" + workshopLocalization.Settings.ApiKeyCount + "/" + MachineTranslationSettings.MaxApiKeyCount + "；并行：" + workshopLocalization.Settings.ParallelWorkerCount + " 路；目标语言：" + workshopLocalization.Settings.TargetLanguage + "；模型：" + model);
		}
		if (_workshopLocalizationProtocolButtonText != null)
		{
			_workshopLocalizationProtocolButtonText.text = MachineTranslationSettings.GetDisplayName(workshopLocalization.Settings.ApiProtocol) + " ▼";
		}
	}
}
