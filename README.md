# Unturned Singleplayer Cheat Menu

[English](README.en.md) · [更新日志](CHANGELOG.md) · [安全说明](SECURITY.md) · [赞助](https://afdian.com/a/Blowthesnow)

一个以 **Unturned 单人世界** 为玩法支持目标的 BepInEx 5 中英双语菜单。它在游戏运行时读取当前已经加载的资产注册表，因此可以自动发现原版、地图附带和 Workshop 模组中的物品与车辆，并提供角色状态、收藏、传送、时间、天气、满月和空投等操作。当前源码版本为 `2.0.0`。

> 本插件的玩法功能仅以 Unturned 单人世界为设计、测试和兼容性目标。插件可以保持安装并允许你选择 BattlEye 启动；若 BepInEx 成功加载，菜单也可以在已生成本地玩家的多人客户端会话中打开。多人/服务器会话中的玩法功能属于不受支持的自担风险使用。Workshop 翻译是本地内容功能，不受单人/多人玩法门或多人风险确认状态影响，也不会因为进入多人而自动停止。项目不会停止、禁用、修改或绕过 BattlEye，也不保证 BattlEye 会允许 BepInEx、本插件或当前连接继续运行。

**默认快捷键：本地玩家生成后按 `End` 打开或关闭菜单。**

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
- 使用游戏生成的物品缩略图和本地化名称；卡片按纹理真实宽高自适应显示，不拉伸图像。
- 每次给予数量可设置为 `1–255`。

### 车辆

- 自动扫描所有已加载的 `VehicleAsset`。
- 按陆地车辆、固定翼飞机、直升机、飞艇和船只分类。
- 支持名称、ID、GUID、来源搜索和分页。
- 载具卡片同样按缩略图真实宽高自适应显示；所有车辆都根据实际模型使用统一的自动取景相机生成缩略图，不再采用资产自带的 `Icon2/Icon` 预设取景。
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

### 小地图

- 支持圆形/方形 HUD、锚点、偏移、尺寸和可视半径设置，并提供直接拖动和缩放的布局编辑模式。
- 可显示玩家、同组原生标记、传送点、僵尸、动物、其他玩家和地面物品；物品可按六档稀有度筛选，并以楼层箭头区分高低差。
- 背景可在原生地图裁剪、实时地表、实时高度切片和回退网格之间切换；洞穴/建筑遮挡探测会按设置自动选择合适背景。
- 自动拾取可以显式选择跟随小地图范围与稀有度；两者默认关闭，避免改变既有拾取规则。

### 其他

- 时间滑条、切换白天/夜晚、冻结时间。
- 强制满月。
- 请求空投。
- 雨、雪、清除天气和关闭天气调度。
- 手动重新扫描当前已加载资产。

## 运行边界

菜单就绪和玩法授权是两个独立边界：只要客户端已经连接且本地玩家已生成，单人和多人客户端都可以打开主菜单。单人玩法立即可用；多人或非单人 listen host 会常驻显示“不受支持”警示，第一次尝试玩法动作或启用玩法自动化时才要求确认本次连接的风险。断线、换服、换图连接或本地玩家实例重建会使确认失效。

| 会话 | 主菜单 | Workshop 翻译 | 玩法功能 | Snapshot 无限储存 |
| --- | --- | --- | --- | --- |
| 主菜单/加载中 | 关闭 | 已开始的任务继续 Pump | 暂停 | 关闭 |
| 单人世界 | 打开 | 可用 | 受支持 | 可用 |
| 多人客户端，未确认 | 打开并警示 | 可用且不中断 | 阻断 | 关闭 |
| 多人客户端，已确认 | 打开并持续警示 | 可用且不中断 | 不受支持的 best-effort | 关闭 |
| U3DS Dedicated Server | 不创建 UI | 不初始化客户端翻译 | inert/no-op | 关闭 |

Workshop 翻译扫描、缓存、请求、批次完成和本地文本应用不经过玩法授权。进入多人、菜单关闭、连接切换或风险确认变化不会取消当前批次；运行期间的资产刷新只合并为批次完成后的一次延迟重扫。

地图创建界面的 **“单人作弊指令”复选框可以不勾选**。那个选项控制 Unturned 原生命令系统；本插件不依赖 `Provider.hasCheats`。

## 兼容环境

当前 `2.0.0` 源码已针对以下环境构建；版本归属、构建记录与验证边界见 [更新日志](CHANGELOG.md)：

| 项目 | 版本 |
| --- | --- |
| Unturned | `3.26.3.8` |
| Unity | `2022.3.62f3` |
| 架构/运行时 | Windows x64 / Unity Mono |
| BepInEx | `5.4.23.5` x64 |
| 插件 | `2.0.0` |

游戏更新可能改变内部 API。若新版 Unturned 启动后插件不加载，请先查看 `BepInEx/LogOutput.log`，再提交 Issue。

## 安装

GitHub Release 中的 `Unturned-Singleplayer-Cheat-Menu-v2.0.0-Plugin-Only.zip` **只包含插件，不包含 BepInEx**。

1. 完全退出 Unturned。
2. 安装 BepInEx `5.4.23.5` x64 到 Unturned 游戏根目录。
3. 下载 Release 中的插件压缩包。
4. 将压缩包内容解压到能看到 `Unturned.exe` 的游戏根目录。
5. 确认最终文件位于：

   ```text
   Unturned\BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll
   ```

   部署约束：`BepInEx\plugins` 下只能保留这一份活动的 `UnturnedSingleplayerCheatMenu.dll`，不能在 `plugins` 根目录或其他子目录再放置同名 DLL。旧版本或备份文件必须改为非 `.dll` 后缀，避免 BepInEx 重复加载，造成新版覆盖安装失效。

6. 继续使用原有的 Steam/Unturned 启动流程；本次运行边界调整不新增或改造启动器。
7. 本地玩家加载完成后按 `End`。单人是唯一受支持的玩法环境；多人菜单只提供不受支持、自担风险的会话级确认，Snapshot 始终不可用。

启动流程保持不变。即使现有启动方式能够启动游戏，也不等于 BattlEye 会允许 BepInEx、本插件或服务器连接继续运行。

## 配置与数据

首次成功运行后会在 `BepInEx/config` 下生成：

| 文件 | 用途 |
| --- | --- |
| `com.codex.unturned.singleplayer-cheat-menu.cfg` | 界面语言、快捷键、UI 缩放、每页卡片数、车辆缩略图、准星工具和移动设置 |
| `UnturnedSingleplayerCheatMenu.favorites.json` | 物品与车辆收藏 |
| `UnturnedSingleplayerCheatMenu.teleports.json` | 具名传送点 |
| `AutoPickup.rules.json` | 自动拾取白名单/黑名单规则 |
| `UnturnedSingleplayerCheatMenu/InfiniteStorageSnapshots/v1/<世界哈希前24位>/<玩家哈希前24位>/snapshot.json` | Snapshot schema 1；96 位目录前缀控制 Unity Mono 路径长度，文档内完整身份仍按世界、Steam ID 与角色槽精确校验 |
| `BepInEx/LogOutput.log` | BepInEx 与插件运行日志 |

默认快捷键是 `End`，可以在配置文件中的 `ToggleShortcut` 修改。修改前请退出游戏。

插件窗口右上角提供 `EN` / `中文` 语言切换按钮，点击后立即刷新界面并写回 `Interface.Language`，不需要重启游戏。

从未发布的旧 `UnturnedSingleplayerCheatMenu/InfiniteStorage/` 格式已废弃；当前运行时不会读取、Claim、迁移、移动或删除该目录中的文件。新路径不存在时，无限储存从空仓库开始。

若同目录出现 `snapshot.json.reconciliation-required`，无限储存与自动拾取会保持锁定，避免把无法证明一致的原生/仓库状态继续扩散。先按活文档核对 primary、`.bak`、临时残件与世界掉落，手工清理标记后重启；不要直接覆盖存档。

`Interface.Language` 初始默认为 `Auto`：简体/繁体中文 Unturned 使用中文，其余游戏语言使用英文。也可以在配置文件中显式填写 `English` 或 `Chinese`；未知值按 `Auto` 处理。

Workshop 本地化页支持最多两个 API Key。首行保留 `+`，后续行可用 `×` 删除；每个 Key 使用独立 worker 并行消费批次，批量大小范围为 `1–100`。该页面使用垂直滚动布局，增加输入行不会压缩下方设置。

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
Get-ChildItem .\tests -Recurse -Filter '*Smoke.csproj' | Sort-Object FullName | ForEach-Object {
    dotnet run --project $_.FullName -c Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke failed: $($_.FullName)" }
}
```

`Prepare-References.ps1` 只会把编译所需程序集复制到被 Git 忽略的本地 `lib/` 目录。Release DLL 输出到 `artifacts/UnturnedSingleplayerCheatMenu.dll`。

主要模块：

- `RuntimeSessionPolicy` / `GameplayFeatureAccessPolicy`：会话分类、菜单就绪、每连接多人风险确认和玩法执行授权。
- `CheatMenuPlugin` / `ShortcutToggleGate`：生命周期、快捷键去重、菜单状态和资产刷新。
- `CheatMenuOverlayUi`：运行时 Overlay UI 与输入隔离。
- `AssetCatalog` / `ItemFilterService`：资产扫描、物品分类与高级筛选。
- `IconCache` / `VehicleIconDiskCache` / `VehicleIconRenderer`：内存与磁盘图标缓存、车辆模型缩略图和渲染设置。
- `CheatActions`：角色、物品、车辆、时间、天气和事件操作。
- `FavoriteStore` / `TeleportStore`：原子化 JSON 持久化与失败回滚。
- `GodModePatch`：当前版本 `PlayerLife.doDamage` 的 Harmony 本地玩家授权补丁。

## 验证

`v2.0.0` 是自 `v1.7.0` 以来首个正式版本；中间源码检查点与本版累计变更、构建和 Smoke 记录见 [主更新日志](CHANGELOG.md)，v1.8 的逆向恢复边界单独记录在 [v1.8 恢复日志](docs/V1.8_RECOVERY_CHANGELOG.md)。2.0 Release 构建为 0 警告、0 错误，递归发现的 27 个 Smoke 项目全部逐项重建并运行通过；源码/API、发布包与真实游戏验收边界仍分别记录。

v2.0.0 发布产物：

```text
DLL SHA-256: 93FED6974CBF793B8619AE66D2846DFE60F1532AF5E030C2C7DA62EED059FECD
Plugin-Only ZIP SHA-256: 338D3570FD6582C5F4CC638C2CA14B72BF4E9DC3108BFD88953417621CA70C85
```

最终 2.0 DLL/发布包在本次发布准备中未部署、未重启游戏验收；自动化通过不能替代真实单人玩法确认。

## 免责声明

本项目是社区制作的非官方单人玩法模组，与 Smartly Dressed Games、Unturned、BattlEye 或 BepInEx 项目没有隶属或背书关系。多人/服务器玩法和 BattlEye 加载均不受支持、不保证可用；使用前请备份存档并理解踢出、回档、断线或账号处罚风险。

插件源码采用 [MIT License](LICENSE)。Unturned、Unity、BepInEx 及发行包中相应第三方文件仍遵循各自的许可证和权利声明。
## 当前架构验证入口

当前源码使用单一 `PluginUiRoot`，由 `RuntimeUpdatePatch` 挂接 `Provider/MenuUI/PlayerUI.Update` 并按帧去重后分发 `PluginRuntimeContext` 的 Update/LateUpdate；这条静态桥与旧版相同，不依赖会在 Unturned 启动场景切换时被销毁的临时 BepInEx/Driver 组件。`PluginUiRoot` 若随启动场景丢失会自行重建。它还包含输入释放、窗口拖动/位置持久化和按当前地图隔离的传送点视图。生产源码不再包含旧 IMGUI 菜单或 `UnityEngine.IMGUIModule` 生产引用。

Release 构建后应递归发现并串行运行 `tests/**/*Smoke.csproj`，不要依赖手写项目数量或旧 `bin/obj` 目录。源码/API、构建、Smoke、部署哈希、BattlEye 启动、BepInEx/plugin 发现、多人连接、翻译连续性和真实游戏交互必须分别记录；其中任何一层都不能替代真实单人玩法验收。
