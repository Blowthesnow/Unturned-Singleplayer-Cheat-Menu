using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.Minimap;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class MinimapMarkerPool
{
	private sealed class Slot
	{
		internal GameObject Root;
		internal RectTransform Rect;
		internal Image Image;
		internal Image HeightIndicator;
	}

	private readonly RectTransform _parent;
	private readonly List<Slot> _slots = new List<Slot>();

	internal MinimapMarkerPool(RectTransform parent)
	{
		_parent = parent;
	}

	internal void Apply(IReadOnlyList<MinimapHudMarker> markers, Sprite dotSprite, Sprite itemStarSprite,
		Sprite heightIndicatorSprite, Sprite nativeMarkerSprite, Func<TeleportMarkerKind, Sprite> teleportSprite)
	{
		int count = markers?.Count ?? 0;
		for (int index = 0; index < count; index++)
		{
			Slot slot = GetSlot(index);
			MinimapHudMarker marker = markers[index];
			bool itemMarker = marker.Kind == MinimapWorldMarkerKind.Item;
			bool hasHeightIndicator = marker.HeightIndicator != MinimapHeightIndicator.None;
			slot.Root.SetActive(true);
			RectTransform rect = slot.Rect;
			rect.anchoredPosition = new Vector2(marker.X, marker.Y);
			rect.localRotation = Quaternion.Euler(0f, 0f, marker.Rotation);
			slot.Image.gameObject.SetActive(true);
			slot.HeightIndicator.gameObject.SetActive(hasHeightIndicator);
			if (itemMarker)
			{
				slot.Image.sprite = itemStarSprite ?? dotSprite;
				slot.Image.color = marker.Color;
				rect.sizeDelta = new Vector2(24f, 24f);
				SetCentered(slot.Image.rectTransform, new Vector2(16f, 16f), Vector2.zero);
			}
			else
			{
				slot.Image.sprite = marker.Kind switch
				{
					MinimapWorldMarkerKind.NativeMapMarker => nativeMarkerSprite ?? dotSprite,
					MinimapWorldMarkerKind.Teleport => teleportSprite(marker.TeleportKind) ?? dotSprite,
					_ => dotSprite
				};
				slot.Image.color = marker.Color;
				float size = marker.Kind == MinimapWorldMarkerKind.NativeMapMarker || marker.Kind == MinimapWorldMarkerKind.Teleport ? 16f : 8f;
				rect.sizeDelta = new Vector2(size, size);
				Stretch(slot.Image.rectTransform);
			}
			if (hasHeightIndicator)
			{
				slot.HeightIndicator.sprite = heightIndicatorSprite;
				slot.HeightIndicator.color = marker.Color;
				SetCentered(slot.HeightIndicator.rectTransform, new Vector2(10f, 10f), new Vector2(7f, 8f));
				float indicatorRotation = marker.HeightIndicator == MinimapHeightIndicator.Above ? 0f : 180f;
				slot.HeightIndicator.rectTransform.localRotation = Quaternion.Euler(0f, 0f, indicatorRotation - marker.Rotation);
			}
		}
		for (int index = count; index < _slots.Count; index++)
		{
			_slots[index].Root.SetActive(false);
		}
	}

	private Slot GetSlot(int index)
	{
		while (_slots.Count <= index)
		{
			GameObject root = new GameObject("MarkerSlot", typeof(RectTransform));
			root.transform.SetParent(_parent, false);
			RectTransform rect = root.GetComponent<RectTransform>();
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(Image));
			imageObject.transform.SetParent(root.transform, false);
			Image image = imageObject.GetComponent<Image>();
			image.preserveAspect = true;
			image.raycastTarget = false;
			Stretch(image.rectTransform);
			GameObject indicatorObject = new GameObject("HeightIndicator", typeof(RectTransform), typeof(Image));
			indicatorObject.transform.SetParent(root.transform, false);
			Image heightIndicator = indicatorObject.GetComponent<Image>();
			heightIndicator.preserveAspect = true;
			heightIndicator.raycastTarget = false;
			Stretch(heightIndicator.rectTransform);
			_slots.Add(new Slot { Root = root, Rect = rect, Image = image, HeightIndicator = heightIndicator });
		}
		return _slots[index];
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
	{
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = size;
		rect.anchoredPosition = position;
	}
}
