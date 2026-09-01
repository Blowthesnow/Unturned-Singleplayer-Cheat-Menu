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
		internal MinimapStarGraphic ItemOutline;
		internal MinimapStarGraphic ItemFill;
		internal Image Image;
	}

	private readonly RectTransform _parent;
	private readonly List<Slot> _slots = new List<Slot>();

	internal MinimapMarkerPool(RectTransform parent)
	{
		_parent = parent;
	}

	internal void Apply(IReadOnlyList<MinimapHudMarker> markers, Sprite dotSprite,
		Sprite nativeMarkerSprite, Func<TeleportMarkerKind, Sprite> teleportSprite)
	{
		int count = markers?.Count ?? 0;
		for (int index = 0; index < count; index++)
		{
			Slot slot = GetSlot(index);
			MinimapHudMarker marker = markers[index];
			bool itemMarker = marker.Kind == MinimapWorldMarkerKind.Item;
			slot.Root.SetActive(true);
			RectTransform rect = slot.Rect;
			rect.anchoredPosition = new Vector2(marker.X, marker.Y);
			rect.localRotation = Quaternion.Euler(0f, 0f, marker.Rotation);
			slot.Image.gameObject.SetActive(!itemMarker);
			slot.ItemOutline.gameObject.SetActive(itemMarker);
			slot.ItemFill.gameObject.SetActive(itemMarker);
			if (itemMarker)
			{
				slot.ItemOutline.color = new Color(0f, 0f, 0f, marker.Color.a * 0.90f);
				SetCentered(slot.ItemOutline.rectTransform, new Vector2(20f, 20f), Vector2.zero);
				slot.ItemFill.color = marker.Color;
				rect.sizeDelta = new Vector2(24f, 24f);
				SetCentered(slot.ItemFill.rectTransform, new Vector2(16f, 16f), Vector2.zero);
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
			GameObject outlineObject = new GameObject("ItemOutline", typeof(RectTransform), typeof(MinimapStarGraphic));
			outlineObject.transform.SetParent(root.transform, false);
			MinimapStarGraphic itemOutline = outlineObject.GetComponent<MinimapStarGraphic>();
			itemOutline.raycastTarget = false;
			Stretch(itemOutline.rectTransform);
			GameObject fillObject = new GameObject("ItemFill", typeof(RectTransform), typeof(MinimapStarGraphic));
			fillObject.transform.SetParent(root.transform, false);
			MinimapStarGraphic itemFill = fillObject.GetComponent<MinimapStarGraphic>();
			itemFill.raycastTarget = false;
			Stretch(itemFill.rectTransform);
			GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(Image));
			imageObject.transform.SetParent(root.transform, false);
			Image image = imageObject.GetComponent<Image>();
			image.preserveAspect = true;
			image.raycastTarget = false;
			Stretch(image.rectTransform);
			_slots.Add(new Slot
			{
				Root = root,
				Rect = rect,
				ItemOutline = itemOutline,
				ItemFill = itemFill,
				Image = image
			});
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
