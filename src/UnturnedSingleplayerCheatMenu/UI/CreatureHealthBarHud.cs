using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

internal readonly struct CreatureHealthBarRenderData
{
	public readonly int InstanceId;
	public readonly Vector3 Anchor;
	public readonly string Text;
	public readonly float Fill;
	public readonly float Scale;
	public readonly Vector2 ScreenPosition;
	public readonly bool HasScreenPosition;

	public CreatureHealthBarRenderData(int instanceId, Vector3 anchor, string text, float fill, float scale)
	{
		InstanceId = instanceId;
		Anchor = anchor;
		Text = text;
		Fill = fill;
		Scale = scale;
		ScreenPosition = default;
		HasScreenPosition = false;
	}

	public CreatureHealthBarRenderData(int instanceId, Vector3 anchor, string text, float fill, float scale, Vector2 screenPosition)
	{
		InstanceId = instanceId;
		Anchor = anchor;
		Text = text;
		Fill = fill;
		Scale = scale;
		ScreenPosition = screenPosition;
		HasScreenPosition = true;
	}
}

internal sealed class CreatureHealthBarHud : IDisposable
{
	private sealed class Item
	{
		public GameObject Root;
		public RectTransform Rect;
		public RectTransform FillRect;
		public Text Label;
		public int LastInstanceId = int.MinValue;
		public string LastText;
		public float LastFill = -1f;
	}

	private readonly ManualLogSource _log;
	private readonly List<Item> _pool = new List<Item>();
	private GameObject _root;
	private RectTransform _rootRect;
	private Font _font;
	private int _fontRevision = -1;
	private float _nextCreateAttempt;
	private bool _loggedCreation;

	internal CreatureHealthBarHud(ManualLogSource log)
	{
		_log = log;
	}

	internal void Render(IReadOnlyList<CreatureHealthBarRenderData> bars, Camera camera)
	{
		if (bars == null || bars.Count == 0 || camera == null || !EnsureCreated())
		{
			HideAll();
			return;
		}

		EnsurePool(bars.Count);
		int visible = 0;
		for (int i = 0; i < bars.Count; i++)
		{
			Vector3 screen = bars[i].HasScreenPosition
				? new Vector3(bars[i].ScreenPosition.x, bars[i].ScreenPosition.y, 1f)
				: camera.WorldToScreenPoint(bars[i].Anchor);
			if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
			{
				continue;
			}
			RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screen, null, out Vector2 local);
			Item item = _pool[visible++];
			bool targetChanged = item.LastInstanceId != bars[i].InstanceId;
			item.Root.SetActive(true);
			item.Rect.anchoredPosition = local + new Vector2(0f, 4f);
			item.Rect.localScale = Vector3.one * Mathf.Clamp(bars[i].Scale, 0.75f, 3f);
			if (targetChanged || !string.Equals(item.LastText, bars[i].Text, StringComparison.Ordinal))
			{
				item.Label.text = bars[i].Text;
				item.LastText = bars[i].Text;
			}
			float fill = Mathf.Clamp01(bars[i].Fill);
			if (targetChanged || Mathf.Abs(item.LastFill - fill) > 0.001f)
			{
				item.FillRect.sizeDelta = new Vector2(116f * fill, 16f);
				item.LastFill = fill;
			}
			item.LastInstanceId = bars[i].InstanceId;
		}
		for (int i = visible; i < _pool.Count; i++)
		{
			_pool[i].Root.SetActive(false);
		}
		_root.SetActive(visible > 0);
	}

	internal void HideAll()
	{
		if (_root == null)
		{
			return;
		}
		for (int i = 0; i < _pool.Count; i++)
		{
			_pool[i].Root.SetActive(false);
		}
		_root.SetActive(false);
	}

	public void Dispose()
	{
		DestroyGenerated();
	}

	internal void ResetWorld()
	{
		DestroyGenerated();
		_nextCreateAttempt = 0f;
	}

	private void DestroyGenerated()
	{
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
		}
		_root = null;
		_rootRect = null;
		_pool.Clear();
		_font = null;
		_fontRevision = -1;
	}

	private bool EnsureCreated()
	{
		if (_root != null && _rootRect != null)
		{
			return EnsureFontBinding();
		}
		if (Time.unscaledTime < _nextCreateAttempt || (MainCamera.instance ?? Camera.main) == null || PlayerUI.window == null)
		{
			return false;
		}
		_nextCreateAttempt = Time.unscaledTime + 1f;
		try
		{
			if (!EnsureFontBinding())
			{
				return false;
			}
			_root = new GameObject("UnturnedSingleplayerCheatMenu.CreatureHealthBarHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
			UnityEngine.Object.DontDestroyOnLoad(_root);
			_rootRect = _root.GetComponent<RectTransform>();
			_rootRect.anchorMin = Vector2.zero;
			_rootRect.anchorMax = Vector2.one;
			_rootRect.offsetMin = Vector2.zero;
			_rootRect.offsetMax = Vector2.zero;
			Canvas canvas = _root.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.overrideSorting = true;
			canvas.sortingOrder = 29940;
			CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
			scaler.scaleFactor = 1f;
			EnsurePool(12);
			_root.SetActive(false);
			if (!_loggedCreation)
			{
				_loggedCreation = true;
				_log.LogInfo($"[CreatureHealthBarHud] created: sortingOrder={canvas.sortingOrder}, screen={Screen.width}x{Screen.height}");
			}
			return true;
		}
		catch (Exception ex)
		{
			_log.LogWarning("[CreatureHealthBarHud] creation failed; will retry: " + ex.Message);
			Dispose();
			return false;
		}
	}

	private bool EnsureFontBinding()
	{
		int revision = SystemFontProvider.Revision;
		if (_font != null && _fontRevision == revision)
		{
			return true;
		}

		Font font = SystemFontProvider.Resolve(_log);
		if (font == null)
		{
			return false;
		}
		_font = font;
		_fontRevision = SystemFontProvider.Revision;
		foreach (Item item in _pool)
		{
			if (item.Label != null)
			{
				item.Label.font = _font;
			}
		}
		return true;
	}

	private void EnsurePool(int count)
	{
		while (_pool.Count < count)
		{
			_pool.Add(CreateItem(_pool.Count));
		}
	}

	private Item CreateItem(int index)
	{
		GameObject root = new GameObject("CreatureHealthBarItem." + index, typeof(RectTransform));
		root.transform.SetParent(_root.transform, false);
		RectTransform rect = root.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(120f, 28f);

		CreateBorderEdge(root.transform, "Border.Bottom", new Vector2(0f, 4f), new Vector2(120f, 2f));
		CreateBorderEdge(root.transform, "Border.Top", new Vector2(0f, 22f), new Vector2(120f, 2f));
		CreateBorderEdge(root.transform, "Border.Left", new Vector2(0f, 4f), new Vector2(2f, 20f));
		CreateBorderEdge(root.transform, "Border.Right", new Vector2(118f, 4f), new Vector2(2f, 20f));

		GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
		fillObject.transform.SetParent(root.transform, false);
		RectTransform fillRect = fillObject.GetComponent<RectTransform>();
		fillRect.anchorMin = new Vector2(0f, 0f);
		fillRect.anchorMax = new Vector2(0f, 0f);
		fillRect.pivot = new Vector2(0f, 0f);
		fillRect.anchoredPosition = new Vector2(2f, 6f);
		fillRect.sizeDelta = new Vector2(116f, 16f);
		Image fill = fillObject.GetComponent<Image>();
		fill.color = new Color(0.86f, 0.08f, 0.08f, 1f);
		fill.raycastTarget = false;

		GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
		labelObject.transform.SetParent(root.transform, false);
		RectTransform labelRect = labelObject.GetComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0f, 0f);
		labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(2f, 4f);
        labelRect.offsetMax = new Vector2(-2f, 0f);
		Text label = labelObject.GetComponent<Text>();
		label.font = _font;
		label.fontSize = 13;
		label.alignment = TextAnchor.MiddleCenter;
		label.lineSpacing = 1f;
		label.color = Color.white;
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.raycastTarget = false;
		Outline outline = labelObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
		outline.effectDistance = new Vector2(1f, -1f);
		root.SetActive(false);
		return new Item { Root = root, Rect = rect, FillRect = fillRect, Label = label };
	}

	private static void CreateBorderEdge(Transform parent, string name, Vector2 position, Vector2 size)
	{
		GameObject edgeObject = new GameObject(name, typeof(RectTransform), typeof(Image));
		edgeObject.transform.SetParent(parent, false);
		RectTransform edgeRect = edgeObject.GetComponent<RectTransform>();
		edgeRect.anchorMin = Vector2.zero;
		edgeRect.anchorMax = Vector2.zero;
		edgeRect.pivot = Vector2.zero;
		edgeRect.anchoredPosition = position;
		edgeRect.sizeDelta = size;
		Image edge = edgeObject.GetComponent<Image>();
		edge.color = Color.white;
		edge.raycastTarget = false;
	}

}
