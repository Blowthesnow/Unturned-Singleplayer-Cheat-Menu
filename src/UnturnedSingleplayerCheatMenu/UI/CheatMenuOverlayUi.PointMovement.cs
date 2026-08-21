using System;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
    private enum ToolsTab
    {
        Interaction,
        Movement
    }

    private ToolsTab _activeToolsTab = ToolsTab.Interaction;
    private static readonly PointToolMode[] PointToolModes =
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
    private void BuildToolsTab()
    {
        AddVerticalLayout(_contentHost, 8f, new RectOffset(10, 10, 9, 9));
        GameObject tabs = CreateRow(_contentHost.transform, 38f, 7f);
        AddToolsTab(tabs.transform, ToolsTab.Interaction, "交互");
        AddToolsTab(tabs.transform, ToolsTab.Movement, "移动");

        GameObject panel = CreateObject("ToolsPanel", _contentHost.transform, typeof(Image));
        panel.GetComponent<Image>().color = CheatMenuStyle.Surface;
        AddVerticalLayout(panel, 10f, new RectOffset(18, 18, 16, 16));
        SetLayout(panel, flexibleWidth: 1f, flexibleHeight: 1f);
        CreateLabel(panel.transform, _activeToolsTab == ToolsTab.Interaction ? "准星交互工具" : "移动工具", 19, CheatMenuStyle.Text);

        if (_activeToolsTab == ToolsTab.Interaction)
            BuildPointToolPanel(panel.transform);
        else
            BuildMovementPanel(panel.transform);
    }

    private void AddToolsTab(Transform parent, ToolsTab tab, string label)
    {
        Button button = CreateButton(parent, label, () =>
        {
            _activeToolsTab = tab;
            ReplaceContentHost();
            BuildToolsTab();
        }, 0f, CheatMenuStyle.TabHeight, _activeToolsTab == tab ? CheatMenuStyle.Accent : CheatMenuStyle.Surface, 14);
        SetLayout(button.gameObject, flexibleWidth: 1f);
    }

    private void BuildPointToolPanel(Transform panel)
    {
        GameObject header = CreateRow(panel, 40f, 7f);
        CreateToggleAction(header.transform, "启用准星交互工具", _plugin.PointTool.Enabled, value =>
        {
            _plugin.SetPointToolEnabled(value);
            SetStatus(value ? "准星交互工具已开启。" : "准星交互工具已关闭。", 5f, StatusKind.Info);
        });
        Button modeTrigger = null;
        modeTrigger = CreatePointToolModeButton(
            header.transform,
            _plugin.PointTool.Mode,
            () => TogglePointToolModeDrawer(modeTrigger));
        CreateLabel(panel, "中键触发；删除必须同时按住 Shift。", 13, CheatMenuStyle.Muted);
        CreateButton(panel, $"最大距离 {_plugin.PointTool.Range:0}m", () =>
        {
            float next = _plugin.PointTool.Range >= 250f ? 25f : _plugin.PointTool.Range + 25f;
            _plugin.SetPointToolRange(next);
            ReplaceContentHost();
            BuildToolsTab();
        }, 220f, 36f);
        GameObject display = CreateRow(panel, 40f, 7f);
        CreateToggleAction(display.transform, "名称", _plugin.PointTool.ShowTargetName, value => _plugin.SetPointToolShowTargetName(value));
        CreateToggleAction(display.transform, "ID / GUID", _plugin.PointTool.ShowId, value => _plugin.SetPointToolShowId(value));
        CreateToggleAction(display.transform, "生命 / 耐久", _plugin.PointTool.ShowHealth, value => _plugin.SetPointToolShowHealth(value));
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
            _ => "智能"
        };
    }
    private Button CreatePointToolModeButton(Transform parent, PointToolMode mode, Action action)
    {
        return CreateButton(
            parent,
            PointToolModeLabel(mode),
            action,
            150f,
            40f,
            _plugin.PointTool.Mode == mode ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
            12);
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
            return;
        if (_pointToolModeDrawerRoot != null)
            QueueForDestroy(_pointToolModeDrawerRoot);
        _pointToolModeDrawerRoot = CreateObject("PointToolModeDrawer", _root.transform, typeof(Image));
        Image popupImage = _pointToolModeDrawerRoot.GetComponent<Image>();
        popupImage.color = CheatMenuStyle.Panel;
        Outline popupOutline = _pointToolModeDrawerRoot.AddComponent<Outline>();
        popupOutline.effectColor = CheatMenuStyle.InputBorder;
        popupOutline.effectDistance = Vector2.one;
        popupOutline.useGraphicAlpha = false;
        _pointToolModeDrawerPopup = _pointToolModeDrawerRoot.GetComponent<RectTransform>();
        RectTransform popup = _pointToolModeDrawerPopup;
        popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
        popup.pivot = new Vector2(0f, 1f);
        popup.sizeDelta = new Vector2(150f, 280f);

        VerticalLayoutGroup popupLayout = _pointToolModeDrawerRoot.AddComponent<VerticalLayoutGroup>();
        popupLayout.padding = new RectOffset(4, 4, 4, 4);
        popupLayout.spacing = 4f;
        popupLayout.childControlWidth = true;
        popupLayout.childControlHeight = true;
        popupLayout.childForceExpandWidth = true;
        popupLayout.childForceExpandHeight = false;

        foreach (PointToolMode mode in PointToolModes)
        {
            PointToolMode capturedMode = mode;
            Button option = CreateButton(
                _pointToolModeDrawerRoot.transform,
                PointToolModeLabel(capturedMode),
                () =>
                {
                    _plugin.SetPointToolMode(capturedMode);
                    HidePointToolModeDrawer();
                    ReplaceContentHost();
                    BuildToolsTab();
                },
                0f,
                42f,
                _plugin.PointTool.Mode == capturedMode ? CheatMenuStyle.Accent : CheatMenuStyle.SurfaceHover,
                12);
            SetLayout(option.gameObject, preferredHeight: 42f, flexibleWidth: 1f);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_pointToolModeDrawerPopup);
        PositionPointToolModeDrawer();
    }
    private void MaintainPointToolModeDrawer()
    {
        if (_pointToolModeDrawerRoot == null)
            return;
        PositionPointToolModeDrawer();
        if (Input.GetMouseButtonDown(0)
            && _pointToolModeTriggerButton != null
            && _pointToolModeDrawerPopup != null
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _pointToolModeDrawerPopup,
                Input.mousePosition,
                null)
            && !RectTransformUtility.RectangleContainsScreenPoint(
                _pointToolModeTriggerButton.GetComponent<RectTransform>(),
                Input.mousePosition,
                null))
        {
            HidePointToolModeDrawer();
        }
    }
    private void PositionPointToolModeDrawer()
    {
        if (_pointToolModeTriggerButton == null
            || _pointToolModeDrawerPopup == null
            || _root == null)
            return;

        RectTransform triggerRect = _pointToolModeTriggerButton.GetComponent<RectTransform>();
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        triggerRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, bottomLeft, null, out Vector2 bottomLeftLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, topLeft, null, out Vector2 topLeftLocal);

        Vector2 popupSize = _pointToolModeDrawerPopup.sizeDelta;
        popupSize.x = triggerRect.rect.width;
        _pointToolModeDrawerPopup.sizeDelta = popupSize;
        _pointToolModeDrawerPopup.anchorMin = _pointToolModeDrawerPopup.anchorMax = new Vector2(0.5f, 0.5f);
        float spaceBelow = bottomLeftLocal.y - rootRect.rect.yMin;
        bool openAbove = spaceBelow < _pointToolModeDrawerPopup.rect.height + 8f;
        _pointToolModeDrawerPopup.pivot = openAbove ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        _pointToolModeDrawerPopup.anchoredPosition = openAbove ? topLeftLocal : bottomLeftLocal;
    }
    private void HidePointToolModeDrawer()
    {
        _pointToolModeDrawerOpen = false;
        if (_pointToolModeDrawerRoot != null)
            QueueForDestroy(_pointToolModeDrawerRoot);
        _pointToolModeDrawerRoot = null;
        _pointToolModeDrawerPopup = null;
        _pointToolModeTriggerButton = null;
    }
    private void BuildMovementPanel(Transform panel)
    {
        GameObject toggles = CreateRow(panel, 42f, 8f);
        CreateToggleAction(toggles.transform, "飞行", _plugin.Movement.FlightEnabled, value =>
        {
            _plugin.Movement.SetFlight(value);
            SetStatus(value ? "飞行已开启。" : "飞行已关闭。", 5f, StatusKind.Info);
        });
        CreateToggleAction(toggles.transform, "穿墙", _plugin.Movement.NoclipEnabled, value =>
        {
            _plugin.Movement.SetNoclip(value);
            SetStatus(value ? "穿墙已开启。" : "穿墙已关闭。", 5f, StatusKind.Info);
        });
        CreateMovementSpeedSlider(panel, "水平速度", _plugin.Movement.FlightSpeed, _plugin.SetFlightSpeed);
        CreateMovementSpeedSlider(panel, "垂直速度", _plugin.Movement.VerticalSpeed, _plugin.SetFlightVerticalSpeed);
        CreateToggleAction(panel, "关闭穿墙后寻找安全位置", _plugin.Movement.SafeExit, value => _plugin.SetNoclipSafeExit(value));
        CreateLabel(panel, "WASD 移动，Space 上升，Ctrl 下降。", 13, CheatMenuStyle.Muted);
    }

    private void CreateMovementSpeedSlider(Transform parent, string label, float value, Action<float> changed)
    {
        GameObject row = CreateRow(parent, 40f, 8f);
        CreateLabel(row.transform, label, 13, CheatMenuStyle.Text, 92f, TextAnchor.MiddleLeft);
        Text valueText = CreateText(row.transform, $"{value:0}x", 12, CheatMenuStyle.Muted, TextAnchor.MiddleRight, localize: false);
        SetLayout(valueText.gameObject, preferredWidth: 42f, preferredHeight: 30f);
        Slider slider = CreateTimeSlider(row.transform, Mathf.InverseLerp(1f, 10f, value), normalized =>
        {
            float snapped = Mathf.Clamp(Mathf.Round(Mathf.Lerp(1f, 10f, Mathf.Clamp01(normalized))), 1f, 10f);
            changed(snapped);
            valueText.text = $"{snapped:0}x";
        });
        SetLayout(slider.gameObject, flexibleWidth: 1f);
    }
}
