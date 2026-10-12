using System;
using System.IO;
using UnturnedSingleplayerCheatMenu.Services;

static void NearlyEqual(float expected, float actual, string message)
{
    if (MathF.Abs(expected - actual) > 0.001f)
        throw new InvalidOperationException($"{message}: expected {expected:0.###}, actual {actual:0.###}.");
}

static void EqualVector(FlightDirectionVector expected, FlightDirectionVector actual, string message)
{
    NearlyEqual(expected.X, actual.X, message + " X");
    NearlyEqual(expected.Y, actual.Y, message + " Y");
    NearlyEqual(expected.Z, actual.Z, message + " Z");
}

if (FlightMode.WorldAxis.ToDisplayName() != "世界轴")
    throw new InvalidOperationException("WorldAxis display name mismatch.");
if (FlightMode.ViewDirection.ToDisplayName() != "视角指向")
    throw new InvalidOperationException("ViewDirection display name mismatch.");

if (!FlightViewmodelPolicy.ShouldTreatAsGrounded(false, true, true))
    throw new InvalidOperationException("Local flight must suppress the native airborne viewmodel tilt.");
if (FlightViewmodelPolicy.ShouldTreatAsGrounded(false, true, false))
    throw new InvalidOperationException("Normal local jumping must preserve the native airborne viewmodel tilt.");
if (FlightViewmodelPolicy.ShouldTreatAsGrounded(false, false, true))
    throw new InvalidOperationException("Remote airborne players must preserve the native airborne viewmodel tilt.");
if (!FlightViewmodelPolicy.ShouldTreatAsGrounded(true, true, false))
    throw new InvalidOperationException("Native grounded state must remain grounded without flight.");

string patchSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src",
    "UnturnedSingleplayerCheatMenu", "Patches", "FlightViewmodelPatch.cs"));
if (!patchSource.Contains("instance?.CanExecuteGameplay == true", StringComparison.Ordinal))
    throw new InvalidOperationException("Flight viewmodel patch must fail closed immediately when gameplay authorization is lost.");

const float horizontalSpeed = 21f;
const float verticalSpeed = 35f;
FlightDirectionVector worldForward = new(0f, 0f, 1f);
FlightDirectionVector pitchForward = new(0f, 0.6f, 0.8f);
FlightDirectionVector yawRightForward = new(1f, 0f, 0f);

EqualVector(new FlightDirectionVector(0f, 0f, 21f),
    FlightDirectionPolicy.ResolveVelocity(worldForward, worldForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Forward input must move along level view direction");
EqualVector(new FlightDirectionVector(0f, 12.6f, 16.8f),
    FlightDirectionPolicy.ResolveVelocity(pitchForward, worldForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Forward input must preserve camera pitch");
EqualVector(new FlightDirectionVector(0f, -12.6f, -16.8f),
    FlightDirectionPolicy.ResolveVelocity(pitchForward, worldForward, -1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Backward input must reverse pitched view direction");
EqualVector(new FlightDirectionVector(21f, 0f, 0f),
    FlightDirectionPolicy.ResolveVelocity(yawRightForward, worldForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Forward input must preserve camera yaw");
EqualVector(new FlightDirectionVector(0f, 0f, -21f),
    FlightDirectionPolicy.ResolveVelocity(yawRightForward, worldForward, 0f, 1f, 0f, horizontalSpeed, verticalSpeed),
    "Right strafe must remain perpendicular to yaw");

FlightDirectionVector diagonal = FlightDirectionPolicy.ResolveVelocity(
    worldForward, worldForward, 1f, 1f, 0f, horizontalSpeed, verticalSpeed);
NearlyEqual(horizontalSpeed, MathF.Sqrt(diagonal.LengthSquared),
    "Diagonal input must not exceed configured horizontal speed");
EqualVector(new FlightDirectionVector(0f, verticalSpeed, 0f),
    FlightDirectionPolicy.ResolveVelocity(worldForward, worldForward, 0f, 0f, 1f, horizontalSpeed, verticalSpeed),
    "Pure ascend input must not add horizontal drift");
EqualVector(new FlightDirectionVector(0f, -verticalSpeed, 0f),
    FlightDirectionPolicy.ResolveVelocity(worldForward, worldForward, 0f, 0f, -1f, horizontalSpeed, verticalSpeed),
    "Pure descend input must not add horizontal drift");
EqualVector(new FlightDirectionVector(0f, verticalSpeed, horizontalSpeed),
    FlightDirectionPolicy.ResolveVelocity(pitchForward, worldForward, 1f, 0f, 1f, horizontalSpeed, verticalSpeed),
    "Explicit vertical input must take priority over camera pitch while preserving forward travel");
EqualVector(new FlightDirectionVector(0f, -verticalSpeed, horizontalSpeed),
    FlightDirectionPolicy.ResolveVelocity(pitchForward, worldForward, 1f, 0f, -1f, horizontalSpeed, verticalSpeed),
    "Explicit descend input must take priority over camera pitch while preserving forward travel");
EqualVector(new FlightDirectionVector(21f, 0f, 0f),
    FlightDirectionPolicy.ResolveVelocity(default, yawRightForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Zero camera direction must fall back to player forward");
EqualVector(new FlightDirectionVector(0f, 0f, 21f),
    FlightDirectionPolicy.ResolveVelocity(new FlightDirectionVector(float.NaN, 0f, 1f), worldForward,
        1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "Non-finite camera direction must fall back without producing NaN");

EqualVector(new FlightDirectionVector(0f, 12.6f, 16.8f),
    FlightDirectionPolicy.ResolveVelocityForMode(FlightMode.ViewDirection,
        pitchForward, worldForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "View-direction mode must preserve camera pitch in the shared flight path");
EqualVector(new FlightDirectionVector(0f, 0f, 21f),
    FlightDirectionPolicy.ResolveVelocityForMode(FlightMode.WorldAxis,
        pitchForward, worldForward, 1f, 0f, 0f, horizontalSpeed, verticalSpeed),
    "World-axis mode must keep forward travel level in the shared flight path");

string movementSource = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "src",
    "UnturnedSingleplayerCheatMenu", "Services", "MovementCheatService.cs"));
if (!movementSource.Contains("FlightDirectionPolicy.ResolveVelocity", StringComparison.Ordinal) ||
    !movementSource.Contains("InputEx.GetKey(ControlsSettings.up)", StringComparison.Ordinal) ||
    !movementSource.Contains("InputEx.GetKey(ControlsSettings.down)", StringComparison.Ordinal) ||
    !movementSource.Contains("InputEx.GetKey(ControlsSettings.left)", StringComparison.Ordinal) ||
    !movementSource.Contains("InputEx.GetKey(ControlsSettings.right)", StringComparison.Ordinal) ||
    !movementSource.Contains("Input.GetKey(KeyCode.Space)", StringComparison.Ordinal) ||
    !movementSource.Contains("Input.GetKey(KeyCode.LeftControl)", StringComparison.Ordinal) ||
    !movementSource.Contains("Input.GetKey(KeyCode.RightControl)", StringComparison.Ordinal) ||
    movementSource.Contains("InputEx.GetKey(ControlsSettings.ascend)", StringComparison.Ordinal) ||
    movementSource.Contains("InputEx.GetKey(ControlsSettings.descend)", StringComparison.Ordinal) ||
    movementSource.Contains("input_x", StringComparison.Ordinal) ||
    movementSource.Contains("input_y", StringComparison.Ordinal) ||
    movementSource.Contains("inputJump", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Flight must use configured directional keys with fixed Space/Ctrl vertical priority, independent of native simulation input fields.");
}

int sharedVelocityCallCount = movementSource.Split(
    "GetCustomFlightVelocity(localPlayer)", StringSplitOptions.None).Length - 1;
if (sharedVelocityCallCount != 2 ||
    movementSource.Contains("private static Vector3 GetHorizontalInput", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Colliding and noclip flight must share one mode-aware velocity path instead of bypassing ViewDirection during noclip.");
}

Console.WriteLine("Flight mode smoke checks passed.");
