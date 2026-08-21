# Unturned Singleplayer Cheat Menu

[English](README.en.md) · [更新日志](CHANGELOG.md) · [验收记录](ACCEPTANCE.md) · [安全说明](SECURITY.md)

一个专门面向 **Unturned 单人世界** 的 BepInEx 5 中英双语作弊菜单。它在游戏运行时读取当前已经加载的资产注册表，因此可以自动发现原版、地图附带和 Workshop 模组中的物品与车辆，并提供角色状态、收藏、传送、时间、天气、满月和空投等操作。v1.7.0 新增准星交互工具、原生模拟飞行/穿墙、传送安全落点增强、时间滑条实时预览和重复插件 DLL 部署保护。

> **仅限无 BattlEye 的单人模式。** 本项目不会关闭、修改或绕过 BattlEye，也不支持多人服务器。

**默认快捷键：进入单人世界后按 `End` 打开或关闭菜单。**

> **截图说明：当前项目展示的截图为 1.1 版界面；2.0 版将统一更新项目截图。**

![物品页：自动扫描原版、地图和 Workshop 物品](docs/images/items-tab.png)

<details>
<summary><strong>查看其他界面截图</strong></summary>

### 角色状态与技能

![角色页：生存状态、经验、声望和技能](docs/images/character-tab.png)

### 载具浏览与生成

![载具页：自动扫描载具并生成模型缩略图](docs/images/vehicles-tab.png)

### 工具

- 准星交互工具支持智能、检查、维修、传送、实用和删除模式；中键触发，删除需要同时按住 `Shift`。
- Smart 模式将语义目标识别与传送坐标分开处理；没有可用目标时仍可检查世界表面或使用安全坐标回退。
- 飞行与穿墙使用游戏原生移动模拟路径，支持水平/垂直速度倍率和关闭穿墙后的安全脱困搜索。

### 收藏

![收藏页：物品与车辆收藏及空状态](docs/images/favorites-tab.png)

### 传送点

![传送页：按地图保存和管理具名位置](docs/images/teleports-tab.png)

### 时间、天气和世界事件

![其他页：时间、天气、满月、空投和资产重扫](docs/images/world-tab.png)

</details>

## 主要功能

### 角色

- 无敌模式与无限生存状态。
- 一键恢复生命、处理伤势并补满生存数值。
- 分别设置生命、饱食、水分、免疫、体力和氧气。
- 自定义增加经验、修改声望。
- 一键将全部技能升至满级。

### 物品

- 从 Unturned 当前资产映射自动扫描所有已加载的 `ItemAsset`。
- 覆盖原版、Workshop 模组和地图随附内容。
- 支持主分类、精确物品类型、来源、稀有度、装备槽位、枪械机制和多选射击模式筛选。
- 支持名称、ID、GUID、来源搜索和分页；普通物品页与收藏页分别保存筛选状态。
- 使用游戏生成的物品图标和本地化名称。
- 每次给予数量可设置为 `1–255`。

### 车辆

- 自动扫描所有已加载的 `VehicleAsset`。
- 按陆地车辆、固定翼飞机、直升机、飞艇和船只分类。
- 支持名称、ID、GUID、来源搜索和分页。
- 优先使用官方车辆图标；缺失或透明时，根据实际模型自动生成缩略图。
- 可选择 `128 × 96`、`192 × 144` 或 `256 × 192`，并调整自动取景倍率；生成结果按车辆 GUID 与渲染配置写入磁盘缓存。
- 每次可生成 `1–20` 辆，并在玩家前方按地面阵列摆放，避免全部重叠。

### 收藏

- 物品和车辆卡片右上角可使用 `☆/★` 收藏或取消收藏。
- 收藏页分别保留物品与车辆的分类、搜索、数量和分页功能。
- 从收藏页可直接给予物品或生成车辆。
- 收藏以资产 GUID 为主键持久保存；暂未加载的模组资产不会被自动删除。

### 传送

- 保存多个具名位置。
- 传送点按地图隔离。
- 在地图与传送点列表之间切换；地图支持缩放、拖动、玩家位置和已保存标记。
- 点击地图位置传送到安全地表；存在房屋或其他建筑物时优先落在可用屋顶/顶部。
- 传送点支持星形、正方形、圆形和菱形标记，以及预设或自定义颜色。
- 点击“传送”立即移动到保存位置，点击 `×` 直接删除。
- 坐标、朝向、地图和创建时间写入独立 JSON 文件。

界面会保存最后选择的主页面和传送子视图，下次打开时恢复。

### 其他

- 时间滑条、切换白天/夜晚、冻结时间。
- 强制满月。
- 请求空投。
- 雨、雪、清除天气和关闭天气调度。
- 手动重新扫描当前已加载资产。

## 运行边界

插件打开菜单前会同时确认：

- 已经进入游戏世界；
- 本地玩家已经生成；
- 当前实例同时是客户端和服务端；
- 服务器 ID 为 `Singleplayer_` 单人世界。

如果仍在主菜单、地图加载阶段、多人服务器或专用服务器中，菜单会拒绝开启；离开单人世界后，已打开的菜单也会自动关闭。

地图创建界面的 **“单人作弊指令”复选框可以不勾选**。那个选项控制 Unturned 原生命令系统；本插件不依赖 `Provider.hasCheats`。

## 兼容环境

v1.7.0 已针对以下环境构建；既有单人运行证据与本轮新增功能的验证边界见 [ACCEPTANCE.md](ACCEPTANCE.md)：

| 项目 | 版本 |
| --- | --- |
| Unturned | `3.26.3.8` |
| Unity | `2022.3.62f3` |
| 架构/运行时 | Windows x64 / Unity Mono |
| BepInEx | `5.4.23.5` x64 |
| 插件 | `1.7.0` |

游戏更新可能改变内部 API。若新版 Unturned 启动后插件不加载，请先查看 `BepInEx/LogOutput.log`，再提交 Issue。

## 安装

GitHub Release 中的 `Unturned-Singleplayer-Cheat-Menu-v1.7.0-Plugin-Only.zip` **只包含插件，不包含 BepInEx**。

1. 完全退出 Unturned。
2. 安装 BepInEx `5.4.23.5` x64 到 Unturned 游戏根目录。
3. 下载 Release 中的插件压缩包。
4. 将压缩包内容解压到能看到 `Unturned.exe` 的游戏根目录。
5. 确认最终文件位于：

   ```text
   Unturned\BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll
   ```

   部署约束：`BepInEx\plugins` 下只能保留这一份活动的 `UnturnedSingleplayerCheatMenu.dll`，不能在 `plugins` 根目录或其他子目录再放置同名 DLL。旧版本或备份文件必须改为非 `.dll` 后缀，避免 BepInEx 重复加载，造成新版覆盖安装失效。

6. 从 Steam 的启动方式选择窗口中明确选择 **不使用 BattlEye Anti-Cheat**。
7. 只进入单人世界，角色加载完成后按 `End` 打开菜单。

不要直接带着本插件进入 BattlEye 或多人服务器。仓库中的启动脚本只会打开 Steam 启动方式选择窗口；它不会停止、禁用或修改 BattlEye。

## 配置与数据

首次成功运行后会在 `BepInEx/config` 下生成：

| 文件 | 用途 |
| --- | --- |
| `com.codex.unturned.singleplayer-cheat-menu.cfg` | 界面语言、快捷键、UI 缩放、每页卡片数、车辆缩略图、准星工具和移动设置 |
| `UnturnedSingleplayerCheatMenu.favorites.json` | 物品与车辆收藏 |
| `UnturnedSingleplayerCheatMenu.teleports.json` | 具名传送点 |
| `BepInEx/LogOutput.log` | BepInEx 与插件运行日志 |

默认快捷键是 `End`，可以在配置文件中的 `ToggleShortcut` 修改。修改前请退出游戏。

插件窗口右上角提供 `EN` / `中文` 语言切换按钮，点击后立即刷新界面并写回 `Interface.Language`，不需要重启游戏。

`Interface.Language` 初始默认为 `Auto`：简体/繁体中文 Unturned 使用中文，其余游戏语言使用英文。也可以在配置文件中显式填写 `English` 或 `Chinese`；未知值按 `Auto` 处理。

自动扫描只会显示**当前游戏进程已经成功加载并进入 `Assets` 映射的内容**。未订阅、下载失败、依赖缺失或当前地图没有加载的模组不会被插件凭空发现。进入世界后可以在“其他”页点击“重新扫描”刷新。

## 卸载

退出游戏后删除：

```text
BepInEx\plugins\UnturnedSingleplayerCheatMenu
```

即可停用插件。配置、收藏和传送点不会随 DLL 自动删除；如需彻底清理，可手动删除上文列出的三个配置文件。

## 从源码构建

仓库不会提交 Unturned、Unity 或 BepInEx 的二进制程序集。构建者必须拥有合法安装的游戏和 BepInEx。

```powershell
.\scripts\Prepare-References.ps1 -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\Unturned'
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
dotnet run --project .\tests\FavoritesSerializationSmoke\FavoritesSerializationSmoke.csproj -c Release
dotnet run --project .\tests\ItemFilteringSmoke\ItemFilteringSmoke.csproj -c Release
dotnet run --project .\tests\LocalizationSmoke\LocalizationSmoke.csproj -c Release
dotnet run --project .\tests\MovementSpeedSmoke\MovementSpeedSmoke.csproj -c Release
dotnet run --project .\tests\PointToolActionSmoke\PointToolActionSmoke.csproj -c Release
dotnet run --project .\tests\ShortcutToggleSmoke\ShortcutToggleSmoke.csproj -c Release
dotnet run --project .\tests\TeleportSerializationSmoke\TeleportSerializationSmoke.csproj -c Release
dotnet run --project .\tests\VehicleThumbnailSettingsSmoke\VehicleThumbnailSettingsSmoke.csproj -c Release
```

`Prepare-References.ps1` 只会把编译所需程序集复制到被 Git 忽略的本地 `lib/` 目录。Release DLL 输出到 `artifacts/UnturnedSingleplayerCheatMenu.dll`。

主要模块：

- `CheatMenuPlugin` / `ShortcutToggleGate`：生命周期、快捷键去重、单人守卫、菜单状态和资产刷新。
- `CheatMenuOverlayUi`：运行时 Overlay UI 与输入隔离。
- `AssetCatalog` / `ItemFilterService`：资产扫描、物品分类与高级筛选。
- `IconCache` / `VehicleIconDiskCache` / `VehicleIconRenderer`：内存与磁盘图标缓存、车辆模型缩略图和渲染设置。
- `CheatActions`：角色、物品、车辆、时间、天气和事件操作。
- `FavoriteStore` / `TeleportStore`：原子化 JSON 持久化与失败回滚。
- `GodModePatch`：当前版本 `PlayerLife.doDamage` 的 Harmony 单人保护补丁。

## 验证

v1.7.0 的 Release 构建、九个解决方案项目、Plugin-Only 发布包校验及哈希记录见 [ACCEPTANCE.md](ACCEPTANCE.md)。本版本加入准星交互工具、原生模拟飞行/穿墙、传送安全落点增强、时间滑条实时预览和重复 DLL 部署保护。

已发布插件 DLL：

```text
SHA-256: FA500D0EC9E4E927797462CB72D713C1B6E409FE698F2F945C498F64BC31B3CF
```

## 免责声明

本项目是社区制作的非官方单人游戏模组，与 Smartly Dressed Games、Unturned、BattlEye 或 BepInEx 项目没有隶属或背书关系。使用前请自行备份存档并理解第三方插件可能在游戏更新后失效。

插件源码采用 [MIT License](LICENSE)。Unturned、Unity、BepInEx 及发行包中相应第三方文件仍遵循各自的许可证和权利声明。
