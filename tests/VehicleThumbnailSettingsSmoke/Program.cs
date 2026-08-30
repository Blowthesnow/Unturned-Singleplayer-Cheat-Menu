using UnturnedSingleplayerCheatMenu.Services;

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
}

VehicleThumbnailRenderSettings defaults =
    VehicleThumbnailRenderSettings.Normalize(128, 1.0f);
Equal(128, defaults.Width, "Default width");
Equal(96, defaults.Height, "Default height");
Equal(1.0f, defaults.Framing, "Default framing");
Equal(1000, defaults.GetFramingMilli(), "Stable milli framing cache key");
Equal(2, defaults.CacheFormatVersion, "Generated-camera cache format version");

string rendererSource;
using (Stream stream = typeof(VehicleThumbnailRenderSettings).Assembly.GetManifestResourceStream("VehicleIconRenderer.cs")
    ?? throw new InvalidOperationException("Embedded VehicleIconRenderer source was not found."))
using (StreamReader reader = new StreamReader(stream))
{
    rendererSource = reader.ReadToEnd();
}

if (rendererSource.Contains("transform.Find(\"Icon2\")", StringComparison.Ordinal) ||
    rendererSource.Contains("transform.Find(\"Icon\")", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Vehicle thumbnails must not use asset-authored Icon2/Icon camera transforms.");
}

if (!rendererSource.Contains("CreateGeneratedIconTransform(transform, componentsInChildren, settings", StringComparison.Ordinal) ||
    !rendererSource.Contains("ForceHighestLod(transform);", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Vehicle thumbnails must always use the generated camera and highest LOD.");
}

VehicleThumbnailRenderSettings balanced =
    VehicleThumbnailRenderSettings.Normalize(192, 0.5f);
Equal(192, balanced.Width, "Balanced width");
Equal(144, balanced.Height, "Balanced height");
Equal(0.5f, balanced.Framing, "Minimum framing");

VehicleThumbnailRenderSettings sharp =
    VehicleThumbnailRenderSettings.Normalize(256, 1.5f);
Equal(256, sharp.Width, "Sharp width");
Equal(192, sharp.Height, "Sharp height");
Equal(1.5f, sharp.Framing, "Maximum framing");

VehicleThumbnailRenderSettings invalid =
    VehicleThumbnailRenderSettings.Normalize(160, float.NaN);
Equal(128, invalid.Width, "Invalid width falls back to default");
Equal(96, invalid.Height, "Invalid width keeps the 4:3 default height");
Equal(1.0f, invalid.Framing, "Invalid framing falls back to default");

VehicleThumbnailRenderSettings clamped =
    VehicleThumbnailRenderSettings.Normalize(256, 2f);
Equal(1.5f, clamped.Framing, "High framing clamps to the safe maximum");

VehicleThumbnailRenderSettings lowClamped =
    VehicleThumbnailRenderSettings.Normalize(128, 0f);
Equal(0.5f, lowClamped.Framing, "Low framing clamps to the safe minimum");

Console.WriteLine("Vehicle thumbnail settings smoke checks passed.");
