using System;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.UI;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class PointToolHud
{
	private readonly ManualLogSource _log;
	private readonly PluginUiRoot _uiRoot;

	private GameObject _root;

	private GameObject _movementPanel;

	private GameObject _pointPanel;

	private Text _movementText;

	private Text _pointText;

	private Font _font;

	private float _nextCreateAttempt;

	private bool _hasLoggedCreation;

	private bool _hasLoggedFontFailure;

	private static readonly bool ShowMovementHud = false;

	private const float PointOnlyHorizontalOffset = -15f;

	private const float PointOnlyTopOffset = -15f;

	private const float PointWithMovementTopOffset = -134f;

	internal PointToolHud(ManualLogSource log, PluginUiRoot uiRoot)
	{
		_log = log;
		_uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
	}

	internal void SetMovement(bool flight, bool noclip, float speed)
	{
		if (!ShowMovementHud)
		{
			if (_root != null && _movementText != null && !string.IsNullOrEmpty(_movementText.text))
			{
				_movementText.text = string.Empty;
				Refresh();
			}
		}
		else if (!flight && !noclip)
		{
			if (_root != null && _movementText != null)
			{
				_movementText.text = string.Empty;
				Refresh();
			}
		}
		else if (EnsureCreated())
		{
			string movementText = ((!flight && !noclip) ? string.Empty : ((flight ? $"FLY ×{speed:0.0}" : string.Empty) + "\n" + (noclip ? "NOCLIP" : string.Empty)).Trim());
			_movementText.text = movementText;
			Refresh();
		}
	}

	internal void SetPoint(string text)
	{
		if (EnsureCreated())
		{
			_pointText.text = text ?? string.Empty;
			Refresh();
		}
	}

	internal void Hide()
	{
		if (!(_root == null) && !(_movementText == null) && !(_pointText == null))
		{
			_movementText.text = string.Empty;
			_pointText.text = string.Empty;
			Refresh();
		}
	}

	internal void ClearPointIfCreated()
	{
		if (!(_root == null) && !(_pointText == null))
		{
			_pointText.text = string.Empty;
			Refresh();
		}
	}

	private bool EnsureCreated()
	{
		if (IsCreated())
		{
			return true;
		}
		if (Time.unscaledTime < _nextCreateAttempt)
		{
			return false;
		}
		if (MainCamera.instance == null || PlayerUI.window == null || Player.LocalPlayer == null)
		{
			return false;
		}
		_nextCreateAttempt = Time.unscaledTime + 1f;
		try
		{
			_font = SystemFontProvider.Resolve(_log);
			if (_font == null)
			{
				if (!_hasLoggedFontFailure)
				{
					_hasLoggedFontFailure = true;
					_log.LogError("[PointToolHud] 未找到支持中文的系统字体，HUD 暂不显示。");
				}
				return false;
			}
			_root = new GameObject("PointToolHudView", typeof(RectTransform));
			_root.transform.SetParent(_uiRoot.HudLayer, false);
			RectTransform component = _root.GetComponent<RectTransform>();
			component.anchorMin = Vector2.zero;
			component.anchorMax = Vector2.one;
			component.offsetMin = Vector2.zero;
			component.offsetMax = Vector2.zero;
			_movementText = CreateLabel("Movement", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(300f, 100f), TextAnchor.UpperRight, 18, out _movementPanel);
			_pointText = CreateLabel("PointTarget", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-15f, -15f), new Vector2(300f, 100f), TextAnchor.UpperRight, 13, out _pointPanel);
			_root.SetActive(value: false);
			if (!_hasLoggedCreation)
			{
				_hasLoggedCreation = true;
				_log.LogInfo($"[PointToolHud] created in unified HudLayer: rootActive={_root.activeSelf}, font={_font.name}, screen={Screen.width}x{Screen.height}");
			}
			return true;
		}
		catch (Exception arg)
		{
			_log.LogError($"[PointToolHud] 创建 HUD 失败，将在游戏 UI 就绪后重试：{arg}");
			DestroyGenerated();
			return false;
		}
	}

	private static void CreateHudBorderEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

	private Text CreateLabel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, int fontSize, out GameObject container)
	{
		container = new GameObject(name, typeof(RectTransform), typeof(Image));
		container.transform.SetParent(_root.transform, worldPositionStays: false);
		RectTransform component = container.GetComponent<RectTransform>();
		component.anchorMin = anchorMin;
		component.anchorMax = anchorMax;
		component.pivot = pivot;
		component.anchoredPosition = anchoredPosition;
		component.sizeDelta = size;
		Image component2 = container.GetComponent<Image>();
		component2.color = Color.clear;
		component2.raycastTarget = false;
		Canvas component3 = _uiRoot.Canvas;
		float num = ((component3 != null) ? Mathf.Max(0.01f, component3.scaleFactor) : 1f);
		float num2 = 3f / num;
		CreateHudBorderEdge(container.transform, name + ".BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f - num2), Vector2.zero);
		CreateHudBorderEdge(container.transform, name + ".BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, num2));
		CreateHudBorderEdge(container.transform, name + ".BorderLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(num2, 0f));
		CreateHudBorderEdge(container.transform, name + ".BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(0f - num2, 0f), Vector2.zero);
		GameObject gameObject = new GameObject(name + ".Background", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(container.transform, worldPositionStays: false);
		RectTransform component4 = gameObject.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(num2, num2);
		component4.offsetMax = new Vector2(0f - num2, 0f - num2);
		Image component5 = gameObject.GetComponent<Image>();
		component5.color = new Color(0.02f, 0.025f, 0.035f, 0.5f);
		component5.raycastTarget = false;
		GameObject gameObject2 = new GameObject(name + ".Text", typeof(RectTransform), typeof(Text));
		gameObject2.transform.SetParent(container.transform, worldPositionStays: false);
		RectTransform component6 = gameObject2.GetComponent<RectTransform>();
		component6.anchorMin = Vector2.zero;
		component6.anchorMax = Vector2.one;
		component6.offsetMin = new Vector2(12f, 8f);
		component6.offsetMax = new Vector2(-12f, -8f);
		Text component7 = gameObject2.GetComponent<Text>();
		component7.font = _font;
		component7.fontSize = fontSize;
		component7.color = Color.white;
		component7.alignment = alignment;
		component7.horizontalOverflow = HorizontalWrapMode.Wrap;
		component7.verticalOverflow = VerticalWrapMode.Truncate;
		component7.raycastTarget = false;
		return component7;
	}

	private bool IsCreated()
	{
		if (_root != null && _uiRoot.Canvas != null && _movementText != null && _pointText != null)
		{
			return _font != null;
		}
		return false;
	}

	private void Refresh()
	{
		if (!(_root == null) && !(_movementText == null) && !(_pointText == null))
		{
			_root.SetActive(!string.IsNullOrWhiteSpace(_movementText.text) || !string.IsNullOrWhiteSpace(_pointText.text));
			bool flag = !string.IsNullOrWhiteSpace(_movementText.text);
			if (_movementPanel != null)
			{
				_movementPanel.SetActive(flag);
			}
			if (_pointPanel != null)
			{
				_pointPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(flag ? (-24f) : (-15f), flag ? (-134f) : (-15f));
				_pointPanel.SetActive(!string.IsNullOrWhiteSpace(_pointText.text));
			}
		}
	}

	private void DestroyGenerated()
	{
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
		}
		_root = null;
		_movementPanel = null;
		_pointPanel = null;
		_movementText = null;
		_pointText = null;
		_font = null;
	}
}
