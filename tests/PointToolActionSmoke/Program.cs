using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Assert(
    PointToolActionGate.CanExecute(
        PointToolMode.Inspect,
        hasCoordinateHit: true,
        hasSemanticTarget: false,
        deleteModifierHeld: false),
    "Inspect should accept a plain world-surface hit.");
Assert(
    !PointToolActionGate.CanExecute(
        PointToolMode.Inspect,
        hasCoordinateHit: false,
        hasSemanticTarget: false,
        deleteModifierHeld: false),
    "Inspect should reject a miss.");
Assert(
    PointToolActionGate.GetFailure(
        PointToolMode.Inspect,
        hasCoordinateHit: false,
        hasSemanticTarget: false,
        deleteModifierHeld: false)
        == PointToolActionGate.Failure.NoWorldHit,
    "Inspect misses should report NoWorldHit.");

Assert(
    PointToolActionGate.CanExecute(
        PointToolMode.Teleport,
        hasCoordinateHit: true,
        hasSemanticTarget: false,
        deleteModifierHeld: false),
    "Teleport should accept a plain world-surface hit.");
Assert(
    !PointToolActionGate.CanExecute(
        PointToolMode.Teleport,
        hasCoordinateHit: false,
        hasSemanticTarget: false,
        deleteModifierHeld: false),
    "Teleport should reject a miss.");
Assert(
    PointToolActionGate.GetFailure(
        PointToolMode.Teleport,
        hasCoordinateHit: false,
        hasSemanticTarget: false,
        deleteModifierHeld: false)
        == PointToolActionGate.Failure.NoWorldHit,
    "Teleport misses should report NoWorldHit.");

foreach (PointToolMode mode in new[]
{
    PointToolMode.Repair,
    PointToolMode.Utility
})
{
    Assert(
        PointToolActionGate.CanExecute(
            mode,
            hasCoordinateHit: true,
            hasSemanticTarget: true,
            deleteModifierHeld: false),
        $"{mode} should accept a supported semantic target.");
    Assert(
        !PointToolActionGate.CanExecute(
            mode,
            hasCoordinateHit: true,
            hasSemanticTarget: false,
            deleteModifierHeld: false),
        $"{mode} should reject a plain world-surface hit.");
    Assert(
        PointToolActionGate.GetFailure(
            mode,
            hasCoordinateHit: true,
            hasSemanticTarget: false,
            deleteModifierHeld: false)
            == PointToolActionGate.Failure.NoSemanticTarget,
        $"{mode} should report NoSemanticTarget.");
}

Assert(
    PointToolActionGate.CanExecute(
        PointToolMode.Delete,
        hasCoordinateHit: true,
        hasSemanticTarget: true,
        deleteModifierHeld: true),
    "Delete should accept a semantic target with Shift held.");
Assert(
    !PointToolActionGate.CanExecute(
        PointToolMode.Delete,
        hasCoordinateHit: true,
        hasSemanticTarget: true,
        deleteModifierHeld: false),
    "Delete should require Shift.");
Assert(
    PointToolActionGate.GetFailure(
        PointToolMode.Delete,
        hasCoordinateHit: true,
        hasSemanticTarget: true,
        deleteModifierHeld: false)
        == PointToolActionGate.Failure.DeleteModifierRequired,
    "Delete without Shift should report DeleteModifierRequired.");
Assert(
    !PointToolActionGate.CanExecute(
        PointToolMode.Delete,
        hasCoordinateHit: true,
        hasSemanticTarget: false,
        deleteModifierHeld: true),
    "Delete should reject a plain world-surface hit.");

static PointToolDecisionInput Smart(
    bool coordinate,
    bool semantic,
    bool repair,
    bool needsRepair,
    bool use,
    bool delete,
    bool shift) => new(coordinate, semantic, repair, needsRepair, use, delete, shift);

Assert(PointToolActionGate.DecideSmart(Smart(true, true, false, false, false, true, true)) == PointToolAction.Delete,
    "Smart Shift-delete should choose Delete when target is deletable.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, false, false, false, false, true)) == PointToolAction.None,
    "Smart Shift on an undeletable target must not fall back to another action.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, true, true, true, false, false)) == PointToolAction.Repair,
    "Smart damaged repairable target should choose Repair before Utility.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, true, true, false, false, false)) == PointToolAction.Repair,
    "Smart damaged structure or barricade should choose Repair.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, true, false, true, false, false)) == PointToolAction.Utility,
    "Smart usable vehicle should choose Utility before a behind-target coordinate.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, false, false, true, false, false)) == PointToolAction.Utility,
    "Smart usable container should choose Utility before a behind-target coordinate.");
Assert(PointToolActionGate.DecideSmart(Smart(false, true, false, false, true, false, false)) == PointToolAction.Utility,
    "Smart should choose Utility when a usable target has no coordinate candidate.");
Assert(PointToolActionGate.DecideSmart(Smart(false, true, true, false, false, false, false)) == PointToolAction.Inspect,
    "Smart target with no executable action should choose Inspect.");
Assert(PointToolActionGate.DecideSmart(Smart(true, true, false, false, false, false, false)) == PointToolAction.Teleport,
    "Smart should use the coordinate ray when no semantic action applies.");
Assert(PointToolActionGate.DecideSmart(Smart(false, true, false, false, false, false, false)) == PointToolAction.Inspect,
    "Smart should inspect a semantic target when the coordinate ray has no candidate.");
Assert(PointToolActionGate.DecideSmart(Smart(true, false, false, false, false, false, false)) == PointToolAction.Teleport,
    "Smart world surface should choose Teleport.");
Assert(PointToolActionGate.DecideSmart(Smart(false, false, false, false, false, false, false)) == PointToolAction.None,
    "Smart miss should choose None.");

PointToolRaycastSnapshot semanticFailureWithGround = new(
    semanticHit: true,
    semanticResolutionSucceeded: false,
    coordinatePhysicsHit: true,
    coordinateTerrainFallback: false);
Assert(semanticFailureWithGround.HasCoordinateHit,
    "A semantic-resolution failure must not discard the coordinate candidate.");
Assert(semanticFailureWithGround.HasAnyHit,
    "A semantic hit with a coordinate candidate must remain an actionable snapshot.");

PointToolRaycastSnapshot terrainFallback = new(
    semanticHit: true,
    semanticResolutionSucceeded: true,
    coordinatePhysicsHit: false,
    coordinateTerrainFallback: true);
Assert(terrainFallback.HasCoordinateHit,
    "Terrain-height fallback must produce a coordinate candidate.");

PointToolRaycastSnapshot semanticGroundFallback = new(
    semanticHit: true,
    semanticResolutionSucceeded: true,
    coordinatePhysicsHit: false,
    coordinateTerrainFallback: false,
    coordinateSemanticFallback: true);
Assert(semanticGroundFallback.HasCoordinateHit,
    "A static semantic ground fallback must produce a coordinate candidate.");
Assert(semanticGroundFallback.CoordinateSemanticFallback,
    "The snapshot must retain that the coordinate came from the semantic ground fallback.");

PointToolMode[] expectedModes =
{
    PointToolMode.Inspect,
    PointToolMode.Repair,
    PointToolMode.Teleport,
    PointToolMode.Utility,
    PointToolMode.Delete,
    PointToolMode.Smart
};
Assert(Enum.GetValues<PointToolMode>().SequenceEqual(expectedModes),
    "Smart must be appended after the five stable manual mode values.");
foreach (PointToolMode mode in expectedModes)
{
    Assert(Enum.TryParse(mode.ToString(), true, out PointToolMode parsed) && parsed == mode,
        $"Configured mode {mode} should remain parseable.");
}
Assert(!Enum.TryParse("not-a-mode", true, out PointToolMode _),
    "Invalid configured mode should be detectable for Smart fallback without rewriting the source value.");

Console.WriteLine("Point-tool action smoke checks passed.");
