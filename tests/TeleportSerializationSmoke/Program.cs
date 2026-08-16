using Newtonsoft.Json;
using UnturnedSingleplayerCheatMenu.Models;

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Exception exception = eventArgs.ExceptionObject as Exception;
    for (int depth = 0; exception != null && depth < 8; depth++)
    {
        Console.Error.WriteLine($"{exception.GetType().FullName}: {exception.Message}");
        exception = exception.InnerException;
    }
};

TeleportPoint point = new()
{
    Id = "roundtrip-id",
    Name = "序列化测试",
    Map = "PEI",
    X = 1.25f,
    Y = 2.5f,
    Z = 3.75f,
    Yaw = 90f,
    CreatedUtcTicks = 123456L,
    MarkerKind = TeleportMarkerKind.Circle,
    MarkerColorHex = "#2E86DE"
};

TeleportPointCollection source = new() { Points = new[] { point } };
string json = JsonConvert.SerializeObject(source, Formatting.Indented);
if (json == "{}" || json.IndexOf("\"Points\"", StringComparison.Ordinal) < 0)
    throw new InvalidOperationException($"Points field was not serialized: {json}");
if (json.IndexOf("\"Position\"", StringComparison.Ordinal) >= 0)
    throw new InvalidOperationException($"Computed Position property leaked into storage: {json}");

TeleportPointCollection roundTrip = JsonConvert.DeserializeObject<TeleportPointCollection>(json);
if (roundTrip?.Points?.Length != 1)
    throw new InvalidOperationException("Round-trip point count mismatch.");

TeleportPoint result = roundTrip.Points[0];
if (result.Id != point.Id || result.Name != point.Name || result.Map != point.Map
    || result.X != point.X || result.Y != point.Y || result.Z != point.Z
    || result.Yaw != point.Yaw || result.CreatedUtcTicks != point.CreatedUtcTicks
    || result.MarkerKind != point.MarkerKind
    || result.MarkerColorHex != point.MarkerColorHex)
{
    throw new InvalidOperationException("Round-trip field mismatch.");
}

TeleportPoint legacyPoint = JsonConvert.DeserializeObject<TeleportPoint>(
    "{\"Id\":\"legacy-id\",\"Name\":\"Legacy\",\"Map\":\"PEI\",\"X\":1,\"Y\":2,\"Z\":3}");
if (legacyPoint == null || legacyPoint.MarkerKind != TeleportMarkerKind.Star)
    throw new InvalidOperationException("Legacy points should default to the star marker.");
if (legacyPoint.MarkerColorHex != "#F5C542")
    throw new InvalidOperationException("Legacy points should default to the yellow marker color.");

string emptyJson = JsonConvert.SerializeObject(new TeleportPointCollection(), Formatting.Indented);
if (emptyJson.IndexOf("\"Points\"", StringComparison.Ordinal) < 0)
    throw new InvalidOperationException($"Empty collection was not explicit: {emptyJson}");

Console.WriteLine("Teleport JSON round-trip: PASS");
Console.WriteLine(json);
Console.WriteLine($"Empty collection JSON: {emptyJson}");
