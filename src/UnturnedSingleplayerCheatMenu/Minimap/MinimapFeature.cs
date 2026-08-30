using System;
using System.Collections.Generic;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class MinimapFeature : IDisposable
{
	private readonly ManualLogSource _log;
	private readonly PluginUiRoot _uiRoot;
	private readonly NearbyItemIndex _itemIndex;
	private readonly NativeMapCropProvider _nativeMap;
	private readonly BackgroundStateMachine _backgroundState = new BackgroundStateMachine();
	private readonly OverheadOcclusionDetector _occlusion = new OverheadOcclusionDetector();
	private readonly MinimapSceneRenderer _sceneRenderer;
	private readonly NearbyWorldScanner _worldScanner = new NearbyWorldScanner();
	private readonly NativeMapMarkerAdapter _nativeMarkers = new NativeMapMarkerAdapter();
	private readonly TeleportMarkerAdapter _teleportMarkers;
	private readonly Action<MinimapSettings> _settingsChanged;
	private readonly List<GroundItemSnapshot> _groundItems = new List<GroundItemSnapshot>();
	private readonly List<MinimapHudMarker> _hudMarkers = new List<MinimapHudMarker>();
	private IReadOnlyList<MinimapWorldMarker> _cachedNativeMarkers = Array.Empty<MinimapWorldMarker>();
	private IReadOnlyList<MinimapWorldMarker> _cachedTeleportMarkers = Array.Empty<MinimapWorldMarker>();
	private IReadOnlyList<MinimapWorldMarker> _cachedWorldMarkers = Array.Empty<MinimapWorldMarker>();
	private IReadOnlyList<MinimapItemMarker> _cachedItemMarkers = Array.Empty<MinimapItemMarker>();
	private MinimapSettings _settings;
	private MinimapHud _hud;
	private NativeMapCropSnapshot _crop = NativeMapCropSnapshot.Unavailable("尚未初始化");
	private OverheadOcclusionDiagnostics _occlusionDiagnostics;
	private MinimapBackgroundState _state = MinimapBackgroundState.NativeMapCrop;
	private float _mapYaw;
	private float _nextWorldScanTime;
	private float _nextMarkerProjectionTime;
	private float _nextProbeTime;
	private float _nextRenderTime;
	private int _itemSnapshotVersion;
	private bool _disposed;

	internal MinimapSettings Settings => _settings.Clone();
	internal MinimapBackgroundState BackgroundState => _state;
	internal OverheadOcclusionDiagnostics OcclusionDiagnostics => _occlusionDiagnostics;
	internal bool NeedsItemScan => _settings.Enabled && _settings.ShowItems && SingleplayerGuard.IsReady &&
		Player.LocalPlayer != null && (_settings.LayoutEditMode || !PointToolService.IsInteractionPaused());
	internal float ItemScanRadius => MinimapProjection.ResolveCandidateScanRadius(
		_settings.VisibleRadiusMeters, _settings.Shape);
	internal float ItemScanFrequencyHz => _settings.WorldScanHz;

	internal MinimapFeature(ManualLogSource log, PluginUiRoot uiRoot, TeleportStore teleports,
		NearbyItemIndex itemIndex, MinimapSettings settings, Action<MinimapSettings> settingsChanged)
	{
		_log = log;
		_uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
		_itemIndex = itemIndex ?? throw new ArgumentNullException(nameof(itemIndex));
		_settings = (settings ?? new MinimapSettings()).Normalize().Clone();
		_nextWorldScanTime = 0f;
		_nextMarkerProjectionTime = 0f;
		_nextRenderTime = 0f;
		_settingsChanged = settingsChanged;
		_nativeMap = new NativeMapCropProvider(log);
		_sceneRenderer = new MinimapSceneRenderer(log);
		_teleportMarkers = new TeleportMarkerAdapter(teleports);
		EnsureHud();
	}

	internal void ApplySettings(MinimapSettings settings)
	{
		_settings = (settings ?? new MinimapSettings()).Normalize().Clone();
		_nextWorldScanTime = 0f;
		_nextMarkerProjectionTime = 0f;
		_nextRenderTime = 0f;
		EnsureHud();
		_hud.ApplySettings(_settings, SingleplayerGuard.IsReady && Player.LocalPlayer != null);
		if (!_settings.Enabled)
		{
			_backgroundState.Reset();
			_hud.SetMarkers(Array.Empty<MinimapHudMarker>());
			_hud.SetBackground(MinimapBackgroundState.Disabled, _crop, null, null);
		}
	}

	internal void Update(float now, NearbyItemSnapshot sharedItemSnapshot = null)
	{
		if (_disposed) return;
		_sceneRenderer.BeginFrame();
		EnsureHud();
		Player player = Player.LocalPlayer;
		bool active = _settings.Enabled && SingleplayerGuard.IsReady && player != null &&
			(_settings.LayoutEditMode || !PointToolService.IsInteractionPaused());
		_hud.SetRuntimeVisible(active);
		if (!active) return;

		Vector3 center = player.transform.position;
		if (now >= _nextProbeTime)
		{
			_nextProbeTime = now + 1f / Mathf.Max(MinimapSettings.MinProbeHz, _settings.OcclusionProbeHz);
			_occlusionDiagnostics = _occlusion.Probe(player, _settings);
		}

		_crop = _nativeMap.Capture(center, _settings.VisibleRadiusMeters);
		CartographyVolume volume = CartographyVolumeManager.Get().GetMainVolume();
		_mapYaw = volume != null ? volume.transform.eulerAngles.y : 0f;
		bool nativeAvailable = _crop.IsAvailable && _crop.CoversVisibleRadius;
		MinimapBackgroundState previousState = _state;
		_state = _backgroundState.Update(now, _settings.BackgroundMode, nativeAvailable,
			_sceneRenderer.IsAvailable, _occlusionDiagnostics.Summary,
			_settings.OcclusionEnterDelaySeconds, _settings.OcclusionExitDelaySeconds);
		bool stateChanged = previousState != _state;
		if (stateChanged)
		{
			_log?.LogInfo($"[Minimap] background {previousState} -> {_state}; requested={_settings.BackgroundMode}, " +
				$"native={nativeAvailable}, realtime={_sceneRenderer.IsAvailable}.");
		}

		bool needsCamera = MinimapRenderPolicy.UsesSceneCameras(_state);
		if (MinimapRenderPolicy.ShouldRender(needsCamera, stateChanged, now, _nextRenderTime))
		{
			_nextRenderTime = now + 1f / Mathf.Max(MinimapSettings.MinRenderHz, _settings.BackgroundRenderHz);
			if (!_sceneRenderer.Render(center, _settings.VisibleRadiusMeters, _mapYaw, _state,
				_settings.ShowLoadedBuildings, _settings.UndergroundSliceAbove, _settings.UndergroundSliceBelow))
			{
				_state = nativeAvailable ? MinimapBackgroundState.NativeMapCrop : MinimapBackgroundState.FallbackGrid;
			}
		}
		needsCamera = MinimapRenderPolicy.UsesSceneCameras(_state);
		bool showLoadedObjects = MinimapRenderPolicy.ShowsLoadedObjects(_state, _settings.ShowLoadedBuildings);
		_hud.SetBackground(_state, _crop,
			needsCamera ? _sceneRenderer.TerrainTexture : null,
			showLoadedObjects ? _sceneRenderer.ObjectTexture : null);

		if (now >= _nextWorldScanTime)
		{
			_nextWorldScanTime = now + 1f / Mathf.Max(MinimapSettings.MinScanHz, _settings.WorldScanHz);
			float candidateScanRadius = ItemScanRadius;
			NearbyItemSnapshot itemSnapshot = sharedItemSnapshot;
			if (_settings.ShowItems && (itemSnapshot == null || itemSnapshot.Radius + 0.001f < candidateScanRadius))
			{
				itemSnapshot = _itemIndex.Refresh(center, candidateScanRadius, ++_itemSnapshotVersion);
			}
			RefreshMarkerSources(center, candidateScanRadius, itemSnapshot);
			_nextMarkerProjectionTime = 0f;
		}
		if (now >= _nextMarkerProjectionTime)
		{
			_nextMarkerProjectionTime = now + 0.05f;
			ProjectCachedMarkers(center);
			_hud.SetMarkers(_hudMarkers);
		}
	}

	internal void LateUpdate()
	{
		if (_disposed || !_settings.Enabled || !SingleplayerGuard.IsReady) return;
		Player player = Player.LocalPlayer;
		if (player == null) return;
		_hud?.UpdatePose(player.transform.position, player.transform.rotation.eulerAngles.y, _mapYaw);
	}

	private void RefreshMarkerSources(Vector3 center, float candidateScanRadius, NearbyItemSnapshot itemSnapshot)
	{
		_cachedNativeMarkers = _settings.ShowNativeMapMarkers
			? _nativeMarkers.Capture(center, candidateScanRadius)
			: Array.Empty<MinimapWorldMarker>();
		_cachedTeleportMarkers = _settings.ShowTeleportMarkers
			? _teleportMarkers.Capture(center, candidateScanRadius)
			: Array.Empty<MinimapWorldMarker>();
		_cachedWorldMarkers = _worldScanner.Scan(center, candidateScanRadius,
			_settings.ShowZombies, _settings.ShowAnimals, _settings.ShowPlayers);

		if (!_settings.ShowItems || itemSnapshot == null)
		{
			_cachedItemMarkers = Array.Empty<MinimapItemMarker>();
			return;
		}
		_groundItems.Clear();
		for (int index = 0; index < itemSnapshot.Items.Count; index++)
		{
			NearbyGroundItem item = itemSnapshot.Items[index];
			if (item?.Asset != null) _groundItems.Add(item.ToMinimapSnapshot());
		}
		MinimapProjectionContext projection = new MinimapProjectionContext(
			new MinimapWorldPoint(center.x, center.y, center.z), _settings.VisibleRadiusMeters,
			_settings.Size, _mapYaw, _settings.Shape, _settings.SameFloorTolerance);
		_cachedItemMarkers = MinimapItemAggregator.Compose(_groundItems, projection,
			_settings.ItemRarityMask, _settings.ItemMergePixels, _settings.ShowOtherFloors);
	}

	private void ProjectCachedMarkers(Vector3 center)
	{
		_hudMarkers.Clear();
		MinimapProjectionContext projection = new MinimapProjectionContext(
			new MinimapWorldPoint(center.x, center.y, center.z), _settings.VisibleRadiusMeters,
			_settings.Size, _mapYaw, _settings.Shape, _settings.SameFloorTolerance);
		AddProjected(_cachedNativeMarkers, projection);
		AddProjected(_cachedTeleportMarkers, projection);
		AddProjected(_cachedWorldMarkers, projection);
		for (int index = 0; index < _cachedItemMarkers.Count; index++)
		{
			MinimapItemMarker item = _cachedItemMarkers[index];
			MinimapProjectedPoint point = MinimapProjection.Project(item.Position, projection);
			if (!point.IsVisible || (!point.IsSameFloor && !_settings.ShowOtherFloors)) continue;
			Color color = ItemTool.getRarityColorUI((EItemRarity)(int)item.Rarity);
			MinimapHeightIndicator indicator = point.HeightDelta > _settings.SameFloorTolerance
				? MinimapHeightIndicator.Above
				: point.HeightDelta < -_settings.SameFloorTolerance
					? MinimapHeightIndicator.Below
					: MinimapHeightIndicator.None;
			if (indicator != MinimapHeightIndicator.None) color.a *= 0.65f;
			_hudMarkers.Add(new MinimapHudMarker(unchecked((int)item.InstanceId), MinimapWorldMarkerKind.Item,
				point.X, point.Y, color, heightIndicator: indicator));
		}
	}

	private void AddProjected(IReadOnlyList<MinimapWorldMarker> markers, MinimapProjectionContext projection)
	{
		if (markers == null) return;
		for (int index = 0; index < markers.Count; index++)
		{
			MinimapWorldMarker marker = markers[index];
			Vector3 position = marker.FollowTarget != null ? marker.FollowTarget.position : marker.Position;
			MinimapProjectedPoint point = MinimapProjection.Project(
				new MinimapWorldPoint(position.x, position.y, position.z), projection);
			if (!point.IsVisible || (!point.IsSameFloor && !_settings.ShowOtherFloors)) continue;
			Color color = marker.Color;
			MinimapHeightIndicator indicator = MinimapHeightIndicator.None;
			if (!point.IsSameFloor)
			{
				color.a *= 0.55f;
				indicator = point.HeightDelta > 0f ? MinimapHeightIndicator.Above : MinimapHeightIndicator.Below;
			}
			_hudMarkers.Add(new MinimapHudMarker(CombineStableId(marker.Kind, marker.StableId), marker.Kind,
				point.X, point.Y, color, marker.Rotation, marker.TeleportKind, indicator));
		}
	}

	private static int CombineStableId(MinimapWorldMarkerKind kind, int stableId)
	{
		unchecked { return ((int)kind + 1) * 397 ^ stableId; }
	}

	private void EnsureHud()
	{
		if (_hud != null && _hud.IsAlive) return;
		_hud?.Dispose();
		_hud = new MinimapHud(_uiRoot, _log, HandleLayoutCommitted);
		_hud.ApplySettings(_settings, SingleplayerGuard.IsReady && Player.LocalPlayer != null);
	}

	private void HandleLayoutCommitted(MinimapAnchor anchor, float offsetX, float offsetY, float size)
	{
		MinimapSettings changed = _settings.Clone();
		changed.Anchor = anchor;
		changed.OffsetX = offsetX;
		changed.OffsetY = offsetY;
		changed.Size = size;
		changed.LayoutEditMode = false;
		_settings = changed.Normalize().Clone();
		_settingsChanged?.Invoke(_settings.Clone());
	}

	internal void HandleSceneChanged()
	{
		_nativeMap.HandleSceneChanged();
		_backgroundState.Reset();
		_crop = NativeMapCropSnapshot.Unavailable("地图场景已切换");
		_nextWorldScanTime = _nextMarkerProjectionTime = _nextProbeTime = _nextRenderTime = 0f;
		_cachedNativeMarkers = Array.Empty<MinimapWorldMarker>();
		_cachedTeleportMarkers = Array.Empty<MinimapWorldMarker>();
		_cachedWorldMarkers = Array.Empty<MinimapWorldMarker>();
		_cachedItemMarkers = Array.Empty<MinimapItemMarker>();
		_hudMarkers.Clear();
		_hud?.SetMarkers(_hudMarkers);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_hud?.Dispose();
		_hud = null;
		_nativeMap.Dispose();
		_sceneRenderer.Dispose();
	}
}
