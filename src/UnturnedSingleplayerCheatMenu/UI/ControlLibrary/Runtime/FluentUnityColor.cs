using UnityEngine;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

internal static class FluentUnityColor
{
	internal static Color ToUnity(this RgbaColor color) =>
		new Color32(color.Red, color.Green, color.Blue, color.Alpha);

	internal static RgbaColor ToRgba(this Color color)
	{
		Color32 value = color;
		return new RgbaColor(value.r, value.g, value.b, value.a);
	}
}
