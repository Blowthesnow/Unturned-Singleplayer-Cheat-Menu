using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class NearbyWorldScanner
{
	private readonly List<Zombie> _zombies = new List<Zombie>();
	private readonly List<Animal> _animals = new List<Animal>();
	private readonly List<MinimapWorldMarker> _markers = new List<MinimapWorldMarker>();
	private int _used;

	public IReadOnlyList<MinimapWorldMarker> Scan(Vector3 center, float radius,
		bool includeZombies, bool includeAnimals, bool includePlayers)
	{
		_used = 0;
		float squaredRadius = radius * radius;
		if (includeZombies)
		{
			_zombies.Clear();
			ZombieManager.getZombiesInRadius(center, squaredRadius, _zombies);
			for (int index = 0; index < _zombies.Count; index++)
			{
				Zombie zombie = _zombies[index];
				if (zombie != null && !zombie.isDead)
				{
					SetMarker(Acquire(), zombie.GetInstanceID(), MinimapWorldMarkerKind.Zombie,
						zombie.transform.position, Color.red, string.Empty, zombie.transform);
				}
			}
		}

		if (includeAnimals)
		{
			_animals.Clear();
			AnimalManager.getAnimalsInRadius(center, squaredRadius, _animals);
			for (int index = 0; index < _animals.Count; index++)
			{
				Animal animal = _animals[index];
				if (animal != null && !animal.isDead)
				{
					SetMarker(Acquire(), animal.GetInstanceID(), MinimapWorldMarkerKind.Animal,
						animal.transform.position, Color.green, string.Empty, animal.transform);
				}
			}
		}

		if (includePlayers && Provider.clients != null)
		{
			Player localPlayer = Player.LocalPlayer;
			for (int index = 0; index < Provider.clients.Count; index++)
			{
				SteamPlayer steamPlayer = Provider.clients[index];
				Player player = steamPlayer?.player;
				if (player == null || player == localPlayer || player.life == null || player.life.isDead)
				{
					continue;
				}
				Vector3 delta = player.transform.position - center;
				if (delta.x * delta.x + delta.z * delta.z > squaredRadius)
				{
					continue;
				}
				SetMarker(Acquire(), player.GetInstanceID(), MinimapWorldMarkerKind.Player,
					player.transform.position, Color.yellow, steamPlayer.playerID?.characterName ?? string.Empty,
					player.transform);
			}
		}

		if (_used < _markers.Count)
		{
			_markers.RemoveRange(_used, _markers.Count - _used);
		}
		return _markers;
	}

	private MinimapWorldMarker Acquire()
	{
		if (_used == _markers.Count)
		{
			_markers.Add(new MinimapWorldMarker());
		}
		return _markers[_used++];
	}

	private static void SetMarker(MinimapWorldMarker marker, int stableId, MinimapWorldMarkerKind kind,
		Vector3 position, Color color, string label, Transform followTarget)
	{
		marker.StableId = stableId;
		marker.Kind = kind;
		marker.Position = position;
		marker.Color = color;
		marker.Label = label ?? string.Empty;
		marker.Rotation = 0f;
		marker.FollowTarget = followTarget;
	}
}
