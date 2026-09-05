using System;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Keeps the control subset independent of tooltip-layer ownership and localization.</summary>
internal sealed class LegacyFluentTooltipBridge
{
	private readonly Action<string, RectTransform, Vector2, bool> _show;
	private readonly Action _hide;

	internal LegacyFluentTooltipBridge(Action<string, RectTransform, Vector2, bool> show, Action hide)
	{
		_show = show ?? throw new ArgumentNullException(nameof(show));
		_hide = hide ?? throw new ArgumentNullException(nameof(hide));
	}

	internal void Attach(OverlaySelectableVisual visual, string message, bool delay)
	{
		if (visual == null)
		{
			throw new ArgumentNullException(nameof(visual));
		}
		visual.SetHoverExplanation(message, _show, _hide, delay);
	}
}
