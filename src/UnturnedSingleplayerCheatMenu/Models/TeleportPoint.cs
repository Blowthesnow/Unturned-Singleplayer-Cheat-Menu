using System;
using Newtonsoft.Json;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Models;

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

    public Vector3 Position => new(X, Y, Z);
}

[Serializable]
[JsonObject(MemberSerialization.Fields)]
internal sealed class TeleportPointCollection
{
    public TeleportPoint[] Points = Array.Empty<TeleportPoint>();
}
