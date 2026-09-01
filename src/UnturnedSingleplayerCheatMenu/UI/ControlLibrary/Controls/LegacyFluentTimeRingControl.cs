using System;
using UnityEngine;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;

/// <summary>Value, label, callback, and lifetime contract for the Legacy uGUI world-time ring.</summary>
internal sealed class LegacyFluentTimeRingControl : LegacyFluentControlHandle
{
	private readonly Action<float> _graphicListener;
	private readonly Func<float, string> _timeFormatter;
	private readonly Func<float, string> _phaseFormatter;
	private readonly string _phasePrefix;

	internal LegacyFluentTimeRingControl(
		GameObject root,
		LegacyFluentTimeRingGraphic graphic,
		Text timeLabel,
		Text phaseLabel,
		Func<float, string> timeFormatter,
		string phasePrefix,
		Func<float, string> phaseFormatter)
		: base(root, surface: null)
	{
		Graphic = graphic ?? throw new ArgumentNullException(nameof(graphic));
		TimeLabel = timeLabel ?? throw new ArgumentNullException(nameof(timeLabel));
		PhaseLabel = phaseLabel ?? throw new ArgumentNullException(nameof(phaseLabel));
		_timeFormatter = timeFormatter ?? throw new ArgumentNullException(nameof(timeFormatter));
		_phasePrefix = phasePrefix ?? string.Empty;
		_phaseFormatter = phaseFormatter ?? throw new ArgumentNullException(nameof(phaseFormatter));
		_graphicListener = HandleTimeChanged;
		Graphic.TimeChanged += _graphicListener;
		AddCleanup(delegate
		{
			Graphic.TimeChanged -= _graphicListener;
			TimeChanged = null;
		});
		RefreshLabels();
	}

	internal LegacyFluentTimeRingGraphic Graphic { get; }

	internal Text TimeLabel { get; }

	internal Text PhaseLabel { get; }

	internal float Time => Graphic.Time;

	internal bool IsInteracting => Graphic.IsDragging;

	internal event Action<float> TimeChanged;

	internal void SetTime(float value, bool notify = true)
	{
		Graphic.SetTime(value, notify);
		if (!notify)
		{
			RefreshLabels();
		}
	}

	private void HandleTimeChanged(float value)
	{
		RefreshLabels();
		TimeChanged?.Invoke(value);
	}

	private void RefreshLabels()
	{
		TimeLabel.text = _timeFormatter(Graphic.Time);
		string phase = _phaseFormatter(Graphic.Time);
		PhaseLabel.text = string.IsNullOrWhiteSpace(_phasePrefix) ? phase : _phasePrefix + "  ·  " + phase;
	}
}
