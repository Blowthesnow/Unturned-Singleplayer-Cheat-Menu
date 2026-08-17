using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum PluginLanguage
{
    Chinese,
    English
}

internal static class PluginLocalization
{
    private sealed class PatternTranslation
    {
        public PatternTranslation(string pattern, string replacement)
        {
            Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
            Replacement = replacement;
        }

        public Regex Pattern { get; }
        public string Replacement { get; }
    }

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["全部"] = "All",
        ["武器"] = "Weapons",
        ["弹药与配件"] = "Ammo & Attachments",
        ["衣物"] = "Clothing",
        ["食物与饮料"] = "Food & Drinks",
        ["医疗与防护"] = "Medical",
        ["建筑与放置物"] = "Structures",
        ["工具"] = "Tools",
        ["其他"] = "Other",
        ["陆地车辆"] = "Land Vehicles",
        ["固定翼飞机"] = "Planes",
        ["直升机"] = "Helicopters",
        ["飞艇"] = "Blimps",
        ["船只"] = "Boats",
        ["火车"] = "Trains",
        ["准备就绪。"] = "Ready.",
        ["信息"] = "Info",
        ["成功"] = "Success",
        ["提示"] = "Warning",
        ["失败"] = "Error",
        ["无法"] = "Unable",
        ["没有可用"] = "No usable",
        ["没有默认"] = "No default",
        ["单人作弊指令菜单"] = "Singleplayer Cheat Menu",
        ["仅限 SINGLEPLAYER"] = "SINGLEPLAYER ONLY",
        ["未进入地图"] = "No map loaded",
        ["角色"] = "Player",
        ["物品"] = "Items",
        ["车辆"] = "Vehicles",
        ["载具"] = "Vehicles",
        ["收藏"] = "Favorites",
        ["传送"] = "Teleports",
        ["地图"] = "Map",
        ["传送点"] = "Teleport Points",
        ["标记"] = "Marker",
        ["五角星"] = "Star",
        ["正方形"] = "Square",
        ["圆形"] = "Circle",
        ["菱形"] = "Diamond",
        ["颜色"] = "Color",
        ["标志颜色"] = "Marker Color",
        ["选择颜色"] = "Choose Color",
        ["预览"] = "Preview",
        ["颜色值"] = "Color Value",
        ["常用颜色"] = "Common Colors",
        ["通道"] = "Channels",
        ["红"] = "Red",
        ["绿"] = "Green",
        ["蓝"] = "Blue",
        ["房子"] = "House",
        ["宝箱"] = "Chest",
        ["双层圆环"] = "Double Rings",
        ["已切换到地图视图。"] = "Switched to map view.",
        ["已切换到传送点视图。"] = "Switched to teleport points view.",
        ["玩家状态尚未准备完成。"] = "Player state is not ready yet.",
        ["生存状态"] = "Survival Stats",
        ["无敌模式"] = "God Mode",
        ["无敌模式已开启。"] = "God mode enabled.",
        ["无敌模式已关闭。"] = "God mode disabled.",
        ["无限生存状态"] = "Infinite Survival Stats",
        ["无限生存状态已开启。"] = "Infinite survival stats enabled.",
        ["无限生存状态已关闭。"] = "Infinite survival stats disabled.",
        ["设置指定数值（0–100；生命最低 1）"] = "Set exact values (0–100; health minimum 1)",
        ["生命"] = "Health",
        ["饱食"] = "Food",
        ["水分"] = "Water",
        ["免疫"] = "Immunity",
        ["体力"] = "Stamina",
        ["氧气"] = "Oxygen",
        ["应用"] = "Apply",
        ["角色生存数值已设置。"] = "Player survival stats updated.",
        ["设置失败。"] = "Update failed.",
        ["全部恢复"] = "Restore All",
        ["角色状态已全部恢复。"] = "All player stats restored.",
        ["恢复失败。"] = "Restore failed.",
        ["补满生存状态"] = "Refill Survival Stats",
        ["生存状态已补满。"] = "Survival stats refilled.",
        ["操作失败。"] = "Action failed.",
        ["治疗流血/骨折"] = "Heal Bleeding / Fracture",
        ["伤势已治疗。"] = "Injuries healed.",
        ["经验、声望与技能"] = "Experience, Reputation & Skills",
        ["经验"] = "Experience",
        ["增加经验"] = "Add Experience",
        ["增加经验失败。"] = "Failed to add experience.",
        ["声望"] = "Reputation",
        ["变更声望"] = "Change Reputation",
        ["变更声望失败。"] = "Failed to change reputation.",
        ["全部技能满级"] = "Max All Skills",
        ["搜索"] = "Search",
        ["数量"] = "Amount",
        ["筛选"] = "Filter",
        ["清除筛选"] = "Clear Filters",
        ["全部来源"] = "All Sources",
        ["物品类型"] = "Item Type",
        ["来源"] = "Source",
        ["稀有度"] = "Rarity",
        ["装备槽位"] = "Equipment Slot",
        ["射击机制"] = "Firing Mechanism",
        ["射击模式（可多选）"] = "Fire Mode (multi-select)",
        ["重置"] = "Reset",
        ["枪械"] = "Gun",
        ["近战"] = "Melee",
        ["投掷物"] = "Throwable",
        ["弹匣"] = "Magazine",
        ["瞄具"] = "Sight",
        ["战术配件"] = "Tactical",
        ["握把"] = "Grip",
        ["枪管"] = "Barrel",
        ["光学配件"] = "Optic",
        ["帽子"] = "Hat",
        ["上衣"] = "Shirt",
        ["裤子"] = "Pants",
        ["背包"] = "Backpack",
        ["背心"] = "Vest",
        ["面罩"] = "Mask",
        ["眼镜"] = "Glasses",
        ["饮料"] = "Drinks",
        ["滤芯"] = "Filter",
        ["建筑"] = "Structure",
        ["路障"] = "Barricade",
        ["储物"] = "Storage",
        ["陷阱"] = "Trap",
        ["农作物"] = "Farm",
        ["种植器"] = "Grower",
        ["发电机"] = "Generator",
        ["油泵"] = "Oil Pump",
        ["信标"] = "Beacon",
        ["哨戒炮"] = "Sentry",
        ["通用工具"] = "Tool",
        ["燃料"] = "Fuel",
        ["容器"] = "Refill",
        ["钓具"] = "Fishing Gear",
        ["轮胎"] = "Tire",
        ["维修工具"] = "Vehicle Repair Tool",
        ["喷漆工具"] = "Vehicle Paint Tool",
        ["开锁工具"] = "Vehicle Lockpick Tool",
        ["地图 / 其他"] = "Map / Other",
        ["普通"] = "Common",
        ["罕见"] = "Uncommon",
        ["稀有"] = "Rare",
        ["史诗"] = "Epic",
        ["传奇"] = "Legendary",
        ["神话"] = "Mythical",
        ["快捷栏"] = "Hotkey",
        ["主武器"] = "Primary",
        ["副武器"] = "Secondary",
        ["第三槽位"] = "Tertiary",
        ["任意槽位"] = "Any",
        ["常规枪械"] = "Trigger",
        ["栓动"] = "Bolt",
        ["泵动"] = "Pump",
        ["轨道武器"] = "Rail",
        ["弓 / 弩"] = "Bow / Crossbow",
        ["折管"] = "Break-Action",
        ["火箭 / 发射器"] = "Rocket / Launcher",
        ["旋转机枪"] = "Minigun",
        ["半自动"] = "Semi-Automatic",
        ["全自动"] = "Automatic",
        ["点射"] = "Burst",
        ["⚙ 渲染设置"] = "⚙ Render Settings",
        ["载具渲染设置"] = "Vehicle Render Settings",
        ["尺寸"] = "Size",
        ["取景倍率"] = "Framing",
        ["128 × 96（低开销）"] = "128 × 96 (Low Cost)",
        ["192 × 144（平衡）"] = "192 × 144 (Balanced)",
        ["256 × 192（更清晰）"] = "256 × 192 (Sharper)",
        ["缩略图尺寸只影响车辆卡片，不会修改游戏全局画质。已生成的缩略图会进入缓存，重复打开不会重复实例化车辆。"] =
            "Thumbnail size only affects vehicle cards and does not change global game quality. Generated thumbnails are cached, so reopening does not instantiate vehicles again.",
        ["128 × 96 为默认，生成更快且占用更低；192 × 144 是清晰度与开销的折中；256 × 192 的输出像素约为默认的 4 倍，首次生成更慢。"] =
            "128 × 96 is the default with faster generation and lower usage; 192 × 144 balances clarity and cost; 256 × 192 produces about 4x the default pixels and takes longer the first time.",
        ["取景倍率 0.5：主体更大、留白更少，可能接近裁切边界；1.0：默认平衡值；1.5：留白更多、主体更小，裁切风险更低。"] =
            "Framing 0.5: larger subject and less padding, closer to the crop boundary; 1.0: balanced default; 1.5: more padding, smaller subject, and lower crop risk.",
        ["车辆缩略图设置已应用并保存。"] = "Vehicle thumbnail settings applied and saved.",
        ["车辆缩略图设置已应用，但配置文件保存失败。"] =
            "Vehicle thumbnail settings applied, but the config file could not be saved.",
        ["重新扫描"] = "Rescan",
        ["已重新扫描当前加载的原版与模组资产。"] = "Rescanned the currently loaded vanilla and mod assets.",
        ["物品分类"] = "Item Categories",
        ["车辆分类"] = "Vehicle Categories",
        ["收藏物品分类"] = "Favorite Items",
        ["收藏车辆分类"] = "Favorite Vehicles",
        ["当前分类没有已加载的收藏物品。"] = "No loaded favorite items in this category.",
        ["当前分类没有已加载的收藏车辆。"] = "No loaded favorite vehicles in this category.",
        ["请在物品卡片右上角点击 ☆ 收藏。"] = "Click ☆ on an item card to add one.",
        ["请在车辆卡片右上角点击 ☆ 收藏。"] = "Click ☆ on a vehicle card to add one.",
        ["当前分类没有可用物品。"] = "No items are available in this category.",
        ["没有匹配的物品。"] = "No items matched.",
        ["当前分类没有可用车辆。"] = "No vehicles are available in this category.",
        ["没有匹配的车辆。"] = "No vehicles matched.",
        ["请选择其他分类，或重新扫描当前加载的资产。"] = "Choose another category or rescan the currently loaded assets.",
        ["请清空搜索或尝试其他关键词。"] = "Clear the search or try another keyword.",
        ["‹ 上一页"] = "‹ Previous",
        ["下一页 ›"] = "Next ›",
        ["保存当前位置"] = "Save Current Position",
        ["保存位置"] = "Save Position",
        ["当前地图 GPS"] = "Current Map GPS",
        ["GPS/卫星图"] = "GPS / Satellite",
        ["地形图"] = "Terrain Chart",
        ["地图预览"] = "Map Preview",
        ["扫描地图"] = "Scan Map",
        ["居中"] = "Center",
        ["当前地图没有可用的 Map.png / Chart.png。"] = "The current map has no usable Map.png / Chart.png.",
        ["当前地图没有可用 GPS 图层。"] = "No usable GPS map layer is available for the current map.",
        ["请确认地图目录中存在 Map.png 或 Chart.png。"] = "Make sure the map folder contains Map.png or Chart.png.",
        ["请确认地图目录中存在 Map.png 或 Chart.png；仍可使用下方保存的传送点。"] = "Make sure the map folder contains Map.png or Chart.png. Saved teleports below remain available.",
        ["当前地图还没有保存的传送点。"] = "No saved teleports for the current map.",
        ["请先输入名称并保存当前位置；不同地图的传送点会自动隔离。"] = "Enter a name and save the current position first. Teleports are isolated per map.",
        ["进入地图后即可保存和使用传送点。"] = "Enter a map to save and use teleport points.",
        ["左键拖动 · 滚轮缩放 · 悬停标记查看信息 · 右键标记快速传送"] = "Left-drag to pan · scroll to zoom · hover markers for details · right-click a marker to teleport",
        ["地图传送失败：未找到安全落点、目标超出地图边界或玩家正在载具中。"] = "Map teleport failed: no safe landing point was found, the destination is outside the map, or the player is in a vehicle.",
        ["传送失败：目标位置有障碍物且无法放置在障碍物顶部，或玩家正在载具中。"] = "Teleport failed: the target is obstructed and a top placement was not available, or the player is in a vehicle.",
        ["取消"] = "Cancel",
        ["确定"] = "Confirm",
        ["删除"] = "Delete",
        ["还没有保存的传送点。"] = "No teleports saved yet.",
        ["请先输入名称并保存当前位置。"] = "Enter a name and save the current position first.",
        ["确认删除传送点？"] = "Delete this teleport?",
        ["其他地图"] = "Other Map",
        ["传送失败：目标被阻挡、地图不同或玩家正在载具中。"] = "Teleport failed: the destination is blocked, on another map, or the player is in a vehicle.",
        ["时间"] = "Time",
        ["时间百分比"] = "Time Percentage",
        ["设为白天"] = "Set Day",
        ["已切换到白天。"] = "Switched to daytime.",
        ["设为夜晚"] = "Set Night",
        ["已切换到夜晚。"] = "Switched to nighttime.",
        ["冻结时间"] = "Freeze Time",
        ["时间已冻结。"] = "Time frozen.",
        ["时间已恢复流动。"] = "Time resumed.",
        ["强制满月"] = "Force Full Moon",
        ["已开启满月。"] = "Full moon enabled.",
        ["已关闭满月。"] = "Full moon disabled.",
        ["世界事件"] = "World Events",
        ["立即呼叫空投"] = "Call Airdrop Now",
        ["空投已呼叫。"] = "Airdrop requested.",
        ["此地图没有可用的空投节点或货物表。"] = "This map has no usable airdrop node or loot table.",
        ["下雨"] = "Rain",
        ["已触发雨天。"] = "Rain triggered.",
        ["当前地图没有默认雨天资产。"] = "This map has no default rain asset.",
        ["暴雪"] = "Snowstorm",
        ["已触发暴雪。"] = "Snowstorm triggered.",
        ["当前地图没有默认雪天资产。"] = "This map has no default snow asset.",
        ["清除当前天气"] = "Clear Current Weather",
        ["当前天气已清除，后续仍可自然调度。"] = "Current weather cleared; natural scheduling remains enabled.",
        ["关闭天气调度"] = "Disable Weather Scheduling",
        ["当前天气和自然天气调度已关闭。"] = "Current weather and natural weather scheduling disabled.",
        ["重新扫描模组资产"] = "Rescan Mod Assets",
        ["资产目录已重新扫描。"] = "Asset catalog rescanned.",
        ["扫描信息"] = "Scan Information",
        ["只显示 Unturned 当前已成功加载的原版、地图随附和 Workshop 资产。"] = "Only vanilla, map-provided, and Workshop assets successfully loaded by Unturned are shown.",
        ["输入…"] = "Enter text…",
        ["加载预览…"] = "Loading preview…",
        ["无预览"] = "No Preview",
        ["模组"] = "Mod",
        ["原版"] = "Vanilla",
        ["未知来源"] = "Unknown Source",
        ["尚未进入世界。"] = "No world is loaded yet.",
        ["此菜单只允许在真正的单人世界中使用。"] = "This menu is restricted to true singleplayer worlds.",
        ["本地玩家尚未生成。"] = "The local player has not spawned yet.",
        ["打开或关闭单人作弊菜单。默认 End，可自由修改。仅在真正的单人世界中生效。"] = "Open or close the singleplayer cheat menu. Defaults to End and only works in a true singleplayer world.",
        ["菜单缩放，范围 0.75 到 1.5。"] = "Menu scale from 0.75 to 1.5.",
        ["物品/车辆每页卡片数量，范围 12 到 80。"] = "Item/vehicle cards per page from 12 to 80."
    };

    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Workshop"] = "创意工坊"
    };

    private static readonly PatternTranslation[] EnglishPatterns =
    {
        new(@"^筛选 (\d+)$", "Filter $1"),
        new(@"^自动扫描完成：(\d+) 个物品、(\d+) 辆载具。$", "Scan complete: $1 items and $2 vehicles."),
        new(@"^生命 (.+?)/100    饱食 (.+?)/100    水分 (.+?)/100    免疫 (.+?)/100    体力 (.+?)/100    氧气 (.+?)/100$", "Health $1/100    Food $2/100    Water $3/100    Immunity $4/100    Stamina $5/100    Oxygen $6/100"),
        new(@"^经验 (.+?)    声望 (.+?)$", "Experience $1    Reputation $2"),
        new(@"^已增加 (.+?) 经验。$", "Added $1 experience."),
        new(@"^已变更 (.+?) 声望。$", "Changed reputation by $1."),
        new(@"^技能已处理，实际变更 (\d+) 项。$", "Skills processed; $1 levels changed."),
        new(@"^显示 (\d+) / (\d+)；模组物品 (\d+)。搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; mod items: $3. Search supports name, ID, GUID, and source."),
        new(@"^显示 (\d+) / (\d+)；搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; search supports name, ID, GUID, and source."),
        new(@"^显示 (\d+) / (\d+)；模组车辆 (\d+)。每次生成 1–20 辆。$", "Showing $1 / $2; mod vehicles: $3. Spawn 1–20 at a time."),
        new(@"^物品收藏  (\d+)/(\d+)$", "Item Favorites  $1/$2"),
        new(@"^车辆收藏  (\d+)/(\d+)$", "Vehicle Favorites  $1/$2"),
        new(@"^显示 (\d+) 个已加载收藏物品；收藏记录 (\d+) 个。搜索支持名称、ID、GUID 和来源。$", "Showing $1 loaded favorite items; $2 saved favorites. Search supports name, ID, GUID, and source."),
        new(@"^显示 (\d+) 个已加载收藏物品。$", "Showing $1 loaded favorite items."),
        new(@"^当前分类：(.+)$", "Current category: $1"),
        new(@"^显示 (\d+) 辆已加载收藏车辆；收藏记录 (\d+) 辆。$", "Showing $1 loaded favorite vehicles; $2 saved favorites."),
        new(@"^已给予 (\d+) × (.+)。$", "Gave $1 × $2."),
        new(@"^无法给予 (.+)。$", "Could not give $1."),
        new(@"^已在玩家前方生成 (\d+) × (.+)。$", "Spawned $1 × $2 in front of the player."),
        new(@"^无法生成 (.+)。$", "Could not spawn $1."),
        new(@"^无法保存 (.+) 的收藏状态。$", "Could not save the favorite state for $1."),
        new(@"^已收藏物品：(.+)。$", "Added item to favorites: $1."),
        new(@"^已取消收藏物品：(.+)。$", "Removed item from favorites: $1."),
        new(@"^已收藏车辆：(.+)。$", "Added vehicle to favorites: $1."),
        new(@"^已取消收藏车辆：(.+)。$", "Removed vehicle from favorites: $1."),
        new(@"^第 (\d+) / (\d+) 页$", "Page $1 / $2"),
        new(@"^地图 (.+?)    X (.+?)    Y (.+?)    Z (.+?)$", "Map $1    X $2    Y $3    Z $4"),
        new(@"^已保存传送点：(.+)。$", "Saved teleport point: $1."),
        new(@"^已保存位置（(\d+)）$", "Saved Positions ($1)"),
        new(@"^当前地图传送点（(\d+)）$", "Current Map Teleports ($1)"),
        new(@"^已传送到 (.+)。$", "Teleported to $1."),
        new(@"^已删除传送点：(.+)。$", "Deleted teleport point: $1."),
        new(@"^当前时间：(.+?) / (.+?)（(.+?)）$", "Current time: $1 / $2 ($3)"),
        new(@"^时间已设置为周期的 (\d+)% 。$", "Time set to $1% of the cycle."),
        new(@"^物品：(\d+)（模组 (\d+)）    车辆：(\d+)（模组 (\d+)）$", "Items: $1 (mod $2)    Vehicles: $3 (mod $4)")
    };

    public static PluginLanguage CurrentLanguage { get; private set; } = PluginLanguage.Chinese;

    public static string CurrentLanguageName => CurrentLanguage == PluginLanguage.English ? "English" : "Chinese";

    public static string SwitchButtonLabel => CurrentLanguage == PluginLanguage.English ? "中文" : "EN";

    public static void Initialize(string configuredLanguage, string unturnedLanguage)
    {
        CurrentLanguage = Resolve(configuredLanguage, unturnedLanguage);
    }

    public static PluginLanguage Resolve(string configuredLanguage, string unturnedLanguage)
    {
        string configured = Normalize(configuredLanguage);
        if (configured is "english" or "en" or "enus")
            return PluginLanguage.English;
        if (configured is "chinese" or "zh" or "zhcn" or "zhtw" or "zhhans" or "zhhant"
            or "simplifiedchinese" or "traditionalchinese" or "schinese" or "tchinese")
            return PluginLanguage.Chinese;

        string gameLanguage = Normalize(unturnedLanguage);
        return gameLanguage.IndexOf("chinese", StringComparison.Ordinal) >= 0
            ? PluginLanguage.Chinese
            : PluginLanguage.English;
    }

    public static string Translate(string source)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (CurrentLanguage == PluginLanguage.Chinese)
            return Chinese.TryGetValue(source, out string chineseTranslation) ? chineseTranslation : source;

        if (English.TryGetValue(source, out string translation))
            return translation;

        foreach (PatternTranslation pattern in EnglishPatterns)
        {
            if (pattern.Pattern.IsMatch(source))
                return pattern.Pattern.Replace(source, pattern.Replacement);
        }

        return source;
    }

    public static string DefaultTeleportName(int number)
    {
        return CurrentLanguage == PluginLanguage.English ? $"Location {number}" : $"位置 {number}";
    }

    public static string LanguageChangedMessage(bool persisted)
    {
        if (CurrentLanguage == PluginLanguage.English)
        {
            return persisted
                ? "Language switched to English."
                : "Language switched to English, but the config file could not be saved.";
        }

        return persisted
            ? "语言已切换为中文。"
            : "语言已切换为中文，但配置文件保存失败。";
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }
}
