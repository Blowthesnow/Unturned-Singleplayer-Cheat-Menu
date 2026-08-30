using UnturnedSingleplayerCheatMenu.Minimap;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

MinimapSettings defaults = new MinimapSettings().Normalize();
Check(!defaults.Enabled && !defaults.ShowItems,
    "The minimap and item radar must remain opt-in on first launch.");
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
      defaults.UndergroundSliceAbove == 4f && defaults.UndergroundSliceBelow == 6f &&
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

Console.WriteLine("Minimap settings, projection, aggregation, and background-state smoke checks passed.");
