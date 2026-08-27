using UnturnedSingleplayerCheatMenu.Services;

if (FlightMode.WorldAxis.ToDisplayName() != "世界轴")
    throw new InvalidOperationException("WorldAxis display name mismatch.");
if (FlightMode.ViewDirection.ToDisplayName() != "视角指向")
    throw new InvalidOperationException("ViewDirection display name mismatch.");

Console.WriteLine("Flight mode smoke checks passed.");
