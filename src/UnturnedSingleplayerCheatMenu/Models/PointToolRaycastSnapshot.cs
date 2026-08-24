namespace UnturnedSingleplayerCheatMenu.Models;

internal readonly struct PointToolRaycastSnapshot
{
	internal bool SemanticHit { get; }

	internal bool SemanticResolutionSucceeded { get; }

	internal bool CoordinatePhysicsHit { get; }

	internal bool CoordinateSemanticFallback { get; }

	internal bool CoordinateTerrainFallback { get; }

	internal bool HasCoordinateHit { get; }

	internal bool HasAnyHit
	{
		get
		{
			if (!SemanticHit)
			{
				return HasCoordinateHit;
			}
			return true;
		}
	}

	internal PointToolRaycastSnapshot(bool semanticHit, bool semanticResolutionSucceeded, bool coordinatePhysicsHit, bool coordinateTerrainFallback, bool coordinateSemanticFallback = false)
	{
		SemanticHit = semanticHit;
		SemanticResolutionSucceeded = semanticResolutionSucceeded;
		CoordinatePhysicsHit = coordinatePhysicsHit;
		CoordinateTerrainFallback = coordinateTerrainFallback;
		CoordinateSemanticFallback = coordinateSemanticFallback;
		HasCoordinateHit = coordinatePhysicsHit | coordinateSemanticFallback | coordinateTerrainFallback;
	}
}
