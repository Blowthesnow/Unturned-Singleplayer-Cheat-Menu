using System;
using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class TeleportMarkerIconFactory
{
	public static Sprite CreateSprite(TeleportMarkerKind kind, int size, out Texture2D texture)
	{
		texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave
		};
		Color[] pixels = new Color[size * size];
		texture.SetPixels(pixels);
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				Vector2 point = new Vector2(j, i);
				if (kind switch
				{
					TeleportMarkerKind.Square => IsSquare(point, size),
					TeleportMarkerKind.Circle => IsCircle(point, size),
					TeleportMarkerKind.Diamond => IsDiamond(point, size),
					_ => IsStar(point, size),
				})
				{
					texture.SetPixel(j, i, Color.white);
				}
			}
		}
		texture.Apply();
		Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
		sprite.hideFlags = HideFlags.HideAndDontSave;
		return sprite;
	}

	public static Sprite CreateCircleSprite(int size, out Texture2D texture)
	{
		Vector2 b = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave
		};
		Color[] array = new Color[size * size];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = Color.clear;
		}
		texture.SetPixels(array);
		float num = (float)size * 0.46f;
		for (int j = 0; j < size; j++)
		{
			for (int k = 0; k < size; k++)
			{
				if (Vector2.Distance(new Vector2(k, j), b) <= num)
				{
					texture.SetPixel(k, j, Color.white);
				}
			}
		}
		texture.Apply();
		Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
		sprite.hideFlags = HideFlags.HideAndDontSave;
		return sprite;
	}

	private static bool IsStar(Vector2 point, int size)
	{
		Vector2 vector = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		Vector2[] array = new Vector2[10];
		float num = (float)size * 0.46f;
		float num2 = (float)size * 0.2f;
		for (int i = 0; i < array.Length; i++)
		{
			float num3 = ((i % 2 == 0) ? num : num2);
			float f = MathF.PI / 2f + (float)i * MathF.PI / 5f;
			array[i] = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num3;
		}
		return IsInsidePolygon(point, array);
	}

	private static bool IsSquare(Vector2 point, int size)
	{
		float num = point.x / ((float)size - 1f);
		float num2 = point.y / ((float)size - 1f);
		if (num >= 0.18f && num <= 0.82f && num2 >= 0.18f)
		{
			return num2 <= 0.82f;
		}
		return false;
	}

	private static bool IsCircle(Vector2 point, int size)
	{
		Vector2 b = new Vector2((float)(size - 1) * 0.5f, (float)(size - 1) * 0.5f);
		return Vector2.Distance(point, b) <= (float)size * 0.39f;
	}

	private static bool IsDiamond(Vector2 point, int size)
	{
		float num = Mathf.Abs(point.x / ((float)size - 1f) - 0.5f);
		float num2 = Mathf.Abs(point.y / ((float)size - 1f) - 0.5f);
		return num + num2 <= 0.42f;
	}

	private static bool IsInsidePolygon(Vector2 point, Vector2[] vertices)
	{
		bool flag = false;
		int num = 0;
		int num2 = vertices.Length - 1;
		while (num < vertices.Length)
		{
			Vector2 vector = vertices[num];
			Vector2 vector2 = vertices[num2];
			if (vector.y > point.y != vector2.y > point.y && point.x < (vector2.x - vector.x) * (point.y - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			num2 = num++;
		}
		return flag;
	}
}
