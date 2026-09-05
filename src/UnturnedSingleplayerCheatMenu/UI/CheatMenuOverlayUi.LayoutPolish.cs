using UnityEngine;
using UnityEngine.UI;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	// Let the owning layout query Text.preferredHeight after assigning its width.
	private Text CreateFlowLabel(Transform parent, string value, Color color, int size = 14)
	{
		Text text = CreateText(parent, value, size, color, TextAnchor.UpperLeft);
		LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
		layout.minHeight = 24f;
		layout.preferredHeight = -1f;
		layout.flexibleWidth = 1f;
		layout.flexibleHeight = 0f;
		return text;
	}
}
