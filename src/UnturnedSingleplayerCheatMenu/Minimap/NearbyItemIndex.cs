using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class NearbyGroundItem
{
	public uint InstanceId { get; set; }
	public byte RegionX { get; set; }
	public byte RegionY { get; set; }
	public Vector3 Position { get; set; }
	public float HorizontalDistanceSquared { get; set; }
	public float DistanceSquared { get; set; }
	public byte Amount { get; set; }
	public byte Quality { get; set; }
	public byte[] State { get; set; }
	public ItemDrop Drop { get; set; }
	public ItemAsset Asset { get; set; }

	public GroundItemSnapshot ToMinimapSnapshot()
	{
		return new GroundItemSnapshot(InstanceId,
			new MinimapWorldPoint(Position.x, Position.y, Position.z),
			(MinimapItemRarity)(int)Asset.rarity,
			Asset.id, Amount, Quality);
	}
}

internal sealed class NearbyItemSnapshot
{
	public static readonly NearbyItemSnapshot Empty = new NearbyItemSnapshot(0, Vector3.zero, 0f, Array.Empty<NearbyGroundItem>());

	public int Version { get; }
	public Vector3 Center { get; }
	public float Radius { get; }
	public IReadOnlyList<NearbyGroundItem> Items { get; }

	public NearbyItemSnapshot(int version, Vector3 center, float radius, IReadOnlyList<NearbyGroundItem> items)
	{
		Version = version;
		Center = center;
		Radius = radius;
		Items = items ?? Array.Empty<NearbyGroundItem>();
	}
}

internal sealed class NearbyItemIndex
{
	private const float MaxSupportedRadius = 1000f;
	private readonly List<RegionCoordinate> _regions = new List<RegionCoordinate>();
	private readonly HashSet<uint> _instances = new HashSet<uint>();
	private readonly List<NearbyGroundItem> _items = new List<NearbyGroundItem>();

	public NearbyItemSnapshot Refresh(Vector3 center, float radius, int snapshotVersion)
	{
		_regions.Clear();
		_instances.Clear();
		_items.Clear();
		if (ItemManager.regions == null)
		{
			return new NearbyItemSnapshot(snapshotVersion, center, 0f, _items);
		}

		radius = SanitizeRadius(radius);
		float radiusSquared = radius * radius;
		Regions.getRegionsInRadius(center, radius, _regions);
		for (int regionIndex = 0; regionIndex < _regions.Count; regionIndex++)
		{
			RegionCoordinate coordinate = _regions[regionIndex];
			ItemRegion region = ItemManager.regions[coordinate.x, coordinate.y];
			if (region == null)
			{
				continue;
			}
			List<ItemDrop> drops = region.drops;
			for (int dropIndex = 0; dropIndex < drops.Count; dropIndex++)
			{
				ItemDrop drop = drops[dropIndex];
				InteractableItem interactable = drop?.interactableItem;
				ItemAsset asset = interactable?.asset;
				Item item = interactable?.item;
				Transform model = drop?.model;
				if (drop == null || model == null || interactable == null || asset == null || item == null || !_instances.Add(drop.instanceID))
				{
					continue;
				}
				Vector3 delta = model.position - center;
				float horizontalDistanceSquared = delta.x * delta.x + delta.z * delta.z;
				if (horizontalDistanceSquared > radiusSquared)
				{
					continue;
				}
				_items.Add(new NearbyGroundItem
				{
					InstanceId = drop.instanceID,
					RegionX = coordinate.x,
					RegionY = coordinate.y,
					Position = model.position,
					HorizontalDistanceSquared = horizontalDistanceSquared,
					DistanceSquared = delta.sqrMagnitude,
					Amount = item.amount,
					Quality = item.quality,
					State = item.state == null ? Array.Empty<byte>() : (byte[])item.state.Clone(),
					Drop = drop,
					Asset = asset
				});
			}
		}
		return new NearbyItemSnapshot(snapshotVersion, center, radius, _items);
	}

	private static float SanitizeRadius(float radius)
	{
		if (float.IsNaN(radius) || float.IsInfinity(radius)) return 0f;
		return Mathf.Clamp(radius, 0f, MaxSupportedRadius);
	}
}
