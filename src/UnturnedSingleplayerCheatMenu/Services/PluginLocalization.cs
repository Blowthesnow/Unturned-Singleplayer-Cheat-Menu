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
		dictionary["全部"] = "All";
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
		dictionary["仅限 SINGLEPLAYER"] = "SINGLEPLAYER ONLY";
		dictionary["关于此插件"] = "About This Plugin";
		dictionary["作者：Blowthesnow"] = "Author: Blowthesnow";
		dictionary["此插件完全免费。如果你在其他地方购买了此插件，请立刻申请退款。"] = "This plugin is completely free. If you bought it somewhere else, please request a refund immediately.";
		dictionary["项目地址"] = "Project";
		dictionary["爱发电赞助地址"] = "Afdian Sponsorship";
		dictionary["关闭"] = "Close";
		dictionary["设置全局快捷键"] = "Set Global Shortcut";
		dictionary["当前动作："] = "Current action: ";
		dictionary["打开或关闭菜单"] = "Toggle Menu";
		dictionary["当前快捷键："] = "Current shortcut: ";
		dictionary["点击“开始设置”，然后按下要使用的键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，可先按住 Ctrl / Alt / Shift / Win 作为组合键。"] = "Click “Start Setup”, then press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; hold Ctrl / Alt / Shift / Win first for a combination.";
		dictionary["开始设置"] = "Start Setup";
		dictionary["恢复默认"] = "Restore Default";
		dictionary["按键监听中，请按下键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，已按住的 Ctrl / Alt / Shift / Win 会作为组合键。"] = "Listening for a key. Press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; held Ctrl / Alt / Shift / Win keys will be used as modifiers.";
		dictionary["全局快捷键已更新。"] = "Global shortcut updated.";
		dictionary["全局快捷键已更新，但配置文件保存失败。"] = "Global shortcut updated, but the configuration file could not be saved.";
		dictionary["各模式功能介绍"] = "Mode Descriptions";
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
		dictionary["选择颜色"] = "Choose Color";
		dictionary["预览"] = "Preview";
		dictionary["颜色值"] = "Color Value";
		dictionary["常用颜色"] = "Common Colors";
		dictionary["通道"] = "Channels";
		dictionary["红"] = "Red";
		dictionary["绿"] = "Green";
		dictionary["蓝"] = "Blue";
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
		dictionary["打开或关闭单人作弊菜单。默认 End，可自由修改。仅在真正的单人世界中生效。"] = "Open or close the singleplayer cheat menu. Defaults to End and only works in a true singleplayer world.";
		dictionary["菜单缩放，范围 0.75 到 1.5。"] = "Menu scale from 0.75 to 1.5.";
			dictionary["插件窗口"] = "Plugin Window";
		dictionary["当前分类："] = "Current category:";
		dictionary["可编辑动作"] = "Editable actions";
		dictionary["点击某一行的设置后按下键盘键、鼠标中键或鼠标侧键；Escape 取消。"] = "Click Set on an action, then press a keyboard key, middle mouse button, or side mouse button. Escape cancels.";
		dictionary["设置"] = "Set";
		dictionary["默认"] = "Default";
		dictionary["背包整理"] = "Inventory Arrange";
		dictionary["排序预设"] = "Sort Preset";
		dictionary["点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。"] = "Choose an arrange preset directly from the list. Hover an option to read what the preset does.";
		dictionary["安全背包整理"] = "Safe Inventory Arrange";
		dictionary["安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。"] = "Safety: cancel the whole operation when preflight cannot place every item; automatic dropping is never used.";
		dictionary["跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。"] = "Cross-container concentration: while a storage box is open, hold Ctrl or Shift and click an item to transfer every instance of the same asset to the other side; it stops when space runs out and never drops items.";
		dictionary["保护装备"] = "Protect Equipped";
		dictionary["保留热键"] = "Preserve Hotkeys";
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
		dictionary["已保护热键绑定物品。"] = "Hotkey-bound items are protected.";
		dictionary["将允许移动热键绑定物品。"] = "Arrange may move hotkey-bound items.";
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
		dictionary["只归并兼容堆叠，不重排无关物品；弹匣和弹药箱会按兼容组重新分配总弹量。"] = "Merge compatible stacks without reordering unrelated items; magazines and ammo boxes redistribute total ammo by compatibility group.";
		dictionary["按类型、占用格数和名称整理，适合平时的一键整理。"] = "Sort by type, occupied space, and name for everyday one-click arranging.";
		dictionary["尽量把物品挤到前面，保持原来的先后顺序；每次整理仍会自动补齐兼容弹匣和弹药箱。"] = "Push items forward while keeping their order; compatible magazines and ammo boxes are still refilled every time.";
		dictionary["把同类物品放在一起，例如武器、衣服、食物和工具。"] = "Put similar items together, such as weapons, clothing, food, and tools.";
		dictionary["按物品名称从 A 到 Z 排列，方便按名字找东西。"] = "Sort item names from A to Z so they are easier to find.";
		dictionary["先放大件，再用小件填空位，适合整理占格较大的物品。"] = "Place large items first, then fill gaps with small items.";
		dictionary["先放小件，尽量留下连续的大空位。"] = "Place small items first to keep larger continuous spaces open.";
		dictionary["耐久或品质高的物品排在前面。"] = "Put items with higher quality or durability first.";
		dictionary["按稀有度从神话、传奇、史诗、稀有、罕见到普通排列。"] = "Sort rarity from Mythical, Legendary, Epic, Rare, Uncommon, to Common.";
		dictionary["相同物品会自动合堆；兼容的半弹匣或弹药箱会重新分配成满的和空的。"] = "Merge matching stacks automatically; compatible half magazines or ammo boxes become full and empty containers.";
		dictionary["剩余弹量多的弹匣或弹药箱排在前面；读不到弹量的物品不会乱改。"] = "Put magazines or ammo boxes with more ammo first; unreadable ammo is left unchanged.";
		dictionary["绑定快捷栏的物品优先；开启保留热键时，绑定物品不会被移动。"] = "Prioritize hotbar-bound items; with Preserve Hotkeys on, bound items are not moved.";
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
		dictionary["大模型平台发给你的 API Key；不会写入日志。\nOllama 本地服务通常不需要填写。"] = "The API key issued by the large-model platform; it is never logged.\nLocal Ollama usually does not need one.";
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
		dictionary["每次发送给翻译接口的文本数量，范围 1-50。数量越小越稳，但请求次数更多；修改后下次翻译任务生效。"] = "Texts sent per translation request, from 1 to 50. Smaller batches are more reliable but require more requests; changes apply to the next task.";
		dictionary["每批翻译数量"] = "Texts Per Batch";
		dictionary["名称"] = "Name";
		dictionary["排序预设："] = "Sort Preset:";
		dictionary["启用准星交互工具"] = "Enable Point Interaction Tool";
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
		English = dictionary;
		Chinese = new Dictionary<string, string>(StringComparer.Ordinal) { ["Workshop"] = "创意工坊" };
		EnglishPatterns = new PatternTranslation[43]
		{
			new PatternTranslation("^状态：(.+?)；已扫描物品 (\\d+) / 载具 (\\d+)$", "State: $1; scanned items $2 / vehicles $3"),
			new PatternTranslation("^文本总数 (\\d+) · 缓存命中 (\\d+) · 中文跳过 (\\d+) · 待翻译 (\\d+) · 完成 (\\d+) · 失败 (\\d+)$", "Texts $1 · cache hits $2 · Chinese skipped $3 · pending $4 · completed $5 · failed $6"),
			new PatternTranslation("^文本总数 (\\d+) · 缓存命中 (\\d+) · 待翻译 (\\d+) · 完成 (\\d+) · 失败 (\\d+)$", "Texts $1 · cache hits $2 · pending $3 · completed $4 · failed $5"),
			new PatternTranslation("^机翻 API：(.+?)；协议：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation API: $1; protocol: $2; target language: $3; model: $4"),
			new PatternTranslation("^翻译接口：(.+?)；供应商：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation: $1; provider: $2; target language: $3; model: $4"),
			new PatternTranslation("^缓存文件：(.+)$", "Cache file: $1"),
			new PatternTranslation("^筛选 (\\d+)$", "Filter $1"),
			new PatternTranslation("^自动扫描完成：(\\d+) 个物品、(\\d+) 辆载具。$", "Scan complete: $1 items and $2 vehicles."),
			new PatternTranslation("^生命 (.+?)/100    饱食 (.+?)/100    水分 (.+?)/100    免疫 (.+?)/100    体力 (.+?)/100    氧气 (.+?)/100$", "Health $1/100    Food $2/100    Water $3/100    Immunity $4/100    Stamina $5/100    Oxygen $6/100"),
			new PatternTranslation("^经验 (.+?)    声望 (.+?)$", "Experience $1    Reputation $2"),
			new PatternTranslation("^已增加 (.+?) 经验。$", "Added $1 experience."),
			new PatternTranslation("^已变更 (.+?) 声望。$", "Changed reputation by $1."),
			new PatternTranslation("^技能已处理，实际变更 (\\d+) 项。$", "Skills processed; $1 levels changed."),
			new PatternTranslation("^显示 (\\d+) / (\\d+)；模组物品 (\\d+)。搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; mod items: $3. Search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 (\\d+) / (\\d+)；搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 (\\d+) / (\\d+)；模组车辆 (\\d+)。每次生成 1–20 辆。$", "Showing $1 / $2; mod vehicles: $3. Spawn 1–20 at a time."),
			new PatternTranslation("^物品收藏  (\\d+)/(\\d+)$", "Item Favorites  $1/$2"),
			new PatternTranslation("^车辆收藏  (\\d+)/(\\d+)$", "Vehicle Favorites  $1/$2"),
			new PatternTranslation("^显示 (\\d+) 个已加载收藏物品；收藏记录 (\\d+) 个。搜索支持名称、ID、GUID 和来源。$", "Showing $1 loaded favorite items; $2 saved favorites. Search supports name, ID, GUID, and source."),
			new PatternTranslation("^显示 (\\d+) 个已加载收藏物品。$", "Showing $1 loaded favorite items."),
			new PatternTranslation("^当前分类：(.+)$", "Current category: $1"),
			new PatternTranslation("^显示 (\\d+) 辆已加载收藏车辆；收藏记录 (\\d+) 辆。$", "Showing $1 loaded favorite vehicles; $2 saved favorites."),
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
			new PatternTranslation("^物品：(\\d+)（模组 (\\d+)）    车辆：(\\d+)（模组 (\\d+)）$", "Items: $1 (mod $2)    Vehicles: $3 (mod $4)")
		};
		CurrentLanguage = PluginLanguage.Chinese;
	}
}
