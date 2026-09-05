using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Newtonsoft.Json;
using SDG.Framework.Water;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

// Bounded diagnostic readback. Does not alter camera geometry, masks or materials.
internal sealed class MinimapRenderCaptureProbe : MonoBehaviour
{
	private Camera _camera;
	private ManualLogSource _log;
	private string _directory;
	private string _pass;
	private bool _armed;
	private bool _captureEnabled;
	private MinimapBackgroundState _state;
	private readonly Dictionary<MinimapBackgroundState, int> _frames = new Dictionary<MinimapBackgroundState, int>();
	private readonly HashSet<MinimapBackgroundState> _attempted = new HashSet<MinimapBackgroundState>();

	internal void Initialize(Camera camera, ManualLogSource log, string directory, string pass)
	{
		_captureEnabled = string.Equals(Environment.GetEnvironmentVariable("UNTURNED_MINIMAP_CAPTURE"),
			"1", StringComparison.Ordinal);
		_camera = camera;
		_log = log;
		_directory = directory;
		_pass = pass;
	}

	internal void Arm(MinimapBackgroundState state, bool armed)
	{
		_state = state;
		_armed = _captureEnabled && armed && !_attempted.Contains(state);
	}

	private void OnPostRender()
	{
		if (!_armed || _camera == null || _camera.targetTexture == null) return;
		_frames.TryGetValue(_state, out int frames);
		_frames[_state] = ++frames;
		if (frames < 3) return; // Wait for the first scheduled frames to settle.
		_attempted.Add(_state);
		_armed = false;
		try
		{
			Directory.CreateDirectory(_directory);
			string prefix = Path.Combine(_directory, _state + "." + _pass);
			WritePng(_camera.targetTexture, prefix + ".png");
			File.WriteAllText(prefix + ".json", JsonConvert.SerializeObject(new
			{
				pass = _pass, state = _state.ToString(), unityFrame = Time.frameCount,
				position = Coordinates(_camera.transform.position),
				rotation = Coordinates(_camera.transform.eulerAngles),
				near = _camera.nearClipPlane, far = _camera.farClipPlane,
				radius = _camera.orthographicSize, mask = _camera.cullingMask,
				path = _camera.actualRenderingPath.ToString(),
				water = CaptureWater()
			}, Formatting.Indented));
			_log?.LogInfo("[Minimap] water-layer-evidence-v4 saved: " + prefix + ".png/.json");
		}
		catch (Exception exception)
		{
			_log?.LogWarning("[Minimap] bounded render-layer capture failed: " + exception);
		}
	}

	private object CaptureWater()
	{
		List<object> results = new List<object>();
		WaterVolume[] volumes = UnityEngine.Object.FindObjectsOfType<WaterVolume>();
		Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_camera);
		foreach (WaterVolume volume in volumes)
		{
			if (volume == null || volume.waterPlane == null) continue;
			Renderer[] renderers = volume.waterPlane.GetComponentsInChildren<Renderer>(true);
			foreach (Renderer renderer in renderers)
			{
				if (renderer == null) continue;
				Bounds bounds = renderer.bounds;
				Vector3 center = _camera.transform.position;
				float extent = _camera.orthographicSize * 1.415f;
				if (bounds.max.x < center.x - extent || bounds.min.x > center.x + extent ||
					bounds.max.z < center.z - extent || bounds.min.z > center.z + extent) continue;
				Material material = renderer.sharedMaterial;
				results.Add(new
				{
					volume = volume.name, surfaceVisible = volume.isSurfaceVisible,
					reflectionVisible = volume.isReflectionVisible,
					renderer = renderer.name, layer = renderer.gameObject.layer,
					active = renderer.gameObject.activeInHierarchy, enabled = renderer.enabled,
					forceOff = renderer.forceRenderingOff,
					inCameraMask = (_camera.cullingMask & (1 << renderer.gameObject.layer)) != 0,
					inFrustum = GeometryUtility.TestPlanesAABB(planes, bounds),
					center = Coordinates(renderer.bounds.center), size = Coordinates(renderer.bounds.size),
					material = material != null ? material.name : null,
					shader = material != null && material.shader != null ? material.shader.name : null,
					shaderLod = material != null && material.shader != null ? material.shader.maximumLOD : -1,
					renderQueue = material != null ? material.renderQueue : -1,
					postRenderDistortParams = ReadVector(material, "_DistortParams"),
					keywords = material != null ? material.shaderKeywords : Array.Empty<string>(),
					baseColor = ReadColor(material, "_BaseColor"),
					reflectionColor = ReadColor(material, "_ReflectionColor")
				});
				if (results.Count >= 64) return new { truncated = true, volumes = volumes.Length, samples = results };
			}
		}
		return new { truncated = false, volumes = volumes.Length, samples = results };
	}

	private static float[] Coordinates(Vector3 value) => new[] { value.x, value.y, value.z };
	private static float[] ReadVector(Material material, string property)
	{
		if (material == null || !material.HasProperty(property)) return null;
		Vector4 value = material.GetVector(property);
		return new[] { value.x, value.y, value.z, value.w };
	}
	private static float[] ReadColor(Material material, string property)
	{
		if (material == null || !material.HasProperty(property)) return null;
		Color value = material.GetColor(property);
		return new[] { value.r, value.g, value.b, value.a };
	}

	private static void WritePng(RenderTexture source, string path)
	{
		RenderTexture previous = RenderTexture.active;
		RenderTexture resolved = null;
		Texture2D pixels = null;
		try
		{
			// Resolve the MSAA source without modifying it before CPU readback.
			resolved = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(source, resolved);
			RenderTexture.active = resolved;
			pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
			pixels.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
			pixels.Apply(false, false);
			Type encoder = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", false);
			MethodInfo method = encoder?.GetMethod("EncodeToPNG", BindingFlags.Static | BindingFlags.Public,
				null, new[] { typeof(Texture2D) }, null);
			byte[] bytes = method?.Invoke(null, new object[] { pixels }) as byte[];
			if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("PNG encoder unavailable.");
			File.WriteAllBytes(path, bytes);
		}
		finally
		{
			RenderTexture.active = previous;
			if (resolved != null) RenderTexture.ReleaseTemporary(resolved);
			if (pixels != null) UnityEngine.Object.Destroy(pixels);
		}
	}
}
