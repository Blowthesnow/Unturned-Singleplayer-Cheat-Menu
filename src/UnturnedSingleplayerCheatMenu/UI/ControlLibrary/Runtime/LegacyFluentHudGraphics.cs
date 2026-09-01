using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnturnedSingleplayerCheatMenu.UI;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

/// <summary>Geometry subset ported from ControlGallery.Ugui's HUD graphics.</summary>
internal static class LegacyFluentHudGeometry
{
	internal static Vector2 PointOnCircle(Vector2 center, float radius, float degrees)
	{
		float radians = degrees * Mathf.Deg2Rad;
		return center + new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
	}

	internal static Color WithAlpha(Color value, float multiplier)
	{
		value.a *= Mathf.Clamp01(multiplier);
		return value;
	}

	internal static void AddDisc(VertexHelper helper, Vector2 center, float radius, Color color, int segments = 32)
	{
		if (radius <= 0f)
		{
			return;
		}

		int count = Mathf.Clamp(segments, 8, 96);
		int first = helper.currentVertCount;
		Color32 vertexColor = color;
		helper.AddVert(center, vertexColor, Vector2.zero);
		for (int index = 0; index <= count; index++)
		{
			float angle = index * Mathf.PI * 2f / count;
			helper.AddVert(center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius, vertexColor, Vector2.zero);
		}
		for (int index = 1; index <= count; index++)
		{
			helper.AddTriangle(first, first + index, first + index + 1);
		}
	}

	internal static void AddRing(
		VertexHelper helper,
		Vector2 center,
		float radius,
		float thickness,
		float startDegrees,
		float sweepDegrees,
		Color color)
	{
		if (radius <= 0f || thickness <= 0f || Mathf.Abs(sweepDegrees) < 0.01f)
		{
			return;
		}

		int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweepDegrees) / 5f), 2, 96);
		float outer = radius + thickness * 0.5f;
		float inner = Mathf.Max(0f, radius - thickness * 0.5f);
		int first = helper.currentVertCount;
		Color32 vertexColor = color;
		for (int index = 0; index <= segments; index++)
		{
			float angle = startDegrees + sweepDegrees * index / segments;
			helper.AddVert(PointOnCircle(center, inner, angle), vertexColor, Vector2.zero);
			helper.AddVert(PointOnCircle(center, outer, angle), vertexColor, Vector2.zero);
		}
		for (int index = 0; index < segments; index++)
		{
			int current = first + index * 2;
			helper.AddTriangle(current, current + 1, current + 3);
			helper.AddTriangle(current, current + 3, current + 2);
		}
	}

	internal static void AddSegment(VertexHelper helper, Vector2 start, Vector2 end, float width, Color color)
	{
		Vector2 direction = end - start;
		if (direction.sqrMagnitude < 0.001f)
		{
			return;
		}

		Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
		int first = helper.currentVertCount;
		Color32 vertexColor = color;
		helper.AddVert(start - normal, vertexColor, Vector2.zero);
		helper.AddVert(start + normal, vertexColor, Vector2.zero);
		helper.AddVert(end + normal, vertexColor, Vector2.zero);
		helper.AddVert(end - normal, vertexColor, Vector2.zero);
		helper.AddTriangle(first, first + 1, first + 2);
		helper.AddTriangle(first, first + 2, first + 3);
	}
}

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LegacyFluentEllipseGraphic : MaskableGraphic
{
	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = rectTransform.rect;
		if (rect.width <= 0f || rect.height <= 0f)
		{
			return;
		}

		Color32 vertexColor = color;
		vertexHelper.AddVert(rect.center, vertexColor, Vector2.zero);
		const int segments = 64;
		for (int index = 0; index <= segments; index++)
		{
			float angle = index * Mathf.PI * 2f / segments;
			Vector2 point = rect.center + new Vector2(
				Mathf.Cos(angle) * rect.width * 0.5f,
				Mathf.Sin(angle) * rect.height * 0.5f);
			vertexHelper.AddVert(point, vertexColor, Vector2.zero);
		}
		for (int index = 1; index <= segments; index++)
		{
			vertexHelper.AddTriangle(0, index, index + 1);
		}
	}
}

/// <summary>Legacy Text-compatible port of ControlGallery.Ugui FluentTimeRingGraphic.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LegacyFluentTimeRingGraphic : MaskableGraphic,
	IPointerDownHandler,
	IInitializePotentialDragHandler,
	IBeginDragHandler,
	IDragHandler,
	IEndDragHandler,
	IPointerUpHandler,
	IScrollHandler,
	IMoveHandler,
	ISelectHandler,
	IDeselectHandler
{
	private float _time = 12f;
	private float _step = 0.25f;
	private float _ringThickness = 18f;
	private float _handleSize = 28f;
	private readonly WorldTimeRingDragAccumulator _dragAccumulator = new WorldTimeRingDragAccumulator();
	private bool _dragging;
	private bool _focused;
	private Camera _dragEventCamera;
	private ScrollRect _lockedScrollRect;
	private bool _lockedScrollHorizontal;
	private bool _lockedScrollVertical;
	private Color _trackColor = Color.gray;
	private Color _accentColor = Color.white;
	private Color _dayColor = Color.yellow;
	private Color _nightColor = Color.cyan;
	private Color _dawnColor = Color.red;
	private Color _tickColor = Color.white;
	private Color _focusColor = Color.cyan;

	internal event Action<float> TimeChanged;

	internal float Time => _time;

	internal bool IsDragging => _dragging;

	internal float Step
	{
		get => _step;
		set => _step = Mathf.Clamp(Sanitize(value, 0.25f), 1f / 60f, 6f);
	}

	internal float RingThickness
	{
		get => _ringThickness;
		set
		{
			_ringThickness = Mathf.Max(4f, Sanitize(value, 18f));
			SetVerticesDirty();
		}
	}

	internal float HandleSize
	{
		get => _handleSize;
		set
		{
			_handleSize = Mathf.Max(10f, Sanitize(value, 28f));
			SetVerticesDirty();
		}
	}

	internal void SetPalette(
		Color track,
		Color accent,
		Color day,
		Color night,
		Color dawn,
		Color ticks,
		Color focus)
	{
		_trackColor = track;
		_accentColor = accent;
		_dayColor = day;
		_nightColor = night;
		_dawnColor = dawn;
		_tickColor = ticks;
		_focusColor = focus;
		SetVerticesDirty();
	}

	internal void SetTime(float value, bool notify)
	{
		float normalized = NormalizeTime(value);
		if (Mathf.Approximately(_time, normalized))
		{
			return;
		}

		_time = normalized;
		SetVerticesDirty();
		if (notify)
		{
			TimeChanged?.Invoke(_time);
		}
	}

	internal void Adjust(float delta, bool notify = true)
	{
		SetTime(_time + delta, notify);
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		_dragging = true;
		eventData.selectedObject = gameObject;
		LockParentScroll();
		BeginPointerAdjustment(eventData);
	}

	public void OnInitializePotentialDrag(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			eventData.useDragThreshold = false;
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		_dragging = true;
		eventData.selectedObject = gameObject;
		LockParentScroll();
		if (!_dragAccumulator.IsActive)
		{
			BeginPointerAdjustment(eventData);
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (eventData.button != PointerEventData.InputButton.Left)
		{
			return;
		}
		if (!_dragging)
		{
			_dragging = true;
			eventData.selectedObject = gameObject;
			LockParentScroll();
			BeginPointerAdjustment(eventData);
		}
		else
		{
			ContinuePointerAdjustment(eventData);
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			EndPointerAdjustment();
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			EndPointerAdjustment();
		}
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (!Mathf.Approximately(eventData.scrollDelta.y, 0f))
		{
			Adjust(Mathf.Sign(eventData.scrollDelta.y) * _step);
			eventData.Use();
		}
	}

	public void OnMove(AxisEventData eventData)
	{
		switch (eventData.moveDir)
		{
			case MoveDirection.Left:
			case MoveDirection.Down:
				Adjust(-_step);
				eventData.Use();
				break;
			case MoveDirection.Right:
			case MoveDirection.Up:
				Adjust(_step);
				eventData.Use();
				break;
		}
	}

	public void OnSelect(BaseEventData eventData)
	{
		_focused = true;
		SetVerticesDirty();
	}

	public void OnDeselect(BaseEventData eventData)
	{
		_focused = false;
		SetVerticesDirty();
	}

	protected override void OnDisable()
	{
		EndPointerAdjustment();
		base.OnDisable();
	}

	private void Update()
	{
		if (!_dragging)
		{
			return;
		}
		if (!Input.GetMouseButton(0))
		{
			EndPointerAdjustment();
			return;
		}
		Vector3 mousePosition = Input.mousePosition;
		ContinuePointerAdjustment(new Vector2(mousePosition.x, mousePosition.y), _dragEventCamera);
	}

	protected override void OnPopulateMesh(VertexHelper vertexHelper)
	{
		vertexHelper.Clear();
		Rect rect = rectTransform.rect;
		float side = Mathf.Min(rect.width, rect.height);
		if (side < 80f)
		{
			return;
		}

		Vector2 center = rect.center;
		float outerRadius = side * 0.5f - 5f;
		float ringRadius = outerRadius - Mathf.Max(_handleSize, _ringThickness) * 0.5f - 8f;
		LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, _ringThickness, 0f, 360f, _trackColor);
		float phaseThickness = Mathf.Max(5f, _ringThickness - 7f);
		if (_time < 12f)
		{
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 0f, 150f, _nightColor);
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 150f, 60f, _dawnColor);
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 210f, 150f, _dayColor);
		}
		else
		{
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 0f, 180f, _dayColor);
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 180f, 60f, _dawnColor);
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius, phaseThickness, 240f, 120f, _nightColor);
		}

		float sweep = Mathf.Repeat(_time, 12f) / 12f * 360f;
		LegacyFluentHudGeometry.AddRing(vertexHelper, center, ringRadius + _ringThickness * 0.5f + 4f, 4f, 0f, sweep, _accentColor);
		for (int tick = 0; tick < 60; tick++)
		{
			bool major = tick % 5 == 0;
			Vector2 outer = LegacyFluentHudGeometry.PointOnCircle(center, ringRadius - _ringThickness * 0.5f - 5f, tick * 6f);
			Vector2 inner = LegacyFluentHudGeometry.PointOnCircle(center, ringRadius - _ringThickness * 0.5f - (major ? 18f : 9f), tick * 6f);
			LegacyFluentHudGeometry.AddSegment(
				vertexHelper,
				outer,
				inner,
				major ? 2f : 0.8f,
				LegacyFluentHudGeometry.WithAlpha(_tickColor, major ? 0.8f : 0.24f));
		}

		Vector2 handle = LegacyFluentHudGeometry.PointOnCircle(center, ringRadius, sweep);
		LegacyFluentHudGeometry.AddSegment(vertexHelper, center, handle, 1.5f, LegacyFluentHudGeometry.WithAlpha(_accentColor, 0.36f));
		LegacyFluentHudGeometry.AddDisc(vertexHelper, handle, _handleSize * 0.72f, LegacyFluentHudGeometry.WithAlpha(_accentColor, 0.2f));
		LegacyFluentHudGeometry.AddDisc(vertexHelper, handle, _handleSize * 0.5f, _trackColor);
		LegacyFluentHudGeometry.AddRing(vertexHelper, handle, _handleSize * 0.5f - 1f, 4f, 0f, 360f, _accentColor);
		LegacyFluentHudGeometry.AddDisc(vertexHelper, handle, 4f, _accentColor, 16);
		if (_focused)
		{
			LegacyFluentHudGeometry.AddRing(vertexHelper, center, outerRadius - 2f, 2f, 0f, 360f, _focusColor);
		}
	}

	private void BeginPointerAdjustment(PointerEventData eventData)
	{
		_dragEventCamera = eventData.pressEventCamera;
		BeginPointerAdjustment(eventData.position, _dragEventCamera);
	}

	private void BeginPointerAdjustment(Vector2 screenPosition, Camera eventCamera)
	{
		if (!TryGetPointerAngle(screenPosition, eventCamera, out float angle))
		{
			return;
		}
		float targetHour = angle / 360f * 12f;
		float currentHour = Mathf.Repeat(_time, 12f);
		float delta = targetHour - currentHour;
		if (delta > 6f)
		{
			delta -= 12f;
		}
		else if (delta < -6f)
		{
			delta += 12f;
		}
		SetTime(Mathf.Round((_time + delta) / _step) * _step, notify: true);
		_dragAccumulator.Begin(_time, angle);
	}

	private void ContinuePointerAdjustment(PointerEventData eventData)
	{
		ContinuePointerAdjustment(eventData.position, eventData.pressEventCamera ?? _dragEventCamera);
	}

	private void ContinuePointerAdjustment(Vector2 screenPosition, Camera eventCamera)
	{
		if (!TryGetPointerAngle(screenPosition, eventCamera, out float angle))
		{
			return;
		}
		if (!_dragAccumulator.IsActive)
		{
			BeginPointerAdjustment(screenPosition, eventCamera);
			return;
		}
		SetTime(_dragAccumulator.Continue(angle, _step), notify: true);
	}

	private void EndPointerAdjustment()
	{
		_dragging = false;
		_dragAccumulator.End();
		_dragEventCamera = null;
		RestoreParentScroll();
	}

	private void LockParentScroll()
	{
		if (_lockedScrollRect != null)
		{
			return;
		}
		ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
		if (scrollRect == null)
		{
			return;
		}
		_lockedScrollRect = scrollRect;
		_lockedScrollHorizontal = scrollRect.horizontal;
		_lockedScrollVertical = scrollRect.vertical;
		scrollRect.StopMovement();
		scrollRect.horizontal = false;
		scrollRect.vertical = false;
	}

	private void RestoreParentScroll()
	{
		ScrollRect scrollRect = _lockedScrollRect;
		_lockedScrollRect = null;
		if (scrollRect != null)
		{
			scrollRect.horizontal = _lockedScrollHorizontal;
			scrollRect.vertical = _lockedScrollVertical;
		}
	}

	private bool TryGetPointerAngle(Vector2 screenPosition, Camera eventCamera, out float angle)
	{
		angle = 0f;
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
				rectTransform,
				screenPosition,
				eventCamera,
				out Vector2 point))
		{
			return false;
		}
		Vector2 delta = point - rectTransform.rect.center;
		angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
		if (angle < 0f)
		{
			angle += 360f;
		}
		return true;
	}

	private static float NormalizeTime(float value)
	{
		return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Repeat(value, 24f);
	}

	private static float Sanitize(float value, float fallback)
	{
		return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
	}
}
