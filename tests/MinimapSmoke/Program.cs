using UnturnedSingleplayerCheatMenu.Minimap;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string FindRepositoryFile(params string[] relativeSegments)
{
    DirectoryInfo directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        string candidate = Path.Combine([directory.FullName, .. relativeSegments]);
        if (File.Exists(candidate))
            return candidate;
        directory = directory.Parent;
    }

    throw new FileNotFoundException($"Could not locate repository file: {Path.Combine(relativeSegments)}");
}

string nearbyWorldScannerSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Minimap", "NearbyWorldScanner.cs"));
Check(nearbyWorldScannerSource.Contains("ZombieManager.AllZombies", StringComparison.Ordinal) &&
      !nearbyWorldScannerSource.Contains("ZombieManager.getZombiesInRadius", StringComparison.Ordinal),
    "Zombie radar must scan every instantiated zombie instead of disappearing at navigation-region boundaries.");

Check(ZombieRegionRetentionPolicy.IsEnabled(retentionAllowed: true, minimapEnabled: true, showZombies: true) &&
      !ZombieRegionRetentionPolicy.IsEnabled(retentionAllowed: false, minimapEnabled: true, showZombies: true) &&
      !ZombieRegionRetentionPolicy.IsEnabled(retentionAllowed: true, minimapEnabled: false, showZombies: true) &&
      !ZombieRegionRetentionPolicy.IsEnabled(retentionAllowed: true, minimapEnabled: true, showZombies: false),
    "Zombie-region retention must be restricted to an active singleplayer zombie radar.");
Check(ZombieRegionRetentionPolicy.ShouldSuppressBoundExitDestroy(
          retentionEnabled: true, insideBoundUpdate: true, existingZombieCount: 1) &&
      !ZombieRegionRetentionPolicy.ShouldSuppressBoundExitDestroy(
          retentionEnabled: true, insideBoundUpdate: false, existingZombieCount: 1) &&
      !ZombieRegionRetentionPolicy.ShouldSuppressBoundExitDestroy(
          retentionEnabled: true, insideBoundUpdate: true, existingZombieCount: 0),
    "Only non-empty ZombieManager bound exits may suppress the native region destroy call.");
Check(ZombieRegionRetentionPolicy.ShouldReuseExistingRegion(retentionEnabled: true, insideBoundUpdate: true, existingZombieCount: 1) &&
      !ZombieRegionRetentionPolicy.ShouldReuseExistingRegion(retentionEnabled: true, insideBoundUpdate: true, existingZombieCount: 0),
    "Re-entering a retained region must reuse its zombies without blocking first-time generation.");
float protectionRadius = ZombieRegionRetentionPolicy.ResolveProtectionRadius(100f);
Check(protectionRadius == 116f &&
      ZombieRegionRetentionPolicy.ResolveProtectionRadius(float.NaN) ==
          ZombieRegionRetentionPolicy.ProtectionRadiusPaddingMeters &&
      ZombieRegionRetentionPolicy.IsInsideProtectionRadius(100f, 0f, protectionRadius) &&
      !ZombieRegionRetentionPolicy.IsInsideProtectionRadius(117f, 0f, protectionRadius),
    "Radar cleanup must protect the complete scan radius plus a stable boundary hysteresis margin.");
Check(ZombieRegionRetentionPolicy.CanRecycleRegion(
          trackedByPlugin: true, isNetworked: false, playerCount: 0,
          hasLiveZombieInsideProtectionRadius: false) &&
      !ZombieRegionRetentionPolicy.CanRecycleRegion(
          trackedByPlugin: false, isNetworked: false, playerCount: 0,
          hasLiveZombieInsideProtectionRadius: false) &&
      !ZombieRegionRetentionPolicy.CanRecycleRegion(
          trackedByPlugin: true, isNetworked: true, playerCount: 0,
          hasLiveZombieInsideProtectionRadius: false) &&
      !ZombieRegionRetentionPolicy.CanRecycleRegion(
          trackedByPlugin: true, isNetworked: false, playerCount: 1,
          hasLiveZombieInsideProtectionRadius: false) &&
      !ZombieRegionRetentionPolicy.CanRecycleRegion(
          trackedByPlugin: true, isNetworked: false, playerCount: 0,
          hasLiveZombieInsideProtectionRadius: true),
    "Cleanup may only recycle plugin-retained, inactive, unoccupied regions outside radar protection.");
Check(ZombieRegionRetentionPolicy.ResolveFarRecycleCount(6) == 0 &&
      ZombieRegionRetentionPolicy.ResolveFarRecycleCount(10) == 2 &&
      ZombieRegionRetentionPolicy.ResolveFarRecycleCount(3, 0) == 3,
    "Far retained regions must use the documented bounded LRU recycle budget.");
string zombieRetentionPatchSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Patches", "ZombieRegionRetentionPatch.cs"));
string runtimeContextSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Services", "PluginRuntimeContext.cs"));
Check(zombieRetentionPatchSource.Contains("onBoundUpdated", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("ShouldSuppressBoundExitDestroy", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("ShouldReuseExistingRegion", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("HasLiveZombieInsideProtectionRadius", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("ResolveFarRecycleCount", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("RuntimeSessionBoundary.CaptureCurrent().IsSupportedGameplay", StringComparison.Ordinal) &&
      zombieRetentionPatchSource.Contains("region.destroy();", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("ZombieRegionRetentionPatch.Install(_harmony, _log);", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("ZombieRegionRetentionPatch.Maintain();", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("ZombieRegionRetentionPatch.ReleaseAll", StringComparison.Ordinal) &&
      runtimeContextSource.Contains("ZombieRegionRetentionPatch.ResetForSceneTransition();", StringComparison.Ordinal),
    "The tested retention, bounded cleanup, scene reset, and teardown paths must remain wired into the live lifecycle.");

MinimapSettings defaults = new MinimapSettings().Normalize();
Check(!defaults.Enabled && !defaults.ShowItems && !defaults.RotateWithView,
    "The minimap, item radar, and optional view rotation must remain opt-in on first launch.");

MinimapLayoutInputState layoutInput = new();
Check(!layoutInput.ShouldBlockGameKeyQueries(9),
    "Native game input must remain available outside minimap layout editing.");
layoutInput.SetEditing(active: true, frame: 10);
Check(layoutInput.ShouldBlockGameKeyQueries(10) && layoutInput.ShouldBlockGameKeyQueries(11),
    "Native InputEx queries must remain blocked for the complete layout-edit session.");
layoutInput.SuppressUntilRelease(MinimapLayoutReservedKey.Return, frame: 11);
layoutInput.SetEditing(active: false, frame: 11);
Check(layoutInput.ShouldBlockGameKeyQueries(11) && !layoutInput.ShouldBlockGameKeyQueries(12),
    "Finishing layout editing must still block native key-down handling for the completion frame.");
Check(layoutInput.ShouldBlockChatAction(frame: 12) && !layoutInput.ShouldBlockChatAction(frame: 13),
	"The Return key-up tail must block native chat exactly once after applying layout changes.");
layoutInput.SetEditing(active: true, frame: 20);
Check(layoutInput.ShouldBlockGameKeyQueries(20) && layoutInput.ShouldBlockGameKeyQueries(21),
	"End and every other InputEx-driven menu or interaction key must remain blocked while layout editing is active.");
layoutInput.Reset();
Check(!layoutInput.IsEditing && !layoutInput.ShouldBlockGameKeyQueries(21),
    "Layout input interception must reset completely during plugin teardown.");
Check(defaults.Shape == MinimapShape.Circle && defaults.BackgroundMode == MinimapBackgroundMode.AutoNativeFirst,
    "The first-launch minimap must use a circular, native-map-first background.");
Check(defaults.Size == 260f && defaults.VisibleRadiusMeters == 100f,
    "The first-launch size and visible radius must match the implementation plan.");
Check(defaults.ItemRarityMask == ItemRarityMask.All,
    "Enabling the item layer for the first time must expose all six rarity tiers.");
Check(defaults.Anchor == MinimapAnchor.TopLeft && defaults.OffsetX == 20f && defaults.OffsetY == 20f &&
      defaults.BackgroundRenderHz == 5f && defaults.WorldScanHz == 5f && defaults.ShowLoadedBuildings &&
      defaults.OcclusionProbeHz == 5f && defaults.OcclusionEnterThickness == 2.5f &&
      defaults.OcclusionExitThickness == 1.5f && defaults.OcclusionEnterDelaySeconds == 0.75f &&
      defaults.OcclusionExitDelaySeconds == 0.25f && defaults.OcclusionMergeGap == 0.25f &&
      defaults.FloorExclusionMaxThickness == 1.25f && defaults.ShowCompass && defaults.ShowCoordinates &&
      defaults.ShowNativeMapMarkers && defaults.ShowTeleportMarkers && defaults.ShowZombies &&
      defaults.ShowAnimals && defaults.ShowPlayers && defaults.ItemMergePixels == 8f &&
      defaults.UndergroundSliceAbove == 4f && defaults.UndergroundSliceBelow == 2048f &&
      defaults.SameFloorTolerance == 6f && defaults.ShowOtherFloors && !defaults.LayoutEditMode,
    "Every first-launch minimap setting must match the documented configuration table.");
Check(MinimapZoomPolicy.Apply(100f, MinimapZoomDirection.In, 1f) == 90f &&
      MinimapZoomPolicy.Apply(90f, MinimapZoomDirection.Out, 1f) == 100f &&
      MinimapZoomPolicy.Apply(20f, MinimapZoomDirection.In, 10f) == MinimapSettings.MinVisibleRadiusMeters &&
      MinimapZoomPolicy.Apply(500f, MinimapZoomDirection.Out, 10f) == MinimapSettings.MaxVisibleRadiusMeters,
    "Mouse-wheel zoom must be noticeable, reversible, and clamped to the configured minimap radius range.");
Check(Math.Abs(MinimapProjection.ResolveCandidateScanRadius(100f, MinimapShape.Circle) - 100f) < 0.001f &&
      Math.Abs(MinimapProjection.ResolveCandidateScanRadius(100f, MinimapShape.Square) - 141.42136f) < 0.001f,
    "Square mode must scan the circumscribed radius before applying its exact rectangular clip.");

MinimapSettings clamped = new MinimapSettings
{
    Size = 9999f,
    VisibleRadiusMeters = -4f,
    BackgroundRenderHz = 99f,
    WorldScanHz = float.NaN,
    OcclusionProbeHz = 0f,
    ItemMergePixels = 100f,
    SameFloorTolerance = -1f,
    UndergroundSliceAbove = float.PositiveInfinity,
    UndergroundSliceBelow = -2f,
    OffsetX = float.NaN,
    OffsetY = float.PositiveInfinity
}.Normalize();
Check(clamped.Size == MinimapSettings.MaxSize, "Minimap size must clamp to the 520px ceiling.");
Check(clamped.VisibleRadiusMeters == MinimapSettings.MinVisibleRadiusMeters,
    "Visible radius must clamp to the 20m floor.");
Check(clamped.BackgroundRenderHz == MinimapSettings.MaxRenderHz && clamped.WorldScanHz == MinimapSettings.MinScanHz,
    "Background and world scan rates must remain inside their supported budgets.");
Check(clamped.OcclusionProbeHz == MinimapSettings.MinProbeHz,
    "Occlusion probing must remain inside its supported budget.");
Check(clamped.ItemMergePixels == MinimapSettings.MaxItemMergePixels,
    "Item overlap merging must clamp to its supported pixel radius.");
Check(clamped.SameFloorTolerance == MinimapSettings.MinSameFloorTolerance,
    "Same-floor filtering must reject a negative tolerance.");
Check(clamped.UndergroundSliceAbove == MinimapSettings.DefaultUndergroundSliceAbove &&
      clamped.UndergroundSliceBelow == MinimapSettings.MinUndergroundSlice,
    "Underground slice bounds must sanitize non-finite and negative values.");
Check(MinimapSettings.MaxUndergroundSliceAbove == 50f &&
      MinimapSettings.MaxUndergroundSliceBelow == 2048f,
    "Height-slice upward clipping must stay bounded while downward rendering can cover the complete Landscape vertical span.");
MinimapCameraDepthProfile terrainDepth = MinimapRenderPolicy.ResolveCameraDepth(
    MinimapSceneCameraPass.Terrain, MinimapBackgroundState.RealtimeHeightSlice,
    playerY: 36f, sliceAbove: 4f, sliceBelow: 2048f);
MinimapCameraDepthProfile surfaceObjectsDepth = MinimapRenderPolicy.ResolveCameraDepth(
    MinimapSceneCameraPass.Objects, MinimapBackgroundState.RealtimeSurface,
    playerY: 36f, sliceAbove: 4f, sliceBelow: 2048f);
MinimapCameraDepthProfile slicedObjectsDepth = MinimapRenderPolicy.ResolveCameraDepth(
    MinimapSceneCameraPass.Objects, MinimapBackgroundState.RealtimeHeightSlice,
    playerY: 36f, sliceAbove: 4f, sliceBelow: 2048f);
Check(terrainDepth.PositionY == 2000f && terrainDepth.NearClip == 0.1f &&
      terrainDepth.FarClip == 2200f &&
      surfaceObjectsDepth.PositionY == 836f && surfaceObjectsDepth.NearClip == 0.1f &&
      surfaceObjectsDepth.FarClip == 1000f &&
      slicedObjectsDepth.PositionY == 40f && slicedObjectsDepth.NearClip == 0.05f &&
      slicedObjectsDepth.FarClip == 2052f,
    "Terrain must use the proven high-altitude full-depth camera while only loaded objects retain the optional underground height slice.");
Check(clamped.OffsetX == 0f && clamped.OffsetY == 0f,
    "Persisted layout offsets must sanitize non-finite values.");
MinimapSettings negativeOffsets = new MinimapSettings { OffsetX = -10f, OffsetY = -20f }.Normalize();
Check(negativeOffsets.OffsetX == 0f && negativeOffsets.OffsetY == 0f,
    "Persisted layout offsets must not remain outside their selected screen anchor.");

MinimapSettings invalidThresholds = new MinimapSettings
{
    FloorExclusionMaxThickness = 3f,
    OcclusionExitThickness = 2f,
    OcclusionEnterThickness = 1f
}.Normalize();
Check(invalidThresholds.FloorExclusionMaxThickness == MinimapSettings.DefaultFloorExclusionMaxThickness &&
      invalidThresholds.OcclusionExitThickness == MinimapSettings.DefaultOcclusionExitThickness &&
      invalidThresholds.OcclusionEnterThickness == MinimapSettings.DefaultOcclusionEnterThickness,
    "Unsafe floor/exit/enter threshold ordering must atomically fall back to the documented defaults.");

MinimapSettings clone = defaults.Clone();
clone.Shape = MinimapShape.Square;
clone.ItemRarityMask = ItemRarityMask.Rare | ItemRarityMask.Mythical;
Check(defaults.Shape == MinimapShape.Circle && defaults.ItemRarityMask == ItemRarityMask.All,
    "A settings clone must not mutate the active settings object.");
MinimapSettings sharedLayout = new MinimapSettings
{
    Anchor = MinimapAnchor.BottomRight,
    OffsetX = 73f,
    OffsetY = 91f,
    Size = 340f,
    RotateWithView = false
}.Normalize();
sharedLayout.RotateWithView = true;
sharedLayout.Normalize();
Check(sharedLayout.Anchor == MinimapAnchor.BottomRight && sharedLayout.OffsetX == 73f &&
      sharedLayout.OffsetY == 91f && sharedLayout.Size == 340f,
    "Enabling view rotation must reuse the exact fixed-map anchor, offsets, and size.");

MinimapProjectionContext circleProjection = new(
    center: new MinimapWorldPoint(0f, 10f, 0f),
    visibleRadiusMeters: 100f,
    viewportSizePixels: 200f,
    mapYawDegrees: 0f,
    shape: MinimapShape.Circle,
    sameFloorTolerance: 6f);
MinimapProjectedPoint east = MinimapProjection.Project(new MinimapWorldPoint(100f, 10f, 0f), circleProjection);
Check(east.IsVisible && Math.Abs(east.X - 100f) < 0.001f && Math.Abs(east.Y) < 0.001f,
    "A target on the east edge must project to the right edge of a north-up minimap.");
MinimapProjectedPoint north = MinimapProjection.Project(new MinimapWorldPoint(0f, 10f, 100f), circleProjection);
Check(north.IsVisible && Math.Abs(north.X) < 0.001f && Math.Abs(north.Y - 100f) < 0.001f,
    "CartographyVolume local +Z must project to the top edge without using player yaw.");
Check(!MinimapProjection.Project(new MinimapWorldPoint(80f, 10f, 80f), circleProjection).IsVisible,
    "Circular mode must clip targets that only fit inside the square corners.");

MinimapProjectionContext squareProjection = circleProjection.WithShape(MinimapShape.Square);
Check(MinimapProjection.Project(new MinimapWorldPoint(80f, 10f, 80f), squareProjection).IsVisible,
    "Square mode must retain targets inside its rectangular extent.");
Check(!MinimapProjection.Project(new MinimapWorldPoint(0f, 17f, 0f), circleProjection).IsSameFloor,
    "Targets outside the configured vertical tolerance must remain distinguishable from the current floor.");

MinimapProjectionContext rotatedMap = circleProjection.WithMapYaw(90f);
MinimapProjectedPoint rotatedNorth = MinimapProjection.Project(new MinimapWorldPoint(100f, 10f, 0f), rotatedMap);
Check(rotatedNorth.IsVisible && Math.Abs(rotatedNorth.X) < 0.001f && Math.Abs(rotatedNorth.Y - 100f) < 0.001f,
    "A 90-degree CartographyVolume yaw must rotate world +X onto the map's top edge.");
MinimapProjectedPoint rotatedLeft = MinimapProjection.Project(new MinimapWorldPoint(0f, 10f, 100f), rotatedMap);
Check(rotatedLeft.IsVisible && Math.Abs(rotatedLeft.X + 100f) < 0.001f && Math.Abs(rotatedLeft.Y) < 0.001f,
    "A 90-degree CartographyVolume yaw must rotate world +Z onto the map's left edge.");
Check(Math.Abs(MinimapProjection.ResolvePlayerMarkerRotation(135f, 90f) + 45f) < 0.001f,
    "Only the player marker should rotate relative to the fixed map orientation.");
MinimapProjectionContext viewRotatedSquare = squareProjection.WithViewportRotation(45f);
MinimapProjectedPoint newlyVisibleSquareCorner = MinimapProjection.Project(
    new MinimapWorldPoint(0f, 10f, 120f), viewRotatedSquare);
Check(!MinimapProjection.Project(new MinimapWorldPoint(0f, 10f, 120f), squareProjection).IsVisible &&
      newlyVisibleSquareCorner.IsVisible,
    "Square visibility must be evaluated after the heading-up viewport rotation.");
Check(Math.Abs(MinimapProjection.ResolveMapContentRotation(false, 135f, 90f)) < 0.001f &&
      Math.Abs(MinimapProjection.ResolveMapContentRotation(true, 135f, 90f) - 45f) < 0.001f &&
      Math.Abs(MinimapProjection.ResolveMapContentRotation(true, 5f, 355f) - 10f) < 0.001f,
    "Fixed north-up must remain unchanged while optional view-up rotation handles angle wraparound.");
Check(Math.Abs(MinimapProjection.ResolvePlayerMarkerRotation(90f, 135f) - 45f) < 0.001f,
    "The player pointer must show body heading relative to the rotating view-up map.");
Check(Math.Abs(MinimapProjection.ResolveContentExtent(false, 100f) - 100f) < 0.001f &&
      Math.Abs(MinimapProjection.ResolveContentExtent(true, 100f) - 141.42136f) < 0.001f,
    "Fixed mode must keep its original extent while rotation mode uses diagonal overscan.");

Check(!MinimapRenderPolicy.ShouldRender(needsCamera: false, stateChanged: true, now: 0f, nextRenderTime: 10f) &&
      MinimapRenderPolicy.ShouldRender(needsCamera: true, stateChanged: true, now: 0f, nextRenderTime: 10f) &&
      MinimapRenderPolicy.ShouldRender(needsCamera: true, stateChanged: false, now: 10f, nextRenderTime: 10f),
    "Realtime state changes must force exactly the camera render path without waiting for the old cadence.");
Check(!MinimapRenderPolicy.UsesSceneCameras(MinimapBackgroundState.NativeMapCrop) &&
      MinimapRenderPolicy.UsesSceneCameras(MinimapBackgroundState.RealtimeSurface) &&
      MinimapRenderPolicy.UsesSceneCameras(MinimapBackgroundState.RealtimeHeightSlice) &&
      !MinimapRenderPolicy.UsesSceneCameras(MinimapBackgroundState.FallbackGrid) &&
      !MinimapRenderPolicy.UsesSceneCameras(MinimapBackgroundState.Disabled),
    "Native, grid, and disabled backgrounds must never schedule a scene camera.");
Check(!MinimapRenderPolicy.ShowsLoadedObjects(MinimapBackgroundState.NativeMapCrop, enabled: true) &&
      MinimapRenderPolicy.ShowsLoadedObjects(MinimapBackgroundState.RealtimeSurface, enabled: true) &&
      MinimapRenderPolicy.ShowsLoadedObjects(MinimapBackgroundState.RealtimeHeightSlice, enabled: true) &&
      !MinimapRenderPolicy.ShowsLoadedObjects(MinimapBackgroundState.RealtimeSurface, enabled: false),
    "The loaded-object camera layer must be restricted to enabled realtime backgrounds.");
Check(MinimapRenderPolicy.ShowsNativeUnderlay(MinimapBackgroundState.RealtimeSurface, nativeAvailable: true) &&
      MinimapRenderPolicy.ShowsNativeUnderlay(MinimapBackgroundState.RealtimeHeightSlice, nativeAvailable: true) &&
      !MinimapRenderPolicy.ShowsNativeUnderlay(MinimapBackgroundState.NativeMapCrop, nativeAvailable: true) &&
      !MinimapRenderPolicy.ShowsNativeUnderlay(MinimapBackgroundState.RealtimeHeightSlice, nativeAvailable: false) &&
      MinimapRenderPolicy.UsesTransparentTerrainClear(MinimapBackgroundState.RealtimeSurface) &&
      MinimapRenderPolicy.UsesTransparentTerrainClear(MinimapBackgroundState.RealtimeHeightSlice) &&
      !MinimapRenderPolicy.UsesTransparentTerrainClear(MinimapBackgroundState.NativeMapCrop),
    "Realtime terrain gaps must reveal the native-map/grid underlay instead of an opaque dark clear color.");
Check(MinimapRenderPolicy.CanUseNativeMap(textureAvailable: true, coversVisibleRadius: false) &&
      !MinimapRenderPolicy.CanUseNativeMap(textureAvailable: false, coversVisibleRadius: true),
    "A valid Map.png must remain the automatic background at map edges even when the crop extends outside its UV bounds.");
MinimapNativeCropWindow fullNativeCrop = MinimapRenderPolicy.ResolveNativeCrop(
    centerX: 0.5f, centerY: 0.5f, halfWidth: 0.1f, halfHeight: 0.1f);
Check(fullNativeCrop.CoversVisibleRadius && fullNativeCrop.HasVisibleTexture &&
      Math.Abs(fullNativeCrop.UvX - 0.4f) < 0.0001f &&
      Math.Abs(fullNativeCrop.UvWidth - 0.2f) < 0.0001f &&
      Math.Abs(fullNativeCrop.ViewportX) < 0.0001f &&
      Math.Abs(fullNativeCrop.ViewportWidth - 1f) < 0.0001f,
    "A centered native crop must fill the viewport without padding.");
MinimapNativeCropWindow rightEdgeCrop = MinimapRenderPolicy.ResolveNativeCrop(
    centerX: 0.95f, centerY: 0.5f, halfWidth: 0.1f, halfHeight: 0.1f);
Check(!rightEdgeCrop.CoversVisibleRadius && rightEdgeCrop.HasVisibleTexture &&
      Math.Abs(rightEdgeCrop.UvX - 0.85f) < 0.0001f &&
      Math.Abs(rightEdgeCrop.UvWidth - 0.15f) < 0.0001f &&
      Math.Abs(rightEdgeCrop.ViewportX) < 0.0001f &&
      Math.Abs(rightEdgeCrop.ViewportWidth - 0.75f) < 0.0001f,
    "A right-edge crop must preserve player-centered alignment and leave only the out-of-map quarter for grid padding.");
MinimapNativeCropWindow leftEdgeCrop = MinimapRenderPolicy.ResolveNativeCrop(
    centerX: 0.05f, centerY: 0.5f, halfWidth: 0.1f, halfHeight: 0.1f);
Check(!leftEdgeCrop.CoversVisibleRadius && leftEdgeCrop.HasVisibleTexture &&
      Math.Abs(leftEdgeCrop.ViewportX - 0.25f) < 0.0001f &&
      Math.Abs(leftEdgeCrop.ViewportWidth - 0.75f) < 0.0001f,
    "A left-edge crop must place grid padding on the left without shifting the player off center.");
MinimapNativeCropWindow outsideNativeCrop = MinimapRenderPolicy.ResolveNativeCrop(
    centerX: 1.2f, centerY: 0.5f, halfWidth: 0.1f, halfHeight: 0.1f);
Check(!outsideNativeCrop.CoversVisibleRadius && !outsideNativeCrop.HasVisibleTexture,
    "A crop fully outside Map.png must show only controlled padding rather than stretching the edge texel.");
MinimapRenderCenter snappedA = MinimapRenderPolicy.SnapCenter(10.01f, -2.01f, 0f, 100f, 512);
MinimapRenderCenter snappedB = MinimapRenderPolicy.SnapCenter(10.02f, -2.02f, 0f, 100f, 512);
Check(Math.Abs(snappedA.X - snappedB.X) < 0.0001f && Math.Abs(snappedA.Z - snappedB.Z) < 0.0001f,
    "Sub-texel player movement must not move the realtime minimap camera between render frames.");
Check(MinimapDisplayText.Shape(MinimapShape.Circle) == "圆形" &&
      MinimapDisplayText.Anchor(MinimapAnchor.BottomRight) == "右下" &&
      MinimapDisplayText.BackgroundMode(MinimapBackgroundMode.RealtimeHeightSlice) == "实时高度切片" &&
      MinimapDisplayText.BackgroundState(MinimapBackgroundState.NativeMapCrop) == "原生地图裁剪" &&
      MinimapDisplayText.Rarity(ItemRarityMask.Rare | ItemRarityMask.Mythical) == "稀有、神话",
    "Every minimap enum shown to the player must have a complete Chinese display name.");

GroundItemSnapshot[] overlappingItems =
[
    new(30, new MinimapWorldPoint(4.1f, 10f, 0f), MinimapItemRarity.Rare),
    new(20, new MinimapWorldPoint(3.9f, 10f, 0f), MinimapItemRarity.Mythical),
    new(10, new MinimapWorldPoint(3.8f, 10f, 0f), MinimapItemRarity.Mythical)
];
IReadOnlyList<MinimapItemMarker> allRarities = MinimapItemAggregator.Compose(
    overlappingItems, circleProjection, ItemRarityMask.All, mergePixels: 8f, showOtherFloors: false);
Check(allRarities.Count == 1 && allRarities[0].InstanceId == 10 && allRarities[0].Rarity == MinimapItemRarity.Mythical,
    "Overlapping items must choose the highest rarity, then nearest distance, then stable instance ID.");
Check(allRarities[0].Position.X == 3.8f && allRarities[0].Position.Y == 10f,
    "Aggregated item winners must retain world coordinates for allocation-free 20Hz reprojection.");

GroundItemSnapshot[] sixRarities =
[
    new(101, new MinimapWorldPoint(-50f, 10f, 0f), MinimapItemRarity.Common),
    new(102, new MinimapWorldPoint(-30f, 10f, 0f), MinimapItemRarity.Uncommon),
    new(103, new MinimapWorldPoint(-10f, 10f, 0f), MinimapItemRarity.Rare),
    new(104, new MinimapWorldPoint(10f, 10f, 0f), MinimapItemRarity.Epic),
    new(105, new MinimapWorldPoint(30f, 10f, 0f), MinimapItemRarity.Legendary),
    new(106, new MinimapWorldPoint(50f, 10f, 0f), MinimapItemRarity.Mythical)
];
IReadOnlyList<MinimapItemMarker> tierOrder = MinimapItemAggregator.Compose(
    sixRarities, circleProjection, ItemRarityMask.All, mergePixels: 8f, showOtherFloors: false);
Check(tierOrder.Count == 6 && tierOrder.Select(marker => marker.Rarity).SequenceEqual(new[]
    {
        MinimapItemRarity.Mythical, MinimapItemRarity.Legendary, MinimapItemRarity.Epic,
        MinimapItemRarity.Rare, MinimapItemRarity.Uncommon, MinimapItemRarity.Common
    }), "All six enabled rarity tiers must retain their documented stable priority order.");
Check(MinimapItemAggregator.Compose(sixRarities, circleProjection, ItemRarityMask.None, 8f, false).Count == 0,
    "Select-none must atomically hide every item tier.");
ItemRarityMask withoutEpic = ItemRarityMask.All & ~ItemRarityMask.Epic;
IReadOnlyList<MinimapItemMarker> fiveTiers = MinimapItemAggregator.Compose(
    sixRarities, circleProjection, withoutEpic, 8f, false);
Check(fiveTiers.Count == 5 && fiveTiers.All(marker => marker.Rarity != MinimapItemRarity.Epic),
    "A single-tier toggle must affect only its selected rarity bit.");

IReadOnlyList<MinimapItemMarker> rareOnly = MinimapItemAggregator.Compose(
    overlappingItems, circleProjection, ItemRarityMask.Rare, mergePixels: 8f, showOtherFloors: false);
Check(rareOnly.Count == 1 && rareOnly[0].InstanceId == 30,
    "Rarity filtering must happen before overlap aggregation so the next enabled tier remains visible.");

IReadOnlyList<MinimapItemMarker> winnerRemoved = MinimapItemAggregator.Compose(
    overlappingItems.Where(item => item.InstanceId != 10).ToArray(), circleProjection,
    ItemRarityMask.All, mergePixels: 8f, showOtherFloors: false);
Check(winnerRemoved.Count == 1 && winnerRemoved[0].InstanceId == 20,
    "Removing an overlap winner must promote the next stable candidate on the next snapshot.");

GroundItemSnapshot[] stackedFloors =
[
    new(40, new MinimapWorldPoint(2f, 10f, 2f), MinimapItemRarity.Common),
    new(41, new MinimapWorldPoint(2f, 22f, 2f), MinimapItemRarity.Mythical)
];
IReadOnlyList<MinimapItemMarker> currentFloor = MinimapItemAggregator.Compose(
    stackedFloors, circleProjection, ItemRarityMask.All, mergePixels: 8f, showOtherFloors: false);
Check(currentFloor.Count == 1 && currentFloor[0].InstanceId == 40,
    "An enabled item on another floor must not hide the current-floor item at the same X/Z.");
IReadOnlyList<MinimapItemMarker> allFloors = MinimapItemAggregator.Compose(
    stackedFloors, circleProjection, ItemRarityMask.All, mergePixels: 8f, showOtherFloors: true);
Check(allFloors.Count == 2,
    "When other-floor arrows are enabled, distinct floor bands must not aggregate into one marker.");

Check(MinimapMarkerPolicies.ShouldShowNativeMarker(isLocalPlayer: true, isSameGroup: false, isMarkerPlaced: true),
    "The local player's placed native marker must be visible.");
Check(MinimapMarkerPolicies.ShouldShowNativeMarker(isLocalPlayer: false, isSameGroup: true, isMarkerPlaced: true),
    "A same-group placed native marker must be visible.");
Check(!MinimapMarkerPolicies.ShouldShowNativeMarker(isLocalPlayer: false, isSameGroup: false, isMarkerPlaced: true),
    "A non-group native marker must not leak into the minimap.");
Check(!MinimapMarkerPolicies.ShouldShowNativeMarker(isLocalPlayer: true, isSameGroup: true, isMarkerPlaced: false),
    "An unplaced native marker must stay hidden.");
Check(MinimapMarkerPolicies.ShouldShowTeleportMarker("PEI", "pei") &&
      !MinimapMarkerPolicies.ShouldShowTeleportMarker("PEI", "Washington"),
    "Teleport markers must match only the current map, ignoring case.");

VerticalOcclusionInterval[] separatedFloors =
[
    new(2f, 3f),
    new(6f, 7f),
    new(10f, 11f)
];
OcclusionProbeSummary separatedSummary = OverheadOcclusionModel.EvaluateProbe(
    separatedFloors, mergeGap: 0.25f, floorExclusionMaxThickness: 1.25f);
Check(separatedSummary.EffectiveThickness == 0f,
    "Separated ordinary floors must not be added into one thick obstruction.");
OcclusionProbeSummary thinFloor = OverheadOcclusionModel.EvaluateProbe(
    [new VerticalOcclusionInterval(2f, 3f)], mergeGap: 0.25f, floorExclusionMaxThickness: 1.25f);
Check(thinFloor.EffectiveThickness == 0f,
    "One ordinary thin floor must not count as a cave roof.");

VerticalOcclusionInterval[] thickRoof = [new(2f, 4.6f)];
OcclusionProbeSummary thickSummary = OverheadOcclusionModel.EvaluateProbe(
    thickRoof, mergeGap: 0.25f, floorExclusionMaxThickness: 1.25f);
Check(Math.Abs(thickSummary.EffectiveThickness - 2.6f) < 0.001f,
    "A continuous roof thicker than the floor exclusion threshold must remain measurable.");

OcclusionProbeSummary[] fiveProbes =
[
    thickSummary,
    thickSummary,
    thickSummary,
    separatedSummary,
    separatedSummary
];
OcclusionSummary majority = OverheadOcclusionModel.Combine(fiveProbes, requiredProbeCount: 3,
    enterThickness: 2.5f, exitThickness: 1.5f);
Check(majority.MeetsEnterThreshold && !majority.MeetsExitThreshold,
    "Three of five thick probes must satisfy the enter threshold without satisfying the exit condition.");
OcclusionSummary minority = OverheadOcclusionModel.Combine(
    [thickSummary, thickSummary, separatedSummary, separatedSummary, separatedSummary], 3, 2.5f, 1.5f);
Check(!minority.MeetsEnterThreshold,
    "Fewer than three of five thick probes must not enter underground mode.");

BackgroundStateMachine background = new();
Check(background.Update(0f, MinimapBackgroundMode.AutoNativeFirst, nativeMapAvailable: true,
          realtimeAvailable: true, majority, enterDelaySeconds: 0.75f, exitDelaySeconds: 1.5f) == MinimapBackgroundState.NativeMapCrop,
    "A single thick sample must not immediately enter realtime height slicing.");
Check(background.Update(0.74f, MinimapBackgroundMode.AutoNativeFirst, true, true, majority, 0.75f, 1.5f) == MinimapBackgroundState.NativeMapCrop,
    "The enter delay must be fully satisfied before switching backgrounds.");
Check(background.Update(0.75f, MinimapBackgroundMode.AutoNativeFirst, true, true, majority, 0.75f, 1.5f) == MinimapBackgroundState.RealtimeHeightSlice,
    "Sustained thick obstruction must enter realtime height slicing at the documented delay.");

OcclusionSummary clear = OverheadOcclusionModel.Combine(
    [separatedSummary, separatedSummary, separatedSummary, separatedSummary, separatedSummary],
    requiredProbeCount: 3, enterThickness: 2.5f, exitThickness: 1.5f);
OcclusionSummary terrainCoveredRoom = OverheadOcclusionModel.ApplyUndergroundEvidence(
    clear, undergroundAllowlisted: false, terrainCoverDepth: 16f,
    enterThickness: 2.5f, exitThickness: 1.5f);
Check(terrainCoveredRoom.MeetsEnterThreshold && !terrainCoveredRoom.MeetsExitThreshold &&
      terrainCoveredRoom.MaximumEffectiveThickness == 16f,
    "A player substantially below the landscape must enter height-slice mode even under a thin prefab roof.");
OcclusionSummary authoredUndergroundRoom = OverheadOcclusionModel.ApplyUndergroundEvidence(
    clear, undergroundAllowlisted: true, terrainCoverDepth: 0f,
    enterThickness: 2.5f, exitThickness: 1.5f);
Check(authoredUndergroundRoom.MeetsEnterThreshold && !authoredUndergroundRoom.MeetsExitThreshold,
    "An authored UndergroundWhitelist volume must participate in automatic height-slice selection.");
OcclusionSummary ordinarySurfaceRoom = OverheadOcclusionModel.ApplyUndergroundEvidence(
    clear, undergroundAllowlisted: false, terrainCoverDepth: 0.5f,
    enterThickness: 2.5f, exitThickness: 1.5f);
Check(!ordinarySurfaceRoom.MeetsEnterThreshold && ordinarySurfaceRoom.MeetsExitThreshold,
    "A shallow terrain delta must not turn every ordinary above-ground house into a cutaway.");
OcclusionSummary unmappedThinRoofRoom = OverheadOcclusionModel.ApplyUnmappedInteriorEvidence(
    clear, nativeCropVisible: false, overheadSurfaceProbeCount: 3, requiredProbeCount: 3);
Check(unmappedThinRoofRoom.MeetsEnterThreshold && !unmappedThinRoofRoom.MeetsExitThreshold,
    "A stable thin roof outside all native-map pixels must recover a save-loaded interior without a teleport event.");
OcclusionSummary mappedThinRoofRoom = OverheadOcclusionModel.ApplyUnmappedInteriorEvidence(
    clear, nativeCropVisible: true, overheadSurfaceProbeCount: 5, requiredProbeCount: 3);
Check(!mappedThinRoofRoom.MeetsEnterThreshold && mappedThinRoofRoom.MeetsExitThreshold,
    "Thin roofs inside a visible native-map crop must keep the ordinary-building exclusion.");
OcclusionSummary unmappedOutdoor = OverheadOcclusionModel.ApplyUnmappedInteriorEvidence(
    clear, nativeCropVisible: false, overheadSurfaceProbeCount: 0, requiredProbeCount: 3);
Check(!unmappedOutdoor.MeetsEnterThreshold && unmappedOutdoor.MeetsExitThreshold,
    "Walking outdoors beyond native-map pixels must not enable realtime mode without overhead evidence.");
Check(Math.Abs(MinimapRenderPolicy.ResolveCutawaySliceAbove(
          configuredAbove: 4f, playerY: 146f, headY: 147.6f) - 2.1f) < 0.001f &&
      MinimapRenderPolicy.ResolveCutawaySliceAbove(
          configuredAbove: 0.75f, playerY: 146f, headY: 147.6f) == 0.75f,
    "The underground cut plane must stay just above the player's head while respecting a lower explicit setting.");
float detectedCeilingY = OverheadOcclusionModel.ResolveCutawayCeilingY(
    [150.3f, 150.2f, 151f, 150.4f, 999f]);
Check(Math.Abs(detectedCeilingY - 150.4f) < 0.001f,
    "Cutaway ceiling selection must use the stable median instead of one low prop or high outlier.");
Check(Math.Abs(MinimapRenderPolicy.ResolveCutawaySliceAbove(
          configuredAbove: 4f, playerY: 148.3f, headY: 150.05f,
          detectedCeilingY: 150.3f) - 1.9f) < 0.001f,
    "The cut plane must move below the measured roof rather than remaining above it at the head-clearance estimate.");
float stableRoomSliceA = MinimapRenderPolicy.ResolveCutawaySliceAbove(
    configuredAbove: 4f, playerY: 148.3f, headY: 150.05f, detectedCeilingY: 153f);
float stableRoomSliceB = MinimapRenderPolicy.ResolveCutawaySliceAbove(
    configuredAbove: 4f, playerY: 149f, headY: 150.75f, detectedCeilingY: 153f);
Check(Math.Abs((148.3f + stableRoomSliceA) - 152.9f) < 0.001f &&
      Math.Abs((149f + stableRoomSliceB) - 152.9f) < 0.001f,
    "Jumping, crouching or flying inside one room must not move its measured absolute cut plane.");

MinimapInstanceCutaway instanceCutaway = new();
Check(instanceCutaway.Update(nativeCropVisible: false, realtimeDestinationLocked: true,
          overheadSurfaceProbeCount: 0, requiredProbeCount: 3,
          fallbackCutPlaneY: 149.65f, detectedCeilingY: float.NaN) &&
      instanceCutaway.Active && Math.Abs(instanceCutaway.CeilingY - 149.75f) < 0.001f,
    "An off-map teleport must lock a cutaway plane at the highest entry before the player reaches a roof.");
Check(instanceCutaway.Update(false, true, 0, 3, fallbackCutPlaneY: 143f,
          detectedCeilingY: float.NaN) && Math.Abs(instanceCutaway.CeilingY - 149.75f) < 0.001f,
    "Walking down a ramp must not lower the instance cutaway plane with player height.");
Check(instanceCutaway.Update(false, true, 5, 3, fallbackCutPlaneY: 143f,
          detectedCeilingY: 150.84f) && Math.Abs(instanceCutaway.CeilingY - 150.84f) < 0.001f,
    "The first measured prefab ceiling must replace the entry fallback plane.");
Check(instanceCutaway.Update(false, true, 5, 3, fallbackCutPlaneY: 138f,
          detectedCeilingY: 146f) && Math.Abs(instanceCutaway.CeilingY - 146f) < 0.001f,
    "A lower measured ceiling must push the instance plane down so no higher roof can reappear.");
Check(instanceCutaway.Update(false, true, 5, 3, fallbackCutPlaneY: 138f,
          detectedCeilingY: 160f) && Math.Abs(instanceCutaway.CeilingY - 146f) < 0.001f,
    "Higher roofs encountered later must never raise an established x-ray cut plane.");
OcclusionSummary directInstanceEvidence = OverheadOcclusionModel.ApplyUnmappedInteriorEvidence(
    clear, nativeCropVisible: false,
    overheadSurfaceProbeCount: instanceCutaway.Active ? 3 : 0, requiredProbeCount: 3);
BackgroundStateMachine directInstanceBackground = new();
Check(directInstanceBackground.Update(0f, MinimapBackgroundMode.AutoNativeFirst,
          nativeMapAvailable: false, realtimeAvailable: true, directInstanceEvidence,
          enterDelaySeconds: 0f, exitDelaySeconds: 0.25f) == MinimapBackgroundState.RealtimeHeightSlice,
    "A native teleport outside Map.png must switch directly to the existing height-slice renderer.");
Check(!instanceCutaway.Update(nativeCropVisible: true, realtimeDestinationLocked: false,
          overheadSurfaceProbeCount: 0, requiredProbeCount: 3,
          fallbackCutPlaneY: 0f, detectedCeilingY: float.NaN) && !instanceCutaway.Active,
    "Returning to visible native-map coverage must release the off-map instance cutaway lock.");

Check(background.Update(1f, MinimapBackgroundMode.AutoNativeFirst, true, true, clear, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeHeightSlice,
    "A transient clear sample must not immediately exit the height slice.");
Check(background.Update(1.24f, MinimapBackgroundMode.AutoNativeFirst, true, true, clear, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeHeightSlice,
    "The exit delay must be fully satisfied before restoring the native map.");
Check(background.Update(1.25f, MinimapBackgroundMode.AutoNativeFirst, true, true, clear, 0.75f, 0.25f) == MinimapBackgroundState.NativeMapCrop,
    "Sustained clear probes must restore the native map at the documented delay.");

Check(background.Update(3f, MinimapBackgroundMode.RealtimeSurface, true, true, clear, 0.75f, 1.5f) == MinimapBackgroundState.RealtimeSurface,
    "A manual realtime-surface override must bypass automatic occlusion selection.");
Check(background.Update(4f, MinimapBackgroundMode.AutoNativeFirst, false, false, clear, 0.75f, 1.5f) == MinimapBackgroundState.FallbackGrid,
    "When neither native nor realtime rendering is available, markers must remain over a controlled fallback grid.");
Check(background.Update(5f, MinimapBackgroundMode.NativeMapOnly, false, true, clear, 0.75f, 1.5f) == MinimapBackgroundState.FallbackGrid &&
      background.Update(6f, MinimapBackgroundMode.RealtimeHeightSlice, true, true, clear, 0.75f, 1.5f) == MinimapBackgroundState.RealtimeHeightSlice &&
      background.Update(7f, MinimapBackgroundMode.Disabled, true, true, clear, 0.75f, 1.5f) == MinimapBackgroundState.Disabled,
    "Every manual background mode must preserve its documented fallback/override semantics.");

string minimapFeatureSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Minimap", "MinimapFeature.cs"));
string overheadDetectorSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Minimap", "OverheadOcclusionDetector.cs"));
string sceneRendererSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Minimap", "MinimapSceneRenderer.cs"));
string minimapHudSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "UI", "MinimapHud.cs"));
Check(minimapFeatureSource.Contains("MinimapRenderPolicy.CanUseNativeMap", StringComparison.Ordinal) &&
      sceneRendererSource.Contains("camera.CopyFrom(playerCamera)", StringComparison.Ordinal) &&
      !sceneRendererSource.Contains("Shader.Find(\"Unlit/Texture\")", StringComparison.Ordinal) &&
      !sceneRendererSource.Contains("SetReplacementShader(", StringComparison.Ordinal) &&
      sceneRendererSource.Contains("camera.ResetReplacementShader()", StringComparison.Ordinal) &&
      sceneRendererSource.Contains("_terrainCamera.ResetReplacementShader()", StringComparison.Ordinal) &&
      sceneRendererSource.Contains("_objectCamera.ResetReplacementShader()", StringComparison.Ordinal) &&
      minimapHudSource.Contains("BackgroundUnderlay", StringComparison.Ordinal) &&
      minimapHudSource.Contains("NativeMapUnderlay", StringComparison.Ordinal) &&
      minimapHudSource.Contains("ApplyBackgroundViewport", StringComparison.Ordinal),
    "Live minimap wiring must retain edge crops and native materials without modifying the player camera.");
Check(!minimapHudSource.Contains("new Rect(0f, 1f, 1f, -1f)", StringComparison.Ordinal) &&
      minimapHudSource.Contains("SetRawImage(view.ObjectOverlay, showObjects ? objects : null, new Rect(0f, 0f, 1f, 1f)", StringComparison.Ordinal) &&
      minimapHudSource.Contains("SetRawImage(view.Background, terrain, new Rect(0f, 0f, 1f, 1f)", StringComparison.Ordinal),
    "Realtime terrain and loaded-object RenderTextures must preserve camera-local up as map north instead of applying a second vertical flip.");
Check(overheadDetectorSource.Contains("Landscape.getWorldHeight(head, out float terrainHeight)", StringComparison.Ordinal) &&
      overheadDetectorSource.Contains("ApplyUndergroundEvidence", StringComparison.Ordinal) &&
      overheadDetectorSource.Contains("overheadSurfaceProbeCount", StringComparison.Ordinal) &&
      overheadDetectorSource.Contains("cutawayCeilingCandidates", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("ApplyUnmappedInteriorEvidence", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("_occlusionDiagnostics.CutawayCeilingY", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("ResolveCutawaySliceAbove", StringComparison.Ordinal),
    "Runtime underground selection must combine landscape depth/author volume evidence and apply a player-head cut plane.");

Console.WriteLine("Minimap settings, projection, aggregation, and background-state smoke checks passed.");
BackgroundStateMachine outside = new();
Check(outside.Update(0f, MinimapBackgroundMode.AutoNativeFirst, false, true, majority, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeSurface &&
      outside.Update(0.75f, MinimapBackgroundMode.AutoNativeFirst, false, true, majority, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeHeightSlice,
    "Out-of-map interiors must still use sustained overhead detection to select a height slice.");
Check(outside.Update(1f, MinimapBackgroundMode.AutoNativeFirst, false, true, clear, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeHeightSlice &&
      outside.Update(1.25f, MinimapBackgroundMode.AutoNativeFirst, false, true, clear, 0.75f, 0.25f) == MinimapBackgroundState.RealtimeSurface,
    "Leaving an out-of-map interior must return to realtime surface, not an unavailable native map.");
MinimapTeleportRecovery recovery = new();
recovery.ObserveDestination(false, false, false);
Check(!recovery.NeedsRealtimeDestination, "Walking beyond image bounds must preserve native-map edge behaviour.");
Check(!recovery.ObservePosition(new MinimapWorldPoint(0, 0, 0)) &&
      !recovery.ObservePosition(new MinimapWorldPoint(1, 0, 1)) &&
      recovery.ObservePosition(new MinimapWorldPoint(64, 68, 3233)), "Large moves should invalidate position-based caches.");
recovery.ObserveDestination(false, false, false);
Check(!recovery.NeedsRealtimeDestination, "A position jump without a native teleport event must not switch map mode.");
recovery.ObserveDestination(true, false, false);
Check(recovery.NeedsRealtimeDestination, "Native teleport into an unmapped destination must recover with realtime rendering.");
recovery.ObserveDestination(false, false, false);
Check(recovery.NeedsRealtimeDestination, "Recovery must persist while the player remains at the unmapped destination.");
recovery.ObserveDestination(false, true, false);
Check(recovery.NeedsRealtimeDestination, "Zooming out until the crop touches the native image must not clear teleport recovery.");
recovery.ObserveDestination(false, true, true);
Check(!recovery.NeedsRealtimeDestination, "Returning to visible native coverage must restore ordinary native-first selection.");
recovery.ObserveDestination(true, true, false);
Check(!recovery.NeedsRealtimeDestination, "A teleport to a partially covered map edge must preserve native mode.");
recovery.Reset();
Check(!recovery.NeedsRealtimeDestination && !recovery.ObservePosition(new MinimapWorldPoint(1, 2, 3)), "Session reset must discard relocation history.");
Check(minimapFeatureSource.Contains("onPlayerTeleported += HandlePlayerTeleported", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("onPlayerTeleported -= HandlePlayerTeleported", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("sharedItemSnapshot = null", StringComparison.Ordinal) &&
      minimapFeatureSource.Contains("_settings.BackgroundMode == MinimapBackgroundMode.AutoNativeFirst", StringComparison.Ordinal),
    "Native teleport subscription must be reversible, refresh departure snapshots and leave explicit background modes alone.");
string worldMaintenanceBridgePath = FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Services", "WorldMaintenanceNativeBridge.cs");
string worldMaintenanceBridgeSource = File.ReadAllText(worldMaintenanceBridgePath);
string worldMaintenanceServiceSource = File.ReadAllText(FindRepositoryFile(
    "src", "UnturnedSingleplayerCheatMenu", "Services", "WorldMaintenanceService.cs"));
Check(worldMaintenanceBridgeSource.Contains("ZombieManager.sendZombieDead", StringComparison.Ordinal) &&
      !worldMaintenanceBridgeSource.Contains("Object.Destroy", StringComparison.Ordinal) &&
      !worldMaintenanceBridgeSource.Contains("ZombieRegion.destroy", StringComparison.Ordinal) &&
      !worldMaintenanceServiceSource.Contains("Object.Destroy", StringComparison.Ordinal) &&
      !worldMaintenanceServiceSource.Contains("ZombieRegion.destroy", StringComparison.Ordinal),
    "World maintenance must clear zombies only through the native death contract.");
Check(nearbyWorldScannerSource.Contains("zombie == null || zombie.isDead", StringComparison.Ordinal),
    "Dead zombies must remain excluded from minimap markers after maintenance.");
Check(!worldMaintenanceServiceSource.Contains("ZombieRegionRetentionPatch", StringComparison.Ordinal),
    "World maintenance must not mutate or suspend the retained-region policy.");
Console.WriteLine("Teleport recovery, unmapped interior and native-edge regression checks passed.");
