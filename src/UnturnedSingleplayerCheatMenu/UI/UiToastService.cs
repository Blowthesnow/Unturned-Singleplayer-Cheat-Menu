using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class UiToastService : IDisposable
{
	private const int MaximumVisible = 3;
	private readonly RectTransform _layer;
	private readonly ManualLogSource _log;
	private readonly List<ToastEntry> _entries = new List<ToastEntry>();

	private sealed class ToastEntry
	{
		internal GameObject Root;
		internal float ExpiresAt;
	}

	internal UiToastService(RectTransform layer, ManualLogSource log)
	{
		_layer = layer;
		_log = log;
		VerticalLayoutGroup layout = layer.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(0, 24, 24, 0);
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.UpperRight;
		layout.childControlWidth = false;
		layout.childControlHeight = false;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
	}

	internal void Show(string message, float seconds, Color accent)
	{
		if (_layer == null || string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		while (_entries.Count >= MaximumVisible)
		{
			DestroyAt(0);
		}
		GameObject root = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
		root.transform.SetParent(_layer, false);
		Image background = root.GetComponent<Image>();
		background.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
		CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;
		LayoutElement size = root.GetComponent<LayoutElement>();
		size.preferredWidth = 420f;
		size.preferredHeight = 46f;

		GameObject bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
		bar.transform.SetParent(root.transform, false);
		RectTransform barRect = bar.GetComponent<RectTransform>();
		barRect.anchorMin = Vector2.zero;
		barRect.anchorMax = new Vector2(0f, 1f);
		barRect.pivot = new Vector2(0f, 0.5f);
		barRect.sizeDelta = new Vector2(4f, 0f);
		bar.GetComponent<Image>().color = accent;

		GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(UiText));
		textObject.transform.SetParent(root.transform, false);
		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(14f, 5f);
		textRect.offsetMax = new Vector2(-10f, -5f);
		UiText text = textObject.GetComponent<UiText>();
		text.font = SystemFontProvider.Resolve(_log);
		text.text = message;
		text.fontSize = 13;
		text.color = Color.white;
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.raycastTarget = false;
		_entries.Add(new ToastEntry { Root = root, ExpiresAt = Time.unscaledTime + Mathf.Max(1f, seconds) });
	}

	internal void Pump()
	{
		for (int i = _entries.Count - 1; i >= 0; i--)
		{
			if (_entries[i].Root == null || Time.unscaledTime >= _entries[i].ExpiresAt)
			{
				DestroyAt(i);
			}
		}
	}

	private void DestroyAt(int index)
	{
		ToastEntry entry = _entries[index];
		_entries.RemoveAt(index);
		if (entry.Root != null)
		{
			UnityEngine.Object.Destroy(entry.Root);
		}
	}

	public void Dispose()
	{
		for (int i = _entries.Count - 1; i >= 0; i--)
		{
			DestroyAt(i);
		}
	}
}
