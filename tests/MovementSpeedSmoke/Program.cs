using UnturnedSingleplayerCheatMenu.Services;

static void NearlyEqual(float expected, float actual, string message)
{
    if (MathF.Abs(expected - actual) > 0.001f)
        throw new InvalidOperationException(
            $"{message}: expected {expected:0.###}, actual {actual:0.###}.");
}

NearlyEqual(7f, MovementSpeedProfile.GetMetersPerSecond(1f), "1x sprint baseline");
NearlyEqual(21f, MovementSpeedProfile.GetMetersPerSecond(3f), "3x default horizontal speed");
NearlyEqual(28f, MovementSpeedProfile.GetMetersPerSecond(4f), "4x configured horizontal speed");
NearlyEqual(70f, MovementSpeedProfile.GetMetersPerSecond(10f), "10x maximum speed");
NearlyEqual(7f, MovementSpeedProfile.GetMetersPerSecond(0f), "Low multiplier clamp");
NearlyEqual(70f, MovementSpeedProfile.GetMetersPerSecond(11f), "High multiplier clamp");
NearlyEqual(7f, MovementSpeedProfile.GetMetersPerSecond(float.NaN), "Invalid multiplier fallback");

Console.WriteLine("Movement speed smoke checks passed.");
