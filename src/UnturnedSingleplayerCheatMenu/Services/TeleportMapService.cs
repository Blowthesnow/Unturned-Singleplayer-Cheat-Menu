using System;
using System.IO;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class TeleportMapService
{
	private readonly ManualLogSource _log;

	private Texture2D _loadedTexture;

	public TeleportMapService(ManualLogSource log)
	{
		_log = log;
	}

	public TeleportMapSnapshot ScanCurrentMap()
	{
		DestroyLoadedTexture();
		string text = Provider.map ?? string.Empty;
		string text2 = Level.info?.path;
		if (string.IsNullOrWhiteSpace(text2))
		{
			_log.LogWarning("地图扫描失败：当前地图没有可用路径。地图=" + text + "。");
			return TeleportMapSnapshot.Unavailable(text, string.Empty, "当前地图尚未加载完成。");
		}
		string[] array = new string[4]
		{
			Path.Combine(text2, "Map.png"),
			Path.Combine(text2, "Chart.png"),
			Path.Combine(text2, "Level.png"),
			Path.Combine(text2, "Preview.png")
		};
		foreach (string text3 in array)
		{
			if (!File.Exists(text3))
			{
				continue;
			}
			try
			{
				Texture2D texture2D = ReadWrite.readTextureFromFile(text3);
				if (texture2D == null)
				{
					continue;
				}
				texture2D.name = "TeleportMap." + text;
				texture2D.hideFlags = HideFlags.HideAndDontSave;
				texture2D.filterMode = FilterMode.Bilinear;
				_loadedTexture = texture2D;
				bool flag = string.Equals(Path.GetFileName(text3), "Map.png", StringComparison.OrdinalIgnoreCase);
				bool flag2 = string.Equals(Path.GetFileName(text3), "Chart.png", StringComparison.OrdinalIgnoreCase);
				string text4 = (flag ? "GPS/卫星图 Map.png" : (flag2 ? "地形图 Chart.png" : ("地图预览 " + Path.GetFileName(text3))));
				_log.LogInfo($"地图扫描完成：地图={text}，来源={text4}，尺寸={texture2D.width}x{texture2D.height}，路径={text3}。");
				return new TeleportMapSnapshot(text, text2, text3, texture2D, flag, flag2, string.Empty);
			}
			catch (Exception arg)
			{
				_log.LogWarning($"读取地图图片失败：{text3}\n{arg}");
			}
		}
		_log.LogWarning("地图扫描完成但未找到 Map.png、Chart.png、Level.png 或 Preview.png：地图=" + text + "，路径=" + text2 + "。");
		return TeleportMapSnapshot.Unavailable(text, text2, "当前地图没有可读取的 Map.png / Chart.png；仍可保存当前位置传送点。");
	}

	public void Dispose()
	{
		DestroyLoadedTexture();
	}

	public static Vector2 ProjectWorldPositionToMap(Vector3 worldPosition)
	{
		CartographyVolume mainVolume = VolumeManager<CartographyVolume, CartographyVolumeManager>.Get().GetMainVolume();
		if (mainVolume != null)
		{
			Vector3 vector = mainVolume.transform.InverseTransformPoint(worldPosition);
			return new Vector2(vector.x + 0.5f, 0.5f - vector.z);
		}
		float num = Math.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
		return new Vector2(worldPosition.x / num + 0.5f, 0.5f - worldPosition.z / num);
	}

	public static bool TryGetLocalPlayerMapRotation(out float rotation)
	{
		Player player = Player.LocalPlayer;
		if (player == null)
		{
			rotation = 0f;
			return false;
		}
		CartographyVolume mainVolume = VolumeManager<CartographyVolume, CartographyVolumeManager>.Get().GetMainVolume();
		rotation = player.transform.rotation.eulerAngles.y - (mainVolume != null ? mainVolume.transform.eulerAngles.y : 0f);
		return true;
	}

	public static Vector3 DeprojectMapToWorld(Vector2 mapPosition)
	{
		CartographyVolume mainVolume = VolumeManager<CartographyVolume, CartographyVolumeManager>.Get().GetMainVolume();
		if (mainVolume != null)
		{
			Vector3 position = new Vector3(mapPosition.x - 0.5f, 0f, 0.5f - mapPosition.y);
			Vector3 result = mainVolume.transform.TransformPoint(position);
			result.y = 0f;
			return result;
		}
		float num = Math.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
		return new Vector3((mapPosition.x - 0.5f) * num, 0f, (0.5f - mapPosition.y) * num);
	}

	private void DestroyLoadedTexture()
	{
		if (!(_loadedTexture == null))
		{
			UnityEngine.Object.Destroy(_loadedTexture);
			_loadedTexture = null;
		}
	}
}
