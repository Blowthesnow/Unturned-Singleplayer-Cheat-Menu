# “其他”页世界维护功能落地方案

日期：2026-09-01
状态：实施前方案，仅新增本文档；本轮不修改生产代码、不构建、不部署
目标插件：`UnturnedSingleplayerCheatMenu` 1.95.0
运行时：BepInEx 5 / Unity Mono / `netstandard2.1` / Legacy uGUI
本机接口基线：`lib/Assembly-CSharp.dll`
SHA-256：`D00E2889729CCA8CB11DB85C18956F4F7C25D043FB3E7F4E029ADDC1CB5A5D9A`

## 1. 已确定的产品决策

1. 删除此前设想的“当前世界”信息块，不显示地图名、地图尺寸、玩家坐标或区域编号。
2. 左侧导航仍保持“其他”，本阶段不连带改名，避免扩大视觉和本地化改动范围。
3. 页面前半段继续负责时间、天气和世界事件；页面后半段改为真正可操作的“世界维护”。
4. “扫描信息”继续保留，但压缩为资产概览；世界维护放在它后面。
5. 首版落地三个维护域：
   - 自然资源刷新；
   - 丧尸清除；
   - 地面掉落物清理。
6. 动物清除、物资点立即补货、载具清理列入后续阶段，不和首版一起冒险落地。
7. 所有世界写操作继续受 `SingleplayerGuard` 限制，不增加联机路径。
8. 不引入 TMP、图标、贴图、第二个 Canvas 或第二个 EventSystem；继续使用当前 Legacy uGUI 与现有 `PluginUiRoot`。

## 2. 当前问题与目标

当前 `CheatMenuOverlayUi.WorldView.cs` 的顶部使用两个同高 `460f` 卡片：左侧时间卡内容充足，右侧世界事件卡只有两排按钮，因此形成大面积无意义空白。底部扫描信息只有两行统计与一个按钮，也没有承担起页面后半段的信息和操作密度。

本次设计不靠堆装饰或无关状态填空，而是把右侧卡片拆成清晰的小区域，并在扫描信息之后加入有明确用途、可预览、可确认、可反馈的世界维护功能。

目标：

- 顶部左右视觉重量接近，不再出现半张空卡；
- 操作前能看到当前状态或受影响数量；
- 破坏性操作不会因误点立即执行；
- 大批量操作不会在单帧处理全部对象导致卡顿；
- 丧尸清除不破坏现有小地图导航区保留与原生死亡计数；
- 地面物品清理后，自动拾取和小地图不继续消费旧快照；
- 资源刷新保留原生碰撞阻挡规则，不把树或岩石刷进玩家、敌人或建筑物中。

## 3. 目标页面结构

```text
┌──────────────────── 时间 ────────────────────┬──────── 环境状态 ────────┐
│                                              │ 当前天气 / 冻结 / 满月   │
│              现有十二小时表盘                ├──────── 世界事件 ────────┤
│                                              │ 空投 / 下雨 / 暴雪       │
│ [设为白天] [设为夜晚]                        │ 清除天气 / 关闭调度      │
│ [冻结时间] [强制满月]                        ├──────── 情景预设 ────────┤
│                                              │ 晴朗正午 / 满月之夜      │
│                                              │ 暴雪夜晚 / 恢复自然      │
├──────────────────── 资产概览（原扫描信息） ────────────────────────────┤
│ 物品总数 / Workshop 物品 / 载具总数 / Workshop 载具 / 重新扫描         │
├──────────────────────── 世界维护 ───────────────────────────────────────┤
│ ┌────────────── 自然资源刷新 ─────────────┐ ┌──────── 丧尸清除 ──────┐ │
│ │ 枯竭 / 可恢复 / 被阻挡                  │ │ 附近存活 / 已加载存活  │ │
│ │ [刷新附近资源] [刷新全图资源]           │ │ [清除附近] [清除已加载]│ │
│ └─────────────────────────────────────────┘ └─────────────────────────┘ │
│ ┌────────────────────── 地面掉落物清理 ───────────────────────────────┐ │
│ │ 附近数量 / 全部数量   范围：100m | 250m | 500m                     │ │
│ │ [清理附近掉落物]                         [清理全部掉落物]          │ │
│ └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3.1 顶部区域

- 左侧时间卡的表盘、几何、拖拽逻辑、白天/夜晚按钮和两个开关保持不变。
- 右侧不再是一张只有按钮的空卡，而是同一列中的三个紧凑区：
  1. **环境状态**：当前活动天气名称、时间是否冻结、满月是否开启；不显示地图和坐标。
  2. **世界事件**：保留现有空投、下雨、暴雪、清除天气、关闭天气调度。
  3. **情景预设**：组合现有动作，不新增底层作弊原语。
- 建议预设：
  - `晴朗正午`：设置正午、清除当前天气、关闭强制满月；
  - `满月之夜`：设置夜晚、开启满月、清除当前天气；
  - `暴雪夜晚`：设置夜晚、触发默认暴雪；
  - `恢复自然`：解除时间冻结、关闭强制满月、调用 `ResetScheduledWeather` 恢复自然调度。

### 3.2 资产概览

- 保留现有物品和载具目录统计，不与“自然资源刷新”混用“资源”一词。
- 将文字行改成四个紧凑数值格：
  - 全部物品；
  - Workshop 物品；
  - 全部载具；
  - Workshop 载具。
- 保留“重新扫描模组资产”。该按钮只重建 `AssetCatalog`，不得触发自然资源、僵尸或掉落物操作。

### 3.3 世界维护

- 位于资产概览之后，继续使用外层纵向 `ScrollRect`。
- 卡片高度根据内容使用 preferred height，不再为对齐而制造大块固定空白。
- 附近范围使用一组单选文本按钮：`100m / 250m / 500m`，默认 `250m`。
- 范围属于 UI 会话状态，可写入 `UiStateStore`；首版不增加 BepInEx 配置项。
- 不为维护操作增加快捷键，防止绕过预览和确认。

## 4. 功能契约

### 4.1 自然资源刷新

#### 用户语义

“自然资源”专指地图中的树木、岩石等 `ResourceSpawnpoint`，不等同于物品资产目录，也不等同于地面物资刷新点。

提供：

- `刷新附近资源`：立即恢复选定半径内已经枯竭的自然资源；
- `刷新全图资源`：立即恢复当前地图全部可安全恢复的自然资源。

卡片统计：

- `枯竭`：`spawn.isDead == true` 的候选数量；
- `可恢复`：枯竭且未被玩家、敌人或建筑阻挡；
- `被阻挡`：枯竭但当前不能安全恢复，执行时跳过。

#### 已核对的本机入口

| 入口 | 本机可见性 | 用途 |
| --- | --- | --- |
| `LevelGround.GetTreesOrNullInRegion(byte, byte)` | public static | 枚举每个资源区的 `ResourceSpawnpoint` |
| `ResourceSpawnpoint.isDead` / `point` | public property | 判断枯竭状态和范围 |
| `ResourceManager.ServerSetResourceAlive(byte, byte, ushort)` | public static | 通过原生可靠消息与 loopback 恢复资源 |
| `ResourceManager.overlapTreeColliders(ResourceSpawnpoint, int)` | private instance | 复用原生树木/岩石重生碰撞检查 |

#### 实现规则

1. 以 `ResourceManager.RESOURCE_REGIONS` 为边界逐区读取 `LevelGround.GetTreesOrNullInRegion(x, y)`，保存 `(x, y, index, spawn)`，不缓存跨场景对象引用。
2. 只选择 `isDead` 候选。
3. 附近范围按 `spawn.point` 与玩家位置的水平 X/Z 距离过滤。
4. “立即刷新”有意跳过自然重生计时 `checkCanReset(...)`，但必须保留原生碰撞阻挡检查。
5. 在服务初始化时一次性解析：
   - 私有静态 `ResourceManager.manager`；
   - 私有实例方法 `overlapTreeColliders(ResourceSpawnpoint, int)`。
6. 使用与原生 `respawnResources()` 一致的遮罩：
   - `RayMasks.PLAYER | RayMasks.ENEMY`；
   - 当 `Items_Obstruct_Tree_Respawns` 开启时追加 `RayMasks.BARRICADE`。
7. `ResourceManager.manager` 尚未创建、私有字段或方法签名解析失败时必须 fail closed：卡片显示“当前版本不支持安全资源刷新”，按钮禁用，不得省略碰撞检查后强行恢复。
8. 每帧最多处理 `64` 个候选；每次调用前重新确认 `spawn != null && spawn.isDead`。
9. 完成后重新扫描统计，结果显示：`已恢复 N，阻挡跳过 M，失效跳过 K`。

#### 明确禁止的错误实现

- 不把 `ResourceManager.askClearAllResources()` 绑定到“刷新资源”；该方法语义是清除/摧毁资源，方向完全相反。
- 不在循环中每次使用反射查找字段或方法；启动时解析并缓存委托。
- 不直接调用 `ResourceSpawnpoint.revive()` 代替 `ServerSetResourceAlive`，避免绕过原生状态分发。
- 不通过连续反射调用私有 `respawnResources()` 实现“立即刷新”；它是原生按时间和区域增量推进的单步流程，不是批量刷新 API。

### 4.2 丧尸清除

#### 用户语义

提供：

- `清除附近丧尸`：清除选定水平半径内的存活丧尸；
- `清除已加载丧尸`：清除 `ZombieManager.AllZombies` 中当前已加载且存活的丧尸。

第二个按钮不能写成“清除全图丧尸”：未加载导航区没有可操作的活体对象，“已加载”才是准确边界。

默认保护：

- 已死亡对象；
- 信标事件区域中的丧尸；
- Boss / Mega 丧尸。

确认窗提供“包括特殊丧尸”复选项，默认关闭。确认摘要必须分别显示普通候选数和受保护跳过数。

#### 已核对的本机入口

| 入口 | 本机可见性 | 用途 |
| --- | --- | --- |
| `ZombieManager.AllZombies` | public static property | 枚举跨导航区已加载丧尸 |
| `Zombie.isDead` / `bound` / `isBoss` / `isMega` | public | 状态与特殊对象判断 |
| `ZombieManager.regions[bound].hasBeacon` | public getter path | 判断信标事件区域 |
| `ZombieManager.sendZombieDead(Zombie, Vector3, ERagdollEffect)` | public static | 走原生死亡、计数、可靠消息和区域回调 |

#### 实现规则

1. 操作开始时复制 `ZombieManager.AllZombies.ToArray()`，禁止边遍历原列表边修改生命周期。
2. 过滤 `null`、`isDead` 和范围外对象。
3. 默认跳过信标区域、Boss 和 Mega；用户在确认窗显式勾选后才纳入。
4. 每帧最多处理 `16` 个丧尸，调用：
   - `ZombieManager.sendZombieDead(zombie, Vector3.zero, ERagdollEffect.None)`。
5. 每个候选执行前再次检查 `zombie != null && !zombie.isDead`。
6. 此路径会生成原生尸体/布娃娃，但不走玩家伤害请求，因此不伪造玩家击杀、经验或普通伤害来源。
7. 完成后让现有 `ZombieRegionRetentionPatch.Maintain()` 的 `0.2s` 周期自然观察空区域并回收符合条件的远区；维护服务不直接篡改其私有保留表。
8. 小地图和生命条下一次扫描必须排除 `isDead` 对象；完成结果显示 `已清除 N，特殊保护 M，执行时失效 K`。

#### 与现有僵尸保留补丁的硬边界

- 不对单个丧尸调用 `UnityEngine.Object.Destroy(zombie.gameObject)`。
- 不把 `ZombieRegion.destroy()` 用作交互式“清除丧尸”。该方法会直接销毁整个区域的 GameObject 并清空列表，是导航区生命周期和 LRU 回收原语，不是玩家操作语义。
- 不临时关闭 `ZombieRegionRetentionPatch`。
- 不手改 `ZombieRegion.alive`、`AllZombies`、`region.zombies` 或 `isNetworked`。
- 清除后丧尸仍会按照游戏原生配置自然重生；首版不加入“永久禁止重生”。

### 4.3 地面掉落物清理

提供：

- `清理附近掉落物`：清除选定半径内的地面物品；
- `清理全部掉落物`：清除当前地图所有 `ItemRegion.items`。

#### 已核对的本机入口

| 入口 | 本机可见性 | 用途 |
| --- | --- | --- |
| `ItemManager.regions` | public static property | 统计地面掉落物与区域候选 |
| `ItemManager.ServerClearItemsInSphere(Vector3, float)` | public static | 原生附近范围清理与可靠销毁消息 |
| `ItemManager.askClearRegionItems(byte, byte)` | public static | 原生逐区域批量清理 |
| `ItemManager.askClearAllItems()` | public static | 原生全图同步清理，保留作兼容依据 |

#### 实现规则

1. 附近清理直接调用 `ServerClearItemsInSphere(playerPosition, radius)`。
2. 全部清理不在一个按钮回调里直接调用 `askClearAllItems()`；为避免一次循环全部世界区域，先生成非空区域队列，再按每帧最多 `8` 个区域调用 `askClearRegionItems(x, y)`。
3. 操作开始前统计候选数量并展示确认；执行结果以清理前后差值和逐区结果为准。
4. 维护操作在 `PluginRuntimeContext.Update()` 中安排在 `RefreshSharedItemSnapshot(...)`、`Minimap.Update(...)` 和 `AutoPickup.Pump(...)` 之前执行。
5. 任何物品清理后立刻：
   - `_sharedItemSnapshot = null`；
   - `_nextSharedItemScanTime = 0f`；
   - 增加共享快照版本；
   - 请求维护统计刷新。
6. 自动拾取执行前仍使用现有 `AutoPickupCandidateAdapter.IsCurrent(...)` 做最终存活校验，旧候选只允许被判为 stale，不允许拾取已删除物品。
7. 不触碰玩家背包、衣物容器、无限仓库或储物箱内容；作用域只包含 `ItemManager` 管理的地面掉落物。

## 5. 后续功能边界

### 5.1 第二阶段可加入

#### 动物清除

- 附近：`AnimalManager.getAnimalsInRadius(...)` + `AnimalManager.sendAnimalDead(...)`；
- 全部：`AnimalManager.askClearAllAnimals()`；
- 默认保护任务/事件相关动物；
- 复用丧尸清除的批处理、预览和确认交互。

#### 物资点立即补货

“物资点补货”与“自然资源刷新”分开命名。原生 `ItemManager.respawnItems()` 是私有、按区域/时间/出生表增量执行的流程，同时检查安全区、出生概率、区域数量和刷新时间。第二阶段只能在完整恢复这些约束并完成实机验证后加入，不能通过无界循环暴力调用私有方法。

### 5.2 暂不加入

- 清除全部载具；
- 自动删除无人载具；
- 重建载具出生点；
- 清除建筑、路障或储物箱；
- 重置整张地图或存档；
- 永久关闭丧尸/动物重生；
- 自动定时执行任何清理操作。

载具和玩家建造物可能携带所有权、储物、附件、燃料和持久化状态。在没有保存前预览、排除规则和恢复方案前，不应放进首版“世界维护”。

## 6. 交互与状态设计

### 6.1 统一操作状态

```text
Idle
  -> ScanningPreview
  -> AwaitingConfirmation
  -> Running
  -> Completed | Failed | Stopped
```

- 同一时间只允许一个世界维护任务运行。
- 任务运行时禁用其他维护按钮，但时间、天气和普通页面导航仍可使用。
- `停止`只停止尚未处理的候选；已经恢复或清除的对象不会回滚，按钮和确认文案必须明确写出这一点。
- 切换普通页面或关闭菜单时任务继续运行；场景切换、返回主菜单、插件卸载时必须停止并丢弃旧场景引用。
- 完成、失败或停止后更新状态栏并增加 `SnapshotVersion`，只在版本变化时重建维护卡片。

### 6.2 预览与确认

- 卡片上的数量是最近一次缓存统计，不作为最终执行依据。
- 点击按钮后重新生成不可变 `WorldMaintenancePreview`。
- 候选为 `0` 时不打开确认窗，直接显示“没有符合条件的目标”。
- 以下操作必须打开确认窗：
  - 清除附近丧尸；
  - 清除已加载丧尸；
  - 清理附近掉落物；
  - 清理全部掉落物；
  - 刷新全图自然资源。
- `刷新附近资源`可直接执行，但仍显示即时统计与结果。
- 确认窗显示：动作、范围、候选数、保护/阻挡跳过数、不可撤销说明。
- 继续复用 `_uiRoot.RegisterModal(...)`、Escape 关闭、焦点恢复和现有 ModalLayer；不创建新 Canvas/EventSystem。

### 6.3 视觉规则

- 资源刷新使用普通或成功语义色。
- 附近清理使用警告语义色。
- 全部清理使用危险语义色。
- 颜色不是唯一提示；按钮和状态文字必须明确写“附近”“已加载”“全部”。
- 不使用入口动画；按钮只保留现有 hover/pressed/focus 状态。
- 进度以文字和细进度条表达，例如：`处理中 128 / 642`。
- 不使用图标或 emoji。

## 7. 代码架构与文件边界

### 7.1 新增生产文件

| 文件 | 职责 |
| --- | --- |
| `Services/WorldMaintenanceService.cs` | 候选扫描、预览、单任务状态机、分帧执行、结果统计、场景取消 |
| `Services/WorldMaintenanceNativeBridge.cs` | 已验证原生 API 调用、资源碰撞私有入口一次性解析、fail-closed 能力状态 |
| `Models/WorldMaintenanceContracts.cs` | `ActionKind`、`Scope`、`Preview`、`Progress`、`Result`、跳过原因 |
| `UI/CheatMenuOverlayUi.WorldMaintenanceView.cs` | 三张维护卡、范围选择、确认窗、进度和结果绑定 |

### 7.2 修改生产文件

| 文件 | 修改内容 |
| --- | --- |
| `CheatMenuPlugin.cs` | 创建并公开 `WorldMaintenanceService`，在 UI 创建前完成原生能力探测 |
| `Services/PluginRuntimeComposition.cs` | 纳入服务所有权 |
| `Services/PluginRuntimeContext.cs` | 每帧 Pump、物品快照失效、场景取消、Dispose |
| `UI/CheatMenuUiFeaturePorts.cs` | 增加 `IWorldMaintenanceUiFeature`，UI 不直接访问 Manager |
| `UI/CheatMenuFeaturePresenters.cs` | 增加 `WorldMaintenancePresenter`，转发预览/执行/停止并提供只读快照 |
| `UI/CheatMenuOverlayUi.cs` | 持有 presenter 和少量 UI 状态；不加入业务逻辑 |
| `UI/CheatMenuOverlayUi.WorldView.cs` | 删除“当前世界”设想，重排右侧、压缩扫描信息、挂载维护视图 |
| `UI/UiStateStore.cs` | 保存本次 UI 会话的维护半径和“保护特殊丧尸”默认值 |

### 7.3 不应修改

- `Minimap/NearbyWorldScanner.cs` 的跨导航区枚举算法；
- `Minimap/ZombieRegionRetentionPolicy.cs` 的上限、保护半径和 LRU 规则；
- `Patches/ZombieRegionRetentionPatch.cs` 的内部保留表结构；
- `AutoPickup` 的筛选和拾取语义；
- 无限仓库数据文件与事务；
- 当前表盘几何和拖拽控件；
- 部署脚本、游戏原始程序集和游戏存档。

## 8. 建议的核心模型

```csharp
internal enum WorldMaintenanceActionKind
{
    RefreshNaturalResources,
    ClearZombies,
    ClearGroundItems
}

internal enum WorldMaintenanceScopeKind
{
    Nearby,
    LoadedWorld,
    WholeMap
}

internal sealed class WorldMaintenancePreview
{
    internal WorldMaintenanceActionKind Action { get; init; }
    internal WorldMaintenanceScopeKind Scope { get; init; }
    internal float RadiusMeters { get; init; }
    internal int CandidateCount { get; init; }
    internal int ProtectedCount { get; init; }
    internal int BlockedCount { get; init; }
    internal int SnapshotVersion { get; init; }
}
```

实际实现可以按当前语言版本调整语法，但模型必须满足：

- Preview 不保存可跨场景长期使用的 Unity 对象；
- Confirm 时校验 preview version，过期则重新扫描；
- Running 任务保存本次短期候选快照，并在每次执行前重新判活；
- Result 分开记录成功、保护跳过、阻挡跳过、失效跳过和异常失败。

## 9. 运行时顺序

`PluginRuntimeContext.Update()` 建议调整为：

```text
UI root / deferred destroy
Workshop localization
WorldMaintenance.Pump()
  └─ 若清理过地面物品：InvalidateSharedItemSnapshot()
Input / shortcuts
Inventory arrange
RefreshSharedItemSnapshot()
Minimap.Update()
AutoPickup.Pump()
InfiniteStorage.Pump()
ZombieRegionRetentionPatch.Maintain()（现有 0.2s 周期保持）
```

原因：维护操作先改变世界，随后同一帧生成的新共享快照才能反映最新状态；自动拾取和小地图不会拿清理前快照继续工作。

## 10. 实施阶段

### 阶段 0：冻结基线

1. 记录 Git 状态，不重置、不暂存、不覆盖当前未提交改动。
2. 记录 `lib/Assembly-CSharp.dll` 哈希和目标方法签名。
3. 递归枚举现有 `tests/**/*Smoke.csproj`。
4. 保存当前“其他”页截图作为视觉基线。

### 阶段 1：纯模型与策略

1. 新增 contracts 和纯策略函数。
2. 完成范围边界、特殊丧尸保护、批处理预算、停止语义和结果汇总测试。
3. 此阶段不调用 Unity/Unturned Manager。

### 阶段 2：原生桥接与服务

1. 实现能力探测和资源碰撞委托缓存。
2. 实现资源、丧尸、掉落物候选扫描。
3. 实现单任务分帧执行。
4. 接入场景切换取消和共享物品快照失效。
5. 每类操作只保留一个原生写入口，避免 UI、presenter 和 service 多处直接写世界。

### 阶段 3：页面重排

1. 保持时间卡不动。
2. 将右侧改成环境状态、世界事件、情景预设。
3. 压缩资产概览。
4. 在其后加入维护卡片、范围选择和进度区。
5. 不创建“当前世界”卡片或对应本地化字符串。

### 阶段 4：确认与反馈

1. 实现预览确认窗。
2. 特殊丧尸保护选项只出现在相关确认窗。
3. 完成、失败、停止统一走现有状态栏和 Toast。
4. 页面重建时保留运行任务状态，不重复启动。

### 阶段 5：验证、部署与实机

1. Release 构建。
2. 递归重建并执行全部 Smoke。
3. 游戏停止后备份旧 DLL，仅复制一个目标 DLL 并比对哈希。
4. 重启游戏，在同一单人存档逐项验收。

## 11. 自动化验证

### 11.1 新增 `WorldMaintenanceSmoke`

覆盖：

- 半径 `100/250/500m` 的边界值；
- dead/null/out-of-range 候选排除；
- Boss/Mega/Beacon 默认保护和显式包含；
- 每帧资源 `64`、丧尸 `16`、物品区 `8` 的预算；
- 运行中停止只保留已完成结果；
- 场景版本改变后旧 preview 失效；
- 结果计数总和守恒。

### 11.2 扩展现有 Smoke

| Smoke | 新增断言 |
| --- | --- |
| `UiArchitectureSmoke` | 存在“世界维护/自然资源刷新/丧尸清除/地面掉落物清理”；不存在“当前世界”；确认窗注册到 ModalLayer |
| `MinimapSmoke` | 清除目标只走原生死亡契约；dead 丧尸不再进入 marker；保留区策略未被改写 |
| `AutoPickupSmoke` | 物品维护后共享快照失效；旧候选只能返回 stale；不会写入背包或无限仓库 |
| `UiControlLibrarySmoke` | 新按钮、范围选择和进度条使用现有 Legacy 控件；无 TMP、图标或第二 EventSystem |

### 11.3 静态检查

- `git diff --check`；
- 生产源码无 `TMPro`；
- 无 `new EventSystem`；
- “刷新资源”调用链中不存在 `askClearAllResources`；
- “清除丧尸”调用链中不存在 `Object.Destroy(zombie.gameObject)` 和 `ZombieRegion.destroy()`；
- UI 文件中不存在直接的 `ResourceManager`、`ZombieManager`、`ItemManager` 写调用。

## 12. 游戏内验收矩阵

### 12.1 页面和输入

- 16:9、16:10、21:9 均无裁剪；
- 右侧顶部不再有明显空白；
- 页面中不存在“当前世界”区块；
- 滚动条能到达全部维护卡片；
- 模态确认打开后焦点进入窗口，Escape 关闭并恢复原焦点；
- 菜单关闭后相机、移动、原生 UI 和鼠标恢复正常。

### 12.2 自然资源

1. 砍倒多个树木/岩石。
2. 刷新附近资源，只恢复范围内目标。
3. 玩家站在树桩上时必须计为阻挡并跳过。
4. 树桩上存在路障且原生配置启用阻挡时必须跳过。
5. 刷新全图资源时显示进度，主线程无明显长时间冻结。
6. 重新载入存档后资源状态符合游戏正常保存结果。

### 12.3 丧尸

1. 同时准备普通丧尸、Mega/Boss 和信标事件丧尸。
2. 默认附近清除只处理普通候选。
3. 确认窗勾选后才处理特殊候选。
4. 清除后 3D 模型进入原生死亡流程，小地图红点和生命条消失。
5. 不获得伪造经验或击杀奖励。
6. 跨导航区保留、远区 LRU 和自然重生继续工作。
7. “清除已加载丧尸”文案和实际范围一致，不宣称未加载全图已被处理。

### 12.4 地面掉落物

1. 在范围内外分别丢弃物品。
2. 附近清理只删除半径内物品。
3. 全部清理按区域分帧完成。
4. 玩家背包、衣物容器、储物箱和无限仓库数量不变。
5. 自动拾取不会继续请求已经删除的 instance ID。
6. 小地图物品标记在下一次刷新后消失。

### 12.5 生命周期

- 任务运行中切换普通页面：任务继续且不重复；
- 任务运行中关闭菜单：任务继续，重开后显示结果；
- 任务运行中切换地图或退回主菜单：任务停止并释放旧对象引用；
- 插件卸载：无残留队列、订阅或静态 Unity 引用。

## 13. 验收标准

完成必须同时满足：

1. “其他”页没有“当前世界”信息块。
2. 顶部右侧由状态、事件和预设组成，不再是空白等高卡。
3. 页面后半段包含自然资源刷新、丧尸清除和地面掉落物清理。
4. 每个写操作都有明确作用域和受影响数量。
5. 全图或清除类操作必须经过确认。
6. 资源刷新保留碰撞阻挡并 fail closed。
7. 丧尸清除走 `sendZombieDead`，不直接销毁 GameObject 或区域。
8. 地面物品清理后共享快照立即失效。
9. 批量任务分帧执行，可停止未处理部分。
10. Release、全部递归 Smoke、部署哈希分别通过。
11. 实机页面、交互和实际世界变化完成独立验收；构建和 Smoke 不能替代游戏内结论。

## 14. 回滚

### 14.1 代码回滚

- 删除新增的 WorldMaintenance 文件；
- 从 composition/context/plugin/UI 移除对应接线；
- 恢复 `CheatMenuOverlayUi.WorldView.cs` 的原页面布局；
- 保留与本功能无关的所有现有未提交改动；
- 恢复部署前备份 DLL 并核对哈希。

### 14.2 世界状态边界

资源恢复、丧尸死亡和掉落物删除属于真实存档世界变化，停止任务不能撤销已经完成的部分。确认窗必须在执行前说明这一点。代码/DLL 回滚也不会自动恢复已被改变的存档内容；实机测试应使用可回退的测试存档副本。

## 15. 最终落地顺序

首版按以下顺序实施：

1. 纯策略和 Smoke；
2. 自然资源刷新原生桥接；
3. 丧尸清除与现有保留补丁协同；
4. 地面物品清理与共享快照失效；
5. 页面后半段与确认窗；
6. Release + 全部 Smoke；
7. 停止游戏后的单 DLL 部署；
8. 使用测试存档完成资源、丧尸、掉落物和页面交互验收。

动物清除与物资点补货只在首版实机稳定后进入第二阶段；载具和建筑清理继续冻结。

## 16. 证据来源

- 当前插件源码：`CheatMenuOverlayUi.WorldView.cs`、`CheatMenuUiFeaturePorts.cs`、`CheatMenuPagePresenters.cs`、`PluginRuntimeContext.cs`、`ZombieRegionRetentionPatch.cs`、`AutoPickupRuntime.cs`。
- 官方 stable 文档：Resource Asset 的 `Reset` 语义、Foliage Resource、Spawn Asset、Zombie Difficulty 与 Gameplay Config。
- U3-SDK 源码：`ResourceManager.respawnResources()`、`ServerSetResourceAlive(...)`、`ZombieManager.sendZombieDead(...)`、`Zombie.tellDead(...)`、`ZombieRegion.destroy()`、`ItemManager.ServerClearItemsInSphere(...)` 与 `askClearRegionItems(...)`。
- 本机程序集元数据：本文开头记录的 `Assembly-CSharp.dll` 哈希及本机可见性/签名。实现前和游戏更新后都必须重新核对，不能把本文快照永久视为当前接口。
