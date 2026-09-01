using System;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>Keeps sub-step pointer motion so slow circular drags are not discarded by snapping.</summary>
internal sealed class WorldTimeRingDragAccumulator
{
	private float _unsnappedHours;
	private float _lastAngle;

	internal bool IsActive { get; private set; }

	internal void Begin(float currentHours, float pointerAngle)
	{
		_unsnappedHours = Sanitize(currentHours);
		_lastAngle = NormalizeAngle(pointerAngle);
		IsActive = true;
	}

	internal float Continue(float pointerAngle, float step)
	{
		float angle = NormalizeAngle(pointerAngle);
		if (!IsActive)
		{
			Begin(0f, angle);
		}

		_unsnappedHours += ShortestAngleDelta(_lastAngle, angle) / 30f;
		_lastAngle = angle;
		return NormalizeHours(SnapToStep(_unsnappedHours, step));
	}

	internal void End()
	{
		IsActive = false;
	}

	private static float SnapToStep(float hours, float step)
	{
		float safeStep = float.IsNaN(step) || float.IsInfinity(step) || step <= 0f ? 0.25f : step;
		return (float)(Math.Round(hours / safeStep, MidpointRounding.ToEven) * safeStep);
	}

	private static float ShortestAngleDelta(float fromAngle, float toAngle)
	{
		float delta = Repeat(toAngle - fromAngle, 360f);
		return delta > 180f ? delta - 360f : delta;
	}

	private static float NormalizeAngle(float angle)
	{
		return Repeat(Sanitize(angle), 360f);
	}

	private static float NormalizeHours(float hours)
	{
		return Repeat(Sanitize(hours), 24f);
	}

	private static float Sanitize(float value)
	{
		return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
	}

	private static float Repeat(float value, float length)
	{
		float result = value % length;
		return result < 0f ? result + length : result;
	}
}
