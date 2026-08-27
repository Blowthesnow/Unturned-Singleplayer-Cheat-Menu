using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnturnedSingleplayerCheatMenu.InfiniteStorage;

internal sealed class VirtualStoragePersistence
{
	private readonly string _path;

	internal VirtualStoragePersistence(string path)
	{
		_path = path;
	}

	internal bool TryLoad(out VirtualStorageDocument document, out string error)
	{
		document = new VirtualStorageDocument();
		error = string.Empty;
		if (!File.Exists(_path))
		{
			return true;
		}
		if (TryRead(_path, out document, out error))
		{
			return true;
		}
		string primaryError = error;
		if (TryRead(_path + ".bak", out document, out error))
		{
			return true;
		}
		error = "主存档和备份存档均无法读取。主存档：" + primaryError + "；备份：" + error;
		return false;
	}

	internal bool TrySave(VirtualStorageDocument document, out string error)
	{
		error = string.Empty;
		string directory = Path.GetDirectoryName(_path);
		if (string.IsNullOrEmpty(directory))
		{
			error = "仓库存档路径没有目录。";
			return false;
		}
		try
		{
			Directory.CreateDirectory(directory);
			string temporaryPath = _path + ".tmp";
			File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(document, Formatting.Indented));
			if (File.Exists(_path))
			{
				File.Replace(temporaryPath, _path, _path + ".bak", ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(temporaryPath, _path);
			}
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}

	internal bool TryExportLegacyAmbiguousEntries(IReadOnlyList<LegacyAmbiguousEntry> entries, out string exportPath, out string error)
	{
		exportPath = string.Empty;
		error = string.Empty;
		if (entries == null || entries.Count == 0)
		{
			error = "No ambiguous legacy entries are available to export.";
			return false;
		}
		try
		{
			string directory = Path.GetDirectoryName(_path);
			if (string.IsNullOrEmpty(directory))
			{
				error = "The storage save path has no directory.";
				return false;
			}
			Directory.CreateDirectory(directory);
			exportPath = Path.Combine(directory, "legacy-ambiguous-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
			File.WriteAllText(exportPath, JsonConvert.SerializeObject(new { ExportedUtc = DateTime.UtcNow, LegacyAmbiguousEntries = entries }, Formatting.Indented));
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			exportPath = string.Empty;
			return false;
		}
	}

	private static bool TryRead(string path, out VirtualStorageDocument document, out string error)
	{
		document = null;
		error = string.Empty;
		try
		{
			document = JsonConvert.DeserializeObject<VirtualStorageDocument>(File.ReadAllText(path));
			if (document == null || (document.SchemaVersion != 1 && document.SchemaVersion != VirtualStorageDocument.CurrentSchemaVersion))
			{
				error = "存档 schema 不受支持。";
				return false;
			}
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}
}
