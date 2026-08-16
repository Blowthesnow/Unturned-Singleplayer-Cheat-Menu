using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class TeleportStore
{
    private const string DefaultMarkerColorHex = "#F5C542";
    private readonly ManualLogSource _log;
    private readonly string _filePath;
    private readonly List<TeleportPoint> _points = new();

    public TeleportStore(ManualLogSource log)
    {
        _log = log;
        _filePath = Path.Combine(Paths.ConfigPath, "UnturnedSingleplayerCheatMenu.teleports.json");
    }

    public IReadOnlyList<TeleportPoint> Points => _points;
    public string FilePath => _filePath;

    public void Load()
    {
        _points.Clear();
        if (!File.Exists(_filePath))
            return;

        try
        {
            string json = File.ReadAllText(_filePath);
            TeleportPointCollection collection = JsonConvert.DeserializeObject<TeleportPointCollection>(json);
            if (collection?.Points != null)
            {
                _points.AddRange(collection.Points.Where(point => point != null && !string.IsNullOrWhiteSpace(point.Id)));
                foreach (TeleportPoint point in _points)
                {
                    if (!Enum.IsDefined(typeof(TeleportMarkerKind), point.MarkerKind))
                        point.MarkerKind = TeleportMarkerKind.Star;
                    point.MarkerColorHex = NormalizeMarkerColor(point.MarkerColorHex);
                }
            }
            Sort();
            _log.LogInfo($"已读取 {_points.Count} 个传送点。");
        }
        catch (Exception ex)
        {
            _log.LogError($"读取传送点失败，已保留原文件：{_filePath}\n{ex}");
        }
    }

    public TeleportPoint AddCurrent(
        string requestedName,
        TeleportMarkerKind markerKind = TeleportMarkerKind.Star,
        string markerColorHex = DefaultMarkerColorHex)
    {
        Player player = Player.LocalPlayer;
        if (player == null)
            return null;

        string map = Provider.map ?? string.Empty;
        int mapPointCount = _points.Count(point => string.Equals(point.Map, map, StringComparison.OrdinalIgnoreCase));
        string name = string.IsNullOrWhiteSpace(requestedName)
            ? PluginLocalization.DefaultTeleportName(mapPointCount + 1)
            : requestedName.Trim();

        Vector3 position = player.transform.position;
        TeleportPoint point = new()
        {
            Name = name,
            Map = map,
            X = position.x,
            Y = position.y,
            Z = position.z,
            Yaw = player.transform.rotation.eulerAngles.y,
            CreatedUtcTicks = DateTime.UtcNow.Ticks,
            MarkerKind = Enum.IsDefined(typeof(TeleportMarkerKind), markerKind)
                ? markerKind
                : TeleportMarkerKind.Star,
            MarkerColorHex = NormalizeMarkerColor(markerColorHex)
        };

        _points.Add(point);
        Sort();
        if (!Save())
        {
            _points.Remove(point);
            return null;
        }
        return point;
    }

    public bool Remove(string id)
    {
        int removed = _points.RemoveAll(point => string.Equals(point.Id, id, StringComparison.Ordinal));
        if (removed == 0)
            return false;

        if (Save())
            return true;

        Load();
        return false;
    }

    private void Sort()
    {
        _points.Sort((left, right) =>
        {
            int mapComparison = string.Compare(left.Map, right.Map, StringComparison.CurrentCultureIgnoreCase);
            if (mapComparison != 0)
                return mapComparison;
            return right.CreatedUtcTicks.CompareTo(left.CreatedUtcTicks);
        });
    }

    private static string NormalizeMarkerColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultMarkerColorHex;

        string candidate = value.Trim();
        if (!candidate.StartsWith("#", StringComparison.Ordinal))
            candidate = "#" + candidate;
        if (candidate.Length != 7)
            return DefaultMarkerColorHex;
        return ColorUtility.TryParseHtmlString(candidate, out _)
            ? candidate.ToUpperInvariant()
            : DefaultMarkerColorHex;
    }

    private bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? Paths.ConfigPath);
            TeleportPointCollection collection = new() { Points = _points.ToArray() };
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(_filePath, json);
            _log.LogInfo($"已保存 {_points.Count} 个传送点。");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"保存传送点失败：{_filePath}\n{ex}");
            return false;
        }
    }
}
