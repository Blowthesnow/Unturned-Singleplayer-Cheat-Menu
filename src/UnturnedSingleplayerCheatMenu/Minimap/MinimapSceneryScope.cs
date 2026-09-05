using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

// A render-callback scope, not a frame-wide mutation. Synchronous reflection
// sub-renders inherit the parent capture's scenery selection.
internal sealed class MinimapSceneryScope : MonoBehaviour
{
	private readonly struct SavedState
	{
		internal readonly Renderer Renderer;
		internal readonly GameObject GameObject;
		internal readonly int Layer;
		internal readonly bool ForceOff;
		internal SavedState(Renderer renderer)
		{
			Renderer = renderer;
			GameObject = renderer.gameObject;
			Layer = renderer.gameObject.layer;
			ForceOff = renderer.forceRenderingOff;
		}
	}

	private readonly List<SavedState> _saved = new List<SavedState>();
	private readonly HashSet<int> _seen = new HashSet<int>();
	private IReadOnlyList<Renderer> _renderers;
	private ManualLogSource _log;
	private bool _terrainPass;
	private int _terrainLayer;
	private bool _armed;
	private bool _logged;
	private bool _reportedFailure;

	internal void Initialize(ManualLogSource log, bool terrainPass, int terrainLayer)
	{
		if (terrainPass && (terrainLayer < 0 || terrainLayer > 31))
			throw new ArgumentOutOfRangeException(nameof(terrainLayer));
		_log = log;
		_terrainPass = terrainPass;
		_terrainLayer = terrainLayer;
	}

	internal void Arm(IReadOnlyList<Renderer> renderers, bool armed)
	{
		Restore();
		_renderers = renderers;
		_armed = armed && _saved.Count == 0;
	}

	private void OnPreCull()
	{
		if (!_armed || _saved.Count != 0 || _renderers == null) return;
		_seen.Clear();
		int batched = 0;
		try
		{
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null || !renderer.gameObject.activeInHierarchy ||
					!renderer.enabled || renderer.forceRenderingOff) continue;
				// Several renderer components may share one GameObject/layer.
				int key = _terrainPass ? renderer.gameObject.GetInstanceID() : renderer.GetInstanceID();
				if (!_seen.Add(key)) continue;
				_saved.Add(new SavedState(renderer));
				if (renderer.isPartOfStaticBatch) batched++;
				if (_terrainPass) renderer.gameObject.layer = _terrainLayer;
				else renderer.forceRenderingOff = true;
			}
			if (!_logged && _saved.Count > 0)
			{
				_logged = true;
				_log?.LogInfo($"[Minimap] scenery-scope-v3: pass={(_terrainPass ? "terrain" : "roads")}, " +
					$"selected={_saved.Count}, staticBatch={batched}.");
			}
		}
		catch (Exception exception)
		{
			_armed = false;
			ReportFailure(exception);
			Restore();
		}
	}

	private void OnPostRender() => Restore();
	private void OnDisable() { _armed = false; Restore(); }
	private void OnDestroy() { _armed = false; Restore(); }

	private void Restore()
	{
		for (int i = _saved.Count - 1; i >= 0; i--)
		{
			SavedState state = _saved[i];
			try
			{
				if (_terrainPass)
				{
					if (state.GameObject != null) state.GameObject.layer = state.Layer;
				}
				else if (state.Renderer != null) state.Renderer.forceRenderingOff = state.ForceOff;
				_saved.RemoveAt(i);
			}
			catch (Exception exception)
			{
				_armed = false;
				ReportFailure(exception);
				// Retain failed receipts for OnDisable/next-frame retry.
			}
		}
	}

	private void ReportFailure(Exception exception)
	{
		if (_reportedFailure) return;
		_reportedFailure = true;
		_log?.LogWarning("[Minimap] scenery capture/restore failed: " + exception.Message);
	}
}
