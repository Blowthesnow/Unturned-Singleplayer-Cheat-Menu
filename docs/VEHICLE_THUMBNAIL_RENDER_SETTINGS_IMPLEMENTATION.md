# 车辆缩略图渲染设置与性能优化落地活文档

> 状态：**源码实施完成；倍率范围已完成用户实机视觉验收；默认值变更 DLL 已部署并加载**
> 文档性质：**实施计划 + 任务台账 + 验收记录**
> 当前阶段：**代码、编译、烟囱测试、静态边界审查、倍率三档截图验收和新 DLL 运行时加载已完成；当前配置仍显式使用 1.5**
> 适用版本边界：BepInEx 5、Unity Mono、Unturned 3.x；本轮功能随 v1.6.0 发布。

本文档把“车辆缩略图可配置渲染尺寸、自动取景倍率、磁盘缓存、懒加载和性能边界”整理为可直接执行的工程任务。
本轮已完成生产代码落地、编译、烟囱测试和静态审查；用户已提供 `0.5`、`1.0`、`1.5` 三档车辆构图截图作为倍率视觉验收证据。新的默认值 `1.0` 已随新 DLL 部署并由 BepInEx 加载；当前配置仍显式使用 `1.5`，不会自动迁移。验证记录见 `artifacts/vehicle-thumbnail-validation-20260816.txt`。

## 1. 目标与非目标

### 1.1 目标

实现一个位于“载具数量”工具栏中的小型“渲染设置”入口，使用户可以：

- 在 `128 × 96`、`192 × 144` 和 `256 × 192` 三档之间切换车辆缩略图尺寸。
- 调整自动取景车辆的模型倍率/边距，范围为 `0.5–1.5`，默认 `1.0`。
- 关闭窗口后设置仍然保留，重启游戏后继续使用。
- 在不影响游戏全局画质的前提下，减少重复实例化、重复渲染和重复透明检测。

### 1.2 非目标

- 不在本阶段允许任意宽高输入；只提供 `128 × 96`、`192 × 144`、`256 × 192` 三个 4:3 比例预设。
- 不修改全局 `QualitySettings`、阴影质量、纹理质量、抗锯齿或全局 LOD。
- 不把缓存 PNG、构建产物、日志或运行时临时文件提交进仓库。
- 不把本设计文档当作生产代码变更；实现前后要分别记录源码、构建和实机证据。

## 2. 用户需求归纳

用户可见行为必须满足以下约束：

1. 在增加载具数量的 `+` 按钮右侧增加“齿轮图标 + 渲染设置”。
2. 点击后打开小型设置窗口。
3. 默认尺寸为 `128 × 96`；用户改为 `192 × 144` 或 `256 × 192` 后，所有车辆缩略图都按新尺寸生成和显示。
4. 尺寸设置必须持久化。
5. 尺寸选项下方必须显示性能说明。
6. 保留 `_pendingVehicles`，避免同一辆车重复排队。
7. 增加以“车辆 GUID + 渲染配置版本”为基础的磁盘 PNG 缓存。
8. 重启后优先读取磁盘缓存，不重新实例化车辆模型。
9. 重新扫描只清理内存缓存，不立即全量重渲染；车辆卡片真正可见时再懒加载。
10. 仅自动取景车辆进行透明检测，且仅自动取景车辆强制 LOD0。
11. 自动取景倍率使用滑动条和滑块把手，复用“其他”页面时间控制滑块的 UI 风格和实现。

## 3. 当前实现基线

### 3.1 车辆图标渲染

当前实现位于 `src/UnturnedSingleplayerCheatMenu/Services/VehicleIconRenderer.cs`：

- `TryEnqueue` 将请求加入渲染队列，`PumpOne` 每次处理一项。
- 当前固定调用 `ItemTool.captureIcon(..., 128, 96, ...)`。
- 取景优先级为 `Icon2`、`Icon`、自动生成取景变换。
- 当前所有模型都会执行 `ForceHighestLod`，需要收窄为“只对自动取景路径执行”。
- 当前所有结果都会执行完整 `GetPixels32()` 透明扫描，需要改为只对自动取景路径执行。
- 原实现使用 `1.12f` 的固定边距系数，现已改为不可变渲染请求中的用户配置值。

### 3.2 内存缓存与去重

当前实现位于 `src/UnturnedSingleplayerCheatMenu/Services/IconCache.cs`：

- `_vehicleIcons : Dictionary<Guid, Texture2D>` 保存内存纹理。
- `_pendingVehicles : HashSet<Guid>` 防止同一 GUID 重复入队。
- `GetVehicleIcon` 先读内存，再判断 pending，最后才入队。
- `Clear()` 会取消请求并销毁内存纹理。

`_pendingVehicles` 是并发/重建卡片时的关键保护，不能删除或改为只依赖 UI 状态。

### 3.3 调度与可见卡片

`CheatMenuPlugin.RunUpdateCallback` 当前每帧调用渲染泵，并且车辆渲染队列已经是一帧一辆。
`CheatMenuOverlayUi` 当前只构建当前页车辆卡片，`OverlayIconBinder` 会在卡片存在时尝试绑定纹理，因此已经具备可扩展为“可见卡片懒加载”的基础。

### 3.4 UI 与持久化

载具数量工具栏位于 `CheatMenuOverlayUi` 的车辆工具栏构建逻辑中。
时间控制滑块由 `CreateTimeSlider`、`GetSliderGripSprite`、`CreateSliderGripTexture` 等方法组成，可作为新倍率滑块的视觉和交互基线。
插件已有 BepInEx `ConfigEntry` 和 `PersistInterfaceState` 保存模式，应继续沿用，不额外引入独立 JSON 设置文件。

## 4. 设计决策

### 4.1 不可变渲染配置快照

新增小型配置模型（建议文件名：`VehicleThumbnailRenderSettings.cs`）：

```text
VehicleThumbnailRenderSettings
- Width: 128、192 或 256
- Height: 96、144 或 192
- Framing: 0.5–1.5
- CacheFormatVersion: int
```

入队时复制一份不可变快照，渲染期间不直接读取 UI 控件或可变 `ConfigEntry`。这样可以避免用户在队列处理中途切换尺寸，导致同一批请求产生混合配置结果。

推荐默认值：

```text
Width = 128
Height = 96
Framing = 1.0
CacheFormatVersion = 1
```

读取配置时必须规范化：

- 非法尺寸回退到 `128 × 96`。
- 只接受 `128×96`、`192×144`、`256×192` 三组固定组合；宽高不匹配时按宽度回退到对应的合法高度。
- `Framing` 使用 `Clamp(0.5, 1.5)`。
- 浮点值按千分比存入缓存键，避免使用不稳定的浮点字符串。

### 4.2 配置项与持久化

推荐在现有 `[Interface]` 区域增加：

```text
VehicleIconResolution = 128
VehicleIconFraming = 1.0
```

其中 `VehicleIconResolution` 为宽度枚举值：`128`、`192` 或 `256`；高度由固定 4:3 比例推导。
如果后续希望配置文件可读性更高，也可以使用独立 `[VehicleThumbnail]` 区域，但必须与当前 BepInEx 配置结构保持一致，不新建平行存储系统。

应用按钮的行为：

1. 读取并规范化 UI 值。
2. 更新运行时渲染设置快照。
3. 调用现有 `PersistInterfaceState` 模式写入 `Config.Save()`。
4. 标记车辆页需要重新绑定，但不批量预渲染所有车辆。
5. 当前可见卡片按新配置懒加载。

配置保存失败时：

- 本次运行仍使用新值。
- 记录 warning。
- 设置窗口显示“已应用但未能持久化”的状态反馈。

### 4.3 缓存键和文件布局

缓存键必须至少包含：

```text
车辆 GUID
渲染配置版本
宽度 × 高度
自动取景倍率（千分比）
```

建议文件名：

```text
{guid}_{cacheVersion}_{width}x{height}_f{framingMilli}.png
```

示例：

```text
3a1f..._1_128x96_f1120.png
3a1f..._1_192x144_f1120.png
3a1f..._1_256x192_f1120.png
```

车辆 `GUID` 采用 `VehicleAsset.GUID`/资产 GUID 作为稳定身份依据；缓存键不使用显示名称、翻译文本或 UI 索引。

建议新增 `VehicleIconDiskCache.cs`，缓存目录使用：

```text
Paths.CachePath / "UnturnedSingleplayerCheatMenu" / "VehicleIcons"
```

不手工拼接 `BepInEx/config`，也不把 PNG 放在插件发布目录。

磁盘缓存要求：

- 首次打开时不扫描并解码全部 PNG。
- `GetVehicleIcon` 在内存未命中后，按当前配置查询单个文件。
- 文件命中后分帧解码，避免一次性加载几十张纹理。
- PNG 损坏、尺寸不符或解码失败时删除坏文件并重新渲染。
- 写文件使用临时文件后替换，避免进程退出留下半文件。
- 缓存写入失败不能让插件崩溃，只记录 warning 并继续使用内存结果。
- 缓存是派生数据，不纳入 Git、发布压缩包或更新日志中的源码文件清单。

### 4.4 透明检测与 LOD 边界

将渲染分为两条路径：

| 路径 | 取景来源 | 透明检测 | 强制 LOD0 | 使用倍率设置 |
|---|---|---:|---:|---:|
| 预设图标 | `Icon2` / `Icon` | 否 | 否 | 否 |
| 自动取景 | `CreateGeneratedIconTransform` | 是 | 是 | 是 |

具体要求：

- 预设图标使用游戏提供的构图，不执行整张 `GetPixels32()` 扫描。
- 预设图标不调用 `ForceHighestLod`，避免不必要的模型开销。
- 自动取景才调用 `ForceLOD(0)`，且只作用于临时预览模型。
- 自动取景才执行透明检测；检测失败时最多重拍一次，超过上限则记录失败并释放临时资源。
- 禁止修改全局 `QualitySettings.globalTextureMipmapLimit`、`antiAliasing`、`shadowResolution`、`lodBias` 或同类全局选项。

### 4.5 自动取景倍率

自动取景计算使用配置快照中的 `Framing`：

```text
orthoSize = max(halfHeight, halfWidth / PreviewAspect) * Framing
```

语义必须在 UI 和文档中保持一致：

- `0.5`：主体更大、留白更少，可能更接近裁切边界。
- `1.0`：默认平衡值。
- `1.5`：留白更多、主体更小，裁切风险更低。

实现必须对计算结果做最小/最大安全限制，不能因为极端模型尺寸导致相机距离或正交尺寸为零。

### 4.6 设置窗口和按钮

建议新增以下 UI 方法：

```text
ShowVehicleThumbnailSettings()
HideVehicleThumbnailSettings()
CreateVehicleThumbnailSettingsDialog()
ApplyVehicleThumbnailSettings()
```

工具栏目标布局：

```text
载具数量：[-] [数量] [+] [⚙ 渲染设置]
```

齿轮图标优先复用项目现有程序生成图标机制，不引入外部图片资源。

小窗口内容：

1. 标题：`载具渲染设置`
2. 尺寸选项：
   - `128 × 96（低开销）`
   - `192 × 144（平衡）`
   - `256 × 192（更清晰）`
3. 性能说明：
   - `128 × 96` 为默认，生成速度快、显存和磁盘占用低。
   - `192 × 144` 在清晰度和开销之间折中，单次输出像素数约为 `128 × 96` 的 2.25 倍、约为 `256 × 192` 的 56%。
   - `256 × 192` 更清晰，单次输出像素数约为 `128 × 96` 的 4 倍，首次生成更慢。
   - 已生成的缩略图会进入缓存，重复打开不会重复实例化车辆。
4. 自动取景倍率滑块：
   - 最小 `0.5`
   - 最大 `1.5`
   - 默认 `1.0`
   - 显示当前数值，例如 `取景倍率：1.0`
5. 操作按钮：
   - `应用`
   - `取消`
   - 可选：`清理当前渲染配置缓存`

倍率滑块复用“其他”页面时间控制的 Slider、轨道、边框和手柄视觉，不复制一套不一致的样式。

### 4.7 重新扫描、内存清理和懒加载

重新扫描模组资产时：

- 取消待处理的渲染请求。
- 清理内存纹理和 `_pendingVehicles`。
- 不删除磁盘 PNG 缓存。
- 不循环请求全部车辆图标。
- 重建当前页面后，由真正存在/可见的车辆卡片触发按需加载。

可见性判断建议沿用现有 `OverlayIconBinder` 的卡片生命周期；不可见分页不提前实例化车辆模型。

同一 GUID 的快速卡片重建必须继续经过 `_pendingVehicles` 去重。
缓存命中时不调用 `VehicleTool.getVehicle`，也不创建临时车辆模型。

限制说明：GUID 相同但资产模型本身发生变化时，当前键无法自动识别内容变化；如后续出现此需求，再增加游戏版本、资产版本或内容哈希字段，不在本阶段扩大范围。

## 5. 推荐渲染流程

```text
车辆卡片进入可见范围
        │
        ▼
IconCache.GetVehicleIcon(guid, settingsSnapshot)
        │
        ├─ 内存命中 ───────────────► 直接绑定 RawImage
        │
        ├─ pending 命中 ───────────► 不重复排队
        │
        ├─ 磁盘 PNG 命中 ──────────► 分帧解码、放入内存、绑定
        │
        └─ 未命中
              │
              ▼
       _pendingVehicles.Add(guid)
              │
              ▼
       一帧最多 PumpOne 一项
              │
              ▼
       实例化临时模型并选择取景路径
              │
              ├─ 预设图标：不透明扫、不强制 LOD0
              │
              └─ 自动取景：LOD0、透明检测、倍率设置
              │
              ▼
       captureIcon(width, height)
              │
              ▼
       内存缓存 + 原子写入 PNG
              │
              ▼
       释放临时模型/文件流，绑定卡片
```

失败分支必须清理：

- 临时车辆模型。
- 临时 `Texture2D`。
- PNG 文件流和临时文件。
- `_pendingVehicles` 中对应 GUID。
- 本次请求的重试状态。

## 6. 性能预算与保护措施

### 6.1 预算

- 默认 `128 × 96` 优先保证首次打开车辆页无明显卡顿。
- `192 × 144` 作为中间档，适合希望提高缩略图清晰度但不希望承担最高生成开销的用户。
- `256 × 192` 输出像素约为 `128 × 96` 的 4 倍，只增加首次生成耗时和缓存占用。
- 每帧最多处理一辆车的渲染请求。
- 磁盘命中不实例化车辆模型。
- 页面分页不可见的车辆不提前渲染。

### 6.2 明确禁止

不可加入以下全局修改：

```text
QualitySettings.globalTextureMipmapLimit
QualitySettings.antiAliasing
QualitySettings.shadowResolution
QualitySettings.lodBias
```

允许的局部优化仅限于：

- 临时预览模型自身 LOD。
- 临时预览模型 Renderer 层设置。
- 当前 capture 的相机和正交尺寸。
- 当前生成纹理自身的尺寸、过滤和读取状态。

### 6.3 资源释放

每条路径都要验证 `Texture2D`、临时模型、组件和文件流的释放。
PNG 写入失败、磁盘不可写或解码异常必须转为可恢复状态，不得使插件初始化或 UI 线程崩溃。

## 7. 受影响文件清单

### 7.1 预计修改

- `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs`
  - 新增配置项、默认值、规范化和持久化。
  - 在更新循环中维持现有单项渲染泵。
- `src/UnturnedSingleplayerCheatMenu/Services/IconCache.cs`
  - 保留 `_pendingVehicles`。
  - 接入磁盘缓存查询、命中和失败回退。
  - 区分“清理内存”与“删除磁盘缓存”。
- `src/UnturnedSingleplayerCheatMenu/Services/VehicleIconRenderer.cs`
  - 接收渲染配置快照。
  - 使用动态尺寸和自动取景倍率。
  - 收窄透明检测、LOD0 到自动取景路径。
- `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs`
  - 在载具数量 `+` 按钮右侧加入设置入口。
  - 创建小型设置窗口、尺寸选项、性能说明和倍率 Slider。
  - 设置应用后只刷新可见卡片。
- `src/UnturnedSingleplayerCheatMenu/Services/PluginLocalization.cs`
  - 增加中文和英文的标题、选项、说明、状态反馈。

### 7.2 建议新增

- `src/UnturnedSingleplayerCheatMenu/Services/VehicleThumbnailRenderSettings.cs`
- `src/UnturnedSingleplayerCheatMenu/Services/VehicleIconDiskCache.cs`

### 7.3 不应加入提交

- `Unturned_UI_Optimization_Codex_Plan_Simplified.docx`
- 构建产物、临时 PNG、缓存目录、运行日志和本地调试文件。

## 8. 分阶段实施任务台账

状态约定：`未开始`、`进行中`、`已完成`、`阻塞`。
证据列必须填写实际路径、日志片段、截图或测试结果；不能用“代码看起来正确”替代。

| 编号 | 任务 | 预计文件 | 状态 | 证据 |
|---|---|---|---|---|
| T01 | 建立 `VehicleThumbnailRenderSettings`，实现尺寸/倍率规范化 | 新增设置模型 | 已完成 | `src/UnturnedSingleplayerCheatMenu/Services/VehicleThumbnailRenderSettings.cs`；`tests/VehicleThumbnailSettingsSmoke` PASS |
| T02 | 增加 BepInEx 配置项，默认 `128×96`，支持 `192×144`、`256×192` 和倍率 `1.0` | `CheatMenuPlugin.cs` | 已完成 | `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs`：`VehicleIconResolution`、`VehicleIconFraming`、`ApplyVehicleThumbnailSettings` |
| T03 | 将配置快照传入渲染请求，禁止队列中途读 UI | `IconCache.cs`、`VehicleIconRenderer.cs` | 已完成 | `VehicleIconRenderer.TryEnqueue(...settings...)`；`IconCache` 捕获不可变快照 |
| T04 | 接入车辆 GUID + 配置版本的 PNG 磁盘缓存 | `VehicleIconDiskCache.cs`、`IconCache.cs` | 已完成 | `VehicleIconDiskCache.GetPath`；缓存根目录使用 `Paths.CachePath` |
| T05 | 实现损坏缓存删除、临时文件替换和资源释放 | 磁盘缓存相关文件 | 已完成 | `VehicleIconDiskCache.TryLoad/TrySave`；静态审查和 Release 编译 PASS |
| T06 | 将透明检测和 LOD0 限定到自动取景路径 | `VehicleIconRenderer.cs` | 已完成 | `Render` 仅对 generated 路径调用 `TryFinalizeTexture`；`ForceLOD(0)` 同路径调用 |
| T07 | 将固定倍率改为配置并保留安全 Clamp | `VehicleIconRenderer.cs` | 已完成 | `CreateGeneratedIconTransform` 使用 `settings.Framing`；设置烟囱测试覆盖 0.5/1.0/1.5 和双向越界 |
| T08 | 在载具数量 `+` 右侧加入齿轮和“渲染设置” | `CheatMenuOverlayUi.cs` | 已完成 | `BuildVehiclesTab` 中 `⚙ 渲染设置` 紧邻 `+`；实机位置截图待补 |
| T09 | 复用时间控制 Slider 创建倍率滑块 | `CheatMenuOverlayUi.cs` | 已完成 | `ShowVehicleThumbnailSettings` 调用现有 `CreateTimeSlider`；实机交互待补 |
| T10 | 增加中英文标题、性能说明和保存失败反馈 | `PluginLocalization.cs` | 已完成 | `tests/LocalizationSmoke` PASS；静态模板基线更新为 168 |
| T11 | 重新扫描只清理内存，不触发全量渲染 | `IconCache.cs`、车辆页刷新逻辑 | 已完成 | `ClearVehicleMemory` 保留磁盘文件；`RefreshCatalog` 未增加全量排队 |
| T12 | 验证可见卡片懒加载与 `_pendingVehicles` 去重 | UI/缓存/渲染链路 | 已完成（源码/静态） | `BuildVehicleGrid` 只创建当前页卡片；`_pendingVehicles` 和单项 `PumpOne` 保留；实机队列日志待补 |
| T13 | 运行编译、单元/烟囱测试和 `git diff --check` | 项目全局 | 已完成 | `artifacts/vehicle-thumbnail-validation-20260816.txt`：Release 0 警告/0 错误，4 组烟囱测试 PASS，`git diff --check` PASS |
| T14 | 在真实 Unturned 游戏中验证尺寸、缓存、性能和 UI | 游戏运行环境 | 部分完成 | 用户实机截图：`C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-2cb7582c-2f2c-4dd5-8fc0-16475bad9713.png`（0.5）、`C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-ee565f9a-018a-4b5f-9be3-d8d9833334a5.png`（1.0）、`C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-efe9662c-092d-43a0-86d5-6ae901cf2436.png`（1.5）；`I:\Steam\steamapps\common\Unturned\BepInEx\LogOutput.log` 确认新 DLL 加载、宿主创建和更新补丁注册；当前配置仍为 `VehicleIconFraming = 1.5` |

## 9. 验收矩阵

### 9.1 设置与 UI

| 验收项 | 通过标准 | 证据 |
|---|---|---|
| 默认值 | 源码默认 `128 × 96`、`1.0`；已有显式配置不被覆盖 | `VehicleThumbnailSettingsSmoke` PASS；部署 DLL SHA-256 为 `74A1AAEA231184975AE9C6ADC53D4FB38F88D5766C9A4E52B3E30F889D268979`，BepInEx 已加载新 DLL；当前配置 `I:\Steam\steamapps\common\Unturned\BepInEx\config\com.codex.unturned.singleplayer-cheat-menu.cfg` 的 `VehicleIconFraming = 1.5` 已保留 |
| 入口位置 | `BuildVehiclesTab` 中设置按钮紧邻车辆数量 `+` | `CheatMenuOverlayUi.cs` 源码已确认；实机位置截图待补 |
| 尺寸切换 | 配置快照和 `captureIcon` 使用三档实际宽高 | 设置烟囱测试 PASS；真实纹理尺寸待实机确认 |
| 页面覆盖 | 普通车辆卡片和收藏车辆卡片共用 `CreateVehicleCard` | `CheatMenuOverlayUi.cs` 源码已确认；实机覆盖待补 |
| 倍率范围 | 设置模型限制到 `0.5–1.5`，覆盖默认值和边界 | `VehicleThumbnailSettingsSmoke` PASS；用户已提供 0.5/1.0/1.5 三档实机截图 |
| 持久化 | 应用写入 BepInEx `ConfigEntry` 并调用 `Config.Save()` | `CheatMenuPlugin.ApplyVehicleThumbnailSettings` 源码已确认；当前配置文件保留用户值 `1.5`，新 DLL 已重启加载 |
| 双语 | 168 个静态中文模板均有英文映射 | `LocalizationSmoke` PASS；实机语言切换截图待补 |

### 9.2 缓存与队列

| 验收项 | 通过标准 | 证据 |
|---|---|---|
| pending 去重 | `_pendingVehicles` 按 GUID 去重，快速重建不重复入队 | `IconCache.GetVehicleIcon` 源码已确认；实机日志待补 |
| 首次缓存 | 渲染回调写入 `Paths.CachePath/.../VehicleIcons` | `VehicleIconDiskCache.TrySave` 源码已确认；实机 PNG 待补 |
| 重启命中 | 单项分帧解码命中后不进入 `VehicleIconRenderer` | `IconCache.PumpOne` 源码已确认；重启日志待补 |
| 配置隔离 | 文件名包含 GUID、版本、宽高和千分比倍率 | `VehicleIconDiskCache.GetPath` 源码已确认；实机文件待补 |
| 损坏恢复 | 解码失败/尺寸不符删除坏文件并回退渲染 | `VehicleIconDiskCache.TryLoad` 源码已确认；损坏文件实测待补 |
| 重新扫描 | 清空内存和 pending，不删除磁盘 PNG、不全量排队 | `IconCache.ClearVehicleMemory` 源码已确认；实机尖峰记录待补 |
| 懒加载 | 只为当前页实际创建的卡片调用图标 loader | `BuildVehicleGrid`/`OverlayIconBinder` 源码已确认；实机可见性待补 |

### 9.3 渲染边界与性能

| 验收项 | 通过标准 | 证据 |
|---|---|---|
| 预设路径 | `Icon2`/`Icon` 不进行完整透明扫描、不强制 LOD0 | `VehicleIconRenderer.Render` 静态审查 PASS |
| 自动取景 | 执行透明检测、强制 LOD0、使用倍率配置 | `VehicleIconRenderer.RenderAttempt` 静态审查 PASS |
| 重试上限 | 自动取景透明检测失败最多重拍一次 | `VehicleIconRenderer.Render` 静态审查 PASS |
| 全局质量 | 全局 `QualitySettings`、阴影、纹理质量和全局 LOD 未被修改 | 源码固定字符串扫描 PASS |
| 帧预算 | 每帧最多处理一项磁盘解码或模型渲染 | `CheatMenuPlugin.RunUpdateCallback` + `IconCache.PumpOne` 静态审查 PASS |
| 默认体验 | 默认设置打开车辆页无明显卡顿 | 实机性能观察待补，不能用编译替代 |
| 中档体验 | `192×144` 提高清晰度但开销明显低于 `256×192`，不改变游戏全局画质 | 尺寸/全局边界源码已完成；实机性能待补 |
| 高清体验 | `256×192` 只增加首次生成耗时，不改变游戏全局画质 | 尺寸/全局边界源码已完成；实机性能待补 |
| 资源释放 | 临时模型、纹理、文件流和坏缓存文件均可回收/清理 | `VehicleIconRenderer`/`VehicleIconDiskCache` 静态审查 PASS；实机回收待补 |

## 10. 实施后的验证顺序

1. 先完成静态实现和编译，记录编译命令、目标框架和输出。
2. 运行缓存键、配置规范化、队列去重和损坏 PNG 测试。
3. 运行 `git diff --check`，确认 Markdown、C# 和配置无尾随空格。
4. 启动真实游戏，验证设置窗口、按钮位置、尺寸切换和倍率滑块。
5. 第一次打开车辆页记录实例化数量、渲染队列深度和卡顿观察。
6. 关闭并重启游戏，确认命中磁盘缓存且不重新实例化车辆模型。
7. 执行重新扫描，确认只清理内存，不触发全量渲染。
8. 依次切换到 `192×144`、`256×192`，确认只渲染可见卡片，切回 `128×96` 可复用旧缓存。
9. 检查普通预设图标和自动取景图标的透明检测、LOD 行为差异。
10. 保存截图、日志和必要的性能记录，回填本文件验收矩阵。

## 11. 异常、回滚和兼容策略

- 配置项缺失：使用默认值，不阻止插件启动。
- 配置项非法：规范化后使用安全值，并记录 warning。
- 配置保存失败：本次运行继续使用新值，提示用户下次启动可能恢复旧值。
- 缓存目录不可写：跳过磁盘写入，继续提供内存图标。
- PNG 解码失败：删除对应文件，回退到渲染路径。
- 渲染失败：释放临时资源，清理 pending，按上限重试；最终显示现有占位状态。
- 新版本缓存格式变化：递增 `CacheFormatVersion`，旧版本文件自然失效，不做危险的批量迁移。
- 用户取消设置：不写入配置，不改变当前运行时快照。

## 12. 当前未完成项与证据账本

截至 2026-08-16：

- 设计与任务拆解：已完成。
- 生产代码实施：已完成，新增设置模型和磁盘缓存服务。
- 编译验证：已完成，Release 0 警告/0 错误，输出 `artifacts/UnturnedSingleplayerCheatMenu.dll`。
- 单元/烟囱测试：已完成，车辆设置、本地化、收藏序列化、传送点序列化均 PASS。
- 静态边界审查：已完成，未发现四项禁止的全局 `QualitySettings` 修改。
- 真实 Unturned 运行验证：阻塞；当前工作区没有 `Unturned.exe`，也没有 `UNTURNED_GAME_DIR`。
- 磁盘缓存命中证据：源码已实现，真实 PNG/重启命中日志待在游戏环境补录。
- UI 截图、性能数据和双语运行时验收证据：待在游戏环境补录。

本轮明确区分：

- 源码设计完成；
- 编译通过；
- 测试通过；
- 真实游戏运行通过；
- 用户可见发布完成。

验证记录：`artifacts/vehicle-thumbnail-validation-20260816.txt`。该文件位于被忽略的构建产物目录，不纳入源码提交。

## 13. 后续可选优化

以下内容不属于本阶段必做项：

- 增加资产内容哈希，识别 GUID 不变但模型已更新的情况。
- 增加缓存总大小上限和按最近使用时间清理。
- 增加后台 PNG 解码线程，但必须确认 Unity `Texture2D` 创建仍在主线程安全边界内。
- 增加“仅清理当前配置缓存”和“清理全部车辆缓存”的分级操作。
- 记录每项渲染耗时，用于后续自动调整每帧预算。
