using System;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal static class MinimapMarkerPolicies
{
	public static bool ShouldShowNativeMarker(bool isLocalPlayer, bool isSameGroup, bool isMarkerPlaced)
	{
		return isMarkerPlaced && (isLocalPlayer || isSameGroup);
	}

	public static bool ShouldShowTeleportMarker(string pointMap, string currentMap)
	{
		return string.Equals(pointMap ?? string.Empty, currentMap ?? string.Empty, StringComparison.OrdinalIgnoreCase);
	}
}
