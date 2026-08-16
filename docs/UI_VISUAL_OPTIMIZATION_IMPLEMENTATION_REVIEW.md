# Unturned UI 视觉优化实现回审

> **回审日期：** 2026-08-16
> **对照文档：** `docs/UI_VISUAL_OPTIMIZATION_PROPOSAL.md`
> **适用插件：** `UnturnedSingleplayerCheatMenu`
> **回审范围：** 当前工作树中的本轮视觉实现，不覆盖已有的非 UI 重构改动。

## 1. 结论摘要

Phase A（视觉基础统一）与 Phase B（层级、密度和反馈优化）已在源码层落地。
根据 2026-08-16 的真实运行截图，先后补强了普通按钮，以及输入框、时间滑块、
已保存传送点行和底部状态栏的 Surface 对比度、内边距与可见边界，解决控件与背景
融合、滑块过高、传送点文字贴边和状态栏无边界的问题。
实现没有引入新的第三方 UI DLL、AssetBundle、Prefab 或 UI 框架。

Phase C 中的视觉验收已依据用户提供的六页真实运行截图完成：角色、物品、车辆、
收藏、传送和其他页面均能正常显示，输入框、时间滑块、按钮/Toggle 边界、生存状态
Summary、传送点行、收藏/关闭图标以及底部状态栏的视觉问题均已闭环。
截图证据由用户提供，本回审不把它扩大解释为已完成 Scale、输入捕获、关闭恢复或
日志回归等未被明确证明的交互/运行项目。

## 2. 已落地项目

| 方案验收项 | 实现位置 | 状态 |
|---|---|---|
| 语义化颜色 Token | `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuStyle.cs` | 已落地 |
| `SurfaceRaised`、`Success`、`Warning`、`DisabledSurface` | `CheatMenuStyle` | 已落地 |
| 输入框 Surface 与边界 | `CreateInput`、`CheatMenuStyle.InputBorder` | 已落地，用户实机截图验收通过 |
| 时间滑块尺寸、轨道边界与程序生成斜向纹理把手 | `CreateTimeSlider`、`CheatMenuStyle.SliderBorder` | 已落地，用户实机截图验收通过 |
| Button Normal/Hover/Pressed/Disabled | `CheatMenuOverlayUi.CreateButton`、`OverlaySelectableVisual` | 已落地 |
| Button 可见边界 | `CreateButton` 的原生 `Outline`、状态化边框色 | 已落地，用户实机截图验收通过 |
| Button 键盘/焦点可见指示 | `OverlaySelectableVisual` 的底部焦点条 | 已落地 |
| Input 焦点可见指示 | `CreateInput`、`OverlayFocusIndicator` | 已落地 |
| Toggle 的勾选、`ON/OFF`、背景状态 | `CreateToggleAction` | 已落地 |
| Toggle OFF 边界 | `CreateToggleAction`、`CheatMenuStyle.ToggleBorder` | 已落地，用户实机截图验收通过 |
| 类型化 Status Bar | `SetStatus`、`ResetStatus`、状态左色条 | 已落地 |
| Status Bar 固定尺寸 | `EnsureShell`、`CheatMenuStyle.StatusHeight` | 已落地，用户实机截图验收通过 |
| Success/Warning/Error/Info 状态色 | `GetStatusColor`、调用点状态分类 | 已落地 |
| 角色页 Summary Row | `CreateSummaryPanel`、`BuildCharacterTab` | 已落地 |
| 生存状态 Summary 边界 | `CreateSummaryPanel`、`CheatMenuStyle.SummaryBorder` | 已落地，用户实机截图验收通过 |
| 收藏星标与关闭图标几何居中 | `CreateFavoriteGlyphSprite`、`CreateCloseGlyphSprite` | 已落地，用户实机截图验收通过 |
| 区块标题的左侧语义标记 | `CreateSection` | 已落地 |
| 物品/车辆搜索空状态 | `CreateEmptyState`、物品/车辆网格构建 | 已落地 |
| 收藏空状态与下一步提示 | `BuildFavoriteGrid`、`CreateEmptyState` | 已落地 |
| 传送空状态与跨地图 Disabled 语义 | `BuildTeleportsTab` | 已落地 |
| 已保存传送点行内边距与边界 | `BuildTeleportsTab`、`CheatMenuStyle.TeleportBorder` | 已落地，用户实机截图验收通过 |
| 删除确认默认焦点落在取消 | `ShowTeleportDeleteConfirmation` | 已落地 |
| 其他页时间/世界事件/扫描信息分组 | `BuildOtherTab` | 已落地 |
| 扫描操作与天气操作分离 | `BuildOtherTab` | 已落地 |
| 卡片加载预览/无预览占位 | `AddCardContents`、`OverlayIconBinder` | 已落地 |
| 卡片来源短标签 | `AssetCatalog.GetOriginLabel` 与卡片元信息 | 已有并保留 |
| Status Bar Surface、内边距与边界 | `EnsureShell`、`CheatMenuStyle.StatusBorder` | 已落地，用户实机截图验收通过 |
| 中英文新增 UI 文案 | `Services/PluginLocalization.cs` | 已落地 |

## 3. 验证证据

已执行：

```text
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release --no-restore
```

结果：

- 0 个警告；
- 0 个错误；
- 插件 DLL 输出到 `artifacts/UnturnedSingleplayerCheatMenu.dll`。

已执行：

```text
dotnet run --project .\tests\LocalizationSmoke\LocalizationSmoke.csproj -c Release --no-build
dotnet run --project .\tests\FavoritesSerializationSmoke\FavoritesSerializationSmoke.csproj -c Release --no-build
dotnet run --project .\tests\TeleportSerializationSmoke\TeleportSerializationSmoke.csproj -c Release --no-build
```

结果：

- Localization smoke checks passed；
- Favorites JSON round-trip: PASS；
- Teleport JSON round-trip: PASS；
- 新增静态中文模板均有英文映射；
- `git diff --check` 未发现空白错误。

本轮针对最新实机反馈完成的源码与自动化验证：

- 输入框、时间滑块、传送点行和状态栏均已加入独立的 Surface/Outline 层级；
- 时间滑块 Release 构建通过，控件目标尺寸进一步收紧为约 `170×22`，把手约 `14×18`，
  使用程序生成的实际像素斜向纹理，并保留百分比文本预览；
- 收藏星标和关闭叉号使用程序生成的几何 Sprite，脱离字体字形差异实现居中对齐；
- 生存状态 Summary、冻结时间 OFF、强制满月 OFF 均增加统一边界；
- Status Bar 使用固定 `minHeight / preferredHeight / flexibleHeight`，内部色条和文字
  固定高度，状态文本禁止换行，避免页面内容把底部区域撑大；
- Release 构建：0 个警告、0 个错误；
- Localization / Favorites / Teleport 三项 smoke：全部通过；
- `git diff --check`：通过。

上一轮已完成针对实机截图反馈的 Release 部署：

- 目标：`I:\Steam\steamapps\common\Unturned\BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll`
- 部署结果：源 DLL 与目标 DLL SHA-256 一致；
- 上一轮按钮边界版本目标 DLL SHA-256：`A519B4C7DD015ED55DB673DAAF75103847C609224394CC1701AA80C73134293A`；
- 本轮输入/滑块/传送点/状态栏版本已重新部署，目标 DLL SHA-256：
  `8A1A3B886F30CD621C1B3AD239207F25DA30679DE4B2D7173237E3D998CB05B1`；
- 上一版本已保留为 `UnturnedSingleplayerCheatMenu.dll.bak-20260816-125730-ui-input-slider-teleport-status`；
- 用户提供的截图已确认普通按钮以及非按钮控件的边界问题；本轮最新 DLL 已在
  `Unturned` 与 `Unturned_BE` 均退出后完成部署。
- 上一轮固定状态栏版本源 DLL 与目标 DLL SHA-256：
  `4A16B3E9725DC2AE78E5F71C9E3D9E90A0856670817596BEBF96909B79257556`；
- 本轮程序化图标/滑块纹理版本源 DLL 与目标 DLL SHA-256：
  `480B6967439BCDA3F25E341490C27792E6D50D8B27CFE53FFF95695AD9ABF6A8`；
- 本轮旧 DLL 已保留为：
  `UnturnedSingleplayerCheatMenu.dll.bak-20260816-134516-ui-procedural-glyph-grip`。
- 用户随后提供的六页真实运行截图已确认上述视觉改动在游戏内正常呈现，视觉验收闭环完成。

### 3.1 真实运行截图证据

以下截图由用户在部署后的真实 Unturned 运行环境中提供，作为本轮视觉验收证据：

| 页面 | 截图 |
|---|---|
| 角色 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-cb950ee5-80f6-443c-a896-c0f045179568.png` |
| 物品 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-b6f4e210-a77c-4aeb-b201-392f0fb4a400.png` |
| 车辆 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-45e6e5b0-8196-40b1-9def-3d4e239aece1.png` |
| 收藏 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-72746227-ed0c-47c9-882b-271689301099.png` |
| 传送 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-1120f2ce-935d-49e0-90e5-de1f1feceb66.png` |
| 其他 | `C:\Users\吹雪\AppData\Local\Temp\codex-clipboard-dc5d5b03-3f94-4c35-bce3-0116f3c74710.png` |

截图可确认六个页面均能正常显示，并覆盖本轮重点视觉项：输入框和传送点行边界、
时间滑块的尺寸与斜向纹理把手、普通按钮与 OFF Toggle 边界、生存状态 Summary 边界、
收藏/关闭图标居中，以及底部状态栏尺寸稳定。截图证据由用户提供；未在截图或用户
反馈中明确证明的日志、Scale、输入捕获和关闭后输入恢复，不在本节宣称为已验证。

## 4. 尚未宣称完成的真实运行项目

以下项目必须在无 BattlEye 的真实 Unturned 单人世界中重新取证：

1. 中文/英文切换后的 Header、Tab、Status、空状态视觉；
2. UI Scale `0.75 / 1.0 / 1.5` 下关键控件不裁切；
3. Hover、Pressed、Disabled、Focus 的实际鼠标/键盘表现；
4. 输入框获得焦点时游戏视角不继续跟随鼠标；
5. Toggle、Slider、传送确认和删除操作的真实交互；
6. 车辆缩略图加载、失败占位和游戏 HUD 共存；
7. 菜单反复切页后没有 UI rebuild loop、异常或明显 GC/卡顿；
8. 关闭菜单后游戏输入恢复，离开单人世界后 Overlay 自动关闭。

## 5. 与提案的偏差与保留项

- 没有引入圆角、渐变、发光、持续动画或第三方控件库；
- 没有改动 `CheatActions`、`AssetCatalog`、`FavoriteStore`、`TeleportStore`、
  单人模式守卫、BattlEye 边界或 Harmony 逻辑；
- 卡片长名称的完整 Hover 详情提示仍未新增；当前保留两行文本区域和
  `Text` 截断行为，避免改变卡片尺寸和网格稳定性；
- 本轮已将用户提供的六页真实运行截图单独记录为视觉验收证据，未把它们扩大解释为
  日志、Scale 或完整交互回归证据。

## 6. 剩余真实验收顺序

1. 在同一真实单人环境切换英文，复核 Header、空状态和 Status；
2. 采集 UI Scale `0.75` 与 `1.5` 的关键页面截图；
3. 依次验证输入焦点、Toggle、Slider、传送确认和删除操作；
4. 检查 `BepInEx/LogOutput.log`，确认没有新增异常或 UI rebuild loop；
5. 验证关闭菜单后的游戏输入恢复，以及离开单人世界后的 Overlay 自动关闭。
