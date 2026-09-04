using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.Motion;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Owns the single plugin canvas, layer hierarchy, and any EventSystem created for it.</summary>
internal sealed class PluginUiRoot : IDisposable
{
	private readonly ManualLogSource _log;
	private GameObject _ownedEventSystem;
	private EventSystem _boundEventSystem;
	private int _fontRevision = -1;
	private readonly List<LayerSurface> _layerStack = new List<LayerSurface>();
	private readonly HashSet<string> _reportedUnsupportedGlazierBackends = new HashSet<string>(StringComparer.Ordinal);
	private readonly DeferredUiDestroyQueue<GameObject> _hudDestroyQueue = new DeferredUiDestroyQueue<GameObject>();
	private GameObject _selectionBeforeNativeUi;
	private bool _nativeUiCoexistence;
	private CanvasGroup _hudGroup;
	private UiMotionDriver _motion;

	internal GameObject Root { get; private set; }
	internal Canvas Canvas { get; private set; }
	internal RectTransform WindowLayer { get; private set; }
	internal RectTransform ModalLayer { get; private set; }
	internal RectTransform PopupLayer { get; private set; }
	internal RectTransform TooltipLayer { get; private set; }
	internal RectTransform HudLayer { get; private set; }
	internal RectTransform ToastLayer { get; private set; }
	internal UiToastService Toasts { get; private set; }
	internal UiMotionDriver Motion => _motion;

	private sealed class LayerSurface
	{
		internal GameObject Surface;
		internal GameObject PreviousSelection;
		internal Action Dismiss;
	}

	private PluginUiRoot(ManualLogSource log)
	{
		_log = log;
	}

	internal static PluginUiRoot Create(ManualLogSource log)
	{
		PluginUiRoot result = new PluginUiRoot(log);
		result.Initialize();
		return result;
	}

	private void Initialize()
	{
		if (Root != null)
		{
			return;
		}
		_layerStack.Clear();
		Toasts?.Dispose();
		Toasts = null;
		if (_motion == null) _motion = new UiMotionDriver();
		else _motion.CancelAll();
		_ownedEventSystem = null;
		_boundEventSystem = null;
		Root = new GameObject("UnturnedSingleplayerCheatMenu.UiRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		UnityEngine.Object.DontDestroyOnLoad(Root);
		Canvas = Root.GetComponent<Canvas>();
		Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		Canvas.overrideSorting = true;
		Canvas.sortingOrder = 29900;
		CanvasScaler scaler = Root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;
		HudLayer = CreateLayer("HudLayer");
		_hudGroup = HudLayer.gameObject.AddComponent<CanvasGroup>();
		_hudGroup.interactable = false;
		_hudGroup.blocksRaycasts = false;
		WindowLayer = CreateLayer("WindowLayer");
		ModalLayer = CreateLayer("ModalLayer");
		PopupLayer = CreateLayer("PopupLayer");
		TooltipLayer = CreateLayer("TooltipLayer");
		ToastLayer = CreateLayer("ToastLayer");
		Toasts = new UiToastService(ToastLayer, _log, _motion);
		EnsureEventSystem();
		_log?.LogInfo("[UI] single UiRoot created with WindowLayer, ModalLayer, PopupLayer, TooltipLayer, HudLayer, and ToastLayer.");
	}

	private RectTransform CreateLayer(string name)
	{
		GameObject layer = new GameObject(name, typeof(RectTransform));
		layer.transform.SetParent(Root.transform, false);
		RectTransform rect = layer.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		return rect;
	}

	private void EnsureEventSystem()
	{
		EventSystem owned = _ownedEventSystem != null ? _ownedEventSystem.GetComponent<EventSystem>() : null;
		EventSystem native = FindNativeEventSystem(owned);
		if (native != null)
		{
			if (_boundEventSystem != native)
			{
				_boundEventSystem = native;
				_log?.LogInfo("[UI] bound to the active native EventSystem.");
			}
			RetireOwnedEventSystem();
			return;
		}
		if (_ownedEventSystem == null)
		{
			_ownedEventSystem = new GameObject("UnturnedSingleplayerCheatMenu.EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
			_log?.LogInfo("[UI] created the plugin-owned EventSystem because no native instance was available.");
		}
		else if (!_ownedEventSystem.activeSelf)
		{
			_ownedEventSystem.SetActive(true);
		}
		_boundEventSystem = _ownedEventSystem.GetComponent<EventSystem>();
	}

	private EventSystem FindNativeEventSystem(EventSystem owned)
	{
		EventSystem current = EventSystem.current;
		if (current != null && current != owned && current.isActiveAndEnabled)
		{
			return current;
		}
		EventSystem[] systems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);
		return UiLifecyclePolicy.SelectBorrowed(
			current,
			owned,
			systems,
			candidate => candidate != null && candidate.isActiveAndEnabled,
			candidate => candidate.isActiveAndEnabled);
	}

	private void RetireOwnedEventSystem()
	{
		if (_ownedEventSystem == null)
		{
			return;
		}
		// Disable synchronously so there is never a duplicate active EventSystem while Destroy is deferred.
		_ownedEventSystem.SetActive(false);
		UnityEngine.Object.Destroy(_ownedEventSystem);
		_ownedEventSystem = null;
		_log?.LogInfo("[UI] retired the plugin-owned EventSystem after a native instance became available.");
	}

	internal void Pump()
	{
		EnsureInitialized();
		RebindFontsIfNeeded();
		Toasts?.Pump();
		PruneLayerStack();
		OverlaySelectableVisual.PumpAll();
		_motion?.Pump(Time.unscaledDeltaTime);
		bool canvasRebuilding = CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics();
		while (_hudDestroyQueue.TryDequeueReady(Time.frameCount, canvasRebuilding, out GameObject retiredHud))
		{
			if (retiredHud != null) UnityEngine.Object.Destroy(retiredHud);
		}
	}

	internal void SetHudInteraction(bool enabled)
	{
		EnsureInitialized();
		if (_hudGroup != null)
		{
			_hudGroup.interactable = enabled;
			_hudGroup.blocksRaycasts = enabled;
		}
	}

	internal void QueueHudDestroy(GameObject target)
	{
		if (target != null)
		{
			_motion?.Cancel(target, includeChildren: true);
			_hudDestroyQueue.Enqueue(target, Time.frameCount, item => item.SetActive(false));
		}
	}

	internal void RegisterModal(GameObject surface, Action dismiss, GameObject preferredSelection = null)
	{
		RegisterLayerSurface(surface, ModalLayer, dismiss, preferredSelection);
	}

	internal void RegisterPopup(GameObject surface, Action dismiss)
	{
		RegisterLayerSurface(surface, PopupLayer, dismiss, null);
	}

	internal void AttachTooltip(GameObject surface)
	{
		if (surface != null && TooltipLayer != null)
		{
			surface.transform.SetParent(TooltipLayer, false);
		}
	}

	internal void UnregisterLayerSurface(GameObject surface)
	{
		if (surface == null)
		{
			return;
		}
		for (int i = _layerStack.Count - 1; i >= 0; i--)
		{
			if (_layerStack[i].Surface != surface)
			{
				continue;
			}
			bool wasTop = i == _layerStack.Count - 1;
			LayerSurface entry = _layerStack[i];
			_layerStack.RemoveAt(i);
			if (wasTop)
			{
				RestoreSelection(entry.PreviousSelection);
			}
			return;
		}
	}

	internal bool HandleCancel()
	{
		PruneLayerStack();
		for (int i = _layerStack.Count - 1; i >= 0; i--)
		{
			Action dismiss = _layerStack[i].Dismiss;
			if (dismiss != null)
			{
				dismiss();
				return true;
			}
		}
		return false;
	}

	internal bool CanOpenWindow(string surfaceName)
	{
		object backend = SDG.Unturned.Glazier.Get();
		string backendType = backend?.GetType().FullName ?? "unavailable";
		if (string.Equals(backendType, "SDG.Unturned.Glazier_uGUI", StringComparison.Ordinal))
		{
			return true;
		}
		if (_reportedUnsupportedGlazierBackends.Add(backendType))
		{
			_log?.LogError(
				"[UI] Refused to open plugin windows because the active Glazier backend is " + backendType +
				". This plugin requires Unturned's default uGUI backend; remove -Glazier=IMGUI or -Glazier=UIToolkit and restart the game.");
		}
		return false;
	}

	internal void SetNativeUiCoexistence(bool active)
	{
		if (_nativeUiCoexistence == active)
		{
			return;
		}
		_nativeUiCoexistence = active;
		if (active)
		{
			GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
			_selectionBeforeNativeUi = IsPluginInteractiveSelection(selected) ? selected : null;
			if (_selectionBeforeNativeUi != null && EventSystem.current != null)
			{
				EventSystem.current.SetSelectedGameObject(null);
			}
		}
		SetLayerInteraction(WindowLayer, !active);
		SetLayerInteraction(ModalLayer, !active);
		SetLayerInteraction(PopupLayer, !active);
		SetLayerInteraction(TooltipLayer, !active);
		if (!active)
		{
			RestoreSelection(_selectionBeforeNativeUi);
			_selectionBeforeNativeUi = null;
		}
		_log?.LogInfo(active
			? "[UI] entered NativeUiCoexistence; plugin windows yielded focus and raycasts to Unturned UI."
			: "[UI] exited NativeUiCoexistence; plugin window interaction resumed.");
	}

	private bool IsPluginInteractiveSelection(GameObject selected)
	{
		if (selected == null)
		{
			return false;
		}
		Transform transform = selected.transform;
		return IsChildOf(transform, WindowLayer) || IsChildOf(transform, ModalLayer) ||
			IsChildOf(transform, PopupLayer) || IsChildOf(transform, TooltipLayer);
	}

	private static bool IsChildOf(Transform candidate, Transform parent)
	{
		return candidate != null && parent != null && candidate.IsChildOf(parent);
	}

	private static void SetLayerInteraction(RectTransform layer, bool enabled)
	{
		if (layer == null)
		{
			return;
		}
		CanvasGroup group = layer.GetComponent<CanvasGroup>() ?? layer.gameObject.AddComponent<CanvasGroup>();
		group.alpha = enabled ? 1f : 0f;
		group.interactable = enabled;
		group.blocksRaycasts = enabled;
	}

	private void RegisterLayerSurface(GameObject surface, RectTransform layer, Action dismiss, GameObject preferredSelection)
	{
		if (surface == null || layer == null)
		{
			return;
		}
		UnregisterLayerSurface(surface);
		GameObject previous = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		surface.transform.SetParent(layer, false);
		surface.transform.SetAsLastSibling();
		_layerStack.Add(new LayerSurface { Surface = surface, PreviousSelection = previous, Dismiss = dismiss });
		if (preferredSelection != null && preferredSelection.activeInHierarchy && EventSystem.current != null)
		{
			EventSystem.current.SetSelectedGameObject(preferredSelection);
		}
	}

	private void PruneLayerStack()
	{
		for (int i = _layerStack.Count - 1; i >= 0; i--)
		{
			if (_layerStack[i].Surface == null)
			{
				_layerStack.RemoveAt(i);
			}
		}
	}

	private static void RestoreSelection(GameObject selection)
	{
		if (selection != null && selection.activeInHierarchy && EventSystem.current != null)
		{
			EventSystem.current.SetSelectedGameObject(selection);
		}
	}

	private void RebindFontsIfNeeded()
	{
		if (Root == null || _fontRevision == SystemFontProvider.Revision)
		{
			return;
		}
		Font font = SystemFontProvider.Resolve(_log);
		_fontRevision = SystemFontProvider.Revision;
		if (font == null)
		{
			return;
		}
		UiText[] texts = Root.GetComponentsInChildren<UiText>(true);
		foreach (UiText text in texts)
		{
			if (text != null && text.font != font)
			{
				text.font = font;
			}
		}
	}

	internal void HandleSceneChanged()
	{
		// Scene changes must settle pending transient exits so their completion cleanup runs.
		_motion?.SnapAll();
		EnsureInitialized();
		EnsureEventSystem();
	}

	internal void HandleNativeUiChanged()
	{
		EnsureInitialized();
		EnsureEventSystem();
	}

	private void EnsureInitialized()
	{
		if (Root != null)
		{
			return;
		}
		_log?.LogWarning("[UI] bootstrap-scene UiRoot was destroyed; recreating the shared uGUI root.");
		Initialize();
	}

	public void Dispose()
	{
		_layerStack.Clear();
		Toasts?.Dispose();
		Toasts = null;
		_motion?.Dispose();
		_motion = null;
		if (_ownedEventSystem != null)
		{
			UnityEngine.Object.Destroy(_ownedEventSystem);
			_ownedEventSystem = null;
		}
		_boundEventSystem = null;
		if (Root != null)
		{
			UnityEngine.Object.Destroy(Root);
		}
		Root = null;
		Canvas = null;
		WindowLayer = null;
		ModalLayer = null;
		PopupLayer = null;
		TooltipLayer = null;
		HudLayer = null;
		_hudGroup = null;
		ToastLayer = null;
		_fontRevision = -1;
	}
}
