using System;
using System.Runtime.Serialization;
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

	public TeleportMarkerKind MarkerKind;

	public string MarkerColorHex = "#F5C542";

	public Vector3 Position => new Vector3(X, Y, Z);

	[OnDeserialized]
	private void NormalizeLegacyFields(StreamingContext context)
	{
		if (!Enum.IsDefined(typeof(TeleportMarkerKind), MarkerKind))
		{
			MarkerKind = TeleportMarkerKind.Star;
		}
		if (!IsValidMarkerColor(MarkerColorHex))
		{
			MarkerColorHex = "#F5C542";
		}
	}

	private static bool IsValidMarkerColor(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		string text = value.Trim();
		if (!text.StartsWith("#", StringComparison.Ordinal))
		{
			text = "#" + text;
		}
		if (text.Length != 7)
		{
			return false;
		}
		for (int i = 1; i < text.Length; i++)
		{
			char c = text[i];
			if ((c < '0' || c > '9') && (c < 'A' || c > 'F') && (c < 'a' || c > 'f'))
			{
				return false;
			}
		}
		return true;
	}
}
