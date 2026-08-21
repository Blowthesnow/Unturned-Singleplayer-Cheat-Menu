# 准星交互工具智能模式落地方案

> 文档状态：源码与纯逻辑验证已落地；部署/实机矩阵仍按证据单独跟踪
>
> 编制日期：2026-08-19
>
> 范围：记录准星交互工具智能融合模式的设计、实现和反向验收；本文件不把源码/Smoke 通过冒充游戏内验收。

## Arbitration clarification (2026-08-19)

The Smart snapshot keeps semantic and coordinate results independent. The
coordinate ray uses `TeleportCoordinateMask`, which intentionally ignores
entities, vehicles, and containers. Therefore a semantic hit is not itself a
coordinate blocker. Smart arbitration is: Shift-delete first, then damaged
repairable target, then usable target interaction, then teleport when a
coordinate candidate exists, and inspect only when a semantic target remains
without an action and without a coordinate candidate.

## Semantic/coordinate failure isolation (2026-08-20)

The failed ground-teleport report was traced to snapshot construction rather
than arbitration. `RefreshSmartRaycast` resolved the semantic hit first, and
`ResolveTarget` called `ResourceManager.tryGetRegion` while walking an ordinary
ground collider. In maps where that region has no resource list, Unturned can
throw from that lookup. The exception happened before the coordinate ray and
left Smart without a coordinate candidate.

The implementation now keeps the two results independent:

- Resource lookup is attempted only for Layer 14 (Resource); ordinary terrain
  on Layer 20 (Ground) remains a world-surface hit with no semantic target.
- Semantic resolution is guarded. A failure records diagnostic data and the
  coordinate ray still runs in the same refresh.
- Every Smart refresh clears the prior target and coordinate fields before
  writing the new frame, preventing stale targets from affecting arbitration.
- `[PointSmartRaycastTrace]` records semantic hit/resolve status, coordinate
  physics hit, terrain fallback, collider/layer, point, and captured frame at
  click time.

This preserves the user-confirmed arbitration order: Shift-delete, damaged
repair, usable-target utility, coordinate teleport, then inspect.

## Ground coordinate fallback (2026-08-20)

Runtime testing showed that the check ray can see a normal Ground/Ground2
surface while the separate coordinate query returns no physics hit. The two
queries therefore remain independent, but a static surface hit from the check
snapshot is now accepted as a coordinate candidate when its layer is also in
the coordinate mask. This fallback is restricted to static surface layers and
cannot turn an entity, vehicle, container, resource, barricade, or other
semantic-only hit into a teleport coordinate. Terrain-height sampling remains
the final fallback.

The Smart trace now records `coordinateSource` as `Physics`,
`SemanticStaticFallback`, `TerrainHeightFallback`, or `None`, so a failed
teleport can be tied to the exact check/coordinate stage rather than inferred
from the HUD text.

## 1. 目标与不可破坏约束

### 1.1 目标

在现有准星交互工具中增加第六种 `Smart`（智能）模式。选择智能模式后：

- 自动开启准星交互工具总开关；
- 检查、维修、传送、实用和删除能力同时可用；
- 每次中键点击只执行一个由上下文决定的动作，避免一次点击产生多项游戏状态修改；
- HUD 实时显示当前将要执行的动作，而不是只显示笼统的“智能”；
- 配置持久化，重启后恢复智能模式。

### 1.2 必须保留

- `Inspect`、`Repair`、`Teleport`、`Utility`、`Delete` 五个独立模式及其按钮全部保留；
- 独立模式的既有射线、动作和安全检查语义保持不变；
- 用户选择任一独立模式时，只退出智能仲裁，不关闭准星工具；
- 删除继续要求 `Shift + 中键`，不得因智能模式降低危险操作门槛；
- 单人模式限制、菜单打开时停用、原生中键回退、距离设置和 HUD 显示项保持有效。

### 1.3 明确不做

- 不把五个动作改成五个可任意组合的持久化布尔开关；
- 不在一次点击中串行执行“维修 + 实用 + 传送”等多个有副作用动作；
- 不删除或重命名现有配置键；
- 不改变独立模式的操作方式来迁就智能模式。

## 2. 现状与可行性结论

### 2.1 当前结构

| 位置 | 当前职责 | 与智能模式的关系 |
|---|---|---|
| `Models/PointToolMode.cs` | 五值互斥枚举 | 增加 `Smart`，保留原五值 |
| `Services/PointToolService.cs` | 30 Hz 瞄准检测、中键触发、HUD、五种动作实现 | 增加目标快照、能力判断和智能动作分派 |
| `Services/PointToolActionGate.cs` | 判断当前单一模式能否执行 | 保留旧入口，新增纯逻辑智能决策入口 |
| `UI/CheatMenuOverlayUi.Tools.cs` | 总开关、五个模式按钮、距离和显示项 | 增加智能按钮并重排模式区 |
| `CheatMenuPlugin.cs` | 配置绑定、恢复和 UI 写回 | 允许 `Mode=Smart`，智能选择时自动启用 |
| `Services/PluginLocalization.cs` | 中英文 UI 和 HUD 文案 | 增加智能模式、动作预览和结果文案 |
| `tests/PointToolActionSmoke` | 独立模式门禁的纯逻辑烟囱测试 | 扩展为智能决策矩阵 |

当前 `Pump()` 每次点击只调用一次 `ExecuteCurrentTarget()`，而后者按 `Mode switch` 进入唯一分支。这说明融合点集中、改动边界可控。现有动作实现 `Repair()`、`Use()`、`TeleportToCurrentCoordinate()`、`Delete()` 可以继续复用，不需要重写游戏 API 调用。

### 2.2 关键技术约束

当前传送模式使用 `TeleportCoordinateMask`，其余模式使用 `BLOCK_STANCE | DAMAGE_CLIENT`。智能模式既要识别语义目标，又要保留其后的安全传送落点，因此不能简单地把 `Mode == Teleport` 改成 `Mode == Teleport || Mode == Smart`。

智能模式应在同一瞄准方向维护两类结果：

1. **语义命中**：用于检查、维修、实用和删除；
2. **坐标命中**：用于没有可执行语义动作时的安全传送。

两次射线只在智能模式下运行，仍受现有 30 Hz 节流控制。点击时消费最近一次完整快照，避免 HUD 预览和实际执行使用不同目标。

### 2.3 结论

方案技术上可行，预计是中等风险的局部扩展。最大风险不是动作 API，而是动作歧义和双射线状态一致性。通过纯逻辑决策器、单动作执行原则、删除修饰键和手动模式兜底，可以把风险限制在准星工具模块内。

## 3. 用户可见行为

### 3.1 模式区布局

交互页改为两层：

- 第一层：宽按钮 `智能模式（全部功能）`；
- 第二层：保留 `检查 / 维修 / 传送 / 实用 / 删除` 五个独立模式按钮。

选中智能模式时只高亮智能按钮；五个独立按钮保持可点击，但不伪装成五个同时选中的开关。按钮附近显示当前状态：`全部功能已启用，系统会按目标自动选择单一动作`。

点击智能模式按钮时：

1. 设置 `Mode = Smart`；
2. 若总开关关闭，则设置 `Enabled = true`；
3. 持久化 `PointTool.Mode=Smart` 和 `PointTool.Enabled=true`；
4. 刷新交互页并显示“智能模式已开启，全部准星功能可用”。

点击任一独立模式按钮时：

1. 设置对应旧模式；
2. 保持 `Enabled` 当前值；
3. 不修改其他独立模式的实现；
4. 显示“已切换为独立的……模式”。

总开关关闭时所有模式停止工作，但保留最后选择；再次打开后恢复最后选择。若最后选择为 `Smart`，即恢复全部功能；若最后选择为旧模式，仍按旧模式运行。

### 3.2 智能动作决策表

现有世界落点传送不是读取 HUD 文本再解析坐标，而是直接复用准星检查阶段产生的 `_currentPoint`：HUD 展示该值，点击时将其取出 `x/z` 交给 `TeleportToMapPosition`，再由地图传送逻辑计算安全 `y`。因此智能模式必须保留“检查坐标候选”，不能把传送仅定义成“没有语义目标时的最后回退”。

每次中键只选出一个 `PointToolAction`。坐标候选始终与当前检查射线同步保存；是否执行语义动作，取决于目标类型和明确的智能策略，而不是重新从 HUD 字符串反解析坐标：

| 优先级 | 条件 | 动作 | 说明 |
|---:|---|---|---|
| 1 | `Shift` 按住，且语义目标可删除 | `Delete` | 保留显式危险操作手势；不回退到其他动作 |
| 2 | `Shift` 按住，但目标不可删除 | `None` | 显示“当前目标不可删除”，防止误触发传送或实用动作 |
| 3 | 智能策略判定当前命中为“目标动作场景”，且语义目标可维修、当前确实受损 | `Repair` | 车辆、路障、建筑等受损对象执行维修；不消费坐标传送候选 |
| 4 | 语义目标支持实用交互 | `Utility` | 沿用既有 `Use()`；载具/容器等目标优先交互，避免把目标后方地面当成传送落点 |
| 5 | 没有可执行实用交互，但存在传送坐标候选 | `Teleport` | 坐标射线仍忽略实体、载具和容器；仅在没有可用语义动作时消费候选 |
| 6 | 语义目标可维修但已满状态，且智能策略未选择世界落点传送 | `Inspect` | 只提示“状态已满”，不执行无意义维修 |
| 7 | 有世界/语义命中但没有可执行动作，且不满足世界落点传送条件 | `Inspect` | 显示详情和“不支持自动操作” |
| 8 | 完全未命中 | `None` | 显示“未命中世界表面” |

歧义处理必须先区分“目标动作”与“世界落点”两种意图，不能只依据“是否存在语义目标”粗暴拦截传送。建议第一版采用以下明确规则：

- 受损且可维修的语义目标优先维修；
- 语义射线命中可用载具、容器或其他交互目标时，先执行既有实用交互；
- 只有没有可执行语义动作时，才消费传送坐标候选，即使该候选位于实体、载具或容器后方；
- 没有坐标候选且没有可执行语义动作时，保留检查或无动作结果。

这样，智能模式不会把传送射线在目标后方找到的地面误认为是对车辆或容器的首选动作：受损目标先维修，可用目标先执行原生实用交互，只有没有可执行语义动作时才消费传送坐标候选。需要强制维修或实用交互时继续使用对应的独立模式。

### 3.3 HUD 预览

智能模式 HUD 必须显示决策结果，例如：

- `[智能 -> 维修]`；
- `[智能 -> 实用]`；
- `[智能 -> 准星传送]`；
- `[智能 -> Shift + 中键删除]`；
- `[智能 -> 仅检查：状态已满]`；
- `[智能 -> 无可用动作]`。

HUD 预览与点击执行必须调用同一个纯决策函数。按下或松开 `Shift` 时，下一帧预览应同步变化。

## 4. 技术设计

### 4.1 数据模型

在 `PointToolMode` 末尾增加 `Smart`，不要插入到中间，避免任何潜在的枚举整数序列兼容问题。

新增内部动作枚举，动作与用户选择的模式分离：

```csharp
internal enum PointToolAction
{
    None,
    Inspect,
    Repair,
    Teleport,
    Utility,
    Delete
}
```

新增不依赖 Unity 类型的决策输入，便于烟囱测试：

```csharp
internal readonly record struct PointToolDecisionInput(
    bool HasCoordinateHit,
    bool HasSemanticTarget,
    bool CanRepair,
    bool NeedsRepair,
    bool CanUse,
    bool CanDelete,
    bool DeleteModifierHeld);
```

项目目标框架若不适合 `record struct`，使用只读 `struct`，不要为此提高语言或框架要求。

### 4.2 目标能力

在 `PointToolService` 内集中计算能力，不把 Unity/Unturned 类型判断散落到 UI 或门禁类：

- `CanRepair(Target)`：车辆、路障、建筑及可解析到其父对象的目标；
- `NeedsRepair(Target)`：比较当前生命/耐久与最大值，必须沿用 `GetDetails()` 已使用的服务端数据来源；
- `CanUse(Target)`：与现有 `Use()` 支持类型保持同一来源；
- `CanDelete(Target)`：与现有 `Delete()` 支持类型保持同一来源；
- `DescribeCapabilities(Target)`：只用于日志/诊断，不作为决策的第二套真相来源。

建议把类型支持抽成 `GetCapabilities(Target)` 返回标志位，并让 `Repair()`、`Use()`、`Delete()` 的入口断言与该标志保持一致，避免未来新增目标类型时决策器和执行器漂移。

### 4.3 双射线快照

新增内部 `PointToolTargetSnapshot`，至少包含：

- `SemanticHit`、`SemanticTarget`；
- `CoordinateHit`、`CoordinatePoint`；
- `RayOrigin`、`RayDirection`；
- `CapturedFrame` 或 `CapturedAt`；
- 由当前语义目标计算出的能力和受损状态。

执行规则：

- 手动 `Teleport` 继续走当前传送射线路径；
- 其他五个手动模式继续走当前语义射线路径；
- 仅 `Smart` 在一次刷新中获取“检查语义结果 + 检查坐标候选”；坐标候选必须沿用检查 HUD 使用的同一射线命中点，不能另起一套与 HUD 不一致的坐标来源；
- `Smart` 不从 HUD 文本解析坐标，HUD 和传送都读取同一个结构化快照字段；
- 若两个结果来自不同帧或瞄准方向，丢弃旧快照并重新检测；
- 智能执行完成后刷新一次快照，避免 HUD 暂时显示修改前状态。

不要用“先语义射线失败才做坐标射线”的短路策略。智能模式必须始终保留检查坐标候选，并按仲裁顺序决定是否消费它：受损且可维修目标先维修；其次是可用目标的实用交互；只有没有可执行语义动作时，才消费传送射线候选，即使语义射线命中传送射线明确忽略的实体、载具或容器；最后回退到检查或无动作。

### 4.4 决策与执行

在 `PointToolActionGate` 中保留现有 `CanExecute(PointToolMode, ...)` 和 `GetFailure(...)`，保证旧测试与旧模式不变。新增：

```csharp
internal static PointToolAction DecideSmart(PointToolDecisionInput input)
```

`PointToolService.ExecuteCurrentTarget()` 分为两条路径：

- `Mode != Smart`：保留当前 `Mode switch`；
- `Mode == Smart`：构造输入、调用 `DecideSmart()`，再由统一的 `ExecuteAction(PointToolAction)` 执行一个动作。

`ExecuteAction` 复用现有方法，不复制动作实现。所有结果消息应包含最终动作，日志记录 `mode=Smart, action=..., target=..., reason=...`，便于实机排错。

### 4.5 配置与迁移

沿用现有键：

```ini
[PointTool]
Enabled = true
Mode = Smart
```

不新增 `SmartEnabled` 或五个 `FeatureEnabled` 键。`Mode` 是唯一模式真相，避免“Smart=false 但 Mode=Smart”之类非法组合。

迁移策略：

- 已存在的 `Inspect/Repair/Teleport/Utility/Delete` 配置原样读取，不自动改写；
- `Enum.TryParse` 自然接受新增的 `Smart`；
- 新安装或缺失 `Mode` 时，默认值由 `Inspect` 改为 `Smart`；
- 非法字符串继续回退到安全模式，并写一次警告日志；建议回退 `Smart`，但不得覆盖用户配置文件，等用户在 UI 中明确选择后再持久化；
- 通过 UI 选择智能模式时，同时持久化 `Enabled=true`。

这保证老用户的明确选择不被升级覆盖，同时让新用户首次开启工具即可获得融合体验。

## 5. 文件级实施清单

| 文件 | 变更 | 完成标准 | 状态 |
|---|---|---|---|
| `Models/PointToolMode.cs` | 在末尾增加 `Smart` | 原五值仍存在，解析兼容 | 已实现 |
| `Models/PointToolAction.cs` | 新增内部动作枚举 | 不引用 Unity 类型 | 已实现 |
| `Models/PointToolDecisionInput.cs` | 新增纯决策输入 | 烟囱项目可直接编译 | 已实现 |
| `Services/PointToolActionGate.cs` | 保留旧门禁，新增 `DecideSmart` 和决策原因 | 决策表全部可单测 | 已实现 |
| `Services/PointToolService.cs` | 双射线快照、能力识别、HUD 预览、单动作执行 | 手动路径无行为回归，智能路径不多重执行 | 已实现，构建/部署已验证；实机待验 |
| `CheatMenuPlugin.cs` | 默认/解析/持久化与智能自动启用 | 老配置保留，新配置默认 Smart | 已实现 |
| `UI/CheatMenuOverlayUi.Tools.cs` | 智能按钮、旧模式区、状态说明 | 六种选择均可见可操作 | 已实现，待实机 |
| `Services/PluginLocalization.cs` | 中英双语新增文案 | 中文/英文无漏翻或原始键泄漏 | 已实现 |
| `tests/PointToolActionSmoke/Program.cs` | 决策矩阵与旧门禁回归 | 所有组合断言通过 | 已实现，本轮通过 |
| `tests/PointToolActionSmoke/*.csproj` | 链接新增纯模型文件 | 独立 `net8.0` 烟囱可编译 | 已实现 |
| `README.md` / `README.en.md` | 记录智能与手动模式操作 | 两种语言语义一致 | 已实现 |
| `ACCEPTANCE.md` | 增加智能模式证据边界 | 静态、构建、游戏内证据分栏 | 已实现，实机待验 |

## 6. 测试与验收

### 6.1 纯逻辑烟囱测试

至少覆盖：

1. `Shift + 可删除目标 -> Delete`；
2. `Shift + 不可删除目标 -> None`，且不能回退传送；
3. `坐标候选 + 受损且可维修语义目标 -> Repair`；
4. `可维修 + 受损 + 无坐标候选 -> Repair`；
5. `健康载具 + 坐标候选 -> Utility`；
6. `储物/实体 + 坐标候选 -> Utility`；
7. `可用目标 + 无坐标候选 -> Utility`；
8. `地形/世界表面 + 检查坐标命中 -> Teleport`；
9. `实体碰撞体 + 受损且可维修 + 无坐标候选 -> Repair`；
10. `实体碰撞体 + 健康且可实用 + 坐标候选 -> Utility`；
11. `实体碰撞体 + 无可执行动作 + 无坐标候选 -> Inspect`；
12. `完全未命中 -> None`；
13. 旧五模式全部既有断言继续通过；
14. 每个输入只产生一个动作值；
15. HUD 显示的坐标与最终 `TeleportToMapPosition` 请求中的 `_currentPoint.x/z` 完全一致。

### 6.2 源码与构建验证

- `dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release --nologo`；
- 递归执行 `tests` 下全部 Smoke 项目，而不只运行 `PointToolActionSmoke`；
- `git diff --check`；
- 检查新增字符串的中英文映射；
- 检查配置默认值、旧值解析和非法值回退；
- 检查智能模式以外的 `RefreshRaycast` 与旧动作路径没有非必要改写。

### 6.3 游戏内功能矩阵

| 场景 | 预览 | 点击结果 | 必须验证 |
|---|---|---|---|
| 受损载具 | 智能 -> 维修 | 只维修，不上车 | 生命恢复、位置不变 |
| 健康载具且坐标射线有落点 | 智能 -> 实用 | 调用载具原生使用 | 不传送到车辆后方 |
| 受损建筑/路障 | 智能 -> 维修 | 只维修 | 耐久恢复 |
| 健康建筑，且坐标射线命中结构表面 | 智能 -> 准星传送 | 穿过语义目标取坐标落点 | 不进入/使用目标；按坐标射线落点传送 |
| 健康路障/容器，且坐标射线有后方落点 | 智能 -> 实用 | 调用目标原生交互；坐标射线候选不抢占交互 | 容器可远程打开；不传送到目标后方 |
| 健康建筑/路障/容器，但坐标射线无候选 | 智能 -> 仅检查或实用 | 仅在目标支持实用交互时执行实用 | 不凭语义命中制造传送落点 |
| 可用目标且坐标射线无落点 | 智能 -> 实用 | 对应原生交互 | 每次只触发一次 |
| 地面或屋顶 | 智能 -> 准星传送 | 到安全落点 | 坐标与 HUD/点击快照一致 |
| 可删除目标 + Shift | 智能 -> 删除 | 删除一次 | 无 Shift 时绝不删除 |
| 不可删除目标 + Shift | 智能 -> 无可用动作 | 不执行其他动作 | 不误传送、不误交互 |
| 菜单打开 | HUD/动作停用 | 中键不作用于世界 | 与现状一致 |
| 离开单人世界 | 工具自动关闭 | 无动作 | 与现状一致 |

### 6.4 UI、配置与回归矩阵

- 中文、英文各检查一次按钮、提示、HUD 和状态消息；
- `Scale 0.75 / 1.0 / 1.5` 下六种模式不裁切、不重叠；
- 智能按钮选中后总开关自动开启；
- 切到五个旧模式中的任意一个，不关闭总开关；
- 关闭/重开总开关保留最后模式；
- 重启游戏后恢复 `Enabled` 和 `Mode`；
- 用旧配置逐项启动，确认不会被升级为 Smart；
- 用缺失 `Mode` 的新配置启动，确认默认 Smart；
- 原生中键回退与 Unity 中键同帧/邻帧去重仍只执行一次。

### 6.5 完成定义

只有同时满足以下条件，才能称为“完整落地”：

- 文件级清单全部完成；
- 解决方案构建和全部 Smoke 项目通过；
- 源 DLL 与部署 DLL 的 SHA-256、程序集版本一致；
- 游戏日志确认新 DLL 被加载；
- 智能模式游戏内功能矩阵通过；
- 五个独立模式逐一回归通过；
- 中文/英文、缩放、配置重启恢复通过；
- `ACCEPTANCE.md` 按实际证据更新，不把源码或 Smoke 通过写成游戏内验收通过。

## 7. 风险与控制

| 风险 | 后果 | 控制措施 |
|---|---|---|
| 载具同时可维修和实用 | 用户动作不符合预期 | 受损先维修；健康时实用交互优先于坐标传送，强制传送使用手动模式 |
| 双射线命中不同对象 | HUD 与执行错位 | 同帧快照、统一射线方向、点击消费同一快照 |
| 语义射线命中传送射线忽略的对象 | 车辆/容器被误传送到后方 | 两种命中独立保存；Utility 优先于坐标候选，不使用语义命中制造传送坐标 |
| 智能删除误触 | 不可逆状态修改 | Shift 强制门禁；Shift 失败时禁止回退其他动作 |
| 能力判断与动作实现漂移 | HUD 说可执行但执行失败 | 能力来源集中化，决策与执行共享标志 |
| 每帧额外射线增加开销 | 帧时间波动 | 仅 Smart 双射线，维持 30 Hz，实机记录开销 |
| 老配置被静默覆盖 | 用户习惯改变 | 保留已存在模式，只改变新/缺失配置默认值 |
| 六按钮一行过窄 | 中英文或小缩放裁切 | 智能独占一行，五个手动模式另起一行并做缩放矩阵 |

## 8. 建议实施顺序

1. 先扩展纯模型和 `PointToolActionGate`，写全决策烟囱测试；
2. 引入目标能力和同帧快照，保持旧模式路径不变；
3. 接入 Smart 单动作执行与 HUD 预览；
4. 接入配置、UI 和本地化；
5. 更新 README 与验收台账；
6. 构建并递归执行全部 Smoke 项目；
7. 部署 DLL，核对哈希与程序集版本；
8. 按游戏内矩阵验证 Smart 与五个手动模式；
9. 反向对照本方案逐项回审，未验证项明确保留为未完成。

## 9. 需求反向审查

| 原始要求 | 方案对应 | 结论 |
|---|---|---|
| 增加智能模式 | 新增 `PointToolMode.Smart` 和独立智能按钮 | 已实现，待实机 |
| 所有功能融合在一起 | 同一模式下检查常驻，维修/实用/传送/删除按上下文仲裁 | 已实现，待实机 |
| 开启智能模式自动打开所有功能 | 选择 Smart 时自动设置并持久化 `Enabled=true`；无需五个易漂移开关 | 已实现，待实机 |
| 不能只能使用对应功能区 | Smart 不再要求用户预先选择单一动作 | 已实现，待实机 |
| 当前独立功能切换不能删除 | 五个旧枚举、按钮、执行路径和测试全部保留 | 已实现，待回归实机 |

本方案把“全部功能同时可用”定义为“全部能力进入同一个智能决策上下文”，而不是“一次点击同时执行全部副作用”。这是在满足融合体验的同时，避免误删、误传送和多重交互的必要安全边界。
