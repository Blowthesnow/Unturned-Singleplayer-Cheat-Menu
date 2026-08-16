using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Models;

internal enum TeleportMarkerKind
{
    Star,
    Square,
    Circle,
    Diamond
}

[Serializable]
[JsonObject(MemberSerialization.Fields)]
internal sealed class TeleportPoint
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Name = string.Empty;
    public string Map = string.Empty;
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
    public long CreatedUtcTicks;
    public TeleportMarkerKind MarkerKind = TeleportMarkerKind.Star;
    public string MarkerColorHex = "#F5C542";

    public Vector3 Position => new(X, Y, Z);

    [OnDeserialized]
    private void NormalizeLegacyFields(StreamingContext context)
    {
        if (!Enum.IsDefined(typeof(TeleportMarkerKind), MarkerKind))
            MarkerKind = TeleportMarkerKind.Star;
        if (!IsValidMarkerColor(MarkerColorHex))
            MarkerColorHex = "#F5C542";
    }

    private static bool IsValidMarkerColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string candidate = value.Trim();
        if (!candidate.StartsWith("#", StringComparison.Ordinal))
            candidate = "#" + candidate;
        if (candidate.Length != 7)
            return false;

        for (int index = 1; index < candidate.Length; index++)
        {
            char character = candidate[index];
            bool hex = character >= '0' && character <= '9'
                || character >= 'A' && character <= 'F'
                || character >= 'a' && character <= 'f';
            if (!hex)
                return false;
        }

        return true;
    }
}

[Serializable]
[JsonObject(MemberSerialization.Fields)]
internal sealed class TeleportPointCollection
{
    public TeleportPoint[] Points = Array.Empty<TeleportPoint>();
}
