using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class VehicleThumbnailRenderSettings
{
    internal const int CurrentCacheFormatVersion = 1;
    internal const int DefaultWidth = 128;
    internal const float DefaultFraming = 1.0f;
    internal const float MinimumFraming = 0.5f;
    internal const float MaximumFraming = 1.5f;

    internal VehicleThumbnailRenderSettings(int width, float framing)
    {
        Width = NormalizeWidth(width);
        Height = Width * 3 / 4;
        Framing = NormalizeFraming(framing);
        CacheFormatVersion = CurrentCacheFormatVersion;
    }

    internal int Width { get; }
    internal int Height { get; }
    internal float Framing { get; }
    internal int CacheFormatVersion { get; }

    internal static VehicleThumbnailRenderSettings Normalize(int width, float framing)
    {
        return new VehicleThumbnailRenderSettings(width, framing);
    }

    internal static int NormalizeWidth(int width)
    {
        return width switch
        {
            192 => 192,
            256 => 256,
            _ => DefaultWidth
        };
    }

    internal static float NormalizeFraming(float framing)
    {
        if (float.IsNaN(framing) || float.IsInfinity(framing))
            return DefaultFraming;

        return Math.Clamp(framing, MinimumFraming, MaximumFraming);
    }

    internal int GetFramingMilli()
    {
        return (int)Math.Round(Framing * 1000f, MidpointRounding.AwayFromZero);
    }
}
