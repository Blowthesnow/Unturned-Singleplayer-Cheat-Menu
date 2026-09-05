using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapWaterOpacityScope
{
	private const string Property = "_DistortParams";
	private readonly struct Receipt
	{
		internal readonly Material Material;
		internal readonly Vector4 Value;
		internal Receipt(Material material, Vector4 value) { Material = material; Value = value; }
	}
	private readonly List<Receipt> _saved = new List<Receipt>();
	private readonly HashSet<int> _seen = new HashSet<int>();
	private ManualLogSource _log;
	private bool _logged;
	private bool _reported;

	internal void Initialize(ManualLogSource log) => _log = log;

	internal bool Begin(IReadOnlyList<Material> materials)
	{
		Restore();
		if (_saved.Count != 0) return false;
		_seen.Clear();
		try
		{
			for (int i = 0; materials != null && i < materials.Count; i++)
			{
				Material material = materials[i];
				if (material == null || material.shader == null || !material.HasProperty(Property) ||
					!_seen.Add(material.GetInstanceID())) continue;
				Vector4 original = material.GetVector(Property);
				float bias = MinimapWaterOpacityPolicy.ResolveCaptureBias(material.shader.name,
					material.shader.maximumLOD, original.w);
				if (bias == original.w || float.IsNaN(bias)) continue;
				_saved.Add(new Receipt(material, original));
				Vector4 capture = original;
				capture.w = bias;
				material.SetVector(Property, capture);
				if (!_logged && _saved.Count == 1)
					_log?.LogInfo($"[Minimap] water-opacity-v5 first material: shader={material.shader.name}, " +
						$"lod={material.shader.maximumLOD}, bias={original.w:R}->{bias:R}.");
			}
			if (!_logged && materials != null && materials.Count > 0)
			{
				_logged = true;
				_log?.LogInfo($"[Minimap] water-opacity-v5: examined={_seen.Count}, adjusted={_saved.Count}, " +
					"lowQualityFresnelBiasFloor=0.25; color properties/textures unchanged.");
			}
			return true;
		}
		catch (Exception exception) { Report(exception); Restore(); return false; }
	}

	internal void Restore()
	{
		for (int i = _saved.Count - 1; i >= 0; i--)
		{
			Receipt receipt = _saved[i];
			try
			{
				if (receipt.Material != null) receipt.Material.SetVector(Property, receipt.Value);
				_saved.RemoveAt(i);
			}
			catch (Exception exception) { Report(exception); }
		}
	}

	private void Report(Exception exception)
	{
		if (_reported) return;
		_reported = true;
		_log?.LogWarning("[Minimap] water opacity capture/restore failed: " + exception.Message);
	}
}
