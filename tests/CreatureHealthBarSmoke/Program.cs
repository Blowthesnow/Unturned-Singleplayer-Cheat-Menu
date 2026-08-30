using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

string serviceSource = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "UnturnedSingleplayerCheatMenu", "Services", "CreatureHealthBarService.cs")));
string hudSource = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "UnturnedSingleplayerCheatMenu", "UI", "CreatureHealthBarHud.cs")));
Check(serviceSource.Contains("gameObject.layer == 23", StringComparison.Ordinal), "Creature anchors must use Unturned Entity hitbox layer 23.");
Check(serviceSource.Contains("DamageTool.getLimb(collider.transform) == ELimb.SKULL", StringComparison.Ordinal), "Creature anchors must prefer the native Skull hitbox classification.");
Check(!serviceSource.Contains("Character/Skeleton/Spine/Skull", StringComparison.Ordinal), "Creature anchors must not depend on a guessed skeleton path.");
Check(serviceSource.Contains("viewmodelCamera", StringComparison.Ordinal), "Dual-render scope projection must use the first-person viewmodel camera.");
Check(serviceSource.Contains("TryMapScopeAnchor", StringComparison.Ordinal), "Dual-render scope projection must map scope viewport coordinates into the scope model rectangle.");
Check(serviceSource.Contains("sharedMesh.bounds", StringComparison.Ordinal), "Scope projection must use local mesh bounds instead of the world axis-aligned renderer bounds when available.");
Check(serviceSource.Contains("Single-render scopes first crop the main view", StringComparison.Ordinal), "Single-render scopes must reverse the native centered-square crop before lens mapping.");
Check(!serviceSource.Contains("scopeScreenRect = scopeCamera.pixelRect", StringComparison.Ordinal), "Single-render scopes must not treat Scope camera pixels as final HUD pixels.");
Check(serviceSource.Contains("IsUsing2DScope(player.look)", StringComparison.Ordinal), "2D scope overlays must not be treated as single-render 3D scopes.");
Check(!serviceSource.Contains("DEBUG-scope-reticule", StringComparison.Ordinal), "Temporary Reticule comparison logging must be removed after diagnosis.");
Check(hudSource.Contains("label.alignment = TextAnchor.MiddleCenter", StringComparison.Ordinal),
    "Health-bar values must remain horizontally and vertically center-aligned.");
Check(hudSource.Contains("labelRect.offsetMin = new Vector2(2f, 4f)", StringComparison.Ordinal)
      && hudSource.Contains("labelRect.offsetMax = new Vector2(-2f, 0f)", StringComparison.Ordinal),
    "Health-bar value bounds must keep their size while shifting two reference pixels upward for optical centering.");
Check(hudSource.Contains("typeof(Canvas), typeof(CanvasScaler)", StringComparison.Ordinal)
      && hudSource.Contains("CanvasScaler.ScaleMode.ConstantPixelSize", StringComparison.Ordinal)
      && hudSource.Contains("scaler.scaleFactor = 1f", StringComparison.Ordinal),
    "Health bars must retain the tooltip-feature pixel-space Canvas so distance scaling is not flattened by the unified UI scaler.");
Check(!hudSource.Contains("_uiRoot.HudLayer", StringComparison.Ordinal),
    "Health bars must not be parented under the unified HudLayer because its responsive Canvas changes the tooltip-feature sizing contract.");

Check(ScopeTextureViewportMapper.TryMap(0.4333f, 0.7348f, 3840f, 2160f, true, out float croppedX, out float croppedY),
    "A target inside the single-render middle-square crop must remain visible.");
Check(Math.Abs(croppedX - 0.38142222f) < 0.0001f && Math.Abs(croppedY - 0.7348f) < 0.0001f,
    "Single-render scope mapping must reverse SrScope's centered square crop instead of compressing horizontal movement.");
Check(ScopeTextureViewportMapper.TryMap(0.4333f, 0.7348f, 3840f, 2160f, false, out float dualX, out float dualY)
    && Math.Abs(dualX - 0.4333f) < 0.0001f && Math.Abs(dualY - 0.7348f) < 0.0001f,
    "Dual-render scope mapping must preserve the scope camera viewport.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Check(CreatureHealthBarState.FormatHealth(75f, 100f) == "75 / 100", "Health format should use current / maximum without a percentage.");
Check(CreatureHealthBarState.FormatHealth(120f, 100f) == "100 / 100", "Current health should clamp to the runtime maximum.");
Check(CreatureHealthBarState.FormatHealth(-2f, 100f) == "0 / 100", "Negative current health should clamp to zero.");
Check(CreatureHealthBarState.FormatHealth(10f, 0f) == string.Empty, "An empty maximum should hide rather than throw.");

CreatureHealthBarState state = new() { Kind = CreatureHealthBarKind.Zombie, InstanceId = 1 };
Check(float.IsPositiveInfinity(state.DistanceSqr), "New health-bar states must not render as zero-distance bars before camera distance is initialized.");
state.SetHealth(100f, 175f);
state.CandidateBeforeHealth = 100f;
state.CandidateAt = 1f;
state.HasDamageCandidate = true;
Check(!state.ConfirmDamage(100f, 1.1f, 10f), "A blocked or zero damage event must not start the timer.");
Check(!state.ShouldShow(2f), "An unconfirmed damage candidate must remain hidden.");

state.CandidateBeforeHealth = 100f;
state.CandidateAt = 2f;
state.HasDamageCandidate = true;
Check(state.ConfirmDamage(80f, 2.1f, 10f), "A real health decrease must start the timer.");
Check(state.ShouldShow(11.99f), "A confirmed hit should remain visible for the full duration.");
Check(state.ShouldShow(12.09f), "The timer should remain visible before the configured deadline.");
Check(!state.ShouldShow(12.1f), "The timer should expire at the configured deadline.");

state.CandidateBeforeHealth = 80f;
state.CandidateAt = 5f;
state.HasDamageCandidate = true;
Check(state.ConfirmDamage(70f, 5.1f, 10f), "A later hit should be confirmed.");
Check(state.ShouldShow(14.99f), "A later hit should replace, not add to, the deadline.");
Check(!state.ShouldShow(15.1f), "Repeated damage must not accumulate multiple display durations.");
state.SetHealth(90f, 175f);
Check(state.ShouldShow(14.99f), "Healing must update health without clearing the active damage timer.");

CreatureHealthBarState near = new() { InstanceId = 2, DistanceSqr = 4f, IsRenderCandidate = true };
near.SetHealth(20f, 20f);
near.IsCrosshairTarget = true;
CreatureHealthBarState farCrosshair = new() { InstanceId = 3, DistanceSqr = 400f, IsCrosshairTarget = true, IsRenderCandidate = true };
farCrosshair.SetHealth(20f, 20f);
CreatureHealthBarState occluded = new() { InstanceId = 4, DistanceSqr = 1f, IsCrosshairTarget = true, IsOccluded = true, IsRenderCandidate = true };
occluded.SetHealth(20f, 20f);
IReadOnlyList<CreatureHealthBarState> selected = CreatureHealthBarSelection.Select(new[] { occluded, farCrosshair, near }, 20f, 100f, 1);
Check(selected.Count == 1 && selected[0] == near, "Selection must filter range/occlusion and enforce the configured limit.");
CreatureHealthBarState damaged = new() { InstanceId = 5, DistanceSqr = 1f, CandidateBeforeHealth = 20f, CandidateAt = 19f, HasDamageCandidate = true, IsRenderCandidate = true };
damaged.SetHealth(20f, 20f);
damaged.ConfirmDamage(10f, 19.1f, 10f);
near.DistanceSqr = 9f;
selected = CreatureHealthBarSelection.Select(new[] { damaged, near }, 20f, 100f, 1);
Check(selected[0] == near, "The crosshair target must outrank a closer damaged target when the pool is full.");
IReadOnlyList<CreatureHealthBarState> candidates = CreatureHealthBarSelection.SelectCandidates(new[] { damaged, near }, 20f, 100f, 1);
Check(candidates.Count == 1 && candidates[0] == near,
    "The expensive occlusion stage must be capped after prioritizing the crosshair target.");

CreatureHealthBarSettings settings = new CreatureHealthBarSettings()
{
    DamageDisplayDuration = 999f,
    CrosshairRange = 1f,
    DisplayRange = 999f,
    MaxVisibleBars = 999,
    OcclusionCheckHz = 1f,
    HealthRefreshHz = 999f
}.Normalize();
Check(settings.DamageDisplayDuration == 30f, "Damage duration must clamp to 30 seconds.");
Check(settings.CrosshairRange == 5f && settings.DisplayRange == 250f, "Ranges must clamp to 5-250 meters.");
Check(settings.MaxVisibleBars == 24, "Visible bars must clamp to 24.");
Check(settings.OcclusionCheckHz == 5f && settings.HealthRefreshHz == 30f, "Refresh rates must clamp to their documented limits.");

CreatureHealthBarSettings scaleSettings = new CreatureHealthBarSettings().Normalize();
Check(Math.Abs(scaleSettings.GetBarScale(2f) - 1.5f) < 0.001f, "Near-distance scale must stop at the configured maximum.");
Check(Math.Abs(scaleSettings.GetBarScale(18f) - 1f) < 0.001f, "Reference distance must use the base scale.");
Check(Math.Abs(scaleSettings.GetBarScale(100f) - 0.75f) < 0.001f, "Far-distance scale must not shrink below the readability floor.");

CreatureHealthBarSettings minimums = new CreatureHealthBarSettings
{
    DamageDisplayDuration = -1f,
    MaxVisibleBars = 0,
    OcclusionCheckHz = float.NaN,
    HealthRefreshHz = float.PositiveInfinity
}.Normalize();
Check(minimums.DamageDisplayDuration == 1f && minimums.MaxVisibleBars == 1,
    "Damage duration and visible count must clamp to their documented minimums.");
Check(minimums.OcclusionCheckHz == 10f && minimums.HealthRefreshHz == 20f,
    "Non-finite refresh settings must fall back to documented defaults.");

Console.WriteLine("Creature health bar smoke checks passed.");
