using System;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>
/// Screen-space HUD for the point tool and movement cheats.
///
/// Unturned can construct the plugin before PlayerUI and the gameplay camera
/// exist. Creation is lazy and the generated Canvas is rebuilt if a scene
/// transition destroys it.
/// </summary>
internal sealed class PointToolHud
{
    private readonly ManualLogSource _log;
    private GameObject _root;
    private GameObject _movementPanel;
    private GameObject _pointPanel;
    private Text _movementText;
    private Text _pointText;
    private Font _font;
    private bool _ownsFont;
    private float _nextCreateAttempt;
    private bool _hasLoggedCreation;
    private bool _hasLoggedFontFailure;

    // Temporarily keep flight/noclip status out of the screen HUD while
    // preserving the underlying movement features.
    private static readonly bool ShowMovementHud = false;
    private const float PointOnlyHorizontalOffset = -15f;
    private const float PointOnlyTopOffset = -15f;
    private const float PointWithMovementTopOffset = -134f;

    internal PointToolHud(ManualLogSource log)
    {
        _log = log;
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

            return;
        }

        if (!flight && !noclip)
        {
            if (_root != null && _movementText != null)
            {
                _movementText.text = string.Empty;
                Refresh();
            }

            return;
        }

        if (!EnsureCreated())
            return;

        _movementText.text = !flight && !noclip
            ? string.Empty
            : $"{(flight ? $"FLY ×{speed:0.0}" : string.Empty)}\n{(noclip ? "NOCLIP" : string.Empty)}".Trim();
        Refresh();
    }

    internal void SetPoint(string text)
    {
        if (!EnsureCreated())
            return;

        _pointText.text = text ?? string.Empty;
        Refresh();
    }

    internal void Hide()
    {
        if (!EnsureCreated())
            return;

        _movementText.text = string.Empty;
        _pointText.text = string.Empty;
        Refresh();
    }

    internal void ClearPointIfCreated()
    {
        if (_root == null || _pointText == null)
            return;

        _pointText.text = string.Empty;
        Refresh();
    }

    private bool EnsureCreated()
    {
        if (IsCreated())
            return true;

        if (Time.unscaledTime < _nextCreateAttempt)
            return false;

        // These are the same readiness boundaries used by the native UI and
        // the point-tool raycast. Do not create a permanent overlay in the
        // bootstrap/menu scene.
        if (MainCamera.instance == null
            || PlayerUI.window == null
            || Player.LocalPlayer == null)
            return false;

        _nextCreateAttempt = Time.unscaledTime + 1f;
        try
        {
            _font = CreateFont();
            if (_font == null)
            {
                if (!_hasLoggedFontFailure)
                {
                    _hasLoggedFontFailure = true;
                    _log.LogError("[PointToolHud] 无法创建系统字体或内置 Arial 回退字体，HUD 暂不显示。");
                }
                return false;
            }

            _root = new GameObject(
                "UnturnedSingleplayerCheatMenu.PointToolHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            UnityEngine.Object.DontDestroyOnLoad(_root);

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // Menu is 29900 and Unturned's native cursor is 30000.
            canvas.sortingOrder = 29950;

            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _movementText = CreateLabel(
                "Movement",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(300f, 100f),
                TextAnchor.UpperRight,
                18,
                out _movementPanel);
            _pointText = CreateLabel(
                "PointTarget",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(PointOnlyHorizontalOffset, PointOnlyTopOffset),
                new Vector2(300f, 100f),
                TextAnchor.UpperRight,
                13,
                out _pointPanel);
            _root.SetActive(false);

            if (!_hasLoggedCreation)
            {
                _hasLoggedCreation = true;
                _log.LogInfo(
                    $"[PointToolHud] created: rootActive={_root.activeSelf}, "
                    + $"canvasEnabled={canvas.enabled}, sortingOrder={canvas.sortingOrder}, "
                    + $"font={_font.name}, screen={Screen.width}x{Screen.height}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"[PointToolHud] 创建 HUD 失败，将在游戏 UI 就绪后重试：{ex}");
            DestroyGenerated();
            return false;
        }
    }

    private Font CreateFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" },
            18);
        if (font != null)
        {
            _ownsFont = true;
            return font;
        }

        _ownsFont = false;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private Text CreateLabel(
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        TextAnchor alignment,
        int fontSize,
        out GameObject container)
    {
        container = new GameObject(name, typeof(RectTransform), typeof(Image));
        container.transform.SetParent(_root.transform, false);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = anchorMin;
        containerRect.anchorMax = anchorMax;
        containerRect.pivot = pivot;
        containerRect.anchoredPosition = anchoredPosition;
        containerRect.sizeDelta = size;

        Image background = container.GetComponent<Image>();
        background.color = new Color(0.02f, 0.025f, 0.035f, 0.50f);
        background.raycastTarget = false;

        GameObject textObject = new($"{name}.Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(container.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        Text text = textObject.GetComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private bool IsCreated()
    {
        return _root != null
            && _root.GetComponent<Canvas>() != null
            && _movementText != null
            && _pointText != null
            && _font != null;
    }

    private void Refresh()
    {
        if (_root == null || _movementText == null || _pointText == null)
            return;

        _root.SetActive(
            !string.IsNullOrWhiteSpace(_movementText.text)
            || !string.IsNullOrWhiteSpace(_pointText.text));

        bool hasMovement = !string.IsNullOrWhiteSpace(_movementText.text);
        if (_movementPanel != null)
            _movementPanel.SetActive(hasMovement);
        if (_pointPanel != null)
        {
            RectTransform pointRect = _pointPanel.GetComponent<RectTransform>();
            pointRect.anchoredPosition = new Vector2(
                hasMovement ? -24f : PointOnlyHorizontalOffset,
                hasMovement ? PointWithMovementTopOffset : PointOnlyTopOffset);
            _pointPanel.SetActive(!string.IsNullOrWhiteSpace(_pointText.text));
        }
    }

    private void DestroyGenerated()
    {
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        if (_ownsFont && _font != null)
            UnityEngine.Object.Destroy(_font);

        _root = null;
        _movementPanel = null;
        _pointPanel = null;
        _movementText = null;
        _pointText = null;
        _font = null;
        _ownsFont = false;
    }
}
