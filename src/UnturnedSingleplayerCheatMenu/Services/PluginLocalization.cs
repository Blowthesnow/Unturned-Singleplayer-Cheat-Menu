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
        ["单人作弊指令菜单"] = "Singleplayer Cheat Menu",
        ["仅限 SINGLEPLAYER"] = "SINGLEPLAYER ONLY",
        ["未进入地图"] = "No map loaded",
        ["角色"] = "Player",
        ["物品"] = "Items",
        ["车辆"] = "Vehicles",
        ["收藏"] = "Favorites",
        ["传送"] = "Teleports",
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
        ["重新扫描"] = "Rescan",
        ["已重新扫描当前加载的原版与模组资产。"] = "Rescanned the currently loaded vanilla and mod assets.",
        ["物品分类"] = "Item Categories",
        ["车辆分类"] = "Vehicle Categories",
        ["收藏物品分类"] = "Favorite Items",
        ["收藏车辆分类"] = "Favorite Vehicles",
        ["当前分类没有已加载的收藏物品。请在物品卡片右上角点击 ☆ 收藏。"] = "No loaded favorite items in this category. Click ☆ on an item card to add one.",
        ["当前分类没有已加载的收藏车辆。请在车辆卡片右上角点击 ☆ 收藏。"] = "No loaded favorite vehicles in this category. Click ☆ on a vehicle card to add one.",
        ["‹ 上一页"] = "‹ Previous",
        ["下一页 ›"] = "Next ›",
        ["保存当前位置"] = "Save Current Position",
        ["保存位置"] = "Save Position",
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

    private static readonly PatternTranslation[] EnglishPatterns =
    {
        new(@"^自动扫描完成：(\d+) 个物品、(\d+) 辆载具。$", "Scan complete: $1 items and $2 vehicles."),
        new(@"^生命 (.+?)/100    饱食 (.+?)/100    水分 (.+?)/100    免疫 (.+?)/100    体力 (.+?)/100    氧气 (.+?)/100$", "Health $1/100    Food $2/100    Water $3/100    Immunity $4/100    Stamina $5/100    Oxygen $6/100"),
        new(@"^经验 (.+?)    声望 (.+?)$", "Experience $1    Reputation $2"),
        new(@"^已增加 (.+?) 经验。$", "Added $1 experience."),
        new(@"^已变更 (.+?) 声望。$", "Changed reputation by $1."),
        new(@"^技能已处理，实际变更 (\d+) 项。$", "Skills processed; $1 levels changed."),
        new(@"^显示 (\d+) / (\d+)；模组物品 (\d+)。搜索支持名称、ID、GUID 和来源。$", "Showing $1 / $2; mod items: $3. Search supports name, ID, GUID, and source."),
        new(@"^显示 (\d+) / (\d+)；模组车辆 (\d+)。每次生成 1–20 辆。$", "Showing $1 / $2; mod vehicles: $3. Spawn 1–20 at a time."),
        new(@"^物品收藏  (\d+)/(\d+)$", "Item Favorites  $1/$2"),
        new(@"^车辆收藏  (\d+)/(\d+)$", "Vehicle Favorites  $1/$2"),
        new(@"^显示 (\d+) 个已加载收藏物品；收藏记录 (\d+) 个。搜索支持名称、ID、GUID 和来源。$", "Showing $1 loaded favorite items; $2 saved favorites. Search supports name, ID, GUID, and source."),
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
        if (CurrentLanguage != PluginLanguage.English || string.IsNullOrEmpty(source))
            return source;

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
