using System;
using System.Collections.Generic;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.UI.Motion;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private readonly Dictionary<int, GameObject> _closingTransientSurfaces = new Dictionary<int, GameObject>();
	private bool _suppressTransientMotion;

	private void AnimateModalEnter(GameObject root, Transform dialog)
	{
		if (root == null || dialog == null || _suppressTransientMotion) return;
		UiTransientMotion.EnterFadeScale(
			_uiRoot.Motion,
			root,
			dialog,
			Vector3.one,
			UiMotionTokens.Modal,
			UiMotionTokens.ModalStartScale,
			blocksRaycasts: true);
	}

	private void ShowHoverExplanationVisual(RectTransform tooltipRect)
	{
		if (_hoverExplanationRoot == null || tooltipRect == null) return;
		bool wasActive = _hoverExplanationRoot.activeSelf;
		_hoverExplanationRoot.SetActive(value: true);
		CanvasGroup group = UiTransientMotion.EnsureCanvasGroup(_hoverExplanationRoot, blocksRaycasts: false);
		if (_suppressTransientMotion || _uiRoot.Motion == null)
		{
			group.alpha = 1f;
			tooltipRect.localScale = Vector3.one;
			return;
		}

		if (!wasActive)
		{
			group.alpha = 0f;
			tooltipRect.localScale = Vector3.one * UiMotionTokens.TooltipStartScale;
		}
		float startAlpha = group.alpha;
		Vector3 startScale = tooltipRect.localScale;
		_uiRoot.Motion.Animate(_hoverExplanationRoot, UiMotionChannel.TransientAlpha, UiMotionTokens.Tooltip,
			UiMotionEasingKind.EaseOutCubic,
			progress => group.alpha = Mathf.LerpUnclamped(startAlpha, 1f, progress));
		_uiRoot.Motion.Animate(_hoverExplanationRoot, UiMotionChannel.TransientScale, UiMotionTokens.Tooltip,
			UiMotionEasingKind.EaseOutCubic,
			progress => tooltipRect.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, progress));
	}

	private void HideHoverExplanation()
	{
		if (_hoverExplanationRoot == null) return;
		GameObject root = _hoverExplanationRoot;
		RectTransform rect = root.GetComponent<RectTransform>();
		CanvasGroup group = UiTransientMotion.EnsureCanvasGroup(root, blocksRaycasts: false);
		if (_suppressTransientMotion || _uiRoot.Motion == null || !root.activeInHierarchy)
		{
			_uiRoot.Motion?.Cancel(root, includeChildren: true);
			root.SetActive(value: false);
			group.alpha = 1f;
			if (rect != null) rect.localScale = Vector3.one;
			return;
		}

		float startAlpha = group.alpha;
		Vector3 startScale = rect != null ? rect.localScale : Vector3.one;
		_uiRoot.Motion.Animate(root, UiMotionChannel.TransientAlpha, UiMotionTokens.Tooltip,
			UiMotionEasingKind.EaseOutCubic,
			progress => group.alpha = Mathf.LerpUnclamped(startAlpha, 0f, progress),
			() =>
			{
				if (root == null) return;
				root.SetActive(value: false);
				group.alpha = 1f;
				if (rect != null) rect.localScale = Vector3.one;
			});
		if (rect != null)
		{
			Vector3 targetScale = Vector3.one * UiMotionTokens.TooltipStartScale;
			_uiRoot.Motion.Animate(root, UiMotionChannel.TransientScale, UiMotionTokens.Tooltip,
				UiMotionEasingKind.EaseOutCubic,
				progress => rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress));
		}
	}

	private void AnimatePopupEnter(GameObject root, Transform popup)
	{
		if (root == null || popup == null || _suppressTransientMotion || _uiRoot.Motion == null) return;
		UiTransientMotion.EnterFadeScale(
			_uiRoot.Motion,
			root,
			popup,
			Vector3.one,
			UiMotionTokens.Popup,
			UiMotionTokens.PopupStartScale,
			blocksRaycasts: true);
		if (popup is RectTransform rect)
		{
			UiPopupMotionOffset offsetState = root.GetComponent<UiPopupMotionOffset>() ?? root.AddComponent<UiPopupMotionOffset>();
			Vector2 startOffset = rect.pivot.y < 0.5f
				? new Vector2(0f, -UiMotionTokens.PopupOffset)
				: new Vector2(0f, UiMotionTokens.PopupOffset);
			offsetState.Offset = startOffset;
			rect.anchoredPosition += startOffset;
			_uiRoot.Motion.Animate(root, UiMotionChannel.TransientPosition, UiMotionTokens.Popup,
				UiMotionEasingKind.EaseOutCubic,
				progress =>
				{
					Vector2 next = Vector2.LerpUnclamped(startOffset, Vector2.zero, progress);
					rect.anchoredPosition += next - offsetState.Offset;
					offsetState.Offset = next;
				});
		}
	}

	private void QueueModalExit(GameObject root, Transform dialog)
	{
		QueueTransientExit(root, dialog, UiMotionTokens.ModalScrim, keepBlockingRaycasts: true);
	}

	private void QueuePopupExit(GameObject root, Transform popup)
	{
		// Popup baseline restores its trigger immediately; only the pixels linger briefly.
		_uiRoot.UnregisterLayerSurface(root);
		QueueTransientExit(root, popup, UiMotionTokens.Popup, keepBlockingRaycasts: false);
	}

	private void QueueTransientExit(GameObject root, Transform visual, float duration, bool keepBlockingRaycasts)
	{
		if (root == null) return;
		int id = root.GetInstanceID();
		if (_suppressTransientMotion || _uiRoot.Motion == null || !root.activeInHierarchy)
		{
			_closingTransientSurfaces.Remove(id);
			QueueForDestroy(root);
			return;
		}
		if (_closingTransientSurfaces.ContainsKey(id)) return;
		_closingTransientSurfaces.Add(id, root);

		UiTransientMotion.ExitFadeScale(
			_uiRoot.Motion,
			root,
			visual,
			Vector3.one,
			duration,
			UiMotionTokens.PopupStartScale,
			keepBlockingRaycasts,
			() =>
			{
				_closingTransientSurfaces.Remove(id);
				if (root != null) QueueForDestroy(root);
			});
	}

	private void RunWithoutTransientMotion(Action action)
	{
		bool previous = _suppressTransientMotion;
		_suppressTransientMotion = true;
		try
		{
			FlushClosingTransientSurfaces();
			action?.Invoke();
		}
		finally
		{
			_suppressTransientMotion = previous;
		}
	}

	private void FlushClosingTransientSurfaces()
	{
		if (_closingTransientSurfaces.Count == 0) return;
		List<GameObject> pending = new List<GameObject>(_closingTransientSurfaces.Values);
		_closingTransientSurfaces.Clear();
		for (int index = 0; index < pending.Count; index++)
		{
			GameObject root = pending[index];
			if (root != null) QueueForDestroy(root);
		}
	}

	private void PrepareModalOpen()
	{
		RunWithoutTransientMotion(CloseTransientVisualSurfaces);
	}

	private static Transform ResolveModalVisual(GameObject root)
	{
		if (root == null) return null;
		return root.transform.childCount > 0 ? root.transform.GetChild(0) : root.transform;
	}

	private static void SetPopupMotionPosition(RectTransform popup, Vector2 restPosition)
	{
		if (popup == null) return;
		UiPopupMotionOffset state = popup.GetComponent<UiPopupMotionOffset>();
		popup.anchoredPosition = restPosition + (state != null ? state.Offset : Vector2.zero);
	}

	private void QueueForDestroy(GameObject target)
	{
		if (target == null) return;
		_closingTransientSurfaces.Remove(target.GetInstanceID());
		_uiRoot.Motion?.Cancel(target, includeChildren: true);
		_fluentHandles.DisposeSubtree(target);
		_uiRoot.UnregisterLayerSurface(target);
		_deferredDestroy.Enqueue(target, Time.frameCount, item => item.SetActive(value: false));
	}
}
