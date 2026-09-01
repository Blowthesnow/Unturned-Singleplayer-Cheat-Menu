using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class MinimapStarGraphic : MaskableGraphic
{
	private const float InnerRadiusRatio = 0.18f;
	private const int PointCount = 8;

	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = GetPixelAdjustedRect();
		if (rect.width <= 0f || rect.height <= 0f)
		{
			return;
		}

		Vector2 center = rect.center;
		float outerX = rect.width * 0.5f;
		float outerY = rect.height * 0.5f;
		float innerX = outerX * InnerRadiusRatio;
		float innerY = outerY * InnerRadiusRatio;
		Color32 vertexColor = color;

		vertexHelper.AddVert(center, vertexColor, Vector2.zero);
		// Counter-clockwise perimeter keeps every triangle front-facing for CanvasRenderer.
		AddPoint(vertexHelper, center + new Vector2(0f, outerY), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(-innerX, innerY), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(-outerX, 0f), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(-innerX, -innerY), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(0f, -outerY), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(innerX, -innerY), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(outerX, 0f), vertexColor);
		AddPoint(vertexHelper, center + new Vector2(innerX, innerY), vertexColor);

		for (int index = 0; index < PointCount; index++)
		{
			int current = index + 1;
			int next = index == PointCount - 1 ? 1 : current + 1;
			vertexHelper.AddTriangle(0, current, next);
		}
	}

	private static void AddPoint(VertexHelper vertexHelper, Vector2 position, Color32 color) =>
		vertexHelper.AddVert(position, color, Vector2.zero);
}
