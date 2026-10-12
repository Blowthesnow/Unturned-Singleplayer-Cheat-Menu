using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using SDG.Unturned;
using SDG.Framework.Water;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapSceneRenderer : IDisposable
{
	private sealed class MinimapCaptureLightingBridge : MonoBehaviour
	{
		private MinimapUnshadowedCaptureScope _scope;
		private bool _armed;

		internal void Initialize(ManualLogSource log, MinimapUnshadowedCaptureScope scope)
		{
			_scope = scope;
			_scope.Initialize(log);
		}

		internal void SetArmed(bool armed)
		{
			_armed = armed;
			if (!armed) _scope?.Restore();
		}

		private void OnPreCull()
		{
			if (_armed) _scope?.Begin(Palette.AMBIENT);
		}

		private void OnPostRender() => _scope?.Restore();

		private void OnDisable()
		{
			_armed = false;
			_scope?.Restore();
		}

		private void OnDestroy()
		{
			_armed = false;
			_scope?.Restore();
		}
	}

	private sealed class MinimapMainCameraIdentityBridge : MonoBehaviour
	{
		private sealed class TerrainQualityState
		{
			internal object Terrain;
			internal float BasemapDistance;
			internal float HeightmapPixelError;
			internal Material OriginalMaterial;
			internal bool MaterialChanged;
		}

		private static PropertyInfo _activeTerrainsProperty;
		private static PropertyInfo _basemapDistanceProperty;
		private static PropertyInfo _heightmapPixelErrorProperty;
		private static PropertyInfo _materialTemplateProperty;
		private Material _forwardTerrainMaterial;
		private Material _deferredTerrainMaterial;
		private ManualLogSource _log;
		private bool _loggedMaterialCapture;
		private bool _loggedCaptureFailure;
		private readonly List<TerrainQualityState> _terrainStates =
			new List<TerrainQualityState>();
		private bool _armed;
		private bool _claimed;
		private bool _terrainCaptureActive;
		private bool _hasSeaState;
		private bool _reflectionSubscribed;
		private string _previousTag = "Untagged";
		private float _lodBias;
		private float _seaShininess;
		private Color _seaSpecularColor;
		private readonly MinimapWaterKeywordScope _waterKeywords = new MinimapWaterKeywordScope();
		private readonly MinimapWaterOpacityScope _waterOpacity = new MinimapWaterOpacityScope();
		private IReadOnlyList<Material> _waterMaterials;
		internal void SetWaterMaterials(IReadOnlyList<Material> materials) => _waterMaterials = materials;

		internal void Initialize(ManualLogSource log)
		{
			_log = log;
			_waterKeywords.Initialize(log);
			_waterOpacity.Initialize(log);
			_forwardTerrainMaterial = Resources.Load<Material>("Materials/Landscapes/Landscape_Forward");
			_deferredTerrainMaterial = Resources.Load<Material>("Materials/Landscapes/Landscape_Deferred");
		}

		internal void SetArmed(bool armed)
		{
			_armed = armed;
			if (!armed)
			{
				EndTerrainCapture();
				ReleaseIdentity();
			}
		}

		private void OnPreCull()
		{
			if (!_armed || _claimed) return;
			_previousTag = gameObject.tag;
			if (!gameObject.CompareTag("MainCamera")) gameObject.tag = "MainCamera";
			_claimed = true;
			BeginTerrainCapture();
		}

		private void OnPostRender()
		{
			EndTerrainCapture();
			ReleaseIdentity();
		}

		private void OnDisable()
		{
			EndTerrainCapture();
			ReleaseIdentity();
		}

		private void OnDestroy()
		{
			EndTerrainCapture();
			ReleaseIdentity();
		}

		private void BeginTerrainCapture()
		{
			if (_terrainCaptureActive) return;
			if (!_waterKeywords.Begin()) return;
			if (!_waterOpacity.Begin(_waterMaterials)) { _waterKeywords.Restore(); return; }
			_lodBias = QualitySettings.lodBias;
			_terrainCaptureActive = true;
			try
			{
				QualitySettings.lodBias = float.MaxValue;
				try
				{
					_seaShininess = LevelLighting.getSeaFloat("_Shininess");
					_seaSpecularColor = LevelLighting.getSeaColor("_SpecularColor");
					_hasSeaState = true;
					LevelLighting.setSeaFloat("_Shininess", 500f);
					LevelLighting.setSeaColor("_SpecularColor", Color.black);
				}
				catch (Exception)
				{
					_hasSeaState = false;
				}
				BeginTerrainQualityCapture();
				PlanarReflection.postRender += HandlePlanarReflectionPostRender;
				_reflectionSubscribed = true;
			}
			catch (Exception)
			{
				EndTerrainCapture();
			}
		}

		private void BeginTerrainQualityCapture()
		{
			if (!TryResolveTerrainProperties())
			{
				LogCaptureFailure("Terrain capture properties are unavailable; material/LOD correction was skipped.");
				return;
			}
			Array terrains = _activeTerrainsProperty.GetValue(null, null) as Array;
			if (terrains == null) return;
			int swappedMaterials = 0;
			foreach (object terrain in terrains)
			{
				if (terrain == null) continue;
				try
				{
					float basemapDistance = Convert.ToSingle(
						_basemapDistanceProperty.GetValue(terrain, null));
					float heightmapPixelError = Convert.ToSingle(
						_heightmapPixelErrorProperty.GetValue(terrain, null));
					Material originalMaterial = _materialTemplateProperty.GetValue(terrain, null) as Material;
					TerrainQualityState state = new TerrainQualityState
					{
						Terrain = terrain,
						BasemapDistance = basemapDistance,
						HeightmapPixelError = heightmapPixelError,
						OriginalMaterial = originalMaterial
					};
					_terrainStates.Add(state);
					// Orthographic cameras cannot consume the game's Deferred terrain pass.
					// Swap only the known shared native material, never custom map materials.
					if (_deferredTerrainMaterial != null && originalMaterial == _deferredTerrainMaterial)
					{
						if (_forwardTerrainMaterial != null && _forwardTerrainMaterial.shader != null &&
							_forwardTerrainMaterial.shader.isSupported)
						{
							state.MaterialChanged = true;
							_materialTemplateProperty.SetValue(terrain, _forwardTerrainMaterial, null);
							swappedMaterials++;
						}
						else LogCaptureFailure("Native Forward terrain material is missing or unsupported; Deferred terrain was left unchanged.");
					}
					_basemapDistanceProperty.SetValue(terrain, 8192f, null);
					_heightmapPixelErrorProperty.SetValue(terrain, 1f, null);
				}
				catch (Exception exception)
				{
					LogCaptureFailure("Terrain capture setup failed: " + exception.Message);
				}
			}
			if (!_loggedMaterialCapture && terrains.Length > 0)
			{
				_loggedMaterialCapture = true;
				_log?.LogInfo($"[Minimap] terrain-material-scope-v1: active={terrains.Length}, captured={_terrainStates.Count}, " +
					$"swapped={swappedMaterials}, gameMode={SDG.Unturned.GraphicsSettings.renderMode}, " +
					$"forward={_forwardTerrainMaterial != null}, deferred={_deferredTerrainMaterial != null}.");
			}
		}

		private void LogCaptureFailure(string message)
		{
			if (_loggedCaptureFailure) return;
			_loggedCaptureFailure = true;
			_log?.LogWarning("[Minimap] " + message);
		}

		private void HandlePlanarReflectionPostRender()
		{
			for (int index = 0; index < _terrainStates.Count; index++)
			{
				TerrainQualityState state = _terrainStates[index];
				try
				{
					_basemapDistanceProperty?.SetValue(state.Terrain, 8192f, null);
					_heightmapPixelErrorProperty?.SetValue(state.Terrain, 1f, null);
				}
				catch (Exception)
				{
				}
			}
		}

		private static bool TryResolveTerrainProperties()
		{
			if (_activeTerrainsProperty != null && _basemapDistanceProperty != null &&
				_heightmapPixelErrorProperty != null && _materialTemplateProperty != null) return true;
			Type terrainType = Type.GetType("UnityEngine.Terrain, UnityEngine.TerrainModule", false);
			if (terrainType == null) return false;
			_activeTerrainsProperty = terrainType.GetProperty("activeTerrains",
				BindingFlags.Public | BindingFlags.Static);
			_basemapDistanceProperty = terrainType.GetProperty("basemapDistance",
				BindingFlags.Public | BindingFlags.Instance);
			_heightmapPixelErrorProperty = terrainType.GetProperty("heightmapPixelError",
				BindingFlags.Public | BindingFlags.Instance);
			_materialTemplateProperty = terrainType.GetProperty("materialTemplate",
				BindingFlags.Public | BindingFlags.Instance);
			return _activeTerrainsProperty != null && _basemapDistanceProperty != null &&
				_heightmapPixelErrorProperty != null && _materialTemplateProperty != null;
		}

		private void EndTerrainCapture()
		{
			_waterOpacity.Restore();
			_waterKeywords.Restore();
			if (!_terrainCaptureActive) return;
			if (_reflectionSubscribed)
			{
				PlanarReflection.postRender -= HandlePlanarReflectionPostRender;
				_reflectionSubscribed = false;
			}
			for (int index = _terrainStates.Count - 1; index >= 0; index--)
			{
				TerrainQualityState state = _terrainStates[index];
				if (state.MaterialChanged)
				{
					try { _materialTemplateProperty.SetValue(state.Terrain, state.OriginalMaterial, null); }
					catch (Exception exception) { LogCaptureFailure("Terrain material restore failed: " + exception.Message); }
				}
				try
				{
					_basemapDistanceProperty?.SetValue(state.Terrain, state.BasemapDistance, null);
					_heightmapPixelErrorProperty?.SetValue(state.Terrain,
						state.HeightmapPixelError, null);
				}
				catch (Exception)
				{
				}
			}
			_terrainStates.Clear();
			if (_hasSeaState)
			{
				try
				{
					LevelLighting.setSeaFloat("_Shininess", _seaShininess);
					LevelLighting.setSeaColor("_SpecularColor", _seaSpecularColor);
				}
				catch (Exception)
				{
				}
			}
			try
			{
				QualitySettings.lodBias = _lodBias;
			}
			catch (Exception)
			{
			}
			_hasSeaState = false;
			_terrainCaptureActive = false;
		}

		private void ReleaseIdentity()
		{
			if (!_claimed) return;
			try { gameObject.tag = _previousTag; }
			catch (Exception) { }
			_claimed = false;
			_previousTag = "Untagged";
		}
	}

	internal const int DefaultResolution = 512;
	private static readonly float[] NoLayerCullDistances = new float[32];
	private readonly ManualLogSource _log;
	private GameObject _root;
	private Camera _terrainCamera;
	private Camera _objectCamera;
	private MinimapMainCameraIdentityBridge _terrainIdentityBridge;
	private MinimapCaptureLightingBridge _terrainLightingBridge;
	private MinimapCaptureLightingBridge _objectLightingBridge;
	private readonly MinimapUnshadowedCaptureScope _captureLighting =
		new MinimapUnshadowedCaptureScope();
	private MinimapSceneryScope _terrainSceneryScope;
	private MinimapSceneryScope _objectSceneryScope;
	private MinimapRenderCaptureProbe _terrainCaptureProbe;
	private MinimapRenderCaptureProbe _objectCaptureProbe;
	private readonly List<Renderer> _cliffRenderers = new List<Renderer>();
	private readonly List<Renderer> _rendererScratch = new List<Renderer>();
	private readonly List<Material> _waterMaterials = new List<Material>();
	private List<LevelObject>[,] _sceneryRegions;
	private float _nextSceneryRefresh;
	private Vector3 _sceneryCenter;
	private float _sceneryRadius;
	private RenderTexture _terrainTexture;
	private RenderTexture _objectTexture;
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
			RefreshScenery(center, radius);
			_terrainIdentityBridge.SetWaterMaterials(_waterMaterials);
			Camera playerCamera = MainCamera.instance;
			Color terrainClear = MinimapRenderPolicy.UsesTransparentTerrainClear(state)
				? Color.clear
				: new Color(0.18f, 0.22f, 0.18f, 1f);
			ConfigureCamera(_terrainCamera, playerCamera, renderCenter, radius, mapYaw, state,
				sliceAbove, sliceBelow, RayMasks.WATER | RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.RESOURCE,
				terrainClear, 100f, MinimapSceneCameraPass.Terrain);
			ConfigureCamera(_objectCamera, playerCamera, renderCenter, radius, mapYaw, state,
				sliceAbove, sliceBelow,
				RayMasks.DEFAULT | RayMasks.TRANSPARENT_FX |
				RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL |
				RayMasks.ENVIRONMENT | RayMasks.BARRICADE | RayMasks.STRUCTURE,
				Color.clear, 101f, MinimapSceneCameraPass.Objects);
			bool renderTerrain = MinimapRenderPolicy.UsesSceneCameras(state);
			bool renderObjects = MinimapRenderPolicy.ShowsLoadedObjects(state, renderLoadedBuildings);
			_terrainIdentityBridge.SetArmed(renderTerrain);
			_terrainLightingBridge.SetArmed(renderTerrain);
			_objectLightingBridge.SetArmed(renderObjects);
			_terrainSceneryScope.Arm(_cliffRenderers, renderTerrain);
			_objectSceneryScope.Arm(_cliffRenderers, renderObjects);
			_terrainCaptureProbe.Arm(state, renderTerrain);
			_objectCaptureProbe.Arm(state, renderObjects);
			_terrainCamera.enabled = renderTerrain;
			_objectCamera.enabled = renderObjects;
			int stateBit = 1 << (int)state;
			if ((_loggedScheduledStates & stateBit) == 0)
			{
				_loggedScheduledStates |= stateBit;
				_log?.LogInfo($"[Minimap] scheduled scene cameras for {state}; terrain={renderTerrain}, objects={renderObjects}.");
				_log?.LogInfo($"[Minimap] native-materials-v1; playerY={center.y:F1}, radius={radius:F1}, " +
					$"terrainY={_terrainCamera.transform.position.y:F1}, near={_terrainCamera.nearClipPlane:F2}, " +
					$"far={_terrainCamera.farClipPlane:F1}, terrainMask={_terrainCamera.cullingMask}, " +
					$"objectsMask={_objectCamera.cullingMask}, path={_terrainCamera.actualRenderingPath}.");
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
		_root = new GameObject("UnturnedSingleplayerCheatMenu.MinimapCameras");
		_root.hideFlags = HideFlags.HideAndDontSave;
		UnityEngine.Object.DontDestroyOnLoad(_root);
		_terrainTexture = CreateRenderTexture("Minimap.Terrain", resolution);
		_objectTexture = CreateRenderTexture("Minimap.Objects", resolution);
		_terrainCamera = CreateCamera("TerrainCamera", _terrainTexture,
			RayMasks.WATER | RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.RESOURCE,
			new Color(0.18f, 0.22f, 0.18f, 1f));
		_terrainIdentityBridge = _terrainCamera.gameObject.AddComponent<MinimapMainCameraIdentityBridge>();
		_terrainIdentityBridge.Initialize(_log);
		_terrainLightingBridge = _terrainCamera.gameObject.AddComponent<MinimapCaptureLightingBridge>();
		_terrainLightingBridge.Initialize(_log, _captureLighting);
		_terrainSceneryScope = _terrainCamera.gameObject.AddComponent<MinimapSceneryScope>();
		_terrainSceneryScope.Initialize(_log, true, LayerMasks.GROUND2);
		_objectCamera = CreateCamera("ObjectCamera", _objectTexture,
			RayMasks.DEFAULT | RayMasks.TRANSPARENT_FX |
			RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL |
			RayMasks.ENVIRONMENT | RayMasks.BARRICADE | RayMasks.STRUCTURE, Color.clear);
		_objectSceneryScope = _objectCamera.gameObject.AddComponent<MinimapSceneryScope>();
		_objectSceneryScope.Initialize(_log, false, 0);
		_objectLightingBridge = _objectCamera.gameObject.AddComponent<MinimapCaptureLightingBridge>();
		_objectLightingBridge.Initialize(_log, _captureLighting);
		string captureDirectory = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath,
			"diagnostics", "minimap-trees-v7-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
		_terrainCaptureProbe = _terrainCamera.gameObject.AddComponent<MinimapRenderCaptureProbe>();
		_terrainCaptureProbe.Initialize(_terrainCamera, _log, captureDirectory, "terrain");
		_objectCaptureProbe = _objectCamera.gameObject.AddComponent<MinimapRenderCaptureProbe>();
		_objectCaptureProbe.Initialize(_objectCamera, _log, captureDirectory, "objects");
		_log?.LogInfo("[Minimap] native-materials-v1: scene cameras preserve terrain, water and object shaders.");
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

	internal void BeginFrame()
	{
		_terrainLightingBridge?.SetArmed(false);
		_objectLightingBridge?.SetArmed(false);
		_terrainIdentityBridge?.SetArmed(false);
		_terrainSceneryScope?.Arm(null, false);
		_objectSceneryScope?.Arm(null, false);
		_terrainCaptureProbe?.Arm(MinimapBackgroundState.Disabled, false);
		_objectCaptureProbe?.Arm(MinimapBackgroundState.Disabled, false);
		if (_terrainCamera != null) _terrainCamera.enabled = false;
		if (_objectCamera != null) _objectCamera.enabled = false;
	}

	internal void InvalidateCachedScenery() => _nextSceneryRefresh = 0f;

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

	private static void ConfigureCamera(Camera camera, Camera playerCamera, Vector3 center,
		float radius, float mapYaw, MinimapBackgroundState state, float sliceAbove,
		float sliceBelow, int cullingMask, Color clearColor, float depthOffset,
		MinimapSceneCameraPass pass)
	{
		RenderTexture target = camera.targetTexture;
		if (playerCamera != null && playerCamera != camera)
		{
			camera.CopyFrom(playerCamera);
		}
		// A generic texture shader cannot preserve terrain splat layers, water reflection
		// or foliage alpha clipping. Reset after CopyFrom so every pass uses its materials.
		camera.ResetReplacementShader();
		camera.enabled = false;
		camera.targetTexture = target;
		camera.cullingMask = cullingMask;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = clearColor;
		camera.rect = new Rect(0f, 0f, 1f, 1f);
		camera.useOcclusionCulling = false;
		camera.layerCullDistances = NoLayerCullDistances;
		camera.layerCullSpherical = false;
		camera.depthTextureMode |= DepthTextureMode.Depth;
		camera.depth = playerCamera != null ? playerCamera.depth + depthOffset : depthOffset;
		camera.renderingPath = RenderingPath.Forward;
		camera.orthographic = true;
		camera.orthographicSize = radius;
		camera.aspect = 1f;
		camera.ResetProjectionMatrix();
		camera.ResetCullingMatrix();
		camera.transform.rotation = Quaternion.Euler(0f, mapYaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
		MinimapCameraDepthProfile depth = MinimapRenderPolicy.ResolveCameraDepth(
			pass, state, center.y, sliceAbove, sliceBelow);
		camera.transform.position = new Vector3(center.x, depth.PositionY, center.z);
		camera.nearClipPlane = depth.NearClip;
		camera.farClipPlane = depth.FarClip;
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
		_terrainIdentityBridge = null;
		_terrainLightingBridge = null;
		_objectLightingBridge = null;
		_terrainSceneryScope = null;
		_objectSceneryScope = null;
		_terrainCaptureProbe = null;
		_objectCaptureProbe = null;
		_cliffRenderers.Clear();
		_rendererScratch.Clear();
		_waterMaterials.Clear();
		_sceneryRegions = null;
	}

	private void RefreshScenery(Vector3 center, float radius)
	{
		List<LevelObject>[,] regions = LevelObjects.objects;
		float now = Time.realtimeSinceStartup;
		if (ReferenceEquals(regions, _sceneryRegions) && now < _nextSceneryRefresh &&
			Mathf.Abs(radius - _sceneryRadius) < 0.01f &&
			(center - _sceneryCenter).sqrMagnitude < 1024f) return;
		_sceneryRegions = regions;
		_sceneryCenter = center;
		_sceneryRadius = radius;
		_nextSceneryRefresh = now + 2f;
		_cliffRenderers.Clear();
		_waterMaterials.Clear();
		if (regions == null) return;
		foreach (WaterVolume volume in UnityEngine.Object.FindObjectsOfType<WaterVolume>())
		{
			if (volume != null && volume.sharedMaterial != null) _waterMaterials.Add(volume.sharedMaterial);
		}
		// A large cliff can intersect the viewport even if its origin is in a distant
		// region. Filter semantic assets first, then renderer bounds, not origin distance.
		for (int x = 0; x < regions.GetLength(0); x++)
		for (int z = 0; z < regions.GetLength(1); z++)
		{
			List<LevelObject> objects = regions[x, z];
			if (objects == null) continue;
			for (int i = 0; i < objects.Count; i++)
			{
				LevelObject levelObject = objects[i];
				if (levelObject?.asset == null || levelObject.asset.chart != EObjectChart.CLIFF) continue;
				CollectScenery(levelObject.transform, center, radius);
				CollectScenery(levelObject.skybox, center, radius);
			}
		}
		// Query nearby regions before enumerating renderer hierarchies. The margin
		// covers large canopies and movement between refreshes, without a map-wide tree scan.
		Regions.GetCoordinateBoundsVector2Int(center, radius * 1.415f + 256f, out Vector2Int minTreeRegion, out Vector2Int maxTreeRegion);
		for (int x = minTreeRegion.x; x <= maxTreeRegion.x; x++)
		for (int z = minTreeRegion.y; z <= maxTreeRegion.y; z++)
		{
			List<ResourceSpawnpoint> resources = LevelGround.GetTreesOrNullInRegion(new Vector2Int(x, z));
			if (resources == null) continue;
			for (int i = 0; i < resources.Count; i++)
			{
				ResourceSpawnpoint resource = resources[i];
				if (resource == null) continue;
				// Include the game's own visible model/stump/landmark choice. The render
				// scope never enables inactive/dead-tree models, so no trees are resurrected.
				CollectScenery(resource.model, center, radius);
				CollectScenery(resource.stump, center, radius);
				CollectScenery(resource.skybox, center, radius);
			}
		}
	}

	private void CollectScenery(Transform root, Vector3 center, float radius)
	{
		if (root == null) return;
		_rendererScratch.Clear();
		root.GetComponentsInChildren(true, _rendererScratch);
		float extent = radius * 1.415f + 64f;
		for (int i = 0; i < _rendererScratch.Count; i++)
		{
			Renderer renderer = _rendererScratch[i];
			if (renderer == null) continue;
			Bounds bounds = renderer.bounds;
			if (bounds.max.x < center.x - extent || bounds.min.x > center.x + extent ||
				bounds.max.z < center.z - extent || bounds.min.z > center.z + extent) continue;
			_cliffRenderers.Add(renderer);
		}
	}

	private static void ReleaseTexture(ref RenderTexture texture)
	{
		if (texture == null) return;
		texture.Release();
		UnityEngine.Object.Destroy(texture);
		texture = null;
	}
}
