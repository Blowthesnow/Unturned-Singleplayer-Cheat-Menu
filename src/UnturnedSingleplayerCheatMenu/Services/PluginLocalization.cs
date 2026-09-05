using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class PluginLocalization
{
	private sealed class PatternTranslation
	{
		public Regex Pattern { get; }

		public string Replacement { get; }

		public PatternTranslation(string pattern, string replacement)
		{
			Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
			Replacement = replacement;
		}
	}

	private static readonly IReadOnlyDictionary<string, string> English;

	private static readonly IReadOnlyDictionary<string, string> Chinese;

	private static readonly PatternTranslation[] EnglishPatterns;

	public static PluginLanguage CurrentLanguage { get; private set; }

	public static string CurrentLanguageName
	{
		get
		{
			if (CurrentLanguage != PluginLanguage.English)
			{
				return "Chinese";
			}
			return "English";
		}
	}

	public static string SwitchButtonLabel
	{
		get
		{
			if (CurrentLanguage != PluginLanguage.English)
			{
				return "EN";
			}
			return "中文";
		}
	}

	public static void Initialize(string configuredLanguage, string unturnedLanguage)
	{
		CurrentLanguage = Resolve(configuredLanguage, unturnedLanguage);
	}

	public static PluginLanguage Resolve(string configuredLanguage, string unturnedLanguage)
	{
		string text = Normalize(configuredLanguage);
		bool flag;
		switch (text)
		{
		case "english":
		case "en":
		case "enus":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return PluginLanguage.English;
		}
		switch (text)
		{
		case "zhcn":
		case "zhtw":
		case "zhhans":
		case "zhhant":
		case "schinese":
		case "tchinese":
		case "chinese":
		case "zh":
		case "simplifiedchinese":
		case "traditionalchinese":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return PluginLanguage.Chinese;
		}
		if (Normalize(unturnedLanguage).IndexOf("chinese", StringComparison.Ordinal) < 0)
		{
			return PluginLanguage.English;
		}
		return PluginLanguage.Chinese;
	}

	public static string Translate(string source)
	{
		if (string.IsNullOrEmpty(source))
		{
			return source;
		}
		if (CurrentLanguage == PluginLanguage.Chinese)
		{
			if (!Chinese.TryGetValue(source, out var value))
			{
				return source;
			}
			return value;
		}
		if (English.TryGetValue(source, out var value2))
		{
			return value2;
		}
		if (source.IndexOf("\\n", StringComparison.Ordinal) >= 0
			&& English.TryGetValue(source.Replace("\\n", "\n", StringComparison.Ordinal), out var escapedValue))
		{
			return escapedValue;
		}
		PatternTranslation[] englishPatterns = EnglishPatterns;
		foreach (PatternTranslation patternTranslation in englishPatterns)
		{
			if (patternTranslation.Pattern.IsMatch(source))
			{
				return patternTranslation.Pattern.Replace(source, patternTranslation.Replacement);
			}
		}
		return source;
	}

	public static string SnapshotModuleFailureMessage(string errorCode)
	{
		return string.Format(Translate("模块失败：{0}"), SnapshotErrorMessage(errorCode));
	}

	public static string SnapshotErrorMessage(string errorCode)
	{
		string normalized = (errorCode ?? string.Empty).Trim();
		string source = normalized switch
		{
			"module-unavailable" => "模块未启用或不可用。",
			"module-not-applicable" => "当前选择不适用于此模块。",
			"module-returned-null" => "模块没有返回有效结果。",
			"runtime-hook-only" => "制作模块由原生制作流程自动调用。",
			"storage-unavailable" or "session-not-operational" => "无限储存尚未就绪或已锁定。",
			"transaction-missing" => "储存事务尚未建立。",
			"invalid-ammo-candidate" => "弹药来源或容器数据无效。",
			"allocation-failed" => "弹药分配计划无法完成。",
			"invalid-requirement" => "制作需求数据无效。",
			"snapshot-not-found" or "snapshot-missing" => "找不到所选快照。",
			"insufficient-units" => "可用数量不足。",
			"group-not-found" => "找不到所选物品组。",
			"output-asset-unavailable" => "无法解析产物资产。",
			"source-asset-unavailable" => "无法解析来源资产。",
			"material-not-decomposable" => "材料物品不能继续分解。",
			"salvage-blueprint-missing" => "该物品没有原生分解配方。",
			"salvage-blueprint-unsupported" => "原生分解配方不符合安全支持条件。",
			"invalid-decomposition-request" => "分解请求无效。",
			"output-transform-invalid" => "分解产物状态转换失败。",
			"arithmetic-overflow" => "数量计算超出安全范围。",
			_ when normalized.StartsWith("module-threw:", StringComparison.Ordinal) => "模块内部发生异常。",
			_ => "模块操作未完成；请查看日志中的错误详情。"
		};
		return Translate(source);
	}

	public static string DefaultTeleportName(int number)
	{
		if (CurrentLanguage != PluginLanguage.English)
		{
			return $"位置 {number}";
		}
		return $"Location {number}";
	}

	public static string LanguageChangedMessage(bool persisted)
	{
		if (CurrentLanguage == PluginLanguage.English)
		{
			if (!persisted)
			{
				return "Language switched to English, but the config file could not be saved.";
			}
			return "Language switched to English.";
		}
		if (!persisted)
		{
			return "语言已切换为中文，但配置文件保存失败。";
		}
		return "语言已切换为中文。";
	}

	private static string Normalize(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}
		return value.Trim().Replace("-", string.Empty).Replace("_", string.Empty)
			.Replace(" ", string.Empty)
			.ToLowerInvariant();
	}

	static PluginLocalization()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		dictionary["接口与密钥"] = "Connection and keys";
		dictionary["高级与缓存维护"] = "Advanced and cache maintenance";
		dictionary["译文显示"] = "Translation display";
		dictionary["展开或收起此设置分组；已输入的内容会保留。"] = "Expand or collapse this section without discarding your input.";
		dictionary["状态：{0}  ·  {1}  ·  {2}"] = "Status: {0}  ·  {1}  ·  {2}";
		dictionary["小地图已开启"] = "Minimap enabled";
		dictionary["小地图已关闭"] = "Minimap disabled";
		dictionary["全部"] = "All";
		dictionary["无"] = "None";
		dictionary["武器"] = "Weapons";
		dictionary["弹药与配件"] = "Ammo & Attachments";
		dictionary["衣物"] = "Clothing";
		dictionary["食物与饮料"] = "Food & Drinks";
		dictionary["医疗与防护"] = "Medical";
		dictionary["建筑与放置物"] = "Structures";
		dictionary["工具"] = "Tools";
		dictionary["其他"] = "Other";
		dictionary["陆地车辆"] = "Land Vehicles";
		dictionary["固定翼飞机"] = "Planes";
		dictionary["直升机"] = "Helicopters";
		dictionary["飞艇"] = "Blimps";
		dictionary["船只"] = "Boats";
		dictionary["火车"] = "Trains";
		dictionary["准备就绪。"] = "Ready.";
		dictionary["信息"] = "Info";
		dictionary["成功"] = "Success";
		dictionary["提示"] = "Warning";
		dictionary["失败"] = "Error";
		dictionary["无法"] = "Unable";
		dictionary["没有可用"] = "No usable";
		dictionary["没有默认"] = "No default";
		dictionary["单人作弊指令菜单"] = "Singleplayer Cheat Menu";
		dictionary["单人（受支持）"] = "Singleplayer (supported)";
		dictionary["多人/服务器（不受支持）"] = "Multiplayer/server (unsupported)";
		dictionary["多人/服务器（不受支持，已确认）"] = "Multiplayer/server (unsupported, acknowledged)";
		dictionary["会话未就绪"] = "Session not ready";
		dictionary["多人玩法风险确认"] = "Multiplayer Gameplay Risk";
		dictionary["玩法功能仅在单人世界中受支持。多人/服务器会话中的操作可能被拒绝、回档、断开连接或导致账号处罚；Workshop 翻译不受此确认影响。"] = "Gameplay features are supported only in singleplayer worlds. Multiplayer/server actions may be rejected, rolled back, disconnected, or result in account penalties; Workshop translation is unaffected by this acknowledgement.";
		dictionary["本次会话继续"] = "Continue This Session";
		dictionary["关闭菜单"] = "Close Menu";
		dictionary["不确认多人玩法风险并关闭插件菜单。"] = "Close the plugin menu without acknowledging multiplayer gameplay risk.";
		dictionary["仅为当前连接确认不受支持的多人玩法风险。"] = "Acknowledge unsupported multiplayer gameplay risk for this connection only.";
		dictionary["已为本次连接确认多人玩法风险；该环境仍不受支持。"] = "Multiplayer gameplay risk acknowledged for this connection; this environment remains unsupported.";
		dictionary["关于此插件"] = "About This Plugin";
		dictionary["从收藏中移除此条目。"] = "Remove this entry from favorites.";
		dictionary["将此条目添加到收藏。"] = "Add this entry to favorites.";
		dictionary["选择“{0}”传送标记样式。"] = "Select the “{0}” teleport marker style.";
		dictionary["打开传送标记颜色选择器。"] = "Open the teleport marker color picker.";
		dictionary["将此预设颜色载入当前传送标记颜色预览。"] = "Load this preset into the current teleport marker color preview.";
		dictionary["打开删除传送点“{0}”的确认窗口。"] = "Open the confirmation dialog to delete teleport point “{0}”.";
		dictionary["将每轮原生拾取请求上限增加 1；到 16 后循环回 1。"] = "Increase the native pickup request limit per tick by 1; after 16 it wraps to 1.";
		dictionary["在关闭、摘要和逐项诊断日志之间循环切换。"] = "Cycle diagnostics between off, summary, and per-item logging.";
		dictionary["打开白名单管理页，查看或移除已加入的物品。"] = "Open whitelist management to review or remove listed items.";
		dictionary["从当前背包选择要加入自动拾取白名单的物品。"] = "Choose items from the current backpack to add to the auto-pickup whitelist.";
		dictionary["打开黑名单管理页，查看或移除已加入的物品。"] = "Open blacklist management to review or remove listed items.";
		dictionary["从当前背包选择要加入自动拾取黑名单的物品。"] = "Choose items from the current backpack to add to the auto-pickup blacklist.";
		dictionary["从当前背包继续选择要加入白名单的物品。"] = "Choose more items from the current backpack to add to the whitelist.";
		dictionary["从当前背包继续选择要加入黑名单的物品。"] = "Choose more items from the current backpack to add to the blacklist.";
		dictionary["点击将此物品从自动拾取白名单移除。"] = "Click to remove this item from the auto-pickup whitelist.";
		dictionary["点击将此物品加入自动拾取白名单。"] = "Click to add this item to the auto-pickup whitelist.";
		dictionary["点击将此物品从自动拾取黑名单移除。"] = "Click to remove this item from the auto-pickup blacklist.";
		dictionary["点击将此物品加入自动拾取黑名单。"] = "Click to add this item to the auto-pickup blacklist.";
		dictionary["关闭白名单物品选择窗口。"] = "Close the whitelist item picker.";
		dictionary["关闭黑名单物品选择窗口。"] = "Close the blacklist item picker.";
		dictionary["将每次给予的物品数量减少 1，最低为 1。"] = "Decrease the item quantity given per click by 1, down to 1.";
		dictionary["将每次给予的物品数量增加 1，最高为 255。"] = "Increase the item quantity given per click by 1, up to 255.";
		dictionary["打开物品类型、来源、稀有度和装备槽位筛选面板。"] = "Open filters for item type, origin, rarity, and equipment slot.";
		dictionary["重新读取当前已加载的原版、地图和 Workshop 物品资产。"] = "Reload the currently loaded official, map, and Workshop item assets.";
		dictionary["将每次生成的载具数量减少 1，最低为 1。"] = "Decrease the number of vehicles spawned per click by 1, down to 1.";
		dictionary["将每次生成的载具数量增加 1，最高为 20。"] = "Increase the number of vehicles spawned per click by 1, up to 20.";
		dictionary["设置车辆卡片缩略图的尺寸和取景倍率。"] = "Configure the size and framing scale of vehicle-card thumbnails.";
		dictionary["重新读取当前已加载的原版、地图和 Workshop 载具资产。"] = "Reload the currently loaded official, map, and Workshop vehicle assets.";
		dictionary["切换到已收藏物品列表。"] = "Switch to the favorite items list.";
		dictionary["切换到已收藏载具列表。"] = "Switch to the favorite vehicles list.";
		dictionary["将收藏物品每次给予的数量减少 1，最低为 1。"] = "Decrease the favorite-item quantity given per click by 1, down to 1.";
		dictionary["将收藏物品每次给予的数量增加 1，最高为 255。"] = "Increase the favorite-item quantity given per click by 1, up to 255.";
		dictionary["打开收藏物品的类型、来源、稀有度和装备槽位筛选面板。"] = "Open type, origin, rarity, and equipment-slot filters for favorite items.";
		dictionary["将收藏载具每次生成的数量减少 1，最低为 1。"] = "Decrease the favorite-vehicle spawn quantity by 1, down to 1.";
		dictionary["将收藏载具每次生成的数量增加 1，最高为 20。"] = "Increase the favorite-vehicle spawn quantity by 1, up to 20.";
		dictionary["重新读取当前已加载资产，并刷新已收藏物品和载具。"] = "Reload the currently loaded assets and refresh favorite items and vehicles.";
		dictionary["只显示“{0}”分类中的车辆。"] = "Show only vehicles in the “{0}” category.";
		dictionary["只显示“{0}”分类中的物品。"] = "Show only items in the “{0}” category.";
		dictionary["清除当前已应用的全部高级物品筛选条件。"] = "Clear all currently applied advanced item filters.";
		dictionary["移除“{0}”筛选条件。"] = "Remove the “{0}” filter.";
		dictionary["关闭筛选面板并放弃尚未应用的更改。"] = "Close the filter panel and discard unapplied changes.";
		dictionary["清除筛选面板中的高级条件；点击应用后才会影响结果。"] = "Clear the advanced conditions in this panel; results change only after Apply.";
		dictionary["放弃筛选面板中尚未应用的更改。"] = "Discard unapplied changes in the filter panel.";
		dictionary["应用面板中的筛选条件并刷新当前物品结果。"] = "Apply the panel's filters and refresh the current item results.";
		dictionary["切换“{0}”中的“{1}”筛选选项。"] = "Toggle the “{1}” option under the “{0}” filter.";
		dictionary["点击卡片按当前数量给予此物品。"] = "Click the card to give this item in the current quantity.";
		dictionary["点击卡片按当前数量在玩家前方生成此载具。"] = "Click the card to spawn the current quantity of this vehicle in front of the player.";
		dictionary["切换到上一页结果。"] = "Switch to the previous page of results.";
		dictionary["切换到下一页结果。"] = "Switch to the next page of results.";
		dictionary["将六个输入框中的数值写入当前角色的生命与生存状态。"] = "Apply the six input values to the current character's health and survival stats.";
		dictionary["恢复生命、生存状态并治疗当前伤势。"] = "Restore health and survival stats and treat current injuries.";
		dictionary["将饱食、水分、免疫、体力和氧气全部补满。"] = "Refill food, water, immunity, stamina, and oxygen to maximum.";
		dictionary["停止流血并治愈骨折，不改动其他角色数值。"] = "Stop bleeding and heal fractures without changing other character stats.";
		dictionary["按左侧输入值增加角色经验，单次范围为 1 到 100000000。"] = "Add the value entered on the left to experience, from 1 to 100000000 per use.";
		dictionary["按左侧输入的正数或负数增减角色声望。"] = "Increase or decrease reputation using the positive or negative value entered on the left.";
		dictionary["将当前角色所有尚未满级的技能提升到满级。"] = "Raise every skill that is not yet maxed to its maximum level.";
		dictionary["将每次从无限储存取出的数量减少 1，最低为 1。"] = "Decrease the amount withdrawn from infinite storage by 1, down to 1.";
		dictionary["将每次从无限储存取出的数量增加 1，最高为 255。"] = "Increase the amount withdrawn from infinite storage by 1, up to 255.";
		dictionary["打开无限储存物品的详细筛选面板。"] = "Open detailed filters for items in infinite storage.";
		dictionary["将准星工具最大作用距离增加 25 米；到 250 米后循环回 25 米。"] = "Increase the crosshair tool's maximum range by 25 m; after 250 m it wraps to 25 m.";
		dictionary["打开准星工具模式列表；当前为“{0}”。"] = "Open the crosshair tool mode list; the current mode is “{0}”.";
		dictionary["将准星工具切换为“{0}”模式。"] = "Switch the crosshair tool to “{0}” mode.";
		dictionary["切换到小地图“{0}”设置页。"] = "Switch to the minimap's “{0}” settings page.";
		dictionary["循环切换小地图视口形状。"] = "Cycle through the minimap viewport shapes.";
		dictionary["循环切换小地图在屏幕上的锚点位置。"] = "Cycle through the minimap's screen anchor positions.";
		dictionary["循环切换小地图使用的背景来源。"] = "Cycle through the minimap background sources.";
		dictionary["允许小地图显示所有稀有度的地面物品。"] = "Allow the minimap to show ground items of every rarity.";
		dictionary["取消所有物品稀有度，小地图将不显示地面物品标记。"] = "Deselect every item rarity so the minimap shows no ground-item markers.";
		dictionary["关闭关于窗口。"] = "Close the About window.";
		dictionary["在系统浏览器中打开此插件的 GitHub 项目页面。"] = "Open this plugin's GitHub project page in the system browser.";
		dictionary["在系统浏览器中打开作者的爱发电支持页面。"] = "Open the author's Afdian support page in the system browser.";
		dictionary["关闭字体选择窗口。"] = "Close the font picker.";
		dictionary["将菜单界面字体切换为“{0}”。"] = "Switch the menu UI font to “{0}”.";
		dictionary["关闭快捷键设置窗口。"] = "Close the shortcut settings window.";
		dictionary["滚动到“{0}”快捷键分组。"] = "Scroll to the “{0}” shortcut group.";
		dictionary["等待输入并把新按键绑定到“{0}”。"] = "Wait for input and bind the new key to “{0}”.";
		dictionary["清除“{0}”的当前快捷键绑定。"] = "Clear the current shortcut binding for “{0}”.";
		dictionary["将“{0}”恢复为默认快捷键。"] = "Restore the default shortcut for “{0}”.";
		dictionary["切换到地图视图，在 GPS 图层上查看和选择传送位置。"] = "Switch to map view to inspect and choose teleport positions on the GPS layer.";
		dictionary["切换到传送点视图，保存、使用或删除当前地图的传送点。"] = "Switch to teleport-point view to save, use, or delete points for the current map.";
		dictionary["重新从当前地图目录读取 Map.png 或 Chart.png。"] = "Reload Map.png or Chart.png from the current map directory.";
		dictionary["缩小当前地图视图以显示更大范围。"] = "Zoom out the current map view to show a larger area.";
		dictionary["放大当前地图视图以查看更小范围。"] = "Zoom in the current map view to inspect a smaller area.";
		dictionary["把地图视图中心移动到本地玩家当前位置。"] = "Center the map view on the local player's current position.";
		dictionary["用输入的名称、标记样式和颜色保存当前玩家位置。"] = "Save the player's current position with the entered name, marker style, and color.";
		dictionary["传送到保存点“{0}”；被阻挡、地图不符或在载具中时会失败。"] = "Teleport to saved point “{0}”; this fails if blocked, on another map, or while in a vehicle.";
		dictionary["保留此传送点并关闭删除确认窗口。"] = "Keep this teleport point and close the delete confirmation.";
		dictionary["永久删除传送点“{0}”。"] = "Permanently delete teleport point “{0}”.";
		dictionary["切换到“{0}”工具页面。"] = "Switch to the “{0}” tools page.";
		dictionary["打开翻译 API 协议列表；选择后可配置对应的 Endpoint、API Key 和 Model。"] = "Open the translation API protocol list; then configure its Endpoint, API Key, and Model below.";
		dictionary["重新扫描已加载资产，并立即把已有缓存译文应用到显示 Getter。"] = "Rescan loaded assets and immediately apply cached translations to display getters.";
		dictionary["从磁盘重新读取翻译缓存，替换当前内存中的缓存内容。"] = "Reload the translation cache from disk, replacing the in-memory cache.";
		dictionary["仅向翻译服务提交缓存中尚不存在的当前资产文本。"] = "Submit only current asset text that is not already in the cache.";
		dictionary["忽略现有缓存，重新提交当前资产文本并覆盖对应缓存译文。"] = "Ignore the existing cache, resubmit current asset text, and overwrite matching cached translations.";
		dictionary["取消当前正在运行的扫描或翻译任务；已写入缓存的结果会保留。"] = "Cancel the current scan or translation task; results already written to cache are kept.";
		dictionary["增加一个并行翻译密钥输入框，最多 2 个。"] = "Add an input for another parallel translation key, up to 2.";
		dictionary["删除此翻译密钥输入框，并保存其余密钥。"] = "Delete this translation-key input and save the remaining keys.";
		dictionary["使用“{0}”协议连接翻译服务。"] = "Use the “{0}” protocol to connect to the translation service.";
		dictionary["立即把当前世界时间切换到白天时段。"] = "Immediately set the current world time to daytime.";
		dictionary["立即把当前世界时间切换到夜晚时段。"] = "Immediately set the current world time to nighttime.";
		dictionary["冻结或恢复世界时间的自然流动。"] = "Freeze or resume the natural flow of world time.";
		dictionary["强制启用或关闭当前世界的满月状态。"] = "Force the current world's full-moon state on or off.";
		dictionary["在当前地图的可用空投节点立即触发一次空投。"] = "Trigger an airdrop immediately at an available airdrop node on the current map.";
		dictionary["使用当前地图的默认雨天资产立即开始降雨。"] = "Start rain immediately using the current map's default rain asset.";
		dictionary["使用当前地图的默认雪天资产立即开始暴雪。"] = "Start a blizzard immediately using the current map's default snow asset.";
		dictionary["结束当前天气效果，但保留后续自然天气调度。"] = "End the current weather effect while keeping future natural weather scheduling.";
		dictionary["结束当前天气并关闭后续自然天气调度。"] = "End the current weather and disable future natural weather scheduling.";
		dictionary["重新读取当前已加载的原版、地图和 Workshop 物品与载具资产。"] = "Reload the currently loaded official, map, and Workshop item and vehicle assets.";
		dictionary["查看并修改全局快捷键绑定。"] = "View and edit global shortcut bindings.";
		dictionary["查看插件版本、项目主页和支持链接。"] = "View the plugin version, project page, and support link.";
		dictionary["关闭无限储存窗口。"] = "Close the infinite storage window.";
		dictionary["切换界面语言并立即重建当前菜单。"] = "Switch the UI language and immediately rebuild the current menu.";
		dictionary["选择此菜单使用的界面字体。"] = "Choose the UI font used by this menu.";
		dictionary["关闭作弊菜单。"] = "Close the cheat menu.";
		dictionary["切换到“{0}”主页面。"] = "Switch to the “{0}” main page.";
		dictionary["关闭载具缩略图设置且不保存本次更改。"] = "Close vehicle thumbnail settings without saving these changes.";
		dictionary["选择 128 × 96 缩略图：生成最快、缓存占用最低。"] = "Choose 128 × 96 thumbnails: fastest to generate and lowest cache usage.";
		dictionary["选择 192 × 144 缩略图：在清晰度与生成开销之间平衡。"] = "Choose 192 × 144 thumbnails: balances clarity and generation cost.";
		dictionary["选择 256 × 192 缩略图：更清晰，但首次生成更慢。"] = "Choose 256 × 192 thumbnails: clearer, but slower on first generation.";
		dictionary["放弃本次缩略图尺寸和取景倍率更改。"] = "Discard these thumbnail size and framing-scale changes.";
		dictionary["保存缩略图尺寸和取景倍率；后续车辆卡片使用新设置。"] = "Save the thumbnail size and framing scale for future vehicle cards.";
		dictionary["关闭颜色选择器并放弃本次颜色更改。"] = "Close the color picker and discard this color change.";
		dictionary["放弃本次传送标记颜色更改。"] = "Discard this teleport marker color change.";
		dictionary["将当前预览颜色用于传送标记。"] = "Use the current preview color for teleport markers.";
		dictionary["字体"] = "Font";
		dictionary["选择界面字体"] = "Choose UI Font";
		dictionary["当前字体："] = "Current font: ";
		dictionary["仅显示能够完整呈现插件常用简体中文字符的已安装系统字体。"] = "Only installed system fonts that can render the plugin's common Simplified Chinese characters are shown.";
		dictionary["未找到支持中文的系统字体。"] = "No Chinese-capable system font was found.";
		dictionary["可用中文字体：{0} 个"] = "Chinese-capable fonts: {0}";
		dictionary["字体切换失败。"] = "The UI font could not be changed.";
		dictionary["界面字体已切换为："] = "UI font changed to: ";
		dictionary["界面字体已切换，但配置文件保存失败。"] = "The UI font changed for this session, but the configuration file could not be saved.";
		dictionary["作者：Blowthesnow"] = "Author: Blowthesnow";
		dictionary["此插件完全免费。如果你在其他地方购买了此插件，请立刻申请退款。"] = "This plugin is completely free. If you bought it somewhere else, please request a refund immediately.";
		dictionary["项目地址"] = "Project";
		dictionary["爱发电赞助地址"] = "Afdian Sponsorship";
		dictionary["关闭"] = "Close";
		dictionary["设置全局快捷键"] = "Set Global Shortcut";
		dictionary["当前动作："] = "Current action: ";
		dictionary["清除"] = "Clear";
		dictionary["世界"] = "World";
		dictionary["准星"] = "Crosshair";
		dictionary["生物生命条"] = "Creature Health Bars";
		dictionary["自动化"] = "Automation";
		dictionary["小地图开关"] = "Minimap Toggle";
		dictionary["小地图放大"] = "Minimap Zoom In";
		dictionary["小地图缩小"] = "Minimap Zoom Out";
		dictionary["固定组合"] = "Fixed Gesture";
		dictionary["Alt + 鼠标滚轮上"] = "Alt + Mouse Wheel Up";
		dictionary["Alt + 鼠标滚轮下"] = "Alt + Mouse Wheel Down";
		dictionary["打开或关闭菜单"] = "Toggle Menu";
		dictionary["当前快捷键："] = "Current shortcut: ";
		dictionary["点击“开始设置”，然后按下要使用的键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，可先按住 Ctrl / Alt / Shift / Win 作为组合键。"] = "Click “Start Setup”, then press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; hold Ctrl / Alt / Shift / Win first for a combination.";
		dictionary["开始设置"] = "Start Setup";
		dictionary["恢复默认"] = "Restore Default";
		dictionary["按键监听中，请按下键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，已按住的 Ctrl / Alt / Shift / Win 会作为组合键。"] = "Listening for a key. Press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; held Ctrl / Alt / Shift / Win keys will be used as modifiers.";
		dictionary["全局快捷键已更新。"] = "Global shortcut updated.";
		dictionary["全局快捷键已更新，但配置文件保存失败。"] = "Global shortcut updated, but the configuration file could not be saved.";
		dictionary["各模式功能介绍"] = "Mode Descriptions";
		dictionary["准星工具不可用"] = "Crosshair tool unavailable";
		dictionary["插件尚未完成交互服务初始化。"] = "The interaction service has not finished initializing.";
		dictionary["移动工具不可用"] = "Movement tools unavailable";
		dictionary["插件尚未完成移动服务初始化。"] = "The movement service has not finished initializing.";
		dictionary["切换飞行模式：在世界轴与视角指向之间切换。"] = "Switch flight mode between world-axis and view-direction movement.";
		dictionary["根据准星目标自动选择单一动作：维修、实用、准星传送、检查或 Shift + 中键删除。"] = "Automatically chooses one action for the crosshair target: repair, utility, crosshair teleport, inspect, or Shift + middle-click delete.";
		dictionary["查看准星指向对象的名称、ID / GUID、生命 / 耐久和位置等信息。"] = "Shows the crosshair target's name, ID / GUID, health / durability, position, and other details.";
		dictionary["维修准星指向的受损车辆、路障或建筑。"] = "Repairs a damaged vehicle, barricade, or structure under the crosshair.";
		dictionary["将玩家传送到准星指向的世界表面，并自动寻找安全落点。"] = "Teleports the player to the world surface under the crosshair and finds a safe landing point automatically.";
		dictionary["执行准星指向对象的原生交互，例如上车、打开容器或切换设备。"] = "Performs the target's native interaction, such as entering a vehicle, opening storage, or toggling a device.";
		dictionary["删除准星指向的可删除对象；必须同时按住 Shift 再点击中键。"] = "Deletes a removable target under the crosshair; hold Shift while clicking the middle mouse button.";
		dictionary["未进入地图"] = "No map loaded";
		dictionary["角色"] = "Player";
		dictionary["物品"] = "Items";
		dictionary["车辆"] = "Vehicles";
		dictionary["载具"] = "Vehicles";
		dictionary["收藏"] = "Favorites";
		dictionary["传送"] = "Teleports";
		dictionary["地图"] = "Map";
		dictionary["传送点"] = "Teleport Points";
		dictionary["标记"] = "Marker";
		dictionary["五角星"] = "Star";
		dictionary["正方形"] = "Square";
		dictionary["圆形"] = "Circle";
		dictionary["菱形"] = "Diamond";
		dictionary["颜色"] = "Color";
		dictionary["强调色"] = "Accent";
		dictionary["选择颜色"] = "Choose Color";
		dictionary["预览"] = "Preview";
		dictionary["颜色值"] = "Color Value";
		dictionary["常用颜色"] = "Common Colors";
		dictionary["通道"] = "Channels";
		dictionary["红"] = "Red";
		dictionary["绿"] = "Green";
		dictionary["蓝"] = "Blue";
		dictionary["选择仅在本次游戏会话生效的强调色；不会修改传送点颜色。"] = "Choose a session-only accent color without changing teleport marker colors.";
		dictionary["强调色已应用；仅在本次游戏会话生效。"] = "Accent color applied for this game session.";
		dictionary["关闭强调色选择器并放弃本次更改。"] = "Close the accent picker and discard this draft.";
		dictionary["仅本次游戏会话生效 · 不影响传送点颜色"] = "Session only · teleport marker color stays independent";
		dictionary["默认蓝色"] = "Default Blue";
		dictionary["把强调色草稿恢复为默认 Fluent 蓝。"] = "Restore the Fluent blue accent draft.";
		dictionary["放弃本次强调色更改。"] = "Discard this session accent draft.";
		dictionary["把当前颜色用于本次会话的强调、焦点、导航选中和滑块。"] = "Apply this color to accent, focus, navigation selection, and sliders for this session.";
		dictionary["把此预设颜色载入强调色预览。"] = "Load this preset into the session accent preview.";
		dictionary["已切换到地图视图。"] = "Switched to map view.";
		dictionary["已切换到传送点视图。"] = "Switched to teleport points view.";
		dictionary["玩家状态尚未准备完成。"] = "Player state is not ready yet.";
		dictionary["生存状态"] = "Survival Stats";
		dictionary["无敌模式"] = "God Mode";
		dictionary["无敌模式已开启。"] = "God mode enabled.";
		dictionary["无敌模式已关闭。"] = "God mode disabled.";
		dictionary["无限生存状态"] = "Infinite Survival Stats";
		dictionary["无限生存状态已开启。"] = "Infinite survival stats enabled.";
		dictionary["无限生存状态已关闭。"] = "Infinite survival stats disabled.";
		dictionary["恢复角色状态"] = "Restore Player State";
		dictionary["设置指定数值（0–100；生命最低 1）"] = "Set exact values (0–100; health minimum 1)";
		dictionary["生命"] = "Health";
		dictionary["饱食"] = "Food";
		dictionary["水分"] = "Water";
		dictionary["免疫"] = "Immunity";
		dictionary["体力"] = "Stamina";
		dictionary["氧气"] = "Oxygen";
		dictionary["应用"] = "Apply";
		dictionary["角色生存数值已设置。"] = "Player survival stats updated.";
		dictionary["设置失败。"] = "Update failed.";
		dictionary["全部恢复"] = "Restore All";
		dictionary["角色状态已全部恢复。"] = "All player stats restored.";
		dictionary["恢复失败。"] = "Restore failed.";
		dictionary["补满生存状态"] = "Refill Survival Stats";
		dictionary["补满失败。"] = "Refill failed.";
		dictionary["生存状态已补满。"] = "Survival stats refilled.";
		dictionary["操作失败。"] = "Action failed.";
		dictionary["治疗流血/骨折"] = "Heal Bleeding / Fracture";
		dictionary["伤势已治疗。"] = "Injuries healed.";
		dictionary["经验、声望与技能"] = "Experience, Reputation & Skills";
		dictionary["经验"] = "Experience";
		dictionary["增加经验"] = "Add Experience";
		dictionary["增加经验失败。"] = "Failed to add experience.";
		dictionary["声望"] = "Reputation";
		dictionary["变更声望"] = "Change Reputation";
		dictionary["变更声望失败。"] = "Failed to change reputation.";
		dictionary["全部技能满级"] = "Max All Skills";
		dictionary["搜索"] = "Search";
		dictionary["数量"] = "Amount";
		dictionary["筛选"] = "Filter";
		dictionary["清除筛选"] = "Clear Filters";
		dictionary["全部来源"] = "All Sources";
		dictionary["物品类型"] = "Item Type";
		dictionary["来源"] = "Source";
		dictionary["高稀有度优先（神话→普通）"] = "High Rarity First (Mythical to Common)";
		dictionary["稀有度"] = "Rarity";
		dictionary["装备槽位"] = "Equipment Slot";
		dictionary["射击机制"] = "Firing Mechanism";
		dictionary["射击模式（可多选）"] = "Fire Mode (multi-select)";
		dictionary["重置"] = "Reset";
		dictionary["枪械"] = "Gun";
		dictionary["近战"] = "Melee";
		dictionary["投掷物"] = "Throwable";
		dictionary["弹匣"] = "Magazine";
		dictionary["瞄具"] = "Sight";
		dictionary["战术配件"] = "Tactical";
		dictionary["握把"] = "Grip";
		dictionary["枪管"] = "Barrel";
		dictionary["光学配件"] = "Optic";
		dictionary["帽子"] = "Hat";
		dictionary["上衣"] = "Shirt";
		dictionary["裤子"] = "Pants";
		dictionary["背包"] = "Backpack";
		dictionary["背心"] = "Vest";
		dictionary["面罩"] = "Mask";
		dictionary["眼镜"] = "Glasses";
		dictionary["饮料"] = "Drinks";
		dictionary["滤芯"] = "Filter";
		dictionary["建筑"] = "Structure";
		dictionary["路障"] = "Barricade";
		dictionary["储物"] = "Storage";
		dictionary["陷阱"] = "Trap";
		dictionary["农作物"] = "Farm";
		dictionary["种植器"] = "Grower";
		dictionary["发电机"] = "Generator";
		dictionary["油泵"] = "Oil Pump";
		dictionary["信标"] = "Beacon";
		dictionary["哨戒炮"] = "Sentry";
		dictionary["通用工具"] = "Tool";
		dictionary["燃料"] = "Fuel";
		dictionary["容器"] = "Refill";
		dictionary["钓具"] = "Fishing Gear";
		dictionary["轮胎"] = "Tire";
		dictionary["维修工具"] = "Vehicle Repair Tool";
		dictionary["喷漆工具"] = "Vehicle Paint Tool";
		dictionary["开锁工具"] = "Vehicle Lockpick Tool";
		dictionary["地图 / 其他"] = "Map / Other";
		dictionary["普通"] = "Common";
		dictionary["罕见"] = "Uncommon";
		dictionary["稀有"] = "Rare";
		dictionary["史诗"] = "Epic";
		dictionary["传奇"] = "Legendary";
		dictionary["神话"] = "Mythical";
		dictionary["快捷栏"] = "Hotkey";
		dictionary["主武器"] = "Primary";
		dictionary["副武器"] = "Secondary";
		dictionary["第三槽位"] = "Tertiary";
		dictionary["任意槽位"] = "Any";
		dictionary["常规枪械"] = "Trigger";
		dictionary["栓动"] = "Bolt";
		dictionary["泵动"] = "Pump";
		dictionary["轨道武器"] = "Rail";
		dictionary["弓 / 弩"] = "Bow / Crossbow";
		dictionary["折管"] = "Break-Action";
		dictionary["火箭 / 发射器"] = "Rocket / Launcher";
		dictionary["旋转机枪"] = "Minigun";
		dictionary["半自动"] = "Semi-Automatic";
		dictionary["全自动"] = "Automatic";
		dictionary["点射"] = "Burst";
		dictionary["⚙ 渲染设置"] = "⚙ Render Settings";
		dictionary["载具渲染设置"] = "Vehicle Render Settings";
		dictionary["尺寸"] = "Size";
		dictionary["取景倍率"] = "Framing";
		dictionary["128 × 96（低开销）"] = "128 × 96 (Low Cost)";
		dictionary["192 × 144（平衡）"] = "192 × 144 (Balanced)";
		dictionary["256 × 192（更清晰）"] = "256 × 192 (Sharper)";
		dictionary["缩略图尺寸只影响车辆卡片，不会修改游戏全局画质。已生成的缩略图会进入缓存，重复打开不会重复实例化车辆。"] = "Thumbnail size only affects vehicle cards and does not change global game quality. Generated thumbnails are cached, so reopening does not instantiate vehicles again.";
		dictionary["128 × 96 为默认，生成更快且占用更低；192 × 144 是清晰度与开销的折中；256 × 192 的输出像素约为默认的 4 倍，首次生成更慢。"] = "128 × 96 is the default with faster generation and lower usage; 192 × 144 balances clarity and cost; 256 × 192 produces about 4x the default pixels and takes longer the first time.";
		dictionary["取景倍率 0.5：主体更大、留白更少，可能接近裁切边界；1.0：默认平衡值；1.5：留白更多、主体更小，裁切风险更低。"] = "Framing 0.5: larger subject and less padding, closer to the crop boundary; 1.0: balanced default; 1.5: more padding, smaller subject, and lower crop risk.";
		dictionary["车辆缩略图设置已应用并保存。"] = "Vehicle thumbnail settings applied and saved.";
		dictionary["车辆缩略图设置已应用，但配置文件保存失败。"] = "Vehicle thumbnail settings applied, but the config file could not be saved.";
		dictionary["重新扫描"] = "Rescan";
		dictionary["已重新扫描当前加载的原版与模组资产。"] = "Rescanned the currently loaded vanilla and mod assets.";
		dictionary["物品分类"] = "Item Categories";
		dictionary["车辆分类"] = "Vehicle Categories";
		dictionary["收藏物品分类"] = "Favorite Items";
		dictionary["收藏车辆分类"] = "Favorite Vehicles";
		dictionary["当前分类没有已加载的收藏物品。"] = "No loaded favorite items in this category.";
		dictionary["当前分类没有已加载的收藏车辆。"] = "No loaded favorite vehicles in this category.";
		dictionary["请在物品卡片右上角点击 ☆ 收藏。"] = "Click ☆ on an item card to add one.";
		dictionary["请在车辆卡片右上角点击 ☆ 收藏。"] = "Click ☆ on a vehicle card to add one.";
		dictionary["当前分类没有可用物品。"] = "No items are available in this category.";
		dictionary["没有匹配的物品。"] = "No items matched.";
		dictionary["当前分类没有可用车辆。"] = "No vehicles are available in this category.";
		dictionary["没有匹配的车辆。"] = "No vehicles matched.";
		dictionary["请选择其他分类，或重新扫描当前加载的资产。"] = "Choose another category or rescan the currently loaded assets.";
		dictionary["请清空搜索或尝试其他关键词。"] = "Clear the search or try another keyword.";
		dictionary["‹ 上一页"] = "‹ Previous";
		dictionary["下一页 ›"] = "Next ›";
		dictionary["保存当前位置"] = "Save Current Position";
		dictionary["保存位置"] = "Save Position";
		dictionary["当前地图 GPS"] = "Current Map GPS";
		dictionary["GPS/卫星图"] = "GPS / Satellite";
		dictionary["地形图"] = "Terrain Chart";
		dictionary["地图预览"] = "Map Preview";
		dictionary["扫描地图"] = "Scan Map";
		dictionary["居中"] = "Center";
		dictionary["当前地图没有可用的 Map.png / Chart.png。"] = "The current map has no usable Map.png / Chart.png.";
		dictionary["当前地图没有可用 GPS 图层。"] = "No usable GPS map layer is available for the current map.";
		dictionary["请确认地图目录中存在 Map.png 或 Chart.png。"] = "Make sure the map folder contains Map.png or Chart.png.";
		dictionary["当前地图还没有保存的传送点。"] = "No saved teleports for the current map.";
		dictionary["请先输入名称并保存当前位置；不同地图的传送点会自动隔离。"] = "Enter a name and save the current position first. Teleports are isolated per map.";
		dictionary["进入地图后即可保存和使用传送点。"] = "Enter a map to save and use teleport points.";
		dictionary["左键拖动 · 滚轮缩放 · 悬停标记查看信息 · 右键标记快速传送"] = "Left-drag to pan · scroll to zoom · hover markers for details · right-click a marker to teleport";
		dictionary["地图传送失败：未找到安全落点、目标超出地图边界或玩家正在载具中。"] = "Map teleport failed: no safe landing point was found, the destination is outside the map, or the player is in a vehicle.";
		dictionary["传送失败：目标位置有障碍物且无法放置在障碍物顶部，或玩家正在载具中。"] = "Teleport failed: the target is obstructed and a top placement was not available, or the player is in a vehicle.";
		dictionary["取消"] = "Cancel";
		dictionary["确定"] = "Confirm";
		dictionary["删除"] = "Delete";
		dictionary["还没有保存的传送点。"] = "No teleports saved yet.";
		dictionary["确认删除传送点？"] = "Delete this teleport?";
		dictionary["其他地图"] = "Other Map";
		dictionary["传送失败：目标被阻挡、地图不同或玩家正在载具中。"] = "Teleport failed: the destination is blocked, on another map, or the player is in a vehicle.";
		dictionary["时间"] = "Time";
		dictionary["时间百分比"] = "Time Percentage";
		dictionary["设为白天"] = "Set Day";
		dictionary["已切换到白天。"] = "Switched to daytime.";
		dictionary["设为夜晚"] = "Set Night";
		dictionary["已切换到夜晚。"] = "Switched to nighttime.";
		dictionary["冻结时间"] = "Freeze Time";
		dictionary["时间已冻结。"] = "Time frozen.";
		dictionary["时间已恢复流动。"] = "Time resumed.";
		dictionary["强制满月"] = "Force Full Moon";
		dictionary["已开启满月。"] = "Full moon enabled.";
		dictionary["已关闭满月。"] = "Full moon disabled.";
		dictionary["世界事件"] = "World Events";
		dictionary["立即呼叫空投"] = "Call Airdrop Now";
		dictionary["空投已呼叫。"] = "Airdrop requested.";
		dictionary["空投呼叫失败。"] = "Airdrop request failed.";
		dictionary["此地图没有可用的空投节点或货物表。"] = "This map has no usable airdrop node or loot table.";
		dictionary["下雨"] = "Rain";
		dictionary["已触发雨天。"] = "Rain triggered.";
		dictionary["当前地图没有默认雨天资产。"] = "This map has no default rain asset.";
		dictionary["暴雪"] = "Snowstorm";
		dictionary["已触发暴雪。"] = "Snowstorm triggered.";
		dictionary["当前地图没有默认雪天资产。"] = "This map has no default snow asset.";
		dictionary["清除当前天气"] = "Clear Current Weather";
		dictionary["当前天气已清除，后续仍可自然调度。"] = "Current weather cleared; natural scheduling remains enabled.";
		dictionary["关闭天气调度"] = "Disable Weather Scheduling";
		dictionary["当前天气和自然天气调度已关闭。"] = "Current weather and natural weather scheduling disabled.";
		dictionary["重新扫描模组资产"] = "Rescan Mod Assets";
		dictionary["资产目录已重新扫描。"] = "Asset catalog rescanned.";
		dictionary["扫描信息"] = "Scan Information";
		dictionary["只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。"] = "Only vanilla, map-provided, and Workshop assets successfully loaded by Unturned are shown.";
		dictionary["输入…"] = "Enter text…";
		dictionary["加载预览…"] = "Loading preview…";
		dictionary["无预览"] = "No Preview";
		dictionary["模组"] = "Mod";
		dictionary["原版"] = "Vanilla";
		dictionary["未知来源"] = "Unknown Source";
		dictionary["尚未进入世界。"] = "No world is loaded yet.";
		dictionary["此菜单只允许在真正的单人世界中使用。"] = "This menu is restricted to true singleplayer worlds.";
		dictionary["本地玩家尚未生成。"] = "The local player has not spawned yet.";
		dictionary["独立专用服务器不运行客户端玩法功能。"] = "Standalone dedicated servers do not run client gameplay features.";
		dictionary["当前会话不支持玩法功能。"] = "The current session does not support gameplay features.";
		dictionary["无法识别当前会话，玩法功能已停止。"] = "The current session is unknown, so gameplay features are stopped.";
		dictionary["Snapshot 无限储存仅在真正的单人世界中可用。"] = "Snapshot infinite storage is available only in a true singleplayer world.";
		dictionary["当前没有获得玩法授权的本地玩家库存。"] = "No gameplay-authorized local player inventory is available.";
		dictionary["当前没有可用的本地玩家库存或玩法授权。"] = "No local player inventory or gameplay authorization is available.";
		dictionary["撤销已失效：连接会话已经变化。"] = "Undo expired because the connection session changed.";
		dictionary["玩法授权未就绪"] = "Gameplay authorization is not ready";
		dictionary["打开或关闭菜单。默认 End；本地玩家生成后可用。"] = "Open or close the menu. Defaults to End and is available after the local player spawns.";
		dictionary["菜单缩放，范围 0.75 到 1.5。"] = "Menu scale from 0.75 to 1.5.";
			dictionary["插件窗口"] = "Plugin Window";
		dictionary["当前分类："] = "Current category:";
		dictionary["可编辑动作"] = "Editable actions";
		dictionary["点击某一行的设置后按下键盘键、鼠标中键或鼠标侧键；Escape 取消。"] = "Click Set on an action, then press a keyboard key, middle mouse button, or side mouse button. Escape cancels.";
		dictionary["设置"] = "Set";
		dictionary["默认"] = "Default";
		dictionary["背包整理"] = "Inventory Arrange";
		dictionary["排序预设"] = "Sort Preset";
		dictionary["整理范围"] = "Arrange Scope";
		dictionary["配置随身页 ▼"] = "Configure Carried Pages ▼";
		dictionary["分别开启或关闭背包、背心、上衣和裤子储物页；关闭的页面不会参与整理。"] = "Configure the backpack, vest, shirt, and pants storage pages; disabled pages are excluded from arranging.";
		dictionary["选择参与整理的随身页"] = "Choose carried pages to arrange";
		dictionary["背包"] = "Backpack";
		dictionary["背心"] = "Vest";
		dictionary["上衣"] = "Shirt";
		dictionary["裤子"] = "Pants";
		dictionary["背包页已纳入整理范围。"] = "The backpack page is included in the arrange scope.";
		dictionary["背包页已排除出整理范围。"] = "The backpack page is excluded from the arrange scope.";
		dictionary["背心页已纳入整理范围。"] = "The vest page is included in the arrange scope.";
		dictionary["背心页已排除出整理范围。"] = "The vest page is excluded from the arrange scope.";
		dictionary["上衣页已纳入整理范围。"] = "The shirt page is included in the arrange scope.";
		dictionary["上衣页已排除出整理范围。"] = "The shirt page is excluded from the arrange scope.";
		dictionary["裤子页已纳入整理范围。"] = "The pants page is included in the arrange scope.";
		dictionary["裤子页已排除出整理范围。"] = "The pants page is excluded from the arrange scope.";
		dictionary["开启后整理背包储物页；关闭后该页物品和位置保持不变。"] = "When enabled, arrange the backpack storage page; when disabled, leave its items and positions unchanged.";
		dictionary["开启后整理背心储物页；关闭后该页物品和位置保持不变。"] = "When enabled, arrange the vest storage page; when disabled, leave its items and positions unchanged.";
		dictionary["开启后整理上衣储物页；关闭后该页物品和位置保持不变。"] = "When enabled, arrange the shirt storage page; when disabled, leave its items and positions unchanged.";
		dictionary["开启后整理裤子储物页；关闭后该页物品和位置保持不变。"] = "When enabled, arrange the pants storage page; when disabled, leave its items and positions unchanged.";
		dictionary["点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。"] = "Choose an arrange preset directly from the list. Hover an option to read what the preset does.";
		dictionary["安全背包整理"] = "Safe Inventory Arrange";
		dictionary["安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。"] = "Safety: cancel the whole operation when preflight cannot place every item; automatic dropping is never used.";
		dictionary["跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。"] = "Cross-container concentration: while a storage box is open, hold Ctrl or Shift and click an item to transfer every instance of the same asset to the other side; it stops when space runs out and never drops items.";
		dictionary["保护装备"] = "Protect Equipped";
		dictionary["固定热键物品"] = "Fix Hotkey Items";
		dictionary["撤销上次整理"] = "Undo Last Arrange";
		dictionary["仅归并"] = "Merge Only";
		dictionary["仅压缩"] = "Compact Only";
		dictionary["快捷键可在窗口右下角的全局快捷键中设置。"] = "Shortcuts can be configured in the global shortcut window at the bottom-right of the menu.";
		dictionary["自动旋转"] = "Auto Rotate";
		dictionary["允许跨页"] = "Allow Cross-Page";
		dictionary["预览当前"] = "Preview Current";
		dictionary["整理当前"] = "Arrange Current";
		dictionary["整理全部随身"] = "Arrange All Carried";
		dictionary["整理将允许自动旋转物品。"] = "Arrange will allow automatic item rotation.";
		dictionary["整理已禁止自动旋转物品。"] = "Arrange will not rotate items automatically.";
		dictionary["整理将允许在当前范围内跨页安置。"] = "Arrange may place items on another page in the current scope.";
		dictionary["整理将保持原页范围。"] = "Arrange will keep items on their original pages.";
		dictionary["已保护当前装备物品。"] = "Equipped items are protected.";
		dictionary["将允许整理当前装备物品。"] = "Arrange may move equipped items.";
		dictionary["热键绑定物品将保持在原坐标。"] = "Hotkey-bound items will remain at their original coordinates.";
		dictionary["热键绑定物品会随整理移动并更新坐标。"] = "Hotkey-bound items will move with arrange and update their coordinates.";
		dictionary["开启时固定绑定热键的物品；关闭时整理会同步快捷键到新坐标。"] = "When on, hotkey-bound items stay fixed; when off, arrange updates shortcuts to their new coordinates.";
		dictionary["没有可撤销的整理记录。"] = "There is no arrange operation to undo.";
		dictionary["自定义排序链（最多 5 项）：Key:Direction,Key:Direction"] = "Custom sort chain (up to 5 keys): Key:Direction,Key:Direction";
		dictionary["弹药管理"] = "Ammo Management";
		dictionary["背包整理尚未准备完成。"] = "Inventory arrange is not ready.";
		dictionary["物品/车辆每页卡片数量，范围 12 到 80。"] = "Item/vehicle cards per page from 12 to 80.";
		dictionary["点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。"] = "Choose an arrange preset directly from the list. Hover an option to read what the preset does.";
		dictionary["允许物品在寻找位置时旋转 90°；关闭后保持原旋转。"] = "Allow items to rotate 90 degrees while finding space; when off, keep their original rotation.";
		dictionary["允许在当前整理范围的随身页之间寻找空位；关闭后保持原页范围。"] = "Search other carried pages in the current arrange scope; when off, keep the original page.";
		dictionary["保护当前装备物品；开启时不会移除或重排它。"] = "Protect the currently equipped item; it will not be removed or rearranged.";
		dictionary["保护绑定热键的物品，避免整理后快捷栏失效。"] = "Protect hotkey-bound items so the hotbar remains valid after arranging.";
		dictionary["只生成移动、归并和跨页摘要，不修改库存。"] = "Show move, merge, and cross-page counts without changing the inventory.";
		dictionary["整理当前上下文：打开储物箱时整理储物箱，否则整理随身容器。"] = "Arrange the current context: storage when a box is open, otherwise carried containers.";
		dictionary["整理所有随身页，不处理当前储物箱；默认保护装备和热键。"] = "Arrange all carried pages, excluding the open storage; equipped and hotkey items are protected by default.";
		dictionary["只压缩空间并保持相对顺序；不按普通预设重排，自动补弹仍会执行。"] = "Compact space while keeping relative order; no normal preset reorder, but automatic ammo redistribution still runs.";
		dictionary["撤销最近一次成功整理；外部改变库存后撤销会失效。"] = "Undo the latest successful arrange; undo expires after external inventory changes.";
		dictionary["只归并兼容堆叠，不重排无关物品；只按相同容器身份或直接 FillTargetItem 来源→目标关系补弹。"] = "Merge compatible stacks without reordering unrelated items; refill ammo only within identical container identities or direct FillTargetItem source-to-target relations.";
		dictionary["整理计划改变了物品实例、品质、状态或容器容量，已取消。"] = "The arrange plan changed item instances, quality, state, or container capacity, so it was cancelled.";
		dictionary["整理计划未保持弹药总量，已取消。"] = "The arrange plan did not preserve total ammo, so it was cancelled.";
		dictionary["整理计划包含无效弹药转移记录，已取消。"] = "The arrange plan contains invalid ammo-transfer ledger entries, so it was cancelled.";
		dictionary["整理计划尝试使用未获直接 FillTargetItem 授权的来源，或让桥接容器跨资产外流，已取消。"] = "The arrange plan attempted an unconfirmed FillTargetItem source or cross-asset bridge-container outflow, so it was cancelled.";
		dictionary["整理计划复用了本轮刚收到的弹药或超出来源快照预算，已取消。"] = "The arrange plan reused rounds received during this operation or exceeded the frozen source budget, so it was cancelled.";
		dictionary["整理计划的跨资产弹量变化与逐笔有向转移记录不一致，已取消。"] = "Cross-asset ammo changes do not match the directed transfer ledger, so the arrange plan was cancelled.";
		dictionary["整理计划改变了弹药资源来源的身份或扣减量，已取消。"] = "The arrange plan changed an ammo-resource source identity or deducted the wrong amount, so it was cancelled.";
		dictionary["按类型、占用格数和名称整理，适合平时的一键整理。"] = "Sort by type, occupied space, and name for everyday one-click arranging.";
		dictionary["尽量把物品挤到前面，保持原来的先后顺序；每次整理仍会按直接 FillTargetItem 关系安全补弹。"] = "Push items forward while keeping their order; every arrange safely refills ammo through direct FillTargetItem relations.";
		dictionary["把同类物品放在一起，例如武器、衣服、食物和工具。"] = "Put similar items together, such as weapons, clothing, food, and tools.";
		dictionary["按物品名称从 A 到 Z 排列，方便按名字找东西。"] = "Sort item names from A to Z so they are easier to find.";
		dictionary["先放大件，再用小件填空位，适合整理占格较大的物品。"] = "Place large items first, then fill gaps with small items.";
		dictionary["先放小件，尽量留下连续的大空位。"] = "Place small items first to keep larger continuous spaces open.";
		dictionary["耐久或品质高的物品排在前面。"] = "Put items with higher quality or durability first.";
		dictionary["按稀有度从神话、传奇、史诗、稀有、罕见到普通排列。"] = "Sort rarity from Mythical, Legendary, Epic, Rare, Uncommon, to Common.";
		dictionary["相同物品会自动合堆；兼容的半弹匣或弹药箱会重新分配成满的和空的。"] = "Merge matching stacks automatically; compatible half magazines or ammo boxes become full and empty containers.";
		dictionary["剩余弹量多的弹匣或弹药箱排在前面；读不到弹量的物品不会乱改。"] = "Put magazines or ammo boxes with more ammo first; unreadable ammo is left unchanged.";
		dictionary["绑定快捷栏的物品优先；开启固定热键物品时，绑定物品不会被移动。"] = "Prioritize hotbar-bound items; with Fix Hotkey Items on, bound items are not moved.";
		dictionary["整理所有随身页，不处理当前储物箱；默认保护装备并固定热键物品。"] = "Arrange all carried pages without touching the open storage; protect equipped items and fix hotkey items by default.";
		dictionary["尽量保持原来的页、位置和旋转，只做必要的整理。"] = "Keep the original page, position, and rotation whenever possible.";
		dictionary["按下面输入框里的排序链执行，例如 Name:Descending,Area:Ascending。"] = "Use the sort chain in the input below, for example Name:Descending,Area:Ascending.";
		dictionary["按安全的默认规则整理。"] = "Arrange using the safe default rules.";
		dictionary["翻译"] = "Translation";
		dictionary["Workshop 翻译"] = "Workshop Translation";
		dictionary["API 设置"] = "API Settings";
		dictionary["Endpoint"] = "Endpoint";
		dictionary["API Key"] = "API Key";
		dictionary["Model"] = "Model";
		dictionary["API Key / App ID"] = "API Key / App ID";
		dictionary["Secret Key"] = "Secret Key";
		dictionary["Region"] = "Region";
		dictionary["Project ID"] = "Project ID";
		dictionary["Model（大模型）"] = "Model (large model)";
		dictionary["接口地址"] = "Endpoint";
		dictionary["API Key / 应用 ID"] = "API Key / App ID";
		dictionary["密钥 / Secret Key"] = "Secret Key";
		dictionary["区域"] = "Region";
		dictionary["项目 ID"] = "Project ID";
		dictionary["模型名称"] = "Model";
		dictionary["开始翻译（未缓存文本）"] = "Start translation (uncached text)";
		dictionary["请求发送到哪里。大模型通常是 Chat/Message 地址；云翻译使用供应商的 REST 地址。可保留默认值，也可以改成代理地址。"] = "Where requests are sent. Large models usually use a Chat/Message URL; cloud translation uses the provider REST URL. You can keep the default or use a proxy.";
		dictionary["云翻译供应商发给你的访问密钥或应用 ID。"] = "The access key or application ID issued by the cloud translation provider.";
		dictionary["云翻译供应商发给你的访问密钥或应用 ID。最多添加 2 个，并按密钥并行翻译。"] = "The access key or application ID issued by the cloud translation provider. Add up to two keys to translate in parallel.";
		dictionary["大模型平台发给你的 API Key；不会写入日志。\nOllama 本地服务通常不需要填写。"] = "The API key issued by the large-model platform; it is never logged.\nLocal Ollama usually does not need one.";
		dictionary["大模型平台发给你的 API Key；不会写入日志。最多添加 2 个，并按密钥并行翻译。\nOllama 本地服务通常不需要填写。"] = "The API key issued by the large-model platform; it is never logged. Add up to two keys to translate in parallel.\nLocal Ollama usually does not need one.";
		dictionary["增加一个并行翻译密钥，最多 2 个。"] = "Add another parallel translation key, up to two.";
		dictionary["删除此翻译密钥输入框。"] = "Remove this translation key field.";
		dictionary["部分云翻译供应商需要的第二段密钥：腾讯云 SecretKey、百度 Secret Key 或有道密钥。"] = "The second credential required by some cloud providers: Tencent SecretKey, Baidu Secret Key, or Youdao secret.";
		dictionary["云服务资源所在区域。例如腾讯云 ap-guangzhou、微软 Translator 的资源区域。"] = "The cloud resource region, such as Tencent ap-guangzhou or the Microsoft Translator resource region.";
		dictionary["云项目或资源的数字/字符串 ID；没有要求的供应商可以留空。"] = "The cloud project or resource ID; leave it empty when the provider does not require one.";
		dictionary["大模型平台上的模型名称，例如 deepseek-chat、gpt-4o-mini、claude-3-5-sonnet。云机器翻译通常不需要填写。"] = "The model name on the large-model platform, such as deepseek-chat, gpt-4o-mini, or claude-3-5-sonnet. Cloud translation usually does not need this.";
		dictionary["先扫描当前已加载的 Workshop 资产，再翻译缓存中没有的文本；已缓存文本不会重复请求。"] = "Scans currently loaded Workshop assets first, then translates only text missing from the cache; cached text is not requested again.";
		dictionary["腾讯云 TMT"] = "Tencent Cloud TMT";
		dictionary["百度翻译"] = "Baidu Translate";
		dictionary["OpenAI 兼容"] = "OpenAI-compatible";
		dictionary["OpenAI"] = "OpenAI";
		dictionary["Azure OpenAI"] = "Azure OpenAI";
		dictionary["Anthropic"] = "Anthropic";
		dictionary["Google Gemini"] = "Google Gemini";
		dictionary["Ollama"] = "Ollama";
		dictionary["OpenRouter"] = "OpenRouter";
		dictionary["DeepSeek"] = "DeepSeek";
		dictionary["SiliconFlow"] = "SiliconFlow";
		dictionary["LM Studio"] = "LM Studio";
		dictionary["腾讯混元"] = "Tencent Hunyuan";
		dictionary["百度千帆"] = "Baidu Qianfan";
		dictionary["Workshop 模组物品翻译"] = "Workshop Mod Item Translation";
		dictionary["只读取当前已加载资产；不会修改 Workshop 原始文件、.dat 或 AssetBundle。"] = "Reads only currently loaded assets; it does not modify Workshop files, .dat files, or AssetBundles.";
		dictionary["翻译服务不可用"] = "Translation service unavailable";
		dictionary["插件尚未完成 Workshop 翻译服务初始化。"] = "The Workshop translation service has not finished initializing.";
		dictionary["启用缓存译文"] = "Enable cached translations";
		dictionary["关闭后立即回退到原始 Getter 文本。"] = "Turning this off immediately falls back to original Getter text.";
		dictionary["应用到原生显示"] = "Apply to native display";
		dictionary["覆盖 ItemAsset、VehicleAsset 和 Workshop Action 的显示 Getter。"] = "Overrides display Getters for ItemAsset, VehicleAsset, and Workshop Actions.";
		dictionary["资产刷新后自动扫描"] = "Scan automatically after asset refresh";
		dictionary["自动翻译未缓存"] = "Translate uncached text automatically";
		dictionary["需要已配置 API；默认关闭以避免意外费用。"] = "Requires a configured API; disabled by default to avoid unexpected charges.";
		dictionary["扫描并应用缓存"] = "Scan and apply cache";
		dictionary["重新加载缓存"] = "Reload cache";
		dictionary["翻译未缓存文本"] = "Translate uncached text";
		dictionary["强制重译当前文本"] = "Force retranslate current text";
		dictionary["取消"] = "Cancel";
		dictionary["Workshop 未缓存文本翻译已启动。"] = "Workshop uncached-text translation started.";
		dictionary["未能启动 Workshop 翻译，请检查 API 配置或当前任务状态。"] = "Could not start Workshop translation; check API configuration or the current task state.";
		dictionary["Workshop 强制重译已启动。"] = "Workshop force retranslation started.";
		dictionary["未能启动 Workshop 强制重译，请检查 API 配置或当前任务状态。"] = "Could not start Workshop force retranslation; check API configuration or the current task state.";
		dictionary["扫描中"] = "Scanning";
		dictionary["翻译中"] = "Translating";
		dictionary["已取消"] = "Cancelled";
		dictionary["错误"] = "Error";
		dictionary["空闲"] = "Idle";
		dictionary["已配置"] = "Configured";
		dictionary["未配置"] = "Not configured";
		dictionary["未填写"] = "Not set";
		dictionary[" 的收藏状态。"] = " favorite state.";
		dictionary["。支持 Map.png / Chart.png。"] = ". Supports Map.png / Chart.png.";
		dictionary["；供应商："] = "; provider:";
		dictionary["；密钥："] = "; keys:";
		dictionary["；并行："] = "; parallel workers:";
		dictionary[" 路；目标语言："] = "; target language:";
		dictionary["；模型："] = "; model:";
		dictionary["；默认禁止自动掉落。"] = "; automatic dropping is disabled by default.";
		dictionary["；目标语言："] = "; target language:";
		dictionary["”将从当前保存列表中删除。"] = "\" will be removed from the saved list.";
		dictionary["Secret Key / 密钥"] = "Secret Key";
		dictionary["WASD 移动，Space 上升，Ctrl 下降。"] = "WASD moves, Space ascends, and Ctrl descends.";
		dictionary["穿墙"] = "Noclip";
		dictionary["穿墙已关闭。"] = "Noclip disabled.";
		dictionary["穿墙已开启。"] = "Noclip enabled.";
		dictionary["垂直速度"] = "Vertical Speed";
		dictionary["当前没有正在运行的翻译任务。"] = "No translation task is currently running.";
		dictionary["翻译接口："] = "Translation API:";
		dictionary["翻译任务已取消。"] = "Translation task cancelled.";
		dictionary["翻译正在进行中，请不要重复点击开始翻译。"] = "Translation is already running; do not click Start again.";
		dictionary["飞行"] = "Flight";
		dictionary["飞行已关闭。"] = "Flight disabled.";
		dictionary["飞行已开启。"] = "Flight enabled.";
		dictionary["工具页"] = "Tools Page";
		dictionary["关闭穿墙后寻找安全位置"] = "Find a safe position after disabling noclip";
		dictionary["缓存文件："] = "Cache file:";
		dictionary["检查"] = "Inspect";
		dictionary["交互"] = "Interaction";
		dictionary["开始翻译（未缓存）"] = "Start Translation (Uncached)";
		dictionary["每次发送给翻译接口的文本数量，范围 1-100。数量越小越稳，但请求次数更多；修改后下次翻译任务生效。"] = "Texts sent per translation request, from 1 to 100. Smaller batches are more reliable but require more requests; changes apply to the next task.";
		dictionary["每批翻译数量"] = "Texts Per Batch";
		dictionary["名称"] = "Name";
		dictionary["排序预设："] = "Sort Preset:";
		dictionary["启用准星交互工具"] = "Enable Point Interaction Tool";
		dictionary["准星工具"] = "Crosshair Tool";
		dictionary["准星工具模式"] = "Crosshair Tool Mode";
		dictionary["准星工具已开启。"] = "Crosshair tool enabled.";
		dictionary["准星工具已关闭。"] = "Crosshair tool disabled.";
		dictionary["生物头顶血条"] = "Creature Health Bars";
		dictionary["生物生命条已开启。"] = "Creature health bars enabled.";
		dictionary["生物生命条已关闭。"] = "Creature health bars disabled.";
		dictionary["准心目标或实际受伤后的生物会显示当前生命 / 生命上限。"] = "Crosshair targets or recently damaged creatures show current health / maximum health.";
		dictionary["生物头顶血条已开启。"] = "Creature health bars enabled.";
		dictionary["生物头顶血条已关闭。"] = "Creature health bars disabled.";
		dictionary["扫描当前已加载资产后，翻译缓存中没有的文本；填写配置后点击这里开始。"] = "Scan currently loaded assets and translate text missing from the cache; configure the API, then click here to start.";
		dictionary["生命 / 耐久"] = "Health / Durability";
		dictionary["实用"] = "Utility";
		dictionary["水平速度"] = "Horizontal Speed";
		dictionary["停止当前翻译任务；已经写入缓存的译文会保留。"] = "Stop the current translation task; translations already written to the cache are kept.";
		dictionary["停止翻译"] = "Stop Translation";
		dictionary["完成"] = "Complete";
		dictionary["维修"] = "Repair";
		dictionary["维修车辆耐久、电瓶、油量、轮胎并扶正翻倒车辆，也可修复路障和建筑。"] = "Repair vehicle durability, battery, fuel, and tires, upright overturned vehicles, and repair barricades and structures.";
		dictionary["未能开始翻译："] = "Could not start translation:";
		dictionary["未能开始翻译：当前供应商配置不完整。"] = "Could not start translation: the current provider configuration is incomplete.";
		dictionary["未能开始翻译：请检查当前供应商配置或任务状态。"] = "Could not start translation: check the current provider configuration or task state.";
		dictionary["无法保存 "] = "Could not save ";
		dictionary["无法给予 "] = "Could not give ";
		dictionary["无法生成 "] = "Could not spawn ";
		dictionary["选择协议并在下方输入 Endpoint、API Key 和 Model。"] = "Choose a protocol and enter the Endpoint, API Key, and Model below.";
		dictionary["移动"] = "Movement";
		dictionary["移动工具"] = "Movement Tool";
		dictionary["已保存传送点："] = "Saved teleport point:";
		dictionary["已传送到 "] = "Teleported to ";
		dictionary["已开始翻译未缓存文本。"] = "Translation of uncached text started.";
		dictionary["已请求停止翻译；已完成并写入缓存的译文会保留。"] = "Translation stop requested; completed translations written to the cache are kept.";
		dictionary["已取消收藏车辆："] = "Vehicle removed from favorites:";
		dictionary["已取消收藏物品："] = "Item removed from favorites:";
		dictionary["已删除传送点："] = "Deleted teleport point:";
		dictionary["已收藏车辆："] = "Vehicle added to favorites:";
		dictionary["已收藏物品："] = "Item added to favorites:";
		dictionary["已重新扫描地图："] = "Map rescanned:";
		dictionary["原版与 Workshop 资产翻译"] = "Vanilla and Workshop Asset Translation";
		dictionary["只读取当前已加载的原版与 Workshop 资产；不会修改原始文件、.dat 或 AssetBundle。已有其他汉化时会优先保留原生汉化。"] = "Reads only currently loaded vanilla and Workshop assets; it does not modify original files, .dat files, or AssetBundles. Existing native translations take priority.";
		dictionary["智能"] = "Smart";
		dictionary["中键触发；删除必须同时按住 Shift。"] = "Middle-click to trigger; hold Shift when deleting.";
		dictionary["准星交互工具"] = "Point Interaction Tool";
		dictionary["准星交互工具已关闭。"] = "Point interaction tool disabled.";
		dictionary["准星交互工具已开启。"] = "Point interaction tool enabled.";
		dictionary["无限储存"] = "Infinite Storage";
		dictionary["无限储存不可用"] = "Infinite storage unavailable";
		dictionary["快照运行时尚未初始化。"] = "The Snapshot Runtime has not initialized.";
		dictionary["取出实例数"] = "Instances to withdraw";
		dictionary["每次少取一个完全相同的原生快照实例。"] = "Withdraw one fewer identical native snapshot instance.";
		dictionary["每次多取一个完全相同的原生快照实例。"] = "Withdraw one more identical native snapshot instance.";
		dictionary["筛选物品组。"] = "Filter ItemGroups.";
		dictionary["自动拾取优先存入"] = "Prioritize storage for auto pickup";
		dictionary["分解范围："] = "Decomposition scope: ";
		dictionary["单实例 → 当前快照桶 → 当前物品组循环。"] = "Cycle through single instance, current bucket, and current ItemGroup.";
		dictionary["没有可存储物品、保存失败，或快照运行时未就绪。"] = "No items were stored, persistence failed, or the Snapshot Runtime is not ready.";
		dictionary["每个背包物品均按原生编号、数量、品质和状态字节完整存储。"] = "Store every carried item with its native ID, amount, quality, and state bytes intact.";
		dictionary["模块失败：{0}"] = "Module failed: {0}";
		dictionary["筛选 "] = "Filters ";
		dictionary["没有匹配的物品组。"] = "No matching ItemGroups.";
		dictionary["新路径首次启用时仓库为空；旧格式不会被读取。"] = "The new path starts empty; retired formats are never read.";
		dictionary["未解析物品组（只读）"] = "Unresolved ItemGroup (read-only)";
		dictionary["资产标识冲突的物品组（只读）"] = "Asset-identity conflict ItemGroup (read-only)";
		dictionary["展开或收起快照桶。"] = "Expand or collapse snapshot buckets.";
		dictionary["资产缺失或编号到资产标识的映射不匹配；物品组被保留为只读且禁止取出。"] = "The asset is missing or its ID-to-identity mapping conflicts; the group is retained read-only and withdrawal is disabled.";
		dictionary["收起 ▲"] = "Collapse ▲";
		dictionary["展开 ▼"] = "Expand ▼";
		dictionary[" · 默认满状态"] = " · default full state";
		dictionary["取出所选精确快照桶。"] = "Withdraw from the selected exact snapshot bucket.";
		dictionary["资产不可解析或资产标识冲突，禁止取出。"] = "Withdrawal is disabled because the asset is unresolved or its identity conflicts.";
		dictionary["取出此精确快照桶的全部实例。"] = "Withdraw every instance from this exact snapshot bucket.";
		dictionary["取出此卡片中全部原始精确快照实例。"] = "Withdraw every original exact snapshot represented by this card.";
		dictionary["取全部 ×"] = "Withdraw all ×";
		dictionary["未取出：背包空间不足、资产冲突或保存失败。"] = "Nothing was withdrawn: inventory full, asset conflict, or persistence failure.";
		dictionary["当前快照桶"] = "Current Bucket";
		dictionary["物品组"] = "ItemGroup";
		dictionary["单实例"] = "Single Instance";
		dictionary["分解"] = "Decompose";
		dictionary["独立弹药填充模块负责解释原生数量与填充目标物品的兼容关系；快照内核不解释弹量。"] = "The independent ammo-refill module interprets native amount and FillTargetItem compatibility; the snapshot kernel has no ammo semantics.";
		dictionary["独立制作模块负责解释所需物品总数与原生数量总和；快照内核不解释制作语义。"] = "The independent crafting module interprets total-item and total-amount requirements; the snapshot kernel has no crafting semantics.";
		dictionary["独立分解模块负责解释原生分解配方；快照内核不读取蓝图。"] = "The independent decomposition module interprets native salvage recipes; the snapshot kernel never reads blueprints.";
		dictionary["制作适配"] = "Crafting Adapter";
		dictionary["物品编号 {0} · 资产标识 {1}\n实例 {2} · 快照桶 {3}"] = "ID {0} · GUID {1}\nInstances {2} · Buckets {3}";
		dictionary["模块未启用或不可用。"] = "The module is disabled or unavailable.";
		dictionary["当前选择不适用于此模块。"] = "The current selection is not applicable to this module.";
		dictionary["模块没有返回有效结果。"] = "The module returned no valid result.";
		dictionary["制作模块由原生制作流程自动调用。"] = "The crafting module is invoked automatically by the native crafting flow.";
		dictionary["无限储存尚未就绪或已锁定。"] = "Infinite storage is not ready or is locked.";
		dictionary["储存事务尚未建立。"] = "The storage transaction was not created.";
		dictionary["弹药来源或容器数据无效。"] = "Ammo source or container data is invalid.";
		dictionary["弹药分配计划无法完成。"] = "The ammo allocation plan could not be completed.";
		dictionary["制作需求数据无效。"] = "Crafting requirement data is invalid.";
		dictionary["找不到所选快照。"] = "The selected snapshot could not be found.";
		dictionary["可用数量不足。"] = "The available quantity is insufficient.";
		dictionary["找不到所选物品组。"] = "The selected ItemGroup could not be found.";
		dictionary["无法解析产物资产。"] = "An output asset could not be resolved.";
		dictionary["无法解析来源资产。"] = "The source asset could not be resolved.";
		dictionary["材料物品不能继续分解。"] = "Material items cannot be decomposed further.";
		dictionary["原生分解配方不符合安全支持条件。"] = "The native salvage recipe does not satisfy the supported safety contract.";
		dictionary["分解请求无效。"] = "The decomposition request is invalid.";
		dictionary["分解产物状态转换失败。"] = "The decomposition output state could not be transformed.";
		dictionary["数量计算超出安全范围。"] = "The quantity calculation exceeded the safe range.";
		dictionary["模块内部发生异常。"] = "The module threw an internal exception.";
		dictionary["模块操作未完成；请查看日志中的错误详情。"] = "The module operation did not complete; check the log for error details.";
		dictionary["没有可安全填充的弹药来源或容器。"] = "No compatible ammo source or target container is available.";
		dictionary["弹药容器已达到稳定状态。"] = "Ammo containers are already in a stable state.";
		dictionary["储存运行时尚未初始化。"] = "The storage runtime has not finished initializing.";
		dictionary["启用无限储存"] = "Enable infinite storage";
		dictionary["拾取优先存入无限储存"] = "Prioritize Infinite Storage for Pickup";
		dictionary["分解模式"] = "Decomposition Mode";
		dictionary["破坏模式"] = "Destruction Mode";
		dictionary["确认开启破坏模式？"] = "Enable destruction mode?";
		dictionary["破坏会永久删除无限储存中同一物品 ID 的全部精确快照，无法撤销。破坏范围固定为 ID。"] = "Destruction permanently deletes every exact snapshot with the same item ID from infinite storage and cannot be undone. The destruction scope is fixed to ID.";
		dictionary["破坏成功后，该物品 ID 会自动加入自动拾取黑名单。此警告确认一次后不再弹出。"] = "After successful destruction, the item ID is automatically added to the auto-pickup blacklist. This warning is shown only until acknowledged.";
		dictionary["取消"] = "Cancel";
		dictionary["确认开启"] = "Enable";
		dictionary["保持破坏模式关闭并关闭警告。"] = "Keep destruction mode disabled and close this warning.";
		dictionary["确认理解永久删除和自动加入黑名单的行为，并开启破坏模式。"] = "Confirm permanent deletion and automatic blacklisting, then enable destruction mode.";
		dictionary["开启后点击物品卡片会永久破坏同一 ID 的全部精确快照，并把该物品 ID 加入自动拾取黑名单。"] = "When enabled, clicking an item card permanently destroys every exact snapshot with the same item ID and adds that ID to the auto-pickup blacklist.";
		dictionary["点击卡片会永久破坏同一物品 ID 的全部精确快照；成功后该 ID 会自动加入自动拾取黑名单。"] = "Clicking the card permanently destroys every exact snapshot with the same item ID; on success that ID is automatically added to the auto-pickup blacklist.";
		dictionary["破坏模式已关闭。"] = "Destruction mode disabled.";
		dictionary["破坏模式已开启；点击红色悬停卡片将永久破坏快照并自动加入黑名单。"] = "Destruction mode enabled; clicking a red-hover card permanently destroys snapshots and automatically blacklists the item.";
		dictionary["破坏模式警告确认状态保存失败；破坏模式仍保持关闭。"] = "Could not save the destruction warning acknowledgement; destruction mode remains disabled.";
		dictionary["破坏结果需要对账；自动拾取黑名单已保留，无限储存已停止继续变更。"] = "Destruction requires reconciliation; the auto-pickup blacklist was kept and further infinite-storage mutations are locked.";
		dictionary["破坏失败且自动拾取黑名单回滚失败；仓库物品仍保留，请手动检查黑名单。"] = "Destruction failed and the auto-pickup blacklist rollback failed; the stored item remains, so inspect the blacklist manually.";
		dictionary["破坏失败；仓库快照和自动拾取黑名单均保持原状。"] = "Destruction failed; stored snapshots and the auto-pickup blacklist remain unchanged.";
		dictionary["分解范围"] = "Decomposition Scope";
		dictionary["单物品"] = "Single Item";
		dictionary["堆叠"] = "Stack";
		dictionary["分解模式已开启；只有可分解的非材料物品会在悬停时变红。"] = "Decomposition mode enabled; only decomposable non-material items turn red on hover.";
		dictionary["分解模式已关闭。"] = "Decomposition mode disabled.";
		dictionary["开启后只有带原生分解配方的非材料物品卡片会在悬停时变红；点击卡片按所选范围分解，分解产物直接存回无限储存。"] = "When enabled, only non-material item cards with a native salvage recipe turn red on hover. Click a card to store its salvage outputs directly in infinite storage.";
		dictionary["选择分解范围：单物品只分解一个；ID 分解所有同 ID 物品；堆叠分解当前卡片的全部数量。"] = "Choose the salvage scope: Single Item salvages one, ID salvages every item with the same ID, and Stack salvages the full pointed card.";
		dictionary["材料无法继续分解；关闭分解模式后可正常取出。"] = "Materials cannot be salvaged again; disable decomposition mode to withdraw them normally.";
		dictionary["该物品没有可用的原生分解配方；分解模式不会处理它。"] = "This item has no supported native salvage recipe and is excluded from decomposition mode.";
		dictionary["点击取出当前数量（{0}）。"] = "Click to withdraw the current amount ({0}).";
		dictionary["点击按“{0}”范围分解；产物会直接存回无限储存。"] = "Click to salvage using the '{0}' scope; outputs are stored directly in infinite storage.";
		dictionary["分解无限储存中所有同 ID 的可分解物品，包括不同品质或状态的卡片。"] = "Salvage every decomposable item with the same ID in infinite storage, including cards with different quality or state.";
		dictionary["分解鼠标指向卡片中的全部数量；不会处理其他同 ID 卡片。"] = "Salvage the full quantity in the pointed card without affecting other cards sharing its ID.";
		dictionary["只从鼠标指向的卡片分解一个物品。"] = "Salvage one item from the pointed card.";
		dictionary["无限储存尚未启用或加载完成。"] = "Infinite storage is not enabled or has not finished loading.";
		dictionary["物品卡片已经失效，请刷新后重试。"] = "The item card is stale; refresh and try again.";
		dictionary["材料无法继续分解。"] = "Materials cannot be salvaged again.";
		dictionary["无法解析该物品资产。"] = "The item asset could not be resolved.";
		dictionary["该物品没有原生分解配方。"] = "This item has no native salvage recipe.";
		dictionary["该物品的分解配方不是受支持的单物品分解配方。"] = "This item's salvage recipe is not a supported single-item salvage recipe.";
		dictionary["分解产物中存在无法解析的物品。"] = "One or more salvage outputs could not be resolved.";
		dictionary["分解结果保存失败，原物品和分解产物已回滚。"] = "Saving the salvage result failed; the original items and outputs were rolled back.";
		dictionary["分解数量过大，操作已取消。"] = "The salvage quantity is too large; the operation was cancelled.";
		dictionary["分解失败，原物品未被扣除。"] = "Salvage failed; no original items were removed.";
		dictionary["存储当前携带物品"] = "Store Current Carried Items";
		dictionary["没有可存储的物品，或无限储存未启用。"] = "No items can be stored, or infinite storage is disabled.";
		dictionary["将当前背包中的物品存入无限储存。"] = "Store the current backpack items in infinite storage.";
		dictionary["一键填充弹药容器"] = "Refill Ammo Containers";
		dictionary["相同容器身份可安全整理；直接 FillTargetItem 来源→目标只是兼容必要条件，bridge 安全门还会收窄执行，桥接容器不会跨资产外流。"] = "Identical container identities can be safely consolidated. A direct FillTargetItem source-to-target edge is only a necessary compatibility condition; the bridge safety gate further narrows execution, and bridge containers do not donate rounds across assets.";
		dictionary["无限储存中没有可安全填充的弹药容器。"] = "Infinite Storage has no ammo containers that can be safely refilled.";
		dictionary["已达到安全稳定状态；桥接容器不会跨资产外流。"] = "Safe stable state reached; bridge containers will not donate rounds across assets.";
		dictionary["弹药容器填充结果保存失败，存储已回滚。"] = "Saving the ammo-container refill failed, and storage was rolled back.";
		dictionary["弹药容器数据无效，填充操作已取消。"] = "Ammo-container data is invalid, so the refill was cancelled.";
		dictionary["弹药容器填充失败，未修改无限储存。"] = "Ammo-container refill failed without modifying Infinite Storage.";
		dictionary["当前分类没有已存储物品。"] = "No stored items in the current category.";
		dictionary["存储物品后会显示在这里。"] = "Stored items will appear here.";
		dictionary["无状态"] = "No state";
		dictionary["取出数量"] = "Withdraw amount";
		dictionary["未解析物品 ID "] = "Unresolved item ID ";
		dictionary["当前资产未解析；条目保持保守状态隔离。"] = "The current asset could not be resolved; this entry remains conservatively isolated.";
		dictionary["取出此条目的全部数量。"] = "Withdraw the full amount of this entry.";
		dictionary["取出此条目的全部单位。"] = "Withdraw all units from this entry.";
		dictionary["取全部"] = "Withdraw All";
		dictionary["原生背包没有可用空间。"] = "The native inventory has no available space.";
		dictionary["\n\n点击取出当前数量。"] = "\n\nClick to withdraw the current amount.";
		dictionary["\n\n点击取出当前数量（"] = "\n\nClick to withdraw the current amount (";
		dictionary["单位"] = "units";
		dictionary["个完整物品"] = "complete items";
		dictionary["导出原始备份"] = "Export original backup";
		dictionary["已导出待处理旧存档原始备份。"] = "Exported the original backup for pending legacy entries.";
		dictionary["导出待处理旧存档失败。"] = "Could not export pending legacy entries.";
		dictionary["导出所有待处理旧存档的原始 JSON 备份。"] = "Export the original JSON backup for all pending legacy entries.";
		dictionary["自动拾取"] = "Auto Pickup";
		dictionary["丢弃后禁拾时长"] = "Post-drop block duration";
		dictionary["分钟（1-1440）"] = "minutes (1-1440)";
		dictionary["玩家丢弃某个物品后，暂时禁止自动拾取同 ID 物品；再次丢弃同 ID 会重新计时。范围 1-1440 分钟，默认 1 分钟。"] = "After dropping an item, temporarily block auto pickup for every item with the same ID. Dropping the same ID again restarts the timer. Range: 1-1440 minutes; default: 1 minute.";
		dictionary["启用品质过滤"] = "Enable quality filter";
		dictionary["最低品质"] = "Minimum quality";
		dictionary["启用后，自动拾取会忽略品质低于该数值的物品。范围 0-100。"] = "When enabled, auto pickup ignores items below this quality. Range: 0-100.";
		dictionary["品质（0-100）"] = "quality (0-100)";
		dictionary["自动拾取不可用"] = "Auto pickup unavailable";
		dictionary["插件尚未完成自动拾取服务初始化。"] = "The auto-pickup service has not finished initializing.";
		dictionary["启用自动拾取"] = "Enable auto pickup";
		dictionary["自动拾取已启用；关闭菜单后开始扫描。"] = "Auto pickup enabled; scanning starts after closing the menu.";
		dictionary["自动拾取已关闭。"] = "Auto pickup disabled.";
		dictionary["自动拾取已开启。"] = "Auto pickup enabled.";
		dictionary["小地图已开启。"] = "Minimap enabled.";
		dictionary["小地图已关闭。"] = "Minimap disabled.";
		dictionary["小地图已放大，可见半径 {0:0} 米。"] = "Minimap zoomed in; visible radius: {0:0} m.";
		dictionary["小地图已缩小，可见半径 {0:0} 米。"] = "Minimap zoomed out; visible radius: {0:0} m.";
		dictionary["无限储存功能"] = "Infinite Storage Feature";
		dictionary["无限储存功能已开启。"] = "Infinite storage feature enabled.";
		dictionary["无限储存功能已关闭。"] = "Infinite storage feature disabled.";
		dictionary["当前会话尚未允许自动拾取；单人立即可用，多人需先确认本次连接风险。"] = "Auto pickup is not currently authorized. It is immediately available in singleplayer; multiplayer requires acknowledgement for this connection.";
		dictionary["菜单打开时暂停"] = "Pause while menu is open";
		dictionary["死亡时暂停"] = "Pause while dead";
		dictionary["扫描半径"] = "Scan radius";
		dictionary["请求距离"] = "Request distance";
		dictionary["拾取距离"] = "Pickup distance";
		dictionary["扫描频率"] = "Scan frequency";
		dictionary["白名单"] = "Whitelist";
		dictionary["启用白名单"] = "Enable whitelist";
		dictionary["管理白名单"] = "Manage whitelist";
		dictionary["添加白名单"] = "Add to whitelist";
		dictionary["白名单物品"] = "Whitelisted Items";
		dictionary["点击物品卡片可取消白名单。"] = "Click an item card to remove it from the whitelist.";
		dictionary["白名单为空"] = "Whitelist is empty";
		dictionary["启用白名单并添加物品后，只会拾取白名单物品。"] = "When the whitelist is enabled, only whitelisted items are picked up.";
		dictionary["该物品已经在白名单中。"] = "This item is already whitelisted.";
		dictionary["保存白名单失败："] = "Failed to save whitelist: ";
		dictionary["从当前背包添加白名单"] = "Add to Whitelist from Current Backpack";
		dictionary["物品按 ID 自动去重；点击卡片加入白名单，悬停卡片可查看完整物品详情。"] = "Items are deduplicated by ID. Click a card to whitelist it, or hover for full item details.";
		dictionary["白名单已启用；现在只拾取白名单物品。"] = "Whitelist enabled; only whitelisted items will be picked up.";
		dictionary["白名单已停用；黑名单仍然生效。"] = "Whitelist disabled; the blacklist remains active.";
		dictionary["白名单开关已打开；名单为空，当前仍按黑名单路径拾取，添加至少一项后自动生效。"] = "Whitelist toggle enabled; the list is empty, so pickups still follow the blacklist path. Add at least one item to activate it automatically.";
		dictionary["保存白名单状态失败："] = "Failed to save whitelist state: ";
		dictionary["黑名单"] = "Blacklist";
		dictionary["管理黑名单"] = "Manage blacklist";
		dictionary["添加黑名单"] = "Add to blacklist";
		dictionary["黑名单物品"] = "Blacklisted Items";
		dictionary["点击物品卡片可取消黑名单。"] = "Click an item card to remove it from the blacklist.";
		dictionary["黑名单为空"] = "Blacklist is empty";
		dictionary["默认不限制物品；点击“添加黑名单”从当前背包选择物品。"] = "Items are unrestricted by default. Choose Add to blacklist to select from the current backpack.";
		dictionary["默认自动拾取所有物品；点击“添加黑名单”从当前背包选择物品。"] = "All items are picked up by default. Choose Add to blacklist to select from the current backpack.";
		dictionary["该物品已经在黑名单中。"] = "This item is already blacklisted.";
		dictionary["保存黑名单失败："] = "Failed to save blacklist: ";
		dictionary["从当前背包添加黑名单"] = "Add from Current Backpack";
		dictionary["物品按 ID 自动去重；点击卡片加入黑名单，悬停卡片可查看完整物品详情。"] = "Items are deduplicated by ID. Click a card to blacklist it, or hover for full item details.";
		dictionary["当前背包没有匹配物品。"] = "No matching items in the current backpack.";
		dictionary["请确认物品在随身背包中；多人玩法需先确认本次连接风险。"] = "Make sure the item is in your carried inventory; multiplayer gameplay requires acknowledgement for this connection.";
		dictionary["已关闭"] = "Off";
		dictionary["世界已切换"] = "World changed";
		dictionary["单人门禁未就绪"] = "Singleplayer not ready";
		dictionary["玩家死亡，已暂停"] = "Paused while dead";
		dictionary["菜单打开，已暂停"] = "Paused while menu is open";
		dictionary["运行中"] = "Running";
		dictionary["无限储存需人工对账，自动拾取已全局暂停"] = "Infinite storage requires reconciliation; AutoPickup is paused globally.";
		dictionary["无限储存需人工对账，已隔离掉落并锁定重试"] = "Infinite storage requires reconciliation; the drop is quarantined and retry is locked.";
		dictionary["原生拾取后态不确定，已暂停该掉落实例的自动重试"] = "Native pickup outcome is uncertain; automatic retry for this drop is paused.";
		dictionary["需要持久化快照对账；所有自动拾取已暂停。"] = "Persistent snapshot reconciliation is required; all automatic pickup is paused.";
		dictionary["掉落已存入无限储存。"] = "The drop was stored in infinite storage.";
		dictionary["储存副本为权威；未解决的世界掉落已隔离，且无限储存已锁定。"] = "Storage is authoritative; the unresolved world drop is quarantined and infinite storage is locked.";
		dictionary["储存副本为权威且无限储存已锁定；世界掉落隔离也失败。"] = "Storage is authoritative and infinite storage is locked; world-drop quarantine also failed.";
		dictionary["持久化对账标记创建失败；当前进程仍保持锁定。"] = "Persistent reconciliation marker creation failed; the current process remains locked.";
		dictionary["无限储存补偿成功；世界掉落仍保留，稍后可重试。"] = "Infinite-storage compensation succeeded; the world drop remains for a later retry.";
		dictionary["原生拾取未确认交付，且无限储存恢复失败。"] = "Native pickup was not confirmed delivered, and Snapshot recovery failed.";
		dictionary["原生拾取未确认交付，且未执行无限储存恢复。"] = "Native pickup was not confirmed delivered, and Snapshot recovery was not attempted.";
		dictionary["背包复合证据已确认原生拾取交付。"] = "Composite inventory evidence confirmed native pickup delivery.";
		dictionary["背包空间不足，已暂停请求"] = "Inventory full; pickup requests paused";
		dictionary["每次扫描最多发送的原生拾取请求数，范围 1-16。"] = "Maximum native pickup requests per scan, from 1 to 16.";
		dictionary["诊断："] = "Diagnostics: ";
		dictionary["启用过滤规则"] = "Enable filter rules";
		dictionary["过滤规则状态已保存。"] = "Filter rule state saved.";
		dictionary["模式："] = "Mode: ";
		dictionary["过滤模式已保存。"] = "Filter mode saved.";
		dictionary["重新导入"] = "Reload";
		dictionary["导入失败："] = "Import failed: ";
		dictionary["导出保存"] = "Export and save";
		dictionary["规则已原子写入配置文件。"] = "Rules were atomically written to the config file.";
		dictionary["清空统计"] = "Clear statistics";
		dictionary["添加"] = "Add";
		dictionary["过滤规则已添加并保存。"] = "Filter rule added and saved.";
		dictionary["规则值无效或与现有规则重复。"] = "The rule value is invalid or duplicates an existing rule.";
		dictionary["保存规则失败："] = "Failed to save rules: ";
		dictionary["规则"] = "Rules";
		dictionary["规则文件："] = "Rules file: ";
		dictionary["条规则  ·  第 "] = " rules · page ";
		dictionary["上一页"] = "Previous";
		dictionary["下一页"] = "Next";
		dictionary["已启用"] = "Enabled";
		dictionary["已停用"] = "Disabled";
		dictionary["删除"] = "Delete";
		dictionary["规则启用状态已保存。"] = "Rule enabled state saved.";
		dictionary["规则已删除。"] = "Rule deleted.";
		dictionary["规则保存失败："] = "Failed to save rules: ";
		dictionary["全部允许"] = "Allow all";
		dictionary["仅白名单"] = "Whitelist only";
		dictionary["仅黑名单"] = "Blacklist only";
		dictionary["先白后黑"] = "Whitelist then blacklist";
		dictionary["允许"] = "Allow";
		dictionary["拒绝"] = "Deny";
		dictionary["物品 ID"] = "Item ID";
		dictionary["资产名"] = "Asset name";
		dictionary["显示名"] = "Display name";
		dictionary["物品类型"] = "Item type";
		dictionary["分类"] = "Category";
		dictionary["关键词"] = "Keyword";
		dictionary["关闭"] = "Off";
		dictionary["摘要"] = "Summary";
		dictionary["逐项"] = "Verbose";
		dictionary["物品收藏  {0}"] = "Item Favorites  {0}";
		dictionary["车辆收藏  {0}"] = "Vehicle Favorites  {0}";
		dictionary["飞行模式：{0}"] = "Flight mode: {0}";
		dictionary["飞行模式已切换为 {0}。"] = "Flight mode changed to {0}.";
		dictionary["世界轴"] = "World Axis";
		dictionary["视角指向"] = "View Direction";
		dictionary["当前地图尚未加载完成。"] = "The current map has not finished loading.";
		dictionary["当前地图没有可读取的 Map.png / Chart.png；仍可保存当前位置传送点。"] = "The current map has no readable Map.png or Chart.png; the current position can still be saved as a teleport point.";
		dictionary["字体名称不能为空。"] = "The font name cannot be empty.";
		dictionary["紧凑"] = "Compact";
		dictionary["类型"] = "Type";
		dictionary["大件优先"] = "Large First";
		dictionary["小件优先"] = "Small First";
		dictionary["品质优先"] = "Quality First";
		dictionary["堆叠整理"] = "Stack Fill";
		dictionary["热键优先"] = "Hotkey First";
		dictionary["低扰动"] = "Low Movement";
		dictionary["自定义"] = "Custom";
		dictionary["参考兼容"] = "Compatibility";
		dictionary["整理正在执行，已忽略重复请求。"] = "Inventory arrangement is already running; the duplicate request was ignored.";
		dictionary["跨箱集中正在执行，已忽略重复请求。"] = "A cross-container transfer is already running; the duplicate request was ignored.";
		dictionary["当前没有可用的本地单人玩家库存。"] = "No local singleplayer inventory is available.";
		dictionary["当前不在储物箱与随身库存之间，放行原生操作。"] = "The current action is not between storage and carried inventory; the native action was allowed.";
		dictionary["角色装备切换中，放行原生操作。"] = "Equipment is busy; the native action was allowed.";
		dictionary["源坐标没有可转移的有效物品，放行原生操作。"] = "The source slot has no valid transferable item; the native action was allowed.";
		dictionary["目标容器没有可用空位，放行原生操作。"] = "The target container has no free slot; the native action was allowed.";
		dictionary["已接管原生快速转移：正在集中同一资产的物品。"] = "Native quick transfer was intercepted; items with the same asset are being consolidated.";
		dictionary["源格子在批量转移期间发生变化，已停止集中。"] = "The source slot changed during batch transfer; consolidation was stopped.";
		dictionary["原生库存未确认转移结果，已停止批量集中。"] = "The native inventory did not confirm the transfer; batch consolidation was stopped.";
		dictionary["整理后校验发现物品指纹变化，已恢复执行前库存。"] = "Post-arrangement validation detected changed item fingerprints; the pre-operation inventory was restored.";
		dictionary["整理出现异常；请查看 BepInEx 日志。"] = "Inventory arrangement failed unexpectedly; check the BepInEx log.";
		dictionary["整理出现异常，已恢复执行前库存和热键坐标。"] = "Inventory arrangement failed unexpectedly; the pre-operation inventory and hotkey positions were restored.";
		dictionary["整理正在执行，暂不能撤销。"] = "Inventory arrangement is running and cannot be undone yet.";
		dictionary["撤销已失效：整理后的库存已被外部操作改变。"] = "Undo is no longer valid because the arranged inventory was changed externally.";
		dictionary["已撤销上次整理。"] = "The last inventory arrangement was undone.";
		dictionary["撤销后校验失败，请查看 BepInEx 日志。"] = "Post-undo validation failed; check the BepInEx log.";
		dictionary["角色装备切换中，无法撤销。"] = "Equipment is busy, so the arrangement cannot be undone.";
		dictionary["库存包含无法解析的物品，无法安全撤销。"] = "The inventory contains an item that cannot be resolved, so it cannot be safely undone.";
		dictionary["角色装备切换中，已取消整理。"] = "Equipment is busy; inventory arrangement was canceled.";
		dictionary["库存包含无法解析的物品，已取消整理以避免丢失物品。"] = "The inventory contains an item that cannot be resolved; arrangement was canceled to avoid item loss.";
		dictionary["当前上下文没有可用库存容器。"] = "No inventory container is available in the current context.";
		dictionary["计划引用了不存在的源物品。"] = "The plan references a source item that no longer exists.";
		dictionary["原生库存 API 拒绝放置计划中的物品。"] = "The native inventory API rejected placement of an item in the plan.";
		dictionary["原生 API 拒绝恢复原始位置。"] = "The native API rejected restoration of the original position.";
		dictionary["整理输入不可用。"] = "Inventory arrangement input is unavailable.";
		dictionary["没有可整理的库存容器。"] = "No inventory container is available for arrangement.";
		dictionary["受保护物品的位置无效，已取消整理。"] = "A protected item has an invalid position; arrangement was canceled.";
		dictionary["预检无法完整安置所有物品；不会修改库存，也不会自动掉落。"] = "The preflight check could not place every item; the inventory was not modified and no items were dropped.";
		dictionary["低扰动整理发现原位置无效，已取消整理。"] = "Low-movement arrangement found an invalid original position; arrangement was canceled.";
		dictionary["小地图"] = "Minimap";
		dictionary["小地图与世界雷达"] = "Minimap & World Radar";
		dictionary["常规"] = "General";
		dictionary["标记"] = "Markers";
		dictionary["地下"] = "Underground";
		dictionary["联动"] = "Links";
		dictionary["启用小地图"] = "Enable minimap";
		dictionary["随视角转动"] = "Rotate with view";
		dictionary["北向固定"] = "North-up";
		dictionary["关闭时保持当前固定北向；开启后地图、标记和指南针随玩家视角转动。"] = "Keep the existing north-up minimap when disabled; when enabled, rotate the map, markers, and compass with the player's view.";
		dictionary["布局编辑"] = "Edit layout";
		dictionary["进入后插件窗口会隐藏；拖动或缩放小地图，按回车键应用。正常状态不拦截鼠标。"] = "The plugin window hides during layout editing. Drag or resize the minimap, then press Enter to apply. Normal mode does not block the mouse.";
		dictionary["跟随小地图范围"] = "Follow minimap range";
		dictionary["默认关闭；开启后只由小地图可见半径单向约束自动拾取距离。"] = "Disabled by default; when enabled, only the minimap visible radius constrains auto-pickup range one way.";
		dictionary["跟随小地图稀有度"] = "Follow minimap rarities";
		dictionary["默认关闭；开启后小地图稀有度掩码会额外约束自动拾取，不会反向影响雷达显示。"] = "Disabled by default; when enabled, the minimap rarity mask additionally constrains auto pickup without affecting radar visibility.";
		dictionary["打开自动拾取稀有度筛选窗口；关闭的稀有度不会被拾取。"] = "Open the auto-pickup rarity filter; disabled rarities will not be picked up.";
		dictionary["关闭自动拾取稀有度筛选窗口。"] = "Close the auto-pickup rarity filter.";
		dictionary["取消某个稀有度后，自动拾取会跳过该稀有度的地面物品；默认全部开启。"] = "Disable a rarity to make auto pickup skip ground items of that rarity; all rarities are enabled by default.";
		dictionary[" 像素"] = " px";
		dictionary[" 米"] = " m";
		dictionary[" 次/秒"] = " Hz";
		dictionary[" 秒"] = " s";
		dictionary["形状："] = "Shape: ";
		dictionary["锚点："] = "Anchor: ";
		dictionary["背景："] = "Background: ";
		dictionary["背景"] = "Background";
		dictionary["左上"] = "Top Left";
		dictionary["右上"] = "Top Right";
		dictionary["左下"] = "Bottom Left";
		dictionary["右下"] = "Bottom Right";
		dictionary["仅原生地图"] = "Native Map Only";
		dictionary["实时地表"] = "Realtime Surface";
		dictionary["实时高度切片"] = "Realtime Height Slice";
		dictionary["关闭背景"] = "Background Off";
		dictionary["自动优先原生地图"] = "Prefer Native Map Automatically";
		dictionary["备用网格"] = "Fallback Grid";
		dictionary["背景已关闭"] = "Background Disabled";
		dictionary["原生地图裁剪"] = "Native Map Crop";
		dictionary["北"] = "N";
		dictionary["东"] = "E";
		dictionary["南"] = "S";
		dictionary["西"] = "W";
		dictionary["拖动小地图可调整位置，拖动右下角可缩放；按回车键应用，按 Esc 取消"] = "Drag the minimap to reposition it or drag the bottom-right corner to resize; press Enter to apply or Esc to cancel";
		dictionary["扫描"] = "Scan";
		dictionary["大小"] = "Size";
		dictionary["可见半径"] = "Visible radius";
		dictionary["实时建筑"] = "Realtime buildings";
		dictionary["指南针"] = "Compass";
		dictionary["坐标"] = "Coordinates";
		dictionary["圆形与正方形视口均已预创建；切换不会在画布重建期间销毁后重建。"] = "Circle and square viewports are pre-created; switching never destroys and rebuilds them during a canvas rebuild.";
		dictionary["原生标记"] = "Native markers";
		dictionary["传送点"] = "Teleport points";
		dictionary["其他玩家"] = "Other players";
		dictionary["僵尸"] = "Zombies";
		dictionary["动物"] = "Animals";
		dictionary["地面物品"] = "Ground items";
		dictionary["物品稀有度"] = "Item rarities";
		dictionary["普通"] = "Common";
		dictionary["罕见"] = "Uncommon";
		dictionary["稀有"] = "Rare";
		dictionary["史诗"] = "Epic";
		dictionary["传奇"] = "Legendary";
		dictionary["神话"] = "Mythical";
		dictionary["全选"] = "Select all";
		dictionary["全不选"] = "Select none";
		dictionary["合并"] = "Merge";
		dictionary["同层"] = "Same floor";
		dictionary["显示其他楼层"] = "Show other floors";
		dictionary["默认显示全部楼层；不同楼层的标记会以淡化颜色区分。"] = "All floors are shown by default; markers on other floors use dimmed colors.";
		dictionary["5 点双向探针按连续厚度判定；薄楼板与楼层间空气不会累计。"] = "Five bidirectional probes use continuous thickness; thin floors and air gaps between floors are not accumulated.";
		dictionary["探针"] = "Probe";
		dictionary["合并间隙"] = "Merge gap";
		dictionary["进入厚度"] = "Enter thickness";
		dictionary["退出厚度"] = "Exit thickness";
		dictionary["进入延时"] = "Enter delay";
		dictionary["退出延时"] = "Exit delay";
		dictionary["楼板排除"] = "Floor exclusion";
		dictionary["切片上方"] = "Slice above";
		dictionary["切片下方"] = "Slice below";
		dictionary["安全关系必须满足：楼板排除 ≤ 退出厚度 < 进入厚度；无效组合会回退默认值。"] = "The safe relationship must be floor exclusion ≤ exit thickness < enter thickness; invalid combinations fall back to defaults.";
		dictionary["两个联动均默认关闭，并且只允许小地图设置单向约束自动拾取。"] = "Both links are disabled by default and only allow minimap settings to constrain auto pickup one way.";
		dictionary["拾取范围跟随小地图"] = "Use minimap pickup range";
		dictionary["拾取稀有度跟随小地图"] = "Use minimap rarity filter";
		dictionary["小地图物品层始终读取原始地面物品快照；自动拾取关闭、品质、名单和丢弃抑制都不会隐藏物品图形。"] = "The minimap item layer always reads raw ground-item snapshots; auto-pickup state, quality, lists, and drop suppression never hide item icons.";
		dictionary["地下白名单辅助证据："] = "Underground allowlist auxiliary evidence: ";
		dictionary["是"] = "Yes";
		dictionary["否"] = "No";
		English = dictionary;
		Chinese = new Dictionary<string, string>(StringComparer.Ordinal) { ["Workshop"] = "创意工坊" };
		EnglishPatterns = new PatternTranslation[]
		{
			new PatternTranslation("^([\\s\\S]+)；背包复合证据已确认原生拾取交付。$", "$1; composite inventory evidence confirmed native pickup delivery."),
			new PatternTranslation("^每轮 ([\\d,]+) 个请求$", "$1 requests per tick"),
			new PatternTranslation("^显示 ([\\d,]+) 辆已加载收藏车辆。$", "Showing $1 loaded favorite vehicles."),
			new PatternTranslation("^最大距离 ([\\d,.]+)m$", "Maximum range: $1m"),
			new PatternTranslation("^状态：(.+?)  ·  默认关闭  ·  北向固定$", "State: $1  ·  disabled by default  ·  north-up"),
			new PatternTranslation("^状态：(.+?)  ·  默认关闭  ·  随视角转动$", "State: $1  ·  disabled by default  ·  view-up"),
			new PatternTranslation("^形状：(.+)$", "Shape: $1"),
			new PatternTranslation("^锚点：(.+)$", "Anchor: $1"),
			new PatternTranslation("^当前可见半径：(.+?) 米  ·  稀有度：(.+)$", "Visible radius: $1 m  ·  rarities: $2"),
			new PatternTranslation("^背景：(.+?)  ·  厚遮挡探针：(\\d+)/(\\d+)  ·  最大连续厚度：(.+?) 米$", "Background: $1  ·  thick probes: $2/$3  ·  max continuous thickness: $4 m"),
			new PatternTranslation("^地下白名单辅助证据：是$", "Underground allowlist auxiliary evidence: yes"),
			new PatternTranslation("^地下白名单辅助证据：否$", "Underground allowlist auxiliary evidence: no"),
			new PatternTranslation("^背景：(.+)$", "Background: $1"),
			new PatternTranslation("^已传送到地图坐标 X (.+?) · Z (.+?)。$", "Teleported to map coordinates X $1 · Z $2."),
			new PatternTranslation("^预览：(\\d+)%$", "Preview: $1%"),
			new PatternTranslation("^无法读取系统字体列表：(.+)$", "Could not read the system font list: $1"),
			new PatternTranslation("^系统中未安装字体：(.+)$", "The font is not installed: $1"),
			new PatternTranslation("^该字体不包含插件所需的简体中文字符：(.+)$", "The font does not contain the Simplified Chinese characters required by the plugin: $1"),
			new PatternTranslation("^Unity 无法加载系统字体：(.+)$", "Unity could not load the system font: $1"),
			new PatternTranslation("^该字体缺少简体中文字符 U\\+([0-9A-Fa-f]+)：(.+)$", "The font is missing Simplified Chinese character U+$1: $2"),
			new PatternTranslation("^加载系统字体失败：(.+)$", "Failed to load the system font: $1"),
			new PatternTranslation("^预览：移动 (\\d+)，归并 (\\d+)，跨页 (\\d+)。$", "Preview: $1 moves, $2 merges, $3 cross-page placements."),
			new PatternTranslation("^预览：移动 (\\d+)，归并 (\\d+)，跨页 (\\d+)，弹药调整 (\\d+)。$", "Preview: $1 moves, $2 merges, $3 cross-page placements, $4 ammo payload changes."),
			new PatternTranslation("^已整理：移动 (\\d+)，归并 (\\d+)，跨页 (\\d+)。$", "Arranged: $1 moves, $2 merges, $3 cross-page placements."),
			new PatternTranslation("^已整理：移动 (\\d+)，归并 (\\d+)，跨页 (\\d+)，弹药调整 (\\d+)。$", "Arranged: $1 moves, $2 merges, $3 cross-page placements, $4 ammo payload changes."),
			new PatternTranslation("^同物品跨箱集中完成：已转移 (\\d+) 件。$", "Cross-container consolidation complete: moved $1 items."),
			new PatternTranslation("^整理未完成，已恢复执行前库存：(.+)$", "Arrangement did not complete; the pre-operation inventory was restored: $1"),
			new PatternTranslation("^整理失败且恢复失败；请停止继续操作并查看 BepInEx 日志。原因：(.+?)；恢复：(.+)$", "Arrangement and restoration both failed; stop further operations and check the BepInEx log. Cause: $1; restoration: $2"),
			new PatternTranslation("^整理后校验失败且恢复失败；请停止继续操作并查看日志。恢复：(.+)$", "Post-arrangement validation and restoration both failed; stop further operations and check the log. Restoration: $1"),
			new PatternTranslation("^整理出现异常且恢复失败；请停止继续操作并查看 BepInEx 日志。恢复：(.+)$", "Inventory arrangement and restoration both failed unexpectedly; stop further operations and check the BepInEx log. Restoration: $1"),
			new PatternTranslation("^撤销失败，无法恢复原始位置：(.+)$", "Undo failed because the original positions could not be restored: $1"),
			new PatternTranslation("^飞行模式：世界轴$", "Flight mode: World Axis"),
			new PatternTranslation("^飞行模式：视角指向$", "Flight mode: View Direction"),
			new PatternTranslation("^横坐标 (.+?)  高度 (.+?)  纵坐标 (.+?)$", "X $1  Height $2  Z $3"),
			new PatternTranslation("^飞行模式已切换为 世界轴。$", "Flight mode changed to World Axis."),
			new PatternTranslation("^飞行模式已切换为 视角指向。$", "Flight mode changed to View Direction."),
			new PatternTranslation("^状态：(.+?)；已扫描物品 (\\d+) / 载具 (\\d+)$", "State: $1; scanned items $2 / vehicles $3"),
			new PatternTranslation("^文本总数 (\\d+) · 缓存命中 (\\d+) · 中文跳过 (\\d+) · 待翻译 (\\d+) · 完成 (\\d+) · 失败 (\\d+)$", "Texts $1 · cache hits $2 · Chinese skipped $3 · pending $4 · completed $5 · failed $6"),
			new PatternTranslation("^文本总数 (\\d+) · 缓存命中 (\\d+) · 待翻译 (\\d+) · 完成 (\\d+) · 失败 (\\d+)$", "Texts $1 · cache hits $2 · pending $3 · completed $4 · failed $5"),
			new PatternTranslation("^机翻 API：(.+?)；协议：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation API: $1; protocol: $2; target language: $3; model: $4"),
			new PatternTranslation("^翻译接口：(.+?)；供应商：(.+?)；密钥：(\\d+)/(\\d+)；并行：(\\d+) 路；目标语言：(.+?)；模型：(.+)$", "Translation: $1; provider: $2; keys: $3/$4; parallel workers: $5; target language: $6; model: $7"),
			new PatternTranslation("^翻译接口：(.+?)；供应商：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation: $1; provider: $2; target language: $3; model: $4"),
			new PatternTranslation("^缓存文件：(.+)$", "Cache file: $1"),
			new PatternTranslation("^筛选 (\\d+)$", "Filter $1"),
			new PatternTranslation("^自动扫描完成：(\\d+) 个物品、(\\d+) 辆载具。$", "Scan complete: $1 items and $2 vehicles."),
			new PatternTranslation("^生命 (.+?)/100    饱食 (.+?)/100    水分 (.+?)/100    免疫 (.+?)/100    体力 (.+?)/100    氧气 (.+?)/100$", "Health $1/100    Food $2/100    Water $3/100    Immunity $4/100    Stamina $5/100    Oxygen $6/100"),
			new PatternTranslation("^经验 (.+?)    声望 (.+?)$", "Experience $1    Reputation $2"),
			new PatternTranslation("^已增加 (.+?) 经验。$", "Added $1 experience."),
			new PatternTranslation("^已变更 (.+?) 声望。$", "Changed reputation by $1."),
			new PatternTranslation("^技能已处理，实际变更 (\\d+) 项。$", "Skills processed; $1 levels changed."),
			new PatternTranslation("^显示 ([\\d,]+) / ([\\d,]+)；模组物品 ([\\d,]+)。搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; mod items: $3. Search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 ([\\d,]+) / ([\\d,]+)；搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 ([\\d,]+) / ([\\d,]+)；模组车辆 ([\\d,]+)。每次生成 1–20 辆。$", "Showing $1 / $2; mod vehicles: $3. Spawn 1–20 at a time."),
			new PatternTranslation("^物品收藏  (\\d+)/(\\d+)$", "Item Favorites  $1/$2"),
			new PatternTranslation("^车辆收藏  (\\d+)/(\\d+)$", "Vehicle Favorites  $1/$2"),
			new PatternTranslation("^显示 ([\\d,]+) 个已加载收藏物品；收藏记录 ([\\d,]+) 个。搜索支持名称、ID、GUID 和来源。$", "Showing $1 loaded favorite items; $2 saved favorites. Search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 ([\\d,]+) 个已加载收藏物品。$", "Showing $1 loaded favorite items."),
			new PatternTranslation("^当前分类：(.+)$", "Current category: $1"),
			new PatternTranslation("^显示 ([\\d,]+) 辆已加载收藏车辆；收藏记录 ([\\d,]+) 辆。$", "Showing $1 loaded favorite vehicles; $2 saved favorites."),
			new PatternTranslation("^已给予 (\\d+) × (.+)。$", "Gave $1 × $2."),
			new PatternTranslation("^无法给予 (.+)。$", "Could not give $1."),
			new PatternTranslation("^已在玩家前方生成 (\\d+) × (.+)。$", "Spawned $1 × $2 in front of the player."),
			new PatternTranslation("^无法生成 (.+)。$", "Could not spawn $1."),
			new PatternTranslation("^无法保存 (.+) 的收藏状态。$", "Could not save the favorite state for $1."),
			new PatternTranslation("^已收藏物品：(.+)。$", "Added item to favorites: $1."),
			new PatternTranslation("^已取消收藏物品：(.+)。$", "Removed item from favorites: $1."),
			new PatternTranslation("^已收藏车辆：(.+)。$", "Added vehicle to favorites: $1."),
			new PatternTranslation("^已取消收藏车辆：(.+)。$", "Removed vehicle from favorites: $1."),
			new PatternTranslation("^第 (\\d+) / (\\d+) 页$", "Page $1 / $2"),
			new PatternTranslation("^地图 (.+?)    X (.+?)    Y (.+?)    Z (.+?)$", "Map $1    X $2    Y $3    Z $4"),
			new PatternTranslation("^已保存传送点：(.+)。$", "Saved teleport point: $1."),
			new PatternTranslation("^已保存位置（(\\d+)）$", "Saved Positions ($1)"),
			new PatternTranslation("^当前地图传送点（(\\d+)）$", "Current Map Teleports ($1)"),
			new PatternTranslation("^已传送到 (.+)。$", "Teleported to $1."),
			new PatternTranslation("^已删除传送点：(.+)。$", "Deleted teleport point: $1."),
			new PatternTranslation("^当前时间：(.+?) / (.+?)（(.+?)）$", "Current time: $1 / $2 ($3)"),
			new PatternTranslation("^时间已设置为周期的 (\\d+)% 。$", "Time set to $1% of the cycle."),
			new PatternTranslation("^排序预设：(.+?)；默认禁止自动掉落。$", "Preset: $1; automatic dropping is disabled by default."),
			new PatternTranslation("^切换排序预设：(.+?)$", "Open preset list: $1"),
			new PatternTranslation("^物品：(\\d+)（模组 (\\d+)）    车辆：(\\d+)（模组 (\\d+)）$", "Items: $1 (mod $2)    Vehicles: $3 (mod $4)"),
			new PatternTranslation("^(.+?)  ·  候选 (\\d+)  ·  成功 (\\d+)  ·  失败 (\\d+)  ·  重试 (\\d+)$", "$1  ·  candidates $2  ·  picked up $3  ·  failed $4  ·  retries $5"),
			new PatternTranslation("^当前 (\\d+) 条白名单物品。$", "$1 whitelist items."),
			new PatternTranslation("^当前 (\\d+) 条黑名单物品；黑名单始终优先。$", "$1 blacklist items; blacklist always takes precedence."),
			new PatternTranslation("^(\\d+) 件  ·  第 (\\d+)/(\\d+) 页$", "$1 items  ·  page $2/$3"),
			new PatternTranslation("^ID (\\d+)  ·  点击取消白名单$", "ID $1  ·  click to remove from whitelist"),
			new PatternTranslation("^ID (\\d+)  ·  点击加入白名单$", "ID $1  ·  click to add to whitelist"),
			new PatternTranslation("^ID (\\d+)  ·  点击取消黑名单$", "ID $1  ·  click to remove from blacklist"),
			new PatternTranslation("^ID (\\d+)  ·  点击加入黑名单$", "ID $1  ·  click to add to blacklist"),
			new PatternTranslation("^已将 (.+) 加入白名单。$", "Added $1 to the whitelist."),
			new PatternTranslation("^已将 (.+) 加入白名单；白名单路径现已生效。$", "Added $1 to the whitelist; whitelist filtering is now active."),
			new PatternTranslation("^已将 (.+) 加入白名单；白名单开关未开启，暂不限制拾取。$", "Added $1 to the whitelist; the whitelist toggle is off, so whitelist filtering remains inactive."),
			new PatternTranslation("^已取消物品 ID (\\d+) 的白名单。$", "Removed item ID $1 from the whitelist."),
			new PatternTranslation("^已将 (.+) 加入黑名单。$", "Added $1 to the blacklist."),
			new PatternTranslation("^已取消物品 ID (\\d+) 的黑名单。$", "Removed item ID $1 from the blacklist."),
			new PatternTranslation("^当前背包去重后 (\\d+) 件  ·  第 (\\d+)/(\\d+) 页$", "$1 unique backpack items  ·  page $2/$3"),
			new PatternTranslation("^已存储 (\\d+) 个背包格。$", "Stored $1 inventory slots."),
			new PatternTranslation("^已存储 ([\\d,]+) 个原生物品快照。$", "Stored $1 native item snapshots."),
			new PatternTranslation("^物品组 ([\\d,]+) / ([\\d,]+)；快照实例 ([\\d,]+)。原生数量字段始终保持不透明，内核不会拆分。$", "ItemGroups $1 / $2; snapshot instances: $3. The kernel always keeps native amount opaque."),
			new PatternTranslation("^取全部 ×([\\d,]+)$", "Withdraw all ×$1"),
			new PatternTranslation("^原生数量 ([\\d,]+) · 实例 ×([\\d,]+) · 品质 ([\\d,]+) · 状态 空$", "amount $1 · instances ×$2 · quality $3 · state empty"),
			new PatternTranslation("^原生数量 ([\\d,]+) · 实例 ×([\\d,]+) · 品质 ([\\d,]+) · 状态指纹 ([0-9A-F]+)$", "amount $1 · instances ×$2 · quality $3 · state fingerprint $4"),
			new PatternTranslation("^原生数量 ([\\d,]+) · 实例 ×([\\d,]+) · 状态 空$", "amount $1 · instances ×$2 · state empty"),
			new PatternTranslation("^原生数量 ([\\d,]+) · 实例 ×([\\d,]+) · 状态指纹 ([0-9A-F]+)$", "amount $1 · instances ×$2 · state fingerprint $3"),
			new PatternTranslation("^弹药 ([\\d,]+)/([\\d,]+) · 实例 ×([\\d,]+) · 品质 ([\\d,]+) · 状态 空$", "ammo $1/$2 · instances ×$3 · quality $4 · state empty"),
			new PatternTranslation("^弹药 ([\\d,]+)/([\\d,]+) · 实例 ×([\\d,]+) · 品质 ([\\d,]+) · 状态指纹 ([0-9A-F]+)$", "ammo $1/$2 · instances ×$3 · quality $4 · state fingerprint $5"),
			new PatternTranslation("^弹药 ([\\d,]+)/([\\d,]+) · 实例 ×([\\d,]+) · 状态 空$", "ammo $1/$2 · instances ×$3 · state empty"),
			new PatternTranslation("^弹药 ([\\d,]+)/([\\d,]+) · 实例 ×([\\d,]+) · 状态指纹 ([0-9A-F]+)$", "ammo $1/$2 · instances ×$3 · state fingerprint $4"),
			new PatternTranslation("^已取出 ([\\d,]+) 个精确快照实例。$", "Withdrew $1 exact snapshot instances."),
			new PatternTranslation("^已填充 ([\\d,]+) 个容器；弹量总和 ([\\d,]+)。$", "Refilled $1 containers; total rounds: $2."),
			new PatternTranslation("^已分解 ([\\d,]+) 个快照实例，生成 ([\\d,]+) 个实例。$", "Decomposed $1 snapshot instances and produced $2 instances."),
			new PatternTranslation("^ID ([\\d,]+)  ·  ×([\\d,]+)/总([\\d,]+)$", "ID $1  ·  ×$2/Total $3"),
			new PatternTranslation("^当前卡片精确快照 ×([\\d,]+)；同 ID 总库存 ×([\\d,]+)。$", "Exact snapshots on this card: ×$1; total inventory for this ID: ×$2."),
			new PatternTranslation("^当前卡片显示实例 ×([\\d,]+)；同 ID 总库存 ×([\\d,]+)。$", "Displayed instances on this card: ×$1; total inventory for this ID: ×$2."),
			new PatternTranslation("^品质分别按 ([\\d,]+) 个精确快照桶保留。$", "Quality is preserved in $1 separate exact snapshot buckets."),
			new PatternTranslation("^已破坏 ([\\d,]+) 个快照实例；物品 ID ([\\d,]+) 已加入自动拾取黑名单。$", "Destroyed $1 snapshot instances; item ID $2 was added to the auto-pickup blacklist."),
			new PatternTranslation("^已破坏 ([\\d,]+) 个快照实例；物品 ID ([\\d,]+) 已在自动拾取黑名单中。$", "Destroyed $1 snapshot instances; item ID $2 was already on the auto-pickup blacklist."),
			new PatternTranslation("^点击按“(.+)”范围分解；产物会直接存回无限储存。$", "Click to salvage using the '$1' scope; outputs are stored directly in infinite storage."),
			new PatternTranslation("^已分解 ([\\d,]+) 个物品；([\\d,]+) 个产物单位（([\\d,]+) 种）已存入无限储存。$", "Salvaged $1 items; $2 output units across $3 types were stored in infinite storage."),
			new PatternTranslation("^已安全填充 ([\\d,]+) / ([\\d,]+) 个弹药容器；([\\d,]+) 个容器身份组；弹药来源与容器合计 ([\\d,]+) 发弹药。$", "Safely refilled $1 / $2 ammo containers; $3 container-identity groups; ammo sources and containers total $4 rounds."),
			new PatternTranslation("^已安全填充 ([\\d,]+) / ([\\d,]+) 个弹药容器；([\\d,]+) 个容器身份组；弹药来源与容器合计 ([\\d,]+) 发弹药；另跳过 ([\\d,]+) 个异常条目。$", "Safely refilled $1 / $2 ammo containers; $3 container-identity groups; ammo sources and containers total $4 rounds; skipped $5 invalid entries."),
			new PatternTranslation("^弹药容器或 FillTargetItem 数据异常，已跳过 ([\\d,]+) 个条目。$", "Ammo-container or FillTargetItem data was invalid; skipped $1 entries."),
			new PatternTranslation("^已跳过 ([\\d,]+) 个异常条目；其余弹药容器已达到安全稳定状态。$", "Skipped $1 invalid entries; the remaining ammo containers are in a safe stable state."),
			new PatternTranslation("^检测到 ([\\d,]+) 条待处理旧存档记录。$", "Detected $1 pending legacy records."),
			new PatternTranslation("^显示 ([\\d,]+) / ([\\d,]+) 条目；资源 ([\\d,]+)，完整物品 ([\\d,]+) 个。$", "Showing $1 / $2 entries; resource units $3, complete items $4."),
			new PatternTranslation("^库存 ([\\d,]+)$", "Stored $1"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 弹药 ([\\d,]+)/([\\d,]+) \\| 品质 (\\d+)%$", "Stored $1 complete items | ammo $2/$3 | quality $4%"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 弹药 ([\\d,]+)/([\\d,]+)$", "Stored $1 complete items | ammo $2/$3"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 燃料 ([\\d,]+)/([\\d,]+) \\| 品质 (\\d+)%$", "Stored $1 complete items | fuel $2/$3 | quality $4%"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 燃料 ([\\d,]+)/([\\d,]+)$", "Stored $1 complete items | fuel $2/$3"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 水 ([\\d,]+)/([\\d,]+) \\| 品质 (\\d+)%$", "Stored $1 complete items | water $2/$3 | quality $4%"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 水 ([\\d,]+)/([\\d,]+)$", "Stored $1 complete items | water $2/$3"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 品质 (\\d+)% \\| 状态独立保存$", "Stored $1 complete items | quality $2% | state preserved separately"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 品质 (\\d+)%$", "Stored $1 complete items | quality $2%"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 状态独立保存$", "Stored $1 complete items | state preserved separately"),
			new PatternTranslation("^库存 ([\\d,]+) 个 \\| 保守状态隔离$", "Stored $1 complete items | conservatively isolated"),
			new PatternTranslation("^库存 ([\\d,]+) 个$", "Stored $1 complete items"),
			new PatternTranslation("^已取出 ([\\d,]+) 单位。$", "Withdrew $1 units."),
			new PatternTranslation("^已取出 ([\\d,]+) 个完整物品。$", "Withdrew $1 complete items."),
			new PatternTranslation("点击取出当前数量（数量）。", "Click to withdraw the current quantity."),
			new PatternTranslation("点击取出当前数量（个）。", "Click to withdraw one complete item."),
			new PatternTranslation("^显示 ([\\d,]+) / ([\\d,]+) 条目；总数量 ([\\d,]+)。$", "Showing $1 / $2 entries; total quantity $3."),
			new PatternTranslation("^ID (\\d+)  ·  ×([\\d,]+)  ·  品质 (\\d+)  ·  无状态$", "ID $1  ·  ×$2  ·  Quality $3  ·  No state"),
			new PatternTranslation("^ID (\\d+)  ·  ×([\\d,]+)  ·  品质 (\\d+)  ·  状态 (\\d+)B$", "ID $1  ·  ×$2  ·  Quality $3  ·  State $4B"),
			new PatternTranslation("^ID (\\d+)  ·  剩余弹药 ([\\d,]+)  ·  无状态$", "ID $1  ·  Ammo left $2  ·  No state"),
			new PatternTranslation("^ID (\\d+)  ·  剩余弹药 ([\\d,]+)  ·  状态 (\\d+)B$", "ID $1  ·  Ammo left $2  ·  State $3B"),
			new PatternTranslation("^ID (\\d+)  ·  剩余弹药 ([\\d,]+)  ·  品质 (\\d+)  ·  无状态$", "ID $1  ·  Ammo left $2  ·  Quality $3  ·  No state"),
			new PatternTranslation("^ID (\\d+)  ·  剩余弹药 ([\\d,]+)  ·  品质 (\\d+)  ·  状态 (\\d+)B$", "ID $1  ·  Ammo left $2  ·  Quality $3  ·  State $4B"),
			new PatternTranslation("^已取出 ([\\d,]+) 个物品。$", "Withdrew $1 items."),
			new PatternTranslation("^准星工具模式已切换为 (.+)。$", "Crosshair tool mode changed to $1.")
		};
		CurrentLanguage = PluginLanguage.Chinese;
	}
}
