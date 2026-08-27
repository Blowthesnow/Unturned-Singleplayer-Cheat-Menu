# 生物头顶血条完整落地方案

> 文档性质：功能落地方案（本轮仅整理文档，不修改生产代码）  
> 更新日期：2026-08-25  
> 目标项目：`D:\Unturned BepInEx`  
> 目标游戏：`I:\Steam\steamapps\common\Unturned\`  
> 目标运行时：BepInEx 5、Unity Mono、当前游戏 `Assembly-CSharp.dll`

## 一、目标与边界

为插件增加僵尸和动物的头顶生命条：

- 平时隐藏；
- 准心对准生物时显示；
- 生物实际受伤后显示 10 秒；
- 10 秒内再次受伤时重新计时；
- 实时刷新当前生命和生命上限；
- 只显示数字 `当前生命 / 生命上限`，不显示百分比；
- 根据每个模型的实际高度自动把血条放在头顶；
- 只影响客户端显示，不修改伤害、生命、网络同步或原生生物行为。

本方案不包含：

- 玩家、载具、建筑、资源的血条；
- 通过 Workshop 资产替换原生生物 Prefab；
- 自定义伤害、自动瞄准或透视；
- 服务器广播血条状态；
- 在多人服务器中绕过权限或反作弊限制。

首版以当前插件已经支持的单人世界为目标。多人模式若未来需要支持，必须重新审查客户端可见性、服务器权威和反作弊边界，不能直接复用单人实现的完成结论。

## 二、可行性结论

### 2.1 已确认可用的运行时能力

当前目标程序集与仓库引用文件相同：

```text
I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll
D:\Unturned BepInEx\lib\Assembly-CSharp.dll
SHA-256: E1146353E5C9BFF901EE94829640D88919C5E89F6A6B90B22C73ABF5C1608F94
```

对该程序集的元数据检查确认：

| 能力 | 当前 API | 结论 |
|---|---|---|
| 僵尸当前生命 | `Zombie.GetHealth()` | 可直接读取 |
| 僵尸生命上限 | `Zombie.GetMaxHealth()` | 必须使用运行时值，不能写死 |
| 动物当前生命 | `Animal.GetHealth()` | 可直接读取 |
| 动物生命上限 | `Animal.asset.health` | 可从运行时资产读取 |
| 僵尸受伤回调 | `DamageTool.zombieDamaged` | 公开静态委托，可订阅 |
| 动物受伤回调 | `DamageTool.animalDamaged` | 公开静态委托，可订阅 |
| 僵尸实例集合 | `ZombieManager.AllZombies` | 可作为候选/清理来源，不建议每帧全量扫描 |
| 动物实例集合 | `AnimalManager.animals` | 可作为候选/清理来源，不建议每帧全量扫描 |
| 屏幕中心射线 | `Camera.ViewportPointToRay(0.5, 0.5)` | 现有 PointTool 已使用 |
| 生物目标解析 | `Zombie` / `Animal` 父级组件解析 | 现有 PointTool 已使用 |
| UI | Unity uGUI `Canvas` / `Image` / `Text` | 现有 PointTool HUD 已使用 |

### 2.2 官方文档边界

官方 stable 文档确认：

- Animal Asset 的 `Health` 是动物总生命值；
- Animal Asset 的 `Regen` 是动物生命恢复间隔；
- Zombie Difficulty Asset 可以按特殊类型覆盖僵尸生命；
- Mod Hooks 没有通用的“生物生命变化事件”或“生物头顶 UI”钩子；
- Glazier 说明 uGUI 是当前推荐 UI 系统，插件可以使用动态 uGUI。

因此，本功能应采用“运行时程序集读取 + BepInEx 事件订阅 + 动态 uGUI”的组合，而不是尝试用 `.dat` 配置或 Mod Hook 单独完成。

官方参考：

- [Animal Assets](https://docs.smartlydressedgames.com/en/stable/assets/animal-asset.html)
- [Zombie Difficulty Assets](https://docs.smartlydressedgames.com/en/stable/assets/zombie-difficulty-asset.html)
- [Mod Hooks](https://docs.smartlydressedgames.com/en/stable/assets/mod-hooks.html)
- [Glazier](https://docs.smartlydressedgames.com/en/stable/servers/glazier.html)

## 三、现有代码基础

当前源码已经具备可复用的入口，但血条不应直接塞进 PointTool 文本 HUD：

| 现有位置 | 已有能力 | 方案用途 |
|---|---|---|
| `Services/PointToolService.cs` | 每约 1/30 秒刷新中心射线 | 复用射线语义，新增只读生物目标采样 |
| `PointToolService.ResolveTarget` | 从 Collider 找到 `Zombie` / `Animal` | 复用生物类型识别规则 |
| `PointToolService.BuildHudText` | 已能输出僵尸 `生命 当前/上限` | 作为数值读取和格式化基线 |
| `UI/PointToolHud.cs` | Screen Space Overlay、CanvasScaler、字体创建、销毁 | 复用 Canvas 和字体创建模式，不复用单个右上角文本布局 |
| `CheatMenuPlugin.RunUpdateCallback` | 每帧去重后的统一更新入口 | 接入血条服务 Pump |
| `CheatMenuPlugin.GameUpdatePostfix` | 游戏更新 Harmony 回调兜底 | 保留现有更新链路，不新增第二个 MonoBehaviour 更新入口 |

现有代码证据：

- [PointToolService.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/Services/PointToolService.cs:140>) 的 `Pump` 负责每帧状态和约 30 Hz 射线；
- [PointToolService.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/Services/PointToolService.cs:192>) 的 `RefreshRaycast` 从屏幕中心创建射线；
- [PointToolService.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/Services/PointToolService.cs:451>) 的 `ResolveTarget` 已解析 `Zombie` 和 `Animal`；
- [PointToolService.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/Services/PointToolService.cs:542>) 的 `BuildHudText` 已读取生物生命；
- [CheatMenuPlugin.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs:674>) 的 `RunUpdateCallback` 是统一更新入口；
- [PointToolHud.cs](<D:/Unturned BepInEx/src/UnturnedSingleplayerCheatMenu/UI/PointToolHud.cs:99>) 的 `EnsureCreated` 已处理 UI 就绪、字体、Canvas 和重试。

## 四、最终用户行为

### 4.1 显示状态

| 条件 | 血条状态 |
|---|---|
| 无有效生物、未进入单人世界、插件关闭 | 隐藏 |
| 准心射线命中存活僵尸或动物 | 显示该目标 |
| 生物实际生命下降，且距离上次确认受伤不足 10 秒 | 显示该生物 |
| 准心移开，但受伤计时仍未到期 | 继续显示 |
| 生命恢复但没有新的伤害 | 继续显示至计时结束，数字实时变化 |
| 生命回满且计时结束、未被准心指向 | 隐藏 |
| 生物死亡、销毁、离开场景或实例被替换 | 立即移除 |
| 生物在相机后方、屏幕外、距离超过配置值 | 不显示 |
| 生物被墙或建筑遮挡 | 不显示 |

准心显示优先级高于受伤计时：只要准心仍命中目标，即使受伤计时已结束，也继续显示；准心移开后再按受伤计时决定是否隐藏。

### 4.2 显示内容

血条由三部分组成：

```text
┌────────────────┐
│      75 / 100  │
│ ███████░░░░░░░  │
└────────────────┘
```

- 数字格式：`{current:0} / {max:0}`；
- 当前生命和上限都按运行时值取整显示；
- 当前生命小于 0 时按 0 显示；
- 当前生命大于上限时按上限显示；
- 上限小于等于 0 时不显示该条并记录一次诊断；
- 不显示 `75%`、伤害数字、名称或 ID，避免与 PointTool 文本 HUD 重复。

## 五、推荐架构

### 5.1 新增模块

推荐新增以下文件：

| 文件 | 职责 |
|---|---|
| `Services/CreatureHealthBarService.cs` | 事件订阅、候选受伤确认、准心目标、显示状态、生命刷新、对象生命周期 |
| `UI/CreatureHealthBarHud.cs` | Canvas、对象池、单条 UI 的创建/更新/隐藏、屏幕投影和层级 |
| `Models/CreatureHealthBarState.cs` | 生物类型、实例引用、当前/上限生命、受伤截止时间、头顶锚点缓存 |
| `Services/CreatureHealthBarSettings.cs` | 默认值、范围钳制和配置键常量；若项目已有统一 Settings 目录，可放入对应目录 |
| `tests/CreatureHealthBarSmoke/Program.cs` | 纯逻辑状态机、格式化、候选确认、排序和配置测试 |

不建议把新逻辑直接写入 `PointToolService.cs` 或 `PointToolHud.cs`。PointTool 是交互工具，血条是独立的被动视觉服务；两者只共享目标解析和更新时机，避免互相关闭、互相清理或争用文本面板。

### 5.2 运行时关系

```text
CheatMenuPlugin.RunUpdateCallback
        │
        ├─ PointTool.Pump()
        └─ CreatureHealthBarService.Pump()
              │
              ├─ ConsumeDamageCandidates()
              ├─ RefreshCrosshairTarget() 约 30 Hz
              ├─ ConfirmPendingDamage() 下一帧读取生命
              ├─ RefreshTrackedHealth() 约 20 Hz
              ├─ SelectVisibleTargets()
              └─ CreatureHealthBarHud.Render()

DamageTool.zombieDamaged / animalDamaged
        │
        └─ RecordDamageCandidate() 仅记录，不改变 shouldAllow
```

事件回调只做轻量记录，不在回调内创建 GameObject、扫描 Renderer 或修改伤害参数。所有 UI 和 Unity 对象操作统一回到主更新循环。

## 六、伤害检测与 10 秒计时

### 6.1 为什么不能直接在伤害回调里显示

当前程序集中的伤害回调签名包含 `ref bool` 允许标志，且调用发生在最终生命扣除前。插件若收到回调就立即开始计时，可能把以下情况误判为受伤：

- 伤害被其他处理器阻止；
- 伤害值被改为 0；
- 目标已经死亡或无效；
- 伤害只触发了请求但没有成功应用。

因此必须采用“两阶段确认”：

1. 回调阶段记录目标实例、事件时间和事件前生命快照；
2. 下一次 `Pump` 读取实际生命；
3. 只有 `currentHealth < beforeHealth` 才设置 `showUntil = Time.unscaledTime + 10f`。

### 6.2 候选记录

建议字段：

```text
CreatureHealthBarState
  CreatureKind Kind              // Zombie / Animal
  Component Creature             // Zombie 或 Animal
  int InstanceId                 // UnityEngine.Object.GetInstanceID()
  float LastKnownHealth
  float MaxHealth
  float ShowUntil
  float CandidateBeforeHealth
  float CandidateAt
  Bounds WorldBounds
  bool BoundsValid
  bool IsCrosshairTarget
```

候选字典以 `InstanceId` 为键。实例被销毁后，`UnityEngine.Object` 的 Unity null 语义必须单独处理，不能只依赖 CLR 引用不为 null。

### 6.3 计时语义

- 使用 `Time.unscaledTime`，菜单、暂停和帧率变化不会延长或缩短 10 秒；
- 每次确认实际掉血都覆盖 `ShowUntil`，不累加多个 10 秒；
- 同一帧多次伤害合并为一次状态更新；
- 死亡时立即隐藏，不等待剩余计时；
- 受伤后离开最大显示距离，隐藏但保留 `ShowUntil`；回到范围内且计时未结束时可重新显示；
- 若目标回血，更新数字但不因回血自动隐藏。

## 七、准心目标与遮挡

### 7.1 准心采样

复用 PointTool 当前的射线语义：

- 首选 `MainCamera.instance`，回退 `Camera.main`；
- 使用 `ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))`；
- 使用 `RayMasks.BLOCK_STANCE | RayMasks.DAMAGE_CLIENT`；
- `QueryTriggerInteraction.Ignore`；
- 距离由新配置 `CrosshairRange` 限制，默认 100 米，钳制 5–250 米；
- 从命中 Collider 向父级查找 `Zombie` 或 `Animal`。

准心命中只决定“当前目标强制显示”，不直接把目标加入受伤列表，也不改变 PointTool 当前目标。

### 7.2 遮挡策略

显示前做一次可见性检查：

1. 从相机位置向头顶锚点发 `Physics.Raycast`；
2. 使用阻挡层，不使用生物自身层作为阻挡；
3. 若首个阻挡物不是该生物层级的 Collider，则隐藏血条；
4. 对准心目标可以使用已有命中结果减少一次射线，但必须确认命中点和头顶锚点之间没有第二个阻挡物。

遮挡检测不在每帧对所有受伤生物执行，建议 10 Hz 更新；血条位置可以在每帧投影。

## 八、不同模型高度的自动适配

### 8.1 锚点计算优先级

```text
有效 SkinnedMeshRenderer / MeshRenderer 的世界 Bounds.max.y
        ↓ 失败
有效 Collider 的世界 Bounds.max.y
        ↓ 失败
Creature.transform.position.y + 回退高度
```

血条锚点为：

```text
anchor = (creatureRoot.position.x,
          highestBoundsY + Padding,
          creatureRoot.position.z)
```

其中 `Padding` 默认 0.15 米，并按生物包围盒高度做有限比例调整，避免巨型僵尸贴在头部、矮小动物浮得过高。

### 8.2 Renderer 过滤规则

首次显示或模型实例变化时扫描 `GetComponentsInChildren<Renderer>(includeInactive: true)`：

- 接受 `SkinnedMeshRenderer` 和 `MeshRenderer`；
- 排除 `ParticleSystemRenderer`、Trail、Line 等特效 Renderer；
- 排除明显是武器、掉落物或附着特效的子节点；
- 只保留与生物根节点同一实例层级的 Renderer；
- 缓存 Renderer 引用和最后的 Transform 层级计数；
- 模型重建、死亡、销毁或 Renderer 失效时重新扫描。

不使用固定的“僵尸高度 2 米”或“动物高度 1 米”常量作为主算法。固定回退高度只用于模型暂时没有可读 Renderer 的异常情况。

### 8.3 屏幕投影

- 使用 `Camera.WorldToScreenPoint(anchor)`；
- `z <= 0` 时隐藏；
- 屏幕坐标超出边界时隐藏；
- 将世界坐标转换到 Screen Space Overlay Canvas 的像素坐标；
- 位置更新放在 `LateUpdate` 语义阶段，减少生物动画和相机移动造成的抖动；
- 不把血条挂在生物 Transform 下，避免世界空间 Canvas 缩放和模型骨骼旋转造成尺寸变化。

## 九、UI 实现细节

### 9.1 Canvas

复用 `PointToolHud` 的动态 uGUI 创建模式，但使用独立根节点：

- `RenderMode.ScreenSpaceOverlay`；
- `CanvasScaler.ScaleWithScreenSize`；
- 参考分辨率 `1920 x 1080`；
- `overrideSorting = true`；
- 建议排序 `29940`，低于现有 PointTool HUD `29950`；
- 根节点 `DontDestroyOnLoad`，世界切换时由服务主动清理。

### 9.2 单条血条层级

```text
CreatureHealthBarItem
  ├─ Background (Image)
  ├─ Fill (Image, Filled / Horizontal)
  └─ Label (Text)
```

建议初始尺寸：

- 宽度 120 px；
- 血条高度 8 px；
- 数字行高度 18 px；
- 数字字号 13；
- 生命颜色：红色；
- 背景颜色：半透明黑；
- 文字颜色：白色并加轻微阴影或描边；
- 所有 `Graphic.raycastTarget = false`。

尺寸必须固定，不随数字位数、语言或目标生命上限改变布局。数字行允许 `9999 / 9999`，不允许撑大血条。

### 9.3 对象池与上限

- 预分配 12 条 UI，按需扩展至硬上限 24 条；
- 超出上限时只保留准心目标和距离最近的受伤目标；
- 隐藏通过 `SetActive(false)`，不销毁重建；
- UI 文本仅在生命数值或目标实例改变时更新；
- 血量填充仅在比例变化超过 0.001 或目标变化时更新。

## 十、配置设计

建议配置：

```ini
[CreatureHealthBar]
Enabled=false
DamageDisplayDuration=10
CrosshairRange=100
DisplayRange=100
MaxVisibleBars=12
ShowAnimals=true
ShowZombies=true
OcclusionCheckHz=10
HealthRefreshHz=20
Diagnostics=false
```

规范：

- `DamageDisplayDuration` 钳制 1–30 秒，默认 10；
- `CrosshairRange` 和 `DisplayRange` 钳制 5–250 米；
- `MaxVisibleBars` 钳制 1–24；
- `OcclusionCheckHz` 钳制 5–20；
- `HealthRefreshHz` 钳制 10–30；
- 默认 `Enabled=false`，避免升级后突然改变现有 HUD；
- `ShowAnimals`、`ShowZombies` 只控制显示，不影响伤害事件和目标解析；
- 设置页面只提供一个总开关，其他高级项可先留在配置文件，避免工具页膨胀。

如果产品决定默认开启，只改变默认值和文案，并保留旧配置文件中的显式值，不覆盖用户已有设置。

## 十一、生命周期与安全清理

### 11.1 初始化

在插件初始化时：

1. 创建 `CreatureHealthBarService`；
2. 创建 `CreatureHealthBarHud`；
3. 订阅 `DamageTool.zombieDamaged` 和 `DamageTool.animalDamaged`；
4. 不立即创建 Canvas，等相机、PlayerUI 和本地玩家就绪后懒创建；
5. 记录一次初始化日志，包含配置和目标程序集版本。

### 11.2 每帧更新

在 `RunUpdateCallback` 中接入：

```csharp
CreatureHealthBars?.Pump();
```

必须保持当前 `_lastRuntimeUpdateFrame` 去重语义，不能因 `Update` 和 Harmony Postfix 双入口导致一帧执行两次。

### 11.3 暂停条件

以下任一条件成立时隐藏全部血条，但不必清除有效伤害状态：

- `SingleplayerGuard.IsReady` 不成立；
- 本地玩家不存在；
- 相机不存在；
- 场景/地图正在切换；
- 插件设置关闭；
- 菜单打开且项目决定让菜单遮挡游戏 HUD。

退出世界或插件销毁时必须：

- 退订两个静态 DamageTool 委托；
- 清空候选和跟踪字典；
- 隐藏并销毁血条 Canvas；
- 清空 Renderer 缓存；
- 防止旧 Unity 对象在下一帧被重新激活。

## 十二、实现步骤与改动账本

### 阶段 A：逻辑垂直切片

| 状态 | 文件/任务 | 结果 |
|---|---|---|
| 已实现 | `CreatureHealthBarState.cs` | 生物类型、实例 ID、生命快照、计时、排序和显示状态 |
| 已实现 | `CreatureHealthBarService.cs` | 订阅伤害、下一帧确认实际掉血、准心目标和清理 |
| 已实现 | `CreatureHealthBarSettings.cs` | 配置默认值、有限值回退和范围钳制 |
| 已实现 | `CheatMenuPlugin.cs` | 创建服务、接入 `RunUpdateCallback`、销毁退订 |
| 已通过 | `CreatureHealthBarSmoke` | 状态机、格式化、排序、范围和配置边界验证 |

阶段 A 不创建复杂 UI，先证明 10 秒计时、生命变化和实例清理正确。

### 阶段 B：单条 UI 与投影

| 状态 | 文件/任务 | 结果 |
|---|---|---|
| 已实现 | `CreatureHealthBarHud.cs` | Canvas、单条 UI、对象池、文本和填充差异更新 |
| 已实现 | `CreatureHealthBarService` | `WorldToScreenPoint`、屏幕边界、10 Hz 遮挡 |
| 已通过 | `CreatureHealthBarSmoke` | 目标排序、准心优先、范围和隐藏条件 |

阶段 B 先只显示准心目标，确认字体、排序、分辨率和遮挡，再打开受伤列表。

### 阶段 C：模型高度适配

| 状态 | 文件/任务 | 结果 |
|---|---|---|
| 已实现 | `CreatureBoundsResolver`（内部类） | Renderer → Collider → 根节点回退链 |
| 已实现 | Renderer 过滤 | 仅接受 Mesh/SkinnedMesh，排除附着物，缓存并低频重扫 |
| 已实现 | 高度诊断 | `Diagnostics=true` 时每实例记录 `kind`, `height`, `rendererCount`, `fallback` |

阶段 C 必须在真实游戏截图中比较不同高度的僵尸和动物，不能只通过代码断言完成。

### 阶段 D：设置与产品验收

| 状态 | 文件/任务 | 结果 |
|---|---|---|
| 已实现 | `CheatMenuPlugin.cs` | 绑定配置并持久化总开关 |
| 已实现 | `CheatMenuOverlayUi.cs` | 增加总开关和状态说明 |
| 已实现 | `PluginLocalization.cs` | 中英文设置文案 |
| 已通过 | `LocalizationSmoke` | 中英文静态文案断言 |
| 待用户验收 | 实机验收 | 计时、瞄准、遮挡、模型高度、性能 |

## 十三、测试与验收矩阵

### 13.1 自动测试

`CreatureHealthBarSmoke` 至少覆盖：

| 测试 | 断言 |
|---|---|
| 生命格式化 | `75, 100` 输出 `75 / 100`，没有 `%` |
| 僵尸上限来源 | 使用运行时 max health，不使用固定 100 |
| 动物上限来源 | 使用 AnimalAsset health |
| 伤害候选 | 回调只记录，不改变 allow 标志 |
| 实际掉血确认 | 当前生命下降才启动计时 |
| 无效伤害 | 生命未下降不启动 10 秒 |
| 重复受伤 | 再次受伤覆盖截止时间为当前时间 + 10 秒 |
| 回血 | 数值变化但截止时间不提前清除 |
| 死亡清理 | `isDead` 或 Unity null 立即隐藏/移除 |
| 准心优先级 | 准心目标在计时结束后仍显示 |
| 数量上限 | 超出上限按准心和距离排序截断 |
| 配置钳制 | 1–30 秒、5–250 米、1–24 条有效 |
| 空上限 | max health <= 0 时隐藏且不抛异常 |

自动测试不能证明 Renderer 高度、遮挡和游戏内视觉效果。

### 13.2 静态/构建验证

实现后执行：

```powershell
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release --nologo
Get-ChildItem .\tests -Recurse -Filter '*.csproj' | ForEach-Object {
    dotnet run --project $_.FullName -c Release --no-restore
}
git diff --check
```

还要检查：

- `DamageTool` 两个委托在所有退出路径退订；
- 事件处理器不改写伤害参数或 `shouldAllow`；
- 没有新增每帧 `FindObjectsOfType`、全地图 Collider 扫描或无上限 GameObject 创建；
- `PointTool` 原有显示、交互和范围逻辑不回归；
- 现有未提交文件未被格式化或重排。

### 13.3 游戏内验收

在 BattlEye 关闭的 Steam 单人世界中，使用正常游戏启动流程：

1. 配置血条开关并进入地图，确认无目标时没有空白 Canvas 或持续日志；
2. 准心对准普通僵尸，确认头顶显示血条和 `当前 / 上限`；
3. 移开准心，确认满血目标立即隐藏；
4. 攻击僵尸，确认实际掉血后血条出现并持续 10 秒；
5. 在 10 秒内再次攻击，确认计时从最近一次掉血重新开始；
6. 等待超过 10 秒且准心移开，确认血条隐藏；
7. 验证动物，确认上限不是固定 100，数值与 AnimalAsset 生命一致；
8. 让动物发生回血，确认当前生命数字实时变化；
9. 验证爬行者、普通僵尸、巨型/特殊僵尸和至少三种不同高度动物；
10. 站在墙后瞄准生物，确认血条不穿墙；
11. 让目标离开视野、进入相机后方或超出显示距离，确认隐藏；
12. 杀死目标，确认血条立即消失且不会复用到新生成生物；
13. 快速切换地图、打开/关闭菜单、死亡/重生，确认没有残留 UI 和静态事件异常；
14. 在 1920x1080、2560x1440、低分辨率窗口和 UI 缩放变化下确认数字不溢出；
15. 在多个生物同时受伤时确认最多显示配置数量，帧率和日志无明显异常。

## 十四、证据账本

| 结论 | 当前证据 | 状态 |
|---|---|---|
| 目标程序集与仓库引用一致 | 两个 `Assembly-CSharp.dll` SHA-256 相同 | 已确认 |
| 僵尸有当前/上限生命读取 | `GetHealth()` / `GetMaxHealth()` 元数据 | 已确认 |
| 动物有当前生命和资产上限 | `Animal.GetHealth()` / `Animal.asset.health` 元数据与官方文档 | 已确认 |
| 僵尸/动物有受伤委托 | `DamageTool.zombieDamaged` / `animalDamaged` 元数据 | 已确认 |
| 受伤委托早于最终扣血 | `DamageTool.damageZombie/damageAnimal` IL 调用顺序 | 已确认 |
| 中心准心射线可复用 | `PointToolService.RefreshRaycast` 源码 | 已确认 |
| 动态 uGUI 可复用 | `PointToolHud.EnsureCreated` 源码 | 已确认 |
| 10 秒状态机逻辑 | `CreatureHealthBarState` 与 `CreatureHealthBarSmoke` | 已实现并通过自动测试 |
| Renderer 高度适配 | 尚未在真实模型运行 | 未验证 |
| 墙体遮挡效果 | 尚未在游戏中运行 | 未验证 |
| 多目标性能 | 尚未在真实场景采样 | 未验证 |
| 游戏加载与真实玩法 | 本轮未启动游戏 | 未执行 |

文档中的“已确认”表示源代码、程序集元数据或官方 stable 文档证据；不等于功能已经交付。只有构建、Smoke、DLL 加载和游戏内矩阵分别通过后，才能更新为实现完成。

## 十五、风险与处理策略

| 风险 | 影响 | 处理 |
|---|---|---|
| 伤害回调在生命扣除前触发 | 无效伤害误显示 | 下一帧读取生命确认 |
| 动物生命上限不是实例字段 | 数字上限错误 | 使用 `animal.asset.health` |
| Zombie Difficulty 覆盖生命 | 写死上限错误 | 使用 `GetMaxHealth()` |
| 模型有粒子/武器 Renderer | 血条高度异常 | Renderer 类型和层级过滤 |
| 生物动画改变包围盒 | 血条抖动 | 缓存根节点模型范围，低频重算，LateUpdate 投影 |
| 目标被遮挡 | 穿墙显示 | 低频视线 Raycast |
| 静态委托重复订阅 | 回调重复、内存泄漏 | 初始化幂等 + Dispose 退订 |
| PointTool 与血条争用 Canvas | UI 层级/输入异常 | 独立 Canvas，低于 PointTool 排序，不接收 Raycast |
| 生物数量过多 | CPU/UI 开销上升 | 事件驱动、距离排序、对象池和数量上限 |
| 游戏更新回调重复 | 每帧重复刷新 | 复用 `_lastRuntimeUpdateFrame` 去重 |
| 多人模式误用 | 权限/反作弊风险 | 首版仅单人，显式门禁 `SingleplayerGuard.IsReady` |

## 十六、完成定义

只有以下条件全部满足，才可以把本功能标记为完成：

1. 阶段 A–D 的源码、配置、Smoke 和本地化改动完成；
2. Release 构建无错误、无新增警告；
3. 所有现有 Smoke 和新增 Smoke 通过；
4. 部署 DLL 与构建产物版本、大小、SHA-256 一致；
5. BepInEx 日志证明插件加载、两个 DamageTool 委托订阅成功；
6. 实机矩阵覆盖僵尸、动物、不同高度、回血、死亡、遮挡、菜单和地图切换；
7. 10 秒计时和重伤重置由实际游戏行为确认；
8. 没有把源码/Smoke/日志加载证据写成“游戏内视觉已完成”；
9. 用户确认血条位置、颜色、尺寸、显示条件和性能表现。

源码、配置、Smoke、构建和 DLL 部署已经完成；本文件仍不能替代上述真实游戏视觉、玩法和性能验收。

## 十七、落地结果（2026-08-25）

本轮已按阶段 A-D 完成源码、配置、UI、本地化、Smoke、构建和部署。游戏内视觉与玩法矩阵由用户在部署版本上继续验收，不以静态证据替代。

| 项目 | 当前证据 | 状态 |
|---|---|---|
| 纯逻辑状态、实际掉血确认、10 秒重置 | `CreatureHealthBarState.cs`、`CreatureHealthBarSmoke` | 已实现并通过 |
| 僵尸/动物事件、准心、范围、遮挡、清理 | `CreatureHealthBarService.cs` | 已实现，等待游戏内验收 |
| Renderer → Collider → 根节点高度链 | `CreatureBoundsResolver` | 已实现，等待不同模型截图验收 |
| 独立 Canvas、固定尺寸、对象池、LateUpdate 投影 | `CreatureHealthBarHud.cs`、`PluginRuntimeHost.LateUpdate` | 已实现，等待分辨率/缩放验收 |
| 配置与工具页总开关 | `[CreatureHealthBar]`、`BuildPointToolPanel` | 已实现 |
| 中英文本地化 | `PluginLocalization.cs`、`LocalizationSmoke` | 已实现并通过 |
| Release 构建 | `dotnet build ... -c Release`，0 警告、0 错误 | 已通过 |
| 仓库内全部可执行 Smoke | 11 个 `*.csproj` Smoke | 已通过 |
| 游戏 DLL 部署 | `1.7.0.0`、525824 字节、SHA-256 `12B344E347B8DFE015CF2381B426A86A0DB2C7C654B1F6DA5E20D43A92A935BF`，源/目标一致 | 已完成 |
| BepInEx 新日志与游戏内 15 项矩阵 | 需启动本次部署版本后采集 | 待用户验收 |

当前交付边界：代码和 DLL 已可供测试；方案第十三节 13.3 与完成定义中的真实游戏行为、视觉、性能及用户确认仍保持待验收，不能提前标记为游戏内完成。

最终审计修正包括：菜单/总开关切换时清除旧准心状态、关闭期间不积累伤害候选、HUD 文本与填充差异更新、Renderer 类型与附着层级过滤、5 Hz bounds 刷新、低频模型重扫、在 `MaxVisibleBars` 截断后才执行遮挡射线、退出世界时销毁 Canvas，以及默认关闭时也记录初始化配置和程序集版本。部署前 DLL 备份位于 `backups/deploy-creature-health-bar-user-test-20260825-223055/`。
