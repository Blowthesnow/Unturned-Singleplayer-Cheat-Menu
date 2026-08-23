using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class WorkshopTranslationCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WorkshopTranslationCacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly string _path;
    private readonly Action<string> _warn;

    internal WorkshopTranslationCache(string path, Action<string> warn = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _warn = warn;
        Load();
    }

    internal string Path => _path;

    internal void Reload()
    {
        lock (_gate)
            _entries.Clear();
        Load();
    }

    internal int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    internal bool TryGet(string key, out WorkshopTranslationCacheEntry entry)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key ?? string.Empty, out WorkshopTranslationCacheEntry value)
                && !string.IsNullOrWhiteSpace(value.Translation))
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
            return _entries.Values.Select(Clone).ToArray();
    }

    internal void Upsert(WorkshopTranslationCacheEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Translation))
            return;

        lock (_gate)
        {
            if (_entries.TryGetValue(entry.Key, out WorkshopTranslationCacheEntry old)
                && entry.CreatedUtc == default)
                entry.CreatedUtc = old.CreatedUtc;
            if (entry.CreatedUtc == default)
                entry.CreatedUtc = DateTime.UtcNow;
            if (entry.UpdatedUtc == default)
                entry.UpdatedUtc = DateTime.UtcNow;
            _entries[entry.Key] = Clone(entry);
        }
    }

    internal bool Touch(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out WorkshopTranslationCacheEntry entry))
                return false;
            entry.LastUsedUtc = DateTime.UtcNow;
            return true;
        }
    }
    internal void Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        lock (_gate)
            _entries.Remove(key);
    }

    internal bool Save(string sourceLanguage, string targetLanguage)
    {
        WorkshopTranslationCacheDocument document;
        lock (_gate)
        {
            document = new WorkshopTranslationCacheDocument
            {
                SchemaVersion = WorkshopTextIdentity.SchemaVersion,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Entries = _entries.Values.Select(Clone).OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList()
            };
        }

        string directory = System.IO.Path.GetDirectoryName(_path);
        string temporaryPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        string backupPath = _path + ".bak";
        try
        {
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(document, Formatting.Indented);
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(_path))
                File.Replace(temporaryPath, _path, backupPath, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, _path);
            temporaryPath = null;
            return true;
        }
        catch (Exception ex)
        {
            _warn?.Invoke($"Workshop translation cache save failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        try
        {
            WorkshopTranslationCacheDocument document = JsonConvert.DeserializeObject<WorkshopTranslationCacheDocument>(File.ReadAllText(_path));
            if (document == null || document.Entries == null)
                throw new InvalidDataException("cache document is empty");

            lock (_gate)
            {
                _entries.Clear();
                foreach (WorkshopTranslationCacheEntry entry in document.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Translation))
                        continue;
                    _entries[entry.Key] = Clone(entry);
                }
            }
        }
        catch (Exception ex)
        {
            string corruptPath = _path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            try { File.Move(_path, corruptPath); } catch { }
            _warn?.Invoke($"Workshop translation cache was isolated after a read failure: {ex.Message}");
        }
    }

    private static WorkshopTranslationCacheEntry Clone(WorkshopTranslationCacheEntry source) => new()
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
        ApiProtocol = source.ApiProtocol,
        Provider = source.Provider,
        Model = source.Model,
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc,
        LastUsedUtc = source.LastUsedUtc
    };
}
