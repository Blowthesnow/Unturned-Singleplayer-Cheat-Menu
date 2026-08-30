using System;
using System.Collections.Generic;
using SDG.Framework.Devkit;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class OverheadOcclusionDetector
{
	private const int ProbeCount = 5;
	private const int HitCapacity = 32;
	private const float MaximumProbeHeight = 96f;
	private static readonly Vector3[] ProbeOffsets =
	{
		Vector3.zero,
		new Vector3(0.32f, 0f, 0f),
		new Vector3(-0.32f, 0f, 0f),
		new Vector3(0f, 0f, 0.32f),
		new Vector3(0f, 0f, -0.32f)
	};
	private readonly RaycastHit[][] _upHits = new RaycastHit[ProbeCount][];
	private readonly RaycastHit[][] _downHits = new RaycastHit[ProbeCount][];
	private readonly List<VerticalOcclusionInterval>[] _intervals = new List<VerticalOcclusionInterval>[ProbeCount];
	private readonly List<OcclusionProbeSummary> _probeSummaries = new List<OcclusionProbeSummary>(ProbeCount);
	private readonly int _mask;

	public OverheadOcclusionDetector()
	{
		_mask = RayMasks.DEFAULT | RayMasks.RESOURCE | RayMasks.LARGE | RayMasks.MEDIUM | RayMasks.SMALL |
			RayMasks.ENVIRONMENT | RayMasks.GROUND | RayMasks.GROUND2 | RayMasks.BARRICADE |
			RayMasks.STRUCTURE | RayMasks.CLIP;
		for (int index = 0; index < ProbeCount; index++)
		{
			_upHits[index] = new RaycastHit[HitCapacity];
			_downHits[index] = new RaycastHit[HitCapacity];
			_intervals[index] = new List<VerticalOcclusionInterval>(HitCapacity);
		}
	}

	public OverheadOcclusionDiagnostics Probe(Player player, MinimapSettings settings)
	{
		if (player == null)
		{
			return new OverheadOcclusionDiagnostics(
				OverheadOcclusionModel.Combine(Array.Empty<OcclusionProbeSummary>(), 3,
					settings.OcclusionEnterThickness, settings.OcclusionExitThickness), false);
		}

		Vector3 head = player.look?.aim != null
			? player.look.aim.position
			: player.transform.position + Vector3.up * 1.6f;
		_probeSummaries.Clear();
		for (int probeIndex = 0; probeIndex < ProbeCount; probeIndex++)
		{
			Vector3 bottomOrigin = head + ProbeOffsets[probeIndex];
			Vector3 topOrigin = bottomOrigin + Vector3.up * MaximumProbeHeight;
			int upCount = Physics.RaycastNonAlloc(bottomOrigin, Vector3.up, _upHits[probeIndex],
				MaximumProbeHeight, _mask, QueryTriggerInteraction.Ignore);
			int downCount = Physics.RaycastNonAlloc(topOrigin, Vector3.down, _downHits[probeIndex],
				MaximumProbeHeight, _mask, QueryTriggerInteraction.Ignore);
			List<VerticalOcclusionInterval> intervals = _intervals[probeIndex];
			intervals.Clear();
			for (int upIndex = 0; upIndex < upCount; upIndex++)
			{
				RaycastHit lower = _upHits[probeIndex][upIndex];
				Collider collider = lower.collider;
				if (collider == null)
				{
					continue;
				}
				float bottom = bottomOrigin.y + lower.distance;
				float nearestTop = float.PositiveInfinity;
				for (int downIndex = 0; downIndex < downCount; downIndex++)
				{
					RaycastHit upper = _downHits[probeIndex][downIndex];
					if (upper.collider != collider)
					{
						continue;
					}
					float top = topOrigin.y - upper.distance;
					if (top > bottom && top < nearestTop)
					{
						nearestTop = top;
					}
				}
				if (!float.IsPositiveInfinity(nearestTop))
				{
					intervals.Add(new VerticalOcclusionInterval(bottom, nearestTop));
				}
			}
			_probeSummaries.Add(OverheadOcclusionModel.EvaluateProbe(intervals,
				settings.OcclusionMergeGap, settings.FloorExclusionMaxThickness));
		}

		OcclusionSummary summary = OverheadOcclusionModel.Combine(_probeSummaries, 3,
			settings.OcclusionEnterThickness, settings.OcclusionExitThickness);
		bool allowlisted = false;
		try
		{
			allowlisted = UndergroundWhitelistVolumeManager.Get().IsPositionInsideAnyVolume(head);
		}
		catch
		{
			// Author volume data is optional evidence; geometric detection remains authoritative.
		}
		return new OverheadOcclusionDiagnostics(summary, allowlisted);
	}
}
