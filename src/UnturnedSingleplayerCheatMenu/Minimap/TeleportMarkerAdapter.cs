using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class TeleportMarkerAdapter
{
	private readonly TeleportStore _store;
	private readonly List<MinimapWorldMarker> _markers = new List<MinimapWorldMarker>();

	public TeleportMarkerAdapter(TeleportStore store)
	{
		_store = store;
	}

	public IReadOnlyList<MinimapWorldMarker> Capture(Vector3 center, float radius)
	{
		_markers.Clear();
		if (_store?.Points == null)
		{
			return _markers;
		}
		string currentMap = Provider.map ?? string.Empty;
		float squaredRadius = radius * radius;
		for (int index = 0; index < _store.Points.Count; index++)
		{
			TeleportPoint point = _store.Points[index];
			if (point == null || !MinimapMarkerPolicies.ShouldShowTeleportMarker(point.Map, currentMap))
			{
				continue;
			}
			Vector3 delta = point.Position - center;
			if (delta.x * delta.x + delta.z * delta.z > squaredRadius)
			{
				continue;
			}
			if (!ColorUtility.TryParseHtmlString(point.MarkerColorHex, out Color color))
			{
				color = new Color(0.96f, 0.77f, 0.26f, 1f);
			}
			_markers.Add(new MinimapWorldMarker
			{
				StableId = StableStringHash(point.Id),
				Kind = MinimapWorldMarkerKind.Teleport,
				Position = point.Position,
				Color = color,
				Label = point.Name ?? string.Empty,
				TeleportKind = point.MarkerKind
			});
		}
		return _markers;
	}

	private static int StableStringHash(string value)
	{
		unchecked
		{
			int hash = 17;
			for (int index = 0; index < (value?.Length ?? 0); index++)
			{
				hash = hash * 31 + value[index];
			}
			return hash;
		}
	}
}
