using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class TeleportMapSurface :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IScrollHandler
{
    private const float MinZoom = 1f;
    private const float MaxZoom = 8f;
    private const float PlayerMarkerSize = 16f;
    private const float TeleportMarkerSize = PlayerMarkerSize;

    private readonly List<TeleportMapMarker> _teleportMarkers = new();
    private readonly List<Sprite> _markerSprites = new();
    private readonly List<Texture2D> _markerTextures = new();

    private RectTransform _viewport;
    private RectTransform _content;
    private RawImage _mapImage;
    private Image _playerMarker;
    private Image _playerMarkerCenter;
    private Texture2D _texture;
    private Texture2D _playerMarkerTexture;
    private Texture2D _playerMarkerCenterTexture;
    private Sprite _playerMarkerSprite;
    private Sprite _playerMarkerCenterSprite;
    private Action<Vector3> _rightClick;
    private Action<TeleportPoint> _markerRightClick;
    private GameObject _tooltipRoot;
    private Text _tooltipText;
    private TeleportPoint _tooltipPoint;
    private Vector2 _lastPointerPosition;
    private bool _dragging;
    private float _zoom = MinZoom;
    private Vector2 _lastViewportSize;

    public float Zoom => _zoom;

    public void Initialize(
        Texture2D texture,
        Action<Vector3> rightClick,
        IReadOnlyList<TeleportPoint> teleportPoints,
        Action<TeleportPoint> markerRightClick)
    {
        _texture = texture;
        _rightClick = rightClick;
        _markerRightClick = markerRightClick;
        _viewport = GetComponent<RectTransform>();

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = CheatMenuStyle.SurfaceInput;
            background.raycastTarget = true;
        }

        Mask mask = GetComponent<Mask>();
        if (mask == null)
            mask = gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject contentObject = new("MapContent", typeof(RectTransform));
        contentObject.transform.SetParent(transform, false);
        _content = contentObject.GetComponent<RectTransform>();
        _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
        _content.pivot = new Vector2(0.5f, 0.5f);
        _content.anchoredPosition = Vector2.zero;

        GameObject imageObject = new("MapImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(_content, false);
        _mapImage = imageObject.GetComponent<RawImage>();
        _mapImage.texture = texture;
        _mapImage.color = Color.white;
        _mapImage.raycastTarget = false;
        Stretch(_mapImage.rectTransform);

        if (teleportPoints != null)
        {
            foreach (TeleportPoint point in teleportPoints)
                CreateTeleportMarker(point);
        }

        GameObject markerObject = new("PlayerMarker", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(_content, false);
        _playerMarker = markerObject.GetComponent<Image>();
        _playerMarker.sprite = TeleportMarkerIconFactory.CreateCircleSprite(
            Mathf.RoundToInt(PlayerMarkerSize),
            out _playerMarkerTexture);
        _playerMarkerSprite = _playerMarker.sprite;
        _playerMarker.color = new Color(0.10f, 0.48f, 0.88f, 1f);
        _playerMarker.raycastTarget = false;
        _playerMarker.rectTransform.anchorMin = _playerMarker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _playerMarker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _playerMarker.rectTransform.sizeDelta = new Vector2(PlayerMarkerSize, PlayerMarkerSize);

        GameObject playerCenterObject = new("PlayerMarkerCenter", typeof(RectTransform), typeof(Image));
        playerCenterObject.transform.SetParent(_content, false);
        _playerMarkerCenter = playerCenterObject.GetComponent<Image>();
        _playerMarkerCenter.sprite = TeleportMarkerIconFactory.CreateCircleSprite(
            Mathf.RoundToInt(PlayerMarkerSize * 0.76f),
            out _playerMarkerCenterTexture);
        _playerMarkerCenterSprite = _playerMarkerCenter.sprite;
        _playerMarkerCenter.color = new Color(0.22f, 0.82f, 0.34f, 1f);
        _playerMarkerCenter.raycastTarget = false;
        _playerMarkerCenter.rectTransform.anchorMin = _playerMarkerCenter.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _playerMarkerCenter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _playerMarkerCenter.rectTransform.sizeDelta = new Vector2(
            PlayerMarkerSize * 0.76f,
            PlayerMarkerSize * 0.76f);

        _zoom = MinZoom;
        _lastViewportSize = Vector2.zero;
        RefreshLayout(force: true);
    }

    public void RefreshPlayerMarker(Vector3 worldPosition)
    {
        RefreshLayout();
        if (_playerMarker == null || _playerMarkerCenter == null || _content == null)
            return;

        Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(worldPosition);
        Vector2 position = NormalizedToContentPosition(normalized);
        _playerMarker.rectTransform.anchoredPosition = position;
        _playerMarkerCenter.rectTransform.anchoredPosition = position;
        _playerMarker.transform.SetAsLastSibling();
        _playerMarkerCenter.transform.SetAsLastSibling();
    }

    public void CenterOnWorld(Vector3 worldPosition)
    {
        RefreshLayout(force: true);
        if (_content == null)
            return;

        Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(worldPosition);
        Vector2 target = NormalizedToContentPosition(normalized);
        _content.anchoredPosition = ClampContentPosition(-target);
    }

    public void ZoomIn()
    {
        SetZoom(_zoom + 1f, null);
    }

    public void ZoomOut()
    {
        SetZoom(_zoom - 1f, null);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (TryGetMapNormalizedPosition(eventData.position, out Vector2 normalized))
            {
                _rightClick?.Invoke(TeleportMapService.DeprojectMapToWorld(normalized));
                eventData.Use();
            }
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
            BeginDrag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            EndDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Drag(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        Scroll(eventData);
    }

    internal void BeginDrag(Vector2 pointerPosition)
    {
        _dragging = true;
        _lastPointerPosition = pointerPosition;
    }

    internal void EndDrag()
    {
        _dragging = false;
    }

    internal void Drag(PointerEventData eventData)
    {
        if (!_dragging || eventData.button != PointerEventData.InputButton.Left || _content == null)
            return;

        RefreshLayout();
        Vector2 delta = eventData.position - _lastPointerPosition;
        _lastPointerPosition = eventData.position;
        _content.anchoredPosition = ClampContentPosition(_content.anchoredPosition + delta);
        HideMarkerTooltip();
    }

    internal void Scroll(PointerEventData eventData)
    {
        if (Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            return;

        RefreshLayout();
        SetZoom(_zoom + Math.Sign(eventData.scrollDelta.y), eventData.position);
    }

    internal void HandleMarkerRightClick(TeleportPoint point)
    {
        HideMarkerTooltip();
        _markerRightClick?.Invoke(point);
    }

    internal void ShowMarkerTooltip(TeleportPoint point, Vector2 screenPosition)
    {
        if (_viewport == null || point == null)
            return;

        EnsureTooltip();
        _tooltipPoint = point;
        _tooltipText.text =
            $"{point.Name}\n{point.Map} · X {point.X:F1}  Y {point.Y:F1}  Z {point.Z:F1}";
        _tooltipRoot.SetActive(true);
        _tooltipRoot.transform.SetAsLastSibling();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                screenPosition,
                null,
                out Vector2 localPosition))
        {
            return;
        }

        Rect viewportRect = _viewport.rect;
        Vector2 tooltipSize = _tooltipRoot.GetComponent<RectTransform>().sizeDelta;
        float x = Mathf.Clamp(
            localPosition.x + 14f,
            -viewportRect.width * 0.5f,
            viewportRect.width * 0.5f - tooltipSize.x);
        float y = Mathf.Clamp(
            localPosition.y - 14f,
            -viewportRect.height * 0.5f + tooltipSize.y,
            viewportRect.height * 0.5f);
        _tooltipRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
    }

    internal void HideMarkerTooltip(TeleportPoint point)
    {
        if (_tooltipPoint == point)
            HideMarkerTooltip();
    }

    private void HideMarkerTooltip()
    {
        _tooltipPoint = null;
        if (_tooltipRoot != null)
            _tooltipRoot.SetActive(false);
    }

    private void CreateTeleportMarker(TeleportPoint point)
    {
        if (point == null)
            return;

        GameObject markerObject = new($"TeleportMarker.{point.Id}", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(_content, false);
        Image image = markerObject.GetComponent<Image>();
        image.sprite = TeleportMarkerIconFactory.CreateSprite(
            point.MarkerKind,
            Mathf.RoundToInt(TeleportMarkerSize),
            out Texture2D texture);
        image.color = ParseMarkerColor(point.MarkerColorHex);
        image.preserveAspect = true;
        image.raycastTarget = true;
        markerObject.GetComponent<RectTransform>().anchorMin =
            markerObject.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f);
        markerObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        markerObject.GetComponent<RectTransform>().sizeDelta = new Vector2(TeleportMarkerSize, TeleportMarkerSize);
        Outline outline = markerObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;

        TeleportMapMarker marker = markerObject.AddComponent<TeleportMapMarker>();
        marker.Initialize(this, point);
        _teleportMarkers.Add(marker);
        _markerSprites.Add(image.sprite);
        _markerTextures.Add(texture);
    }

    private void EnsureTooltip()
    {
        if (_tooltipRoot != null)
            return;

        _tooltipRoot = new GameObject("TeleportMarkerTooltip", typeof(RectTransform), typeof(Image));
        _tooltipRoot.transform.SetParent(transform, false);
        Image background = _tooltipRoot.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.055f, 0.96f);
        background.raycastTarget = false;
        Outline outline = _tooltipRoot.AddComponent<Outline>();
        outline.effectColor = CheatMenuStyle.Accent;
        outline.effectDistance = Vector2.one;
        outline.useGraphicAlpha = false;
        RectTransform rect = _tooltipRoot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(300f, 56f);

        _tooltipText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        _tooltipText.transform.SetParent(_tooltipRoot.transform, false);
        _tooltipText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _tooltipText.fontSize = 12;
        _tooltipText.color = Color.white;
        _tooltipText.alignment = TextAnchor.MiddleLeft;
        _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
        _tooltipText.raycastTarget = false;
        _tooltipText.rectTransform.anchorMin = Vector2.zero;
        _tooltipText.rectTransform.anchorMax = Vector2.one;
        _tooltipText.rectTransform.offsetMin = new Vector2(9f, 5f);
        _tooltipText.rectTransform.offsetMax = new Vector2(-9f, -5f);
        _tooltipRoot.SetActive(false);
    }

    private void SetZoom(float requestedZoom, Vector2? screenFocus)
    {
        if (_content == null)
            return;

        RefreshLayout();
        float targetZoom = Mathf.Clamp(requestedZoom, MinZoom, MaxZoom);
        if (Mathf.Abs(targetZoom - _zoom) < 0.001f)
            return;

        Vector2 normalizedFocus = new(0.5f, 0.5f);
        if (screenFocus.HasValue && TryGetMapNormalizedPosition(screenFocus.Value, out Vector2 focus))
            normalizedFocus = focus;

        _zoom = targetZoom;
        RefreshLayout(force: true);

        if (screenFocus.HasValue
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                screenFocus.Value,
                null,
                out Vector2 viewportPoint))
        {
            Vector2 focusInContent = NormalizedToContentPosition(normalizedFocus);
            _content.anchoredPosition = ClampContentPosition(viewportPoint - focusInContent);
        }

        HideMarkerTooltip();
    }

    private bool TryGetMapNormalizedPosition(Vector2 screenPosition, out Vector2 normalized)
    {
        normalized = default;
        if (_viewport == null || _content == null || _content.rect.width <= 0f || _content.rect.height <= 0f)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPosition, null, out Vector2 local))
            return false;

        Vector2 contentLocal = local - _content.anchoredPosition;
        normalized = new Vector2(
            contentLocal.x / _content.rect.width + 0.5f,
            0.5f - contentLocal.y / _content.rect.height);
        normalized.x = Mathf.Clamp01(normalized.x);
        normalized.y = Mathf.Clamp01(normalized.y);
        return true;
    }

    private void RefreshLayout(bool force = false)
    {
        if (_viewport == null || _content == null || _texture == null || _texture.width <= 0 || _texture.height <= 0)
            return;

        Vector2 viewportSize = _viewport.rect.size;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f)
            return;
        if (!force && viewportSize == _lastViewportSize)
        {
            RefreshTeleportMarkerPositions();
            return;
        }

        _lastViewportSize = viewportSize;
        float fitScale = Mathf.Min(viewportSize.x / _texture.width, viewportSize.y / _texture.height);
        Vector2 baseSize = new(_texture.width * fitScale, _texture.height * fitScale);
        _content.sizeDelta = baseSize * _zoom;
        _content.anchoredPosition = ClampContentPosition(_content.anchoredPosition);
        RefreshTeleportMarkerPositions();
    }

    private void RefreshTeleportMarkerPositions()
    {
        if (_content == null)
            return;

        foreach (TeleportMapMarker marker in _teleportMarkers)
        {
            if (marker == null)
                continue;

            Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(marker.Point.Position);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.anchoredPosition = NormalizedToContentPosition(normalized);
            rect.localScale = Vector3.one;
        }
    }

    private Vector2 NormalizedToContentPosition(Vector2 normalized)
    {
        return new Vector2(
            (normalized.x - 0.5f) * _content.rect.width,
            (0.5f - normalized.y) * _content.rect.height);
    }

    private Vector2 ClampContentPosition(Vector2 position)
    {
        if (_viewport == null || _content == null)
            return position;

        Vector2 viewport = _viewport.rect.size;
        Vector2 content = _content.rect.size;
        float maxX = Mathf.Max(0f, (content.x - viewport.x) * 0.5f);
        float maxY = Mathf.Max(0f, (content.y - viewport.y) * 0.5f);
        return new Vector2(
            Mathf.Clamp(position.x, -maxX, maxX),
            Mathf.Clamp(position.y, -maxY, maxY));
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_tooltipRoot != null)
            Destroy(_tooltipRoot);
        foreach (Sprite sprite in _markerSprites)
        {
            if (sprite != null)
                Destroy(sprite);
        }
        foreach (Texture2D texture in _markerTextures)
        {
            if (texture != null)
                Destroy(texture);
        }
        if (_playerMarkerSprite != null)
            Destroy(_playerMarkerSprite);
        if (_playerMarkerTexture != null)
            Destroy(_playerMarkerTexture);
        if (_playerMarkerCenterSprite != null)
            Destroy(_playerMarkerCenterSprite);
        if (_playerMarkerCenterTexture != null)
            Destroy(_playerMarkerCenterTexture);
        _markerSprites.Clear();
        _markerTextures.Clear();
        _teleportMarkers.Clear();
    }

    private static Color ParseMarkerColor(string value)
    {
        return ColorUtility.TryParseHtmlString(value, out Color color)
            ? color
            : new Color(0.96f, 0.77f, 0.26f, 1f);
    }
}

internal sealed class TeleportMapMarker :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IScrollHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private TeleportMapSurface _owner;

    public TeleportPoint Point { get; private set; }

    public void Initialize(TeleportMapSurface owner, TeleportPoint point)
    {
        _owner = owner;
        Point = point;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _owner?.HandleMarkerRightClick(Point);
            eventData.Use();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
            _owner?.BeginDrag(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            _owner?.EndDrag();
    }

    public void OnDrag(PointerEventData eventData)
    {
        _owner?.Drag(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        _owner?.Scroll(eventData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowMarkerTooltip(Point, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideMarkerTooltip(Point);
    }
}
