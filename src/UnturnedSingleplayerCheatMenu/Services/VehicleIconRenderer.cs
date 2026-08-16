using System;
using System.Collections.Generic;
using SDG.Unturned;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class VehicleIconRenderer
{
    private const int PreviewLayer = 26;
    private const float PreviewAspect = 128f / 96f;
    private const float MaximumBoundsRadius = 6f;
    private const byte VisibleAlphaThreshold = 16;
    private const int MinimumVisiblePixelCount = 12;

    private readonly Queue<Request> _requests = new();
    private readonly CheatMenuPlugin _plugin;

    internal VehicleIconRenderer(CheatMenuPlugin plugin)
    {
        _plugin = plugin;
    }

    internal bool TryEnqueue(VehicleAsset asset, Action<Texture2D> callback)
    {
        if (asset == null || callback == null)
            return false;

        // VehicleTool.getIcon only enqueues work for VehicleTool.Update. That
        // component is not guaranteed to survive Unturned's bootstrap scene
        // transition, which previously left every card as a white placeholder.
        // Own the queue here and capture one vehicle per reliable Harmony frame.
        _plugin?.LogVehicleIconRequest(asset, "插件直接离屏渲染");
        _requests.Enqueue(new Request(asset, callback));
        return true;
    }

    internal void PumpOne()
    {
        if (_requests.Count == 0)
            return;

        Request request = _requests.Dequeue();
        Texture2D texture = null;
        try
        {
            texture = Render(request.Asset);
        }
        catch (Exception exception)
        {
            _plugin?.LogVehicleIconFailure(request.Asset, exception);
        }

        _plugin?.LogVehicleIconResult(request.Asset, texture, "插件直接离屏渲染");
        request.Callback(texture);
    }

    internal void CancelPending()
    {
        while (_requests.Count > 0)
            _requests.Dequeue().Callback(null);
    }

    private Texture2D Render(VehicleAsset asset)
    {
        Texture2D texture = RenderAttempt(asset, forceGeneratedCamera: false, out bool usedGeneratedCamera);
        if (TryFinalizeTexture(texture))
            return texture;

        DestroyTexture(texture);
        if (usedGeneratedCamera)
            return null;

        _plugin?.LogVehicleIconFallback(asset, "透明图检测后的自动取景重拍");
        texture = RenderAttempt(asset, forceGeneratedCamera: true, out _);
        if (TryFinalizeTexture(texture))
            return texture;

        DestroyTexture(texture);
        return null;
    }

    private Texture2D RenderAttempt(
        VehicleAsset asset,
        bool forceGeneratedCamera,
        out bool usedGeneratedCamera)
    {
        usedGeneratedCamera = false;
        Transform model = VehicleTool.getVehicle(asset.id, 0, 0, asset, null);
        if (model == null)
            return null;

        try
        {
            model.position = new Vector3(-256f, -256f, 0f);
            ForceHighestLod(model);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            SetPreviewLayers(renderers);

            Transform icon = forceGeneratedCamera ? null : model.Find("Icon2");
            float orthoSize = asset.size2_z;
            if (icon == null)
            {
                icon = forceGeneratedCamera ? null : model.Find("Icon");
                if (icon == null)
                {
                    icon = CreateGeneratedIconTransform(model, renderers, out orthoSize);
                    usedGeneratedCamera = true;
                    if (!forceGeneratedCamera)
                        _plugin?.LogVehicleIconFallback(asset, "缺少可用 Icon2 时的自动取景");
                }
                else
                {
                    orthoSize = CalculateOrthoSize(icon, GetRenderableBounds(renderers));
                    _plugin?.LogVehicleIconFallback(asset, "旧版 Icon 取景");
                }
            }
            else if (!IsUsableSize(orthoSize))
            {
                orthoSize = CalculateOrthoSize(icon, GetRenderableBounds(renderers));
            }

            Texture2D texture = ItemTool.captureIcon(
                asset.id,
                0,
                model,
                icon,
                128,
                96,
                Mathf.Max(0.25f, orthoSize),
                true);

            // ItemTool.captureIcon owns and destroys the temporary model.
            model = null;
            return texture;
        }
        finally
        {
            if (model != null)
                UnityEngine.Object.Destroy(model.gameObject);
        }
    }

    private static bool TryFinalizeTexture(Texture2D texture)
    {
        if (texture == null)
            return false;

        Color32[] pixels = texture.GetPixels32();
        int visiblePixels = 0;
        foreach (Color32 pixel in pixels)
        {
            if (pixel.a < VisibleAlphaThreshold)
                continue;

            visiblePixels++;
            if (visiblePixels >= MinimumVisiblePixelCount)
            {
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                return true;
            }
        }

        return false;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
    }

    private static bool IsUsableSize(float value)
    {
        return value > 0.01f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void ForceHighestLod(Transform model)
    {
        foreach (LODGroup lodGroup in model.GetComponentsInChildren<LODGroup>(true))
        {
            if (lodGroup == null)
                continue;

            lodGroup.enabled = true;
            lodGroup.ForceLOD(0);
        }
    }

    private static Transform CreateGeneratedIconTransform(
        Transform model,
        Renderer[] renderers,
        out float orthoSize)
    {
        Bounds bounds = GetRenderableBounds(renderers);
        float radius = bounds.extents.magnitude;
        if (radius > MaximumBoundsRadius)
        {
            float scale = MaximumBoundsRadius / radius;
            model.localScale = Vector3.Scale(model.localScale, new Vector3(scale, scale, scale));
            bounds = GetRenderableBounds(renderers);
            radius = bounds.extents.magnitude;
        }

        GameObject iconObject = new("CodexGeneratedVehicleIcon");
        Transform icon = iconObject.transform;
        icon.SetParent(model, true);

        Vector3 cameraOffset = new Vector3(1f, 0.65f, 1f).normalized;
        float distance = Mathf.Clamp(radius + 1.5f, 2.5f, 7.5f);
        icon.position = bounds.center + cameraOffset * distance;
        icon.rotation = Quaternion.LookRotation(bounds.center - icon.position, Vector3.up);
        orthoSize = CalculateOrthoSize(icon, bounds);
        return icon;
    }

    private static Bounds GetRenderableBounds(Renderer[] renderers)
    {
        bool initialized = false;
        Bounds bounds = new(Vector3.zero, Vector3.one);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null
                || !renderer.enabled
                || !renderer.gameObject.activeInHierarchy
                || (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!initialized)
            bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        return bounds;
    }

    private static float CalculateOrthoSize(Transform cameraTransform, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float halfWidth = 0f;
        float halfHeight = 0f;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
            Vector3 local = cameraTransform.InverseTransformPoint(corner);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
        }

        return Mathf.Max(halfHeight, halfWidth / PreviewAspect) * 1.12f;
    }

    private static void SetPreviewLayers(Renderer[] renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
                renderer.gameObject.layer = PreviewLayer;
        }
    }

    private sealed class Request
    {
        internal Request(VehicleAsset asset, Action<Texture2D> callback)
        {
            Asset = asset;
            Callback = callback;
        }

        internal VehicleAsset Asset { get; }
        internal Action<Texture2D> Callback { get; }
    }
}
