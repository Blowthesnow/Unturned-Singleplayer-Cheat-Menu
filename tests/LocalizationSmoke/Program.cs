using UnturnedSingleplayerCheatMenu.Services;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.InfiniteStorage.SnapshotKernel;
using System.Text;
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
Equal("Singleplayer (supported)", PluginLocalization.Translate("单人（受支持）"), "Supported-session badge translation");
Equal("Multiplayer/server (unsupported)", PluginLocalization.Translate("多人/服务器（不受支持）"), "Unsupported-session badge translation");
Equal("Multiplayer/server (unsupported, acknowledged)", PluginLocalization.Translate("多人/服务器（不受支持，已确认）"), "Acknowledged unsupported-session badge translation");
Equal("Continue This Session", PluginLocalization.Translate("本次会话继续"), "Multiplayer acknowledgement action translation");
Equal("Close Menu", PluginLocalization.Translate("关闭菜单"), "Multiplayer close-menu action translation");
Equal("Snapshot infinite storage is available only in a true singleplayer world.",
    PluginLocalization.Translate("Snapshot 无限储存仅在真正的单人世界中可用。"),
    "Snapshot singleplayer exception translation");
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
    "Action: Zombie cleanup\nScope: Nearby 250m\nExecutable: 12    protected special: 3    collision blocked: 0",
    string.Format(
        PluginLocalization.Translate("动作：{0}\n范围：{1}\n可执行候选：{2}    特殊保护：{3}    碰撞阻挡：{4}"),
        PluginLocalization.Translate("丧尸清除"),
        string.Format(PluginLocalization.Translate("附近 {0}m"), 250), 12, 3, 0),
    "World-maintenance confirmation summary must be fully English as one structured template");
Equal(
    "Processing 16 / 40. Maintenance is running in frame-sized batches.",
    string.Format(PluginLocalization.Translate("处理中 {0} / {1}。{2}"), 16, 40,
        PluginLocalization.Translate("维护任务正在分帧执行。")),
    "World-maintenance progress refresh must explicitly translate both template and service state");
Equal(
    "Task completed: succeeded 7, protected 2, blocked 1, stale 0, failed 0, unprocessed 0.",
    PluginLocalization.Translate("任务完成：成功 7，保护 2，阻挡 1，失效 0，失败 0，未处理 0。"),
    "World-maintenance terminal result must translate after the next-frame refresh");
Equal("Current weather: Rain",
    string.Format(PluginLocalization.Translate("当前天气：{0}"), "Rain"),
    "Environment status must translate the prefix of a runtime weather asset name");
Equal("Current weather: No active weather",
    string.Format(PluginLocalization.Translate("当前天气：{0}"),
        PluginLocalization.Translate("无活动天气")),
    "Environment status must translate the no-weather sentinel value");
Equal("Time: Frozen    full moon: On",
    string.Format(PluginLocalization.Translate("时间：{0}    满月：{1}"),
        PluginLocalization.Translate("已冻结"), PluginLocalization.Translate("已开启")),
    "Environment status must translate both runtime state values");
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
Equal("Infinite storage requires reconciliation; AutoPickup is paused globally.",
    PluginLocalization.Translate("无限储存需人工对账，自动拾取已全局暂停"),
    "AutoPickup global reconciliation state translation");
Equal("Infinite storage requires reconciliation; the drop is quarantined and retry is locked.",
    PluginLocalization.Translate("无限储存需人工对账，已隔离掉落并锁定重试"),
    "AutoPickup drop-quarantine state translation");
Equal("Native pickup outcome is uncertain; automatic retry for this drop is paused.",
    PluginLocalization.Translate("原生拾取后态不确定，已暂停该掉落实例的自动重试"),
    "AutoPickup uncertain-native state translation");
Equal("Persistent snapshot reconciliation is required; all automatic pickup is paused.",
    PluginLocalization.Translate("需要持久化快照对账；所有自动拾取已暂停。"),
    "AutoPickup persistent reconciliation detail translation");
Equal("Storage is authoritative; the unresolved world drop is quarantined and infinite storage is locked.",
    PluginLocalization.Translate("储存副本为权威；未解决的世界掉落已隔离，且无限储存已锁定。"),
    "AutoPickup storage-authoritative detail translation");
Equal("Storage is authoritative and infinite storage is locked; world-drop quarantine also failed.",
    PluginLocalization.Translate("储存副本为权威且无限储存已锁定；世界掉落隔离也失败。"),
    "AutoPickup quarantine-failure detail translation");
Equal("Infinite-storage compensation succeeded; the world drop remains for a later retry.",
    PluginLocalization.Translate("无限储存补偿成功；世界掉落仍保留，稍后可重试。"),
    "AutoPickup compensated-storage detail translation");
Equal("Native pickup was not confirmed delivered, and Snapshot recovery failed.",
    PluginLocalization.Translate("原生拾取未确认交付，且无限储存恢复失败。"),
    "AutoPickup native-and-Snapshot double-failure detail translation");
Equal("Composite inventory evidence confirmed native pickup delivery.",
    PluginLocalization.Translate("背包复合证据已确认原生拾取交付。"),
    "AutoPickup evidence-confirmed delivery detail translation");
Equal("native-detail=E42; composite inventory evidence confirmed native pickup delivery.",
    PluginLocalization.Translate("native-detail=E42；背包复合证据已确认原生拾取交付。"),
    "AutoPickup preserved native detail plus evidence-confirmation translation");
Equal("native-detail=E42\r\nstate=unchanged; composite inventory evidence confirmed native pickup delivery.",
    PluginLocalization.Translate("native-detail=E42\r\nstate=unchanged；背包复合证据已确认原生拾取交付。"),
    "AutoPickup multiline native detail plus evidence-confirmation translation");
const string dynamicPatternCollisionPrefix = "无法读取系统字体列表：E42";
const string multilineDynamicPatternPrefix = "无法读取系统字体列表：E42\r\nstate=unchanged";
const string translatedEvidenceSuffix = "; composite inventory evidence confirmed native pickup delivery.";
const string chineseEvidenceSuffix = "背包复合证据已确认原生拾取交付。";
Equal(dynamicPatternCollisionPrefix + translatedEvidenceSuffix,
    PluginLocalization.Translate(dynamicPatternCollisionPrefix + "；背包复合证据已确认原生拾取交付。"),
    "AutoPickup evidence suffix must take priority over an older single-line dynamic pattern and preserve its prefix exactly");
Equal(multilineDynamicPatternPrefix + translatedEvidenceSuffix,
    PluginLocalization.Translate(multilineDynamicPatternPrefix + "；背包复合证据已确认原生拾取交付。"),
    "AutoPickup evidence suffix must match multiline details and preserve their prefix exactly");
Equal(multilineDynamicPatternPrefix,
    PluginLocalization.Translate(multilineDynamicPatternPrefix),
    "A multiline native detail without the evidence suffix must not be consumed by a single-line dynamic pattern");
const string replacementMetacharacterPrefix =
    "无法读取系统字体列表：$1\\native\\path；内部分隔\r\nstate=$1\\tail";
string replacementMetacharacterTranslation = PluginLocalization.Translate(
    replacementMetacharacterPrefix + "；" + chineseEvidenceSuffix);
byte[] replacementMetacharacterPrefixUtf8 = Encoding.UTF8.GetBytes(replacementMetacharacterPrefix);
byte[] replacementMetacharacterTranslationUtf8 = Encoding.UTF8.GetBytes(replacementMetacharacterTranslation);
int replacementMetacharacterSuffixIndex = replacementMetacharacterTranslation.IndexOf(
    translatedEvidenceSuffix, StringComparison.Ordinal);
Equal(replacementMetacharacterPrefix + translatedEvidenceSuffix,
    replacementMetacharacterTranslation,
    "AutoPickup replacement metacharacters must remain literal while only the fixed evidence suffix is translated");
Equal(true,
    replacementMetacharacterTranslationUtf8.Length >= replacementMetacharacterPrefixUtf8.Length &&
    replacementMetacharacterTranslationUtf8.Take(replacementMetacharacterPrefixUtf8.Length)
        .SequenceEqual(replacementMetacharacterPrefixUtf8),
    "AutoPickup replacement metacharacter prefix must remain byte-identical in UTF-8");
Equal(true,
    replacementMetacharacterSuffixIndex >= 0 &&
    replacementMetacharacterSuffixIndex == replacementMetacharacterTranslation.LastIndexOf(
        translatedEvidenceSuffix, StringComparison.Ordinal),
    "AutoPickup translated evidence suffix must occur exactly once");
Equal(false,
    replacementMetacharacterTranslation.Contains(chineseEvidenceSuffix, StringComparison.Ordinal),
    "AutoPickup translated detail must not retain the Chinese evidence suffix");

SnapshotBucketMetadata snapshotMetadataSource = SnapshotBucketMetadata.Build(
    new ItemSnapshot(363, 10, 73, new byte[] { 1, 2, 3, 4 }), count: 3, showQuality: true);
SnapshotBucketMetadata ammoMetadataSource = SnapshotBucketMetadata.Build(
    new ItemSnapshot(363, 0, 73, Array.Empty<byte>()), count: 1, showQuality: true,
    SnapshotBucketDisplayFacts.AmmoContainer(capacity: 40));
string snapshotHeaderSource = string.Format(
    PluginLocalization.Translate("物品编号 {0} · 资产标识 {1}\n实例 {2} · 快照桶 {3}"),
    363, "11111111-1111-1111-1111-111111111111", 3, 1);
string[] snapshotCanonicalUiSamples =
{
    "快照运行时尚未初始化。",
    "筛选物品组。",
    "单实例 → 当前快照桶 → 当前物品组循环。",
    "没有可存储物品、保存失败，或快照运行时未就绪。",
    "每个背包物品均按原生编号、数量、品质和状态字节完整存储。",
    "物品组 2 / 5；快照实例 3。原生数量字段始终保持不透明，内核不会拆分。",
    "没有匹配的物品组。",
    "未解析物品组（只读）",
    "资产标识冲突的物品组（只读）",
    "展开或收起快照桶。",
    "资产缺失或编号到资产标识的映射不匹配；物品组被保留为只读且禁止取出。",
    snapshotMetadataSource.Text,
    ammoMetadataSource.Text,
    "ID 330  ·  ×1/总2",
    " · 默认满状态",
    "取出所选精确快照桶。",
    "资产不可解析或资产标识冲突，禁止取出。",
    "取全部 ×3",
    "取出此精确快照桶的全部实例。",
    "已取出 3 个精确快照实例。",
    "未取出：背包空间不足、资产冲突或保存失败。",
    "一键填充弹药容器",
    "独立弹药填充模块负责解释原生数量与填充目标物品的兼容关系；快照内核不解释弹量。",
    "没有可安全填充的弹药来源或容器。",
    "弹药容器已达到稳定状态。",
    "已填充 2 个容器；弹量总和 30。",
    "制作适配",
    "独立制作模块负责解释所需物品总数与原生数量总和；快照内核不解释制作语义。",
    "分解",
    "独立分解模块负责解释原生分解配方；快照内核不读取蓝图。",
    "已分解 2 个快照实例，生成 4 个实例。"
};
foreach (string sample in snapshotCanonicalUiSamples)
{
    string translated = PluginLocalization.Translate(sample);
    if (Regex.IsMatch(translated, @"[\u4e00-\u9fff]"))
        throw new InvalidOperationException($"Snapshot UI template was not translated: {sample} -> {translated}");
}
Equal("ID 363 · GUID 11111111-1111-1111-1111-111111111111\nInstances 3 · Buckets 1",
    snapshotHeaderSource, "Snapshot ItemGroup header translation");
Equal("Module failed: The selected snapshot could not be found.",
    PluginLocalization.SnapshotModuleFailureMessage("snapshot-not-found"),
    "Known Snapshot module errors should be localized without exposing internal error codes");
Equal("Module failed: The module threw an internal exception.",
    PluginLocalization.SnapshotModuleFailureMessage("module-threw:NullReferenceException"),
    "Thrown Snapshot module errors should hide implementation type names from the UI");

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
string maintenanceViewSource = File.ReadAllText(Path.Combine(overlayDirectory, "CheatMenuOverlayUi.WorldMaintenanceView.cs"));
string worldViewSource = File.ReadAllText(Path.Combine(overlayDirectory, "CheatMenuOverlayUi.WorldView.cs"));
Equal(true, worldViewSource.Contains("PluginLocalization.Translate(\"当前天气：{0}\")", StringComparison.Ordinal) &&
            worldViewSource.Contains("PluginLocalization.Translate(snapshot.ActiveWeatherName)", StringComparison.Ordinal) &&
            worldViewSource.Contains("PluginLocalization.Translate(\"时间：{0}    满月：{1}\")", StringComparison.Ordinal),
    "Environment runtime rows must localize their full templates before rendering");
int maintenancePreviewFormatter = maintenanceViewSource.IndexOf("private static string FormatMaintenancePreview", StringComparison.Ordinal);
int maintenanceActionFormatter = maintenanceViewSource.IndexOf("private static string MaintenanceActionLabel", StringComparison.Ordinal);
int maintenanceProgressFormatter = maintenanceViewSource.IndexOf("private static string FormatMaintenanceProgress", StringComparison.Ordinal);
string maintenancePreviewBody = maintenancePreviewFormatter >= 0 && maintenanceActionFormatter > maintenancePreviewFormatter
    ? maintenanceViewSource[maintenancePreviewFormatter..maintenanceActionFormatter]
    : string.Empty;
string maintenanceProgressBody = maintenanceProgressFormatter >= 0 && maintenancePreviewFormatter > maintenanceProgressFormatter
    ? maintenanceViewSource[maintenanceProgressFormatter..maintenancePreviewFormatter]
    : string.Empty;
Equal(true, maintenancePreviewBody.Contains("PluginLocalization.Translate", StringComparison.Ordinal),
    "World-maintenance initial confirmation formatter must localize its complete structured template");
Equal(true, maintenanceProgressBody.Contains("PluginLocalization.Translate", StringComparison.Ordinal),
    "World-maintenance next-frame progress formatter must localize template and runtime message explicitly");
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

SnapshotBucketMetadata chineseSnapshotMetadata = SnapshotBucketMetadata.Build(
    new ItemSnapshot(363, 10, 73, new byte[] { 1, 2, 3, 4 }), count: 3, showQuality: true);
if (Regex.IsMatch(chineseSnapshotMetadata.Text,
        @"\b(?:amount|quality|state|total|Snapshot|Bucket|Core|adapter)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    throw new InvalidOperationException("Chinese Snapshot bucket metadata leaked product-owned English: " +
                                        chineseSnapshotMetadata.Text);
foreach (string sample in snapshotCanonicalUiSamples)
{
    string translated = PluginLocalization.Translate(sample);
    if (Regex.IsMatch(translated,
            @"\b(?:Snapshot|Runtime|ItemGroup|Bucket|Core|adapter|amount|quality|state|Crafting|AmmoRefill|Decomposition|salvage|blueprint)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        throw new InvalidOperationException("Chinese Snapshot UI leaked product-owned English: " + translated);
}
string chineseKnownError = PluginLocalization.SnapshotModuleFailureMessage("snapshot-not-found");
string chineseThrownError = PluginLocalization.SnapshotModuleFailureMessage("module-threw:NullReferenceException");
if (chineseKnownError.Contains("snapshot-not-found", StringComparison.Ordinal) ||
    chineseThrownError.Contains("module-threw", StringComparison.Ordinal) ||
    chineseThrownError.Contains("NullReferenceException", StringComparison.Ordinal))
    throw new InvalidOperationException("Chinese Snapshot errors exposed internal English codes or type names.");

string infiniteStorageViewPath = Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI",
    "CheatMenuOverlayUi.InfiniteStorageView.cs");
string infiniteStorageViewSource = File.ReadAllText(infiniteStorageViewPath);
HashSet<string> internalSnapshotLiterals = new(StringComparer.Ordinal)
{
    "single", "bucket", "asset", "decomposition", "InfiniteStorageResultsHost",
    "InfiniteStorageGridColumn", "InfiniteStorageGrid"
};
foreach (Match match in Regex.Matches(infiniteStorageViewSource,
             "(?<!\\\\)\\\"((?:\\\\.|[^\\\"\\\\])*)\\\""))
{
    string literal = match.Groups[1].Value;
    if (internalSnapshotLiterals.Contains(literal)) continue;
    string visibleLiteral = Regex.Replace(literal, @"\{[^{}]*\}", string.Empty);
    if (Regex.IsMatch(visibleLiteral,
            @"\b(?:Snapshot|Runtime|ItemGroup|Bucket|Core|adapter|amount|quality|state)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        throw new InvalidOperationException("Snapshot UI has a product-owned English literal in Chinese mode: " + literal);
}

foreach (string modulePath in Directory.EnumerateFiles(
             Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "InfiniteStorage", "Adapters"),
             "*.cs", SearchOption.AllDirectories))
{
    string moduleSource = File.ReadAllText(modulePath);
    foreach (Match match in Regex.Matches(moduleSource,
                 "public string (?:Label|Tooltip) => \\\"(?<text>(?:\\\\.|[^\\\"\\\\])*)\\\""))
    {
        string text = match.Groups["text"].Value;
        if (Regex.IsMatch(text,
                @"\b(?:Snapshot|Runtime|ItemGroup|Bucket|Core|adapter|amount|quality|state|Crafting|AmmoRefill|Decomposition|salvage|blueprint)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Snapshot module UI text has product-owned English in Chinese mode: " + text);
    }
}

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
