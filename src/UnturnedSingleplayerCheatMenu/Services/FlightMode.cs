using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal readonly struct FlightDirectionVector
{
	internal FlightDirectionVector(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	internal float X { get; }
	internal float Y { get; }
	internal float Z { get; }
	internal float LengthSquared => X * X + Y * Y + Z * Z;
	internal bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

internal static class FlightDirectionPolicy
{
	private const float DirectionEpsilonSquared = 0.000001f;

	internal static FlightDirectionVector ResolveVelocity(
		FlightDirectionVector viewForward,
		FlightDirectionVector fallbackForward,
		float forwardInput,
		float strafeInput,
		float verticalInput,
		float horizontalSpeed,
		float verticalSpeed)
	{
		FlightDirectionVector forward = NormalizeOrFallback(viewForward, fallbackForward);
		FlightDirectionVector horizontalForward = NormalizeOrFallback(
			new FlightDirectionVector(forward.X, 0f, forward.Z),
			new FlightDirectionVector(fallbackForward.X, 0f, fallbackForward.Z));
		FlightDirectionVector right = new FlightDirectionVector(
			horizontalForward.Z, 0f, -horizontalForward.X);
		forwardInput = NormalizeInput(forwardInput);
		strafeInput = NormalizeInput(strafeInput);
		verticalInput = NormalizeInput(verticalInput);
		FlightDirectionVector directionalForward = verticalInput == 0f ? forward : horizontalForward;
		FlightDirectionVector planar = new FlightDirectionVector(
			directionalForward.X * forwardInput + right.X * strafeInput,
			directionalForward.Y * forwardInput + right.Y * strafeInput,
			directionalForward.Z * forwardInput + right.Z * strafeInput);
		if (planar.LengthSquared > 1f)
			planar = NormalizeOrFallback(planar, default);

		horizontalSpeed = NormalizeSpeed(horizontalSpeed);
		verticalSpeed = NormalizeSpeed(verticalSpeed);
		FlightDirectionVector result = new FlightDirectionVector(
			planar.X * horizontalSpeed,
			planar.Y * horizontalSpeed + verticalInput * verticalSpeed,
			planar.Z * horizontalSpeed);
		return result.IsFinite ? result : default;
	}

	private static FlightDirectionVector NormalizeOrFallback(
		FlightDirectionVector value,
		FlightDirectionVector fallback)
	{
		if (!value.IsFinite || value.LengthSquared < DirectionEpsilonSquared)
			value = fallback;
		if (!value.IsFinite || value.LengthSquared < DirectionEpsilonSquared)
			value = new FlightDirectionVector(0f, 0f, 1f);
		float inverseLength = 1f / MathF.Sqrt(value.LengthSquared);
		return new FlightDirectionVector(
			value.X * inverseLength,
			value.Y * inverseLength,
			value.Z * inverseLength);
	}

	private static float NormalizeInput(float value)
	{
		if (!float.IsFinite(value)) return 0f;
		return Math.Clamp(value, -1f, 1f);
	}

	private static float NormalizeSpeed(float value)
	{
		return float.IsFinite(value) && value > 0f ? value : 0f;
	}
}

internal enum FlightMode
{
	WorldAxis,
	ViewDirection
}

internal static class FlightModeExtensions
{
	internal static string ToDisplayName(this FlightMode mode)
	{
		return mode switch
		{
			FlightMode.ViewDirection => "视角指向",
			_ => "世界轴"
		};
	}
}
