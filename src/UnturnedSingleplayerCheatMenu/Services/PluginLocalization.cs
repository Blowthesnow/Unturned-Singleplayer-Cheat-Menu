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
        ["关于此插件"] = "About This Plugin",
        ["作者：Blowthesnow"] = "Author: Blowthesnow",
        ["此插件完全免费。如果你在其他地方购买了此插件，请立刻申请退款。"] = "This plugin is completely free. If you bought it somewhere else, please request a refund immediately.",
        ["项目地址"] = "Project",
        ["爱发电赞助地址"] = "Afdian Sponsorship",
        ["关闭"] = "Close",
        ["设置全局快捷键"] = "Set Global Shortcut",
        ["当前动作："] = "Current action: ",
        ["打开或关闭菜单"] = "Toggle Menu",
        ["当前快捷键："] = "Current shortcut: ",
        ["点击“开始设置”，然后按下要使用的键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，可先按住 Ctrl / Alt / Shift / Win 作为组合键。"] = "Click “Start Setup”, then press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; hold Ctrl / Alt / Shift / Win first for a combination.",
        ["开始设置"] = "Start Setup",
        ["恢复默认"] = "Restore Default",
        ["按键监听中，请按下键盘键、鼠标中键或鼠标侧键；鼠标左键和右键不可用，已按住的 Ctrl / Alt / Shift / Win 会作为组合键。"] = "Listening for a key. Press a keyboard key, middle mouse button, or side mouse button. Left and right mouse buttons are unavailable; held Ctrl / Alt / Shift / Win keys will be used as modifiers.",
        ["全局快捷键已更新。"] = "Global shortcut updated.",
        ["全局快捷键已更新，但配置文件保存失败。"] = "Global shortcut updated, but the configuration file could not be saved.",
        ["各模式功能介绍"] = "Mode Descriptions",
        ["根据准星目标自动选择单一动作：维修、实用、准星传送、检查或 Shift + 中键删除。"] = "Automatically chooses one action for the crosshair target: repair, utility, crosshair teleport, inspect, or Shift + middle-click delete.",
        ["查看准星指向对象的名称、ID / GUID、生命 / 耐久和位置等信息。"] = "Shows the crosshair target's name, ID / GUID, health / durability, position, and other details.",
        ["维修准星指向的受损车辆、路障或建筑。"] = "Repairs a damaged vehicle, barricade, or structure under the crosshair.",
        ["将玩家传送到准星指向的世界表面，并自动寻找安全落点。"] = "Teleports the player to the world surface under the crosshair and finds a safe landing point automatically.",
        ["执行准星指向对象的原生交互，例如上车、打开容器或切换设备。"] = "Performs the target's native interaction, such as entering a vehicle, opening storage, or toggling a device.",
        ["删除准星指向的可删除对象；必须同时按住 Shift 再点击中键。"] = "Deletes a removable target under the crosshair; hold Shift while clicking the middle mouse button.",
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
        ["选择颜色"] = "Choose Color",
        ["预览"] = "Preview",
        ["颜色值"] = "Color Value",
        ["常用颜色"] = "Common Colors",
        ["通道"] = "Channels",
        ["红"] = "Red",
        ["绿"] = "Green",
        ["蓝"] = "Blue",
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
        ["高稀有度优先（神话→普通）"] = "High Rarity First (Mythical to Common)",
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
        ["插件窗口"] = "Plugin Window",
        ["可编辑动作"] = "Editable actions",
        ["点击某一行的设置后按下键盘键、鼠标中键或鼠标侧键；Escape 取消。"] = "Click Set on an action, then press a keyboard key, middle mouse button, or side mouse button. Escape cancels.",
        ["设置"] = "Set",
        ["默认"] = "Default",
        ["背包整理"] = "Inventory Arrange",
        ["排序预设"] = "Sort Preset",
        ["点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。"] = "Choose an arrange preset directly from the list. Hover an option to read what the preset does.",
        ["安全背包整理"] = "Safe Inventory Arrange",
        ["安全策略：预检无法完整安置时取消整次操作；不调用自动掉落。"] = "Safety: cancel the whole operation when preflight cannot place every item; automatic dropping is never used.",
        ["跨箱集中：打开储物箱时，按住 Ctrl 或 Shift 点击物品，会把同一资产的物品逐件转移到另一侧；目标空间不足时停止，不会丢弃物品。"] = "Cross-container concentration: while a storage box is open, hold Ctrl or Shift and click an item to transfer every instance of the same asset to the other side; it stops when space runs out and never drops items.",
        ["保护装备"] = "Protect Equipped",
        ["保留热键"] = "Preserve Hotkeys",
        ["撤销上次整理"] = "Undo Last Arrange",
        ["仅归并"] = "Merge Only",
        ["仅压缩"] = "Compact Only",
        ["快捷键可在窗口右下角的全局快捷键中设置。"] = "Shortcuts can be configured in the global shortcut window at the bottom-right of the menu.",
        ["自动旋转"] = "Auto Rotate",
        ["允许跨页"] = "Allow Cross-Page",
        ["预览当前"] = "Preview Current",
        ["整理当前"] = "Arrange Current",
        ["整理全部随身"] = "Arrange All Carried",
        ["整理将允许自动旋转物品。"] = "Arrange will allow automatic item rotation.",
        ["整理已禁止自动旋转物品。"] = "Arrange will not rotate items automatically.",
        ["整理将允许在当前范围内跨页安置。"] = "Arrange may place items on another page in the current scope.",
        ["整理将保持原页范围。"] = "Arrange will keep items on their original pages.",
        ["已保护当前装备物品。"] = "Equipped items are protected.",
        ["将允许整理当前装备物品。"] = "Arrange may move equipped items.",
        ["已保护热键绑定物品。"] = "Hotkey-bound items are protected.",
        ["将允许移动热键绑定物品。"] = "Arrange may move hotkey-bound items.",
        ["没有可撤销的整理记录。"] = "There is no arrange operation to undo.",
        ["自定义排序链（最多 5 项）：Key:Direction,Key:Direction"] = "Custom sort chain (up to 5 keys): Key:Direction,Key:Direction",
        ["弹药管理"] = "Ammo Management",
        ["背包整理尚未准备完成。"] = "Inventory arrange is not ready.",
        ["物品/车辆每页卡片数量，范围 12 到 80。"] = "Item/vehicle cards per page from 12 to 80.",



        ["点击后从列表直接选择一个整理预设，预设的具体规则可将鼠标移到选项上查看。"] = "Choose an arrange preset directly from the list. Hover an option to read what the preset does.",
        ["允许物品在寻找位置时旋转 90°；关闭后保持原旋转。"] = "Allow items to rotate 90 degrees while finding space; when off, keep their original rotation.",
        ["允许在当前整理范围的随身页之间寻找空位；关闭后保持原页范围。"] = "Search other carried pages in the current arrange scope; when off, keep the original page.",
        ["保护当前装备物品；开启时不会移除或重排它。"] = "Protect the currently equipped item; it will not be removed or rearranged.",
        ["保护绑定热键的物品，避免整理后快捷栏失效。"] = "Protect hotkey-bound items so the hotbar remains valid after arranging.",

        ["只生成移动、归并和跨页摘要，不修改库存。"] = "Show move, merge, and cross-page counts without changing the inventory.",
        ["整理当前上下文：打开储物箱时整理储物箱，否则整理随身容器。"] = "Arrange the current context: storage when a box is open, otherwise carried containers.",
        ["整理所有随身页，不处理当前储物箱；默认保护装备和热键。"] = "Arrange all carried pages, excluding the open storage; equipped and hotkey items are protected by default.",
        ["只压缩空间并保持相对顺序；不按普通预设重排，自动补弹仍会执行。"] = "Compact space while keeping relative order; no normal preset reorder, but automatic ammo redistribution still runs.",
        ["撤销最近一次成功整理；外部改变库存后撤销会失效。"] = "Undo the latest successful arrange; undo expires after external inventory changes.",
        ["只归并兼容堆叠，不重排无关物品；弹匣和弹药箱会按兼容组重新分配总弹量。"] = "Merge compatible stacks without reordering unrelated items; magazines and ammo boxes redistribute total ammo by compatibility group.",
        ["按类型、占用格数和名称整理，适合平时的一键整理。"] = "Sort by type, occupied space, and name for everyday one-click arranging.",
        ["尽量把物品挤到前面，保持原来的先后顺序；每次整理仍会自动补齐兼容弹匣和弹药箱。"] = "Push items forward while keeping their order; compatible magazines and ammo boxes are still refilled every time.",
        ["把同类物品放在一起，例如武器、衣服、食物和工具。"] = "Put similar items together, such as weapons, clothing, food, and tools.",
        ["按物品名称从 A 到 Z 排列，方便按名字找东西。"] = "Sort item names from A to Z so they are easier to find.",
        ["先放大件，再用小件填空位，适合整理占格较大的物品。"] = "Place large items first, then fill gaps with small items.",
        ["先放小件，尽量留下连续的大空位。"] = "Place small items first to keep larger continuous spaces open.",
        ["耐久或品质高的物品排在前面。"] = "Put items with higher quality or durability first.",
        ["按稀有度从神话、传奇、史诗、稀有、罕见到普通排列。"] = "Sort rarity from Mythical, Legendary, Epic, Rare, Uncommon, to Common.",
        ["相同物品会自动合堆；兼容的半弹匣或弹药箱会重新分配成满的和空的。"] = "Merge matching stacks automatically; compatible half magazines or ammo boxes become full and empty containers.",
        ["剩余弹量多的弹匣或弹药箱排在前面；读不到弹量的物品不会乱改。"] = "Put magazines or ammo boxes with more ammo first; unreadable ammo is left unchanged.",
        ["绑定快捷栏的物品优先；开启保留热键时，绑定物品不会被移动。"] = "Prioritize hotbar-bound items; with Preserve Hotkeys on, bound items are not moved.",
        ["尽量保持原来的页、位置和旋转，只做必要的整理。"] = "Keep the original page, position, and rotation whenever possible.",
        ["按下面输入框里的排序链执行，例如 Name:Descending,Area:Ascending。"] = "Use the sort chain in the input below, for example Name:Descending,Area:Ascending.",
        ["按安全的默认规则整理。"] = "Arrange using the safe default rules.",

        ["翻译"] = "Translation",
        ["Workshop 翻译"] = "Workshop Translation",
        ["API 设置"] = "API Settings",
        ["Endpoint"] = "Endpoint",
        ["API Key"] = "API Key",
        ["Model"] = "Model",
        ["API Key / App ID"] = "API Key / App ID",
        ["Secret Key"] = "Secret Key",
        ["Region"] = "Region",
        ["Project ID"] = "Project ID",
        ["Model（大模型）"] = "Model (large model)",
        ["接口地址"] = "Endpoint",
        ["API Key / 应用 ID"] = "API Key / App ID",
        ["密钥 / Secret Key"] = "Secret Key",
        ["区域"] = "Region",
        ["项目 ID"] = "Project ID",
        ["模型名称"] = "Model",
        ["开始翻译（未缓存文本）"] = "Start translation (uncached text)",
        ["请求发送到哪里。大模型通常是 Chat/Message 地址；云翻译使用供应商的 REST 地址。可保留默认值，也可以改成代理地址。"] = "Where requests are sent. Large models usually use a Chat/Message URL; cloud translation uses the provider REST URL. You can keep the default or use a proxy.",
        ["云翻译供应商发给你的访问密钥或应用 ID。"] = "The access key or application ID issued by the cloud translation provider.",
        ["大模型平台发给你的 API Key；不会写入日志。\nOllama 本地服务通常不需要填写。"] = "The API key issued by the large-model platform; it is never logged.\nLocal Ollama usually does not need one.",
        ["部分云翻译供应商需要的第二段密钥：腾讯云 SecretKey、百度 Secret Key 或有道密钥。"] = "The second credential required by some cloud providers: Tencent SecretKey, Baidu Secret Key, or Youdao secret.",
        ["云服务资源所在区域。例如腾讯云 ap-guangzhou、微软 Translator 的资源区域。"] = "The cloud resource region, such as Tencent ap-guangzhou or the Microsoft Translator resource region.",
        ["云项目或资源的数字/字符串 ID；没有要求的供应商可以留空。"] = "The cloud project or resource ID; leave it empty when the provider does not require one.",
        ["大模型平台上的模型名称，例如 deepseek-chat、gpt-4o-mini、claude-3-5-sonnet。云机器翻译通常不需要填写。"] = "The model name on the large-model platform, such as deepseek-chat, gpt-4o-mini, or claude-3-5-sonnet. Cloud translation usually does not need this.",
        ["先扫描当前已加载的 Workshop 资产，再翻译缓存中没有的文本；已缓存文本不会重复请求。"] = "Scans currently loaded Workshop assets first, then translates only text missing from the cache; cached text is not requested again.",
        ["腾讯云 TMT"] = "Tencent Cloud TMT",
        ["百度翻译"] = "Baidu Translate",
        ["OpenAI 兼容"] = "OpenAI-compatible",
        ["OpenAI"] = "OpenAI",
        ["Azure OpenAI"] = "Azure OpenAI",
        ["Anthropic"] = "Anthropic",
        ["Google Gemini"] = "Google Gemini",
        ["Ollama"] = "Ollama",
        ["OpenRouter"] = "OpenRouter",
        ["DeepSeek"] = "DeepSeek",
        ["SiliconFlow"] = "SiliconFlow",
        ["LM Studio"] = "LM Studio",
        ["腾讯混元"] = "Tencent Hunyuan",
        ["百度千帆"] = "Baidu Qianfan",
        ["Workshop 模组物品翻译"] = "Workshop Mod Item Translation",
        ["只读取当前已加载资产；不会修改 Workshop 原始文件、.dat 或 AssetBundle。"] = "Reads only currently loaded assets; it does not modify Workshop files, .dat files, or AssetBundles.",
        ["翻译服务不可用"] = "Translation service unavailable",
        ["插件尚未完成 Workshop 翻译服务初始化。"] = "The Workshop translation service has not finished initializing.",
        ["启用缓存译文"] = "Enable cached translations",
        ["关闭后立即回退到原始 Getter 文本。"] = "Turning this off immediately falls back to original Getter text.",
        ["应用到原生显示"] = "Apply to native display",
        ["覆盖 ItemAsset、VehicleAsset 和 Workshop Action 的显示 Getter。"] = "Overrides display Getters for ItemAsset, VehicleAsset, and Workshop Actions.",
        ["资产刷新后自动扫描"] = "Scan automatically after asset refresh",
        ["自动翻译未缓存"] = "Translate uncached text automatically",
        ["需要已配置 API；默认关闭以避免意外费用。"] = "Requires a configured API; disabled by default to avoid unexpected charges.",
        ["扫描并应用缓存"] = "Scan and apply cache",
        ["重新加载缓存"] = "Reload cache",
        ["翻译未缓存文本"] = "Translate uncached text",
        ["强制重译当前文本"] = "Force retranslate current text",
        ["取消"] = "Cancel",
        ["Workshop 未缓存文本翻译已启动。"] = "Workshop uncached-text translation started.",
        ["未能启动 Workshop 翻译，请检查 API 配置或当前任务状态。"] = "Could not start Workshop translation; check API configuration or the current task state.",
        ["Workshop 强制重译已启动。"] = "Workshop force retranslation started.",
        ["未能启动 Workshop 强制重译，请检查 API 配置或当前任务状态。"] = "Could not start Workshop force retranslation; check API configuration or the current task state.",
        ["扫描中"] = "Scanning",
        ["翻译中"] = "Translating",
        ["已取消"] = "Cancelled",
        ["错误"] = "Error",
        ["空闲"] = "Idle",
        ["已配置"] = "Configured",
        ["未配置"] = "Not configured",
        ["未填写"] = "Not set",    };

    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Workshop"] = "创意工坊"
    };

    private static readonly PatternTranslation[] EnglishPatterns =
    {
        new(@"^状态：(.+?)；已扫描物品 (\d+) / 载具 (\d+)$", "State: $1; scanned items $2 / vehicles $3"),
        new(@"^文本总数 (\d+) · 缓存命中 (\d+) · 中文跳过 (\d+) · 待翻译 (\d+) · 完成 (\d+) · 失败 (\d+)$", "Texts $1 · cache hits $2 · Chinese skipped $3 · pending $4 · completed $5 · failed $6"),
        new(@"^文本总数 (\d+) · 缓存命中 (\d+) · 待翻译 (\d+) · 完成 (\d+) · 失败 (\d+)$", "Texts $1 · cache hits $2 · pending $3 · completed $4 · failed $5"),
        new(@"^机翻 API：(.+?)；协议：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation API: $1; protocol: $2; target language: $3; model: $4"),
        new(@"^翻译接口：(.+?)；供应商：(.+?)；目标语言：(.+?)；模型：(.+)$", "Translation: $1; provider: $2; target language: $3; model: $4"),
        new(@"^缓存文件：(.+)$", "Cache file: $1"),
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
        new(@"^排序预设：(.+?)；默认禁止自动掉落。$", "Preset: $1; automatic dropping is disabled by default."),
        new(@"^切换排序预设：(.+?)$", "Open preset list: $1"),
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
