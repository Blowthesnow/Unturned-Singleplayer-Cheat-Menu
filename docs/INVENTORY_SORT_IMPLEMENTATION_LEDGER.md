# InventorySortMod 1.5.0 重新实现落地账本

> 依据：`H:\InventorySortMod_1.5.0_analysis_plan_optimized.md`
> 更新日期：2026-08-21
>
> 本账本记录本项目中的重新实现，不复制参考 DLL 的反编译代码，也不重新启用仓库中废弃的整理服务、旧锁定区、旧快照或原生背包注入层。

## 阶段 0：运行时契约核对

状态：已完成静态核对。

当前 `lib\Assembly-CSharp.dll` 通过 ILSpy 核对到：

- `Player.InitializePlayer()`：无参数、`internal`。
- `PlayerInventory.ReceiveDragItem(byte, byte, byte, byte, byte, byte, byte)`。
- `PlayerInventory.PAGES = 9`、`BACKPACK = 3`、`STORAGE = 7`。
- `PlayerInventory.isStoring`、`PlayerInventory.items`、`getIndex`、`getItem`、`removeItem`、`tryAddItem(Item, byte, byte, byte, byte)`。
- `Items.items`、`Items.tryAddItem`、`Items.removeItem`、`tryFindSpace`。
- `ItemJar.x/y/rot/size_x/size_y/item` 与 `GetAsset()`。
- `Item.GetAsset()`、`id/amount/quality/state`。
- `PlayerEquipment.checkSelection`、`isBusy`、`dequip`、`hotkeys`。
- `HotkeyInfo.id/page/x/y`。
- `ItemGunAsset` 的弹药状态：`state[10]` 在当前程序集的 `getState`/描述逻辑中代表弹药数量；已通过按资产类型限制的读取器接入。

证据：`lib\Assembly-CSharp.dll`，2026-08-13 00:58:49；ILSpy 11.0.0.9375 输出。

## 阶段 1：排序器与规划器

状态：已编码、已编译、静态 Smoke 通过。

实际文件：

- `src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangeModels.cs`
- `src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangePlanner.cs`
- `src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryStateReader.cs`
- `tests/InventoryArrangePlannerSmoke/Program.cs`
- `tests/InventoryArrangePlannerSmoke/InventoryArrangePlannerSmoke.csproj`

已实现：

- 兼容、紧凑、类型、名称、大件、小件、品质、稀有度、堆叠填充、弹药、热键、低扰动和自定义排序链预设。
- 多级排序与 GUID/状态/原页/原始序号稳定键。
- 旋转、跨页、装备/热键保护、不可完整安置时整次失败。
- 资产 GUID + 品质 + 状态哈希 + 最大堆叠数归并；不以旧 ID 单独作为身份。
- 弹药读取：枪械使用 `ItemGunAsset.state[10]`，弹匣使用 `Item.amount`；未知类型按 Unavailable 降级。
- 弹药读取仅对 `ItemGunAsset` 且状态长度足够时启用；不可用时按 Unavailable 降级。

Smoke 覆盖：重复规划稳定性、满容器失败、兼容归并、仅压缩不归并、零数量物品、不同状态不归并、保护物品位置、六个首批预设、低扰动、稀有度、弹药和自定义排序链。

## 阶段 2：安全执行器与撤销

状态：已编码、已编译；真实游戏执行证据未完成。



已实现：

- 本地单人门禁、装备忙碌门禁、未知物品取消。
- 执行前快照、完整规划、原生 `tryAddItem` 定位放置。
- 执行后内容指纹校验；失败时恢复原始库存。
- 默认不调用任何掉落 API。
- 单次 Journal：记录实际页集合、布局指纹；外部库存变化后撤销失效。
- 预览不修改真实库存。

尚缺：真实游戏中逐项核对物品数量、位置、状态字节、装备和热键的运行日志证据。

## 阶段 3：快捷键入口

状态：已编码、已编译、Smoke 通过；真实游戏捕获/触发未完成。

实际文件：

- `src/UnturnedSingleplayerCheatMenu/Services/GlobalShortcutManager.cs`
- `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs`
- `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs`

全局快捷键动作：

- 菜单开关：默认 `End`。
- 整理当前容器：默认 `Ctrl+I`。
- 整理全部随身容器：默认 `Ctrl+Shift+I`。

快捷键均注册到现有右下角键盘按钮打开的“设置全局快捷键”窗口；每项都支持捕获、恢复默认、持久化和重复绑定拒绝。

## 阶段 4：原生快速转移通道的同物品跨箱集中

状态：已编码、已编译；已改为接入当前程序集的原生快速转移点击入口，真实游戏仍需验收。

实际文件：

- `src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangePatch.cs`
- `src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangeFeature.cs`

入口：接入 `PlayerDashboardInventoryUI.onGrabbedItem` 和 `onSelectedItem`，兼容当前原生 `ControlsSettings.other`（默认 Ctrl）以及 Shift。点击一件物品后，插件只调用原生 `sendDragItem`，每次等待源格子确认移除再继续处理同一 `Item.id + Asset GUID` 的其他实例；目标空间不足或同步超时即停止，不调用掉落 API。

## 阶段 5：菜单入口与设置

状态：已编码、已编译、本地化 Smoke 通过；真实 UI 截图/交互未验收。

工具页已加入：

- 当前/全部随身整理。
- 预览、仅压缩、仅归并、撤销。
- 预设循环：兼容、紧凑、类型、名称、大件、小件、品质、稀有度、堆叠、热键、弹药、低扰动、自定义。
- 自动旋转、允许跨页、保护装备、保留热键、原生快速转移通道的同物品跨箱集中。
- 安全策略提示：预检失败取消、不自动掉落。

## 构建与测试证据

2026-08-21：

- `dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release --nologo`：通过，0 警告，0 错误。
- 全部 Smoke 项目：通过，包括 `InventoryArrangePlannerSmoke`、`LocalizationSmoke`、`ShortcutToggleSmoke`、`PointToolActionSmoke`、`ItemFilteringSmoke`、`MovementSpeedSmoke`、`TeleportSerializationSmoke`、`FavoritesSerializationSmoke`、`VehicleThumbnailSettingsSmoke`。
- `git diff --check`：仅报告现有工作树的 LF/CRLF 转换提示，无空白错误。
- Release DLL：`artifacts\UnturnedSingleplayerCheatMenu.dll`，程序集版本 `1.7.0.0` / 产品版本 `1.7.0`。

## 未完成的外部验收

当前环境没有发现 `Unturned.exe`、BepInEx 5 x64 压缩包或 `UNTURNED_GAME_DIR`，因此以下证据尚不存在：

- DLL 已复制到游戏 `BepInEx\plugins\UnturnedSingleplayerCheatMenu\` 的目标哈希对比。
- BepInEx 启动日志中的 `Player.InitializePlayer` 和原生快速转移入口补丁注册行。
- 实际单机背包整理、跨页、装备、热键、归并、撤销和无掉落验收。
- 真实 UI 右下角快捷键窗口的截图与鼠标/键盘交互记录。

这些项目不能用编译或 Smoke 结果替代。


## 用户反馈修正（2026-08-21）

- 稀有度预设改为明确的“高稀有度优先（神话→普通）”，基于当前程序集 `EItemRarity` 的 `COMMON → UNCOMMON → RARE → EPIC → LEGENDARY → MYTHICAL` 枚举顺序使用降序比较。
- 每次整理都会按弹药兼容组重新分配 `ItemMagazineAsset` 的总弹量，同时保留弹匣/弹药箱数量；两个半容器会变成一个满容器和一个空容器。
- 整理页的预设按钮、所有开关、预览、整理、全部随身、仅压缩、撤销和仅归并按钮均加入鼠标悬停说明，并接入中英文本地化。


## Workshop 弹药兼容核对（2026-08-22）

- 整理器现在在运行时扫描已加载的 `ItemMagazineAsset.blueprints`，只把 `FillTargetItem`（旧 DAT 中的 `Blueprint_*_Type Ammo`）引用的 `ItemSupplyAsset` 识别为可补弹的弹药箱。
- `3565375423` 的 58908 弹匣静态定义引用了弹药箱 ID `60040`、`60041`、`60048`、`60049` 和 `119`；其中 `60048`、`60049` 在 `3560661136` 中分别是警用 60 发和 200 发弹药箱。58902 与 58908 共享口径 `58902`，因此两种弹匣会进入同一兼容组。
- 弹药箱使用自身 `ItemAsset.MaxAmount` 作为容量、`Item.amount` 作为当前弹量；两个半弹匣或两个半弹药箱在总量不超过总容量时会按原顺序重新分配为满容器和空容器。
- 只通过 `Tool` 蓝图把弹药箱转换成新弹匣的资产，不会被误当成“直接补充来源”；当前整理器不自动消耗物品、生成新物品或改变物品数量结构，因此这类转换仍需游戏原生蓝图操作。
- 本次已完成源码构建、规划器回归 Smoke 和全量 Smoke；真实游戏中加载两个 Workshop 后的库存截图、日志和空弹药箱保留行为仍需单机运行验收。

## Plugin-Only 打包证据

- `scripts\\Build-PortablePackage.ps1 -Version 1.7.0`：通过。
- 包内文件数：3；不含 BepInEx 核心、运行时日志、用户配置或收藏/传送数据。
- Plugin-Only archive SHA-256：`575A206B48B58EA13F77DECACDBF944BACCDE64E3A362C846340A040FDC59EDE`。
- Release DLL SHA-256：`361C536196DC150A37C9F3BB6CB51A83FC79A7539F9CD5D66903741C8D3C9FBB`。
- 包内 DLL 与 Release DLL 哈希一致。
