using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Rectangular HUD backdrop: black at 80% opacity with four independent thin borders.</summary>
internal static class HudFrameStyle
{
	private const float BorderThickness = 1f;

	internal static void Apply(GameObject host, Image background)
	{
		if (host == null || background == null)
		{
			return;
		}

		background.color = CheatMenuStyle.HudBackdrop;
		background.raycastTarget = false;
		CreateHorizontalBorder(host.transform, "HudBorder.Top", top: true);
		CreateHorizontalBorder(host.transform, "HudBorder.Bottom", top: false);
		CreateVerticalBorder(host.transform, "HudBorder.Left", right: false);
		CreateVerticalBorder(host.transform, "HudBorder.Right", right: true);
	}

	private static void CreateHorizontalBorder(Transform parent, string name, bool top)
	{
		GameObject edge = CreateEdge(parent, name);
		RectTransform rect = edge.GetComponent<RectTransform>();
		float y = top ? 1f : 0f;
		rect.anchorMin = new Vector2(0f, y);
		rect.anchorMax = new Vector2(1f, y);
		rect.pivot = new Vector2(0.5f, y);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(0f, BorderThickness);
	}

	private static void CreateVerticalBorder(Transform parent, string name, bool right)
	{
		GameObject edge = CreateEdge(parent, name);
		RectTransform rect = edge.GetComponent<RectTransform>();
		float x = right ? 1f : 0f;
		rect.anchorMin = new Vector2(x, 0f);
		rect.anchorMax = new Vector2(x, 1f);
		rect.pivot = new Vector2(x, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(BorderThickness, 0f);
	}

	private static GameObject CreateEdge(Transform parent, string name)
	{
		GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
		edge.transform.SetParent(parent, worldPositionStays: false);
		Image image = edge.GetComponent<Image>();
		image.color = CheatMenuStyle.HudBorder;
		image.raycastTarget = false;
		edge.GetComponent<LayoutElement>().ignoreLayout = true;
		edge.transform.SetAsFirstSibling();
		return edge;
	}
}
