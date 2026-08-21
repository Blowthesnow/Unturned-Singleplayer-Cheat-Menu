namespace UnturnedSingleplayerCheatMenu.Models;

/// <summary>
/// Immutable results of the two Smart raycasts. Semantic resolution status is
/// diagnostic; a coordinate hit remains valid even when semantic resolution
/// failed. A static semantic surface can also be retained as a coordinate
/// fallback when the second physics query misses that same surface.
/// </summary>
internal readonly struct PointToolRaycastSnapshot
{
    internal PointToolRaycastSnapshot(
        bool semanticHit,
        bool semanticResolutionSucceeded,
        bool coordinatePhysicsHit,
        bool coordinateTerrainFallback,
        bool coordinateSemanticFallback = false)
    {
        SemanticHit = semanticHit;
        SemanticResolutionSucceeded = semanticResolutionSucceeded;
        CoordinatePhysicsHit = coordinatePhysicsHit;
        CoordinateTerrainFallback = coordinateTerrainFallback;
        CoordinateSemanticFallback = coordinateSemanticFallback;
        HasCoordinateHit = coordinatePhysicsHit
            || coordinateSemanticFallback
            || coordinateTerrainFallback;
    }

    internal bool SemanticHit { get; }
    internal bool SemanticResolutionSucceeded { get; }
    internal bool CoordinatePhysicsHit { get; }
    internal bool CoordinateSemanticFallback { get; }
    internal bool CoordinateTerrainFallback { get; }
    internal bool HasCoordinateHit { get; }
    internal bool HasAnyHit => SemanticHit || HasCoordinateHit;
}
