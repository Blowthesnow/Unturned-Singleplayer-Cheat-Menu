using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnturnedSingleplayerCheatMenu.Minimap;

/// <summary>
/// Temporarily removes scene darkness from a realtime minimap camera pass without
/// replacing native materials. The receipt is shared by both cameras so a failed
/// restore cannot be overwritten by the following pass.
/// </summary>
internal sealed class MinimapUnshadowedCaptureScope
{
	private ManualLogSource _log;
	private bool _captured;
	private bool _logged;
	private bool _reported;
	private bool _fog;
	private AmbientMode _ambientMode;
	private Color _ambientSkyColor;
	private Color _ambientEquatorColor;
	private Color _ambientGroundColor;
	private float _ambientIntensity;
	private Color _subtractiveShadowColor;
	private ShadowQuality _shadows;
	private float _shadowDistance;

	internal void Initialize(ManualLogSource log) => _log = log;

	internal bool Begin(Color ambientFill)
	{
		if (_captured) return false;
		try
		{
			_fog = RenderSettings.fog;
			_ambientMode = RenderSettings.ambientMode;
			_ambientSkyColor = RenderSettings.ambientSkyColor;
			_ambientEquatorColor = RenderSettings.ambientEquatorColor;
			_ambientGroundColor = RenderSettings.ambientGroundColor;
			_ambientIntensity = RenderSettings.ambientIntensity;
			_subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
			_shadows = QualitySettings.shadows;
			_shadowDistance = QualitySettings.shadowDistance;
			_captured = true;

			RenderSettings.fog = false;
			RenderSettings.ambientMode = AmbientMode.Trilight;
			RenderSettings.ambientSkyColor = ambientFill;
			RenderSettings.ambientEquatorColor = ambientFill;
			RenderSettings.ambientGroundColor = ambientFill;
			RenderSettings.ambientIntensity = 1f;
			RenderSettings.subtractiveShadowColor = ambientFill;
			QualitySettings.shadows = ShadowQuality.Disable;
			QualitySettings.shadowDistance = 0f;

			if (!_logged)
			{
				_logged = true;
				_log?.LogInfo("[Minimap] unshadowed-capture-v1: shadows and fog disabled with uniform ambient fill; native state is restored after each pass.");
			}
			return true;
		}
		catch (Exception exception)
		{
			Report(exception);
			Restore();
			return false;
		}
	}

	internal void Restore()
	{
		if (!_captured) return;
		bool restored = true;
		try { QualitySettings.shadows = _shadows; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { QualitySettings.shadowDistance = _shadowDistance; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.ambientGroundColor = _ambientGroundColor; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.subtractiveShadowColor = _subtractiveShadowColor; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.ambientIntensity = _ambientIntensity; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.ambientEquatorColor = _ambientEquatorColor; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.ambientSkyColor = _ambientSkyColor; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.ambientMode = _ambientMode; }
		catch (Exception exception) { restored = false; Report(exception); }
		try { RenderSettings.fog = _fog; }
		catch (Exception exception) { restored = false; Report(exception); }
		if (restored) _captured = false;
	}

	private void Report(Exception exception)
	{
		if (_reported) return;
		_reported = true;
		_log?.LogWarning("[Minimap] unshadowed capture/restore failed: " + exception.Message);
	}
}
