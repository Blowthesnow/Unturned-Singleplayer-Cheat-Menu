using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class ShortcutInputPolicy
{
	internal static readonly KeyCode[] ModifierKeys = new KeyCode[8]
	{
		KeyCode.LeftControl,
		KeyCode.RightControl,
		KeyCode.LeftAlt,
		KeyCode.RightAlt,
		KeyCode.LeftShift,
		KeyCode.RightShift,
		KeyCode.LeftMeta,
		KeyCode.RightMeta
	};

	internal static readonly KeyCode[] MouseKeys = new KeyCode[5]
	{
		KeyCode.Mouse2,
		KeyCode.Mouse3,
		KeyCode.Mouse4,
		KeyCode.Mouse5,
		KeyCode.Mouse6
	};

	internal static readonly KeyCode[] KeyboardMainKeys = (from KeyCode key in Enum.GetValues(typeof(KeyCode))
		where key != KeyCode.None && key < KeyCode.Mouse0 && !Enumerable.Contains(ModifierKeys, key)
		select key).ToArray();

	internal static bool IsAllowedShortcut(KeyboardShortcut shortcut)
	{
		if (shortcut.MainKey == KeyCode.None)
		{
			return true;
		}
		if (shortcut.MainKey != KeyCode.None && shortcut.MainKey != KeyCode.Mouse0 && shortcut.MainKey != KeyCode.Mouse1)
		{
			if (!IsKeyboardKey(shortcut.MainKey))
			{
				return Enumerable.Contains(MouseKeys, shortcut.MainKey);
			}
			return true;
		}
		return false;
	}

	internal static bool IsUnityPressed(KeyboardShortcut shortcut)
	{
		if (!IsAllowedShortcut(shortcut))
		{
			return false;
		}
		int mouseButtonIndex = GetMouseButtonIndex(shortcut.MainKey);
		if (mouseButtonIndex >= 0)
		{
			if (Input.GetMouseButtonDown(mouseButtonIndex))
			{
				return shortcut.Modifiers.All(Input.GetKey);
			}
			return false;
		}
		if (shortcut.IsDown())
		{
			return true;
		}
		if (Input.GetKeyDown(shortcut.MainKey))
		{
			return shortcut.Modifiers.All(Input.GetKey);
		}
		return false;
	}

	internal static int GetMouseButtonIndex(KeyCode key)
	{
		return key switch
		{
			KeyCode.Mouse2 => 2,
			KeyCode.Mouse3 => 3,
			KeyCode.Mouse4 => 4,
			KeyCode.Mouse5 => 5,
			KeyCode.Mouse6 => 6,
			_ => -1,
		};
	}

	internal static KeyCode NormalizeModifier(KeyCode key)
	{
		return key switch
		{
			KeyCode.RightControl => KeyCode.LeftControl,
			KeyCode.RightAlt => KeyCode.LeftAlt,
			KeyCode.RightShift => KeyCode.LeftShift,
			KeyCode.RightMeta => KeyCode.LeftMeta,
			_ => key,
		};
	}

	private static bool IsKeyboardKey(KeyCode key)
	{
		if (key > KeyCode.None)
		{
			return key < KeyCode.Mouse0;
		}
		return false;
	}

}
