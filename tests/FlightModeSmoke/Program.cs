using UnturnedSingleplayerCheatMenu.Services;

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

Console.WriteLine("Flight mode smoke checks passed.");
