using UnityEngine;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class TeleportMarkerIconFactory
{
    public static Sprite CreateSprite(TeleportMarkerKind kind, int size, out Texture2D texture)
    {
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] clearPixels = new Color[size * size];
        texture.SetPixels(clearPixels);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new(x, y);
                bool filled = kind switch
                {
                    TeleportMarkerKind.Square => IsSquare(point, size),
                    TeleportMarkerKind.Circle => IsCircle(point, size),
                    TeleportMarkerKind.Diamond => IsDiamond(point, size),
                    _ => IsStar(point, size)
                };
                if (filled)
                    texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public static Sprite CreateCircleSprite(int size, out Texture2D texture)
    {
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] clearPixels = new Color[size * size];
        for (int index = 0; index < clearPixels.Length; index++)
            clearPixels[index] = Color.clear;
        texture.SetPixels(clearPixels);

        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (Vector2.Distance(new Vector2(x, y), center) <= radius)
                    texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static bool IsStar(Vector2 point, int size)
    {
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        Vector2[] vertices = new Vector2[10];
        float outerRadius = size * 0.46f;
        float innerRadius = size * 0.20f;
        for (int index = 0; index < vertices.Length; index++)
        {
            float radius = index % 2 == 0 ? outerRadius : innerRadius;
            float angle = Mathf.PI * 0.5f + index * Mathf.PI / 5f;
            vertices[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return IsInsidePolygon(point, vertices);
    }

    private static bool IsSquare(Vector2 point, int size)
    {
        float x = point.x / (size - 1f);
        float y = point.y / (size - 1f);
        return x >= 0.18f && x <= 0.82f && y >= 0.18f && y <= 0.82f;
    }

    private static bool IsCircle(Vector2 point, int size)
    {
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        return Vector2.Distance(point, center) <= size * 0.39f;
    }

    private static bool IsDiamond(Vector2 point, int size)
    {
        float x = Mathf.Abs(point.x / (size - 1f) - 0.5f);
        float y = Mathf.Abs(point.y / (size - 1f) - 0.5f);
        return x + y <= 0.42f;
    }

    private static bool IsInsidePolygon(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        for (int index = 0, previous = vertices.Length - 1; index < vertices.Length; previous = index++)
        {
            Vector2 current = vertices[index];
            Vector2 prior = vertices[previous];
            bool crosses = current.y > point.y != prior.y > point.y;
            if (crosses
                && point.x < (prior.x - current.x) * (point.y - current.y) / (prior.y - current.y) + current.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
