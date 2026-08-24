using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class NativeShortcutDetector
{
	private KeyCode _trackedMainKey;

	private bool _wasMainKeyDown;

	internal bool IsPressed(KeyboardShortcut shortcut)
	{
		KeyCode mainKey = shortcut.MainKey;
		short state = NativeKeyboard.GetState(mainKey);
		bool flag = (state & 0x8000) != 0;
		bool flag2 = (state & 1) != 0;
		if (_trackedMainKey != mainKey)
		{
			_trackedMainKey = mainKey;
			_wasMainKeyDown = flag;
			return false;
		}
		bool result = (flag2 || (flag && !_wasMainKeyDown)) && shortcut.Modifiers.All(NativeKeyboard.IsDown);
		_wasMainKeyDown = flag;
		return result;
	}
}
