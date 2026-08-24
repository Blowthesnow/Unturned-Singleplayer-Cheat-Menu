using System.Runtime.InteropServices;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class NativeKeyboard
{
	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int virtualKey);

	internal static bool IsDown(KeyCode key)
	{
		return (GetState(key) & 0x8000) != 0;
	}

	internal static short GetState(KeyCode key)
	{
		int num = ToVirtualKey(key);
		if (num != 0)
		{
			return GetAsyncKeyState(num);
		}
		return 0;
	}

	private static int ToVirtualKey(KeyCode key)
	{
		if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
		{
			return (int)(48 + (key - 48));
		}
		if (key >= KeyCode.A && key <= KeyCode.Z)
		{
			return (int)(65 + (key - 97));
		}
		if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
		{
			return (int)(96 + (key - 256));
		}
		if (key >= KeyCode.F1 && key <= KeyCode.F15)
		{
			return (int)(112 + (key - 282));
		}
		switch (key)
		{
		case KeyCode.Backspace:
			return 8;
		case KeyCode.Tab:
			return 9;
		case KeyCode.Clear:
			return 12;
		case KeyCode.Return:
		case KeyCode.KeypadEnter:
			return 13;
		case KeyCode.Pause:
			return 19;
		case KeyCode.Escape:
			return 27;
		case KeyCode.Space:
			return 32;
		case KeyCode.PageUp:
			return 33;
		case KeyCode.PageDown:
			return 34;
		case KeyCode.End:
			return 35;
		case KeyCode.Home:
			return 36;
		case KeyCode.LeftArrow:
			return 37;
		case KeyCode.UpArrow:
			return 38;
		case KeyCode.RightArrow:
			return 39;
		case KeyCode.DownArrow:
			return 40;
		case KeyCode.Insert:
			return 45;
		case KeyCode.Delete:
			return 46;
		case KeyCode.KeypadMultiply:
			return 106;
		case KeyCode.KeypadPlus:
			return 107;
		case KeyCode.KeypadMinus:
			return 109;
		case KeyCode.KeypadPeriod:
			return 110;
		case KeyCode.KeypadDivide:
			return 111;
		case KeyCode.Numlock:
			return 144;
		case KeyCode.ScrollLock:
			return 145;
		case KeyCode.CapsLock:
			return 20;
		case KeyCode.LeftShift:
			return 160;
		case KeyCode.RightShift:
			return 161;
		case KeyCode.LeftControl:
			return 162;
		case KeyCode.RightControl:
			return 163;
		case KeyCode.LeftAlt:
			return 164;
		case KeyCode.RightAlt:
		case KeyCode.AltGr:
			return 165;
		case KeyCode.LeftMeta:
		case KeyCode.LeftWindows:
			return 91;
		case KeyCode.RightMeta:
		case KeyCode.RightWindows:
			return 92;
		case KeyCode.Menu:
			return 93;
		case KeyCode.Colon:
		case KeyCode.Semicolon:
			return 186;
		case KeyCode.Plus:
		case KeyCode.Equals:
		case KeyCode.KeypadEquals:
			return 187;
		case KeyCode.Comma:
		case KeyCode.Less:
			return 188;
		case KeyCode.Minus:
		case KeyCode.Underscore:
			return 189;
		case KeyCode.Period:
		case KeyCode.Greater:
			return 190;
		case KeyCode.Slash:
		case KeyCode.Question:
			return 191;
		case KeyCode.BackQuote:
			return 192;
		case KeyCode.LeftBracket:
			return 219;
		case KeyCode.Backslash:
			return 220;
		case KeyCode.RightBracket:
			return 221;
		case KeyCode.DoubleQuote:
		case KeyCode.Quote:
			return 222;
		default:
			return 0;
		}
	}
}
