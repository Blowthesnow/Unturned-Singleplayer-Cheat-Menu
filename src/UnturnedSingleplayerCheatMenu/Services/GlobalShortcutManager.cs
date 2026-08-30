using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum ShortcutMouseWheelDirection
{
	None,
	Up,
	Down
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

		internal ShortcutMouseWheelDirection WheelDirection;

		internal KeyCode WheelModifier;

		internal string FixedLabel;

		internal Action<string, float> WheelHandler;

		internal NativeShortcutDetector NativeDetector = new NativeShortcutDetector();

		internal ShortcutToggleGate Gate = new ShortcutToggleGate();
	}

	private readonly ManualLogSource _log;

	private readonly Dictionary<string, Registration> _registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);

	internal GlobalShortcutManager(ManualLogSource log)
	{
		_log = log;
	}

	internal void Register(string actionId, string displayName, ConfigEntry<KeyboardShortcut> config, Action<string> handler)
	{
		Register(actionId, displayName, config, config?.Value ?? new KeyboardShortcut(KeyCode.End), handler);
	}

	internal void Register(string actionId, string displayName, ConfigEntry<KeyboardShortcut> config, KeyboardShortcut defaultShortcut, Action<string> handler)
	{
		if (string.IsNullOrWhiteSpace(actionId))
		{
			throw new ArgumentException("Shortcut action ID is required.", "actionId");
		}
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		if (handler == null)
		{
			throw new ArgumentNullException("handler");
		}
		if (!ShortcutInputPolicy.IsAllowedShortcut(config.Value))
		{
			throw new ArgumentException("The configured shortcut is not supported.", "config");
		}
		if (!ShortcutInputPolicy.IsAllowedShortcut(defaultShortcut))
		{
			throw new ArgumentException("The default shortcut is not supported.", "defaultShortcut");
		}
		if (_registrations.ContainsKey(actionId))
		{
			throw new InvalidOperationException("Shortcut action already registered: " + actionId);
		}
		_registrations.Add(actionId, new Registration
		{
			ActionId = actionId,
			DisplayName = (displayName ?? actionId),
			Config = config,
			DefaultShortcut = defaultShortcut,
			Handler = handler
		});
	}

	internal void RegisterMouseWheel(string actionId, string displayName, ShortcutMouseWheelDirection direction,
		KeyCode modifier, string fixedLabel, Action<string, float> handler)
	{
		if (string.IsNullOrWhiteSpace(actionId))
		{
			throw new ArgumentException("Shortcut action ID is required.", "actionId");
		}
		if (direction == ShortcutMouseWheelDirection.None)
		{
			throw new ArgumentException("Mouse-wheel direction is required.", "direction");
		}
		if (handler == null)
		{
			throw new ArgumentNullException("handler");
		}
		if (_registrations.ContainsKey(actionId))
		{
			throw new InvalidOperationException("Shortcut action already registered: " + actionId);
		}
		_registrations.Add(actionId, new Registration
		{
			ActionId = actionId,
			DisplayName = displayName ?? actionId,
			WheelDirection = direction,
			WheelModifier = ShortcutInputPolicy.NormalizeModifier(modifier),
			FixedLabel = fixedLabel ?? string.Empty,
			WheelHandler = handler
		});
	}

	internal bool Contains(string actionId)
	{
		if (!string.IsNullOrEmpty(actionId))
		{
			return _registrations.ContainsKey(actionId);
		}
		return false;
	}

	internal bool IsFixedGesture(string actionId)
	{
		return _registrations.TryGetValue(actionId, out var value) &&
			value.WheelDirection != ShortcutMouseWheelDirection.None;
	}

	internal KeyboardShortcut GetShortcut(string actionId)
	{
		if (!_registrations.TryGetValue(actionId, out var value) || value.Config == null)
		{
			return new KeyboardShortcut(KeyCode.None);
		}
		return value.Config.Value;
	}

	internal string GetLabel(string actionId)
	{
		if (_registrations.TryGetValue(actionId, out var value) && value.WheelDirection != ShortcutMouseWheelDirection.None)
		{
			return PluginLocalization.Translate(value.FixedLabel);
		}
		return GetShortcut(actionId).ToString();
	}

	internal KeyboardShortcut GetDefaultShortcut(string actionId)
	{
		if (_registrations.TryGetValue(actionId, out var value) && value.Config != null)
		{
			return value.DefaultShortcut;
		}
		return new KeyboardShortcut(KeyCode.End);
	}

	internal string GetDisplayName(string actionId)
	{
		string text;
		if (!_registrations.TryGetValue(actionId, out var value))
		{
			text = actionId;
			if (text == null)
			{
				return string.Empty;
			}
		}
		else
		{
			text = value.DisplayName;
		}
		return text;
	}

	internal bool TrySetShortcut(string actionId, KeyboardShortcut shortcut, ConfigFile configFile)
	{
		if (!_registrations.TryGetValue(actionId, out var value) || value.Config == null ||
			!ShortcutInputPolicy.IsAllowedShortcut(shortcut) || configFile == null)
		{
			return false;
		}
		if (shortcut.MainKey != KeyCode.None && _registrations.Values.Any((Registration candidate) =>
			candidate.Config != null && candidate.ActionId != actionId && candidate.Config.Value.MainKey != KeyCode.None && candidate.Config.Value.Equals(shortcut)))
		{
			_log?.LogWarning($"全局快捷键重复，拒绝保存 {actionId}：{shortcut}。");
			return false;
		}
		bool saveOnConfigSet = configFile.SaveOnConfigSet;
		try
		{
			configFile.SaveOnConfigSet = false;
			value.Config.Value = shortcut;
			configFile.Save();
			return true;
		}
		catch (Exception arg)
		{
			_log?.LogWarning($"无法保存全局快捷键 {actionId}：{arg}");
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
		{
			return;
		}
		float wheelDelta = Input.mouseScrollDelta.y;
		foreach (Registration value2 in _registrations.Values)
		{
			if (value2.WheelDirection != ShortcutMouseWheelDirection.None)
			{
				if (WheelDirectionMatches(value2.WheelDirection, wheelDelta) && IsModifierPressed(value2.WheelModifier))
				{
					value2.WheelHandler?.Invoke("Unity 鼠标滚轮", Math.Max(1f, Math.Abs(wheelDelta)));
				}
				continue;
			}
			KeyboardShortcut value = value2.Config.Value;
			if (value.MainKey == KeyCode.None)
			{
				continue;
			}
			bool flag = ShortcutInputPolicy.IsUnityPressed(value);
			bool flag2 = value2.NativeDetector.IsPressed(value);
			if ((flag || flag2) && value2.Gate.TryAccept(frame, realtimeSinceStartup))
			{
				value2.Handler((flag2 && !flag) ? "Windows 原生输入" : "Unity 输入");
			}
		}
	}

	private static bool WheelDirectionMatches(ShortcutMouseWheelDirection direction, float delta)
	{
		return direction == ShortcutMouseWheelDirection.Up ? delta > 0f :
			direction == ShortcutMouseWheelDirection.Down && delta < 0f;
	}

	private static bool IsModifierPressed(KeyCode modifier)
	{
		return modifier switch
		{
			KeyCode.LeftControl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
			KeyCode.LeftAlt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
			KeyCode.LeftShift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
			KeyCode.LeftMeta => Input.GetKey(KeyCode.LeftMeta) || Input.GetKey(KeyCode.RightMeta),
			KeyCode.None => true,
			_ => Input.GetKey(modifier)
		};
	}

}
