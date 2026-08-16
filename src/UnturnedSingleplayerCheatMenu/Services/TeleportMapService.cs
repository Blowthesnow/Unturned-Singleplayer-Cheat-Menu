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

        string mapName = Provider.map ?? string.Empty;
        string mapPath = Level.info?.path;
        if (string.IsNullOrWhiteSpace(mapPath))
        {
            _log.LogWarning($"地图扫描失败：当前地图没有可用路径。地图={mapName}。");
            return TeleportMapSnapshot.Unavailable(mapName, string.Empty, "当前地图尚未加载完成。");
        }

        string[] candidates =
        {
            Path.Combine(mapPath, "Map.png"),
            Path.Combine(mapPath, "Chart.png"),
            Path.Combine(mapPath, "Level.png"),
            Path.Combine(mapPath, "Preview.png")
        };

        foreach (string filePath in candidates)
        {
            if (!File.Exists(filePath))
                continue;

            try
            {
                Texture2D texture = ReadWrite.readTextureFromFile(filePath);
                if (texture == null)
                    continue;

                texture.name = $"TeleportMap.{mapName}";
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                _loadedTexture = texture;

                bool isGps = string.Equals(Path.GetFileName(filePath), "Map.png", StringComparison.OrdinalIgnoreCase);
                bool isChart = string.Equals(Path.GetFileName(filePath), "Chart.png", StringComparison.OrdinalIgnoreCase);
                string source = isGps
                    ? "GPS/卫星图 Map.png"
                    : isChart
                        ? "地形图 Chart.png"
                        : $"地图预览 {Path.GetFileName(filePath)}";
                _log.LogInfo($"地图扫描完成：地图={mapName}，来源={source}，尺寸={texture.width}x{texture.height}，路径={filePath}。");
                return new TeleportMapSnapshot(mapName, mapPath, filePath, texture, isGps, isChart, string.Empty);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"读取地图图片失败：{filePath}\n{ex}");
            }
        }

        _log.LogWarning($"地图扫描完成但未找到 Map.png、Chart.png、Level.png 或 Preview.png：地图={mapName}，路径={mapPath}。");
        return TeleportMapSnapshot.Unavailable(
            mapName,
            mapPath,
            "当前地图没有可读取的 Map.png / Chart.png；仍可保存当前位置传送点。");
    }

    public void Dispose()
    {
        DestroyLoadedTexture();
    }

    public static Vector2 ProjectWorldPositionToMap(Vector3 worldPosition)
    {
        CartographyVolume mainVolume =
            VolumeManager<CartographyVolume, CartographyVolumeManager>.Get().GetMainVolume();
        if (mainVolume != null)
        {
            Vector3 local = mainVolume.transform.InverseTransformPoint(worldPosition);
            return new Vector2(local.x + 0.5f, 0.5f - local.z);
        }

        float mapSize = Math.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
        return new Vector2(worldPosition.x / mapSize + 0.5f, 0.5f - worldPosition.z / mapSize);
    }

    public static Vector3 DeprojectMapToWorld(Vector2 mapPosition)
    {
        CartographyVolume mainVolume =
            VolumeManager<CartographyVolume, CartographyVolumeManager>.Get().GetMainVolume();
        if (mainVolume != null)
        {
            Vector3 local = new(mapPosition.x - 0.5f, 0f, 0.5f - mapPosition.y);
            Vector3 world = mainVolume.transform.TransformPoint(local);
            world.y = 0f;
            return world;
        }

        float mapSize = Math.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
        return new Vector3(
            (mapPosition.x - 0.5f) * mapSize,
            0f,
            (0.5f - mapPosition.y) * mapSize);
    }

    private void DestroyLoadedTexture()
    {
        if (_loadedTexture == null)
            return;

        UnityEngine.Object.Destroy(_loadedTexture);
        _loadedTexture = null;
    }
}

internal sealed class TeleportMapSnapshot
{
    public TeleportMapSnapshot(
        string mapName,
        string mapPath,
        string imagePath,
        Texture2D texture,
        bool isGps,
        bool isChart,
        string error)
    {
        MapName = mapName;
        MapPath = mapPath;
        ImagePath = imagePath;
        Texture = texture;
        IsGps = isGps;
        IsChart = isChart;
        Error = error;
    }

    public string MapName { get; }
    public string MapPath { get; }
    public string ImagePath { get; }
    public Texture2D Texture { get; }
    public bool IsGps { get; }
    public bool IsChart { get; }
    public string Error { get; }
    public bool IsAvailable => Texture != null;

    public static TeleportMapSnapshot Unavailable(string mapName, string mapPath, string error)
    {
        return new TeleportMapSnapshot(mapName, mapPath, string.Empty, null, false, false, error);
    }
}
