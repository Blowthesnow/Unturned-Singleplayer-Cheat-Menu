using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

/// <summary>
/// Keeps one absolute cutaway plane for an unmapped instance. Player movement only
/// changes the camera's relative offset; it never drags the plane down a ramp.
/// </summary>
internal sealed class MinimapInstanceCutaway
{
	private const float CeilingClearance = 0.1f;
	private bool _hasMeasuredCeiling;

	internal bool Active { get; private set; }
	internal float CeilingY { get; private set; } = float.NaN;
	internal bool HasMeasuredCeiling => _hasMeasuredCeiling;

	internal bool Update(bool nativeCropVisible, bool realtimeDestinationLocked,
		int overheadSurfaceProbeCount, int requiredProbeCount, float fallbackCutPlaneY,
		float detectedCeilingY)
	{
		if (nativeCropVisible)
		{
			Reset();
			return false;
		}

		int required = Math.Max(1, requiredProbeCount);
		bool hasEntryEvidence = realtimeDestinationLocked ||
			overheadSurfaceProbeCount >= required;
		if (!Active && !hasEntryEvidence) return false;

		bool measured = IsFinite(detectedCeilingY);
		if (!Active)
		{
			Active = true;
			_hasMeasuredCeiling = measured;
			CeilingY = measured
				? detectedCeilingY
				: (IsFinite(fallbackCutPlaneY)
					? fallbackCutPlaneY + CeilingClearance
					: float.NaN);
			return true;
		}

		if (measured)
		{
			// The first real ceiling replaces an entry-height fallback. Afterwards only
			// lower the plane: a higher roof must never reappear above an established cutaway.
			CeilingY = _hasMeasuredCeiling
				? Math.Min(CeilingY, detectedCeilingY)
				: detectedCeilingY;
			_hasMeasuredCeiling = true;
		}
		else if (!IsFinite(CeilingY) && IsFinite(fallbackCutPlaneY))
		{
			CeilingY = fallbackCutPlaneY + CeilingClearance;
		}
		return true;
	}

	internal void Reset()
	{
		Active = false;
		_hasMeasuredCeiling = false;
		CeilingY = float.NaN;
	}

	private static bool IsFinite(float value) =>
		!float.IsNaN(value) && !float.IsInfinity(value);
}
