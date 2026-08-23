using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class GlobalShortcutActions
{
    internal const string ToggleMenu = "menu.toggle";
    internal const string ArrangeCurrent = "inventory.arrange.current";
    internal const string ArrangeAllCarried = "inventory.arrange.all-carried";
}


internal sealed class GlobalShortcutManager
{
    private sealed class Registration
    {
        internal string ActionId;
        internal string DisplayName;
        internal ConfigEntry<KeyboardShortcut> Config;
        internal KeyboardShortcut DefaultShortcut;
        internal Action<string> Handler;
        internal NativeShortcutDetector NativeDetector = new();
        internal ShortcutToggleGate Gate = new();
    }

    private readonly ManualLogSource _log;
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);

    internal GlobalShortcutManager(ManualLogSource log)
    {
        _log = log;
    }

    internal void Register(
        string actionId,
        string displayName,
        ConfigEntry<KeyboardShortcut> config,
        Action<string> handler)
    {
        Register(actionId, displayName, config, config?.Value ?? new KeyboardShortcut(KeyCode.End), handler);
    }
    internal void Register(
        string actionId,
        string displayName,
        ConfigEntry<KeyboardShortcut> config,
        KeyboardShortcut defaultShortcut,
        Action<string> handler)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            throw new ArgumentException("Shortcut action ID is required.", nameof(actionId));
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (!ShortcutInputPolicy.IsAllowedShortcut(config.Value))
            throw new ArgumentException("The configured shortcut is not supported.", nameof(config));
        if (!ShortcutInputPolicy.IsAllowedShortcut(defaultShortcut))
            throw new ArgumentException("The default shortcut is not supported.", nameof(defaultShortcut));
        if (_registrations.ContainsKey(actionId))
            throw new InvalidOperationException($"Shortcut action already registered: {actionId}");

        _registrations.Add(actionId, new Registration
        {
            ActionId = actionId,
            DisplayName = displayName ?? actionId,
            Config = config,
            DefaultShortcut = defaultShortcut,
            Handler = handler
        });
    }

    internal bool Contains(string actionId)
    {
        return !string.IsNullOrEmpty(actionId) && _registrations.ContainsKey(actionId);
    }

    internal KeyboardShortcut GetShortcut(string actionId)
    {
        return _registrations.TryGetValue(actionId, out Registration registration)
            ? registration.Config.Value
            : new KeyboardShortcut(KeyCode.End);
    }

    internal string GetLabel(string actionId)
    {
        return GetShortcut(actionId).ToString();
    }

    internal string GetDisplayName(string actionId)
    {
        return _registrations.TryGetValue(actionId, out Registration registration)
            ? registration.DisplayName
            : actionId ?? string.Empty;
    }

    internal bool TrySetShortcut(string actionId, KeyboardShortcut shortcut, ConfigFile configFile)
    {
        if (!_registrations.TryGetValue(actionId, out Registration registration)
            || !ShortcutInputPolicy.IsAllowedShortcut(shortcut)
            || configFile == null)
        {
            return false;
        }
        if (_registrations.Values.Any(candidate => candidate.ActionId != actionId && candidate.Config.Value.Equals(shortcut)))
        {
            _log?.LogWarning($"全局快捷键重复，拒绝保存 {actionId}：{shortcut}。");
            return false;
        }


        bool saveOnConfigSet = configFile.SaveOnConfigSet;
        try
        {
            configFile.SaveOnConfigSet = false;
            registration.Config.Value = shortcut;
            configFile.Save();
            return true;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"无法保存全局快捷键 {actionId}：{ex}");
            return false;
        }
        finally
        {
            configFile.SaveOnConfigSet = saveOnConfigSet;
        }
    }

    internal void ProcessUpdate(int frame, float realtimeSinceStartup, bool inputSuppressed)
    {
        if (inputSuppressed)
            return;

        foreach (Registration registration in _registrations.Values)
        {
            KeyboardShortcut shortcut = registration.Config.Value;
            bool unityPressed = ShortcutInputPolicy.IsUnityPressed(shortcut);
            bool nativePressed = registration.NativeDetector.IsPressed(shortcut);
            if (!unityPressed && !nativePressed)
                continue;
            if (!registration.Gate.TryAccept(frame, realtimeSinceStartup))
                continue;

            registration.Handler(nativePressed && !unityPressed
                ? "Windows 原生输入"
                : "Unity 输入");
        }
    }

    internal bool ProcessGuiEvent(Event current, int frame, float realtimeSinceStartup, bool inputSuppressed)
    {
        if (inputSuppressed)
            return false;

        foreach (Registration registration in _registrations.Values)
        {
            if (!ShortcutInputPolicy.IsGuiEvent(current, registration.Config.Value)
                || !registration.Gate.TryAccept(frame, realtimeSinceStartup))
            {
                continue;
            }

            registration.Handler("IMGUI 事件");
            return true;
        }

        return false;
    }
}

internal static class ShortcutInputPolicy
{
    internal static readonly KeyCode[] ModifierKeys =
    {
        KeyCode.LeftControl,
        KeyCode.RightControl,
        KeyCode.LeftAlt,
        KeyCode.RightAlt,
        KeyCode.LeftShift,
        KeyCode.RightShift,
        KeyCode.LeftCommand,
        KeyCode.RightCommand
    };

    internal static readonly KeyCode[] MouseKeys =
    {
        KeyCode.Mouse2,
        KeyCode.Mouse3,
        KeyCode.Mouse4,
        KeyCode.Mouse5,
        KeyCode.Mouse6
    };

    internal static readonly KeyCode[] KeyboardMainKeys = Enum
        .GetValues(typeof(KeyCode))
        .Cast<KeyCode>()
        .Where(key => key != KeyCode.None
            && (int)key < (int)KeyCode.Mouse0
            && !ModifierKeys.Contains(key))
        .ToArray();

    internal static bool IsAllowedShortcut(KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None
            && shortcut.MainKey != KeyCode.Mouse0
            && shortcut.MainKey != KeyCode.Mouse1
            && (IsKeyboardKey(shortcut.MainKey) || MouseKeys.Contains(shortcut.MainKey));
    }

    internal static bool IsUnityPressed(KeyboardShortcut shortcut)
    {
        if (!IsAllowedShortcut(shortcut))
            return false;

        int mouseButton = GetMouseButtonIndex(shortcut.MainKey);
        if (mouseButton >= 0)
        {
            return Input.GetMouseButtonDown(mouseButton)
                && shortcut.Modifiers.All(Input.GetKey);
        }

        if (shortcut.IsDown())
            return true;

        return Input.GetKeyDown(shortcut.MainKey)
            && shortcut.Modifiers.All(Input.GetKey);
    }

    internal static bool IsGuiEvent(Event current, KeyboardShortcut shortcut)
    {
        if (current == null
            || current.type != EventType.KeyDown
            || !IsAllowedShortcut(shortcut)
            || GetMouseButtonIndex(shortcut.MainKey) >= 0)
        {
            return false;
        }

        return current.keyCode == shortcut.MainKey
            && shortcut.Modifiers.All(modifier => IsModifierPressed(current, modifier));
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
            _ => -1
        };
    }

    internal static KeyCode NormalizeModifier(KeyCode key)
    {
        return key switch
        {
            KeyCode.RightControl => KeyCode.LeftControl,
            KeyCode.RightAlt => KeyCode.LeftAlt,
            KeyCode.RightShift => KeyCode.LeftShift,
            KeyCode.RightCommand => KeyCode.LeftCommand,
            _ => key
        };
    }

    private static bool IsKeyboardKey(KeyCode key)
    {
        return (int)key > (int)KeyCode.None && (int)key < (int)KeyCode.Mouse0;
    }

    private static bool IsModifierPressed(Event current, KeyCode modifier)
    {
        return modifier switch
        {
            KeyCode.LeftShift or KeyCode.RightShift => current.shift,
            KeyCode.LeftControl or KeyCode.RightControl => current.control,
            KeyCode.LeftAlt or KeyCode.RightAlt => current.alt,
            KeyCode.LeftCommand or KeyCode.RightCommand => current.command,
            _ => Input.GetKey(modifier)
        };
    }
}
