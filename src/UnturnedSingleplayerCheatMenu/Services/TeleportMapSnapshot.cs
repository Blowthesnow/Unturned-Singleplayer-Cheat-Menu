using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class TeleportMapSnapshot
{
	public string MapName { get; }

	public string MapPath { get; }

	public string ImagePath { get; }

	public Texture2D Texture { get; }

	public bool IsGps { get; }

	public bool IsChart { get; }

	public string Error { get; }

	public bool IsAvailable => Texture != null;

	public TeleportMapSnapshot(string mapName, string mapPath, string imagePath, Texture2D texture, bool isGps, bool isChart, string error)
	{
		MapName = mapName;
		MapPath = mapPath;
		ImagePath = imagePath;
		Texture = texture;
		IsGps = isGps;
		IsChart = isChart;
		Error = error;
	}

	public static TeleportMapSnapshot Unavailable(string mapName, string mapPath, string error)
	{
		return new TeleportMapSnapshot(mapName, mapPath, string.Empty, null, isGps: false, isChart: false, error);
	}
}
