using UnturnedSingleplayerCheatMenu.UI;
using System.Runtime.InteropServices;
using System.Text;

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string sourceRoot = Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu");

static string Read(string path) => File.ReadAllText(path);
static string ReadMethodBody(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    Check(signatureIndex >= 0, $"Production method is missing: {signature}");
    int openBrace = source.IndexOf('{', signatureIndex);
    Check(openBrace >= 0, $"Production method body is missing: {signature}");
    int depth = 0;
    for (int index = openBrace; index < source.Length; index++)
    {
        if (source[index] == '{')
            depth++;
        else if (source[index] == '}' && --depth == 0)
            return source.Substring(openBrace, index - openBrace + 1);
    }
    throw new InvalidOperationException($"Production method body is unterminated: {signature}");
}
static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void CheckButtonTooltipCoverage(string source, string overlay)
{
    string buttonFactory = ReadMethodBody(overlay, "private Button CreateButton(");
    Check(source.Split("typeof(Button)", StringSplitOptions.None).Length - 1 == 1,
        "Production UI creates a Button outside the shared tooltip-aware factory.");
    Check(overlay.Contains("private Button CreateButton(Transform parent, string label, System.Action action, string tooltip,", StringComparison.Ordinal),
        "Button tooltips are still optional at the shared factory boundary.");
    Check(buttonFactory.Contains("ButtonTooltipText.Require(tooltip)", StringComparison.Ordinal),
        "The shared Button factory does not reject a blank hover explanation.");
    Check(!source.Contains("执行“{0}”操作。", StringComparison.Ordinal) &&
          !source.Contains("执行此按钮对应的操作。", StringComparison.Ordinal),
        "Button coverage still relies on a generic action-label fallback.");
    Check(ButtonTooltipText.Require("Apply the pending changes.") == "Apply the pending changes.",
        "An explicit Button explanation was altered by the validation boundary.");
    bool missingTooltipRejected = false;
    try
    {
        ButtonTooltipText.Require(null);
    }
    catch (ArgumentException)
    {
        missingTooltipRejected = true;
    }
    Check(missingTooltipRejected, "A Button can still be created without a hover explanation.");
    bool blankTooltipRejected = false;
    try
    {
        ButtonTooltipText.Require("   ");
    }
    catch (ArgumentException)
    {
        blankTooltipRejected = true;
    }
    Check(blankTooltipRejected, "A Button can still be created with a blank hover explanation.");
    string markerPickerFactory = ReadMethodBody(overlay, "private Button CreateMarkerPickerButton(");
    Check(markerPickerFactory.Contains("选择“{0}”传送标记样式。", StringComparison.Ordinal),
        "Marker picker Buttons still rely on the generic icon-only fallback explanation.");
    string colorPickerFactory = ReadMethodBody(overlay, "private Button CreateColorPickerButton(");
    Check(colorPickerFactory.Contains("打开传送标记颜色选择器。", StringComparison.Ordinal),
        "The marker color picker Button still relies on the generic icon-only fallback explanation.");
    string colorSwatchFactory = ReadMethodBody(overlay, "private void CreateColorSwatchButton(");
    Check(colorSwatchFactory.Contains("将此预设颜色载入当前传送标记颜色预览。", StringComparison.Ordinal),
        "Marker color swatch Buttons still rely on the generic icon-only fallback explanation.");
    string teleportPointsView = ReadMethodBody(overlay, "private void BuildTeleportPointsView(");
    Check(teleportPointsView.Contains("打开删除传送点“{0}”的确认窗口。", StringComparison.Ordinal),
        "Teleport delete Buttons are incorrectly described as window-close Buttons.");
}

IEnumerable<string> sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
string creatureHealthBarHudPath = Path.Combine(sourceRoot, "UI", "CreatureHealthBarHud.cs");
string source = string.Join("\n", sourceFiles.Select(Read));
string sourceWithoutCreatureHealthBarHud = string.Join("\n", sourceFiles
    .Where(path => !string.Equals(path, creatureHealthBarHudPath, StringComparison.OrdinalIgnoreCase))
    .Select(Read));
string overlayPath = Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.cs");
string overlay = string.Join("\n", Directory.EnumerateFiles(Path.Combine(sourceRoot, "UI"), "CheatMenuOverlayUi*.cs")
    .OrderBy(path => path, StringComparer.Ordinal)
    .Select(Read));
if (args.Contains("--button-tooltips-only", StringComparer.Ordinal))
{
    CheckButtonTooltipCoverage(source, overlay);
    Console.WriteLine("Button tooltip smoke checks passed.");
    return;
}
string project = Read(Path.Combine(sourceRoot, "UnturnedSingleplayerCheatMenu.csproj"));
string prepareReferences = Read(Path.Combine(repoRoot, "scripts", "Prepare-References.ps1"));
string teleportMapSurface = Read(Path.Combine(sourceRoot, "UI", "TeleportMapSurface.cs"));
string teleportMapService = Read(Path.Combine(sourceRoot, "Services", "TeleportMapService.cs"));
string minimapHud = Read(Path.Combine(sourceRoot, "UI", "MinimapHud.cs"));
string minimapMarkerPool = Read(Path.Combine(sourceRoot, "UI", "MinimapMarkerPool.cs"));
string minimapFeature = Read(Path.Combine(sourceRoot, "Minimap", "MinimapFeature.cs"));
string minimapRenderer = Read(Path.Combine(sourceRoot, "Minimap", "MinimapSceneRenderer.cs"));
string nativeMapCropProvider = Read(Path.Combine(sourceRoot, "Minimap", "NativeMapCropProvider.cs"));
string minimapToolsView = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.MinimapToolsView.cs"));
string globalShortcutActions = Read(Path.Combine(sourceRoot, "Services", "GlobalShortcutActions.cs"));
string globalShortcutManager = Read(Path.Combine(sourceRoot, "Services", "GlobalShortcutManager.cs"));
string translationToolsView = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.TranslationToolsView.cs"));
string toolsShell = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.ToolsShell.cs"));
string catalogViews = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.CatalogViews.cs"));
string autoPickupToolsView = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.AutoPickupToolsView.cs"));
string infiniteStorageView = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.InfiniteStorageView.cs"));
string overlayIconBinder = Read(Path.Combine(sourceRoot, "UI", "OverlayIconBinder.cs"));

string cardContents = ReadMethodBody(catalogViews, "private void AddCardContents(");
Check(cardContents.Contains("new Vector2(0.06f, 0.28f)", StringComparison.Ordinal) &&
      cardContents.Contains("new Vector2(0.94f, 0.97f)", StringComparison.Ordinal),
    "The shared card icon viewport does not own the enlarged upper 69% of the card.");
Check(cardContents.Contains("typeof(AspectRatioFitter)", StringComparison.Ordinal) &&
      cardContents.Contains("AspectRatioFitter.AspectMode.FitInParent", StringComparison.Ordinal),
    "Card icons can still be stretched instead of fitting their source aspect ratio.");
Check(cardContents.Contains("new Vector2(0.04f, 0.09f)", StringComparison.Ordinal) &&
      cardContents.Contains("new Vector2(0.96f, 0.28f)", StringComparison.Ordinal) &&
      cardContents.Contains("new Vector2(0.04f, 0.01f)", StringComparison.Ordinal) &&
      cardContents.Contains("new Vector2(0.96f, 0.09f)", StringComparison.Ordinal),
    "The shared card text bands are not constrained to the lower 28% of the card.");
Check(ReadMethodBody(catalogViews, "private void CreateItemCard(").Contains("AddCardContents(", StringComparison.Ordinal) &&
      ReadMethodBody(catalogViews, "private void CreateVehicleCard(").Contains("AddCardContents(", StringComparison.Ordinal) &&
      ReadMethodBody(autoPickupToolsView, "private void CreateAutoPickupItemCard(").Contains("AddCardContents(", StringComparison.Ordinal) &&
      ReadMethodBody(infiniteStorageView, "private void CreateInfiniteStorageCard(").Contains("AddCardContents(", StringComparison.Ordinal),
    "A catalog, favorite, auto-pickup, or infinite-storage card bypasses the shared layout contract.");
Check(overlayIconBinder.Contains("_aspectFitter.aspectRatio", StringComparison.Ordinal) &&
      overlayIconBinder.Contains("texture2D.width", StringComparison.Ordinal) &&
      overlayIconBinder.Contains("texture2D.height", StringComparison.Ordinal),
    "The icon binder does not update the fit ratio from the loaded texture dimensions.");

string[] residues =
{
    "CheatMenu" + "Ui",
    "On" + "GUI",
    "GUI" + "Layout",
    "GUI" + ".Window",
    "UnityEngine." + "IMGUIModule",
    "GameUpdate" + "Postfix",
    "PatchUpdate" + "Loop",
};
foreach (string residue in residues)
    Check(!source.Contains(residue, StringComparison.Ordinal), $"Deprecated UI/runtime residue remains in production source: {residue}");
Check(!source.Contains("using TMPro", StringComparison.Ordinal) &&
      !source.Contains("TMP_FontAsset", StringComparison.Ordinal) &&
      !source.Contains("TMP_InputField", StringComparison.Ordinal) &&
      !source.Contains("TextMeshProUGUI", StringComparison.Ordinal),
    "Production plugin UI still depends on the abandoned TMP rendering path.");
string uiText = Read(Path.Combine(sourceRoot, "UI", "UiText.cs"));
Check(uiText.Contains("class UiText : Text", StringComparison.Ordinal) &&
      uiText.Contains("using UnityEngine.UI", StringComparison.Ordinal),
    "Plugin text is not backed by the legacy uGUI Text component used by the working release.");
string builtinFontLookup = "Resources.GetBuiltinResource" + "<Font>";
string builtinArialName = "Arial" + ".ttf";
Check(!sourceWithoutCreatureHealthBarHud.Contains(builtinFontLookup, StringComparison.Ordinal), "Production UI outside the tooltip-feature health-bar baseline still resolves a built-in font.");
Check(!sourceWithoutCreatureHealthBarHud.Contains(builtinArialName, StringComparison.Ordinal), "Production UI outside the tooltip-feature health-bar baseline still references the legacy built-in font asset.");

string imguiModule = "UnityEngine." + "IMGUIModule";
Check(!project.Contains(imguiModule, StringComparison.Ordinal), "The project still references the legacy IMGUI module.");
Check(!project.Contains("Unity.TextMeshPro", StringComparison.Ordinal) &&
      !project.Contains("UnityEngine.TextCoreFontEngineModule", StringComparison.Ordinal) &&
      !project.Contains("UnityEngine.TextCoreTextEngineModule", StringComparison.Ordinal),
    "The production plugin project still references TMP/TextCore assemblies.");
Check(!prepareReferences.Contains($"'{imguiModule}' =", StringComparison.Ordinal), "Reference preparation still copies the legacy IMGUI module.");
Check(prepareReferences.Contains("RemoveRetiredReferences", StringComparison.Ordinal), "Reference preparation does not detect retired local assemblies.");

Check(teleportMapSurface.Contains("PlayerDashboardInformationUI.icons.load<Texture2D>(\"Player\")", StringComparison.Ordinal),
    "Teleport map player marker does not load Unturned's native Player icon.");
Check(teleportMapSurface.Contains("_ownsPlayerMarkerTexture", StringComparison.Ordinal) &&
      teleportMapSurface.Contains("if (_ownsPlayerMarkerTexture && _playerMarkerTexture != null)", StringComparison.Ordinal),
    "Teleport map does not protect the shared native player-marker texture from destruction.");
Check(!teleportMapSurface.Contains("PlayerMarkerCenter", StringComparison.Ordinal),
    "Teleport map still overlays the legacy two-circle player marker over the native icon.");
Check(teleportMapSurface.Contains("TryGetLocalPlayerMapRotation", StringComparison.Ordinal) &&
      teleportMapSurface.Contains("Quaternion.Euler(0f, 0f, -rotation)", StringComparison.Ordinal),
    "Teleport map native player marker does not follow the original map-heading behavior.");
Check(teleportMapService.Contains("player.transform.rotation.eulerAngles.y", StringComparison.Ordinal) &&
      teleportMapService.Contains("mainVolume.transform.eulerAngles.y", StringComparison.Ordinal),
    "Teleport map heading projection does not account for the active cartography volume rotation.");
Check(teleportMapService.Contains("0.5f - vector.z", StringComparison.Ordinal) &&
      teleportMapService.Contains("0.5f - worldPosition.z / num", StringComparison.Ordinal) &&
      teleportMapService.Contains("0.5f - mapPosition.y", StringComparison.Ordinal) &&
      !teleportMapService.Contains("vector.z + 0.5f", StringComparison.Ordinal),
    "Teleport map projection no longer matches the tooltip-feature baseline and is vertically mirrored.");
string nativeMapCapture = ReadMethodBody(nativeMapCropProvider, "public NativeMapCropSnapshot Capture(Vector3 center, float radius)");
Check(nativeMapCropProvider.Contains("ProjectWorldPositionToNativeMap", StringComparison.Ordinal) &&
      nativeMapCropProvider.Contains("local.z + 0.5f", StringComparison.Ordinal) &&
      nativeMapCropProvider.Contains("worldPosition.z / levelSize + 0.5f", StringComparison.Ordinal) &&
      !nativeMapCapture.Contains("TeleportMapService.ProjectWorldPositionToMap", StringComparison.Ordinal),
    "Native minimap crop still shares the teleport-map projection instead of owning its raw-texture UV contract.");

string root = Read(Path.Combine(sourceRoot, "UI", "PluginUiRoot.cs"));
foreach (string layer in new[] { "WindowLayer", "ModalLayer", "PopupLayer", "TooltipLayer", "HudLayer", "ToastLayer" })
    Check(root.Contains($"CreateLayer(\"{layer}\")", StringComparison.Ordinal), $"UiRoot layer is missing: {layer}");
Check(root.Contains("typeof(Canvas)", StringComparison.Ordinal), "UiRoot does not own the plugin Canvas.");
Check(root.Contains("DontDestroyOnLoad(Root)", StringComparison.Ordinal), "UiRoot is not persistent across scenes.");
Check(root.Contains("public void Dispose()", StringComparison.Ordinal), "UiRoot teardown is missing.");
string rootPump = ReadMethodBody(root, "internal void Pump()");
Check(rootPump.Contains("EnsureInitialized()", StringComparison.Ordinal),
    "UiRoot cannot recreate itself after Unturned destroys bootstrap-scene Unity objects.");
string rootSceneChanged = ReadMethodBody(root, "internal void HandleSceneChanged()");
Check(rootSceneChanged.Contains("EnsureInitialized()", StringComparison.Ordinal),
    "UiRoot scene handling does not restore a destroyed bootstrap-scene root.");
Check(!rootPump.Contains("FindObjectsOfType<EventSystem>", StringComparison.Ordinal),
    "UiRoot still performs a global EventSystem search from its Update/Pump path.");
Check(root.Contains("SDG.Unturned.Glazier_uGUI", StringComparison.Ordinal), "UiRoot does not identify the supported Glazier uGUI backend.");
Check(root.Contains("Refused to open", StringComparison.Ordinal), "Unsupported Glazier backends have no explicit diagnostic.");
Check(root.Contains("_reportedUnsupportedGlazierBackends.Add(backendType)", StringComparison.Ordinal),
    "Unsupported Glazier diagnostics are not deduplicated by backend type.");
Check(root.Contains("_hudGroup.blocksRaycasts = false", StringComparison.Ordinal), "HUD layer does not enforce click-through raycasts.");
Check(root.Contains("SetNativeUiCoexistence", StringComparison.Ordinal), "UiRoot cannot suspend plugin window interaction for native UI.");
Check(minimapHud.Contains("_root.transform.SetParent(_uiRoot.HudLayer, false)", StringComparison.Ordinal),
    "Minimap is not parented under the one shared UiRoot HUD layer.");
Check(!minimapHud.Contains("typeof(Canvas)", StringComparison.Ordinal) &&
      !minimapHud.Contains("EventSystem", StringComparison.Ordinal) &&
      !minimapHud.Contains("DontDestroyOnLoad", StringComparison.Ordinal),
    "Minimap creates an independent Canvas/EventSystem/persistent UI root.");
Check(minimapHud.Contains("_circle = CreateView(\"CircleViewport\"", StringComparison.Ordinal) &&
      minimapHud.Contains("_square = CreateView(\"SquareViewport\"", StringComparison.Ordinal) &&
      minimapHud.Contains("_circle.Root.SetActive(_settings.Shape == MinimapShape.Circle)", StringComparison.Ordinal) &&
      minimapHud.Contains("_square.Root.SetActive(_settings.Shape == MinimapShape.Square)", StringComparison.Ordinal),
    "Circle and square minimap viewports are not pre-created and switched without rebuilds.");
Check(minimapHud.Contains("CreateText(\"Coordinates\", _root.transform", StringComparison.Ordinal),
    "Coordinates are inside the clipped viewport instead of the outer minimap root.");
Check(minimapHud.Contains("_dragSurface.raycastTarget = _settings.LayoutEditMode", StringComparison.Ordinal) &&
      minimapHud.Contains("_uiRoot.SetHudInteraction(visible && _settings.LayoutEditMode)", StringComparison.Ordinal),
    "Minimap click-through is not limited to explicit layout edit mode.");
Check(minimapHud.Contains("PlayerUI.message(EPlayerMessage.NPC_CUSTOM, LayoutPromptText", StringComparison.Ordinal) &&
      minimapHud.Contains("Input.GetKeyDown(KeyCode.Return)", StringComparison.Ordinal) &&
      minimapHud.Contains("Input.GetKeyDown(KeyCode.KeypadEnter)", StringComparison.Ordinal) &&
      minimapToolsView.Contains("_shellPresenter.CloseMenu()", StringComparison.Ordinal),
    "Layout editing does not hide the plugin window or use the native bottom-center HUD with Enter confirmation.");
string minimapViewportBorder = ReadMethodBody(minimapHud, "private void CreateViewportBorder(Transform parent, bool circle)");
Check(minimapHud.Contains("CreateCircleBorderSprite", StringComparison.Ordinal) &&
      minimapViewportBorder.Contains("_circleBorderSprite", StringComparison.Ordinal) &&
      minimapViewportBorder.Contains("ViewportBorder.Top", StringComparison.Ordinal) &&
      minimapViewportBorder.Contains("ViewportBorder.Bottom", StringComparison.Ordinal) &&
      minimapViewportBorder.Contains("ViewportBorder.Left", StringComparison.Ordinal) &&
      minimapViewportBorder.Contains("ViewportBorder.Right", StringComparison.Ordinal) &&
      !minimapViewportBorder.Contains("Outline", StringComparison.Ordinal),
    "The persistent minimap border is not an independent transparent-center ring/edge stroke.");
Check(minimapHud.Contains("_uiRoot.QueueHudDestroy(_root)", StringComparison.Ordinal),
    "Minimap teardown bypasses the lifecycle-safe deferred HUD destroy queue.");
Check(minimapHud.Contains("SetRawImage(view.ObjectOverlay, showObjects ? objects : null, new Rect(0f, 1f, 1f, -1f)", StringComparison.Ordinal),
    "The loaded-object camera texture is not using the same vertical orientation as the terrain camera texture.");
Check(minimapHud.Contains("MinimapRenderPolicy.ShowsLoadedObjects(state, objects != null)", StringComparison.Ordinal) &&
      minimapHud.Contains("showObjects ? objects : null", StringComparison.Ordinal),
    "The HUD does not clear stale scene-camera textures when returning to the native map background.");
string nativeIconBind = ReadMethodBody(minimapHud, "private void TryBindNativeIcons()");
Check(nativeIconBind.Contains("PlayerDashboardInformationUI.icons.load<Texture2D>(\"Player\")", StringComparison.Ordinal) &&
      nativeIconBind.Contains("PlayerDashboardInformationUI.icons.load<Texture2D>(\"Marker\")", StringComparison.Ordinal) &&
      !nativeIconBind.Contains("_ownedTextures.Add", StringComparison.Ordinal),
    "Minimap does not borrow the native Player/Marker textures without assuming texture ownership.");
Check(minimapMarkerPool.Contains("private readonly List<Slot> _slots", StringComparison.Ordinal) &&
      minimapMarkerPool.Contains("Slot slot = GetSlot(index)", StringComparison.Ordinal) &&
      minimapMarkerPool.Contains("_slots[index].Root.SetActive(false)", StringComparison.Ordinal),
    "Minimap markers are rebuilt instead of reused through the marker pool.");
Check(minimapRenderer.Contains("internal const int DefaultResolution = 512", StringComparison.Ordinal) &&
      minimapRenderer.Contains("camera.enabled = false", StringComparison.Ordinal) &&
      minimapRenderer.Contains("camera.allowMSAA = true", StringComparison.Ordinal) &&
      minimapRenderer.Contains("antiAliasing = 2", StringComparison.Ordinal) &&
      minimapRenderer.Contains("resolution, resolution, 24", StringComparison.Ordinal) &&
      minimapRenderer.Contains("MinimapRenderPolicy.SnapCenter", StringComparison.Ordinal) &&
      minimapRenderer.Contains("Quaternion.Euler(0f, mapYaw, 0f) * Quaternion.Euler(90f, 0f, 0f)", StringComparison.Ordinal) &&
      !minimapRenderer.Contains("ClearTexture(", StringComparison.Ordinal) &&
      minimapRenderer.Contains("_terrainCamera.enabled = renderTerrain", StringComparison.Ordinal) &&
      minimapRenderer.Contains("_objectCamera.enabled = renderObjects", StringComparison.Ordinal) &&
      minimapRenderer.Contains("texture.Release()", StringComparison.Ordinal) &&
      minimapRenderer.Contains("UnityEngine.Object.Destroy(texture)", StringComparison.Ordinal),
    "Conditional minimap cameras are not armed for one Unity render frame or their render textures are not released.");
Check(minimapRenderer.Contains("Shader.Find(\"Unlit/Texture\")", StringComparison.Ordinal) &&
      minimapRenderer.Contains("if (_replacementShader != null)", StringComparison.Ordinal) &&
      minimapRenderer.Contains("camera.SetReplacementShader(_replacementShader, string.Empty)", StringComparison.Ordinal) &&
      minimapRenderer.Contains("texture.IsCreated()", StringComparison.Ordinal) &&
      !minimapRenderer.Contains("Unity compatibility shader 'Unlit/Texture' is unavailable.", StringComparison.Ordinal) &&
      !minimapRenderer.Contains("RenderWithShader", StringComparison.Ordinal),
    "Realtime minimap cameras treat an optional compatibility shader as mandatory or bypass Unity's normal camera render loop.");
Check(minimapRenderer.Contains("internal void BeginFrame()", StringComparison.Ordinal) &&
      minimapFeature.Contains("_sceneRenderer.BeginFrame();", StringComparison.Ordinal),
    "The one-frame minimap camera activation is not retired at the start of the next plugin update.");
string minimapRendererInitialize = ReadMethodBody(minimapRenderer, "private void Initialize(int resolution)");
Check(!minimapRendererInitialize.Contains("RayMasks.DEFAULT", StringComparison.Ordinal) &&
      !minimapRendererInitialize.Contains("RayMasks.PLAYER", StringComparison.Ordinal) &&
      !minimapRendererInitialize.Contains("RayMasks.ENEMY", StringComparison.Ordinal) &&
      !minimapRendererInitialize.Contains("RayMasks.AGENT", StringComparison.Ordinal) &&
      !minimapRendererInitialize.Contains("RayMasks.ITEM", StringComparison.Ordinal),
    "The minimap object camera includes a broad/default character, creature, or item layer.");
Check(minimapFeature.Contains("_hud.SetRuntimeVisible(active)", StringComparison.Ordinal) &&
      !ReadMethodBody(minimapFeature, "internal void Update(float now, NearbyItemSnapshot sharedItemSnapshot = null)")
          .Contains("_hud.ApplySettings", StringComparison.Ordinal),
    "The minimap reapplies layout settings every frame and can reset an active drag.");
Check(minimapFeature.Contains("_nextMarkerProjectionTime = now + 0.05f", StringComparison.Ordinal) &&
      minimapFeature.Contains("marker.FollowTarget != null ? marker.FollowTarget.position", StringComparison.Ordinal),
    "Moving minimap markers are not reprojected from live transforms at the 20Hz pose budget.");
Check(ReadMethodBody(minimapFeature, "internal void ApplySettings(MinimapSettings settings)")
        .Contains("_nextRenderTime = 0f", StringComparison.Ordinal),
    "Changing the minimap background mode does not invalidate the scheduled camera render immediately.");
string minimapUpdate = ReadMethodBody(minimapFeature, "internal void Update(float now, NearbyItemSnapshot sharedItemSnapshot = null)");
Check(minimapUpdate.Contains("MinimapRenderPolicy.UsesSceneCameras(_state)", StringComparison.Ordinal) &&
      minimapUpdate.Contains("MinimapRenderPolicy.ShowsLoadedObjects(_state, _settings.ShowLoadedBuildings)", StringComparison.Ordinal) &&
      !minimapUpdate.Contains("_state == MinimapBackgroundState.NativeMapCrop && _settings.ShowLoadedBuildings", StringComparison.Ordinal),
    "Native map crop still schedules or displays the loaded-object scene camera.");
Check(minimapFeature.Contains("!PointToolService.IsInteractionPaused()", StringComparison.Ordinal) &&
      minimapFeature.Contains("_settings.LayoutEditMode || !PointToolService.IsInteractionPaused()", StringComparison.Ordinal),
    "Minimap visibility does not reuse the crosshair interaction tool's HUD hiding policy.");

foreach (string file in new[] { "CheatMenuOverlayUi.cs", "PointToolHud.cs" })
{
    string text = Read(Path.Combine(sourceRoot, "UI", file));
    Check(!text.Contains("typeof(Canvas)", StringComparison.Ordinal), $"{file} creates an independent Canvas.");
    Check(!text.Contains("DontDestroyOnLoad", StringComparison.Ordinal), $"{file} owns a persistent root instead of UiRoot.");
}
string creatureHealthBarHud = Read(creatureHealthBarHudPath);
Check(creatureHealthBarHud.Contains("CanvasScaler.ScaleMode.ConstantPixelSize", StringComparison.Ordinal)
      && creatureHealthBarHud.Contains("scaler.scaleFactor = 1f", StringComparison.Ordinal),
    "CreatureHealthBarHud no longer preserves the tooltip-feature pixel-space scaling contract.");

string context = Read(Path.Combine(sourceRoot, "Services", "PluginRuntimeContext.cs"));
string runtimeComposition = Read(Path.Combine(sourceRoot, "Services", "PluginRuntimeComposition.cs"));
string plugin = Read(Path.Combine(sourceRoot, "CheatMenuPlugin.cs"));
Check(context.Contains("_minimap?.Update(Time.realtimeSinceStartup, sharedItemSnapshot)", StringComparison.Ordinal) &&
      context.Contains("_minimap?.LateUpdate()", StringComparison.Ordinal) &&
      context.Contains("_minimap?.HandleSceneChanged()", StringComparison.Ordinal) &&
      context.Contains("RunTeardownStep(\"minimap\", () => _minimap?.Dispose())", StringComparison.Ordinal),
    "RuntimeContext does not own the minimap update, late-update, scene, and teardown lifecycle.");
Check(context.Contains("Mathf.Max(radius, autoRadius)", StringComparison.Ordinal) &&
      context.Contains("Mathf.Max(frequency, autoFrequency)", StringComparison.Ordinal) &&
      context.Contains("_nearbyItemIndex.Refresh", StringComparison.Ordinal),
    "Minimap and auto-pickup do not share a max-radius/max-frequency raw item scan.");
Check(!File.Exists(Path.Combine(sourceRoot, "Services", "PluginRuntimeDriver.cs")),
    "Runtime updates still depend on a transient Unity driver that Unturned destroys before Start/Update.");
string runtimeUpdatePatchPath = Path.Combine(sourceRoot, "Patches", "RuntimeUpdatePatch.cs");
Check(File.Exists(runtimeUpdatePatchPath),
    "The runtime has no static game Update bridge that survives destruction of bootstrap Unity components.");
string runtimeUpdatePatch = Read(runtimeUpdatePatchPath);
foreach (string targetType in new[] { "typeof(Provider)", "typeof(MenuUI)", "typeof(PlayerUI)" })
    Check(runtimeUpdatePatch.Contains(targetType, StringComparison.Ordinal), $"Runtime Update bridge is missing the proven legacy target: {targetType}");
string runtimePostfix = ReadMethodBody(runtimeUpdatePatch, "private static void Postfix(");
Check(runtimePostfix.Contains("context?.Update()", StringComparison.Ordinal) &&
      runtimePostfix.Contains("context?.LateUpdate()", StringComparison.Ordinal),
    "The static game Update bridge does not dispatch the shared Update and LateUpdate loops.");
Check(!plugin.Contains("private void Update(", StringComparison.Ordinal) &&
      !plugin.Contains("private void LateUpdate(", StringComparison.Ordinal),
    "The plugin still declares an unverified duplicate Unity frame callback.");
Check(context.Contains("RuntimeUpdatePatch.Install(_harmony, _log, this)", StringComparison.Ordinal),
    "RuntimeContext does not install the static game Update bridge.");
Check(context.Contains("RuntimeUpdatePatch.Reset(this)", StringComparison.Ordinal),
    "RuntimeContext teardown does not release the static game Update bridge reference.");
Check(context.Contains("Application.quitting += HandleApplicationQuitting", StringComparison.Ordinal) &&
      context.Contains("Application.quitting -= HandleApplicationQuitting", StringComparison.Ordinal),
    "The runtime cannot clean up on real application exit after the bootstrap plugin component is destroyed.");
string pluginOnDestroy = ReadMethodBody(plugin, "private void OnDestroy()");
Check(!pluginOnDestroy.Contains("Teardown(", StringComparison.Ordinal),
    "Unturned's startup scene transition still tears down the static runtime bridge.");
Check(context.Contains("MovementCheatService _movement", StringComparison.Ordinal), "RuntimeContext does not own movement state.");
Check(context.Contains("Harmony _harmony", StringComparison.Ordinal), "RuntimeContext does not own Harmony.");
Check(context.Contains("SceneManager.sceneLoaded +=", StringComparison.Ordinal), "RuntimeContext does not subscribe to sceneLoaded.");
Check(context.Contains("SceneManager.activeSceneChanged +=", StringComparison.Ordinal), "RuntimeContext does not subscribe to activeSceneChanged.");
Check(context.Contains("_inputCapture.NativeUiChanged +=", StringComparison.Ordinal), "RuntimeContext does not react to native PlayerUI window replacement.");
Check(context.Contains("_inputCapture.StateChanged +=", StringComparison.Ordinal), "RuntimeContext does not react to native UI coexistence transitions.");
Check(context.Contains("UiInputOwnershipState.NativeUiCoexistence", StringComparison.Ordinal), "RuntimeContext does not suppress plugin input during native UI coexistence.");
Check(context.Contains("_movement?.Restore()", StringComparison.Ordinal), "Runtime teardown does not restore movement.");
Check(context.Contains("_teleportMaps?.Dispose()", StringComparison.Ordinal), "Runtime teardown does not dispose teleport maps.");
Check(context.Contains("_icons?.Clear()", StringComparison.Ordinal), "Runtime teardown does not clear icon textures.");
Check(context.Contains("OwnRuntime(PluginRuntimeComposition composition)", StringComparison.Ordinal),
    "Runtime ownership still crosses the composition boundary as a long parameter list.");
Check(runtimeComposition.Contains("internal sealed class PluginRuntimeComposition", StringComparison.Ordinal) &&
      runtimeComposition.Contains("internal WorkshopLocalizationService WorkshopLocalization { get; }", StringComparison.Ordinal),
    "The runtime composition is not represented by an immutable ownership bundle.");
Check(plugin.Contains("private bool _isTornDown", StringComparison.Ordinal), "Plugin teardown is not guarded for idempotence.");
Check(plugin.Contains("Awake initialization failed", StringComparison.Ordinal), "Awake does not enter unified cleanup after initialization failure.");
Check(plugin.Contains("PluginConfigInitializationTransaction.Run(base.Config, base.Logger, InitializeProtected)", StringComparison.Ordinal),
    "The real plugin caller does not protect its complete configuration binding session.");
Check(plugin.Contains("Config.Bind(\"Minimap\", \"LayoutEditMode\", defaultValue: false", StringComparison.Ordinal) &&
      plugin.Contains("_minimapLayoutEditMode.Value = settings.LayoutEditMode", StringComparison.Ordinal),
    "Minimap layout edit mode is not persisted through the protected BepInEx configuration session.");
Check(plugin.Contains("Config.Bind(\"Minimap\", \"SchemaVersion\", 0", StringComparison.Ordinal) &&
      plugin.Contains("_minimapShowOtherFloors.Value = true", StringComparison.Ordinal) &&
      plugin.Contains("_minimapSchemaVersion.Value = CurrentMinimapSchemaVersion", StringComparison.Ordinal),
    "Existing minimap configs are not migrated to the new all-floor default exactly once.");
Check(plugin.Contains("SynchronizeMinimapLayoutInput(settings.LayoutEditMode)", StringComparison.Ordinal) &&
      plugin.Contains("if (!IsMinimapLayoutEditActive) ReleaseMenuInput()", StringComparison.Ordinal) &&
      context.Contains("_plugin.IsMinimapLayoutEditActive", StringComparison.Ordinal),
    "The hidden-window minimap layout editor does not retain and release the shared plugin input capture safely.");
Check(plugin.Contains("WarnInvalidMinimapConfiguration", StringComparison.Ordinal) &&
      plugin.Contains("configuration could not be read; safe defaults are active", StringComparison.Ordinal),
    "Invalid minimap settings do not fall back with one explicit diagnostic.");
Check(globalShortcutActions.Contains("ToggleMinimap", StringComparison.Ordinal) &&
      globalShortcutActions.Contains("ZoomMinimapIn", StringComparison.Ordinal) &&
      globalShortcutActions.Contains("ZoomMinimapOut", StringComparison.Ordinal) &&
      plugin.Contains("GlobalShortcutActions.ToggleMinimap", StringComparison.Ordinal) &&
      plugin.Contains("new KeyboardShortcut(KeyCode.None)", StringComparison.Ordinal) &&
      plugin.Contains("RegisterMouseWheel(GlobalShortcutActions.ZoomMinimapIn", StringComparison.Ordinal) &&
      plugin.Contains("RegisterMouseWheel(GlobalShortcutActions.ZoomMinimapOut", StringComparison.Ordinal),
    "Minimap toggle and fixed Alt+wheel zoom gestures are not registered in the global shortcut system.");
Check(globalShortcutManager.Contains("Input.mouseScrollDelta.y", StringComparison.Ordinal) &&
      globalShortcutManager.Contains("ShortcutMouseWheelDirection.Up", StringComparison.Ordinal) &&
      globalShortcutManager.Contains("ShortcutMouseWheelDirection.Down", StringComparison.Ordinal) &&
      globalShortcutManager.Contains("IsFixedGesture", StringComparison.Ordinal) &&
      plugin.Contains("_shortcutManager?.IsFixedGesture(actionId)", StringComparison.Ordinal),
    "Global shortcut dispatch does not own both mouse-wheel directions.");
Check(plugin.Contains("_minimapOcclusionProbeHz.Value == 2f", StringComparison.Ordinal) &&
      plugin.Contains("_minimapOcclusionExitDelay.Value == 1.5f", StringComparison.Ordinal) &&
      plugin.Contains("_minimapOcclusionProbeHz.Value = 5f", StringComparison.Ordinal) &&
      plugin.Contains("_minimapOcclusionExitDelay.Value = 0.25f", StringComparison.Ordinal),
    "Existing minimap configs using the slow legacy defaults are not migrated without overwriting custom values.");
Check(plugin.Contains("private void InitializeProtected()", StringComparison.Ordinal),
    "The final configuration binding path still carries a migration snapshot.");
string setMenuOpen = ReadMethodBody(plugin, "internal void SetMenuOpen(bool open)");
string setInfiniteStorageOpen = ReadMethodBody(plugin, "internal void SetInfiniteStorageOpen(bool open)");
Check(setMenuOpen.Contains("CanOpenWindow", StringComparison.Ordinal), "Main menu opening bypasses the Glazier backend guard.");
Check(setInfiniteStorageOpen.Contains("CanOpenWindow", StringComparison.Ordinal), "Infinite storage opening bypasses the Glazier backend guard.");
foreach (string signature in new[]
{
    "private void NormalizeConfiguredToggleShortcut()",
    "private void NormalizeConfiguredInventoryShortcut(ConfigEntry<KeyboardShortcut> entry)",
    "private void NormalizeConfiguredInfiniteStorageShortcut()",
})
{
    string methodBody = ReadMethodBody(plugin, signature);
    Check(!methodBody.Contains("base.Config.Save()", StringComparison.Ordinal),
        $"Startup shortcut normalization bypasses the configuration transaction: {signature}");
}
string configTransaction = Read(Path.Combine(sourceRoot, "Services", "PluginConfigInitializationTransaction.cs"));
int disableAutoSave = configTransaction.IndexOf("config.SaveOnConfigSet = false", StringComparison.Ordinal);
int captureSnapshot = configTransaction.IndexOf("snapshot = ConfigFileSnapshot.Capture(config)", StringComparison.Ordinal);
int canonicalize = configTransaction.IndexOf("RemoveUnboundPluginEntries(config)", StringComparison.Ordinal);
Check(disableAutoSave >= 0 && captureSnapshot > disableAutoSave,
    "The configuration transaction captures or binds before disabling SaveOnConfigSet.");
Check(canonicalize > captureSnapshot && canonicalize < configTransaction.IndexOf("(save ??", StringComparison.Ordinal),
    "The final binding session does not remove unbound plugin-owned entries before canonical save.");
Check(!File.Exists(Path.Combine(sourceRoot, "Services", "PluginConfigMigrator.cs")) &&
      !File.Exists(Path.Combine(sourceRoot, "Services", "PluginConfigMigrationPolicy.cs")),
    "The terminal build still compiles the one-shot configuration migrator.");
foreach (string legacyConfigKey in new[] { "Scan" + "Radius", "Request" + "Range", "Preserve" + "Hotkeys", "WindowPosition" + "X", "WindowPosition" + "Y" })
    Check(!source.Contains($"\"{legacyConfigKey}\"", StringComparison.Ordinal), $"Legacy plugin configuration key remains in production source: {legacyConfigKey}");

string inputCapture = Read(Path.Combine(sourceRoot, "UI", "UiInputCaptureController.cs"));
Check(inputCapture.Contains("ReleaseCapturedWindow(ShouldRestoreBorrowedWindow())", StringComparison.Ordinal), "Window replacement does not isolate borrowed-window release.");
Check(!inputCapture.Contains("Release();\n\t\t\t_capturedWindow", StringComparison.Ordinal), "Window replacement still clears the Unity cursor snapshot.");
Check(inputCapture.Contains("PlayerDashboardUI.active || PlayerPauseUI.active", StringComparison.Ordinal),
    "Input capture does not recognize inventory/map and pause UI ownership.");
Check(inputCapture.Contains("UiInputOwnershipState.NativeUiCoexistence", StringComparison.Ordinal),
    "Input capture has no NativeUiCoexistence state transition.");
Check(inputCapture.Contains("IsNativeUiRequested", StringComparison.Ordinal) && inputCapture.Contains("ControlsSettings.inventory", StringComparison.Ordinal),
    "Input capture does not yield the borrowed SleekWindow for native dashboard shortcuts.");
string windowDrag = Read(Path.Combine(sourceRoot, "UI", "WindowDragHandle.cs"));
Check(windowDrag.Split("_onPositionChanged?.Invoke", StringSplitOptions.None).Length - 1 == 1, "Window drag still persists on every drag event.");
CheckButtonTooltipCoverage(source, overlay);
Check(File.ReadLines(overlayPath).Count() < 3000, "The overlay shell is still a monolithic all-pages source file.");
foreach (string slice in new[]
{
    "CheatMenuOverlayUi.CharacterView.cs",
    "CheatMenuOverlayUi.CatalogViews.cs",
    "CheatMenuOverlayUi.TeleportView.cs",
    "CheatMenuOverlayUi.ModalViews.cs",
    "CheatMenuOverlayUi.WorldView.cs",
    "CheatMenuOverlayUi.ToolsShell.cs",
    "CheatMenuOverlayUi.InfiniteStorageView.cs",
    "CheatMenuOverlayUi.TranslationToolsView.cs",
    "CheatMenuOverlayUi.InventoryToolsView.cs",
    "CheatMenuOverlayUi.AutoPickupToolsView.cs",
    "CheatMenuOverlayUi.InteractionToolsView.cs",
    "CheatMenuOverlayUi.MovementToolsView.cs",
})
    Check(File.Exists(Path.Combine(sourceRoot, "UI", slice)), $"Vertical uGUI slice source is missing: {slice}");
Check(!overlay.Contains("ReplaceContentHost(", StringComparison.Ordinal), "Overlay still directly replaces the whole content host.");
Check(overlay.Contains("CheatMenuPageViewHost", StringComparison.Ordinal), "Main pages are not routed through a stable active-view host.");
Check(overlay.Contains("CheatMenuToolsPageViewHost", StringComparison.Ordinal), "Tools pages are not routed through a stable active-view host.");
string queueForDestroy = ReadMethodBody(overlay, "private void QueueForDestroy(GameObject target)");
Check(queueForDestroy.Contains("item => item.SetActive(value: false)", StringComparison.Ordinal),
    "Deferred uGUI destruction does not retire the subtree before the safe destroy frame.");
string pumpDeferredDestroy = ReadMethodBody(overlay, "internal void PumpDeferredDestroy()");
Check(pumpDeferredDestroy.Contains("_deferredDestroy.TryDequeueReady", StringComparison.Ordinal) &&
      pumpDeferredDestroy.Contains("CanvasUpdateRegistry.IsRebuildingLayout()", StringComparison.Ordinal),
    "The overlay does not expose a safe lifecycle pump for deferred uGUI destruction.");
string runtimeUpdate = ReadMethodBody(context, "internal void Update()");
int deferredDestroyPump = runtimeUpdate.IndexOf("_ui?.PumpDeferredDestroy()", StringComparison.Ordinal);
int pluginWindowGate = runtimeUpdate.IndexOf("bool pluginWindowOpen", StringComparison.Ordinal);
Check(deferredDestroyPump >= 0 && pluginWindowGate >= 0 && deferredDestroyPump < pluginWindowGate,
    "Deferred uGUI destruction is still gated by plugin-window visibility or native UI coexistence.");
string characterView = ReadMethodBody(overlay, "private void BuildCharacterTab()");
Check(characterView.Contains("_characterPresenter.CaptureSnapshot()", StringComparison.Ordinal),
    "Character View does not render from a presenter snapshot.");
Check(!characterView.Contains("Player.LocalPlayer", StringComparison.Ordinal) && !characterView.Contains("_plugin.Actions", StringComparison.Ordinal),
    "Character View still calls the Unturned gameplay API or action service directly.");
string worldView = ReadMethodBody(overlay, "private void BuildOtherTab()");
Check(worldView.Contains("_worldPresenter.CaptureSnapshot()", StringComparison.Ordinal),
    "World View does not render from a presenter snapshot.");
Check(!worldView.Contains("LightingManager.", StringComparison.Ordinal) && !worldView.Contains("_plugin.Actions", StringComparison.Ordinal),
    "World View still calls the Unturned gameplay API or action service directly.");
string pagePresenters = Read(Path.Combine(sourceRoot, "UI", "CheatMenuPagePresenters.cs"));
string featurePresenters = Read(Path.Combine(sourceRoot, "UI", "CheatMenuFeaturePresenters.cs"));
string featurePorts = Read(Path.Combine(sourceRoot, "UI", "CheatMenuUiFeaturePorts.cs"));
string uiStateStore = Read(Path.Combine(sourceRoot, "UI", "UiStateStore.cs"));
Check(pagePresenters.Contains("sealed class CharacterPageSnapshot", StringComparison.Ordinal) &&
      pagePresenters.Contains("internal bool IsReady { get; }", StringComparison.Ordinal),
    "Character presenter does not expose an immutable snapshot.");
Check(pagePresenters.Contains("sealed class WorldPageSnapshot", StringComparison.Ordinal) &&
      pagePresenters.Contains("internal uint Cycle { get; }", StringComparison.Ordinal),
    "World presenter does not expose an immutable snapshot.");
foreach (string leakedService in new[]
{
    "AssetCatalog Catalog", "IconCache Icons", "FavoriteStore Favorites", "TeleportStore Teleports",
    "TeleportMapService Maps", "InfiniteStorageRuntime Runtime", "WorkshopLocalizationService Service",
    "InventoryArrangeFeature Feature", "AutoPickupFeature Feature", "PointToolService PointTool",
    "MovementCheatService Movement",
})
    Check(!featurePresenters.Contains(leakedService, StringComparison.Ordinal), $"A feature presenter leaks its mutable service to the View: {leakedService}");
Check(!featurePresenters.Contains("ManualLogSource", StringComparison.Ordinal), "The shell presenter leaks logging infrastructure to the View.");
Check(!featurePresenters.Contains("CheatMenuPlugin", StringComparison.Ordinal),
    "A page presenter still depends on the complete plugin facade.");
foreach (string featurePort in new[]
{
    "IShellUiFeature", "ICatalogUiFeature", "ITeleportUiFeature", "IWorldUiFeature",
    "IInfiniteStorageUiFeature", "ITranslationUiFeature", "IInventoryUiFeature",
    "IAutoPickupUiFeature", "IInteractionUiFeature", "IMovementUiFeature",
})
    Check(featurePorts.Contains($"interface {featurePort}", StringComparison.Ordinal), $"Narrow UI feature seam is missing: {featurePort}");
int featureAdapterOffset = featurePorts.IndexOf("internal sealed class PluginUiFeatureAdapter", StringComparison.Ordinal);
Check(featureAdapterOffset > 0, "The plugin-to-feature adapter seam is missing.");
string featureContracts = featurePorts[..featureAdapterOffset];
foreach (string broadDependency in new[]
{
    "CheatMenuPlugin", "InfiniteStorageRuntime", "WorkshopLocalizationService", "AutoPickupFeature",
    "PointToolService", "MovementCheatService",
})
    Check(!featureContracts.Contains(broadDependency, StringComparison.Ordinal),
        $"A narrow UI feature contract leaks a mutable runtime dependency: {broadDependency}");
Check(uiStateStore.Contains("sealed class UiStateStore", StringComparison.Ordinal), "The long-lived UI state store is missing.");
Check(!uiStateStore.Contains("GameObject", StringComparison.Ordinal), "UiStateStore retains a Unity View object.");
Check(uiStateStore.Contains("ItemFilterState ItemFilter", StringComparison.Ordinal) &&
      uiStateStore.Contains("string AutoPickupRuleSearch", StringComparison.Ordinal) &&
      uiStateStore.Contains("float ShortcutScrollPosition", StringComparison.Ordinal),
    "Draft, filter, pagination, or scroll state still lacks a persistent store owner.");
Check(featurePresenters.Contains("void ProjectItems()", StringComparison.Ordinal) &&
      featurePresenters.Contains("void ProjectVehicles()", StringComparison.Ordinal) &&
      featurePresenters.Contains("void ProjectFavorites()", StringComparison.Ordinal),
    "Catalog projection is not owned by its presenter.");
foreach (string snapshot in new[]
{
    "InfiniteStoragePageSnapshot", "TranslationToolsSnapshot", "AutoPickupToolsSnapshot",
    "InteractionToolsSnapshot", "MovementToolsSnapshot",
})
    Check(featurePresenters.Contains($"sealed class {snapshot}", StringComparison.Ordinal), $"Feature snapshot is missing: {snapshot}");
Check(!overlay.Contains("_plugin.", StringComparison.Ordinal),
    "The uGUI View still reaches through the plugin instead of page presenters.");
foreach (string gameplayAccess in new[] { "Player.LocalPlayer", "Provider.map", "LightingManager." })
    Check(!overlay.Contains(gameplayAccess, StringComparison.Ordinal), $"The uGUI View still calls a gameplay API directly: {gameplayAccess}");
string catalogRefresh = ReadMethodBody(overlay, "public void OnCatalogRefreshed()");
Check(!catalogRefresh.Contains("RebuildInfiniteStoragePage()", StringComparison.Ordinal),
    "Catalog refresh still destroys the infinite-storage search and input controls.");
Check(catalogRefresh.Contains("RefreshInfiniteStorageContentRegions()", StringComparison.Ordinal),
    "Catalog refresh does not update the stable infinite-storage regions.");
string localizedAssetRefresh = ReadMethodBody(overlay, "public void OnLocalizedAssetViewsRefreshed()");
Check(!localizedAssetRefresh.Contains("RebuildInfiniteStoragePage()", StringComparison.Ordinal),
    "Localized asset refresh still destroys the infinite-storage search and input controls.");
string workshopRefresh = ReadMethodBody(overlay, "private void RefreshWorkshopLocalizationPage()");
Check(!workshopRefresh.Contains("RefreshActiveToolsPageContent()", StringComparison.Ordinal),
    "Workshop status refresh still destroys the complete Tools page and its credential inputs.");
Check(workshopRefresh.Contains("RefreshWorkshopLocalizationPageValues()", StringComparison.Ordinal),
    "Workshop status refresh does not update its stable live-value region.");
string apiKeyRows = ReadMethodBody(translationToolsView, "private void CreateWorkshopApiKeyRows(");
Check(apiKeyRows.Contains("MachineTranslationSettings.MaxApiKeyCount", StringComparison.Ordinal) &&
      apiKeyRows.Contains("\"+\"", StringComparison.Ordinal) &&
      apiKeyRows.Contains("\"×\"", StringComparison.Ordinal),
    "Workshop translation does not expose one add button and removable extra API-key rows with a two-key limit.");
Check(apiKeyRows.Contains("CheatMenuStyle.Muted, 86f, TextAnchor.MiddleRight", StringComparison.Ordinal),
    "Workshop API-key inputs do not share the same left edge as the other Chinese translation-setting inputs.");
Check(apiKeyRows.Contains("36f, 34f, CheatMenuStyle.SurfaceHover, 17", StringComparison.Ordinal),
    "Workshop API-key alignment changed the add-button geometry or style.");
string createToolsPageView = ReadMethodBody(toolsShell, "private UiPageView<ToolsTab> CreateToolsPageView(");
Check(createToolsPageView.Contains("tab == ToolsTab.Translation", StringComparison.Ordinal) &&
      createToolsPageView.Contains("CreateShortcutScroll", StringComparison.Ordinal) &&
      createToolsPageView.Contains("AddShortcutScrollbar", StringComparison.Ordinal) &&
      createToolsPageView.Contains("AddShortcutScrollLayout", StringComparison.Ordinal),
    "The translation page is not hosted in the same clipped vertical waterfall/scroll pattern as global shortcuts.");
string showTab = ReadMethodBody(overlay, "private void ShowTab(MenuTab tab, bool invalidate = true)");
Check(showTab.Contains("Activate(tab, invalidate)", StringComparison.Ordinal), "Main page invalidation is not passed to its presenter.");
foreach (string signature in new[]
{
    "private void PopulateItemGrid()",
    "private void PopulateVehicleGrid()",
    "private void PopulateFavoriteGrid(int num)",
})
{
    string gridBody = ReadMethodBody(overlay, signature);
    Check(!gridBody.Contains("ShowTab(", StringComparison.Ordinal), $"Catalog pagination still rebuilds the full page: {signature}");
    Check(gridBody.Contains("Refresh", StringComparison.Ordinal), $"Catalog pagination does not target its stable grid region: {signature}");
}
string maintain = ReadMethodBody(overlay, "public void Maintain()");
Check(maintain.Contains("if (!_uiRoot.HandleCancel())", StringComparison.Ordinal), "Unhandled Escape is not routed to the top-level window state.");
Check(maintain.Contains("_shellPresenter.CloseMenu()", StringComparison.Ordinal), "Escape cannot close the main menu through the Shell presenter.");
Check(maintain.Contains("_shellPresenter.CloseInfiniteStorage()", StringComparison.Ordinal), "Escape cannot close infinite storage through the Shell presenter.");
Check(!maintain.Contains("RefreshWorkshopLocalizationPageValues()", StringComparison.Ordinal),
    "Workshop localization values are still refreshed from the per-frame UI maintenance path.");
Check(context.Contains("PresentationVersion", StringComparison.Ordinal) &&
      context.Contains("OnWorkshopLocalizationPresentationChanged", StringComparison.Ordinal),
    "Workshop localization progress/configuration refresh is not version-driven.");
string buildHeader = ReadMethodBody(overlay, "private void BuildHeader(Transform parent)");
Check(buildHeader.Contains("dragArea.AddComponent<WindowDragHandle>()", StringComparison.Ordinal), "Header has no independent drag area.");
Check(!buildHeader.Contains("gameObject.AddComponent<WindowDragHandle>()", StringComparison.Ordinal), "Header drag handler still covers action buttons.");
Check(!overlay.Contains("sizeDelta = new Vector2(1120f, 790f)", StringComparison.Ordinal), "Window panels still use a fixed 1120x790 assignment.");
Check(overlay.Contains("WindowGeometry.ResolveSize", StringComparison.Ordinal), "Window panels do not resolve responsive min/preferred sizes.");
Check(overlay.Contains("_lastMainLayoutScreenWidth", StringComparison.Ordinal) && overlay.Contains("_lastInfiniteLayoutScreenWidth", StringComparison.Ordinal),
    "Main and infinite-storage windows still share layout version markers.");
string onOpened = ReadMethodBody(overlay, "public void OnOpened()");
Check(onOpened.Contains("RefreshWindowGeometry", StringComparison.Ordinal), "Reopening the main menu does not force a geometry refresh.");
string buildTabs = ReadMethodBody(overlay, "private void BuildTabs(Transform parent)");
Check(buildTabs.Contains("ScrollRect", StringComparison.Ordinal) && buildTabs.Contains("RectMask2D", StringComparison.Ordinal),
    "Primary navigation is not horizontally scrollable on narrow windows.");
Check(buildTabs.Contains("contentLayout.minWidth = PreferredWindowWidth - 36f", StringComparison.Ordinal) &&
      buildTabs.Contains("layout.childAlignment = TextAnchor.MiddleCenter", StringComparison.Ordinal),
    "Primary navigation buttons are not centered within the preferred-width viewport.");
Check(!overlay.Contains("ShowToolsTab(_activeToolsTab, invalidate: true)", StringComparison.Ordinal),
    "Tools callbacks still invalidate and rebuild the complete active Tools view.");
string autoPickupPanel = ReadMethodBody(overlay, "private void PopulateAutoPickupPanel()");
Check(!autoPickupPanel.Contains("ShowToolsTab(", StringComparison.Ordinal) && autoPickupPanel.Contains("RefreshAutoPickupPanel", StringComparison.Ordinal),
    "Auto-pickup pagination does not refresh its stable local content host.");
string pageViewHost = Read(Path.Combine(sourceRoot, "UI", "CheatMenuPageViewHost.cs"));
string toolsViewHost = Read(Path.Combine(sourceRoot, "UI", "CheatMenuToolsPageViewHost.cs"));
string activePageViewHost = Read(Path.Combine(sourceRoot, "UI", "ActivePageViewHost.cs"));
string uiPageView = Read(Path.Combine(sourceRoot, "UI", "UiPageView.cs"));
Check(activePageViewHost.Contains("!invalidate && _activeView?.Root != null && _activePage.HasValue", StringComparison.Ordinal),
    "Shared page View host ignores its invalidation contract.");
Check(activePageViewHost.Contains("_activeView.Dispose()", StringComparison.Ordinal),
    "Shared page View host does not delegate subtree teardown to the View.");
string activatePageView = ReadMethodBody(activePageViewHost, "internal GameObject Activate(TPage page, bool invalidate)");
Check(activatePageView.IndexOf("DestroyActiveView()", StringComparison.Ordinal) >= 0 &&
      activatePageView.IndexOf("DestroyActiveView()", StringComparison.Ordinal) <
      activatePageView.IndexOf("_activeView = _createView(page)", StringComparison.Ordinal),
    "A replacement page is created before the previous page has retired from layout.");
Check(uiPageView.Contains("sealed class UiPageView", StringComparison.Ordinal) &&
      uiPageView.Contains("_destroy(root)", StringComparison.Ordinal) &&
      uiPageView.Contains("public void Dispose()", StringComparison.Ordinal),
    "A page View does not independently own its root lifecycle.");
Check(pageViewHost.Contains("ActivePageViewHost<CheatMenuOverlayUi.MenuTab>", StringComparison.Ordinal),
    "Main page presenter does not use the shared lifecycle implementation.");
Check(toolsViewHost.Contains("ActivePageViewHost<CheatMenuOverlayUi.ToolsTab>", StringComparison.Ordinal),
    "Tools page presenter does not use the shared lifecycle implementation.");
Check(!overlay.Contains("Dictionary<ToolsTab, GameObject>", StringComparison.Ordinal), "Tools pages still use an unversioned GameObject cache.");
Check(overlay.Contains("using InputField = UnityEngine.UI.InputField", StringComparison.Ordinal) &&
      overlay.Contains("component2.textComponent = text", StringComparison.Ordinal) &&
      overlay.Contains("component2.placeholder = text2", StringComparison.Ordinal) &&
      !overlay.Contains("textViewport", StringComparison.Ordinal),
    "Input controls are not using the legacy uGUI InputField path.");
foreach (string layerUse in new[] { "_uiRoot.ModalLayer", "_uiRoot.PopupLayer", "_uiRoot.TooltipLayer" })
    Check(overlay.Contains(layerUse, StringComparison.Ordinal), $"Overlay has no production consumer for {layerUse}.");
Check(overlay.Contains("IsCapturingKeyboardInput", StringComparison.Ordinal), "Text editing does not participate in shortcut suppression.");
Check(context.Contains("IsCapturingKeyboardInput", StringComparison.Ordinal), "Runtime shortcut dispatch ignores text editing focus.");
Check(!File.Exists(Path.Combine(sourceRoot, "UI", "TmpFontProvider.cs")),
    "The retired TMP font provider still exists in production source.");
string fontProvider = Read(Path.Combine(sourceRoot, "UI", "SystemFontProvider.cs"));
string fontFamilyOptionPath = Path.Combine(sourceRoot, "UI", "SystemFontFamilyOption.cs");
Check(File.Exists(fontFamilyOptionPath),
    "The font picker has no display model separating localized labels from Unity's stable family name.");
string fontFamilyOption = File.Exists(fontFamilyOptionPath) ? Read(fontFamilyOptionPath) : string.Empty;
Check(fontFamilyOption.Contains("FamilyName", StringComparison.Ordinal) &&
      fontFamilyOption.Contains("DisplayName", StringComparison.Ordinal),
    "The system-font option does not keep an independent internal family name and localized display name.");
Check(!fontProvider.Contains("TMPro", StringComparison.Ordinal) &&
      !fontProvider.Contains("TMP_", StringComparison.Ordinal) &&
      !fontProvider.Contains("TextCore", StringComparison.Ordinal),
    "The system font provider still converts Windows fonts through TMP/TextCore.");
Check(fontProvider.Contains("Font.GetOSInstalledFontNames()", StringComparison.Ordinal),
    "The plugin does not discover an installed Windows CJK font.");
foreach (string family in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑" })
    Check(fontProvider.Contains(family, StringComparison.Ordinal), $"System font candidate is missing: {family}");
Check(fontProvider.Contains("Font.CreateDynamicFontFromOSFont", StringComparison.Ordinal) &&
      fontProvider.Contains("new[] { requestedFamily }", StringComparison.Ordinal),
    "The plugin does not use the working release's direct dynamic UnityEngine.Font path.");
Check(fontProvider.Contains("GetInstalledChineseFontFamilies", StringComparison.Ordinal) &&
      fontProvider.Contains("GetGlyphIndicesW", StringComparison.Ordinal) &&
      fontProvider.Contains("ushort.MaxValue", StringComparison.Ordinal),
    "The font picker does not reject font faces that lack real Chinese glyphs.");
Check(fontProvider.Contains("TrySelectSystemFont", StringComparison.Ordinal),
    "The system font provider does not expose a validated runtime selection path.");
Check(fontProvider.Contains("System.Drawing.Text.InstalledFontCollection, System.Drawing", StringComparison.Ordinal) &&
      fontProvider.Contains("GetName\", new[] { typeof(int) }", StringComparison.Ordinal) &&
      fontProvider.Contains("SimplifiedChineseLcid", StringComparison.Ordinal),
    "Installed font metadata is not queried for its Simplified Chinese family name.");
Check(!project.Contains("System.Drawing.dll", StringComparison.Ordinal) &&
      !prepareReferences.Contains("'System.Drawing.dll'", StringComparison.Ordinal),
    "Localized font metadata introduced a conflicting compile-time System.Drawing dependency.");
Check(plugin.Contains("Config.Bind(\"Interface\", \"FontFamily\", \"Microsoft YaHei UI\"", StringComparison.Ordinal),
    "The selected system font is not persisted in the plugin interface configuration.");
Check(featurePorts.Contains("GetInstalledChineseFontFamilies", StringComparison.Ordinal) &&
      featurePorts.Contains("TrySetFontFamily", StringComparison.Ordinal) &&
      featurePresenters.Contains("GetInstalledChineseFontFamilies", StringComparison.Ordinal) &&
      featurePresenters.Contains("TrySetFontFamily", StringComparison.Ordinal),
    "The shell presenter boundary does not expose Chinese-capable system font selection.");
Check(overlay.Contains("ShowFontPicker", StringComparison.Ordinal) &&
      overlay.Contains("HideFontPicker", StringComparison.Ordinal) &&
      overlay.Contains("_fontPickerRoot", StringComparison.Ordinal) &&
      overlay.Contains("选择界面字体", StringComparison.Ordinal),
    "The top-level plugin window has no modal system-font picker.");
string modalViews = Read(Path.Combine(sourceRoot, "UI", "CheatMenuOverlayUi.ModalViews.cs"));
Check(modalViews.Contains("CreateShortcutSectionHeader(_shortcutScrollContent, \"小地图\")", StringComparison.Ordinal) &&
      modalViews.Contains("BuildShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ToggleMinimap)", StringComparison.Ordinal) &&
      modalViews.Contains("BuildFixedShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ZoomMinimapIn)", StringComparison.Ordinal) &&
      modalViews.Contains("BuildFixedShortcutActionRow(_shortcutScrollContent, GlobalShortcutActions.ZoomMinimapOut)", StringComparison.Ordinal),
    "The global shortcut dialog does not expose the minimap toggle and fixed zoom gestures.");
string fontPickerBody = ReadMethodBody(modalViews, "private void ShowFontPicker()");
Check(fontPickerBody.Contains("fontOption.DisplayName", StringComparison.Ordinal) &&
      fontPickerBody.Contains("fontOption.FamilyName", StringComparison.Ordinal),
    "The font picker does not render localized labels while selecting by Unity's internal family name.");
Check(fontPickerBody.Contains("CreateShortcutScroll(listPanel.transform)", StringComparison.Ordinal) &&
      !fontPickerBody.Contains("CreateScroll(listPanel.transform)", StringComparison.Ordinal),
    "The font picker still uses the stencil-mask scroll path that creates rows but clips every row from view.");
string sharedScrollFactory = ReadMethodBody(modalViews, "private static RectTransform CreateShortcutScroll(Transform parent)");
Check(sharedScrollFactory.Contains("typeof(RectMask2D)", StringComparison.Ordinal) &&
      !sharedScrollFactory.Contains("typeof(Mask)", StringComparison.Ordinal),
    "The shared modal list viewport does not use deterministic RectMask2D clipping.");
Check(!Directory.EnumerateFiles(sourceRoot, "*.otf", SearchOption.AllDirectories).Any(),
    "A heavyweight OTF font was bundled even though the runtime must use the installed Windows font.");
Check(minimapHud.Contains("CreateStarSprite(32", StringComparison.Ordinal) &&
      minimapHud.Contains("CreateHeightIndicatorSprite(16", StringComparison.Ordinal) &&
      minimapMarkerPool.Contains("slot.Image.sprite = itemStarSprite", StringComparison.Ordinal) &&
      minimapMarkerPool.Contains("slot.HeightIndicator.sprite = heightIndicatorSprite", StringComparison.Ordinal) &&
      !minimapMarkerPool.Contains("UiText", StringComparison.Ordinal) &&
      !minimapMarkerPool.Contains("Glyph", StringComparison.Ordinal) &&
      !fontProvider.Contains("MinimapArrowProbeGlyphs", StringComparison.Ordinal),
    "The minimap star or floor indicator still depends on font rendering instead of pooled Image sprites.");
Check(minimapHud.Contains("MinimapDisplayText.BackgroundState(state)", StringComparison.Ordinal) &&
      minimapToolsView.Contains("MinimapDisplayText.Shape(settings.Shape)", StringComparison.Ordinal) &&
      minimapToolsView.Contains("MinimapDisplayText.BackgroundMode(settings.BackgroundMode)", StringComparison.Ordinal) &&
      minimapToolsView.Contains("MinimapDisplayText.Rarity(settings.ItemRarityMask)", StringComparison.Ordinal) &&
      !minimapHud.Contains("state.ToString()", StringComparison.Ordinal) &&
      !minimapToolsView.Contains(" click-through", StringComparison.Ordinal) &&
      !minimapToolsView.Contains(" Hz", StringComparison.Ordinal) &&
      !minimapToolsView.Contains(" px", StringComparison.Ordinal),
    "Player-visible minimap state, enum names, tooltips, or units still contain English residues.");
if (OperatingSystem.IsWindows())
    SystemFontProbe.AssertGlyphs("Microsoft YaHei UI", "YaHei", "中文单人作弊菜单无限储存地图坐标");
string localizationPatch = Read(Path.Combine(sourceRoot, "Patches", "WorkshopLocalizationPatch.cs"));
Check(!localizationPatch.Contains("SystemFontProvider", StringComparison.Ordinal) &&
      !localizationPatch.Contains("FontFamily", StringComparison.Ordinal),
    "Native workshop text replacement is incorrectly coupled to the plugin-selected UI font.");
int textTraversal = root.IndexOf("GetComponentsInChildren<UiText>(true)", StringComparison.Ordinal);
int revisionGate = root.IndexOf("_fontRevision == SystemFontProvider.Revision", StringComparison.Ordinal);
Check(revisionGate >= 0 && textTraversal > revisionGate,
    "UiRoot enumerates all text before checking the provider revision.");

Dictionary<string, string> localizedFontNames = new(StringComparer.OrdinalIgnoreCase)
{
    ["Microsoft YaHei"] = "微软雅黑"
};
Check(SystemFontDisplayNameResolver.Resolve("Microsoft YaHei", localizedFontNames) == "微软雅黑",
    "A font's zh-CN metadata name does not take priority over fallback aliases.");
Check(SystemFontDisplayNameResolver.Resolve("Microsoft YaHei UI", localizedFontNames) == "微软雅黑 UI",
    "Microsoft YaHei UI has no Chinese display-name fallback.");
Check(SystemFontDisplayNameResolver.Resolve("SimSun-ExtB", localizedFontNames) == "宋体-ExtB",
    "SimSun extension fonts have no Chinese display-name fallback.");
Check(SystemFontDisplayNameResolver.Resolve("Source Han Sans SC VF", localizedFontNames) == "思源黑体 VF",
    "Source Han Sans SC variants do not preserve their suffix after localization.");
Check(SystemFontDisplayNameResolver.Resolve("Unknown CJK Font", localizedFontNames) == "Unknown CJK Font",
    "Unknown font families do not fall back to their stable Unity family name.");

UiSearchEditState search = new("alpha");
search.ChangeDraft("beta");
Check(search.ApplyPreview(out string preview) && preview == "beta", "Search preview did not apply the draft.");
Check(search.Committed == "alpha", "Debounced preview overwrote the committed search value.");
Check(search.Revert(out string reverted) && reverted == "alpha", "Escape did not restore the committed search value.");
search.ChangeDraft("gamma");
Check(search.Commit("gamma", out string committed) && committed == "gamma", "Explicit search commit failed.");
Check(search.Committed == "gamma", "Explicit search commit was not retained.");

FakeEventSystem owned = new("owned", Active: true, Preferred: true);
FakeEventSystem native = new("native", Active: true, Preferred: true);
FakeEventSystem selected = UiLifecyclePolicy.SelectBorrowed(
    owned,
    owned,
    new[] { owned, native },
    candidate => candidate.Active,
    candidate => candidate.Preferred);
Check(ReferenceEquals(selected, native), "Owned-current EventSystem blocked discovery of a native instance.");

string capturedWindow = "old";
int releasedWindows = 0;
int capturedWindows = 0;
UiLifecyclePolicy.ReplaceBorrowed(
    ref capturedWindow,
    "new",
    _ => releasedWindows++,
    _ => capturedWindows++);
Check(capturedWindow == "new" && releasedWindows == 1 && capturedWindows == 1, "Borrowed PlayerUI.window replacement transition failed.");
UiLifecyclePolicy.ReplaceBorrowed(ref capturedWindow, "new", _ => releasedWindows++, _ => capturedWindows++);
Check(releasedWindows == 1 && capturedWindows == 1, "Stable PlayerUI.window was captured twice.");

float normalized = UiLifecyclePolicy.NormalizeAxis(200f, 1920f, 1120f, 1f);
Check(Math.Abs(normalized - 0.5f) < 0.0001f, "Window position normalization is incorrect.");
float resized = UiLifecyclePolicy.RestoreAxis(normalized, 2560f, 1120f, 1f);
Check(Math.Abs(resized - 360f) < 0.0001f, "Normalized window position did not adapt to resolution change.");
Check(UiLifecyclePolicy.ClampAxis(float.NaN, 1920f, 1120f, 1f) == 0f, "Non-finite window position was not sanitized.");
float responsiveScale = UiLifecyclePolicy.ResolveResponsiveScale(1.5f, 1280f, 720f, 900f, 620f, 16f);
Check(responsiveScale < 1.5f && responsiveScale >= 0.5f, "Responsive scale does not fit a narrow 16:9 viewport.");
float responsiveWidth = UiLifecyclePolicy.ResolveResponsiveAxis(1024f, 1f, 900f, 1120f, 16f);
Check(Math.Abs(responsiveWidth - 992f) < 0.0001f, "Responsive width did not honor viewport margins.");
float preferredWidth = UiLifecyclePolicy.ResolveResponsiveAxis(1920f, 1f, 900f, 1120f, 16f);
Check(Math.Abs(preferredWidth - 1120f) < 0.0001f, "Responsive width exceeded its preferred size.");

Check(UiLifecyclePolicy.ResolveInputOwnership(pluginWindowOpen: false, nativeUiActive: false) == UiInputOwnershipState.Hidden,
    "Closed plugin windows did not resolve to Hidden input ownership.");
Check(UiLifecyclePolicy.ResolveInputOwnership(pluginWindowOpen: true, nativeUiActive: false) == UiInputOwnershipState.ModalPluginUi,
    "Open plugin windows did not resolve to modal plugin ownership.");
Check(UiLifecyclePolicy.ResolveInputOwnership(pluginWindowOpen: true, nativeUiActive: true) == UiInputOwnershipState.NativeUiCoexistence,
    "Native UI did not take input ownership from plugin windows.");

IdempotentTeardownGate teardownGate = new();
Check(teardownGate.TryBegin(), "First teardown was rejected.");
Check(!teardownGate.TryBegin(), "Repeated teardown was not rejected.");

DeferredUiDestroyQueue<FakeUiSubtree> destroyQueue = new();
FakeUiSubtree queuedTextSubtree = new();
destroyQueue.Enqueue(queuedTextSubtree, currentFrame: 100, subtree => subtree.Active = false);
Check(!queuedTextSubtree.Active,
    "A deferred uGUI subtree remained visible and layout-active while awaiting destruction.");
Check(!destroyQueue.TryDequeueReady(currentFrame: 100, canvasRebuilding: false, out _),
    "A uGUI subtree was destroyed in the same frame it was queued.");
Check(!destroyQueue.TryDequeueReady(currentFrame: 101, canvasRebuilding: true, out _),
    "A uGUI subtree was destroyed while CanvasUpdateRegistry was rebuilding.");
Check(destroyQueue.TryDequeueReady(currentFrame: 101, canvasRebuilding: false, out FakeUiSubtree readyTextSubtree) &&
      ReferenceEquals(queuedTextSubtree, readyTextSubtree),
    "A uGUI subtree was not released on the first safe frame after rebuild completion.");

Console.WriteLine("UI architecture smoke checks passed.");

internal sealed record FakeEventSystem(string Name, bool Active, bool Preferred);

internal sealed class FakeUiSubtree
{
    internal bool Active { get; set; } = true;
}

internal static class SystemFontProbe
{
    private const uint DefaultCharset = 1;
    private const uint OutDefaultPrecision = 0;
    private const uint ClipDefaultPrecision = 0;
    private const uint DefaultQuality = 0;
    private const uint DefaultPitchAndFamily = 0;
    private const uint MarkMissingGlyphs = 1;

    internal static void AssertGlyphs(string familyName, string expectedFaceToken, string requiredText)
    {
		IntPtr font = CreateFontW(-24, 0, 0, 0, 400, 0, 0, 0, DefaultCharset,
			OutDefaultPrecision, ClipDefaultPrecision, DefaultQuality, DefaultPitchAndFamily, familyName);
		if (font == IntPtr.Zero)
			throw new InvalidOperationException($"Windows could not create {familyName}.");

        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            DeleteObject(font);
            throw new InvalidOperationException("Windows could not create a font probe device context.");
        }

        IntPtr previous = SelectObject(dc, font);
        try
        {
            StringBuilder actualFace = new(128);
			if (GetTextFaceW(dc, actualFace.Capacity, actualFace) <= 0 ||
				actualFace.ToString().IndexOf(expectedFaceToken, StringComparison.OrdinalIgnoreCase) < 0)
				throw new InvalidOperationException($"{familyName} was substituted by an unrelated font: {actualFace}.");

            ushort[] glyphs = new ushort[requiredText.Length];
            if (GetGlyphIndicesW(dc, requiredText, requiredText.Length, glyphs, MarkMissingGlyphs) == uint.MaxValue)
				throw new InvalidOperationException($"Windows failed to query {familyName} glyph indices.");

            int missingIndex = Array.IndexOf(glyphs, ushort.MaxValue);
            if (missingIndex >= 0)
				throw new InvalidOperationException($"{familyName} is missing U+{(int)requiredText[missingIndex]:X4}.");
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SelectObject(dc, previous);
            DeleteDC(dc);
            DeleteObject(font);
        }
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strikeOut, uint charSet, uint outputPrecision, uint clipPrecision,
        uint quality, uint pitchAndFamily, string faceName);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetTextFaceW(IntPtr dc, int count, StringBuilder faceName);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetGlyphIndicesW(IntPtr dc, string text, int count, [Out] ushort[] glyphIndices, uint flags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);
}
