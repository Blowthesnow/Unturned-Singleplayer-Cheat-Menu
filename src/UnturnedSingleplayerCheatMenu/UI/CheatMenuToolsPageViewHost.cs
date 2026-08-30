using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class CheatMenuToolsPageViewHost : ActivePageViewHost<CheatMenuOverlayUi.ToolsTab>
{
	internal CheatMenuToolsPageViewHost(
		Func<CheatMenuOverlayUi.ToolsTab, UiPageView<CheatMenuOverlayUi.ToolsTab>> createView) : base(createView)
	{
	}
}
