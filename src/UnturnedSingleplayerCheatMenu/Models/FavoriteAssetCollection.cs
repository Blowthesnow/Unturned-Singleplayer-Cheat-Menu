using System;
using Newtonsoft.Json;

namespace UnturnedSingleplayerCheatMenu.Models;

[Serializable]
[JsonObject(MemberSerialization.Fields)]
internal sealed class FavoriteAssetCollection
{
    public string[] ItemKeys = Array.Empty<string>();
    public string[] VehicleKeys = Array.Empty<string>();
}
