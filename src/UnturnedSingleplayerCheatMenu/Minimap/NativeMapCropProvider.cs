using System;
using System.IO;
using BepInEx.Logging;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Minimap;

internal sealed class NativeMapCropProvider : IDisposable
{
	private readonly ManualLogSource _log;
	private Texture2D _texture;
	private string _loadedPath = string.Empty;
	private string _loadedMap = string.Empty;
	private string _lastFailure = string.Empty;

	public NativeMapCropProvider(ManualLogSource log)
	{
		_log = log;
	}

	public NativeMapCropSnapshot Capture(Vector3 center, float radius)
	{
		EnsureLoaded();
		if (_texture == null)
		{
			return NativeMapCropSnapshot.Unavailable(string.IsNullOrEmpty(_lastFailure)
				? "Map.png 不可用。"
				: _lastFailure);
		}

		CartographyVolume volume = CartographyVolumeManager.Get().GetMainVolume();
		Vector2 normalizedCenter = ProjectWorldPositionToNativeMap(center, volume);
		float halfWidth;
		float halfHeight;
		float mapYaw;
		if (volume != null)
		{
			Vector2 right = ProjectWorldPositionToNativeMap(center + volume.transform.right * radius, volume);
			Vector2 forward = ProjectWorldPositionToNativeMap(center + volume.transform.forward * radius, volume);
			halfWidth = Mathf.Abs(right.x - normalizedCenter.x);
			halfHeight = Mathf.Abs(forward.y - normalizedCenter.y);
			mapYaw = volume.transform.eulerAngles.y;
		}
		else
		{
			float levelSize = Mathf.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
			halfWidth = radius / levelSize;
			halfHeight = radius / levelSize;
			mapYaw = 0f;
		}

		halfWidth = Mathf.Max(0.0001f, halfWidth);
		halfHeight = Mathf.Max(0.0001f, halfHeight);
		Rect uv = new Rect(normalizedCenter.x - halfWidth, normalizedCenter.y - halfHeight,
			halfWidth * 2f, halfHeight * 2f);
		bool covers = uv.xMin >= 0f && uv.yMin >= 0f && uv.xMax <= 1f && uv.yMax <= 1f;
		return new NativeMapCropSnapshot(_texture, uv, mapYaw, true, covers,
			covers ? "原生 Map.png 局部裁剪" : "可见半径超出 Map.png 边界");
	}

	private static Vector2 ProjectWorldPositionToNativeMap(Vector3 worldPosition, CartographyVolume volume)
	{
		if (volume != null)
		{
			Vector3 local = volume.transform.InverseTransformPoint(worldPosition);
			return new Vector2(local.x + 0.5f, local.z + 0.5f);
		}

		float levelSize = Mathf.Max(1f, (float)(int)Level.size - (float)(int)Level.border * 2f);
		return new Vector2(worldPosition.x / levelSize + 0.5f, worldPosition.z / levelSize + 0.5f);
	}

	public void HandleSceneChanged()
	{
		DestroyTexture();
		_loadedMap = string.Empty;
		_loadedPath = string.Empty;
		_lastFailure = string.Empty;
	}

	private void EnsureLoaded()
	{
		string map = Provider.map ?? string.Empty;
		string levelPath = Level.info?.path ?? string.Empty;
		string mapPath = string.IsNullOrEmpty(levelPath) ? string.Empty : Path.Combine(levelPath, "Map.png");
		if (_texture != null && string.Equals(map, _loadedMap, StringComparison.Ordinal) &&
			string.Equals(mapPath, _loadedPath, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		DestroyTexture();
		_loadedMap = map;
		_loadedPath = mapPath;
		if (string.IsNullOrEmpty(mapPath) || !File.Exists(mapPath))
		{
			_lastFailure = "当前地图没有可读取的 Map.png。";
			return;
		}
		try
		{
			_texture = ReadWrite.readTextureFromFile(mapPath);
			if (_texture == null)
			{
				_lastFailure = "游戏读取接口没有返回 Map.png 纹理。";
				return;
			}
			_texture.name = "Minimap.NativeMap." + map;
			_texture.hideFlags = HideFlags.HideAndDontSave;
			_texture.filterMode = FilterMode.Bilinear;
			_texture.wrapMode = TextureWrapMode.Clamp;
			_lastFailure = string.Empty;
			_log?.LogInfo($"[Minimap] loaded private Map.png texture {map} {_texture.width}x{_texture.height}.");
		}
		catch (Exception exception)
		{
			_lastFailure = exception.Message;
			_log?.LogWarning("[Minimap] failed to load Map.png; realtime/grid fallback remains available.\n" + exception);
		}
	}

	private void DestroyTexture()
	{
		if (_texture != null)
		{
			UnityEngine.Object.Destroy(_texture);
			_texture = null;
		}
	}

	public void Dispose()
	{
		DestroyTexture();
	}
}
