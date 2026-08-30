using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal enum MinimapItemRarity
{
	Common = 0,
	Uncommon = 1,
	Rare = 2,
	Epic = 3,
	Legendary = 4,
	Mythical = 5
}

internal readonly struct GroundItemSnapshot
{
	public uint InstanceId { get; }
	public MinimapWorldPoint Position { get; }
	public MinimapItemRarity Rarity { get; }
	public ushort AssetId { get; }
	public byte Amount { get; }
	public byte Quality { get; }

	public GroundItemSnapshot(uint instanceId, MinimapWorldPoint position, MinimapItemRarity rarity,
		ushort assetId = 0, byte amount = 0, byte quality = 0)
	{
		InstanceId = instanceId;
		Position = position;
		Rarity = rarity;
		AssetId = assetId;
		Amount = amount;
		Quality = quality;
	}
}

internal readonly struct MinimapItemMarker
{
	public uint InstanceId { get; }
	public MinimapItemRarity Rarity { get; }
	public float X { get; }
	public float Y { get; }
	public float HeightDelta { get; }
	public float HorizontalDistanceSquared { get; }
	public MinimapWorldPoint Position { get; }

	public MinimapItemMarker(uint instanceId, MinimapItemRarity rarity, float x, float y,
		float heightDelta, float horizontalDistanceSquared, MinimapWorldPoint position)
	{
		InstanceId = instanceId;
		Rarity = rarity;
		X = x;
		Y = y;
		HeightDelta = heightDelta;
		HorizontalDistanceSquared = horizontalDistanceSquared;
		Position = position;
	}
}

internal static class MinimapItemAggregator
{
	private readonly struct Candidate
	{
		public GroundItemSnapshot Item { get; }
		public MinimapProjectedPoint Projection { get; }

		public Candidate(GroundItemSnapshot item, MinimapProjectedPoint projection)
		{
			Item = item;
			Projection = projection;
		}
	}

	private sealed class CandidateComparer : IComparer<Candidate>
	{
		public static readonly CandidateComparer Instance = new CandidateComparer();

		public int Compare(Candidate left, Candidate right)
		{
			int rarity = ((int)right.Item.Rarity).CompareTo((int)left.Item.Rarity);
			if (rarity != 0) return rarity;
			int distance = left.Projection.HorizontalDistanceSquared.CompareTo(right.Projection.HorizontalDistanceSquared);
			if (distance != 0) return distance;
			return left.Item.InstanceId.CompareTo(right.Item.InstanceId);
		}
	}

	public static IReadOnlyList<MinimapItemMarker> Compose(IReadOnlyList<GroundItemSnapshot> items,
		MinimapProjectionContext projection, ItemRarityMask enabledRarities, float mergePixels, bool showOtherFloors)
	{
		if (items == null || items.Count == 0 || enabledRarities == ItemRarityMask.None)
		{
			return Array.Empty<MinimapItemMarker>();
		}

		Candidate[] candidates = new Candidate[items.Count];
		int candidateCount = 0;
		for (int index = 0; index < items.Count; index++)
		{
			GroundItemSnapshot item = items[index];
			int rarityIndex = (int)item.Rarity;
			if (rarityIndex < 0 || rarityIndex > (int)MinimapItemRarity.Mythical ||
				(enabledRarities & (ItemRarityMask)(1 << rarityIndex)) == 0)
			{
				continue;
			}
			MinimapProjectedPoint projected = MinimapProjection.Project(item.Position, projection);
			if (!projected.IsVisible || (!showOtherFloors && !projected.IsSameFloor))
			{
				continue;
			}
			candidates[candidateCount++] = new Candidate(item, projected);
		}

		if (candidateCount == 0)
		{
			return Array.Empty<MinimapItemMarker>();
		}

		Array.Sort(candidates, 0, candidateCount, CandidateComparer.Instance);
		float merge = Math.Max(0.5f, mergePixels);
		float mergeSquared = merge * merge;
		List<MinimapItemMarker> markers = new List<MinimapItemMarker>(candidateCount);
		Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
		for (int index = 0; index < candidateCount; index++)
		{
			Candidate candidate = candidates[index];
			int cellX = (int)Math.Floor(candidate.Projection.X / merge);
			int cellY = (int)Math.Floor(candidate.Projection.Y / merge);
			bool overlapsWinner = false;
			for (int offsetY = -1; offsetY <= 1 && !overlapsWinner; offsetY++)
			{
				for (int offsetX = -1; offsetX <= 1 && !overlapsWinner; offsetX++)
				{
					if (!cells.TryGetValue(CellKey(cellX + offsetX, cellY + offsetY), out List<int> cellMarkers))
					{
						continue;
					}
					for (int markerIndex = 0; markerIndex < cellMarkers.Count; markerIndex++)
					{
						MinimapItemMarker winner = markers[cellMarkers[markerIndex]];
						if (Math.Abs(winner.HeightDelta - candidate.Projection.HeightDelta) > projection.SameFloorTolerance)
						{
							continue;
						}
						float deltaX = winner.X - candidate.Projection.X;
						float deltaY = winner.Y - candidate.Projection.Y;
						if (deltaX * deltaX + deltaY * deltaY <= mergeSquared)
						{
							overlapsWinner = true;
							break;
						}
					}
				}
			}
			if (overlapsWinner)
			{
				continue;
			}

			int acceptedIndex = markers.Count;
			markers.Add(new MinimapItemMarker(candidate.Item.InstanceId, candidate.Item.Rarity,
				candidate.Projection.X, candidate.Projection.Y, candidate.Projection.HeightDelta,
				candidate.Projection.HorizontalDistanceSquared, candidate.Item.Position));
			long key = CellKey(cellX, cellY);
			if (!cells.TryGetValue(key, out List<int> acceptedCell))
			{
				acceptedCell = new List<int>();
				cells.Add(key, acceptedCell);
			}
			acceptedCell.Add(acceptedIndex);
		}
		return markers;
	}

	private static long CellKey(int x, int y)
	{
		return ((long)x << 32) ^ (uint)y;
	}
}
