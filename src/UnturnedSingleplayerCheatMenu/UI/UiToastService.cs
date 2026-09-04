using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using UnturnedSingleplayerCheatMenu.UI.Motion;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class UiToastService : IDisposable
{
	private const int MaximumVisible = 3;
	private readonly RectTransform _layer;
	private readonly ManualLogSource _log;
	private readonly UiMotionDriver _motion;
	private readonly List<ToastEntry> _entries = new List<ToastEntry>();

	private sealed class ToastEntry
	{
		internal GameObject Root;
		internal float ExpiresAt;
		internal LegacyFluentSurface Surface;
		internal RectTransform VisualRect;
		internal CanvasGroup VisualGroup;
		internal LayoutElement Layout;
		internal bool Exiting;
	}

	internal UiToastService(RectTransform layer, ManualLogSource log, UiMotionDriver motion)
	{
		_layer = layer;
		_log = log;
		_motion = motion;
		VerticalLayoutGroup layout = layer.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(0, 24, 24, 0);
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.UpperRight;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
	}

	internal void Show(string message, float seconds, Color accent)
	{
		if (_layer == null || string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		if (CountVisible() >= MaximumVisible)
		{
			ToastEntry oldest = FindOldestVisible();
			if (oldest != null) BeginExit(oldest);
		}
		GameObject root = new GameObject("ToastSlot", typeof(RectTransform), typeof(LayoutElement));
		root.transform.SetParent(_layer, false);
		LayoutElement size = root.GetComponent<LayoutElement>();
		size.preferredWidth = 420f;
		size.preferredHeight = 46f;

		GameObject visual = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
		visual.transform.SetParent(root.transform, false);
		RectTransform visualRect = visual.GetComponent<RectTransform>();
		visualRect.anchorMin = Vector2.zero;
		visualRect.anchorMax = Vector2.one;
		visualRect.offsetMin = Vector2.zero;
		visualRect.offsetMax = Vector2.zero;
		Image background = visual.GetComponent<Image>();
		background.color = Color.clear;
		background.raycastTarget = false;
		FluentTheme theme = FluentThemes.DarkInk;
		LegacyFluentSurface surface = LegacyFluentSurface.Attach(visual, theme.HudBackdrop.ToUnity(), theme.HudBorder.ToUnity(), theme.RadiusControl);
		CanvasGroup canvasGroup = visual.GetComponent<CanvasGroup>();
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;

		GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
		bar.transform.SetParent(visual.transform, false);
		RectTransform barRect = bar.GetComponent<RectTransform>();
		barRect.anchorMin = Vector2.zero;
		barRect.anchorMax = new Vector2(0f, 1f);
		barRect.pivot = new Vector2(0f, 0.5f);
		barRect.sizeDelta = new Vector2(4f, 0f);
		bar.GetComponent<Image>().color = accent;

		GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(UiText));
		textObject.transform.SetParent(visual.transform, false);
		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(14f, 5f);
		textRect.offsetMax = new Vector2(-10f, -5f);
		UiText text = textObject.GetComponent<UiText>();
		text.font = SystemFontProvider.Resolve(_log);
		text.text = message;
		text.fontSize = 13;
		text.color = theme.HudText.ToUnity();
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.raycastTarget = false;
		ToastEntry entry = new ToastEntry
		{
			Root = root,
			ExpiresAt = Time.unscaledTime + Mathf.Max(1f, seconds),
			Surface = surface,
			VisualRect = visualRect,
			VisualGroup = canvasGroup,
			Layout = size
		};
		_entries.Add(entry);
		BeginEnter(entry);
	}

	internal void Pump()
	{
		for (int i = _entries.Count - 1; i >= 0; i--)
		{
			ToastEntry entry = _entries[i];
			if (entry.Root == null)
			{
				DestroyEntry(entry);
			}
			else if (!entry.Exiting && Time.unscaledTime >= entry.ExpiresAt)
			{
				BeginExit(entry);
			}
		}
	}

	private void BeginEnter(ToastEntry entry)
	{
		if (entry?.Root == null || entry.VisualGroup == null || entry.VisualRect == null)
		{
			return;
		}
		entry.VisualGroup.alpha = 0f;
		Vector2 target = entry.VisualRect.anchoredPosition;
		Vector2 start = target + new Vector2(UiMotionTokens.ToastEnterOffset, 0f);
		entry.VisualRect.anchoredPosition = start;
		if (_motion == null)
		{
			entry.VisualGroup.alpha = 1f;
			entry.VisualRect.anchoredPosition = target;
			return;
		}
		_motion.Animate(entry.Root, UiMotionChannel.ToastAlpha, UiMotionTokens.ToastIn, UiMotionEasingKind.EaseOutCubic,
			progress => entry.VisualGroup.alpha = progress);
		_motion.Animate(entry.Root, UiMotionChannel.ToastPosition, UiMotionTokens.ToastIn, UiMotionEasingKind.EaseOutCubic,
			progress => entry.VisualRect.anchoredPosition = Vector2.LerpUnclamped(start, target, progress));
	}

	private void BeginExit(ToastEntry entry)
	{
		if (entry == null || entry.Exiting)
		{
			return;
		}
		entry.Exiting = true;
		if (entry.Root == null || entry.VisualGroup == null || entry.VisualRect == null || entry.Layout == null || _motion == null)
		{
			DestroyEntry(entry);
			return;
		}

		float startAlpha = entry.VisualGroup.alpha;
		Vector2 startPosition = entry.VisualRect.anchoredPosition;
		Vector2 targetPosition = startPosition + new Vector2(UiMotionTokens.ToastExitOffset, 0f);
		float startHeight = entry.Layout.preferredHeight;
		_motion.Animate(entry.Root, UiMotionChannel.ToastAlpha, UiMotionTokens.ToastOut, UiMotionEasingKind.EaseOutCubic,
			progress => entry.VisualGroup.alpha = Mathf.LerpUnclamped(startAlpha, 0f, progress));
		_motion.Animate(entry.Root, UiMotionChannel.ToastPosition, UiMotionTokens.ToastOut, UiMotionEasingKind.EaseOutCubic,
			progress => entry.VisualRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, progress));
		_motion.Animate(entry.Root, UiMotionChannel.ToastHeight, UiMotionTokens.ToastOut, UiMotionEasingKind.EaseInOutCubic,
			progress => entry.Layout.preferredHeight = Mathf.LerpUnclamped(startHeight, 0f, progress),
			() => DestroyEntry(entry));
	}

	private int CountVisible()
	{
		int count = 0;
		for (int index = 0; index < _entries.Count; index++)
		{
			if (!_entries[index].Exiting && _entries[index].Root != null) count++;
		}
		return count;
	}

	private ToastEntry FindOldestVisible()
	{
		for (int index = 0; index < _entries.Count; index++)
		{
			if (!_entries[index].Exiting && _entries[index].Root != null) return _entries[index];
		}
		return null;
	}

	private void DestroyEntry(ToastEntry entry)
	{
		if (entry == null) return;
		_entries.Remove(entry);
		entry.Surface?.Dispose();
		entry.Surface = null;
		if (entry.Root != null)
		{
			_motion?.Cancel(entry.Root, includeChildren: true);
			UnityEngine.Object.Destroy(entry.Root);
		}
		entry.Root = null;
	}

	public void Dispose()
	{
		while (_entries.Count > 0)
		{
			DestroyEntry(_entries[_entries.Count - 1]);
		}
	}
}
