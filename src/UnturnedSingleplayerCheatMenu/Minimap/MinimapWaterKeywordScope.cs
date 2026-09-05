using System;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapWaterKeywordScope
{
	private bool _captured;
	private bool _on;
	private bool _off;
	private bool _logged;
	private bool _reportedFailure;
	private ManualLogSource _log;

	internal void Initialize(ManualLogSource log) => _log = log;

	internal bool Begin()
	{
		// A failed restoration retains the original receipt. Never capture over it.
		Restore();
		if (_captured) return false;
		try
		{
			_on = Shader.IsKeywordEnabled("WATER_EDGEBLEND_ON");
			_off = Shader.IsKeywordEnabled("WATER_EDGEBLEND_OFF");
			_captured = true;
			Shader.DisableKeyword("WATER_EDGEBLEND_ON");
			Shader.EnableKeyword("WATER_EDGEBLEND_OFF");
			if (!_logged)
			{
				_logged = true;
				_log?.LogInfo($"[Minimap] water-edge-scope-v3: originalOn={_on}, originalOff={_off}, capture=off.");
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
		try
		{
			if (_on) Shader.EnableKeyword("WATER_EDGEBLEND_ON");
			else Shader.DisableKeyword("WATER_EDGEBLEND_ON");
			if (_off) Shader.EnableKeyword("WATER_EDGEBLEND_OFF");
			else Shader.DisableKeyword("WATER_EDGEBLEND_OFF");
			_captured = false;
		}
		catch (Exception exception) { Report(exception); }
	}

	private void Report(Exception exception)
	{
		if (_reportedFailure) return;
		_reportedFailure = true;
		_log?.LogWarning("[Minimap] water keyword capture/restore failed: " + exception.Message);
	}
}
