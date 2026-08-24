using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class TeleportMapSurface : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IDragHandler, IScrollHandler
{
	private const float MinZoom = 1f;

	private const float MaxZoom = 8f;

	private const float PlayerMarkerSize = 16f;

	private const float TeleportMarkerSize = 16f;

	private readonly List<TeleportMapMarker> _teleportMarkers = new List<TeleportMapMarker>();

	private readonly List<Sprite> _markerSprites = new List<Sprite>();

	private readonly List<Texture2D> _markerTextures = new List<Texture2D>();

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

	private float _zoom = 1f;

	private Vector2 _lastViewportSize;

	public float Zoom => _zoom;

	public void Initialize(Texture2D texture, Action<Vector3> rightClick, IReadOnlyList<TeleportPoint> teleportPoints, Action<TeleportPoint> markerRightClick)
	{
		_texture = texture;
		_rightClick = rightClick;
		_markerRightClick = markerRightClick;
		_viewport = GetComponent<RectTransform>();
		Image component = GetComponent<Image>();
		if (component != null)
		{
			component.color = CheatMenuStyle.SurfaceInput;
			component.raycastTarget = true;
		}
		Mask mask = GetComponent<Mask>();
		if (mask == null)
		{
			mask = base.gameObject.AddComponent<Mask>();
		}
		mask.showMaskGraphic = true;
		GameObject gameObject = new GameObject("MapContent", typeof(RectTransform));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		_content = gameObject.GetComponent<RectTransform>();
		RectTransform content = _content;
		Vector2 anchorMin = (_content.anchorMax = new Vector2(0.5f, 0.5f));
		content.anchorMin = anchorMin;
		_content.pivot = new Vector2(0.5f, 0.5f);
		_content.anchoredPosition = Vector2.zero;
		GameObject gameObject2 = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
		gameObject2.transform.SetParent(_content, worldPositionStays: false);
		_mapImage = gameObject2.GetComponent<RawImage>();
		_mapImage.texture = texture;
		_mapImage.color = Color.white;
		_mapImage.raycastTarget = false;
		Stretch(_mapImage.rectTransform);
		if (teleportPoints != null)
		{
			foreach (TeleportPoint teleportPoint in teleportPoints)
			{
				CreateTeleportMarker(teleportPoint);
			}
		}
		GameObject gameObject3 = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
		gameObject3.transform.SetParent(_content, worldPositionStays: false);
		_playerMarker = gameObject3.GetComponent<Image>();
		_playerMarker.sprite = TeleportMarkerIconFactory.CreateCircleSprite(Mathf.RoundToInt(16f), out _playerMarkerTexture);
		_playerMarkerSprite = _playerMarker.sprite;
		_playerMarker.color = new Color(0.1f, 0.48f, 0.88f, 1f);
		_playerMarker.raycastTarget = false;
		RectTransform rectTransform = _playerMarker.rectTransform;
		anchorMin = (_playerMarker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform.anchorMin = anchorMin;
		_playerMarker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		_playerMarker.rectTransform.sizeDelta = new Vector2(16f, 16f);
		GameObject gameObject4 = new GameObject("PlayerMarkerCenter", typeof(RectTransform), typeof(Image));
		gameObject4.transform.SetParent(_content, worldPositionStays: false);
		_playerMarkerCenter = gameObject4.GetComponent<Image>();
		_playerMarkerCenter.sprite = TeleportMarkerIconFactory.CreateCircleSprite(Mathf.RoundToInt(12.16f), out _playerMarkerCenterTexture);
		_playerMarkerCenterSprite = _playerMarkerCenter.sprite;
		_playerMarkerCenter.color = new Color(0.22f, 0.82f, 0.34f, 1f);
		_playerMarkerCenter.raycastTarget = false;
		RectTransform rectTransform2 = _playerMarkerCenter.rectTransform;
		anchorMin = (_playerMarkerCenter.rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform2.anchorMin = anchorMin;
		_playerMarkerCenter.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		_playerMarkerCenter.rectTransform.sizeDelta = new Vector2(12.16f, 12.16f);
		_zoom = 1f;
		_lastViewportSize = Vector2.zero;
		RefreshLayout(force: true);
	}

	public void RefreshPlayerMarker(Vector3 worldPosition)
	{
		RefreshLayout();
		if (!(_playerMarker == null) && !(_playerMarkerCenter == null) && !(_content == null))
		{
			Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(worldPosition);
			Vector2 anchoredPosition = NormalizedToContentPosition(normalized);
			_playerMarker.rectTransform.anchoredPosition = anchoredPosition;
			_playerMarkerCenter.rectTransform.anchoredPosition = anchoredPosition;
			_playerMarker.transform.SetAsLastSibling();
			_playerMarkerCenter.transform.SetAsLastSibling();
		}
	}

	public void CenterOnWorld(Vector3 worldPosition)
	{
		RefreshLayout(force: true);
		if (!(_content == null))
		{
			Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(worldPosition);
			Vector2 vector = NormalizedToContentPosition(normalized);
			_content.anchoredPosition = ClampContentPosition(-vector);
		}
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
			if (TryGetMapNormalizedPosition(eventData.position, out var normalized))
			{
				_rightClick?.Invoke(TeleportMapService.DeprojectMapToWorld(normalized));
				eventData.Use();
			}
		}
		else if (eventData.button == PointerEventData.InputButton.Left)
		{
			BeginDrag(eventData.position);
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			EndDrag();
		}
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
		if (_dragging && eventData.button == PointerEventData.InputButton.Left && !(_content == null))
		{
			RefreshLayout();
			Vector2 vector = eventData.position - _lastPointerPosition;
			_lastPointerPosition = eventData.position;
			_content.anchoredPosition = ClampContentPosition(_content.anchoredPosition + vector);
			HideMarkerTooltip();
		}
	}

	internal void Scroll(PointerEventData eventData)
	{
		if (!(Mathf.Abs(eventData.scrollDelta.y) < 0.01f))
		{
			RefreshLayout();
			SetZoom(_zoom + (float)Math.Sign(eventData.scrollDelta.y), eventData.position);
		}
	}

	internal void HandleMarkerRightClick(TeleportPoint point)
	{
		HideMarkerTooltip();
		_markerRightClick?.Invoke(point);
	}

	internal void ShowMarkerTooltip(TeleportPoint point, Vector2 screenPosition)
	{
		if (!(_viewport == null) && point != null)
		{
			EnsureTooltip();
			_tooltipPoint = point;
			_tooltipText.text = $"{point.Name}\n{point.Map} · X {point.X:F1}  Y {point.Y:F1}  Z {point.Z:F1}";
			_tooltipRoot.SetActive(value: true);
			_tooltipRoot.transform.SetAsLastSibling();
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPosition, null, out var localPoint))
			{
				Rect rect = _viewport.rect;
				Vector2 sizeDelta = _tooltipRoot.GetComponent<RectTransform>().sizeDelta;
				float x = Mathf.Clamp(localPoint.x + 14f, (0f - rect.width) * 0.5f, rect.width * 0.5f - sizeDelta.x);
				float y = Mathf.Clamp(localPoint.y - 14f, (0f - rect.height) * 0.5f + sizeDelta.y, rect.height * 0.5f);
				_tooltipRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
			}
		}
	}

	internal void HideMarkerTooltip(TeleportPoint point)
	{
		if (_tooltipPoint == point)
		{
			HideMarkerTooltip();
		}
	}

	private void HideMarkerTooltip()
	{
		_tooltipPoint = null;
		if (_tooltipRoot != null)
		{
			_tooltipRoot.SetActive(value: false);
		}
	}

	private void CreateTeleportMarker(TeleportPoint point)
	{
		if (point != null)
		{
			GameObject gameObject = new GameObject("TeleportMarker." + point.Id, typeof(RectTransform), typeof(Image));
			gameObject.transform.SetParent(_content, worldPositionStays: false);
			Image component = gameObject.GetComponent<Image>();
			component.sprite = TeleportMarkerIconFactory.CreateSprite(point.MarkerKind, Mathf.RoundToInt(16f), out var texture);
			component.color = ParseMarkerColor(point.MarkerColorHex);
			component.preserveAspect = true;
			component.raycastTarget = true;
			RectTransform component2 = gameObject.GetComponent<RectTransform>();
			Vector2 anchorMin = (gameObject.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0.5f));
			component2.anchorMin = anchorMin;
			gameObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
			gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(16f, 16f);
			Outline outline = gameObject.AddComponent<Outline>();
			outline.effectColor = Color.black;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			TeleportMapMarker teleportMapMarker = gameObject.AddComponent<TeleportMapMarker>();
			teleportMapMarker.Initialize(this, point);
			_teleportMarkers.Add(teleportMapMarker);
			_markerSprites.Add(component.sprite);
			_markerTextures.Add(texture);
		}
	}

	private void EnsureTooltip()
	{
		if (!(_tooltipRoot != null))
		{
			_tooltipRoot = new GameObject("TeleportMarkerTooltip", typeof(RectTransform), typeof(Image));
			_tooltipRoot.transform.SetParent(base.transform, worldPositionStays: false);
			Image component = _tooltipRoot.GetComponent<Image>();
			component.color = new Color(0.025f, 0.035f, 0.055f, 0.96f);
			component.raycastTarget = false;
			Outline outline = _tooltipRoot.AddComponent<Outline>();
			outline.effectColor = CheatMenuStyle.Accent;
			outline.effectDistance = Vector2.one;
			outline.useGraphicAlpha = false;
			RectTransform component2 = _tooltipRoot.GetComponent<RectTransform>();
			Vector2 anchorMin = (component2.anchorMax = new Vector2(0.5f, 0.5f));
			component2.anchorMin = anchorMin;
			component2.pivot = new Vector2(0f, 1f);
			component2.sizeDelta = new Vector2(300f, 56f);
			_tooltipText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
			_tooltipText.transform.SetParent(_tooltipRoot.transform, worldPositionStays: false);
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
			_tooltipRoot.SetActive(value: false);
		}
	}

	private void SetZoom(float requestedZoom, Vector2? screenFocus)
	{
		if (_content == null)
		{
			return;
		}
		RefreshLayout();
		float num = Mathf.Clamp(requestedZoom, 1f, 8f);
		if (!(Mathf.Abs(num - _zoom) < 0.001f))
		{
			Vector2 normalized = new Vector2(0.5f, 0.5f);
			if (screenFocus.HasValue && TryGetMapNormalizedPosition(screenFocus.Value, out var normalized2))
			{
				normalized = normalized2;
			}
			_zoom = num;
			RefreshLayout(force: true);
			if (screenFocus.HasValue && RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenFocus.Value, null, out var localPoint))
			{
				Vector2 vector = NormalizedToContentPosition(normalized);
				_content.anchoredPosition = ClampContentPosition(localPoint - vector);
			}
			HideMarkerTooltip();
		}
	}

	private bool TryGetMapNormalizedPosition(Vector2 screenPosition, out Vector2 normalized)
	{
		normalized = default(Vector2);
		if (_viewport == null || _content == null || _content.rect.width <= 0f || _content.rect.height <= 0f)
		{
			return false;
		}
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screenPosition, null, out var localPoint))
		{
			return false;
		}
		Vector2 vector = localPoint - _content.anchoredPosition;
		normalized = new Vector2(vector.x / _content.rect.width + 0.5f, 0.5f - vector.y / _content.rect.height);
		normalized.x = Mathf.Clamp01(normalized.x);
		normalized.y = Mathf.Clamp01(normalized.y);
		return true;
	}

	private void RefreshLayout(bool force = false)
	{
		if (_viewport == null || _content == null || _texture == null || _texture.width <= 0 || _texture.height <= 0)
		{
			return;
		}
		Vector2 size = _viewport.rect.size;
		if (!(size.x <= 1f) && !(size.y <= 1f))
		{
			if (!force && size == _lastViewportSize)
			{
				RefreshTeleportMarkerPositions();
				return;
			}
			_lastViewportSize = size;
			float num = Mathf.Min(size.x / (float)_texture.width, size.y / (float)_texture.height);
			Vector2 vector = new Vector2((float)_texture.width * num, (float)_texture.height * num);
			_content.sizeDelta = vector * _zoom;
			_content.anchoredPosition = ClampContentPosition(_content.anchoredPosition);
			RefreshTeleportMarkerPositions();
		}
	}

	private void RefreshTeleportMarkerPositions()
	{
		if (_content == null)
		{
			return;
		}
		foreach (TeleportMapMarker teleportMarker in _teleportMarkers)
		{
			if (!(teleportMarker == null))
			{
				Vector2 normalized = TeleportMapService.ProjectWorldPositionToMap(teleportMarker.Point.Position);
				RectTransform component = teleportMarker.GetComponent<RectTransform>();
				component.anchoredPosition = NormalizedToContentPosition(normalized);
				component.localScale = Vector3.one;
			}
		}
	}

	private Vector2 NormalizedToContentPosition(Vector2 normalized)
	{
		return new Vector2((normalized.x - 0.5f) * _content.rect.width, (0.5f - normalized.y) * _content.rect.height);
	}

	private Vector2 ClampContentPosition(Vector2 position)
	{
		if (_viewport == null || _content == null)
		{
			return position;
		}
		Vector2 size = _viewport.rect.size;
		Vector2 size2 = _content.rect.size;
		float num = Mathf.Max(0f, (size2.x - size.x) * 0.5f);
		float num2 = Mathf.Max(0f, (size2.y - size.y) * 0.5f);
		return new Vector2(Mathf.Clamp(position.x, 0f - num, num), Mathf.Clamp(position.y, 0f - num2, num2));
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
		{
			UnityEngine.Object.Destroy(_tooltipRoot);
		}
		foreach (Sprite markerSprite in _markerSprites)
		{
			if (markerSprite != null)
			{
				UnityEngine.Object.Destroy(markerSprite);
			}
		}
		foreach (Texture2D markerTexture in _markerTextures)
		{
			if (markerTexture != null)
			{
				UnityEngine.Object.Destroy(markerTexture);
			}
		}
		if (_playerMarkerSprite != null)
		{
			UnityEngine.Object.Destroy(_playerMarkerSprite);
		}
		if (_playerMarkerTexture != null)
		{
			UnityEngine.Object.Destroy(_playerMarkerTexture);
		}
		if (_playerMarkerCenterSprite != null)
		{
			UnityEngine.Object.Destroy(_playerMarkerCenterSprite);
		}
		if (_playerMarkerCenterTexture != null)
		{
			UnityEngine.Object.Destroy(_playerMarkerCenterTexture);
		}
		_markerSprites.Clear();
		_markerTextures.Clear();
		_teleportMarkers.Clear();
	}

	private static Color ParseMarkerColor(string value)
	{
		if (!ColorUtility.TryParseHtmlString(value, out var color))
		{
			return new Color(0.96f, 0.77f, 0.26f, 1f);
		}
		return color;
	}
}
