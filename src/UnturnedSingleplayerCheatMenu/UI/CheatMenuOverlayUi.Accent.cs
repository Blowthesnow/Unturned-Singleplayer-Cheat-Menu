using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private void CreateAccentPickerButton(Transform parent)
	{
		Button button = CreateButton(parent, "强调色", ShowAccentColorPicker,
			"选择仅在本次游戏会话生效的强调色；不会修改传送点颜色。", 82f, 40f, CheatMenuStyle.Surface);
		GameObject swatch = CreateObject("AccentSwatch", button.transform, typeof(Image));
		Image swatchImage = swatch.GetComponent<Image>();
		swatchImage.color = CheatMenuStyle.Accent;
		swatchImage.raycastTarget = false;
		RectTransform swatchRect = swatch.GetComponent<RectTransform>();
		swatchRect.anchorMin = new Vector2(0f, 0.2f);
		swatchRect.anchorMax = new Vector2(0f, 0.8f);
		swatchRect.pivot = new Vector2(0f, 0.5f);
		swatchRect.anchoredPosition = new Vector2(7f, 0f);
		swatchRect.sizeDelta = new Vector2(5f, 0f);
	}

	private void ShowAccentColorPicker()
	{
		ShowColorPicker(ColorPickerPurpose.Accent);
	}

	private void ApplyAccentColorDraft()
	{
		_accentColor = _teleportColorDraft;
		CheatMenuStyle.ApplyAccent(_accentColor);
		HideTeleportColorPicker();
		RebuildVisualTreesPreservingVisibility();
		SetStatus("强调色已应用；仅在本次游戏会话生效。", 4f, StatusKind.Success);
	}

	private void ApplyTeleportMarkerColorDraft()
	{
		_teleportMarkerColor = _teleportColorDraft;
		HideTeleportColorPicker();
		ShowTab(MenuTab.Teleports);
	}
}
