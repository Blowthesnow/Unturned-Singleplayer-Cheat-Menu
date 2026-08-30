using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class ConfigFileSnapshot
{
	private readonly byte[] _originalBytes;
	private readonly bool _originalFileExists;

	private ConfigFileSnapshot(string path, bool originalFileExists, byte[] originalBytes)
	{
		Path = path;
		_originalFileExists = originalFileExists;
		_originalBytes = originalBytes;
	}

	internal string Path { get; }
	internal bool OriginalFileExists => _originalFileExists;

	internal static ConfigFileSnapshot Capture(ConfigFile config)
	{
		if (config == null)
		{
			throw new ArgumentNullException(nameof(config));
		}
		string path = config.ConfigFilePath;
		bool exists = File.Exists(path);
		return new ConfigFileSnapshot(path, exists, exists ? File.ReadAllBytes(path) : Array.Empty<byte>());
	}

	internal void Restore()
	{
		if (string.IsNullOrEmpty(Path))
		{
			return;
		}
		if (!_originalFileExists)
		{
			if (File.Exists(Path))
			{
				File.Delete(Path);
			}
			return;
		}
		File.WriteAllBytes(Path, _originalBytes);
	}
}

/// <summary>
/// Makes the complete BepInEx binding session atomic without retaining any retired key knowledge.
/// </summary>
internal static class PluginConfigInitializationTransaction
{
	internal static void Run(
		ConfigFile config,
		ManualLogSource log,
		Action initialize,
		Action<ConfigFile> save = null)
	{
		if (config == null)
		{
			throw new ArgumentNullException(nameof(config));
		}
		if (initialize == null)
		{
			throw new ArgumentNullException(nameof(initialize));
		}

		bool originalSaveOnConfigSet = config.SaveOnConfigSet;
		ConfigFileSnapshot snapshot = null;
		config.SaveOnConfigSet = false;
		try
		{
			snapshot = ConfigFileSnapshot.Capture(config);
			initialize();
			RemoveUnboundPluginEntries(config);
			(save ?? (file => file.Save()))(config);
		}
		catch (Exception exception)
		{
			if (snapshot != null)
			{
				try
				{
					config.Clear();
					snapshot.Restore();
					if (snapshot.OriginalFileExists)
					{
						config.Reload();
					}
				}
				catch (Exception restoreException)
				{
					log?.LogError("配置初始化事务失败，且原文件恢复失败。\n" + restoreException);
					throw new AggregateException("Configuration initialization and rollback both failed.", exception, restoreException);
				}
			}
			log?.LogError("配置初始化事务失败；已恢复启动前配置，后续绑定已中止。\n" + exception);
			throw;
		}
		finally
		{
			config.SaveOnConfigSet = originalSaveOnConfigSet;
		}
	}

	private static void RemoveUnboundPluginEntries(ConfigFile config)
	{
		PropertyInfo property = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic);
		if (property?.GetValue(config, null) is not IDictionary entries)
		{
			throw new MissingMemberException(typeof(ConfigFile).FullName, "OrphanedEntries");
		}
		entries.Clear();
	}
}
