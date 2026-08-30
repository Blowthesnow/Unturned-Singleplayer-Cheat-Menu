using System;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapSceneRenderer : IDisposable
{
	internal const int DefaultResolution = 512;
	private readonly ManualLogSource _log;
	private GameObject _root;
	private Camera _terrainCamera;
	private Camera _objectCamera;
	private RenderTexture _terrainTexture;
	private RenderTexture _objectTexture;
	private Shader _replacementShader;
	private int _loggedScheduledStates;
	private bool _failed;

	public Texture TerrainTexture => _terrainTexture;
	public Texture ObjectTexture => _objectTexture;
	public bool IsAvailable => !_failed;

	public MinimapSceneRenderer(ManualLogSource log)
	{
		_log = log;
		try
		{
			Initialize(DefaultResolution);
		}
		catch (Exception exception)
		{
			_failed = true;
			_log?.LogWarning("[Minimap] conditional camera initialization failed; grid/native fallback will be used.\n" + exception);
			Dispose();
		}
	}

	public bool Render(Vector3 center, float radius, float mapYaw, MinimapBackgroundState state,
		bool renderLoadedBuildings, float sliceAbove, float sliceBelow)
	{
		if (_failed || _terrainCamera == null || _objectCamera == null)
		{
			return false;
		}
		try
		{
			if (!EnsureRenderTarget(_terrainTexture) || !EnsureRenderTarget(_objectTexture))
			{
				throw new InvalidOperationException("Realtime minimap render targets could not be created.");
			}
			MinimapRenderCenter snapped = MinimapRenderPolicy.SnapCenter(
				center.x, center.z, mapYaw, radius, DefaultResolution);
			Vector3 renderCenter = new Vector3(snapped.X, center.y, snapped.Z);
			ConfigureCamera(_terrainCamera, renderCenter, radius, mapYaw, state, sliceAbove, sliceBelow);
			ConfigureCamera(_objectCamera, renderCenter, radius, mapYaw, state, sliceAbove, sliceBelow);
			bool renderTerrain = MinimapRenderPolicy.UsesSceneCameras(state);
			bool renderObjects = MinimapRenderPolicy.ShowsLoadedObjects(state, renderLoadedBuildings);
			_terrainCamera.enabled = renderTerrain;
			_objectCamera.enabled = renderObjects;
			int stateBit = 1 << (int)state;
			if ((_loggedScheduledStates & stateBit) == 0)
			{
				_loggedScheduledStates |= stateBit;
				_log?.LogInfo($"[Minimap] scheduled scene cameras for {state}; terrain={renderTerrain}, objects={renderObjects}.");
			}
			return true;
		}
		catch (Exception exception)
		{
			BeginFrame();
			_failed = true;
			_log?.LogWarning("[Minimap] conditional camera render failed and has been disabled for this session.\n" + exception);
			return false;
		}
	}

	private void Initialize(int resolution)
	{
		_replacementShader = Shader.Find("Unlit/Texture");
		_root = new GameObject("UnturnedSingleplayerCheatMenu.MinimapCameras");
		_root.hideFlags = HideFlags.HideAndDontSave;
		UnityEngine.Object.DontDestroyOnLoad(_root);
		_terrainTexture = CreateRenderTexture("Minimap.Terrain", resolution);
		_objectTexture = CreateRenderTexture("Minimap.Objects", resolution);
		_terrainCamera = CreateCamera("TerrainCamera", _terrainTexture,
			RayMasks.GROUND | RayMasks.GROUND2, new Color(0.18f, 0.22f, 0.18f, 1f));
		_objectCamera = CreateCamera("ObjectCamera", _objectTexture,
			RayMasks.RESOURCE | RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL |
			RayMasks.ENVIRONMENT | RayMasks.BARRICADE | RayMasks.STRUCTURE, Color.clear);
		ApplyCompatibilityShader(_terrainCamera);
		ApplyCompatibilityShader(_objectCamera);
		if (_replacementShader == null)
		{
			_log?.LogInfo("[Minimap] optional compatibility shader is unavailable; native Unturned materials will be used.");
		}
	}

	private Camera CreateCamera(string name, RenderTexture target, int mask, Color clearColor)
	{
		GameObject cameraObject = new GameObject(name, typeof(Camera));
		cameraObject.hideFlags = HideFlags.HideAndDontSave;
		cameraObject.transform.SetParent(_root.transform, false);
		Camera camera = cameraObject.GetComponent<Camera>();
		camera.enabled = false;
		camera.orthographic = true;
		camera.allowHDR = false;
		camera.allowMSAA = true;
		camera.useOcclusionCulling = false;
		camera.renderingPath = RenderingPath.Forward;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = clearColor;
		camera.cullingMask = mask;
		camera.targetTexture = target;
		return camera;
	}

	private void ApplyCompatibilityShader(Camera camera)
	{
		if (_replacementShader != null)
		{
			camera.SetReplacementShader(_replacementShader, string.Empty);
		}
	}

	internal void BeginFrame()
	{
		if (_terrainCamera != null) _terrainCamera.enabled = false;
		if (_objectCamera != null) _objectCamera.enabled = false;
	}

	private static bool EnsureRenderTarget(RenderTexture texture)
	{
		if (texture == null) return false;
		if (!texture.IsCreated()) texture.Create();
		return texture.IsCreated();
	}

	private static RenderTexture CreateRenderTexture(string name, int resolution)
	{
		RenderTexture texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
		{
			name = name,
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave,
			antiAliasing = 2
		};
		texture.Create();
		return texture;
	}

	private static void ConfigureCamera(Camera camera, Vector3 center, float radius, float mapYaw,
		MinimapBackgroundState state, float sliceAbove, float sliceBelow)
	{
		camera.orthographicSize = radius;
		camera.aspect = 1f;
		camera.transform.rotation = Quaternion.Euler(0f, mapYaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
		if (state == MinimapBackgroundState.RealtimeHeightSlice)
		{
			camera.transform.position = center + Vector3.up * sliceAbove;
			camera.nearClipPlane = 0.05f;
			camera.farClipPlane = Mathf.Max(0.1f, sliceAbove + sliceBelow);
		}
		else
		{
			camera.transform.position = center + Vector3.up * 768f;
			camera.nearClipPlane = 1f;
			camera.farClipPlane = 1536f;
		}
	}

	public void Dispose()
	{
		BeginFrame();
		if (_terrainCamera != null)
		{
			_terrainCamera.ResetReplacementShader();
			_terrainCamera.targetTexture = null;
		}
		if (_objectCamera != null)
		{
			_objectCamera.ResetReplacementShader();
			_objectCamera.targetTexture = null;
		}
		ReleaseTexture(ref _terrainTexture);
		ReleaseTexture(ref _objectTexture);
		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
			_root = null;
		}
		_terrainCamera = null;
		_objectCamera = null;
		_replacementShader = null;
	}

	private static void ReleaseTexture(ref RenderTexture texture)
	{
		if (texture == null) return;
		texture.Release();
		UnityEngine.Object.Destroy(texture);
		texture = null;
	}
}
