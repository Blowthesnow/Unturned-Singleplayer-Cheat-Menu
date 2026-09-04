using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnturnedSingleplayerCheatMenu.UI.ControlLibrary.Runtime;

/// <summary>Rounded border/fill pair which never owns or destroys its host.</summary>
internal sealed class LegacyFluentSurface : IDisposable
{
	private const string BorderName = "__FluentSurfaceBorder";
	private const string FillName = "__FluentSurfaceFill";
	private FluentRoundedGraphic _border;
	private FluentRoundedGraphic _fill;
	private bool _disposed;

	private LegacyFluentSurface(FluentRoundedGraphic border, FluentRoundedGraphic fill)
	{
		_border = border;
		_fill = fill;
	}

	internal Color CurrentFill { get; private set; }
	internal Color CurrentBorder { get; private set; }

	internal static LegacyFluentSurface Attach(GameObject host, Color fill, Color border, float radius, float borderThickness = 1f)
	{
		if (host == null)
		{
			throw new ArgumentNullException(nameof(host));
		}

		Transform borderTransform = host.transform.Find(BorderName);
		GameObject borderObject = borderTransform != null ? borderTransform.gameObject : CreateGraphicObject(BorderName, host.transform);
		FluentRoundedGraphic borderGraphic = borderObject.GetComponent<FluentRoundedGraphic>();
		borderGraphic.Radius = radius;
		borderGraphic.color = Color.white;
		borderGraphic.canvasRenderer.SetColor(border);
		borderGraphic.raycastTarget = false;
		borderObject.transform.SetAsFirstSibling();

		Transform fillTransform = borderObject.transform.Find(FillName);
		GameObject fillObject = fillTransform != null ? fillTransform.gameObject : CreateGraphicObject(FillName, borderObject.transform);
		FluentRoundedGraphic fillGraphic = fillObject.GetComponent<FluentRoundedGraphic>();
		fillGraphic.Radius = Mathf.Max(0f, radius - borderThickness);
		fillGraphic.color = Color.white;
		fillGraphic.canvasRenderer.SetColor(fill);
		fillGraphic.raycastTarget = false;
		SetOffsets(fillObject.GetComponent<RectTransform>(), borderThickness);

		return new LegacyFluentSurface(borderGraphic, fillGraphic)
		{
			CurrentFill = fill,
			CurrentBorder = border
		};
	}

	internal void SetColors(Color fill, Color border)
	{
		if (_disposed)
		{
			return;
		}
		CurrentFill = fill;
		CurrentBorder = border;
		if (_fill != null)
		{
			_fill.canvasRenderer.SetColor(fill);
		}
		if (_border != null)
		{
			_border.canvasRenderer.SetColor(border);
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_border = null;
		_fill = null;
	}

	private static GameObject CreateGraphicObject(string name, Transform parent)
	{
		GameObject result = new GameObject(name, typeof(RectTransform), typeof(FluentRoundedGraphic), typeof(LayoutElement));
		result.transform.SetParent(parent, false);
		RectTransform rect = result.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		result.GetComponent<LayoutElement>().ignoreLayout = true;
		return result;
	}

	private static void SetOffsets(RectTransform rect, float inset)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = new Vector2(inset, inset);
		rect.offsetMax = new Vector2(-inset, -inset);
	}
}
