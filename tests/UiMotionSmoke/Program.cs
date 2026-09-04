using UnturnedSingleplayerCheatMenu.UI.Motion;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static bool Approximately(float left, float right) => Math.Abs(left - right) < 0.0001f;

foreach (UiMotionEasingKind easing in Enum.GetValues<UiMotionEasingKind>())
{
    Check(Approximately(UiMotionEasing.Evaluate(easing, 0f), 0f), $"{easing} does not begin at zero.");
    Check(Approximately(UiMotionEasing.Evaluate(easing, 1f), 1f), $"{easing} does not end at one.");
    float previous = 0f;
    for (int step = 1; step <= 100; step++)
    {
        float current = UiMotionEasing.Evaluate(easing, step / 100f);
        Check(!float.IsNaN(current) && !float.IsInfinity(current), $"{easing} produced a non-finite value.");
        Check(current + 0.0001f >= previous, $"{easing} is not monotonic at step {step}.");
        previous = current;
    }
}

Check(Approximately(UiMotionEasing.Evaluate(UiMotionEasingKind.EaseOutCubic, float.NaN), 0f),
    "NaN easing input does not fail closed to zero.");
Check(Approximately(UiMotionEasing.Evaluate(UiMotionEasingKind.EaseOutCubic, float.PositiveInfinity), 1f),
    "Positive-infinite easing input does not finish at one.");

UiMotionProgress progress = new();
progress.Begin(0.2f);
Check(!progress.IsComplete && Approximately(progress.Advance(0.05f), 0.25f),
    "Motion progress did not advance deterministically.");
Check(Approximately(progress.Advance(-1f), 0.25f), "Negative delta changed progress.");
Check(Approximately(progress.Advance(float.NaN), 0.25f), "NaN delta changed progress.");
Check(Approximately(progress.Advance(0.15f), 1f) && progress.IsComplete,
    "Motion progress did not complete at its duration.");
Check(Approximately(progress.Advance(1f), 1f), "Completed motion did not remain complete.");

progress.Begin(0f);
Check(progress.IsComplete && Approximately(progress.Advance(0f), 1f), "Zero-duration motion was not instant.");
progress.Begin(float.NaN);
Check(progress.IsComplete, "NaN duration was not normalized to instant.");
progress.Begin(1f);
Check(Approximately(progress.Advance(float.PositiveInfinity), 1f) && progress.IsComplete,
    "Infinite frame delta did not settle the motion.");

Check(UiMotionTokens.Hover <= 0.2f && UiMotionTokens.Modal <= 0.2f && UiMotionTokens.ToastIn <= 0.2f,
    "A standard UI motion token exceeds the 200ms budget.");
int completed = 0;
UiMotionCompletion completion = new();
completion.Reset(() => completed++);
completion.Cancel();
completion.Invoke();
Check(completed == 0, "Cancel unexpectedly invoked completion.");
completion.Reset(() => completed++);
completion.Reset(() => completed += 10);
completion.Invoke();
completion.Invoke();
Check(completed == 10, "Retarget completion was not replaced or invoked exactly once.");

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string rootSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "PluginUiRoot.cs"));
string handleSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "Motion", "UiMotionHandle.cs"));
string selectableSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "OverlaySelectableVisual.cs"));
string factorySource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "ControlLibrary", "Runtime", "LegacyFluentControlFactory.cs"));
string transientSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "Motion", "UiTransientMotion.cs"));
string overlaySource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.cs"));
string overlayMotionSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.Motion.cs"));
string fluentShellSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.FluentShell.cs"));
string toolsShellSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.ToolsShell.cs"));
string modalViewsSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.ModalViews.cs"));
string toastSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "UiToastService.cs"));
string allOverlaySource = string.Join("\n", Directory.EnumerateFiles(
        Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI"), "CheatMenuOverlayUi*.cs")
    .OrderBy(path => path, StringComparer.Ordinal)
    .Select(File.ReadAllText));
Check(rootSource.Contains("_motion?.Pump(Time.unscaledDeltaTime)", StringComparison.Ordinal),
    "PluginUiRoot does not pump motion with unscaled delta time.");
Check(rootSource.Contains("_motion?.SnapAll();", StringComparison.Ordinal) &&
      !ReadMethodBody(rootSource, "internal void HandleSceneChanged()").Contains("CancelAll", StringComparison.Ordinal),
    "Scene changes can swallow transient completion cleanup instead of settling it.");
string pumpBody = ReadMethodBody(File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "Motion", "UiMotionDriver.cs")), "internal void Pump(float unscaledDeltaTime)");
string snapAllBody = ReadMethodBody(File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "Motion", "UiMotionDriver.cs")), "internal void SnapAll()");
Check(pumpBody.Contains("finished && !handle.IsActive && _handles.Remove(handle)", StringComparison.Ordinal),
    "A completion retarget can be removed by the same Pump iteration.");
int removeBeforeSnap = snapAllBody.IndexOf("_handles.RemoveAt(index)", StringComparison.Ordinal);
int snapAfterRemove = snapAllBody.IndexOf("handle?.SnapToEnd()", StringComparison.Ordinal);
Check(removeBeforeSnap >= 0 && snapAfterRemove > removeBeforeSnap,
    "SnapAll invokes completion before removing the old handle and is not re-entrancy safe.");
Check(ReadMethodBody(handleSource, "internal void Retarget(").Contains("_completion.Reset(completed)", StringComparison.Ordinal) &&
      ReadMethodBody(handleSource, "internal void Cancel()").Contains("_completion.Cancel()", StringComparison.Ordinal) &&
      ReadMethodBody(handleSource, "internal void SnapToEnd()").Contains("_completion.Invoke()", StringComparison.Ordinal),
    "Runtime motion handle does not preserve deterministic retarget/cancel/snap completion semantics.");
Check(ReadMethodBody(handleSource, "internal void SnapToEnd()").Contains("if (Owner == null)", StringComparison.Ordinal) &&
      ReadMethodBody(handleSource, "internal void SnapToEnd()").Contains("Cancel();", StringComparison.Ordinal),
    "Destroyed Unity owners can execute stale apply/completion callbacks during forced settle.");
string refreshFluent = ReadMethodBody(selectableSource, "private void RefreshFluent(bool animate)");
Check(!refreshFluent.Contains("transform.localScale", StringComparison.Ordinal) &&
      !refreshFluent.Contains("UiMotionChannel.Scale", StringComparison.Ordinal),
    "Selectable motion scales the Button/Toggle hit target instead of preserving its geometry.");
Check(refreshFluent.Contains("previousState == state && !targetsChanged", StringComparison.Ordinal),
    "Equivalent selectable state updates can restart the transition indefinitely.");
Check(factorySource.Contains("UiMotionChannel.SwitchThumbColor", StringComparison.Ordinal) &&
      !ReadMethodBody(factorySource, "private void UpdateSwitch(").Contains("UiMotionChannel.TextColor", StringComparison.Ordinal),
    "Switch thumb color still competes with the row-label text channel.");
Check(factorySource.Contains("handle.InteractableChanged = interactable", StringComparison.Ordinal) &&
	  factorySource.Contains("Color disabledActiveFill = activeColor ?? _theme.AccentSoft.ToUnity()", StringComparison.Ordinal) &&
	  factorySource.Contains("disabledActiveFill.a = Mathf.Min(disabledActiveFill.a, 150f / 255f)", StringComparison.Ordinal) &&
      factorySource.Contains("_theme.Border.WithAlpha(120)", StringComparison.Ordinal),
    "Disabled switches do not synchronously settle both ON and OFF track states.");
Check(overlaySource.Contains("Color baseColor = CheatMenuStyle.Surface;", StringComparison.Ordinal) &&
      !ReadMethodBody(overlaySource, "private void CreateToggleAction(").Contains("SetBaseColor(value ?", StringComparison.Ordinal) &&
      factorySource.Contains("_theme.SurfaceRaised", StringComparison.Ordinal) &&
      factorySource.Contains("_theme.BorderStrong", StringComparison.Ordinal),
    "Toggle row/track accent boundary does not match the accepted OFF/ON design.");
string adapterSource = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "LegacyFluentControlAdapter.cs"));
Check(ReadMethodBody(adapterSource, "private FluentSelectablePalette ResolveTogglePalette()")
          .Contains("_context.Theme.SurfaceHover", StringComparison.Ordinal) &&
      !ReadMethodBody(adapterSource, "private FluentSelectablePalette ResolveTogglePalette()")
          .Contains("FocusSoft", StringComparison.Ordinal),
    "Toggle keyboard focus still paints the entire row with an accent-derived fill.");
Check(transientSource.Contains("Vector3 restScale", StringComparison.Ordinal) &&
      !transientSource.Contains("UiTransientRestState", StringComparison.Ordinal),
    "Transient entry does not accept an explicit per-open rest target and can drift across trigger positions.");
Check(transientSource.Contains("class UiPopupMotionOffset", StringComparison.Ordinal) &&
      allOverlaySource.Split("SetPopupMotionPosition(", StringSplitOptions.None).Length - 1 >= 6 &&
      overlayMotionSource.Contains("UiMotionTokens.PopupOffset", StringComparison.Ordinal),
    "Popup motion does not preserve trigger-follow positioning while applying the four-pixel entry offset.");
Check(factorySource.Contains("animate: toggle.interactable", StringComparison.Ordinal),
    "A disabled Toggle can animate when its value is changed programmatically.");
string initializeRoot = ReadMethodBody(rootSource, "private void Initialize()");
Check(initializeRoot.Contains("if (_motion == null) _motion = new UiMotionDriver();", StringComparison.Ordinal) &&
      initializeRoot.Split("new UiMotionDriver()", StringSplitOptions.None).Length - 1 == 1,
    "PluginUiRoot recovery replaces the driver cached by adapters instead of retaining one stable instance.");
Check(ReadMethodBody(overlaySource, "internal void HandleSceneChanged()").Contains("RunWithoutTransientMotion(CloseTransientVisualSurfaces)", StringComparison.Ordinal),
    "Scene changes rebuild pages without synchronously closing Modal/Popup/Tooltip surfaces.");
Check(!allOverlaySource.Contains("InfiniteStorageDecomposeScopeDrawer", StringComparison.Ordinal),
    "The retired Infinite Storage PopupLayer drawer still participates in motion lifecycle.");
Check(overlayMotionSource.Contains("Dictionary<int, GameObject> _closingTransientSurfaces", StringComparison.Ordinal) &&
      ReadMethodBody(overlayMotionSource, "private void RunWithoutTransientMotion(Action action)").Contains("FlushClosingTransientSurfaces();", StringComparison.Ordinal),
    "Forced close tracks only ids or fails to flush already-closing roots.");
Check(!ReadMethodBody(overlayMotionSource, "private void QueueTransientExit(").Contains("UnregisterLayerSurface", StringComparison.Ordinal) &&
      ReadMethodBody(overlayMotionSource, "private void QueueForDestroy(GameObject target)").Contains("_uiRoot.UnregisterLayerSurface(target)", StringComparison.Ordinal),
    "Modal fade-out restores background focus before visual exit completes.");
Check(ReadMethodBody(overlayMotionSource, "private void QueuePopupExit(").Contains("_uiRoot.UnregisterLayerSurface(root)", StringComparison.Ordinal),
    "Popup close no longer restores its trigger synchronously with the baseline behavior.");
Check(ReadMethodBody(fluentShellSource, "private void ShowTab(").Contains("RunWithoutTransientMotion", StringComparison.Ordinal) &&
      ReadMethodBody(toolsShellSource, "private void ShowToolsTab(").Contains("RunWithoutTransientMotion", StringComparison.Ordinal),
    "Tab replacement can leave old transient surfaces over the new page.");
foreach (string modalOpen in new[] { "private void ShowAboutDialog()", "private void ShowFontPicker()", "private void ShowShortcutDialog()" })
{
    Check(ReadMethodBody(modalViewsSource, modalOpen).Contains("PrepareModalOpen();", StringComparison.Ordinal),
        "Modal-to-modal focus chain does not synchronously settle the previous modal: " + modalOpen);
}
Check(selectableSource.Contains("DetailedTooltipDelaySeconds = 0.45f", StringComparison.Ordinal) &&
      selectableSource.Contains("TooltipWarmWindowSeconds = 0.70f", StringComparison.Ordinal) &&
      selectableSource.Contains("_tooltipOwner", StringComparison.Ordinal) &&
      !selectableSource.Contains(">= 2f", StringComparison.Ordinal),
    "Tooltip browsing still has the two-second cold delay or lacks owner/warm-window continuity.");
Check(toastSource.Contains("private void BeginEnter(ToastEntry entry)", StringComparison.Ordinal) &&
      toastSource.Contains("private void BeginExit(ToastEntry entry)", StringComparison.Ordinal) &&
      toastSource.Contains("UiMotionChannel.ToastHeight", StringComparison.Ordinal) &&
      toastSource.Contains("blocksRaycasts = false", StringComparison.Ordinal) &&
      toastSource.Contains("layout.childControlWidth = true", StringComparison.Ordinal) &&
      toastSource.Contains("layout.childControlHeight = true", StringComparison.Ordinal) &&
      toastSource.Contains("layout.childForceExpandHeight = false", StringComparison.Ordinal),
    "Toast entry/exit/reflow motion or click-through behavior is incomplete.");
int modalRegistrations = allOverlaySource.Split("RegisterModal(", StringSplitOptions.None).Length - 1;
int modalAnimations = allOverlaySource.Split("AnimateModalEnter(_", StringSplitOptions.None).Length - 1;
int popupRegistrations = allOverlaySource.Split("RegisterPopup(", StringSplitOptions.None).Length - 1;
int popupAnimations = allOverlaySource.Split("AnimatePopupEnter(_", StringSplitOptions.None).Length - 1;
Check(modalRegistrations == modalAnimations && modalRegistrations >= 9,
    $"Not every modal enters through the motion path: register={modalRegistrations}, animate={modalAnimations}.");
Check(popupRegistrations == popupAnimations && popupRegistrations >= 5,
    $"Not every popup enters through the motion path: register={popupRegistrations}, animate={popupAnimations}.");
string protocolView = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.TranslationToolsView.cs"));
Check(protocolView.Contains("RunWithoutTransientMotion(HideWorkshopProtocolDrawer);", StringComparison.Ordinal),
    "Protocol selection rebuilds its trigger while the old drawer is still fading.");
string onClosed = ReadMethodBody(overlaySource, "public void OnClosed()");
foreach (string modalClose in new[]
{
    "HideConfirmDialog();", "HideAboutDialog();", "HideFontPicker();", "HideShortcutDialog();",
    "HideTeleportColorPicker();", "HideVehicleThumbnailSettings();", "HideItemFilterPanel();",
    "HideAutoPickupBlacklistPicker();", "HideAutoPickupRarityPicker();"
})
{
    Check(onClosed.Contains(modalClose, StringComparison.Ordinal),
        "Menu close does not synchronously retire every global ModalLayer surface: " + modalClose);
}

Console.WriteLine("UI motion smoke checks passed.");

static string ReadMethodBody(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    Check(signatureIndex >= 0, $"Production method is missing: {signature}");
    int openBrace = source.IndexOf('{', signatureIndex);
    Check(openBrace >= 0, $"Production method body is missing: {signature}");
    int depth = 0;
    for (int index = openBrace; index < source.Length; index++)
    {
        if (source[index] == '{') depth++;
        else if (source[index] == '}' && --depth == 0) return source.Substring(openBrace, index - openBrace + 1);
    }
    throw new InvalidOperationException($"Production method body is unterminated: {signature}");
}
