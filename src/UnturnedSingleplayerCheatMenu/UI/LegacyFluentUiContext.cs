using System;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Injects only theme and the existing dynamic system-font resolver.</summary>
internal sealed class LegacyFluentUiContext
{
	private readonly Func<Font> _fontResolver;

	internal LegacyFluentUiContext(FluentTheme theme, Func<Font> fontResolver)
	{
		Theme = theme ?? throw new ArgumentNullException(nameof(theme));
		_fontResolver = fontResolver ?? throw new ArgumentNullException(nameof(fontResolver));
	}

	internal FluentTheme Theme { get; }

	internal Font ResolveFont() => _fontResolver();
}
