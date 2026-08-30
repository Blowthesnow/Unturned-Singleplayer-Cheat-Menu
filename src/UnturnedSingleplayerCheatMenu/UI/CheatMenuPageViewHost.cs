using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed class CheatMenuPageViewHost : ActivePageViewHost<CheatMenuOverlayUi.MenuTab>
{
	internal CheatMenuPageViewHost(
		Func<CheatMenuOverlayUi.MenuTab, UiPageView<CheatMenuOverlayUi.MenuTab>> createView) : base(createView)
	{
	}
}
