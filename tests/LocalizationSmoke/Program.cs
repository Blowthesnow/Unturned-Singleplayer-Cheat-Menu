using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.Models;
using System.Text.RegularExpressions;

AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
{
    if (args.ExceptionObject is Exception exception)
        Console.Error.WriteLine($"UNHANDLED {exception.GetType().FullName}: {exception.Message}");
};

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
}

Equal(PluginLanguage.English, PluginLocalization.Resolve("Auto", "English"), "Auto should follow English Unturned");
Equal(PluginLanguage.Chinese, PluginLocalization.Resolve("Auto", "Schinese"), "Auto should follow Simplified Chinese Unturned");
Equal(PluginLanguage.English, PluginLocalization.Resolve("English", "Schinese"), "Explicit English should override Unturned");
Equal(PluginLanguage.Chinese, PluginLocalization.Resolve("zh-CN", "English"), "Explicit Chinese alias should override Unturned");
Equal(PluginLanguage.Chinese, PluginLocalization.Resolve("Traditional Chinese", "English"), "Traditional Chinese alias should override Unturned");
Equal(PluginLanguage.English, PluginLocalization.Resolve("invalid", "German"), "Invalid values should fall back to Auto");

PluginLocalization.Initialize("English", "Schinese");
Equal("Singleplayer Cheat Menu", PluginLocalization.Translate("单人作弊指令菜单"), "Static UI translation");
Equal("Workshop", PluginLocalization.Translate("Workshop"), "English origin label should remain Workshop");
Equal("中文", PluginLocalization.SwitchButtonLabel, "English UI should offer the Chinese switch target");
Equal("Language switched to English.", PluginLocalization.LanguageChangedMessage(true), "English switch success message");
Equal(
    "Language switched to English, but the config file could not be saved.",
    PluginLocalization.LanguageChangedMessage(false),
    "English switch persistence failure message");
Equal(
    "Scan complete: 123 items and 45 vehicles.",
    PluginLocalization.Translate("自动扫描完成：123 个物品、45 辆载具。"),
    "Dynamic scan status translation");
Equal(
    "Gave 12 × Example Item.",
    PluginLocalization.Translate("已给予 12 × Example Item。"),
    "Dynamic asset status translation");
Equal(
    "Map PEI    X 1.0    Y 2.0    Z 3.0",
    PluginLocalization.Translate("地图 PEI    X 1.0    Y 2.0    Z 3.0"),
    "Dynamic position translation");
Equal("Location 3", PluginLocalization.DefaultTeleportName(3), "English default teleport name");
Equal("自定义名称", PluginLocalization.Translate("自定义名称"), "Unknown and user-authored text should be preserved");
Equal(
    "Item Favorites  3",
    string.Format(PluginLocalization.Translate("物品收藏  {0}"), 3),
    "English favorite-item count template");
Equal(
    "Flight mode: World Axis",
    string.Format(PluginLocalization.Translate("飞行模式：{0}"), PluginLocalization.Translate("世界轴")),
    "English flight-mode template and value");

string[] dynamicUiSamples =
{
    "自动扫描完成：123 个物品、45 辆载具。",
    "生命 90/100    饱食 80/100    水分 70/100    免疫 60/100    体力 50/100    氧气 40/100",
    "经验 1,234    声望 -50",
    "已增加 1,000 经验。",
    "已变更 -25 声望。",
    "技能已处理，实际变更 12 项。",
    "显示 10 / 100；模组物品 20。搜索支持名称、ID、GUID 和来源。",
    "显示 5 / 50；模组车辆 10。每次生成 1–20 辆。",
    "物品收藏  3/7",
    "车辆收藏  2/4",
    "显示 3 个已加载收藏物品；收藏记录 7 个。搜索支持名称、ID、GUID 和来源。",
    "显示 2 辆已加载收藏车辆；收藏记录 4 辆。",
    "已给予 12 × Example Item。",
    "无法给予 Example Item。",
    "已在玩家前方生成 4 × Example Vehicle。",
    "无法生成 Example Vehicle。",
    "无法保存 Example Item 的收藏状态。",
    "已收藏物品：Example Item。",
    "已取消收藏物品：Example Item。",
    "已收藏车辆：Example Vehicle。",
    "已取消收藏车辆：Example Vehicle。",
    "第 2 / 8 页",
    "地图 PEI    X 1.0    Y 2.0    Z 3.0",
    "已保存传送点：Home。",
    "已保存位置（3）",
    "已传送到 Home。",
    "已删除传送点：Home。",
    "当前时间：1,234 / 3,600（34%）",
    "时间已设置为周期的 50% 。",
    "物品：100（模组 20）    车辆：50（模组 10）",
    "Running  ·  候选 12  ·  成功 8  ·  失败 1  ·  重试 2",
    "当前 2 条白名单物品。",
    "当前 3 条黑名单物品；黑名单始终优先。",
    "15 件  ·  第 2/3 页",
    "ID 363  ·  点击取消黑名单",
    "ID 363  ·  点击加入黑名单",
    "ID 363  ·  点击取消白名单",
    "ID 363  ·  点击加入白名单",
    "已将 Maplestrike 加入黑名单。",
    "已取消物品 ID 363 的黑名单。",
    "已将 Maplestrike 加入白名单。",
    "已取消物品 ID 363 的白名单。",
    "当前背包去重后 15 件  ·  第 2/3 页",
    "已存储 12 个背包格。",
    "显示 2 / 5 条目；总数量 1,024。",
    "ID 363  ·  ×1,024  ·  品质 100  ·  无状态",
    "ID 363  ·  ×1,024  ·  品质 100  ·  状态 8B",
    "ID 44  ·  剩余弹药 40  ·  无状态",
    "ID 44  ·  剩余弹药 40  ·  状态 2B",
    "ID 1200  ·  剩余弹药 5  ·  品质 73  ·  无状态",
    "ID 1200  ·  剩余弹药 5  ·  品质 73  ·  状态 2B",
    "已取出 12 个物品。"
    ,"显示 2 / 5 条目；资源 1,024，完整物品 3 个。"
    ,"库存 1,024"
    ,"库存 2 个 | 弹药 40/40"
    ,"库存 2 个 | 弹药 40/40 | 品质 73%"
    ,"库存 2 个 | 燃料 5/10"
    ,"库存 2 个 | 燃料 5/10 | 品质 73%"
    ,"库存 2 个 | 水 5/10"
    ,"库存 2 个 | 水 5/10 | 品质 73%"
    ,"库存 2 个 | 品质 73% | 状态独立保存"
    ,"库存 2 个 | 品质 73%"
    ,"库存 2 个 | 状态独立保存"
    ,"库存 2 个 | 保守状态隔离"
    ,"库存 2 个"
    ,"已取出 12 单位。"
    ,"已取出 2 个完整物品。"
    ,"每轮 4 个请求"
    ,"显示 2 辆已加载收藏车辆。"
    ,"最大距离 100m"
    ,"已传送到地图坐标 X 12.5 · Z -8.0。"
    ,"预览：50%"
    ,"无法读取系统字体列表：Access denied"
    ,"系统中未安装字体：Example Font"
    ,"该字体不包含插件所需的简体中文字符：Example Font"
    ,"Unity 无法加载系统字体：Example Font"
    ,"该字体缺少简体中文字符 U+4E2D：Example Font"
    ,"加载系统字体失败：Example failure"
    ,"预览：移动 3，归并 2，跨页 1。"
    ,"已整理：移动 3，归并 2，跨页 1。"
    ,"同物品跨箱集中完成：已转移 4 件。"
    ,"整理未完成，已恢复执行前库存：The native inventory API rejected the item."
    ,"整理失败且恢复失败；请停止继续操作并查看 BepInEx 日志。原因：The plan failed；恢复：The restore failed"
    ,"整理后校验失败且恢复失败；请停止继续操作并查看日志。恢复：The restore failed"
    ,"整理出现异常且恢复失败；请停止继续操作并查看 BepInEx 日志。恢复：The restore failed"
    ,"撤销失败，无法恢复原始位置：The native API rejected the position"
};

string[] runtimeUiSamples =
{
	"从收藏中移除此条目。",
	"将此条目添加到收藏。",
	"选择“{0}”传送标记样式。",
	"打开传送标记颜色选择器。",
	"将此预设颜色载入当前传送标记颜色预览。",
	"打开删除传送点“{0}”的确认窗口。",
    "当前地图尚未加载完成。",
    "当前地图没有可读取的 Map.png / Chart.png；仍可保存当前位置传送点。",
    "字体名称不能为空。",
    "紧凑",
    "类型",
    "大件优先",
    "小件优先",
    "品质优先",
    "堆叠整理",
    "热键优先",
    "低扰动",
    "自定义",
    "参考兼容",
    "整理正在执行，已忽略重复请求。",
    "跨箱集中正在执行，已忽略重复请求。",
    "当前没有可用的本地单人玩家库存。",
    "当前不在储物箱与随身库存之间，放行原生操作。",
    "角色装备切换中，放行原生操作。",
    "源坐标没有可转移的有效物品，放行原生操作。",
    "目标容器没有可用空位，放行原生操作。",
    "已接管原生快速转移：正在集中同一资产的物品。",
    "源格子在批量转移期间发生变化，已停止集中。",
    "原生库存未确认转移结果，已停止批量集中。",
    "整理后校验发现物品指纹变化，已恢复执行前库存。",
    "整理出现异常；请查看 BepInEx 日志。",
    "整理出现异常，已恢复执行前库存和热键坐标。",
    "整理正在执行，暂不能撤销。",
    "撤销已失效：整理后的库存已被外部操作改变。",
    "已撤销上次整理。",
    "撤销后校验失败，请查看 BepInEx 日志。",
    "角色装备切换中，无法撤销。",
    "库存包含无法解析的物品，无法安全撤销。",
    "角色装备切换中，已取消整理。",
    "库存包含无法解析的物品，已取消整理以避免丢失物品。",
    "当前上下文没有可用库存容器。",
    "计划引用了不存在的源物品。",
    "原生库存 API 拒绝放置计划中的物品。",
    "原生 API 拒绝恢复原始位置。",
    "整理输入不可用。",
    "没有可整理的库存容器。",
    "受保护物品的位置无效，已取消整理。",
    "预检无法完整安置所有物品；不会修改库存，也不会自动掉落。",
    "低扰动整理发现原位置无效，已取消整理。"
};

foreach (string sample in dynamicUiSamples)
{
    string translated = PluginLocalization.Translate(sample);
    if (Regex.IsMatch(translated, @"[\u4e00-\u9fff]"))
        throw new InvalidOperationException($"Dynamic UI template was not translated: {sample} -> {translated}");
}

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string overlayDirectory = Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI");
string[] overlayFiles = Directory.EnumerateFiles(overlayDirectory, "CheatMenuOverlayUi*.cs")
    .OrderBy(path => path, StringComparer.Ordinal)
    .ToArray();
string overlaySource = string.Join("\n", overlayFiles.Select(File.ReadAllText));
MatchCollection stringLiterals = Regex.Matches(overlaySource, "(?<!\\\\)\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"");
HashSet<string> staticChineseTemplates = new(StringComparer.Ordinal);
HashSet<string> dynamicChineseTemplates = new(StringComparer.Ordinal);
List<string> localizationGaps = new();
foreach (Match match in stringLiterals)
{
    string literal = match.Groups[1].Value;
    if (!Regex.IsMatch(literal, @"[\u4e00-\u9fff]"))
        continue;

    if (literal.Contains('{'))
    {
        dynamicChineseTemplates.Add(literal);
        string translatedTemplate = PluginLocalization.Translate(literal);
        if (!Regex.IsMatch(translatedTemplate, @"[\u4e00-\u9fff]"))
            continue;

        string sample = Regex.Replace(literal, @"\{[^{}]+\}", "1");
        string translated = PluginLocalization.Translate(sample);
        if (Regex.IsMatch(translated, @"[\u4e00-\u9fff]"))
            localizationGaps.Add($"Dynamic UI template: {literal} -> {translated}");
        continue;
    }

    staticChineseTemplates.Add(literal);
    string staticTranslation = PluginLocalization.Translate(literal);
    if (Regex.IsMatch(staticTranslation, @"[\u4e00-\u9fff]"))
        localizationGaps.Add($"Static UI template: {literal} -> {staticTranslation}");
}

foreach (string sample in runtimeUiSamples)
{
    string translated = PluginLocalization.Translate(sample);
    if (Regex.IsMatch(translated, @"[\u4e00-\u9fff]"))
        throw new InvalidOperationException($"Runtime UI message was not translated: {sample} -> {translated}");
}
Equal(true, staticChineseTemplates.Count >= 455, "Active overlay static localization templates must not fall below the locked baseline");
Equal(true, dynamicChineseTemplates.Count >= 40, "Active overlay dynamic localization templates must not fall below the locked baseline");

foreach (string overlayFile in overlayFiles)
{
    string sourceText = File.ReadAllText(overlayFile);
    foreach (Match assignment in Regex.Matches(sourceText, @"\.text\s*=\s*(?<rhs>[^\r\n;]+);"))
    {
        string rhs = assignment.Groups["rhs"].Value;
        if (!Regex.IsMatch(rhs, @"[\u4e00-\u9fff]") || rhs.Contains("PluginLocalization.Translate", StringComparison.Ordinal))
            continue;

        int line = sourceText.AsSpan(0, assignment.Index).Count('\n') + 1;
        localizationGaps.Add($"Direct UI text assignment: {Path.GetFileName(overlayFile)}:{line}: {rhs.Trim()}");
    }
}

if (localizationGaps.Count > 0)
    throw new InvalidOperationException("Localization coverage gaps:\n" + string.Join("\n", localizationGaps.Distinct(StringComparer.Ordinal)));

PluginLocalization.Initialize("Chinese", "English");
Equal("单人作弊指令菜单", PluginLocalization.Translate("单人作弊指令菜单"), "Chinese source should remain unchanged");
Equal("创意工坊", PluginLocalization.Translate("Workshop"), "Chinese origin label should translate Workshop");
Equal("EN", PluginLocalization.SwitchButtonLabel, "Chinese UI should offer the English switch target");
Equal("语言已切换为中文。", PluginLocalization.LanguageChangedMessage(true), "Chinese switch success message");
Equal("语言已切换为中文，但配置文件保存失败。", PluginLocalization.LanguageChangedMessage(false), "Chinese switch persistence failure message");
Equal("位置 2", PluginLocalization.DefaultTeleportName(2), "Chinese default teleport name");

string source = "<color=#FF0000><name_npc></color><br><pause> Meet <name_char>";
WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect(source);
Equal(6, protectedText.Tokens.Count, "NPC dialogue tags should all be protected");
Equal(true, WorkshopRichTextProtector.TryRestore(protectedText, "见到 " + protectedText.Text, out string restored, out string restoreError), "NPC dialogue tags should restore");
Equal("见到 " + source, restored, "NPC dialogue tags should retain their original text and order");
Equal(false, WorkshopRichTextProtector.TryRestore(protectedText, "见到 __USCM_PROTECTED_0000__", out _, out _), "Missing protected tokens must be rejected");
Equal(false, WorkshopRichTextProtector.TryRestore(protectedText, protectedText.Text + " __USCM_PROTECTED_0000__", out _, out _), "Duplicate protected tokens must be rejected");
Equal(false, WorkshopRichTextProtector.TryRestore(protectedText, protectedText.Text.Replace("__USCM_PROTECTED_0000__", "__USCM_PROTECTED_9999__"), out _, out _), "Unknown protected tokens must be rejected");
string reorderedTokens = protectedText.Text.Replace("__USCM_PROTECTED_0000__", "__USCM_PROTECTED_9998__").Replace("__USCM_PROTECTED_0001__", "__USCM_PROTECTED_0000__").Replace("__USCM_PROTECTED_9998__", "__USCM_PROTECTED_0001__");
Equal(false, WorkshopRichTextProtector.TryRestore(protectedText, reorderedTokens, out _, out _), "Reordered protected tokens must be rejected");

string sameSource = "Open the gate";
string dialogueKey = WorkshopTextIdentity.CreateAssetKey("English", "Schinese", WorkshopTextField.DialogueMessagePage, "DialogueAsset", "11111111111111111111111111111111", "Message_0_Page_0", sameSource);
string differentGuidKey = WorkshopTextIdentity.CreateAssetKey("English", "Schinese", WorkshopTextField.DialogueMessagePage, "DialogueAsset", "22222222222222222222222222222222", "Message_0_Page_0", sameSource);
string differentLocalizationKey = WorkshopTextIdentity.CreateAssetKey("English", "Schinese", WorkshopTextField.DialogueMessagePage, "DialogueAsset", "11111111111111111111111111111111", "Message_1_Page_0", sameSource);
string differentTypeKey = WorkshopTextIdentity.CreateAssetKey("English", "Schinese", WorkshopTextField.ObjectInteract, "ObjectAsset", "11111111111111111111111111111111", "Interact", sameSource);
Equal(false, dialogueKey == differentGuidKey, "Asset-scoped keys must distinguish asset GUIDs");
Equal(false, dialogueKey == differentLocalizationKey, "Asset-scoped keys must distinguish localization keys");
Equal(false, dialogueKey == differentTypeKey, "Asset-scoped keys must distinguish fields and asset types");

Console.WriteLine(
    $"Localization smoke checks passed: static={staticChineseTemplates.Count}, " +
    $"dynamic={dynamicChineseTemplates.Count}, dynamic-samples={dynamicUiSamples.Length}, " +
    $"runtime-samples={runtimeUiSamples.Length}.");
