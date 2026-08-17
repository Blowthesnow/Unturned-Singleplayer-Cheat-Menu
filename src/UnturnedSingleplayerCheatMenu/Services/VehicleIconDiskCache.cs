using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class VehicleIconDiskCache
{
    private readonly string _directory;

    internal VehicleIconDiskCache()
    {
        _directory = Path.Combine(Paths.CachePath, "UnturnedSingleplayerCheatMenu", "VehicleIcons");
    }

    internal bool HasEntry(Guid vehicleGuid, VehicleThumbnailRenderSettings settings)
    {
        return File.Exists(GetPath(vehicleGuid, settings));
    }

    internal Texture2D TryLoad(Guid vehicleGuid, VehicleThumbnailRenderSettings settings)
    {
        string path = GetPath(vehicleGuid, settings);
        try
        {
            if (!File.Exists(path))
                return null;

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            if (!TryLoadImage(texture, bytes)
                || texture.width != settings.Width
                || texture.height != settings.Height)
            {
                UnityEngine.Object.Destroy(texture);
                DeleteCorruptFile(path);
                return null;
            }

            texture.name = $"VehicleIcon_{vehicleGuid:N}_{settings.Width}x{settings.Height}";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
        catch (Exception)
        {
            DeleteCorruptFile(path);
            return null;
        }
    }

    internal bool TrySave(
        Guid vehicleGuid,
        VehicleThumbnailRenderSettings settings,
        Texture2D texture,
        out string failure)
    {
        failure = null;
        if (texture == null || texture.width != settings.Width || texture.height != settings.Height)
        {
            failure =
                $"纹理尺寸不匹配：实际 {texture?.width ?? 0}x{texture?.height ?? 0}，" +
                $"预期 {settings.Width}x{settings.Height}。";
            return false;
        }

        string temporaryPath = null;
        try
        {
            byte[] png = TryEncodeToPng(texture);
            if (png == null || png.Length == 0)
            {
                failure = "UnityEngine.ImageConversion.EncodeToPNG 未返回有效 PNG。";
                return false;
            }

            Directory.CreateDirectory(_directory);
            string path = GetPath(vehicleGuid, settings);
            temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporaryPath, png);

            if (File.Exists(path))
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, path);
            temporaryPath = null;

            if (!File.Exists(path))
            {
                failure = $"写入操作完成但目标文件不存在：{path}";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            failure = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
        finally
        {
            if (temporaryPath != null)
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // A failed cache cleanup must not affect the plugin.
                }
            }
        }
    }

    internal string GetPath(Guid vehicleGuid, VehicleThumbnailRenderSettings settings)
    {
        string fileName =
            $"{vehicleGuid:N}_{settings.CacheFormatVersion}_{settings.Width}x{settings.Height}_f{settings.GetFramingMilli()}.png";
        return Path.Combine(_directory, fileName);
    }

    private static void DeleteCorruptFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A failed cleanup is recoverable on the next lookup.
        }
    }

    private static bool TryLoadImage(Texture2D texture, byte[] bytes)
    {
        MethodInfo method = ResolveImageConversionMethod(
            "LoadImage",
            typeof(bool),
            typeof(Texture2D),
            typeof(byte[]),
            typeof(bool));
        if (method == null)
            return false;

        try
        {
            return (bool)method.Invoke(null, new object[] { texture, bytes, true });
        }
        catch
        {
            return false;
        }
    }

    private static byte[] TryEncodeToPng(Texture2D texture)
    {
        MethodInfo method = ResolveImageConversionMethod(
            "EncodeToPNG",
            typeof(byte[]),
            typeof(Texture2D));
        if (method == null)
            return null;

        try
        {
            return method.Invoke(null, new object[] { texture }) as byte[];
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo ResolveImageConversionMethod(
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        Type imageConversion = Type.GetType(
            "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
            ?? typeof(Texture2D).Assembly.GetType("UnityEngine.ImageConversion");
        if (imageConversion == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                imageConversion = assembly.GetType("UnityEngine.ImageConversion");
                if (imageConversion != null)
                    break;
            }
        }

        MethodInfo method = imageConversion?.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        return method != null && method.ReturnType == returnType ? method : null;
    }
}
