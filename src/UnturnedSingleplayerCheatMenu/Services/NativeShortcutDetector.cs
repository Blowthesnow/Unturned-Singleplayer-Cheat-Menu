using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Configuration;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class NativeShortcutDetector
{
    private KeyCode _trackedMainKey = KeyCode.None;
    private bool _wasMainKeyDown;

    internal bool IsPressed(KeyboardShortcut shortcut)
    {
        KeyCode mainKey = shortcut.MainKey;
        short mainKeyState = NativeKeyboard.GetState(mainKey);
        bool mainKeyDown = (mainKeyState & 0x8000) != 0;
        bool wasPressedSinceLastPoll = (mainKeyState & 0x0001) != 0;
        if (_trackedMainKey != mainKey)
        {
            _trackedMainKey = mainKey;
            _wasMainKeyDown = mainKeyDown;
            return false;
        }

        bool pressed = (wasPressedSinceLastPoll || (mainKeyDown && !_wasMainKeyDown))
            && shortcut.Modifiers.All(NativeKeyboard.IsDown);
        _wasMainKeyDown = mainKeyDown;
        return pressed;
    }
}

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
        int virtualKey = ToVirtualKey(key);
        return virtualKey == 0 ? (short)0 : GetAsyncKeyState(virtualKey);
    }

    private static int ToVirtualKey(KeyCode key)
    {
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return 0x30 + ((int)key - (int)KeyCode.Alpha0);
        if (key >= KeyCode.A && key <= KeyCode.Z)
            return 0x41 + ((int)key - (int)KeyCode.A);
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            return 0x60 + ((int)key - (int)KeyCode.Keypad0);
        if (key >= KeyCode.F1 && key <= KeyCode.F15)
            return 0x70 + ((int)key - (int)KeyCode.F1);

        return key switch
        {
            KeyCode.Backspace => 0x08,
            KeyCode.Tab => 0x09,
            KeyCode.Clear => 0x0C,
            KeyCode.Return or KeyCode.KeypadEnter => 0x0D,
            KeyCode.Pause => 0x13,
            KeyCode.Escape => 0x1B,
            KeyCode.Space => 0x20,
            KeyCode.PageUp => 0x21,
            KeyCode.PageDown => 0x22,
            KeyCode.End => 0x23,
            KeyCode.Home => 0x24,
            KeyCode.LeftArrow => 0x25,
            KeyCode.UpArrow => 0x26,
            KeyCode.RightArrow => 0x27,
            KeyCode.DownArrow => 0x28,
            KeyCode.Insert => 0x2D,
            KeyCode.Delete => 0x2E,
            KeyCode.KeypadMultiply => 0x6A,
            KeyCode.KeypadPlus => 0x6B,
            KeyCode.KeypadMinus => 0x6D,
            KeyCode.KeypadPeriod => 0x6E,
            KeyCode.KeypadDivide => 0x6F,
            KeyCode.Numlock => 0x90,
            KeyCode.ScrollLock => 0x91,
            KeyCode.CapsLock => 0x14,
            KeyCode.LeftShift => 0xA0,
            KeyCode.RightShift => 0xA1,
            KeyCode.LeftControl => 0xA2,
            KeyCode.RightControl => 0xA3,
            KeyCode.LeftAlt => 0xA4,
            KeyCode.RightAlt or KeyCode.AltGr => 0xA5,
            KeyCode.LeftWindows or KeyCode.LeftCommand => 0x5B,
            KeyCode.RightWindows or KeyCode.RightCommand => 0x5C,
            KeyCode.Menu => 0x5D,
            KeyCode.Semicolon or KeyCode.Colon => 0xBA,
            KeyCode.Equals or KeyCode.Plus or KeyCode.KeypadEquals => 0xBB,
            KeyCode.Comma or KeyCode.Less => 0xBC,
            KeyCode.Minus or KeyCode.Underscore => 0xBD,
            KeyCode.Period or KeyCode.Greater => 0xBE,
            KeyCode.Slash or KeyCode.Question => 0xBF,
            KeyCode.BackQuote => 0xC0,
            KeyCode.LeftBracket => 0xDB,
            KeyCode.Backslash => 0xDC,
            KeyCode.RightBracket => 0xDD,
            KeyCode.Quote or KeyCode.DoubleQuote => 0xDE,
            _ => 0
        };
    }
}
