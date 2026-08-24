using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

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
		{
			if (!input.HasSemanticTarget || !input.CanDelete)
			{
				return PointToolAction.None;
			}
			return PointToolAction.Delete;
		}
		if (input.CanRepair && input.NeedsRepair)
		{
			return PointToolAction.Repair;
		}
		if (input.CanUse)
		{
			return PointToolAction.Utility;
		}
		if (input.HasCoordinateHit)
		{
			return PointToolAction.Teleport;
		}
		if (input.HasSemanticTarget)
		{
			return PointToolAction.Inspect;
		}
		return PointToolAction.None;
	}

	internal static bool CanExecute(PointToolMode mode, bool hasCoordinateHit, bool hasSemanticTarget, bool deleteModifierHeld)
	{
		return GetFailure(mode, hasCoordinateHit, hasSemanticTarget, deleteModifierHeld) == Failure.None;
	}

	internal static Failure GetFailure(PointToolMode mode, bool hasCoordinateHit, bool hasSemanticTarget, bool deleteModifierHeld)
	{
		if (mode == PointToolMode.Delete && !deleteModifierHeld)
		{
			return Failure.DeleteModifierRequired;
		}
		switch (mode)
		{
		case PointToolMode.Inspect:
		case PointToolMode.Teleport:
			return (!hasCoordinateHit) ? Failure.NoWorldHit : Failure.None;
		case PointToolMode.Repair:
		case PointToolMode.Utility:
		case PointToolMode.Delete:
			return (!hasSemanticTarget) ? Failure.NoSemanticTarget : Failure.None;
		default:
			return Failure.NoSemanticTarget;
		}
	}
}
