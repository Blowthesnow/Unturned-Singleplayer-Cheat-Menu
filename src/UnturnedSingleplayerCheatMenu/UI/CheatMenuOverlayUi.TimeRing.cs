using System;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Controls;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Core;
using UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;
using Text = UnturnedSingleplayerCheatMenu.UI.UiText;

namespace UnturnedSingleplayerCheatMenu.UI;

internal sealed partial class CheatMenuOverlayUi
{
	private const float WorldTimeRingSize = 220f;

	private LegacyFluentTimeRingControl CreateWorldTimeRing(Transform parent, float normalized, Action<float> changed)
	{
		GameObject root = CreateObject("WorldTimeRingControl", parent);
		SetLayout(root, -1f, WorldTimeRingSize, 1f, 0f);

		GameObject face = CreateObject("ClockFace", root.transform);
		RectTransform faceRect = face.GetComponent<RectTransform>();
		faceRect.anchorMin = faceRect.anchorMax = new Vector2(0.5f, 0.5f);
		faceRect.pivot = new Vector2(0.5f, 0.5f);
		faceRect.sizeDelta = new Vector2(WorldTimeRingSize, WorldTimeRingSize);
		faceRect.anchoredPosition = Vector2.zero;

		GameObject borderObject = CreateObject("Border", face.transform, typeof(LegacyFluentEllipseGraphic));
		SetOffsets(borderObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
		LegacyFluentEllipseGraphic border = borderObject.GetComponent<LegacyFluentEllipseGraphic>();
		border.color = CheatMenuStyle.Theme.HudBorder.ToUnity();
		border.raycastTarget = false;

		GameObject backgroundObject = CreateObject("Background", face.transform, typeof(LegacyFluentEllipseGraphic));
		SetOffsets(backgroundObject.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
		LegacyFluentEllipseGraphic background = backgroundObject.GetComponent<LegacyFluentEllipseGraphic>();
		background.color = CheatMenuStyle.Theme.HudBackdrop.ToUnity();
		background.raycastTarget = false;

		GameObject graphicObject = CreateObject("TimeRing", face.transform, typeof(LegacyFluentTimeRingGraphic));
		SetOffsets(graphicObject.GetComponent<RectTransform>(), 3f, 3f, 3f, 3f);
		LegacyFluentTimeRingGraphic graphic = graphicObject.GetComponent<LegacyFluentTimeRingGraphic>();
		graphic.SetPalette(
			CheatMenuStyle.Theme.HudTrack.ToUnity(),
			CheatMenuStyle.Theme.Accent.ToUnity(),
			CheatMenuStyle.Theme.Warning.ToUnity(),
			CheatMenuStyle.Theme.Info.ToUnity(),
			CheatMenuStyle.Theme.Accent.ToUnity(),
			CheatMenuStyle.Theme.HudText.ToUnity(),
			CheatMenuStyle.Theme.Focus.ToUnity());
		FluentTimeRingOptions options = new FluentTimeRingOptions
		{
			Time = WorldTimeClockMapping.GameNormalizedToClockHours(normalized),
			Step = 0.25f,
			RingThickness = 15f,
			HandleSize = 24f
		};
		graphic.Step = options.Step;
		graphic.RingThickness = options.RingThickness;
		graphic.HandleSize = options.HandleSize;
		graphic.SetTime(options.Time, notify: false);

		GameObject labelsObject = CreateObject("Labels", face.transform);
		SetOffsets(labelsObject.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
		Text timeLabel = CreateWorldTimeRingLabel(
			labelsObject.transform, "Time", string.Empty, 26, CheatMenuStyle.Theme.HudText.ToUnity(),
			TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.53f), new Vector2(180f, 46f));
		Text phaseLabel = CreateWorldTimeRingLabel(
			labelsObject.transform, "Phase", string.Empty, 12, CheatMenuStyle.Theme.HudSubtle.ToUnity(),
			TextAnchor.MiddleCenter, FontStyle.Bold, new Vector2(0.5f, 0.39f), new Vector2(180f, 24f));
		CreateWorldTimeRingHourLabels(labelsObject.transform);

		string phasePrefix = PluginLocalization.CurrentLanguage == PluginLanguage.English ? "World time" : "\u4E16\u754C\u65F6\u95F4";
		return _fluentControls.AdaptTimeRing(
			root,
			graphic,
			timeLabel,
			phaseLabel,
			FormatWorldTime,
			phasePrefix,
			FormatWorldTimePhase,
			changed);
	}

	private void CreateWorldTimeRingHourLabels(Transform parent)
	{
		const float labelRadius = 0.34f;
		for (int hour = 1; hour <= 12; hour++)
		{
			float angle = hour % 12 * 30f * Mathf.Deg2Rad;
			Vector2 anchor = new Vector2(
				0.5f + Mathf.Sin(angle) * labelRadius,
				0.5f + Mathf.Cos(angle) * labelRadius);
			CreateWorldTimeRingLabel(
				parent,
				$"Hour{hour:00}",
				hour.ToString(),
				11,
				CheatMenuStyle.Theme.HudText.ToUnity(),
				TextAnchor.MiddleCenter,
				FontStyle.Bold,
				anchor,
				new Vector2(28f, 18f));
		}
	}

	private static string FormatWorldTime(float value)
	{
		return WorldTimeClockMapping.FormatTwelveHour(
			value,
			PluginLocalization.CurrentLanguage == PluginLanguage.English);
	}

	private Text CreateWorldTimeRingLabel(
		Transform parent,
		string name,
		string value,
		int size,
		Color color,
		TextAnchor alignment,
		FontStyle style,
		Vector2 anchor,
		Vector2 dimensions)
	{
		Text label = CreateText(parent, value, size, color, alignment, style, localize: false);
		label.gameObject.name = name;
		RectTransform rect = label.rectTransform;
		rect.anchorMin = rect.anchorMax = anchor;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = dimensions;
		rect.anchoredPosition = Vector2.zero;
		return label;
	}

	private static string FormatWorldTimePhase(float value)
	{
		float hours = Mathf.Repeat(value, 24f);
		bool english = PluginLocalization.CurrentLanguage == PluginLanguage.English;
		if (hours < 5f) return english ? "Late night" : "\u6DF1\u591C";
		if (hours < 7f) return english ? "Dawn" : "\u9ECE\u660E";
		if (hours < 11f) return english ? "Morning" : "\u6E05\u6668";
		if (hours < 14f) return english ? "Noon" : "\u6B63\u5348";
		if (hours < 18f) return english ? "Afternoon" : "\u5348\u540E";
		if (hours < 20f) return english ? "Dusk" : "\u9EC4\u660F";
		return english ? "Night" : "\u591C\u95F4";
	}
}
