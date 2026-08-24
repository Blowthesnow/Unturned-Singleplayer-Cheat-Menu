using System;
using Newtonsoft.Json;

namespace UnturnedSingleplayerCheatMenu.Models;

[Serializable]
[JsonObject(MemberSerialization.Fields)]
internal sealed class TeleportPointCollection
{
	public TeleportPoint[] Points = Array.Empty<TeleportPoint>();
}
