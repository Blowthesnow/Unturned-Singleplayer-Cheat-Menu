using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopTranslationCache
{
	private readonly object _gate = new object();

	private readonly Dictionary<string, WorkshopTranslationCacheEntry> _entries = new Dictionary<string, WorkshopTranslationCacheEntry>(StringComparer.Ordinal);

	private readonly string _path;

	private readonly Action<string> _warn;

	internal string Path => _path;

	internal int Count
	{
		get
		{
			lock (_gate)
			{
				return _entries.Count;
			}
		}
	}

	internal WorkshopTranslationCache(string path, Action<string> warn = null)
	{
		_path = path ?? throw new ArgumentNullException("path");
		_warn = warn;
		Load();
	}

	internal void Reload()
	{
		lock (_gate)
		{
			_entries.Clear();
		}
		Load();
	}

	internal bool TryGet(string key, out WorkshopTranslationCacheEntry entry)
	{
		lock (_gate)
		{
			if (_entries.TryGetValue(key ?? string.Empty, out var value) && !string.IsNullOrWhiteSpace(value.Translation))
			{
				entry = Clone(value);
				return true;
			}
		}
		entry = null;
		return false;
	}

	internal IReadOnlyList<WorkshopTranslationCacheEntry> GetEntries()
	{
		lock (_gate)
		{
			return _entries.Values.Select(Clone).ToArray();
		}
	}

	internal void Upsert(WorkshopTranslationCacheEntry entry)
	{
		if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Translation))
		{
			return;
		}
		lock (_gate)
		{
			if (_entries.TryGetValue(entry.Key, out var value) && entry.CreatedUtc == default(DateTime))
			{
				entry.CreatedUtc = value.CreatedUtc;
			}
			if (entry.CreatedUtc == default(DateTime))
			{
				entry.CreatedUtc = DateTime.UtcNow;
			}
			if (entry.UpdatedUtc == default(DateTime))
			{
				entry.UpdatedUtc = DateTime.UtcNow;
			}
			_entries[entry.Key] = Clone(entry);
		}
	}

	internal bool Touch(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}
		lock (_gate)
		{
			if (!_entries.TryGetValue(key, out var value))
			{
				return false;
			}
			value.LastUsedUtc = DateTime.UtcNow;
			return true;
		}
	}

	internal void Remove(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return;
		}
		lock (_gate)
		{
			_entries.Remove(key);
		}
	}

	internal bool Save(string sourceLanguage, string targetLanguage)
	{
		WorkshopTranslationCacheDocument value;
		lock (_gate)
		{
			value = new WorkshopTranslationCacheDocument
			{
				SchemaVersion = WorkshopTextIdentity.AssetScopedSchemaVersion,
				SourceLanguage = sourceLanguage,
				TargetLanguage = targetLanguage,
				Entries = _entries.Values.Select(Clone).OrderBy<WorkshopTranslationCacheEntry, string>((WorkshopTranslationCacheEntry entry) => entry.Key, StringComparer.Ordinal).ToList()
			};
		}
		string directoryName = System.IO.Path.GetDirectoryName(_path);
		string text = _path + ".tmp-" + Guid.NewGuid().ToString("N");
		string destinationBackupFileName = _path + ".bak";
		bool result;
		try
		{
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string s = JsonConvert.SerializeObject(value, Formatting.Indented);
			byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(s);
			using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
			{
				fileStream.Write(bytes, 0, bytes.Length);
				fileStream.Flush(flushToDisk: true);
			}
			if (File.Exists(_path))
			{
				File.Replace(text, _path, destinationBackupFileName, ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(text, _path);
			}
			text = null;
			result = true;
		}
		catch (Exception ex)
		{
			_warn?.Invoke("Workshop translation cache save failed: " + ex.Message);
			result = false;
		}
		finally
		{
			if (!string.IsNullOrEmpty(text))
			{
				try
				{
					File.Delete(text);
				}
				catch
				{
				}
			}
		}
		return result;
	}

	private void Load()
	{
		if (!File.Exists(_path))
		{
			return;
		}
		try
		{
			WorkshopTranslationCacheDocument workshopTranslationCacheDocument = JsonConvert.DeserializeObject<WorkshopTranslationCacheDocument>(File.ReadAllText(_path));
			if (workshopTranslationCacheDocument == null || workshopTranslationCacheDocument.Entries == null)
			{
				throw new InvalidDataException("cache document is empty");
			}
			lock (_gate)
			{
				_entries.Clear();
				foreach (WorkshopTranslationCacheEntry entry in workshopTranslationCacheDocument.Entries)
				{
					if (entry != null && !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Translation))
					{
						_entries[entry.Key] = Clone(entry);
					}
				}
			}
		}
		catch (Exception ex)
		{
			string destFileName = _path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
			try
			{
				File.Move(_path, destFileName);
			}
			catch
			{
			}
			_warn?.Invoke("Workshop translation cache was isolated after a read failure: " + ex.Message);
		}
	}

	private static WorkshopTranslationCacheEntry Clone(WorkshopTranslationCacheEntry source)
	{
		return new WorkshopTranslationCacheEntry
		{
			Key = source.Key,
			SchemaVersion = source.SchemaVersion,
			SourceLanguage = source.SourceLanguage,
			TargetLanguage = source.TargetLanguage,
			Field = source.Field,
			SourceText = source.SourceText,
			Translation = source.Translation,
			AssetName = source.AssetName,
			AssetGuid = source.AssetGuid,
			LegacyId = source.LegacyId,
			ActionKey = source.ActionKey,
			AssetType = source.AssetType,
			LocalizationKey = source.LocalizationKey,
			ApiProtocol = source.ApiProtocol,
			Provider = source.Provider,
			Model = source.Model,
			CreatedUtc = source.CreatedUtc,
			UpdatedUtc = source.UpdatedUtc,
			LastUsedUtc = source.LastUsedUtc
		};
	}
}
