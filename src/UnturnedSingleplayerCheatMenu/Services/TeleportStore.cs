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

	private readonly List<TeleportPoint> _points = new List<TeleportPoint>();

	public IReadOnlyList<TeleportPoint> Points => _points;

	public string FilePath => _filePath;

	public TeleportStore(ManualLogSource log)
	{
		_log = log;
		_filePath = Path.Combine(Paths.ConfigPath, "UnturnedSingleplayerCheatMenu.teleports.json");
	}

	public void Load()
	{
		_points.Clear();
		if (!File.Exists(_filePath))
		{
			return;
		}
		try
		{
			TeleportPointCollection teleportPointCollection = JsonConvert.DeserializeObject<TeleportPointCollection>(File.ReadAllText(_filePath));
			if (teleportPointCollection?.Points != null)
			{
				_points.AddRange(teleportPointCollection.Points.Where((TeleportPoint point) => point != null && !string.IsNullOrWhiteSpace(point.Id)));
				foreach (TeleportPoint point in _points)
				{
					if (!Enum.IsDefined(typeof(TeleportMarkerKind), point.MarkerKind))
					{
						point.MarkerKind = TeleportMarkerKind.Star;
					}
					point.MarkerColorHex = NormalizeMarkerColor(point.MarkerColorHex);
				}
			}
			Sort();
			_log.LogInfo($"已读取 {_points.Count} 个传送点。");
		}
		catch (Exception arg)
		{
			_log.LogError($"读取传送点失败，已保留原文件：{_filePath}\n{arg}");
		}
	}

	public TeleportPoint AddCurrent(string requestedName, TeleportMarkerKind markerKind = TeleportMarkerKind.Star, string markerColorHex = "#F5C542")
	{
		Player localPlayer = Player.LocalPlayer;
		if (localPlayer == null)
		{
			return null;
		}
		string map = Provider.map ?? string.Empty;
		int num = _points.Count((TeleportPoint point) => string.Equals(point.Map, map, StringComparison.OrdinalIgnoreCase));
		string name = (string.IsNullOrWhiteSpace(requestedName) ? PluginLocalization.DefaultTeleportName(num + 1) : requestedName.Trim());
		Vector3 position = localPlayer.transform.position;
		TeleportPoint teleportPoint = new TeleportPoint
		{
			Name = name,
			Map = map,
			X = position.x,
			Y = position.y,
			Z = position.z,
			Yaw = localPlayer.transform.rotation.eulerAngles.y,
			CreatedUtcTicks = DateTime.UtcNow.Ticks,
			MarkerKind = (Enum.IsDefined(typeof(TeleportMarkerKind), markerKind) ? markerKind : TeleportMarkerKind.Star),
			MarkerColorHex = NormalizeMarkerColor(markerColorHex)
		};
		_points.Add(teleportPoint);
		Sort();
		if (!Save())
		{
			_points.Remove(teleportPoint);
			return null;
		}
		return teleportPoint;
	}

	public bool Remove(string id)
	{
		if (_points.RemoveAll((TeleportPoint point) => string.Equals(point.Id, id, StringComparison.Ordinal)) == 0)
		{
			return false;
		}
		if (Save())
		{
			return true;
		}
		Load();
		return false;
	}

	private void Sort()
	{
		_points.Sort(delegate(TeleportPoint left, TeleportPoint right)
		{
			int num = string.Compare(left.Map, right.Map, StringComparison.CurrentCultureIgnoreCase);
			return (num != 0) ? num : right.CreatedUtcTicks.CompareTo(left.CreatedUtcTicks);
		});
	}

	private static string NormalizeMarkerColor(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "#F5C542";
		}
		string text = value.Trim();
		if (!text.StartsWith("#", StringComparison.Ordinal))
		{
			text = "#" + text;
		}
		if (text.Length != 7)
		{
			return "#F5C542";
		}
		if (!ColorUtility.TryParseHtmlString(text, out var _))
		{
			return "#F5C542";
		}
		return text.ToUpperInvariant();
	}

	private bool Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? Paths.ConfigPath);
			string contents = JsonConvert.SerializeObject(new TeleportPointCollection
			{
				Points = _points.ToArray()
			}, Formatting.Indented);
			File.WriteAllText(_filePath, contents);
			_log.LogInfo($"已保存 {_points.Count} 个传送点。");
			return true;
		}
		catch (Exception arg)
		{
			_log.LogError($"保存传送点失败：{_filePath}\n{arg}");
			return false;
		}
	}
}
