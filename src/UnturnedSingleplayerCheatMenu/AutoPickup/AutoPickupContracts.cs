using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal enum AutoPickupFilterMode
{
	AllowAll,
	WhitelistOnly,
	BlacklistOnly,
	WhitelistThenBlacklist
}

internal enum ItemFilterRuleAction
{
	Allow,
	Deny
}

internal enum ItemFilterRuleField
{
	AssetId,
	Guid,
	AssetName,
	DisplayName,
	ItemType,
	Category,
	Keyword
}

internal enum AutoPickupDiagnosticsLevel
{
	Off,
	Failures,
	Summary,
	Verbose
}

internal enum AutoPickupResultKind
{
	Requested,
	PickedUp,
	Filtered,
	RejectedByDistance,
	RejectedByCapacity,
	StaleDrop,
	RateLimited,
	UnknownFailure
}

internal sealed class AutoPickupSettings
{
	internal const float MinRange = 1f;
	internal const float MaxRange = 1000f;
	internal const float MinFrequencyHz = 1f;
	internal const float MaxFrequencyHz = 20f;
	internal const int MinRequestsPerTick = 1;
	internal const int MaxRequestsPerTick = 16;
	internal const int MinDropBlockMinutes = 1;
	internal const int MaxDropBlockMinutes = 1440;
	internal const int MinQuality = 0;
	internal const int MaxQuality = 100;

	public bool Enabled { get; set; }

	public float Range { get; set; } = 40f;

	public float FrequencyHz { get; set; } = 10f;

	public int RequestsPerTick { get; set; } = 4;

	public int DropBlockMinutes { get; set; } = 1;

	public bool QualityFilterEnabled { get; set; }

	public int MinimumQuality { get; set; }

	public bool PauseWhenMenuOpen { get; set; } = true;

	public bool PauseWhenDead { get; set; } = true;

	public bool UseMinimapRange { get; set; }

	public bool UseMinimapRarityFilter { get; set; }

	public AutoPickupDiagnosticsLevel Diagnostics { get; set; } = AutoPickupDiagnosticsLevel.Failures;

	public AutoPickupSettings Normalize()
	{
		Range = Clamp(Range, MinRange, MaxRange);
		FrequencyHz = Clamp(FrequencyHz, MinFrequencyHz, MaxFrequencyHz);
		RequestsPerTick = Math.Max(MinRequestsPerTick, Math.Min(MaxRequestsPerTick, RequestsPerTick));
		DropBlockMinutes = Math.Max(MinDropBlockMinutes, Math.Min(MaxDropBlockMinutes, DropBlockMinutes));
		MinimumQuality = Math.Max(MinQuality, Math.Min(MaxQuality, MinimumQuality));
		return this;
	}

	public AutoPickupSettings Clone()
	{
		return new AutoPickupSettings
		{
			Enabled = Enabled,
			Range = Range,
			FrequencyHz = FrequencyHz,
			RequestsPerTick = RequestsPerTick,
			DropBlockMinutes = DropBlockMinutes,
			QualityFilterEnabled = QualityFilterEnabled,
			MinimumQuality = MinimumQuality,
			PauseWhenMenuOpen = PauseWhenMenuOpen,
			PauseWhenDead = PauseWhenDead,
			UseMinimapRange = UseMinimapRange,
			UseMinimapRarityFilter = UseMinimapRarityFilter,
			Diagnostics = Diagnostics
		};
	}

	public bool AllowsQuality(byte quality)
	{
		return !QualityFilterEnabled || quality >= MinimumQuality;
	}

	private static float Clamp(float value, float min, float max)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			return min;
		}
		return Math.Max(min, Math.Min(max, value));
	}
}

internal sealed class AutoPickupDropBlockRegistry
{
	private readonly Dictionary<ushort, float> _expiresAtByItemId = new Dictionary<ushort, float>();

	public void Block(ushort itemId, float now, float durationSeconds)
	{
		_expiresAtByItemId[itemId] = now + Math.Max(0f, durationSeconds);
	}

	public bool IsBlocked(ushort itemId, float now)
	{
		if (!_expiresAtByItemId.TryGetValue(itemId, out float expiresAt))
		{
			return false;
		}
		if (expiresAt > now)
		{
			return true;
		}
		_expiresAtByItemId.Remove(itemId);
		return false;
	}

	public void Clear()
	{
		_expiresAtByItemId.Clear();
	}
}

internal sealed class ItemFilterRule
{
	public string Id { get; set; } = System.Guid.NewGuid().ToString("N");

	public bool Enabled { get; set; } = true;

	public ItemFilterRuleAction Action { get; set; }

	public ItemFilterRuleField Field { get; set; }

	public string Value { get; set; } = string.Empty;

	public ItemFilterRule Clone()
	{
		return new ItemFilterRule
		{
			Id = Id,
			Enabled = Enabled,
			Action = Action,
			Field = Field,
			Value = Value
		};
	}
}

internal sealed class AutoPickupRuleDocument
{
	public int Version { get; set; } = 2;

	public bool Enabled { get; set; }

	public AutoPickupFilterMode Mode { get; set; } = AutoPickupFilterMode.BlacklistOnly;

	public List<ItemFilterRule> Rules { get; set; } = new List<ItemFilterRule>();
}

internal sealed class AutoPickupItemDescriptor
{
	public ushort AssetId { get; set; }

	public string Guid { get; set; } = string.Empty;

	public string AssetName { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string ItemType { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;
}

internal readonly struct AutoPickupDiagnosticsSnapshot
{
	public int CandidateCount { get; }

	public long ScanCount { get; }

	public long RequestedCount { get; }

	public long PickedUpCount { get; }

	public long FilteredCount { get; }

	public long FailureCount { get; }

	public long RetryCount { get; }

	public float LastScanTime { get; }

	public string LastFailure { get; }

	public string State { get; }

	public AutoPickupDiagnosticsSnapshot(int candidateCount, long scanCount, long requestedCount, long pickedUpCount, long filteredCount, long failureCount, long retryCount, float lastScanTime, string lastFailure, string state)
	{
		CandidateCount = candidateCount;
		ScanCount = scanCount;
		RequestedCount = requestedCount;
		PickedUpCount = pickedUpCount;
		FilteredCount = filteredCount;
		FailureCount = failureCount;
		RetryCount = retryCount;
		LastScanTime = lastScanTime;
		LastFailure = lastFailure ?? string.Empty;
		State = state ?? string.Empty;
	}
}

internal readonly struct AutoPickupRuleLoadResult
{
	public int Accepted { get; }

	public int Rejected { get; }

	public string Error { get; }

	public bool Success => string.IsNullOrEmpty(Error);

	public AutoPickupRuleLoadResult(int accepted, int rejected, string error)
	{
		Accepted = accepted;
		Rejected = rejected;
		Error = error ?? string.Empty;
	}
}
