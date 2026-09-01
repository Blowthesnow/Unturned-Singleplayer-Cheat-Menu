using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class MinimapHud : IDisposable
{
	private sealed class View
	{
		internal GameObject Root;
		internal RectTransform MapContent;
		internal RawImage Background;
		internal RawImage ObjectOverlay;
		internal Image Player;
		internal MinimapMarkerPool Pool;
		internal UiText State;
		internal RectTransform CompassRoot;
		internal UiText[] Compass;
		internal MinimapBackgroundState LastState;
		internal bool HasState;
	}

	private const float CoordinateHeight = 24f;
	private const float SafeMargin = 12f;
	private const float CircleBorderExpansionPixels = 2f;
	private const float LayoutPromptRefreshSeconds = 1f;
	private const string LayoutPromptText = "拖动小地图可调整位置，拖动右下角可缩放；按回车键应用，按 Esc 取消";
	private static readonly string[] CompassSourceLabels = { "北", "东", "南", "西" };
	private readonly PluginUiRoot _uiRoot;
	private readonly ManualLogSource _log;
	private readonly Action<MinimapAnchor, float, float, float> _layoutCommitted;
	private readonly Dictionary<TeleportMarkerKind, Sprite> _teleportSprites = new Dictionary<TeleportMarkerKind, Sprite>();
	private readonly List<Texture2D> _ownedTextures = new List<Texture2D>();
	private readonly List<Sprite> _ownedSprites = new List<Sprite>();
	private readonly Vector3[] _rootWorldCorners = new Vector3[4];
	private GameObject _root;
	private RectTransform _rootRect;
	private Image _dragSurface;
	private Outline _editOutline;
	private GameObject _resizeHandle;
	private LegacyFluentSurface _resizeSurface;
	private int _accentRevision = -1;
	private PluginLanguage? _lastLanguage;
	private UiText _coordinates;
	private View _circle;
	private View _square;
	private Texture2D _gridTexture;
	private Sprite _maskSprite;
	private Sprite _circleBorderSprite;
	private Sprite _dotSprite;
	private Sprite _playerSprite;
	private Sprite _nativeMarkerSprite;
	private bool _nativeIconsBound;
	private bool _layoutDragging;
	private bool _runtimeVisible;
	private bool _layoutPromptVisible;
	private float _nextLayoutPromptTime;
	private MinimapSettings _layoutStartSettings;
	private MinimapSettings _settings = new MinimapSettings();
	private IReadOnlyList<MinimapHudMarker> _markers = Array.Empty<MinimapHudMarker>();

	internal bool IsAlive => _root != null && _uiRoot.HudLayer != null;

	internal MinimapHud(PluginUiRoot uiRoot, ManualLogSource log,
		Action<MinimapAnchor, float, float, float> layoutCommitted)
	{
		_uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
		_log = log;
		_layoutCommitted = layoutCommitted;
		Initialize();
	}

	private void Initialize()
	{
		Font font = SystemFontProvider.Resolve(_log);
		_gridTexture = CreateGridTexture();
		_ownedTextures.Add(_gridTexture);
		_maskSprite = CreateCircleSprite(128, out Texture2D maskTexture);
		_ownedTextures.Add(maskTexture);
		_ownedSprites.Add(_maskSprite);
		_circleBorderSprite = CreateCircleBorderSprite(128, 1f, out Texture2D circleBorderTexture);
		_ownedTextures.Add(circleBorderTexture);
		_ownedSprites.Add(_circleBorderSprite);
		_dotSprite = TeleportMarkerIconFactory.CreateCircleSprite(16, out Texture2D dotTexture);
		_ownedTextures.Add(dotTexture);
		_ownedSprites.Add(_dotSprite);
		_playerSprite = CreateFallbackPlayerSprite(out Texture2D playerTexture);
		_ownedTextures.Add(playerTexture);
		_ownedSprites.Add(_playerSprite);

		_root = new GameObject("MinimapRoot", typeof(RectTransform), typeof(Image));
		_root.transform.SetParent(_uiRoot.HudLayer, false);
		_rootRect = _root.GetComponent<RectTransform>();
		_dragSurface = _root.GetComponent<Image>();
		_dragSurface.color = Color.clear;
		_dragSurface.raycastTarget = false;
		_editOutline = _root.AddComponent<Outline>();
		_editOutline.effectColor = CheatMenuStyle.Theme.Focus.ToUnity();
		_editOutline.effectDistance = new Vector2(2f, 2f);
		_editOutline.enabled = false;
		MinimapLayoutHandle drag = _root.AddComponent<MinimapLayoutHandle>();
		drag.Initialize(this, resize: false);

		_circle = CreateView("CircleViewport", font, circle: true);
		_square = CreateView("SquareViewport", font, circle: false);
		_coordinates = CreateText("Coordinates", _root.transform, font, 12, TextAnchor.MiddleCenter);
		_coordinates.rectTransform.anchorMin = new Vector2(0f, 0f);
		_coordinates.rectTransform.anchorMax = new Vector2(1f, 0f);
		_coordinates.rectTransform.pivot = new Vector2(0.5f, 0f);
		_coordinates.rectTransform.sizeDelta = new Vector2(0f, CoordinateHeight);
		_coordinates.rectTransform.anchoredPosition = Vector2.zero;

		_resizeHandle = new GameObject("ResizeHandle", typeof(RectTransform), typeof(Image));
		_resizeHandle.transform.SetParent(_root.transform, false);
		RectTransform resizeRect = _resizeHandle.GetComponent<RectTransform>();
		resizeRect.anchorMin = resizeRect.anchorMax = new Vector2(1f, 0f);
		resizeRect.pivot = new Vector2(1f, 0f);
		resizeRect.sizeDelta = new Vector2(22f, 22f);
		Image resizeImage = _resizeHandle.GetComponent<Image>();
		resizeImage.color = Color.clear;
		resizeImage.raycastTarget = true;
		_resizeSurface = LegacyFluentSurface.Attach(_resizeHandle, CheatMenuStyle.Theme.Focus.ToUnity(), CheatMenuStyle.Theme.BorderStrong.ToUnity(), CheatMenuStyle.Theme.RadiusField);
		RefreshAccentVisuals();
		MinimapLayoutHandle resize = _resizeHandle.AddComponent<MinimapLayoutHandle>();
		resize.Initialize(this, resize: true);
		_resizeHandle.SetActive(false);
		TryBindNativeIcons();
	}

	private View CreateView(string name, Font font, bool circle)
	{
		GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
		root.transform.SetParent(_root.transform, false);
		RectTransform rect = root.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = Vector2.zero;
		Image viewportImage = root.GetComponent<Image>();
		viewportImage.raycastTarget = false;
		if (circle)
		{
			viewportImage.sprite = _maskSprite;
			viewportImage.color = Color.white;
			// The mask is scoped to map pixels only.
			Mask mask = root.AddComponent<Mask>();
			mask.showMaskGraphic = false;
		}
		else
		{
			viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
			root.AddComponent<RectMask2D>();
		}

		GameObject mapContentObject = new GameObject("RotatingMapContent", typeof(RectTransform));
		mapContentObject.transform.SetParent(root.transform, false);
		RectTransform mapContent = mapContentObject.GetComponent<RectTransform>();
		Center(mapContent);
		RawImage background = CreateRawImage("Background", mapContent.transform);
		background.texture = _gridTexture;
		RawImage objectOverlay = CreateRawImage("LoadedBuildings", mapContent.transform);
		GameObject markerObject = new GameObject("MarkerRoot", typeof(RectTransform));
		markerObject.transform.SetParent(mapContent.transform, false);
		RectTransform markerRoot = markerObject.GetComponent<RectTransform>();
		Stretch(markerRoot);
		MinimapMarkerPool pool = new MinimapMarkerPool(markerRoot);
		GameObject compassObject = new GameObject("RotatingCompass", typeof(RectTransform));
		compassObject.transform.SetParent(root.transform, false);
		RectTransform compassRoot = compassObject.GetComponent<RectTransform>();
		Stretch(compassRoot);
		UiText[] compass = CreateCompass(compassRoot, font);
		GameObject playerObject = new GameObject("Player", typeof(RectTransform), typeof(Image));
		playerObject.transform.SetParent(root.transform, false);
		Image player = playerObject.GetComponent<Image>();
		player.sprite = _playerSprite;
		player.color = Color.white;
		player.preserveAspect = true;
		player.raycastTarget = false;
		RectTransform playerRect = player.rectTransform;
		playerRect.anchorMin = playerRect.anchorMax = new Vector2(0.5f, 0.5f);
		playerRect.pivot = new Vector2(0.5f, 0.5f);
		playerRect.sizeDelta = new Vector2(22f, 22f);
		UiText state = CreateText("BackgroundState", root.transform, font, 10, TextAnchor.LowerLeft);
		state.rectTransform.anchorMin = Vector2.zero;
		state.rectTransform.anchorMax = Vector2.one;
		state.rectTransform.offsetMin = new Vector2(5f, 4f);
		state.rectTransform.offsetMax = new Vector2(-5f, -4f);
		CreateViewportBorder(root.transform, circle);
		return new View
		{
			Root = root,
			MapContent = mapContent,
			Background = background,
			ObjectOverlay = objectOverlay,
			Player = player,
			Pool = pool,
			State = state,
			CompassRoot = compassRoot,
			Compass = compass
		};
	}

	internal void ApplySettings(MinimapSettings settings, bool runtimeVisible)
	{
		MinimapSettings next = (settings ?? new MinimapSettings()).Normalize().Clone();
		bool enteringLayoutEdit = !_settings.LayoutEditMode && next.LayoutEditMode;
		bool leavingLayoutEdit = _settings.LayoutEditMode && !next.LayoutEditMode;
		if (enteringLayoutEdit)
		{
			_layoutStartSettings = next.Clone();
			_layoutStartSettings.LayoutEditMode = false;
		}
		_settings = next;
		_runtimeVisible = runtimeVisible;
		bool visible = _runtimeVisible && _settings.Enabled;
		_root.SetActive(visible);
		ApplyViewportSize();
		if (!_layoutDragging) ApplyAnchor();
		if (!_settings.RotateWithView)
		{
			ApplyViewRotation(_circle, 0f);
			ApplyViewRotation(_square, 0f);
		}
		_circle.Root.SetActive(_settings.Shape == MinimapShape.Circle);
		_square.Root.SetActive(_settings.Shape == MinimapShape.Square);
		SetCompassVisible(_circle, _settings.ShowCompass);
		SetCompassVisible(_square, _settings.ShowCompass);
		_coordinates.gameObject.SetActive(_settings.ShowCoordinates);
		_editOutline.enabled = _settings.LayoutEditMode;
		_resizeHandle.SetActive(_settings.LayoutEditMode);
		_dragSurface.raycastTarget = _settings.LayoutEditMode;
		_uiRoot.SetHudInteraction(visible && _settings.LayoutEditMode);
		MinimapLayoutInputGate.SetEditing(visible && _settings.LayoutEditMode);
		ClampToSafeArea();
		ApplyMarkersToViews();
		if (enteringLayoutEdit && visible) UpdateLayoutPrompt(force: true);
		else if (leavingLayoutEdit) HideLayoutPrompt();
	}

	internal void SetRuntimeVisible(bool runtimeVisible)
	{
		_runtimeVisible = runtimeVisible;
		bool visible = _runtimeVisible && _settings.Enabled;
		_root.SetActive(visible);
		_uiRoot.SetHudInteraction(visible && _settings.LayoutEditMode);
		MinimapLayoutInputGate.SetEditing(visible && _settings.LayoutEditMode);
		if (!visible) HideLayoutPrompt();
		else if (_settings.LayoutEditMode) UpdateLayoutPrompt(force: true);
	}

	internal void SetBackground(MinimapBackgroundState state, NativeMapCropSnapshot nativeMap,
		Texture realtimeTerrain, Texture loadedBuildings)
	{
		SetViewBackground(_circle, state, nativeMap, realtimeTerrain, loadedBuildings);
		SetViewBackground(_square, state, nativeMap, realtimeTerrain, loadedBuildings);
	}

	internal void SetMarkers(IReadOnlyList<MinimapHudMarker> markers)
	{
		_markers = markers ?? Array.Empty<MinimapHudMarker>();
		TryBindNativeIcons();
		ApplyMarkersToViews();
	}

	internal void UpdatePose(Vector3 position, float playerYaw, float viewYaw, float mapYaw)
	{
		RefreshAccentVisuals();
		RefreshLocalizedText();
		UpdateLayoutEditInput();
		TryBindNativeIcons();
		float contentRotation = MinimapProjection.ResolveMapContentRotation(
			_settings.RotateWithView, viewYaw, mapYaw);
		ApplyViewRotation(_circle, contentRotation);
		ApplyViewRotation(_square, contentRotation);
		float headingReferenceYaw = _settings.RotateWithView ? viewYaw : mapYaw;
		float rotation = MinimapProjection.ResolvePlayerMarkerRotation(playerYaw, headingReferenceYaw);
		_circle.Player.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
		_square.Player.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
		string text = PluginLocalization.Translate($"横坐标 {position.x:0.0}  高度 {position.y:0.0}  纵坐标 {position.z:0.0}");
		if (!string.Equals(_coordinates.text, text, StringComparison.Ordinal)) _coordinates.text = text;
		ClampToSafeArea();
	}

	private static void ApplyViewRotation(View view, float contentRotation)
	{
		Quaternion rotation = Quaternion.Euler(0f, 0f, contentRotation);
		view.MapContent.localRotation = rotation;
		view.CompassRoot.localRotation = rotation;
		Quaternion upright = Quaternion.Euler(0f, 0f, -contentRotation);
		for (int index = 0; index < view.Compass.Length; index++)
		{
			view.Compass[index].rectTransform.localRotation = upright;
		}
	}

	private void RefreshAccentVisuals()
	{
		if (_accentRevision == CheatMenuStyle.AccentRevision)
		{
			return;
		}
		_accentRevision = CheatMenuStyle.AccentRevision;
		if (_editOutline != null)
		{
			_editOutline.effectColor = CheatMenuStyle.Theme.Focus.ToUnity();
		}
		_resizeSurface?.SetColors(CheatMenuStyle.Theme.Focus.ToUnity(), CheatMenuStyle.Theme.BorderStrong.ToUnity());
	}

	private void RefreshLocalizedText()
	{
		PluginLanguage language = PluginLocalization.CurrentLanguage;
		if (_lastLanguage == language)
		{
			return;
		}
		_lastLanguage = language;
		RefreshViewLanguage(_circle);
		RefreshViewLanguage(_square);
		if (_layoutPromptVisible)
		{
			UpdateLayoutPrompt(force: true);
		}
	}

	private static void RefreshViewLanguage(View view)
	{
		if (view == null)
		{
			return;
		}
		if (view.HasState && view.State != null)
		{
			view.State.text = PluginLocalization.Translate(MinimapDisplayText.BackgroundState(view.LastState));
		}
		if (view.Compass == null)
		{
			return;
		}
		for (int index = 0; index < view.Compass.Length && index < CompassSourceLabels.Length; index++)
		{
			if (view.Compass[index] != null)
			{
				view.Compass[index].text = PluginLocalization.Translate(CompassSourceLabels[index]);
			}
		}
	}

	internal void BeginLayoutDrag()
	{
		_layoutDragging = true;
	}

	internal void ApplyLayoutDrag(Vector2 screenDelta, bool resize)
	{
		float scale = Mathf.Max(0.01f, _uiRoot.Canvas?.scaleFactor ?? 1f);
		Vector2 delta = screenDelta / scale;
		if (resize)
		{
			float signedDelta = _settings.Anchor == MinimapAnchor.TopRight || _settings.Anchor == MinimapAnchor.BottomRight
				? -delta.x : delta.x;
			_settings.Size = Mathf.Clamp(_settings.Size + signedDelta, MinimapSettings.MinSize, MinimapSettings.MaxSize);
			ApplyViewportSize();
		}
		else
		{
			_rootRect.anchoredPosition += delta;
		}
		ClampToSafeArea();
	}

	internal void EndLayoutDrag()
	{
		CaptureCurrentLayout();
		_layoutDragging = false;
	}

	private void CaptureCurrentLayout()
	{
		Vector2 position = _rootRect.anchoredPosition;
		float offsetX = _settings.Anchor == MinimapAnchor.TopRight || _settings.Anchor == MinimapAnchor.BottomRight ? -position.x : position.x;
		float offsetY = _settings.Anchor == MinimapAnchor.TopLeft || _settings.Anchor == MinimapAnchor.TopRight ? -position.y : position.y;
		_settings.OffsetX = Mathf.Max(0f, offsetX);
		_settings.OffsetY = Mathf.Max(0f, offsetY);
	}

	private void UpdateLayoutEditInput()
	{
		if (!_settings.LayoutEditMode || !_runtimeVisible || !_settings.Enabled) return;
		UpdateLayoutPrompt(force: false);
		if (Input.GetKeyDown(KeyCode.Return))
		{
			MinimapLayoutInputGate.SuppressUntilRelease(KeyCode.Return);
			FinishLayoutEdit(apply: true);
		}
		else if (Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			MinimapLayoutInputGate.SuppressUntilRelease(KeyCode.KeypadEnter);
			FinishLayoutEdit(apply: true);
		}
		else if (Input.GetKeyDown(KeyCode.Escape))
		{
			MinimapLayoutInputGate.SuppressUntilRelease(KeyCode.Escape);
			FinishLayoutEdit(apply: false);
		}
	}

	private void FinishLayoutEdit(bool apply)
	{
		if (!_settings.LayoutEditMode) return;
		if (apply)
		{
			CaptureCurrentLayout();
		}
		else if (_layoutStartSettings != null)
		{
			_settings = _layoutStartSettings.Clone();
			ApplyViewportSize();
			ApplyAnchor();
			ClampToSafeArea();
		}
		_layoutDragging = false;
		_settings.LayoutEditMode = false;
		_editOutline.enabled = false;
		_resizeHandle.SetActive(false);
		_dragSurface.raycastTarget = false;
		_uiRoot.SetHudInteraction(false);
		MinimapLayoutInputGate.SetEditing(false);
		HideLayoutPrompt();
		_layoutCommitted?.Invoke(_settings.Anchor, _settings.OffsetX, _settings.OffsetY, _settings.Size);
	}

	private void UpdateLayoutPrompt(bool force)
	{
		float now = Time.realtimeSinceStartup;
		if (!force && now < _nextLayoutPromptTime) return;
		_nextLayoutPromptTime = now + LayoutPromptRefreshSeconds;
		try
		{
			PlayerUI.message(EPlayerMessage.NPC_CUSTOM, PluginLocalization.Translate(LayoutPromptText), LayoutPromptRefreshSeconds + 0.25f);
			_layoutPromptVisible = true;
		}
		catch (Exception exception)
		{
			_layoutPromptVisible = false;
			_log?.LogWarning("[Minimap] 无法显示布局编辑原生提示。\n" + exception);
		}
	}

	private void HideLayoutPrompt()
	{
		if (!_layoutPromptVisible) return;
		try
		{
			PlayerUI.message(EPlayerMessage.NONE, string.Empty, 0f);
		}
		catch
		{
		}
		_layoutPromptVisible = false;
	}

	private void ApplyAnchor()
	{
		bool right = _settings.Anchor == MinimapAnchor.TopRight || _settings.Anchor == MinimapAnchor.BottomRight;
		bool top = _settings.Anchor == MinimapAnchor.TopLeft || _settings.Anchor == MinimapAnchor.TopRight;
		_rootRect.anchorMin = _rootRect.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
		_rootRect.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
		_rootRect.anchoredPosition = new Vector2(right ? -_settings.OffsetX : _settings.OffsetX,
			top ? -_settings.OffsetY : _settings.OffsetY);
	}

	private void ApplyViewportSize()
	{
		_rootRect.sizeDelta = new Vector2(_settings.Size, _settings.Size + CoordinateHeight);
		_circle.Root.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, _settings.Size);
		_square.Root.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, _settings.Size);
		float contentSize = MinimapProjection.ResolveContentExtent(_settings.RotateWithView, _settings.Size);
		_circle.MapContent.sizeDelta = new Vector2(contentSize, contentSize);
		_square.MapContent.sizeDelta = new Vector2(contentSize, contentSize);
		LayoutCompass(_circle, _settings.Size);
		LayoutCompass(_square, _settings.Size);
	}

	private void ClampToSafeArea()
	{
		RectTransform parent = _uiRoot.HudLayer;
		if (parent == null || _rootRect == null) return;
		_rootRect.GetWorldCorners(_rootWorldCorners);
		Vector3 first = parent.InverseTransformPoint(_rootWorldCorners[0]);
		float minX = first.x;
		float maxX = first.x;
		float minY = first.y;
		float maxY = first.y;
		for (int index = 1; index < _rootWorldCorners.Length; index++)
		{
			Vector3 corner = parent.InverseTransformPoint(_rootWorldCorners[index]);
			minX = Mathf.Min(minX, corner.x);
			maxX = Mathf.Max(maxX, corner.x);
			minY = Mathf.Min(minY, corner.y);
			maxY = Mathf.Max(maxY, corner.y);
		}
		Rect available = parent.rect;
		Vector2 correction = Vector2.zero;
		if (minX < available.xMin + SafeMargin) correction.x += available.xMin + SafeMargin - minX;
		if (maxX > available.xMax - SafeMargin) correction.x -= maxX - (available.xMax - SafeMargin);
		if (minY < available.yMin + SafeMargin) correction.y += available.yMin + SafeMargin - minY;
		if (maxY > available.yMax - SafeMargin) correction.y -= maxY - (available.yMax - SafeMargin);
		_rootRect.anchoredPosition += correction;
	}

	private void ApplyMarkersToViews()
	{
		_circle.Pool.Apply(_markers, _dotSprite, _nativeMarkerSprite, GetTeleportSprite);
		_square.Pool.Apply(_markers, _dotSprite, _nativeMarkerSprite, GetTeleportSprite);
	}

	private void SetViewBackground(View view, MinimapBackgroundState state, NativeMapCropSnapshot nativeMap,
		Texture terrain, Texture objects)
	{
		string stateText = PluginLocalization.Translate(MinimapDisplayText.BackgroundState(state));
		if (!view.HasState || view.LastState != state || !string.Equals(view.State.text, stateText, StringComparison.Ordinal))
		{
			view.State.text = stateText;
			view.LastState = state;
			view.HasState = true;
		}
		bool showObjects = MinimapRenderPolicy.ShowsLoadedObjects(state, objects != null);
		SetRawImage(view.ObjectOverlay, showObjects ? objects : null, new Rect(0f, 1f, 1f, -1f),
			showObjects ? Color.white : Color.clear);
		if (state == MinimapBackgroundState.NativeMapCrop && nativeMap.IsAvailable)
		{
			SetRawImage(view.Background, nativeMap.Texture, nativeMap.UvRect, Color.white);
		}
		else if ((state == MinimapBackgroundState.RealtimeSurface || state == MinimapBackgroundState.RealtimeHeightSlice) && terrain != null)
		{
			SetRawImage(view.Background, terrain, new Rect(0f, 1f, 1f, -1f), Color.white);
		}
		else if (state == MinimapBackgroundState.Disabled)
		{
			SetRawImage(view.Background, _gridTexture, new Rect(0f, 0f, 1f, 1f), Color.clear);
		}
		else
		{
			SetRawImage(view.Background, _gridTexture, new Rect(0f, 0f, 1f, 1f), Color.white);
		}
	}

	private static void SetRawImage(RawImage image, Texture texture, Rect uvRect, Color color)
	{
		if (image.texture != texture) image.texture = texture;
		if (image.uvRect != uvRect) image.uvRect = uvRect;
		if (image.color != color) image.color = color;
	}

	private void TryBindNativeIcons()
	{
		if (_nativeIconsBound) return;
		Texture2D playerTexture;
		Texture2D markerTexture;
		try
		{
			playerTexture = PlayerDashboardInformationUI.icons.load<Texture2D>("Player");
			markerTexture = PlayerDashboardInformationUI.icons.load<Texture2D>("Marker");
		}
		catch
		{
			return;
		}
		if (playerTexture == null || markerTexture == null) return;
		Sprite player = Sprite.Create(playerTexture, new Rect(0f, 0f, playerTexture.width, playerTexture.height), new Vector2(0.5f, 0.5f), 100f);
		Sprite marker = Sprite.Create(markerTexture, new Rect(0f, 0f, markerTexture.width, markerTexture.height), new Vector2(0.5f, 0.5f), 100f);
		_ownedSprites.Add(player);
		_ownedSprites.Add(marker);
		_playerSprite = player;
		_nativeMarkerSprite = marker;
		_circle.Player.sprite = player;
		_square.Player.sprite = player;
		_nativeIconsBound = true;
	}

	private Sprite GetTeleportSprite(TeleportMarkerKind kind)
	{
		if (_teleportSprites.TryGetValue(kind, out Sprite sprite)) return sprite;
		sprite = TeleportMarkerIconFactory.CreateSprite(kind, 20, out Texture2D texture);
		_teleportSprites[kind] = sprite;
		_ownedSprites.Add(sprite);
		_ownedTextures.Add(texture);
		return sprite;
	}

	private static RawImage CreateRawImage(string name, Transform parent)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
		obj.transform.SetParent(parent, false);
		RawImage image = obj.GetComponent<RawImage>();
		image.raycastTarget = false;
		Stretch(image.rectTransform);
		return image;
	}

	private static UiText CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
	{
		GameObject obj = new GameObject(name, typeof(RectTransform), typeof(UiText));
		obj.transform.SetParent(parent, false);
		UiText text = obj.GetComponent<UiText>();
		text.font = font;
		text.fontSize = size;
		text.color = CheatMenuStyle.Theme.HudText.ToUnity();
		text.alignment = anchor;
		text.raycastTarget = false;
		return text;
	}

	private void CreateViewportBorder(Transform parent, bool circle)
	{
		if (circle)
		{
			GameObject borderObject = new GameObject("ViewportBorder.Circle", typeof(RectTransform), typeof(Image));
			borderObject.transform.SetParent(parent, false);
			Image border = borderObject.GetComponent<Image>();
			border.sprite = _circleBorderSprite;
			border.color = CheatMenuStyle.Theme.HudBorder.ToUnity();
			border.raycastTarget = false;
			border.maskable = false;
			RectTransform borderRect = border.rectTransform;
			Stretch(borderRect);
			Vector2 expansion = new Vector2(CircleBorderExpansionPixels, CircleBorderExpansionPixels);
			borderRect.offsetMin = -expansion;
			borderRect.offsetMax = expansion;
			return;
		}

		CreateBorderEdge(parent, "ViewportBorder.Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
			new Vector2(0f, -1f), Vector2.zero);
		CreateBorderEdge(parent, "ViewportBorder.Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
			Vector2.zero, new Vector2(0f, 1f));
		CreateBorderEdge(parent, "ViewportBorder.Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
			Vector2.zero, new Vector2(1f, 0f));
		CreateBorderEdge(parent, "ViewportBorder.Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
			new Vector2(-1f, 0f), Vector2.zero);
	}

	private static void CreateBorderEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
		Vector2 offsetMin, Vector2 offsetMax)
	{
		GameObject borderObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		borderObject.transform.SetParent(parent, false);
		Image border = borderObject.GetComponent<Image>();
		border.color = CheatMenuStyle.Theme.HudBorder.ToUnity();
		border.raycastTarget = false;
		RectTransform rect = border.rectTransform;
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.offsetMin = offsetMin;
		rect.offsetMax = offsetMax;
	}

	private static UiText[] CreateCompass(Transform parent, Font font)
	{
		return new[]
		{
			CreateCompassLabel(parent, font, "北"),
			CreateCompassLabel(parent, font, "东"),
			CreateCompassLabel(parent, font, "南"),
			CreateCompassLabel(parent, font, "西")
		};
	}

	private static UiText CreateCompassLabel(Transform parent, Font font, string label)
	{
		UiText text = CreateText("Compass" + label, parent, font, 12, TextAnchor.MiddleCenter);
		text.text = PluginLocalization.Translate(label);
		text.fontStyle = FontStyle.Bold;
		text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		text.rectTransform.sizeDelta = new Vector2(20f, 20f);
		return text;
	}

	private static void LayoutCompass(View view, float size)
	{
		float extent = Mathf.Max(0f, size * 0.5f - 7f);
		view.Compass[0].rectTransform.anchoredPosition = new Vector2(0f, extent);
		view.Compass[1].rectTransform.anchoredPosition = new Vector2(extent, 0f);
		view.Compass[2].rectTransform.anchoredPosition = new Vector2(0f, -extent);
		view.Compass[3].rectTransform.anchoredPosition = new Vector2(-extent, 0f);
	}

	private static void SetCompassVisible(View view, bool visible)
	{
		view.CompassRoot.gameObject.SetActive(visible);
	}

	private static Texture2D CreateGridTexture()
	{
		const int size = 64;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			name = "Minimap.FallbackGrid",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Repeat,
			hideFlags = HideFlags.HideAndDontSave
		};
		Color background = new Color(0.025f, 0.045f, 0.065f, 0.94f);
		Color line = new Color(0.12f, 0.32f, 0.42f, 0.8f);
		Color[] pixels = new Color[size * size];
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++) pixels[y * size + x] = x % 16 == 0 || y % 16 == 0 ? line : background;
		texture.SetPixels(pixels);
		texture.Apply(false, true);
		return texture;
	}

	private static Sprite CreateCircleSprite(int size, out Texture2D texture)
	{
		texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
		Color[] pixels = new Color[size * size];
		float center = (size - 1) * 0.5f;
		float radius = center - 1f;
		float squared = radius * radius;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++) pixels[y * size + x] = (x - center) * (x - center) + (y - center) * (y - center) <= squared ? Color.white : Color.clear;
		texture.SetPixels(pixels);
		texture.Apply(false, true);
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
	}

	private static Sprite CreateCircleBorderSprite(int size, float thickness, out Texture2D texture)
	{
		texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			name = "Minimap.CircleBorder",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave
		};
		Color[] pixels = new Color[size * size];
		float center = (size - 1) * 0.5f;
		float outerRadius = center - 1f;
		float innerRadius = Mathf.Max(0f, outerRadius - thickness);
		float outerSquared = outerRadius * outerRadius;
		float innerSquared = innerRadius * innerRadius;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float squaredDistance = (x - center) * (x - center) + (y - center) * (y - center);
			pixels[y * size + x] = squaredDistance <= outerSquared && squaredDistance >= innerSquared
				? Color.white
				: Color.clear;
		}
		texture.SetPixels(pixels);
		texture.Apply(false, true);
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
	}

	private static Sprite CreateFallbackPlayerSprite(out Texture2D texture)
	{
		const int size = 32;
		texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
		Color[] pixels = new Color[size * size];
		for (int y = 0; y < size; y++)
		{
			float half = (float)y / (size - 1) * 12f;
			for (int x = 0; x < size; x++) pixels[y * size + x] = Mathf.Abs(x - 15.5f) <= half ? Color.white : Color.clear;
		}
		texture.SetPixels(pixels);
		texture.Apply(false, true);
		return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static void Center(RectTransform rect)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
	}

	public void Dispose()
	{
		HideLayoutPrompt();
		_uiRoot.SetHudInteraction(false);
		MinimapLayoutInputGate.SetEditing(false);
		_resizeSurface?.Dispose();
		_resizeSurface = null;
		if (_root != null)
		{
			_uiRoot.QueueHudDestroy(_root);
			_root = null;
		}
		for (int index = 0; index < _ownedSprites.Count; index++)
			if (_ownedSprites[index] != null) UnityEngine.Object.Destroy(_ownedSprites[index]);
		for (int index = 0; index < _ownedTextures.Count; index++)
			if (_ownedTextures[index] != null) UnityEngine.Object.Destroy(_ownedTextures[index]);
		_ownedSprites.Clear();
		_ownedTextures.Clear();
		_teleportSprites.Clear();
	}
}
