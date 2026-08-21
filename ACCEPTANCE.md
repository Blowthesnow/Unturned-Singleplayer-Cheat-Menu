# v1.7.0 验收矩阵

日期：2026-08-21

本文将源码/构建/Smoke/发布包证据与真实游戏运行证据分开记录。编译通过不等同于已经完成游戏内交互验收。

## 1. 版本与环境

- 插件版本：`1.7.0`
- DLL 文件版本 / 产品版本：`1.7.0.0` / `1.7.0`
- Unturned：`3.26.3.8`
- Unity：`2022.3.62f3`
- BepInEx：`5.4.23.5` x64
- 运行时：Windows x64 / Unity Mono
- Release DLL：`244,736` bytes
- Release DLL SHA-256：`FA500D0EC9E4E927797462CB72D713C1B6E409FE698F2F945C498F64BC31B3CF`

## 2. 本轮保留功能范围

- 准星交互工具：Smart、Inspect、Repair、Teleport、Utility、Delete 模式；中键触发、可配置范围、HUD 字段和 Shift 删除保护。
- 移动工具：基于游戏原生模拟路径的飞行与穿墙，水平/垂直速度倍率，以及关闭穿墙后的安全脱困搜索。
- 传送安全落点：地表/建筑表面校验、站立空间校验和附近候选落点回退。
- Overlay 输入与时间控制：原生光标/输入隔离、PlayerUI 更新后的状态维护、时间滑条实时预览。
- 既有资产扫描、物品筛选、收藏、传送点、车辆缩略图缓存、天气和世界控制功能继续保留。
- 安装安全：部署后只保留规范目录中的一份活动插件 DLL。

## 3. 源码与构建证据

| 检查项 | 结果 |
| --- | --- |
| `dotnet build .\\UnturnedSingleplayerCheatMenu.slnx -c Release --no-restore` | PASS；0 warnings，0 errors |
| 解决方案项目数 | PASS；1 插件项目 + 8 个 Smoke 项目 |
| 程序集版本元数据 | PASS；`1.7.0.0` / `1.7.0` |
| 废弃功能引用检查 | PASS；源码、测试和当前文档无残留实现引用 |

## 4. Smoke 证据

以下命令均以 Release、`--no-build` 执行并通过：

- `FavoritesSerializationSmoke`：PASS
- `ItemFilteringSmoke`：PASS
- `LocalizationSmoke`：PASS
- `MovementSpeedSmoke`：PASS
- `PointToolActionSmoke`：PASS
- `ShortcutToggleSmoke`：PASS
- `TeleportSerializationSmoke`：PASS
- `VehicleThumbnailSettingsSmoke`：PASS

## 5. 发布包证据

正式发布资产只有：

`Unturned-Singleplayer-Cheat-Menu-v1.7.0-Plugin-Only.zip`

- ZIP 大小：`100,272` bytes
- ZIP SHA-256：`432937F83D932EEFCF3724C9AF5BDE5450DA5A907EAC21CF5D22A194F68A4697`
- 包内文件数：3
- 包内 DLL 路径：`BepInEx\\plugins\\UnturnedSingleplayerCheatMenu\\UnturnedSingleplayerCheatMenu.dll`
- 包内 DLL SHA-256 与 Release 构建一致：PASS
- 包内没有 Doorstop、BepInEx 核心、配置、缓存、日志或保存数据：PASS
- `SHA256SUMS.txt`：PASS

## 6. 真实游戏运行边界

本轮已完成源码、构建、Smoke 和 Plugin-Only 包验证；未在本轮重新执行完整的无 BattlEye 单人游戏操作矩阵，因此以下项目不标记为本轮完成：

- 准星工具在真实地图中的 Smart 仲裁、维修、传送、实用和删除交互；
- 飞行/穿墙在真实单人世界中的手感、固定模拟节奏和安全退出；
- 菜单打开/关闭后的相机、原生光标和角色输入恢复；
- 车辆缩略图、地图传送和六页 Overlay 的完整截图回归。

真实运行时必须使用 Steam 的无 BattlEye 启动项，只进入单人世界，并从角色完成加载后按 `End` 打开菜单。不得用于多人服务器或 BattlEye 会话。

## 7. 发布结论

- 源码构建：PASS
- Smoke 验证：PASS
- Plugin-Only 包验证：PASS
- 游戏内完整验收：未在本轮重新执行，按上面的边界记录
- 正式 Release 资产范围：仅 Plugin-Only ZIP