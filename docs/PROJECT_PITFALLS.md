# Unturned Singleplayer Cheat Menu 项目避坑文档

> 状态：持续维护
> 整理日期：2026-08-15
> 适用范围：`D:\Unturned BepInEx`、Unturned 3.26.3.8、Unity Mono、BepInEx 5.4.23.5
> 目标：把项目历史对话中真实遇到过的问题、根因、修复方式和验证边界沉淀为可复用的操作手册。

## 0. 先看结论

这个项目最容易犯的不是“代码写错”，而是把不同层级的证据混在一起：

| 证据层 | 能证明什么 | 不能证明什么 |
|---|---|---|
| 源码/程序集 | API 调用、边界判断、文件路径和设计是否存在 | 游戏内一定能显示、一定能点击 |
| Release 构建 | 编译成功、引用完整、产物可生成 | DLL 已部署到正确游戏、游戏一定加载 |
| 冒烟/静态检查 | 序列化、静态模板、架构边界、安装布局 | 真实鼠标、焦点、滚动、资产和玩法 |
| BepInEx 日志 | 插件是否加载、回调是否运行、资产扫描和错误阶段 | 画面是否正确、点击是否落在目标控件 |
| 游戏截图/用户实际操作 | 视觉、鼠标层级、按钮交互和实际效果 | 其他未操作页面或另一版本 DLL |
| 发布包/远程 Release | 包结构、哈希、公开内容和下载链路 | 用户机器上的真实运行 |

**任何“完成”结论都必须写清是哪一层。**
例如“Release 构建通过”不能写成“游戏内菜单已完成”；“日志出现缩略图已绑定”不能替代车辆截图验收。

---

## 1. 本次整理覆盖的历史对话

本文件根据当前项目相关的 Codex 会话、现有 README/ACCEPTANCE、living plan、源码和脚本整理：

- `01a0014d-c2e3-78d2-8aea-0c5556727816`：从零创建插件、输入冲突、IMGUI/uGUI、实机验收。
- `01a0038f-3492-78b3-be49-5a9ac4d51051`：车辆缩略图、原生光标、地图标题、收藏页、打包。
- `01a00468-650d-7692-b447-0ad4749d7914`：GitHub 公开发布准备和认证阻塞。
- `01a00600-fdce-70a0-b06b-a785e7ecf609`：UI 重构、持久化页面、收藏页/UI 边界和运行时 Gate。
- `01a006a2-fde5-7bc0-aa39-cf5266b0d0f2`：明确“以公开仓库为基线，UI 轮次不得改 UI 外代码”。
- 当前会话：汇总历史坑点并核对当前工作区。

历史对话中出现过的版本、哈希、备份目录和截图路径属于**当时证据**，不能直接覆盖当前工作区或当前游戏目录的状态。

---

## 2. 环境、版本和引用方面的坑

### 2.1 不要凭“最新版”猜 BepInEx 或游戏环境

**现象**

- 初始工作区没有 BepInEx。
- 在线查询“最新 BepInEx”遇到 GitHub 匿名速率限制和 PowerShell 重定向异常。
- 直接按经验使用某个版本会让编译引用、运行时和最终包不一致。

**修复**

1. 以用户提供的压缩包为准。
2. 先校验压缩包 SHA-256。
3. 从压缩包内读取实际程序集版本。
4. 把游戏版本、Unity 版本、运行时、架构和 BepInEx 版本写入验收台账。

当前锁定基线：

```text
Unturned   3.26.3.8
Unity      2022.3.62f3
Runtime    Windows x64 / Unity Mono
BepInEx    5.4.23.5 x64
```

**原则**

- “在线最新”只是下载信息，不是项目兼容性证据。
- 版本变化后必须重新准备引用、构建、启动和实机验收。
- 不要把旧版本 DLL、旧日志和新源码混在一起判断。

### 2.2 BepInEx 压缩包常有顶层目录，路径会多一层

**现象**

解压后读取版本失败，原因不是文件损坏，而是压缩包结构类似：

```text
<archive>\BepInEx\core\BepInEx.dll
```

代码却按：

```text
<archive>\core\BepInEx.dll
```

读取。

**修复**

- 解压后先列出实际树结构，再构造引用路径。
- 安装脚本只接受明确的 `BepInEx\core\BepInEx.dll`。
- 不要为了“修路径”移动或重解压用户提供的原包。

### 2.3 引用必须来自实际游戏根目录，不要把二进制提交到仓库

项目通过 `scripts/Prepare-References.ps1` 从以下位置准备本地引用：

```text
<GAME_ROOT>\Unturned_Data\Managed
<GAME_ROOT>\BepInEx\core
```

引用复制到被 Git 忽略的 `lib/`，而不是提交游戏 DLL、BepInEx DLL 或反编译产物。

**避坑**

- 源码可以公开，游戏和 BepInEx 二进制不要随源码提交。
- `vendor/`、`lib/`、反编译输出和运行证据要检查 `.gitignore`。
- 公开发布前扫描本机盘符、用户目录、日志、存档、收藏和传送数据。

### 2.4 不要假设内部字段和 BepInEx API 存在

历史编译问题包括：

- 需要额外引用 Unity 文本渲染程序集。
- `hasBeenReplaced` 是游戏内部字段，插件不能直接访问。
- BepInEx 5 没有预期中的 `Paths.BepInExVersion`。

**修复方式**

- 先从当前 `Assembly-CSharp.dll`、XML 或官方稳定文档核对签名。
- 对内部字段使用公开 API、反射封装或程序集版本读取替代。
- 不要因为旧版本示例能编译，就把它当成当前版本契约。
- 每次 API 修复后重新 Release 构建，并检查产物引用。

---

## 3. 启动、BattlEye 和单人边界

### 3.1 直接启动 `Unturned.exe` 可能被 Steam 重定向到 BattlEye

**现象**

- 系统应用入口或直接启动游戏后出现 BattlEye Launcher。
- 进程同时出现 `Unturned.exe`、`Unturned_BE.exe` 或 BattlEye 服务。
- 这不等于“无 BattlEye 测试”。

**修复**

- 使用项目启动脚本打开 Steam 启动方式选择窗口。
- 明确选择 **Play without BattlEye Anti-Cheat**。
- 启动后检查：

```text
只有 Unturned.exe
没有 Unturned_BE.exe
BEService = Stopped
```

- 启动脚本只做检查和打开选择窗口，不停止、禁用或修改 BattlEye。

**不要做**

- 不要使用会直接选默认入口的 `steam://rungameid/304930` 作为无 BattlEye 证明。
- 不要为了测试停止或修改 BattlEye 服务。
- 不要进入多人服务器。

### 3.2 “单人作弊指令”复选框不是本插件的开关

**结论**

地图创建界面的“单人作弊指令”控制的是 Unturned 原生命令系统，关联 `Provider.hasCheats`。本插件的单人守卫检查的是：

- 已连接并已进入世界；
- 同时是客户端和服务端；
- `serverID` 以 `Singleplayer_` 开头；
- 本地玩家已经生成。

因此本插件不依赖该复选框；不勾选时仍可使用插件菜单，但原生作弊命令本身不可用。

**避坑**

- 不要把“原生作弊开关未勾选”误判为插件加载失败。
- 不要为了让插件工作而放宽 `SingleplayerGuard`。
- 不要只看菜单能否打开，还要确认多人/专用服务器会拒绝打开。

### 3.3 插件启动时加载，不能在运行中热启用

- DLL 在游戏启动阶段由 BepInEx 加载。
- 菜单默认关闭不代表插件未加载。
- 进入地图后再按 `End` 是正常使用方式。
- 如果启动前移走 DLL，不能在游戏中临时热加载，必须恢复 DLL 并重启。

进入地图后再打开菜单更可靠，因为 Workshop 和地图附带资产可能尚未进入 `Assets` 映射。

---

## 4. 快捷键和 Unity 生命周期

### 4.1 `Home` 与 Unturned 原生 HUD 冲突

**现象**

按 `Home` 后原生 HUD 消失，插件菜单却没有打开。日志没有插件快捷键触发记录。

**根因**

`Home` 是 Unturned 原生 HUD 键；插件和游戏同时竞争同一个输入。

**修复**

1. 读取实际 `Controls.dat`，不要凭经验猜空闲键。
2. 避开 `Home`、`Insert`、`PageUp`、`PageDown` 和车辆座位常用键。
3. 选择当前未绑定且不影响游戏操作的 `End`。
4. 保留 BepInEx 配置项 `ToggleShortcut`，默认值只是默认值，不要硬编码锁死。
5. 修改源码默认值后，同时更新已有游戏配置；旧配置会覆盖新的源码默认值。

### 4.2 `KeyboardShortcut.IsDown()` 会被“其他按键仍按住”误伤

**现象**

配置键明明正确，但插件不响应。

**根因**

BepInEx 5 的 `KeyboardShortcut.IsDown()` 会要求其他支持的按键处于释放状态。Unturned 运行中可能保留内部输入状态，导致主键被游戏识别、却被插件拒绝。

**修复**

- 先检查 `MainKey`。
- 只检查配置中要求的修饰键。
- 不要拒绝无关的常驻输入。
- 对 `Update`、IMGUI 事件和原生按键兜底统一到一个带帧去重的 `HandleToggleShortcut()`。

### 4.3 只靠 `Input.GetKeyDown` 或 `OnGUI` 不够

历史上分别遇到过：

- 自动化的快速 `Home` 按下/抬起落在两帧之间，当前按住位被错过。
- `Home` 进入 GUI/游戏事件队列，却没有进入 Unity 轮询状态。
- BepInEx 原始宿主在 Unturned 启动场景切换后被销毁，组件上的 `Update` 和 `OnGUI` 不再可靠。
- 对 `PlayerUI.Update/OnGUI` 打补丁成功，但当前世界实际不调用预期方法。

**修复策略**

按可靠性逐层加证据，而不是盲目换键：

1. Unity `Update` 轮询。
2. Unity `OnGUI` `KeyDown` 事件。
3. Windows 原生按键状态的当前位和“自上次轮询以来按下”位。
4. 对当前版本实际持续运行的方法做 Harmony Postfix，例如 `Provider.Update` 等可靠入口。
5. 用 `DontDestroyOnLoad` 的运行宿主承载需要持续泵送的队列。
6. `Update`、`OnGUI`、Harmony 回调共用帧号去重，避免一次按键开关两次。

**重要**

- 不能因为“回调补丁注册成功”就认为回调真的执行。
- 日志必须记录回调首次运行和快捷键触发来源。
- `OnDestroy` 可能只是场景切换，不应在此时盲目卸载 Harmony 和运行宿主。

### 4.4 DLL 被映射时不能覆盖

**现象**

部署返回 `user-mapped section open`。

**根因**

正在运行的 Unturned 已经映射旧 DLL，Windows 不允许覆盖。

**修复**

1. 先正常退出游戏。
2. 退出后再部署。
3. 部署前逐文件备份。
4. 比较源 DLL 和目标 DLL 的版本、大小、SHA-256。

不要热替换正在运行的插件，也不要用强制杀进程掩盖部署状态。

---

## 5. UI 层级、鼠标和滚动

### 5.1 IMGUI 可能“逻辑打开但完全不可见”

历史尝试依次遇到：

- `Modal + ForceBlur`：菜单逻辑打开，但背景模糊，IMGUI 内容不可见。
- `Modal + NoBlur`：不再模糊，但透明 Modal 覆盖层仍压在 IMGUI 上方。
- 调整 IMGUI 深度仍不能解决最终 UI 层遮挡。

**最终方向**

- 改为 Screen Space Overlay uGUI Canvas。
- 插件 Overlay 使用 `sortingOrder = 29900`。
- 依赖 Unturned 自己的 UI/光标层级，而不是再叠加一个透明 Modal。
- 保持业务服务、单人守卫、资产目录和持久化格式不变。

### 5.2 鼠标显示不等于鼠标能点击窗口

**现象**

- 鼠标指针在窗口下层。
- 鼠标仍控制游戏视角。
- 看起来“有光标”，但点击位置与控件不一致。

**根因**

Unturned 的原生 Glazier 光标有独立 Canvas，排序高于普通 UI；同时 `SleekWindow.showCursor` 参与游戏输入门控，不只是显示图标。

**可靠方案**

- 插件 Overlay：`sortingOrder = 29900`。
- Unturned 原生光标：约 `30000`，因此显示在插件窗口上方。
- 打开菜单时保存并设置：

```text
PlayerUI.window.isEnabled = true
PlayerUI.window.showCursor = true
Cursor.lockState = None
Cursor.visible = false
```

- 关闭菜单时恢复原值。
- 每个可靠游戏更新回调中维护输入捕获，因为 Unturned 可能每帧重算 `showCursor`。

如果当前版本无法稳定使用原生光标，再考虑自绘光标，但自绘对象必须：

- `raycastTarget = false`；
- 每帧置于 Overlay 最后；
- 使用正确的屏幕坐标到 Canvas 坐标转换；
- 不能遮挡按钮点击。

### 5.3 动态 `ScrollRect + ContentSizeFitter` 会导致页面空白

**现象**

首版 Overlay 的动态 `ScrollRect + ContentSizeFitter` 组合导致角色正文和资产卡片区域为空。

**修复**

- 先使用固定页大小和明确布局验证功能。
- 物品/车辆每页固定卡片数。
- 传送点固定每页条数。
- 角色、传送、其他页使用直接分页/固定布局。
- 只有在截图和运行时证据稳定后，才引入虚拟化或复杂布局。

**不要做**

- 不要同时修改 CanvasScaler、Panel `localScale`、Grid、ScrollRect、Mask 和字体，再根据一张截图猜根因。
- 不要把“能编译”当成“布局不会空白”。

### 5.4 整页 Destroy/Rebuild 会丢焦点、滚动位置和控件状态

**现象**

切 Tab、刷新资产或改数量后，搜索框、输入值、滚动位置和焦点丢失；频繁开关还会产生大量对象分配。

**修复**

已采用的渐进式边界：

1. `ContentHost` 只创建一次。
2. 每个 Tab 有稳定的 page Root，切换使用 `SetActive()`。
3. Character/World/Teleport 使用控件级或区域级局部刷新。
4. Items/Vehicles/Favorites 保留工具栏、搜索和数量控件，只刷新结果 Body。
5. VirtualGrid/CardPool 作为后续阶段，不在同一轮混入。

**当前边界**

资产结果 Body 仍可能重建 Card 子节点；因此 Grid 滚动、焦点保持和 Card 级局部刷新仍需独立运行时验收，不能因为 page Root 持久化就宣称全部完成。

### 5.5 UI 重构必须遵守 UI-only 边界

当用户明确以公开仓库为基线、要求“UI 改动不改 UI 之外的代码”时：

- 只能改 `UI/` 和对应 UI 测试/文档。
- 不要顺手修改 `FavoriteStore`、`AssetCatalog`、`CheatActions`、网络/服务边界或持久化格式。
- 截图中的 `0/1` 如果是已保存 GUID 未匹配当前加载资产，属于数据/资产层问题，不应在 UI 轮次偷偷修存储层。

---

## 6. 资产扫描、物品图标和车辆缩略图

### 6.1 不要扫描 Workshop 文件夹来猜资产

**正确做法**

使用游戏当前已经建立的资产注册表：

```csharp
Assets.find(List<ItemAsset>)
Assets.find(List<VehicleAsset>)
```

这样才能覆盖当前已经加载的原版、地图附带和 Workshop 资产。

**边界**

- 未订阅、下载失败、依赖缺失或当前地图未加载的模组不会被“凭空发现”。
- 进入世界后再扫描更可靠。
- 提供“重新扫描”操作，而不是每次读取磁盘猜测。

### 6.2 物品图标是异步回调，不是同步返回值

物品图标请求会先返回空值，稍后通过游戏回调得到纹理。

必须处理：

- pending GUID 去重；
- 回调绑定到正确资产；
- 资产重扫时清理旧缓存和 pending；
- 清理生成的 `Texture2D`，避免每次重扫泄漏；
- 日志区分“请求中”和“已绑定”。

不要把第一次返回 `null` 当成“没有图标”。

### 6.3 车辆官方图标可能只是白色/透明占位

**现象**

- 部分车辆有图标，部分车辆是白色块。
- 代码看起来请求成功，但截图仍然没有可用缩略图。

**根因组合**

1. `VehicleTool.getIcon` 只是把请求放入内部队列。
2. 真正处理依赖其私有 `Update()`，而插件自己的运行宿主可能在场景切换后失效。
3. 非空 `Texture2D` 不代表纹理有可见像素。
4. 某些车辆的预设取景/模型结构不能直接使用。

**修复**

- 不依赖静态 `VehicleIconRenderer.Instance`。
- 将 `VehicleIconRenderer` 改为由插件字段持有的普通 C# 服务。
- 使用插件自己的请求队列，并由可靠 Harmony 更新入口 `PumpOne()`。
- 预设图标不可用时：
  - 实例化模型；
  - 强制使用最高可见 LOD；
  - 根据 Renderer 包围盒自动计算相机位置和正交尺寸；
  - 进行离屏捕获；
  - 绑定前检查 alpha 可见像素，而不是只检查纹理非空。
- 记录五个阶段：

```text
请求提交
纹理生成/回退
写入缓存
绑定到卡片
用户截图确认
```

**视觉回归**

可以用车辆截图的近白像素比例捕获“全白占位块”：

```text
NearWhiteRatio <= 0.20  -> 通过候选
```

但脚本只能筛出明显白块，最终仍需要真实游戏截图和用户确认。

---

## 7. 收藏、传送和持久化

### 7.1 收藏页 `0/1` 不一定是 UI 过滤错误

如果收藏记录存在，但当前地图尚未加载对应 Workshop 资产，页面可能显示：

```text
当前加载 0 / 收藏记录 1
```

这表示 GUID 在磁盘上存在、当前资产映射里暂时没有匹配项，不应在 UI 轮次直接改 `FavoriteStore`。

**正确处理**

- 记录“当前加载数”和“收藏记录总数”两个数。
- 暂时未加载的 Workshop 收藏不要自动删除。
- 进入正确地图或资产完成加载后重新扫描。
- 如果需要修复匹配键，单独开数据/资产层任务。

### 7.2 收藏按钮嵌套在卡片按钮内有事件冒泡风险

星标按钮如果嵌套在“给予物品/生成车辆”的父卡片 Button 中，必须实机确认：

- 点击 `☆/★` 不会同时触发父卡片操作；
- 取消收藏后卡片和收藏页状态立即正确；
- 收藏页切换和分类筛选不会误触发生成。

若出现冒泡，优先把卡片改成普通容器，并设置独立主操作 Button；不要只凭 Unity 事件系统的理论行为判断已正确。

### 7.3 JSON 文件不存在可能是正常状态

首次运行、尚未收藏任何内容时，`favorites.json` 不存在是正常的，不应把“没有文件”当成读取失败。

持久化必须覆盖：

- 单条记录；
- 多条记录；
- 空集合明确写成 `[]`；
- GUID 和回退键；
- 保存失败时内存状态回滚；
- 退出/重启后恢复。

传送点还要额外验证：

- 多个具名点；
- 地图隔离；
- 保存、传送、删除；
- 重启恢复；
- 坐标、朝向、地图和创建时间字段完整。

---

## 8. 编码、PowerShell 和脚本

### 8.1 Windows 非 ASCII 用户目录会破坏 Python 路径

**现象**

Python 收到 `C:\Users\??`，出现 `WinError 123` 或找不到文件。

**修复**

- 不要把含中文用户名的字面路径通过 PowerShell 管道传给 Python。
- 在 Python 内使用：

```python
os.environ["USERPROFILE"]
```

再拼接 `.codex` 或项目路径。

- 脚本读写中文内容时显式使用 UTF-8。
- 需要时设置：

```powershell
$env:PYTHONUTF8 = "1"
```

### 8.2 `Copy-Item -LiteralPath` 不展开通配符

下面的写法不会按预期复制通配内容：

```powershell
Copy-Item -LiteralPath "$source\*" -Destination $target
```

应当：

- 枚举源目录子项后逐项复制；
- 或使用 `-Path` 处理通配符；
- 或显式复制文件；
- 复制后立即列出目标目录核对。

### 8.3 PowerShell 内嵌脚本和管道要拆开验证

历史上出现过：

- 哈希汇总命令因管道末尾语法错误在解析阶段失败；
- `Start-Process` 因带空格脚本路径参数引用丢失，隐藏监控器没有启动；
- 批量扫描巨大 JSONL 会超时。

**修复**

- 复杂命令拆成多个短命令；
- 参数使用明确的数组或 `-ArgumentList`，不要依赖嵌套引号猜测；
- 每个后台进程都检查 PID、状态文件和实际输出；
- 读大日志时按文件、日期和关键字符串定向读取，不做无界全量递归。

---

## 9. 打包和公开发布

### 9.1 本机路径不能进入可分发启动脚本

最初脚本含本机游戏路径，不能直接给别人使用。

**修复**

- 发行包脚本根据自身位置推导游戏根目录；
- 只检查根目录中的 `Unturned.exe`；
- 插件固定落到：

```text
BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll
```

- 公开 Release 只上传 Plugin-Only 包；包内不得出现 BepInEx 核心、Doorstop 或私有运行时数据。
- 部署时只能保留这一份活动 DLL。禁止在 `BepInEx\plugins` 根目录或其他子目录留下同名 `.dll`；旧版本和备份必须使用 `.bak`、`.disabled` 等非 `.dll` 后缀，否则 BepInEx 会递归扫描并可能加载重复插件，导致新版无法覆盖生效。
- 安装脚本必须在部署后校验：活动 DLL 数量为 `1`，且路径必须是上面的规范子目录路径。

### 9.2 包根层级和私有数据必须单独验收

发行包应在临时伪游戏根目录解压并检查：

- 没有额外外层目录；
- 不包含 `Unturned.exe` 或其他游戏文件；
- 不包含个人配置、收藏、传送点、日志和缓存；
- 不包含本机盘符和用户目录；
- 包内 DLL 哈希等于 Release DLL；
- 便携启动脚本的 `-ValidateOnly` 只做布局检查，不启动 Steam。

### 9.3 中文文件名要求 UTF-8 清单

曾出现 `SHA256SUMS.txt` 使用 ASCII，导致中文文件名被写成问号，清单无法验证。

**修复**

```powershell
$entries | Set-Content -LiteralPath $manifestPath -Encoding UTF8
```

打包后必须重新解压并验证清单，不要只看压缩命令返回成功。

### 9.4 GitHub 认证失败不代表源码失败

历史发布任务遇到过：

- `gh auth status` 超时；
- 令牌失效；
- GitHub 连接器只能读取，创建仓库返回 403；
- 浏览器会话未登录；
- 登录后标签页被释放。

**正确分层**

1. 先完成本地源码、文档、标签和敏感信息审计。
2. 再确认远程仓库是否存在。
3. 仓库创建成功后再推送。
4. Release 创建成功后再上传 ZIP。
5. 最后下载远程资产，重新计算 SHA-256。

没有远程查询、Release 页面和下载哈希证据时，不能写“已公开发布”。

---

## 10. 验证、日志和证据管理

### 10.1 构建必须串行

Windows 下并行构建或测试可能造成 DLL 文件锁（例如 `CS2012`）。推荐顺序：

```powershell
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
dotnet run --project .\tests\FavoritesSerializationSmoke\FavoritesSerializationSmoke.csproj -c Release
dotnet run --project .\tests\LocalizationSmoke\LocalizationSmoke.csproj -c Release
dotnet run --project .\tests\TeleportSerializationSmoke\TeleportSerializationSmoke.csproj -c Release
```

最后执行：

```powershell
git diff --check
git diff --cached --check
```

### 10.2 旧日志和新日志必须分开

日志监控器或共享读取失败产生的历史错误，可能位于同一个捕获文件前部。必须：

- 按本次冷启动时间截取；
- 区分旧监控器错误和本次插件错误；
- 先确认当前 DLL 哈希，再读日志；
- 用关键阶段日志定位，而不是只搜一个 `ERROR`。

### 10.3 截图验收要“观察—单动作—刷新”

对游戏进行自动化或人工验收时：

1. 先观察当前窗口和状态；
2. 只执行一个动作；
3. 立即刷新截图和日志；
4. 再决定下一步。

特别是以下操作不能盲点：

- 删除传送点；
- 关闭/退出游戏；
- 选择 BattlEye 或无 BattlEye 启动项；
- 生成大量车辆；
- 切换多人入口。

用户明确要求自己测试时，代理只构建、部署、监控日志和诊断，不再控制游戏窗口。

### 10.4 UI living plan 的 Gate 不能被静态测试绕过

当前 UI 重构计划明确要求：

- 每轮只做一个 Round；
- 每轮完成后立刻构建、测试、回写计划；
- Gate 未通过不得进入下一阶段；
- 静态检查不能替代真实 Unturned 运行。

当前工作区还存在这些必须关注的边界：

```text
UI-007 OPEN：需要 fresh Unturned runtime evidence
UI-010 OPEN：资产 Card、滚动位置、焦点和局部刷新仍未完全收口
Phase 2 Gate：以 living plan 当前记录为准，不能从 README 历史验收倒推通过
```

---

## 11. Git、脏工作区和文档一致性

### 11.1 不要 reset、stash 或清理用户的无关改动

当前工作区存在 UI 重构相关的已暂存和未暂存修改，也有未跟踪的 companion 文档/DOCX。处理任务时：

- 先 `git status --short --branch`；
- 不要使用 `reset --hard`、广泛 `clean` 或自动 stash；
- 不要把暂存状态误写成已提交；
- 不要把 working tree 的修改误写成远程已发布版本。

### 11.2 已暂存版本和工作区版本可能不一样

本次核对发现 `scripts/Launch-Unturned-Singleplayer.ps1` 的暂存版本包含 `-ValidateOnly`，而工作区未暂存版本移除了该参数。类似情况会造成：

- 计划文档记录“已修复”，实际执行文件却没有修复；
- `git diff` 与 `git diff --cached` 分别代表不同代码；
- 测试运行的可能不是你以为的那一版。

**每次验证前必须明确：**

```text
验证的是 HEAD
验证的是 index（已暂存）
还是验证 working tree（当前文件）
```

### 11.3 canonical 文档和 companion 文档要同步

UI 计划同时存在：

```text
docs/UI_REFACTOR_IMPLEMENTATION_PLAN.md
Unturned_UI_Refactor_Codex_Living_Plan.md
```

若两份内容用于同一执行主线，修改后应比较 SHA-256 或逐段 diff。否则会出现一份写 `DONE`、另一份写 `BLOCKED` 的状态分裂。

---

## 12. 推荐的故障处理顺序

遇到新问题时按以下顺序，不要直接重装或大改：

1. **确认版本**：游戏、Unity、BepInEx、插件 DLL 版本。
2. **确认进程**：是否只有无 BattlEye 的 `Unturned.exe`。
3. **确认部署**：源 DLL、目标 DLL 的哈希和备份目录。
4. **确认日志起点**：从本次冷启动开始读。
5. **确认回调**：插件 `Awake`、更新回调、UI 回调是否真实执行。
6. **确认单层现象**：是逻辑没执行、纹理没生成、绑定失败，还是画面被遮挡。
7. **只改最小范围**：UI 问题不要改服务层；资产问题不要改收藏存储。
8. **串行构建和冒烟**。
9. **冷启动真实游戏**。
10. **截图 + 日志 + 用户操作三方交叉验证**。
11. **回写 README/ACCEPTANCE/living plan**。
12. **最后才判断是否完成或发布**。

---

## 13. 发布前最终检查表

### 源码和边界

- [ ] 目标版本、运行时和 BepInEx 版本已锁定。
- [ ] 不包含游戏/BepInEx 二进制、用户路径、日志、存档和个人数据。
- [ ] 单人守卫仍有效。
- [ ] BattlEye/多人边界没有被 UI 或启动脚本放宽。
- [ ] UI 轮次没有偷偷改服务、资产目录或存储层。

### 构建和部署

- [ ] Release 构建 0 警告、0 错误。
- [ ] 三项序列化/本地化 smoke 顺序执行并通过。
- [ ] 游戏已退出后才部署。
- [ ] 部署前已有逐文件备份。
- [ ] 源 DLL 与目标 DLL SHA-256 一致。

### 真实运行

- [ ] 选择无 BattlEye 启动方式。
- [ ] 没有 `Unturned_BE.exe`，`BEService` 未运行。
- [ ] 进入单人世界后再打开菜单。
- [ ] 快捷键不会切换原生 HUD。
- [ ] 原生光标位于菜单上方，视角不再跟随鼠标。
- [ ] 六个 Tab、搜索、分类、分页和数量控件真实可用。
- [ ] 物品图标、车辆缩略图不是白色占位。
- [ ] 收藏、传送保存/恢复/删除真实验证。
- [ ] 时间、天气、满月、空投等逐项验证。
- [ ] 关闭菜单后角色输入恢复。

### 发布

- [ ] 公开 Release 资产只有 Plugin-Only 包，且包内不含 BepInEx 核心或私有数据。
- [ ] 包可直接解压到含 `Unturned.exe` 的根目录。
- [ ] 中文文件名清单使用 UTF-8。
- [ ] 临时伪游戏根目录解压/布局检查通过。
- [ ] 远程仓库、Release、下载资产和远程 SHA-256 均有证据。

---

## 14. 当前项目状态说明

截至 2026-08-15，本文件的整理依据显示：

- v1.2.0 的源码、README 和历史 ACCEPTANCE 包含完整功能与运行验收记录。
- 历史 UI 重构会话使用过 `codex/refactor-ui-v2`；当前工作树分支必须以 `git status --short --branch` 的实时结果为准。当前仍有已暂存、未暂存和未跟踪文件混合存在。
- living plan 明确保留 UI-007、UI-010 等运行时/局部刷新边界，不能仅凭 v1.2.0 历史 README 宣称当前 UI 重构全部完成。
- 本避坑文档只新增文档，不替代当前 living plan，也不自动关闭任何 Gate。

后续每修复一个新问题，都应按以下格式追加到对应章节：

```text
现象：
根因：
最小修复：
源码/构建证据：
真实运行证据：
仍未证明：
```
