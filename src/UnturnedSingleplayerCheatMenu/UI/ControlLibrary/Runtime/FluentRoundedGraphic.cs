using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class FluentRoundedGraphic : MaskableGraphic
{
	[SerializeField]
	private float _radius = 6f;

	[SerializeField]
	private int _cornerSegments = 6;

	internal float Radius
	{
		get => _radius;
		set
		{
			float normalized = Mathf.Max(0f, value);
			if (!Mathf.Approximately(_radius, normalized))
			{
				_radius = normalized;
				SetVerticesDirty();
			}
		}
	}

	internal int CornerSegments
	{
		get => _cornerSegments;
		set
		{
			int normalized = Mathf.Clamp(value, 2, 16);
			if (_cornerSegments != normalized)
			{
				_cornerSegments = normalized;
				SetVerticesDirty();
			}
		}
	}

	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = rectTransform.rect;
		if (rect.width <= 0f || rect.height <= 0f)
		{
			return;
		}

		float radius = Mathf.Min(_radius, Mathf.Min(rect.width, rect.height) * 0.5f);
		Color32 vertexColor = color;
		vertexHelper.AddVert(rect.center, vertexColor, Vector2.zero);
		if (radius <= 0.01f)
		{
			AddPerimeterVertex(vertexHelper, new Vector2(rect.xMin, rect.yMin), vertexColor);
			AddPerimeterVertex(vertexHelper, new Vector2(rect.xMax, rect.yMin), vertexColor);
			AddPerimeterVertex(vertexHelper, new Vector2(rect.xMax, rect.yMax), vertexColor);
			AddPerimeterVertex(vertexHelper, new Vector2(rect.xMin, rect.yMax), vertexColor);
		}
		else
		{
			AddCorner(vertexHelper, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, vertexColor);
			AddCorner(vertexHelper, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, vertexColor);
			AddCorner(vertexHelper, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, vertexColor);
			AddCorner(vertexHelper, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, vertexColor);
		}

		int perimeterCount = vertexHelper.currentVertCount - 1;
		for (int index = 0; index < perimeterCount; index++)
		{
			int current = index + 1;
			int next = index == perimeterCount - 1 ? 1 : current + 1;
			vertexHelper.AddTriangle(0, current, next);
		}
	}

	private void AddCorner(VertexHelper helper, Vector2 center, float radius, float startDegrees, float endDegrees, Color32 color)
	{
		int segments = Mathf.Clamp(_cornerSegments, 2, 16);
		for (int index = 0; index <= segments; index++)
		{
			float radians = Mathf.Lerp(startDegrees, endDegrees, index / (float)segments) * Mathf.Deg2Rad;
			AddPerimeterVertex(helper, center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius, color);
		}
	}

	private static void AddPerimeterVertex(VertexHelper helper, Vector2 position, Color32 color) =>
		helper.AddVert(position, color, Vector2.zero);
}
