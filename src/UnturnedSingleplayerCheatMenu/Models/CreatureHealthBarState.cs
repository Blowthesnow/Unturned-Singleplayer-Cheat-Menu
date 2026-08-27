using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.Models;

internal enum CreatureHealthBarKind
{
	Zombie,
	Animal
}

internal static class CreatureHealthBarSelection
{
	public static IReadOnlyList<CreatureHealthBarState> SelectCandidates(IEnumerable<CreatureHealthBarState> states, float now, float maxDistanceSqr, int limit)
	{
		if (states == null || limit <= 0)
		{
			return Array.Empty<CreatureHealthBarState>();
		}
		return states
			.Where(state => state != null && state.IsHealthUsable && state.ShouldShow(now) && state.DistanceSqr <= maxDistanceSqr)
			.OrderByDescending(state => state.IsCrosshairTarget)
			.ThenBy(state => state.DistanceSqr)
			.Take(limit)
			.ToArray();
	}

	public static IReadOnlyList<CreatureHealthBarState> Select(IEnumerable<CreatureHealthBarState> states, float now, float maxDistanceSqr, int limit)
	{
		if (states == null || limit <= 0)
		{
			return Array.Empty<CreatureHealthBarState>();
		}
		return states
			.Where(state => state != null && state.IsRenderCandidate && state.IsHealthUsable && state.ShouldShow(now) && !state.IsOccluded && state.DistanceSqr <= maxDistanceSqr)
			.OrderByDescending(state => state.IsCrosshairTarget)
			.ThenBy(state => state.DistanceSqr)
			.Take(limit)
			.ToArray();
	}
}

internal sealed class CreatureHealthBarState
{
	public CreatureHealthBarKind Kind { get; set; }
	public int InstanceId { get; set; }
	public object Creature { get; set; }
	public float LastKnownHealth { get; private set; }
	public float MaxHealth { get; private set; }
	public float ShowUntil { get; private set; }
	public float CandidateBeforeHealth { get; set; }
	public float CandidateAt { get; set; }
	public bool HasDamageCandidate { get; set; }
	public bool IsCrosshairTarget { get; set; }
	public float DistanceSqr { get; set; } = float.PositiveInfinity;
	public bool IsOccluded { get; set; }
	public bool IsRenderCandidate { get; set; }

	public void SetHealth(float current, float max)
	{
		LastKnownHealth = current;
		MaxHealth = max;
	}

	public bool ConfirmDamage(float current, float now, float duration)
	{
		bool damaged = current < CandidateBeforeHealth - 0.001f;
		HasDamageCandidate = false;
		if (damaged)
		{
			ShowUntil = now + duration;
		}
		LastKnownHealth = current;
		return damaged;
	}

	public bool ShouldShow(float now)
	{
		return IsCrosshairTarget || ShowUntil > now;
	}

	public bool IsHealthUsable => MaxHealth > 0f && !float.IsNaN(MaxHealth) && !float.IsInfinity(MaxHealth);

	public static string FormatHealth(float current, float max)
	{
		if (max <= 0f || float.IsNaN(max) || float.IsInfinity(max))
		{
			return string.Empty;
		}
		float normalizedCurrent = Math.Max(0f, Math.Min(current, max));
		return $"{normalizedCurrent:0} / {max:0}";
	}
}
