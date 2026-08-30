using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;

namespace UnturnedSingleplayerCheatMenu.UI;

/// <summary>
/// Owns the plugin's legacy uGUI dynamic font. The selected Windows font is used directly by
/// UnityEngine.UI.Text; it is never converted to, or injected into, a TMP font asset.
/// </summary>
internal static class SystemFontProvider
{
	private const string DefaultSystemFont = "Microsoft YaHei UI";
	private const int DynamicFontSize = 18;
	private const int SimplifiedChineseLcid = 2052;
	private const uint DefaultCharset = 1;
	private const uint OutDefaultPrecision = 0;
	private const uint ClipDefaultPrecision = 0;
	private const uint DefaultQuality = 0;
	private const uint DefaultPitchAndFamily = 0;
	private const uint MarkMissingGlyphs = 1;
	private const string ChineseProbeGlyphs =
		"中文简体菜单角色物品载具收藏传送工具无限储存设置状态开启关闭确认取消搜索" +
		"字体当前可用支持安装系统选择界面功能汉化翻译生命健康地图快捷键输入应用保存";

	private static readonly string[] SystemFontCandidates =
	{
		DefaultSystemFont,
		"Microsoft YaHei",
		"微软雅黑",
		"SimHei",
		"Arial"
	};
	private static Font _current;
	private static ManualLogSource _log;
	private static string _preferredSystemFont = DefaultSystemFont;
	private static string _selectedSystemFont = string.Empty;
	private static SystemFontFamilyOption[] _installedChineseFontFamilies = Array.Empty<SystemFontFamilyOption>();
	private static bool _installedChineseFontFamiliesDirty = true;
	private static string _lastWarningSignature = string.Empty;

	internal static int Revision { get; private set; }

	internal static string SelectedSystemFont =>
		string.IsNullOrWhiteSpace(_selectedSystemFont) ? _preferredSystemFont : _selectedSystemFont;

	internal static void ConfigurePreferredFamily(string fontFamily, ManualLogSource log)
	{
		_log ??= log;
		_preferredSystemFont = string.IsNullOrWhiteSpace(fontFamily) ? DefaultSystemFont : fontFamily.Trim();
	}

	internal static Font Resolve(ManualLogSource log)
	{
		_log ??= log;
		if (_current != null)
		{
			return _current;
		}

		List<string> candidates = new List<string>();
		AddCandidate(candidates, _preferredSystemFont);
		foreach (string candidate in SystemFontCandidates)
		{
			AddCandidate(candidates, candidate);
		}

		foreach (string candidate in candidates)
		{
			if (!TryCreateValidatedFont(candidate, out Font font, out string actualFamily, out _))
			{
				continue;
			}
			Commit(font, actualFamily);
			_log?.LogInfo("[UI] legacy uGUI system font initialized: " + actualFamily + ".");
			return _current;
		}

		WarnOnce("font-unavailable", "[UI] no installed Chinese-capable system font could initialize the legacy uGUI text renderer.");
		return null;
	}

	internal static IReadOnlyList<SystemFontFamilyOption> GetInstalledChineseFontFamilies(ManualLogSource log)
	{
		_log ??= log;
		if (!_installedChineseFontFamiliesDirty)
		{
			return _installedChineseFontFamilies;
		}

		try
		{
			Dictionary<string, string> localizedNames = GetSimplifiedChineseFamilyNames();
			_installedChineseFontFamilies = GetInstalledFontFamilies()
				.Where(SupportsChineseFace)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(familyName => new SystemFontFamilyOption(
					familyName,
					SystemFontDisplayNameResolver.Resolve(familyName, localizedNames)))
				.OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(option => option.FamilyName, StringComparer.OrdinalIgnoreCase)
				.ToArray();
			_installedChineseFontFamiliesDirty = false;
			_log?.LogInfo("[UI] installed Chinese-capable system fonts discovered: " + _installedChineseFontFamilies.Length + ".");
		}
		catch (Exception exception)
		{
			WarnOnce("font-list:" + exception.GetType().Name,
				"[UI] unable to enumerate Chinese-capable system fonts.\n" + exception);
			_installedChineseFontFamilies = Array.Empty<SystemFontFamilyOption>();
			_installedChineseFontFamiliesDirty = false;
		}
		return _installedChineseFontFamilies;
	}

	internal static bool TrySelectSystemFont(string fontFamily, ManualLogSource log, out string error)
	{
		_log ??= log;
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(fontFamily))
		{
			error = "字体名称不能为空。";
			return false;
		}

		string requested = fontFamily.Trim();
		string installedFamily;
		try
		{
			installedFamily = GetInstalledFontFamilies()
				.FirstOrDefault(value => string.Equals(value, requested, StringComparison.OrdinalIgnoreCase));
		}
		catch (Exception exception)
		{
			error = "无法读取系统字体列表：" + exception.Message;
			return false;
		}

		if (string.IsNullOrEmpty(installedFamily))
		{
			error = "系统中未安装字体：" + requested;
			return false;
		}
		if (!SupportsChineseFace(installedFamily))
		{
			error = "该字体不包含插件所需的简体中文字符：" + installedFamily;
			return false;
		}
		if (_current != null && string.Equals(installedFamily, _selectedSystemFont, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (!TryCreateValidatedFont(installedFamily, out Font font, out string actualFamily, out error))
		{
			return false;
		}

		Commit(font, actualFamily);
		_log?.LogInfo("[UI] legacy uGUI system font selected: " + actualFamily + ".");
		return true;
	}

	internal static void Reset()
	{
		Font previous = _current;
		_current = null;
		_selectedSystemFont = string.Empty;
		_lastWarningSignature = string.Empty;
		Revision++;
		DestroyFont(previous);
	}

	private static bool TryCreateValidatedFont(
		string requestedFamily,
		out Font font,
		out string actualFamily,
		out string error)
	{
		font = null;
		actualFamily = requestedFamily;
		error = string.Empty;
		if (string.IsNullOrWhiteSpace(requestedFamily))
		{
			error = "字体名称不能为空。";
			return false;
		}

		if (!SupportsChineseFace(requestedFamily))
		{
			error = "该字体不包含插件所需的简体中文字符：" + requestedFamily;
			return false;
		}

		try
		{
			font = Font.CreateDynamicFontFromOSFont(new[] { requestedFamily }, DynamicFontSize);
			if (font == null)
			{
				error = "Unity 无法加载系统字体：" + requestedFamily;
				return false;
			}
			string requiredGlyphs = ChineseProbeGlyphs;
			font.RequestCharactersInTexture(requiredGlyphs, DynamicFontSize, FontStyle.Normal);
			foreach (char character in requiredGlyphs)
			{
				if (!font.HasCharacter(character))
				{
					error = "该字体缺少简体中文字符 U+" + ((int)character).ToString("X4") + "：" + requestedFamily;
				DestroyFont(font);
				font = null;
				return false;
				}
			}
			actualFamily = requestedFamily;
			return true;
		}
		catch (Exception exception)
		{
			DestroyFont(font);
			font = null;
			error = "加载系统字体失败：" + exception.Message;
			return false;
		}
	}

	private static bool SupportsChineseFace(string fontFamily)
	{
		if (string.IsNullOrWhiteSpace(fontFamily))
		{
			return false;
		}
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return SupportsChineseWithUnity(fontFamily);
		}

		IntPtr font = CreateFontW(-24, 0, 0, 0, 400, 0, 0, 0, DefaultCharset,
			OutDefaultPrecision, ClipDefaultPrecision, DefaultQuality, DefaultPitchAndFamily, fontFamily);
		if (font == IntPtr.Zero)
		{
			return false;
		}

		IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
		if (dc == IntPtr.Zero)
		{
			DeleteObject(font);
			return false;
		}

		IntPtr previous = SelectObject(dc, font);
		try
		{
			ushort[] glyphs = new ushort[ChineseProbeGlyphs.Length];
			uint result = GetGlyphIndicesW(dc, ChineseProbeGlyphs, ChineseProbeGlyphs.Length, glyphs, MarkMissingGlyphs);
			return result != uint.MaxValue && Array.IndexOf(glyphs, ushort.MaxValue) < 0;
		}
		finally
		{
			if (previous != IntPtr.Zero)
			{
				SelectObject(dc, previous);
			}
			DeleteDC(dc);
			DeleteObject(font);
		}
	}

	private static bool SupportsChineseWithUnity(string fontFamily)
	{
		Font font = null;
		try
		{
			font = Font.CreateDynamicFontFromOSFont(new[] { fontFamily }, DynamicFontSize);
			if (font == null)
			{
				return false;
			}
			font.RequestCharactersInTexture(ChineseProbeGlyphs, DynamicFontSize, FontStyle.Normal);
			return ChineseProbeGlyphs.All(font.HasCharacter);
		}
		catch
		{
			return false;
		}
		finally
		{
			DestroyFont(font);
		}
	}

	private static string[] GetInstalledFontFamilies()
	{
		return (Font.GetOSInstalledFontNames() ?? Array.Empty<string>())
			.Where(value => !string.IsNullOrWhiteSpace(value))
			.ToArray();
	}

	private static Dictionary<string, string> GetSimplifiedChineseFamilyNames()
	{
		Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			Type collectionType = Type.GetType(
				"System.Drawing.Text.InstalledFontCollection, System.Drawing",
				throwOnError: false);
			if (collectionType == null)
			{
				throw new TypeLoadException("System.Drawing.Text.InstalledFontCollection is unavailable.");
			}

			object installedFonts = Activator.CreateInstance(collectionType);
			try
			{
				PropertyInfo familiesProperty = collectionType.GetProperty("Families", BindingFlags.Instance | BindingFlags.Public);
				IEnumerable families = familiesProperty?.GetValue(installedFonts) as IEnumerable;
				if (families == null)
				{
					throw new MissingMemberException(collectionType.FullName, "Families");
				}

				foreach (object fontFamily in families)
				{
					if (fontFamily == null)
					{
						continue;
					}
					Type familyType = fontFamily.GetType();
					PropertyInfo nameProperty = familyType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
					MethodInfo getNameMethod = familyType.GetMethod("GetName", new[] { typeof(int) });
					string familyName = (nameProperty?.GetValue(fontFamily) as string)?.Trim();
					string localizedName = (getNameMethod?.Invoke(fontFamily, new object[] { SimplifiedChineseLcid }) as string)?.Trim();
					if (!string.IsNullOrWhiteSpace(familyName) && !string.IsNullOrWhiteSpace(localizedName))
					{
						names[familyName] = localizedName;
					}
				}
			}
			finally
			{
				(installedFonts as IDisposable)?.Dispose();
			}
		}
		catch (Exception exception)
		{
			WarnOnce("localized-font-names:" + exception.GetType().Name,
				"[UI] unable to read Simplified Chinese font family metadata; canonical names will be shown.\n" + exception);
		}
		return names;
	}

	private static void AddCandidate(ICollection<string> candidates, string fontFamily)
	{
		if (!string.IsNullOrWhiteSpace(fontFamily) &&
			!candidates.Contains(fontFamily.Trim(), StringComparer.OrdinalIgnoreCase))
		{
			candidates.Add(fontFamily.Trim());
		}
	}

	private static void Commit(Font font, string fontFamily)
	{
		Font previous = _current;
		_current = font;
		_selectedSystemFont = fontFamily;
		Revision++;
		DestroyFont(previous);
	}

	private static void WarnOnce(string signature, string message)
	{
		if (string.Equals(signature, _lastWarningSignature, StringComparison.Ordinal))
		{
			return;
		}
		_lastWarningSignature = signature;
		_log?.LogWarning(message);
	}

	private static void DestroyFont(Font font)
	{
		if (font != null)
		{
			UnityEngine.Object.Destroy(font);
		}
	}

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight,
		uint italic, uint underline, uint strikeOut, uint charSet, uint outputPrecision, uint clipPrecision,
		uint quality, uint pitchAndFamily, string faceName);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr dc);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern uint GetGlyphIndicesW(IntPtr dc, string text, int count, [Out] ushort[] glyphIndices, uint flags);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr dc);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr value);
}
