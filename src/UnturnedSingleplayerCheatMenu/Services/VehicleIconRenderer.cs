using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class VehicleIconRenderer
{
	private sealed class Request
	{
		internal VehicleAsset Asset { get; }

		internal VehicleThumbnailRenderSettings Settings { get; }

		internal Action<Texture2D> Callback { get; }

		internal Request(VehicleAsset asset, VehicleThumbnailRenderSettings settings, Action<Texture2D> callback)
		{
			Asset = asset;
			Settings = settings;
			Callback = callback;
		}
	}

	private const int PreviewLayer = 26;

	private const float PreviewAspect = 1.3333334f;

	private const float MaximumBoundsRadius = 6f;

	private const byte VisibleAlphaThreshold = 16;

	private const int MinimumVisiblePixelCount = 12;

	private readonly Queue<Request> _requests = new Queue<Request>();

	private readonly CheatMenuPlugin _plugin;

	internal VehicleIconRenderer(CheatMenuPlugin plugin)
	{
		_plugin = plugin;
	}

	internal bool TryEnqueue(VehicleAsset asset, VehicleThumbnailRenderSettings settings, Action<Texture2D> callback)
	{
		if (asset == null || settings == null || callback == null)
		{
			return false;
		}
		_plugin?.LogVehicleIconRequest(asset, "插件直接离屏渲染");
		_requests.Enqueue(new Request(asset, settings, callback));
		return true;
	}

	internal bool PumpOne()
	{
		if (_requests.Count == 0)
		{
			return false;
		}
		Request request = _requests.Dequeue();
		Texture2D texture2D = null;
		try
		{
			texture2D = Render(request.Asset, request.Settings);
		}
		catch (Exception exception)
		{
			_plugin?.LogVehicleIconFailure(request.Asset, exception);
		}
		_plugin?.LogVehicleIconResult(request.Asset, texture2D, "插件直接离屏渲染");
		request.Callback(texture2D);
		return true;
	}

	internal void CancelPending()
	{
		while (_requests.Count > 0)
		{
			_requests.Dequeue().Callback(null);
		}
	}

	private Texture2D Render(VehicleAsset asset, VehicleThumbnailRenderSettings settings)
	{
		Texture2D texture2D = RenderAttempt(asset, settings, forceGeneratedCamera: false, out var usedGeneratedCamera);
		if (!usedGeneratedCamera)
		{
			return texture2D;
		}
		if (TryFinalizeTexture(texture2D))
		{
			return texture2D;
		}
		DestroyTexture(texture2D);
		_plugin?.LogVehicleIconFallback(asset, "透明图检测后的自动取景重拍");
		texture2D = RenderAttempt(asset, settings, forceGeneratedCamera: true, out var _);
		if (TryFinalizeTexture(texture2D))
		{
			return texture2D;
		}
		DestroyTexture(texture2D);
		return null;
	}

	private Texture2D RenderAttempt(VehicleAsset asset, VehicleThumbnailRenderSettings settings, bool forceGeneratedCamera, out bool usedGeneratedCamera)
	{
		usedGeneratedCamera = false;
		Transform transform = VehicleTool.getVehicle(asset.id, 0, 0, asset, null);
		if (transform == null)
		{
			return null;
		}
		try
		{
			transform.position = new Vector3(-256f, -256f, 0f);
			Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
			SetPreviewLayers(componentsInChildren);
			Transform transform2 = (forceGeneratedCamera ? null : transform.Find("Icon2"));
			float orthoSize = asset.size2_z;
			if (transform2 == null)
			{
				transform2 = (forceGeneratedCamera ? null : transform.Find("Icon"));
				if (transform2 == null)
				{
					transform2 = CreateGeneratedIconTransform(transform, componentsInChildren, settings, out orthoSize);
					usedGeneratedCamera = true;
					if (!forceGeneratedCamera)
					{
						_plugin?.LogVehicleIconFallback(asset, "缺少可用 Icon2 时的自动取景");
					}
				}
				else
				{
					orthoSize = CalculateOrthoSize(transform2, GetRenderableBounds(componentsInChildren), 1f);
					_plugin?.LogVehicleIconFallback(asset, "旧版 Icon 取景");
				}
			}
			else if (!IsUsableSize(orthoSize))
			{
				orthoSize = CalculateOrthoSize(transform2, GetRenderableBounds(componentsInChildren), 1f);
			}
			if (usedGeneratedCamera)
			{
				ForceHighestLod(transform);
			}
			Texture2D result = ItemTool.captureIcon(asset.id, 0, transform, transform2, settings.Width, settings.Height, Mathf.Max(0.25f, orthoSize), readableOnCPU: true);
			transform = null;
			return result;
		}
		finally
		{
			if (transform != null)
			{
				UnityEngine.Object.Destroy(transform.gameObject);
			}
		}
	}

	private static bool TryFinalizeTexture(Texture2D texture)
	{
		if (texture == null)
		{
			return false;
		}
		Color32[] pixels = texture.GetPixels32();
		int num = 0;
		Color32[] array = pixels;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].a >= 16)
			{
				num++;
				if (num >= 12)
				{
					texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
					return true;
				}
			}
		}
		return false;
	}

	private static void DestroyTexture(Texture2D texture)
	{
		if (texture != null)
		{
			UnityEngine.Object.Destroy(texture);
		}
	}

	private static bool IsUsableSize(float value)
	{
		if (value > 0.01f && !float.IsNaN(value))
		{
			return !float.IsInfinity(value);
		}
		return false;
	}

	private static void ForceHighestLod(Transform model)
	{
		LODGroup[] componentsInChildren = model.GetComponentsInChildren<LODGroup>(includeInactive: true);
		foreach (LODGroup lODGroup in componentsInChildren)
		{
			if (!(lODGroup == null))
			{
				lODGroup.enabled = true;
				lODGroup.ForceLOD(0);
			}
		}
	}

	private static Transform CreateGeneratedIconTransform(Transform model, Renderer[] renderers, VehicleThumbnailRenderSettings settings, out float orthoSize)
	{
		Bounds renderableBounds = GetRenderableBounds(renderers);
		float magnitude = renderableBounds.extents.magnitude;
		if (magnitude > 6f)
		{
			float num = 6f / magnitude;
			model.localScale = Vector3.Scale(model.localScale, new Vector3(num, num, num));
			renderableBounds = GetRenderableBounds(renderers);
			magnitude = renderableBounds.extents.magnitude;
		}
		Transform transform = new GameObject("CodexGeneratedVehicleIcon").transform;
		transform.SetParent(model, worldPositionStays: true);
		Vector3 normalized = new Vector3(1f, 0.65f, 1f).normalized;
		float num2 = Mathf.Clamp(magnitude + 1.5f, 2.5f, 7.5f);
		transform.position = renderableBounds.center + normalized * num2;
		transform.rotation = Quaternion.LookRotation(renderableBounds.center - transform.position, Vector3.up);
		orthoSize = CalculateOrthoSize(transform, renderableBounds, settings.Framing);
		return transform;
	}

	private static Bounds GetRenderableBounds(Renderer[] renderers)
	{
		bool flag = false;
		Bounds result = new Bounds(Vector3.zero, Vector3.one);
		foreach (Renderer renderer in renderers)
		{
			if (!(renderer == null) && renderer.enabled && renderer.gameObject.activeInHierarchy && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
			{
				if (!flag)
				{
					result = renderer.bounds;
					flag = true;
				}
				else
				{
					result.Encapsulate(renderer.bounds);
				}
			}
		}
		if (!flag)
		{
			result = new Bounds(Vector3.zero, Vector3.one * 2f);
		}
		return result;
	}

	private static float CalculateOrthoSize(Transform cameraTransform, Bounds bounds, float framing)
	{
		Vector3 center = bounds.center;
		Vector3 extents = bounds.extents;
		float num = 0f;
		float a = 0f;
		for (int i = -1; i <= 1; i += 2)
		{
			for (int j = -1; j <= 1; j += 2)
			{
				for (int k = -1; k <= 1; k += 2)
				{
					Vector3 position = center + Vector3.Scale(extents, new Vector3(i, j, k));
					Vector3 vector = cameraTransform.InverseTransformPoint(position);
					num = Mathf.Max(num, Mathf.Abs(vector.x));
					a = Mathf.Max(a, Mathf.Abs(vector.y));
				}
			}
		}
		float num2 = Mathf.Max(0.01f, Mathf.Max(a, num / 1.3333334f));
		return Mathf.Max(0.25f, num2 * framing);
	}

	private static void SetPreviewLayers(Renderer[] renderers)
	{
		foreach (Renderer renderer in renderers)
		{
			if (renderer != null)
			{
				renderer.gameObject.layer = 26;
			}
		}
	}
}
