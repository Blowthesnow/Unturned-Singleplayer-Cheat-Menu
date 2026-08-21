using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class MovementSpeedProfile
{
    // Unturned 3.26.3.8 PlayerMovement.SPEED_SPRINT is 7 m/s. Treat the
    // displayed value as a real multiplier over that familiar baseline rather
    // than incorrectly interpreting "3x" as only 3 m/s.
    internal const float BaseMetersPerSecond = 7f;
    internal const float MinimumMultiplier = 1f;
    internal const float MaximumMultiplier = 10f;

    internal static float NormalizeMultiplier(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return MinimumMultiplier;
        return Math.Max(MinimumMultiplier, Math.Min(MaximumMultiplier, value));
    }

    internal static float GetMetersPerSecond(float multiplier)
    {
        return BaseMetersPerSecond * NormalizeMultiplier(multiplier);
    }
}
