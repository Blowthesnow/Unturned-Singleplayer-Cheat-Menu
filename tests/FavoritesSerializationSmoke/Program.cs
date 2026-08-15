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

FavoriteAssetCollection source = new()
{
    ItemKeys =
    [
        "guid:11111111-1111-1111-1111-111111111111",
        "legacy:SDG.Unturned.ItemAsset:42:Core:Test Item"
    ],
    VehicleKeys =
    [
        "guid:22222222-2222-2222-2222-222222222222"
    ]
};

string json = JsonConvert.SerializeObject(source, Formatting.Indented);
if (json == "{}"
    || json.IndexOf("\"ItemKeys\"", StringComparison.Ordinal) < 0
    || json.IndexOf("\"VehicleKeys\"", StringComparison.Ordinal) < 0)
{
    throw new InvalidOperationException($"Favorite fields were not serialized: {json}");
}

FavoriteAssetCollection roundTrip = JsonConvert.DeserializeObject<FavoriteAssetCollection>(json);
if (roundTrip?.ItemKeys?.Length != source.ItemKeys.Length
    || roundTrip.VehicleKeys?.Length != source.VehicleKeys.Length)
{
    throw new InvalidOperationException("Favorite round-trip count mismatch.");
}

if (!roundTrip.ItemKeys.SequenceEqual(source.ItemKeys, StringComparer.Ordinal)
    || !roundTrip.VehicleKeys.SequenceEqual(source.VehicleKeys, StringComparer.Ordinal))
{
    throw new InvalidOperationException("Favorite round-trip field mismatch.");
}

string emptyJson = JsonConvert.SerializeObject(new FavoriteAssetCollection(), Formatting.Indented);
if (emptyJson.IndexOf("\"ItemKeys\"", StringComparison.Ordinal) < 0
    || emptyJson.IndexOf("\"VehicleKeys\"", StringComparison.Ordinal) < 0)
{
    throw new InvalidOperationException($"Empty favorite collection was not explicit: {emptyJson}");
}

Console.WriteLine("Favorites JSON round-trip: PASS");
Console.WriteLine(json);
Console.WriteLine($"Empty collection JSON: {emptyJson}");
