using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal readonly struct MinimapWorldPoint
{
	public float X { get; }
	public float Y { get; }
	public float Z { get; }

	public MinimapWorldPoint(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}
}

internal readonly struct MinimapProjectionContext
{
	public MinimapWorldPoint Center { get; }
	public float VisibleRadiusMeters { get; }
	public float ViewportSizePixels { get; }
	public float MapYawDegrees { get; }
	public MinimapShape Shape { get; }
	public float SameFloorTolerance { get; }

	public MinimapProjectionContext(MinimapWorldPoint center, float visibleRadiusMeters, float viewportSizePixels,
		float mapYawDegrees, MinimapShape shape, float sameFloorTolerance)
	{
		Center = center;
		VisibleRadiusMeters = Math.Max(0.001f, visibleRadiusMeters);
		ViewportSizePixels = Math.Max(1f, viewportSizePixels);
		MapYawDegrees = mapYawDegrees;
		Shape = shape;
		SameFloorTolerance = Math.Max(0f, sameFloorTolerance);
	}

	public MinimapProjectionContext WithShape(MinimapShape shape)
	{
		return new MinimapProjectionContext(Center, VisibleRadiusMeters, ViewportSizePixels, MapYawDegrees, shape, SameFloorTolerance);
	}

	public MinimapProjectionContext WithMapYaw(float mapYawDegrees)
	{
		return new MinimapProjectionContext(Center, VisibleRadiusMeters, ViewportSizePixels, mapYawDegrees, Shape, SameFloorTolerance);
	}
}

internal readonly struct MinimapProjectedPoint
{
	public float X { get; }
	public float Y { get; }
	public float HorizontalDistanceSquared { get; }
	public float HeightDelta { get; }
	public bool IsVisible { get; }
	public bool IsSameFloor { get; }

	public MinimapProjectedPoint(float x, float y, float horizontalDistanceSquared, float heightDelta, bool isVisible, bool isSameFloor)
	{
		X = x;
		Y = y;
		HorizontalDistanceSquared = horizontalDistanceSquared;
		HeightDelta = heightDelta;
		IsVisible = isVisible;
		IsSameFloor = isSameFloor;
	}
}

internal static class MinimapProjection
{
	public static float ResolveCandidateScanRadius(float visibleRadiusMeters, MinimapShape shape)
	{
		float radius = Math.Max(0f, visibleRadiusMeters);
		return shape == MinimapShape.Square ? radius * (float)Math.Sqrt(2d) : radius;
	}

	public static MinimapProjectedPoint Project(MinimapWorldPoint worldPoint, MinimapProjectionContext context)
	{
		float deltaX = worldPoint.X - context.Center.X;
		float deltaZ = worldPoint.Z - context.Center.Z;
		float radians = context.MapYawDegrees * (float)(Math.PI / 180d);
		float cosine = (float)Math.Cos(radians);
		float sine = (float)Math.Sin(radians);
		float mapX = cosine * deltaX - sine * deltaZ;
		float mapZ = sine * deltaX + cosine * deltaZ;
		float horizontalDistanceSquared = mapX * mapX + mapZ * mapZ;
		float radius = context.VisibleRadiusMeters;
		bool visible = context.Shape == MinimapShape.Circle
			? horizontalDistanceSquared <= radius * radius
			: Math.Abs(mapX) <= radius && Math.Abs(mapZ) <= radius;
		float pixelsPerMeter = context.ViewportSizePixels * 0.5f / radius;
		float heightDelta = worldPoint.Y - context.Center.Y;
		return new MinimapProjectedPoint(
			mapX * pixelsPerMeter,
			mapZ * pixelsPerMeter,
			horizontalDistanceSquared,
			heightDelta,
			visible,
			Math.Abs(heightDelta) <= context.SameFloorTolerance);
	}

	public static float ResolvePlayerMarkerRotation(float playerYawDegrees, float mapYawDegrees)
	{
		return -NormalizeSignedDegrees(playerYawDegrees - mapYawDegrees);
	}

	private static float NormalizeSignedDegrees(float degrees)
	{
		degrees %= 360f;
		if (degrees > 180f) degrees -= 360f;
		if (degrees <= -180f) degrees += 360f;
		return degrees;
	}
}
