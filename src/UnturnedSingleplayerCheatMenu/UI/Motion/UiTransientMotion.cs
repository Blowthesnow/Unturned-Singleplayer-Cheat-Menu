using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI.Motion;

internal sealed class UiPopupMotionOffset : MonoBehaviour
{
	internal Vector2 Offset;
}

internal static class UiTransientMotion
{
	internal static CanvasGroup EnsureCanvasGroup(GameObject root, bool blocksRaycasts)
	{
		if (root == null) return null;
		CanvasGroup group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
		group.interactable = blocksRaycasts;
		group.blocksRaycasts = blocksRaycasts;
		return group;
	}

	internal static void EnterFadeScale(
		UiMotionDriver driver,
		GameObject root,
		Transform visual,
		Vector3 restScale,
		float duration,
		float startScale,
		bool blocksRaycasts)
	{
		if (driver == null || root == null || visual == null) return;
		CanvasGroup group = EnsureCanvasGroup(root, blocksRaycasts);
		Vector3 initialScale = restScale * startScale;
		group.alpha = 0f;
		visual.localScale = initialScale;
		driver.Animate(root, UiMotionChannel.TransientAlpha, duration, UiMotionEasingKind.EaseOutCubic,
			progress => group.alpha = progress);
		driver.Animate(root, UiMotionChannel.TransientScale, duration, UiMotionEasingKind.EaseOutCubic,
			progress => visual.localScale = Vector3.LerpUnclamped(initialScale, restScale, progress));
	}

	internal static void ExitFadeScale(
		UiMotionDriver driver,
		GameObject root,
		Transform visual,
		Vector3 restScale,
		float duration,
		float endScale,
		bool keepBlockingRaycasts,
		Action completed)
	{
		if (root == null)
		{
			completed?.Invoke();
			return;
		}
		if (driver == null || !root.activeInHierarchy)
		{
			completed?.Invoke();
			return;
		}
		CanvasGroup group = EnsureCanvasGroup(root, keepBlockingRaycasts);
		group.interactable = false;
		Vector3 startScale = visual != null ? visual.localScale : restScale;
		Vector3 targetScale = restScale * endScale;
		float startAlpha = group.alpha;
		driver.Animate(root, UiMotionChannel.TransientAlpha, duration, UiMotionEasingKind.EaseOutCubic,
			progress => group.alpha = Mathf.LerpUnclamped(startAlpha, 0f, progress), completed);
		if (visual != null)
		{
			driver.Animate(root, UiMotionChannel.TransientScale, duration, UiMotionEasingKind.EaseOutCubic,
				progress => visual.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress));
		}
	}
}
