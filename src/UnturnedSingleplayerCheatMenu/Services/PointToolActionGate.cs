using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

/// <summary>
/// Keeps point-tool action admission separate from Unity raycast and target
/// resolution. A coordinate hit is enough for inspection and teleport, while
/// state-changing interactions require a supported semantic target.
/// </summary>
internal static class PointToolActionGate
{
    internal enum Failure
    {
        None,
        NoWorldHit,
        NoSemanticTarget,
        DeleteModifierRequired
    }

    internal static PointToolAction DecideSmart(PointToolDecisionInput input)
    {
        if (input.DeleteModifierHeld)
            return input.HasSemanticTarget && input.CanDelete
                ? PointToolAction.Delete
                : PointToolAction.None;

        if (input.CanRepair && input.NeedsRepair)
            return PointToolAction.Repair;

        if (input.CanUse)
            return PointToolAction.Utility;
        // The coordinate ray intentionally ignores semantic colliders such as
        // entities, vehicles, and containers. Consume it only when the
        // semantic target has no usable interaction, so aiming at a vehicle
        // or storage opens/uses it instead of teleporting behind it.
        if (input.HasCoordinateHit)
            return PointToolAction.Teleport;
        if (input.HasSemanticTarget)
            return PointToolAction.Inspect;
        return PointToolAction.None;
    }

    internal static bool CanExecute(
        PointToolMode mode,
        bool hasCoordinateHit,
        bool hasSemanticTarget,
        bool deleteModifierHeld)
    {
        return GetFailure(
            mode,
            hasCoordinateHit,
            hasSemanticTarget,
            deleteModifierHeld) == Failure.None;
    }

    internal static Failure GetFailure(
        PointToolMode mode,
        bool hasCoordinateHit,
        bool hasSemanticTarget,
        bool deleteModifierHeld)
    {
        if (mode == PointToolMode.Delete && !deleteModifierHeld)
            return Failure.DeleteModifierRequired;

        return mode switch
        {
            PointToolMode.Inspect or PointToolMode.Teleport =>
                hasCoordinateHit ? Failure.None : Failure.NoWorldHit,
            PointToolMode.Repair or PointToolMode.Utility or PointToolMode.Delete =>
                hasSemanticTarget ? Failure.None : Failure.NoSemanticTarget,
            _ => Failure.NoSemanticTarget
        };
    }
}
