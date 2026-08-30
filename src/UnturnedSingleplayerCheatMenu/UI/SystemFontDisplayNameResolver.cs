using System;
using System.Collections.Generic;

namespace UnturnedSingleplayerCheatMenu.UI;

internal static class SystemFontDisplayNameResolver
{
	internal static string Resolve(
		string familyName,
		IReadOnlyDictionary<string, string> localizedNames)
	{
		if (localizedNames != null &&
			localizedNames.TryGetValue(familyName, out string localizedName) &&
			!string.IsNullOrWhiteSpace(localizedName) &&
			!string.Equals(localizedName, familyName, StringComparison.OrdinalIgnoreCase))
		{
			return localizedName;
		}

		if (string.Equals(familyName, "Microsoft YaHei UI", StringComparison.OrdinalIgnoreCase))
		{
			return "微软雅黑 UI";
		}
		if (string.Equals(familyName, "Microsoft YaHei UI Light", StringComparison.OrdinalIgnoreCase))
		{
			return "微软雅黑 UI Light";
		}
		if (string.Equals(familyName, "SimSun-ExtB", StringComparison.OrdinalIgnoreCase))
		{
			return "宋体-ExtB";
		}
		if (string.Equals(familyName, "SimSun-ExtG", StringComparison.OrdinalIgnoreCase))
		{
			return "宋体-ExtG";
		}
		if (familyName.StartsWith("Source Han Sans SC", StringComparison.OrdinalIgnoreCase))
		{
			return "思源黑体" + familyName.Substring("Source Han Sans SC".Length);
		}
		if (familyName.StartsWith("Source Han Serif SC", StringComparison.OrdinalIgnoreCase))
		{
			return "思源宋体" + familyName.Substring("Source Han Serif SC".Length);
		}
		if (familyName.StartsWith("Source Han Serif CN", StringComparison.OrdinalIgnoreCase))
		{
			return "思源宋体 CN" + familyName.Substring("Source Han Serif CN".Length);
		}
		return familyName;
	}
}
