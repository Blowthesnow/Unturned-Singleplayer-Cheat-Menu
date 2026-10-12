using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal readonly struct VerticalOcclusionInterval
{
	public float Bottom { get; }
	public float Top { get; }

	public VerticalOcclusionInterval(float bottom, float top)
	{
		Bottom = bottom;
		Top = top;
	}
}

internal readonly struct OcclusionProbeSummary
{
	public float EffectiveThickness { get; }
	public int SurfaceIntervalCount { get; }

	public OcclusionProbeSummary(float effectiveThickness, int surfaceIntervalCount)
	{
		EffectiveThickness = effectiveThickness;
		SurfaceIntervalCount = surfaceIntervalCount;
	}
}

internal readonly struct OcclusionSummary
{
	public int ProbeCount { get; }
	public int EnterProbeCount { get; }
	public int ExitProbeCount { get; }
	public float MaximumEffectiveThickness { get; }
	public bool MeetsEnterThreshold { get; }
	public bool MeetsExitThreshold { get; }

	public OcclusionSummary(int probeCount, int enterProbeCount, int exitProbeCount,
		float maximumEffectiveThickness, bool meetsEnterThreshold, bool meetsExitThreshold)
	{
		ProbeCount = probeCount;
		EnterProbeCount = enterProbeCount;
		ExitProbeCount = exitProbeCount;
		MaximumEffectiveThickness = maximumEffectiveThickness;
		MeetsEnterThreshold = meetsEnterThreshold;
		MeetsExitThreshold = meetsExitThreshold;
	}
}

internal static class OverheadOcclusionModel
{
	public static OcclusionSummary ApplyUndergroundEvidence(OcclusionSummary geometric,
		bool undergroundAllowlisted, float terrainCoverDepth, float enterThickness,
		float exitThickness)
	{
		float coverDepth = IsFinite(terrainCoverDepth) ? Math.Max(0f, terrainCoverDepth) : 0f;
		float enter = IsFinite(enterThickness) ? Math.Max(0f, enterThickness) : 0f;
		float exit = IsFinite(exitThickness) ? Math.Max(0f, exitThickness) : 0f;
		bool terrainMeetsEnter = coverDepth >= enter;
		bool terrainMeetsExit = coverDepth <= exit;
		return new OcclusionSummary(
			geometric.ProbeCount,
			geometric.EnterProbeCount,
			geometric.ExitProbeCount,
			Math.Max(geometric.MaximumEffectiveThickness, coverDepth),
			geometric.MeetsEnterThreshold || undergroundAllowlisted || terrainMeetsEnter,
			geometric.MeetsExitThreshold && !undergroundAllowlisted && terrainMeetsExit);
	}

	public static OcclusionSummary ApplyUnmappedInteriorEvidence(OcclusionSummary evidence,
		bool nativeCropVisible, int overheadSurfaceProbeCount, int requiredProbeCount)
	{
		int required = Math.Max(1, requiredProbeCount);
		bool unmappedInterior = !nativeCropVisible && overheadSurfaceProbeCount >= required;
		return new OcclusionSummary(
			evidence.ProbeCount,
			evidence.EnterProbeCount,
			evidence.ExitProbeCount,
			evidence.MaximumEffectiveThickness,
			evidence.MeetsEnterThreshold || unmappedInterior,
			evidence.MeetsExitThreshold && !unmappedInterior);
	}

	public static float ResolveCutawayCeilingY(IReadOnlyList<float> candidates)
	{
		int sourceCount = candidates?.Count ?? 0;
		if (sourceCount == 0) return float.NaN;
		float[] sorted = new float[sourceCount];
		int count = 0;
		for (int index = 0; index < sourceCount; index++)
		{
			float value = candidates[index];
			if (IsFinite(value)) sorted[count++] = value;
		}
		if (count == 0) return float.NaN;
		Array.Sort(sorted, 0, count);
		return sorted[count / 2];
	}

	public static OcclusionProbeSummary EvaluateProbe(IReadOnlyList<VerticalOcclusionInterval> intervals,
		float mergeGap, float floorExclusionMaxThickness)
	{
		if (intervals == null || intervals.Count == 0)
		{
			return default;
		}

		VerticalOcclusionInterval[] sorted = new VerticalOcclusionInterval[intervals.Count];
		int count = 0;
		for (int index = 0; index < intervals.Count; index++)
		{
			VerticalOcclusionInterval interval = intervals[index];
			if (!IsFinite(interval.Bottom) || !IsFinite(interval.Top) || interval.Top <= interval.Bottom)
			{
				continue;
			}
			sorted[count++] = interval;
		}
		if (count == 0)
		{
			return default;
		}
		Array.Sort(sorted, 0, count, VerticalIntervalComparer.Instance);

		float allowedGap = Math.Max(0f, mergeGap);
		float floorMaximum = Math.Max(0f, floorExclusionMaxThickness);
		float mergedBottom = sorted[0].Bottom;
		float mergedTop = sorted[0].Top;
		float maximumEffectiveThickness = 0f;
		int mergedCount = 0;
		for (int index = 1; index < count; index++)
		{
			VerticalOcclusionInterval interval = sorted[index];
			if (interval.Bottom - mergedTop <= allowedGap)
			{
				if (interval.Top > mergedTop) mergedTop = interval.Top;
				continue;
			}
			mergedCount++;
			AccumulateThickness(mergedTop - mergedBottom, floorMaximum, ref maximumEffectiveThickness);
			mergedBottom = interval.Bottom;
			mergedTop = interval.Top;
		}
		mergedCount++;
		AccumulateThickness(mergedTop - mergedBottom, floorMaximum, ref maximumEffectiveThickness);
		return new OcclusionProbeSummary(maximumEffectiveThickness, mergedCount);
	}

	public static OcclusionSummary Combine(IReadOnlyList<OcclusionProbeSummary> probes, int requiredProbeCount,
		float enterThickness, float exitThickness)
	{
		int count = probes?.Count ?? 0;
		int required = Math.Max(1, Math.Min(count, requiredProbeCount));
		int enterCount = 0;
		int exitCount = 0;
		float maximum = 0f;
		for (int index = 0; index < count; index++)
		{
			float thickness = probes[index].EffectiveThickness;
			if (thickness >= enterThickness) enterCount++;
			if (thickness <= exitThickness) exitCount++;
			if (thickness > maximum) maximum = thickness;
		}
		return new OcclusionSummary(count, enterCount, exitCount, maximum,
			count > 0 && enterCount >= required,
			count == 0 || exitCount >= required);
	}

	private static void AccumulateThickness(float thickness, float floorMaximum, ref float maximum)
	{
		if (thickness > floorMaximum && thickness > maximum)
		{
			maximum = thickness;
		}
	}

	private static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}

	private sealed class VerticalIntervalComparer : IComparer<VerticalOcclusionInterval>
	{
		public static readonly VerticalIntervalComparer Instance = new VerticalIntervalComparer();

		public int Compare(VerticalOcclusionInterval left, VerticalOcclusionInterval right)
		{
			int bottom = left.Bottom.CompareTo(right.Bottom);
			return bottom != 0 ? bottom : left.Top.CompareTo(right.Top);
		}
	}
}
