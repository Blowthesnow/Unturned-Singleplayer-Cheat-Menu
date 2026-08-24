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
			{
				return null;
			}
			byte[] bytes = File.ReadAllBytes(path);
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (!TryLoadImage(texture2D, bytes) || texture2D.width != settings.Width || texture2D.height != settings.Height)
			{
				UnityEngine.Object.Destroy(texture2D);
				DeleteCorruptFile(path);
				return null;
			}
			texture2D.name = $"VehicleIcon_{vehicleGuid:N}_{settings.Width}x{settings.Height}";
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			return texture2D;
		}
		catch (Exception)
		{
			DeleteCorruptFile(path);
			return null;
		}
	}

	internal bool TrySave(Guid vehicleGuid, VehicleThumbnailRenderSettings settings, Texture2D texture, out string failure)
	{
		failure = null;
		if (texture == null || texture.width != settings.Width || texture.height != settings.Height)
		{
			failure = $"纹理尺寸不匹配：实际 {texture?.width ?? 0}x{texture?.height ?? 0}，" + $"预期 {settings.Width}x{settings.Height}。";
			return false;
		}
		string text = null;
		try
		{
			byte[] array = TryEncodeToPng(texture);
			if (array == null || array.Length == 0)
			{
				failure = "UnityEngine.ImageConversion.EncodeToPNG 未返回有效 PNG。";
				return false;
			}
			Directory.CreateDirectory(_directory);
			string path = GetPath(vehicleGuid, settings);
			text = path + ".tmp-" + Guid.NewGuid().ToString("N");
			File.WriteAllBytes(text, array);
			if (File.Exists(path))
			{
				File.Replace(text, path, null);
			}
			else
			{
				File.Move(text, path);
			}
			text = null;
			if (!File.Exists(path))
			{
				failure = "写入操作完成但目标文件不存在：" + path;
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			failure = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
		finally
		{
			if (text != null)
			{
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch
				{
				}
			}
		}
	}

	internal string GetPath(Guid vehicleGuid, VehicleThumbnailRenderSettings settings)
	{
		string path = $"{vehicleGuid:N}_{settings.CacheFormatVersion}_{settings.Width}x{settings.Height}_f{settings.GetFramingMilli()}.png";
		return Path.Combine(_directory, path);
	}

	private static void DeleteCorruptFile(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static bool TryLoadImage(Texture2D texture, byte[] bytes)
	{
		MethodInfo methodInfo = ResolveImageConversionMethod("LoadImage", typeof(bool), typeof(Texture2D), typeof(byte[]), typeof(bool));
		if (methodInfo == null)
		{
			return false;
		}
		try
		{
			return (bool)methodInfo.Invoke(null, new object[3] { texture, bytes, true });
		}
		catch
		{
			return false;
		}
	}

	private static byte[] TryEncodeToPng(Texture2D texture)
	{
		MethodInfo methodInfo = ResolveImageConversionMethod("EncodeToPNG", typeof(byte[]), typeof(Texture2D));
		if (methodInfo == null)
		{
			return null;
		}
		try
		{
			return methodInfo.Invoke(null, new object[1] { texture }) as byte[];
		}
		catch
		{
			return null;
		}
	}

	private static MethodInfo ResolveImageConversionMethod(string name, Type returnType, params Type[] parameterTypes)
	{
		Type type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule") ?? typeof(Texture2D).Assembly.GetType("UnityEngine.ImageConversion");
		if (type == null)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				type = assemblies[i].GetType("UnityEngine.ImageConversion");
				if (type != null)
				{
					break;
				}
			}
		}
		MethodInfo methodInfo = type?.GetMethod(name, BindingFlags.Static | BindingFlags.Public, null, parameterTypes, null);
		if (!(methodInfo != null) || !(methodInfo.ReturnType == returnType))
		{
			return null;
		}
		return methodInfo;
	}
}
