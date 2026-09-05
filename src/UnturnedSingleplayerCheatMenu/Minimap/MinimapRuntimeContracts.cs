using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal enum MinimapWorldMarkerKind
{
	NativeMapMarker,
	Teleport,
	Zombie,
	Animal,
	Player,
	Item
}

internal enum MinimapHeightIndicator
{
	None,
	Above,
	Below
}

internal sealed class MinimapWorldMarker
{
	public int StableId { get; set; }
	public MinimapWorldMarkerKind Kind { get; set; }
	public Vector3 Position { get; set; }
	public Color Color { get; set; }
	public string Label { get; set; }
	public TeleportMarkerKind TeleportKind { get; set; }
	public MinimapItemRarity ItemRarity { get; set; }
	public float Rotation { get; set; }
	public Transform FollowTarget { get; set; }
}

internal readonly struct MinimapHudMarker
{
	public int StableId { get; }
	public MinimapWorldMarkerKind Kind { get; }
	public float X { get; }
	public float Y { get; }
	public Color Color { get; }
	public float Rotation { get; }
	public TeleportMarkerKind TeleportKind { get; }
	public MinimapHeightIndicator HeightIndicator { get; }

	public MinimapHudMarker(int stableId, MinimapWorldMarkerKind kind, float x, float y,
		Color color, float rotation = 0f, TeleportMarkerKind teleportKind = TeleportMarkerKind.Star,
		MinimapHeightIndicator heightIndicator = MinimapHeightIndicator.None)
	{
		StableId = stableId;
		Kind = kind;
		X = x;
		Y = y;
		Color = color;
		Rotation = rotation;
		TeleportKind = teleportKind;
		HeightIndicator = heightIndicator;
	}
}

internal readonly struct NativeMapCropSnapshot
{
	public Texture2D Texture { get; }
	public Rect UvRect { get; }
	public Rect ViewportRect { get; }
	public float MapYawDegrees { get; }
	public bool IsAvailable { get; }
	public bool CoversVisibleRadius { get; }
	public string Status { get; }

	public NativeMapCropSnapshot(Texture2D texture, Rect uvRect, Rect viewportRect, float mapYawDegrees,
		bool isAvailable, bool coversVisibleRadius, string status)
	{
		Texture = texture;
		UvRect = uvRect;
		ViewportRect = viewportRect;
		MapYawDegrees = mapYawDegrees;
		IsAvailable = isAvailable;
		CoversVisibleRadius = coversVisibleRadius;
		Status = status ?? string.Empty;
	}

	public static NativeMapCropSnapshot Unavailable(string status)
	{
		return new NativeMapCropSnapshot(null, new Rect(0f, 0f, 1f, 1f),
			new Rect(0f, 0f, 1f, 1f), 0f, false, false, status);
	}
}

internal readonly struct OverheadOcclusionDiagnostics
{
	public OcclusionSummary Summary { get; }
	public bool UndergroundAllowlistContainsPlayer { get; }

	public OverheadOcclusionDiagnostics(OcclusionSummary summary, bool undergroundAllowlistContainsPlayer)
	{
		Summary = summary;
		UndergroundAllowlistContainsPlayer = undergroundAllowlistContainsPlayer;
	}
}
