using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace UnturnedSingleplayerCheatMenu.AutoPickup;

internal sealed class AutoPickupRuleStore
{
	private readonly string _filePath;

	private readonly Action<string> _logInfo;

	private readonly Action<string> _logError;

	public AutoPickupRuleDocument Document { get; private set; } = new AutoPickupRuleDocument();

	public string FilePath => _filePath;

	public AutoPickupRuleStore(string filePath, Action<string> logInfo = null, Action<string> logError = null)
	{
		_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
		_logInfo = logInfo;
		_logError = logError;
	}

	public AutoPickupRuleLoadResult Load(ItemFilterMatcher matcher)
	{
		try
		{
			AutoPickupRuleDocument input = File.Exists(_filePath)
				? JsonConvert.DeserializeObject<AutoPickupRuleDocument>(File.ReadAllText(_filePath)) ?? new AutoPickupRuleDocument()
				: new AutoPickupRuleDocument();
			bool whitelistEnabled = input.Enabled && (input.Mode == AutoPickupFilterMode.WhitelistOnly || input.Mode == AutoPickupFilterMode.WhitelistThenBlacklist);
			AutoPickupFilterMode mode = whitelistEnabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly;
			AutoPickupRuleLoadResult result = matcher.Replace(whitelistEnabled, mode, input.Rules, out IReadOnlyList<ItemFilterRule> accepted);
			Document = new AutoPickupRuleDocument
			{
				Version = 2,
				Enabled = whitelistEnabled,
				Mode = mode,
				Rules = accepted.Select(rule => rule.Clone()).ToList()
			};
			_logInfo?.Invoke($"[AutoPickup] 已读取 {result.Accepted} 条过滤规则，跳过 {result.Rejected} 条无效或重复规则。文件={_filePath}");
			return result;
		}
		catch (Exception exception)
		{
			string error = exception.Message;
			_logError?.Invoke($"[AutoPickup] 读取规则失败，已保留原文件：{_filePath}\n{exception}");
			return new AutoPickupRuleLoadResult(0, 0, error);
		}
	}

	public AutoPickupRuleLoadResult ReplaceAndSave(bool enabled, AutoPickupFilterMode mode, IEnumerable<ItemFilterRule> rules, ItemFilterMatcher matcher)
	{
		bool whitelistEnabled = enabled && (mode == AutoPickupFilterMode.WhitelistOnly || mode == AutoPickupFilterMode.WhitelistThenBlacklist);
		AutoPickupFilterMode normalizedMode = whitelistEnabled ? AutoPickupFilterMode.WhitelistThenBlacklist : AutoPickupFilterMode.BlacklistOnly;
		AutoPickupRuleLoadResult result = matcher.Replace(whitelistEnabled, normalizedMode, rules, out IReadOnlyList<ItemFilterRule> accepted);
		Document = new AutoPickupRuleDocument
		{
			Version = 2,
			Enabled = whitelistEnabled,
			Mode = normalizedMode,
			Rules = accepted.Select(rule => rule.Clone()).ToList()
		};
		try
		{
			SaveAtomic(Document);
			_logInfo?.Invoke($"[AutoPickup] 已原子保存 {Document.Rules.Count} 条过滤规则。文件={_filePath}");
			return result;
		}
		catch (Exception exception)
		{
			_logError?.Invoke($"[AutoPickup] 保存规则失败：{_filePath}\n{exception}");
			return new AutoPickupRuleLoadResult(result.Accepted, result.Rejected, exception.Message);
		}
	}

	private void SaveAtomic(AutoPickupRuleDocument document)
	{
		string directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
		if (directory.Length > 0)
		{
			Directory.CreateDirectory(directory);
		}
		string tempPath = _filePath + ".tmp";
		string backupPath = _filePath + ".bak";
		File.WriteAllText(tempPath, JsonConvert.SerializeObject(document, Formatting.Indented));
		if (File.Exists(_filePath))
		{
			File.Replace(tempPath, _filePath, backupPath, ignoreMetadataErrors: true);
			if (File.Exists(backupPath))
			{
				File.Delete(backupPath);
			}
		}
		else
		{
			File.Move(tempPath, _filePath);
		}
	}
}
