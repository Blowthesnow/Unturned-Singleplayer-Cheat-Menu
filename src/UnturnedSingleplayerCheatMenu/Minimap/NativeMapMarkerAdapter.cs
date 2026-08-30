using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class NativeMapMarkerAdapter
{
	private readonly List<MinimapWorldMarker> _markers = new List<MinimapWorldMarker>();

	public IReadOnlyList<MinimapWorldMarker> Capture(Vector3 center, float radius)
	{
		_markers.Clear();
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null || Provider.clients == null)
		{
			return _markers;
		}

		float squaredRadius = radius * radius;
		for (int index = 0; index < Provider.clients.Count; index++)
		{
			SteamPlayer steamPlayer = Provider.clients[index];
			Player player = steamPlayer?.player;
			PlayerQuests quests = player?.quests;
			if (player == null || quests == null)
			{
				continue;
			}
			if (!MinimapMarkerPolicies.ShouldShowNativeMarker(player == localPlayer,
				quests.isMemberOfSameGroupAs(localPlayer), quests.isMarkerPlaced))
			{
				continue;
			}
			Vector3 delta = quests.markerPosition - center;
			if (delta.x * delta.x + delta.z * delta.z > squaredRadius)
			{
				continue;
			}
			string label = quests.markerTextOverride;
			if (string.IsNullOrEmpty(label))
			{
				label = string.IsNullOrEmpty(steamPlayer.playerID.nickName)
					? steamPlayer.playerID.characterName
					: steamPlayer.playerID.nickName;
			}
			_markers.Add(new MinimapWorldMarker
			{
				StableId = player.GetInstanceID(),
				Kind = MinimapWorldMarkerKind.NativeMapMarker,
				Position = quests.markerPosition,
				Color = steamPlayer.markerColor,
				Label = label ?? string.Empty
			});
		}
		return _markers;
	}
}
