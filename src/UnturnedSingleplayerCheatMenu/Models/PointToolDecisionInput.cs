namespace UnturnedSingleplayerCheatMenu.Models;

internal readonly struct PointToolDecisionInput
{
    internal PointToolDecisionInput(
        bool hasCoordinateHit,
        bool hasSemanticTarget,
        bool canRepair,
        bool needsRepair,
        bool canUse,
        bool canDelete,
        bool deleteModifierHeld)
    {
        HasCoordinateHit = hasCoordinateHit;
        HasSemanticTarget = hasSemanticTarget;
        CanRepair = canRepair;
        NeedsRepair = needsRepair;
        CanUse = canUse;
        CanDelete = canDelete;
        DeleteModifierHeld = deleteModifierHeld;
    }

    internal bool HasCoordinateHit { get; }
    internal bool HasSemanticTarget { get; }
    internal bool CanRepair { get; }
    internal bool NeedsRepair { get; }
    internal bool CanUse { get; }
    internal bool CanDelete { get; }
    internal bool DeleteModifierHeld { get; }
}
