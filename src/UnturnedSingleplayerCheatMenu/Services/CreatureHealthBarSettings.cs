using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class CreatureHealthBarSettings
{
	public const float MinDamageDisplayDuration = 1f;
	public const float MaxDamageDisplayDuration = 30f;
	public const float MinRange = 5f;
	public const float MaxRange = 250f;
	public const int MinVisibleBars = 1;
	public const int MaxVisibleBarsLimit = 24;
	public const float MinBarScale = 0.75f;
	public const float MaxBarScaleLimit = 3f;

	public bool Enabled { get; set; }
	public float DamageDisplayDuration { get; set; } = 10f;
	public float CrosshairRange { get; set; } = 100f;
	public float DisplayRange { get; set; } = 100f;
	public int MaxVisibleBars { get; set; } = 12;
	public bool ShowAnimals { get; set; } = true;
	public bool ShowZombies { get; set; } = true;
	public float OcclusionCheckHz { get; set; } = 10f;
	public float HealthRefreshHz { get; set; } = 20f;
	public float BarScaleReferenceDistance { get; set; } = 18f;
	public float MaxBarScale { get; set; } = 1.5f;
	public bool Diagnostics { get; set; }

	public CreatureHealthBarSettings Normalize()
	{
		DamageDisplayDuration = ClampFinite(DamageDisplayDuration, 10f, MinDamageDisplayDuration, MaxDamageDisplayDuration);
		CrosshairRange = ClampFinite(CrosshairRange, 100f, MinRange, MaxRange);
		DisplayRange = ClampFinite(DisplayRange, 100f, MinRange, MaxRange);
		MaxVisibleBars = Math.Max(MinVisibleBars, Math.Min(MaxVisibleBars, MaxVisibleBarsLimit));
		OcclusionCheckHz = ClampFinite(OcclusionCheckHz, 10f, 5f, 20f);
		HealthRefreshHz = ClampFinite(HealthRefreshHz, 20f, 10f, 30f);
		BarScaleReferenceDistance = ClampFinite(BarScaleReferenceDistance, 18f, 1f, 100f);
		MaxBarScale = ClampFinite(MaxBarScale, 1.5f, MinBarScale, MaxBarScaleLimit);
		return this;
	}

	public float GetBarScale(float distance)
	{
		if (float.IsNaN(distance) || float.IsInfinity(distance))
		{
			distance = BarScaleReferenceDistance;
		}
		return Math.Max(MinBarScale, Math.Min(MaxBarScale, BarScaleReferenceDistance / Math.Max(distance, 0.1f)));
	}

	private static float ClampFinite(float value, float fallback, float min, float max)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			value = fallback;
		}
		return Math.Max(min, Math.Min(value, max));
	}
}
