# 无限储存物品语义与堆叠修复方案

## 文档状态

- 日期：2026-08-27
- 状态：设计与审计完成，**本轮未修改任何插件运行时代码**。
- 目标：让无限储存保留 Unturned `Item` 的真实语义，特别是区分“普通堆叠数量”和“一个容器内部装有多少资源”。
- 非目标：本文件不代表无限储存已修复、已构建、已部署或已在游戏中验收。

## 1. 已确认的问题与风险

当前实现把所有 `Item.amount` 一律理解为仓库中的物品数量：

- `InventoryBridge.StoreAllCarried` 将 `Math.Max(1, item.amount)` 传给 `VirtualStorageService.Add`。
- `VirtualStorageService` 使用 `AssetId + Quality + StateFingerprint` 聚合该数值。
- `InfiniteStorageRuntime.Withdraw` 将聚合数值再次写入 `new Item(assetId, amount, quality, state)`。

这只对“`amount` 就是普通堆叠个数”的资源成立。对弹匣和通过补给蓝图关联的弹药箱，`amount` 是该容器当前装填的子弹数，不是弹药箱个数。于是一个 `40/40` 民用弹药盒会被错误存成 40 个物品；取出 1 后又变成仓库 `39/40`、背包 `1/40`。这是改变物品内容的错误，继续使用会造成数据损坏。

当前 `VirtualStorageEntry.Count` 也没有声明单位：它有时代表资源件数，有时事实上代表容器内部子弹数。后续代码、制作逻辑、UI 和旧存档均不能再猜测这个字段的含义。

## 2. 不可混用的术语

| 术语 | 精确定义 | 不能混同为 |
|---|---|---|
| 原生物品实例 | 一个完整 `Item(id, amount, quality, state)` | 显示卡片或逻辑仓库条目 |
| 原生堆叠数量 | 对可普通堆叠资源，`amount` 代表的件数 | 容器内部资源量 |
| 容器内部内容量 | 弹匣/弹药箱的当前弹药、油桶的油量等 | 仓库中完整容器的数量 |
| 完整实例数 | 仓库中可单独取出的完整物品数 | `Item.amount` 的通用别名 |
| 逻辑条目 | 合并键完全一致的一组资源单位或完整实例 | 一个原生 `Item` |
| 合并键 | 决定两个物品能否进入同一逻辑条目的所有语义字段 | 仅名称、ID 或可见文本 |
| 展示字段 | 给玩家看的品质、耐久、弹药、油量等摘要 | 合并判断本身 |

核心原则：`state[]` 的可读摘要只用于展示；只要状态策略要求保留，合并必须比较完整原始字节，不能以“看起来相同”替代字节比较。

## 3. 统一的物品实例语义模块

后续实现必须引入单一深模块 `ItemInstanceSemantics`。入库、自动拾取、取出、制作消耗、制作产出、持久化迁移和卡片展示都只能通过该模块取得规则；不得由 UI、`VirtualStorageService`、制作补丁各自再判断 `asset.amount`、`asset.showQuality` 或资产类型。

建议对外接口保持很小：

```csharp
ItemStorageDescriptor Describe(ItemAsset asset, Item item);
StorageKey CreateKey(ItemStorageDescriptor descriptor);
IReadOnlyList<Item> CreateWithdrawItems(StorageEntry entry, long requestedUnits);
CardMetadata BuildCardMetadata(ItemAsset asset, StorageEntry entry);
```

`ItemStorageDescriptor` 至少包含下列固定决策：

| 决策 | 取值 | 含义 |
|---|---|---|
| `StackUnitKind` | `ResourceUnits` / `ContainerInstances` / `SingletonInstances` | 仓库存的最小单位 |
| `AmountRole` | `StackCount` / `AmmoRounds` / `FuelUnits` / `WaterUnits` / `ContainerContents` / `NotApplicable` | 原生 `amount` 的真实语义 |
| `QualityPolicy` | `Ignore` / `Exact` | 是否将 `quality` 放入合并键 |
| `StatePolicy` | `Ignore` / `ExactBytes` | 是否将完整 `state[]` 放入合并键 |
| `WithdrawPolicy` | `SplitResourceUnits` / `WholeInstancesOnly` | 取出时是否允许拆分 |
| `DisplayPolicy` | 仅真实支持的字段 | 卡片可以显示哪些属性 |

### 3.1 三种库存单位

1. `ResourceUnits`
   - 仅用于已证明 `amount` 是普通资源件数、没有实例状态、且质量不参与语义的资产。
   - 仓库数量是资源件数；允许按输入框指定数量拆分，并按原生最大堆叠数生成多个 `Item`。
   - 金属碎片、木头等普通无品质资源应走此分支。

2. `ContainerInstances`
   - `amount` 或 `state[]` 表示该容器自己的内容，而不是库存件数。
   - 仓库数量永远是完整容器数；`amount` 与 `state[]` 是该容器的载荷，必须原样保存。
   - 只允许按完整容器个数取出，绝不从 `40/40` 拆成 `39/40 + 1/40`。

3. `SingletonInstances`
   - 用于原生不可普通堆叠、但完全同态的物品。仓库可以把多个同态完整实例显示为一个条目，但每次取出都生成一个完整原生实例。
   - 枪、近战、服装、带内部状态的工具和未知 Workshop 资产均应保守使用此分支。

## 4. 合并键与保存模型

### 4.1 合并键

新的逻辑键应当由以下字段构成，而不是现有的固定 `AssetId + Quality + StateFingerprint`：

```text
SchemaVersion
AssetId
AssetGuid（运行时可取得时，用于检测存档与 Workshop 资产漂移）
StackUnitKind
CanonicalQuality（QualityPolicy=Ignore 时固定为 0）
CanonicalState（StatePolicy=Ignore 时固定为空字节数组；否则完整原始 state[]）
NativePayloadAmount（ContainerInstances 中保留原 Item.amount；若 amount 已在 CanonicalState 内仍显式保留）
```

`AssetId` 保持为创建原生 `Item` 所需的当前会话 ID；`AssetGuid` 用于在重启、订阅变动或 ID 重排后侦测错误映射。找不到同 GUID 资产时，条目必须隔离并提示，不能按碰巧相同的 ID 取出。

### 4.2 新持久化字段

建议替换语义含混的 `Count`，而非给它继续叠加例外：

```csharp
StorageEntry
{
    int SchemaVersion;
    string EntryId;
    ushort AssetId;
    string AssetGuid;
    StackUnitKind UnitKind;
    long StoredUnitCount;        // ResourceUnits=资源件数；其余=完整实例数
    byte NativeAmount;           // 单个完整实例的原生 amount
    byte NativeQuality;          // 原样保留；是否进入键由 QualityPolicy 决定
    byte[] NativeState;          // 原样保留；是否进入键由 StatePolicy 决定
    byte NativeMaxStack;
    string SemanticsRevision;
}
```

`StoredUnitCount` 的单位必须由 `UnitKind` 明示。禁止再出现一个字段同时代表“40 发弹药”和“40 个弹药盒”。

### 4.3 旧存档迁移

当前旧条目的 `Count` 已经丢失单位信息。不能从 `Count=40` 推出它是“40 个满弹盒”还是“1 个 40/40 弹药盒”。因此迁移必须遵循以下规则：

| 旧条目条件 | 迁移动作 |
|---|---|
| 经语义模块确定为 `ResourceUnits` | 可迁移为相同资源件数 |
| 完整实例类型且旧 `Count == 1` | 可迁移为一个完整实例，保留原始 `amount/quality/state` |
| 弹匣、映射弹药箱、燃料/水容器或其他完整实例类型，且旧 `Count > 1` | 隔离到 `LegacyAmbiguousEntries`，保留原 JSON 与原因，不可自动取出或消耗 |
| 找不到资产或 GUID 不一致 | 隔离，不可按 ID 强行恢复 |

隔离不是丢弃。UI 应显示待处理条目数和导出原始备份入口；在真正实现前，不得写“自动修复”为把 `Count` 直接当作实例数或弹药数的任一做法。

## 5. 资产分类矩阵

本矩阵列出当前程序集中的全部 `ItemAsset` 具体派生类型。它是实现分类器的覆盖清单，不是仅凭类名修改 `amount` 的许可。

标记说明：

- `R`：经过运行时白名单验证后可作为普通资源；未验证时仍走保守完整实例。
- `C`：完整容器实例；仓库按容器个数存取。
- `S`：完整独立实例；同态时可逻辑聚合。
- `Q`：质量必须精确比较并按真实语义显示。
- `q?`：质量是否有语义依赖资产配置，默认精确保留，未验证前不得忽略。
- `X`：完整 `state[]` 必须精确比较；空/`null` 统一规范化为空数组。
- `W`：只允许完整实例取出。

| 资产类 | 默认库存单位 / amount 角色 | 质量 | 状态 | 取出 | 卡片与验证要求 |
|---|---|---|---|---|---|
| `ItemSupplyAsset`，非映射弹药箱 | `R` / `StackCount` | 无真实品质时忽略 | 默认忽略；发现状态即升级为 `X` | 可拆分 | 金属碎片等仅显示库存数量；以运行时样本确认无状态后加入白名单 |
| `ItemMagazineAsset` | `C` / `AmmoRounds` | 通常忽略；`stuck > 0` 时 `Q` | `X` | `W` | 显示“库存 N 个，弹药 a/b”；不能显示“品质”，除非该弹匣真实有品质 |
| `ItemSupplyAsset`，由填装蓝图映射为弹药箱 | `C` / `AmmoRounds` | 默认忽略 | `X` | `W` | 与弹匣同规则；同弹量、同 state 才合并 |
| `ItemGunAsset` | `S` / `NotApplicable` | `Q` | `X` | `W` | 显示真实品质、枪内弹药、附件摘要；附件、弹量、射击模式、皮肤或任意状态差异均分开 |
| `ItemMeleeAsset` | `S` / `NotApplicable` | `Q` | `X` | `W` | 显示真实品质/耐久；任何状态差异分开 |
| `ItemClothingAsset` 及 `ItemBackpackAsset`、`ItemGlassesAsset`、`ItemHatAsset`、`ItemMaskAsset`、`ItemPantsAsset`、`ItemShirtAsset`、`ItemVestAsset` | `S` / `NotApplicable` | `Q` | `X` | `W` | 官方文档明确服装总显示品质；显示品质，状态保留且不猜测字段 |
| `ItemBarrelAsset` | `S` / `NotApplicable` | 仅 `durability > 0` 时 `Q`，否则忽略 | `X` | `W` | 仅真实有耐久的枪管显示品质；附件状态精确隔离 |
| `ItemFuelAsset` | `C` / `FuelUnits`（载荷在状态） | `q?` | `X` | `W` | 显示油量/容量；油量差异不合并、不拆分 |
| `ItemTankAsset` | `C` / `FuelUnits` 或 `WaterUnits`（载荷在状态） | `q?` | `X` | `W` | 显示油/水量与容量；保留完整状态 |
| `ItemGeneratorAsset` | `C` / `ContainerContents` | `q?` | `X` | `W` | 发电机状态固定长度但字段不作为合并简化条件；运行时验证燃料/开关状态 |
| `ItemStorageAsset` | `C` / `ContainerContents` | `q?` | `X` | `W` | 内部物品状态可能为 17/21 字节；必须完整保留，不能只显示为空箱 |
| `ItemLibraryAsset` | `C` / `ContainerContents` | `q?` | `X` | `W` | 库状态约 20 字节；完整保留并用真实游戏样本验收 |
| `ItemBarricadeAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 建筑类有多个状态长度；任何状态差异分开 |
| `ItemStructureAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 保守完整实例；实际状态与部署行为必须游戏内验证 |
| `ItemFarmAsset`、`ItemGrowerAsset`、`ItemFisherAsset`、`ItemOilPumpAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 生长/捕捞/产出进度可能有状态，绝不直接资源化 |
| `ItemFoodAsset`、`ItemMedicalAsset`、`ItemWaterAsset`、`ItemFilterAsset`、`ItemConsumeableAsset`、`ItemRefillAsset` | 初始 `S` / `NotApplicable` | `q?` | `X` | `W` | 食物、药品、滤芯和补充品不得因“看似消耗品”就忽略质量/状态；只有对具体资产取得运行时证明后才可提升为 `R` |
| `ItemBagAsset`、`ItemBoxAsset`、`ItemMapAsset`、`ItemKeyAsset`、`ItemBeaconAsset` | 初始 `S` / `NotApplicable` | `q?` | `X` | `W` | 容器、地图、钥匙或信标的附加数据必须先保守保存；允许在回归样本证明后单独白名单化 |
| `ItemArrestStartAsset`、`ItemArrestEndAsset`、`ItemChargeAsset`、`ItemDetonatorAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 互动或拘束相关状态未知，保持完整实例 |
| `ItemCaliberAsset`、`ItemGearAsset`、`ItemGripAsset`、`ItemOpticAsset`、`ItemSightAsset`、`ItemTacticalAsset` | 初始 `S` / `NotApplicable` | `q?` | `X` | `W` | 武器配件可能携带耐久、倍率或设置；未逐资产验证前不能当普通资源 |
| `ItemPlaceableAsset`、`ItemSentryAsset`、`ItemTrapAsset`、`ItemThrowableAsset`、`ItemTireAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 可部署或可消耗交互物可能有激活/耐久/载荷状态；完整保留 |
| `ItemToolAsset`、`ItemVehicleLockpickToolAsset`、`ItemVehiclePaintToolAsset`、`ItemVehicleRepairToolAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 工具耐久或用量差异不得合并；卡片仅在真实支持时显示品质/耐久 |
| `ItemWeaponAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 基类/通用武器实现采取保守策略；由实际具体资产进一步覆盖 |
| `ItemCloudAsset` | `S` / `NotApplicable` | `q?` | `X` | `W` | 不以名称推断可堆叠性 |

上表合并了具有同一策略的同族类；下列类型亦必须由分类器显式覆盖，默认采用 `S + q? + X + W`，直到加入经过测试的专用规则：`ItemCaliberAsset`、`ItemGearAsset`、`ItemCloudAsset`、`ItemChargeAsset`、`ItemDetonatorAsset`、`ItemFisherAsset`、`ItemMapAsset`、`ItemPlaceableAsset`、`ItemSentryAsset`、`ItemThrowableAsset`、`ItemTireAsset`、`ItemTrapAsset`、`ItemWeaponAsset`。

### 5.1 可显示品质不等于质量具有堆叠语义

不能把 `ItemAsset.showQuality` 单独当作合并键规则：官方 `Override_Show_Quality` 可以强制显示品质，而不证明质量参与该物品的游戏语义。反过来，已知有耐久/品质语义的族（枪、近战、服装，及有耐久的枪管）必须始终精确比较，即使某个 UI 路径没有显示。

实现顺序应为：先按“已知资产族 + 资产运行时配置”决定 `QualityPolicy`，再用 `showQuality` 决定卡片是否显示品质。未知 Workshop 资产保守使用 `Exact`，直到真实样本证明可安全忽略。

## 6. 关键验收示例

| 操作 | 正确结果 | 禁止结果 |
|---|---|---|
| 存入两个同状态 `40/40` 民用弹药盒 | 一个条目：库存 2 个，单盒弹药 `40/40` | 库存或弹药显示为 80/40 |
| 存入一个 `40/40` 与一个 `80/80` 民用弹药盒 | 两个条目 | 合并为一个条目 |
| 对 `40/40` 条目取出 1 | 背包新增一个完整 `40/40`；仓库实例数减 1 | 背包 `1/40`、仓库 `39/40` |
| 存入金属碎片（默认 quality=100） | 同 ID 的无状态资源直接合并，仅显示资源总数 | 卡片显示虚假的品质 100 |
| 两把同 ID 枪，附件/弹量/模式任一不同 | 两个条目 | 仅因名称和 ID 相同而合并 |
| 两个质量不同的服装、枪或近战 | 两个条目 | 忽略质量并合并 |
| 两个油量不同的油桶 | 两个条目，分别显示油量 | 把油量当成可拆分库存 |

## 7. 统一业务流程

### 7.1 入库与批量归并

1. 捕获原始完整 `Item`，克隆 `state[]`。
2. 查找 `ItemAsset`，调用 `ItemInstanceSemantics.Describe`。
3. `ResourceUnits` 使用 `item.amount` 作为资源件数；`ContainerInstances` 和 `SingletonInstances` 固定存入一个完整实例。
4. 用语义化合并键聚合；任何错误先回滚仓库快照。
5. 只有持久化成功后，才从原生背包移除已捕获的原始物品；任一移除失败恢复双方快照。

### 7.2 取出

1. UI 输入框的单位来自 `UnitKind`：资源显示“数量”，完整实例显示“个”。
2. `ResourceUnits` 可按请求数量切分为符合原生最大堆叠的多个 `Item`。
3. `ContainerInstances`/`SingletonInstances` 每次仅还原完整的 `(id, NativeAmount, NativeQuality, NativeState)`；请求 1 就少一个完整实例。
4. `取全部` 对完整实例表示取全部实例，对资源表示取全部资源单位。
5. 任何背包放入、保存或回滚失败都必须恢复完整的背包与仓库快照。

### 7.3 自动拾取与制作

- 自动拾取的转存必须调用同一个 `Describe`；不得旁路为 `_storage.Add(item.amount)`。
- 制作的“可用数量”只能将 `ResourceUnits` 按资源单位求和。完整容器不是任意配方材料的可分数量。
- 当原生蓝图明确消耗一个完整容器时，制作适配层必须按“一个完整实例”预留与删除，不能消费其内部弹药/油量。
- 制作产物转存同样必须通过语义模块；输出失败时保留产物或回滚材料，不能产生半提交。

## 8. UI 显示规则

无限储存独立 uGUI 页面继续复用物品页的卡片、数量输入、滚轮分页、悬停详情、搜索和筛选，但数据来源改为语义化逻辑条目。不得再把“完整实例数”直接标成“剩余弹药”。

卡片摘要规则：

- 普通资源：`库存 12,480`。不显示品质、状态字节数或默认 quality=100。
- 弹匣/弹药箱：`库存 2 个 | 弹药 40/40`。只有实际带 `stuck > 0` 的弹匣才附加品质。
- 武器/服装/近战：`库存 N 个 | 品质/耐久 ...`，并由悬停展示真实状态摘要。
- 燃料/水容器：`库存 N 个 | 燃料/水 a/b`。
- 未解析 Workshop 资产：仅显示 `库存 N 个` 与“保守状态隔离”，不显示虚假的品质或“状态 17B”。

右上角“取全部”可以保留；普通点击及数量输入框必须复用物品页的数量控件。对于完整实例，输入框最小值为 1，单位为“个”，并不能改变单件的 `amount`。

## 9. Workshop 与版本漂移的保守回退

Workshop 资产不能仅依赖类名和当前默认值。分类器启动时应收集审计数据：资产 GUID、具体类、`MaxAmount`、`showQuality`、已知能力、观察到的 `state[]` 长度、`amount` 值和专用匹配规则。

满足以下任一条件时，必须使用 `SingletonInstances + Exact quality + ExactBytes state + WholeInstancesOnly`：

- 资产具体类不在已测试的白名单；
- `state[]` 非空或在多个运行时样本中长度/内容变化；
- 资产显示或配置了可能的品质/耐久；
- 无法证明 `amount` 是普通堆叠数量；
- 存档中的资产 GUID 与当前 ID 对应资产不一致。

宁可少合并，也不能为了“看起来像资源”改变实例内容。

## 10. 实施顺序与测试矩阵

### 10.1 实施顺序

1. 先实现纯 C# 的 `ItemInstanceSemantics`、数据契约和旧条目隔离读取，不改 UI。
2. 改造 `InventoryBridge`、`VirtualStorageService`、`InfiniteStorageRuntime`，去除所有直接以 `item.amount` 作为通用仓库数量的代码。
3. 让自动拾取、制作适配、产物回退全部调用同一模块。
4. 最后接入卡片元数据和数量输入单位；展示层不得重新推断语义。
5. 构建、运行全部 Smoke、部署 DLL、比对源/目标哈希后，再进行真实单人游戏验收。

### 10.2 必须新增的自动测试

| 测试组 | 最低案例 |
|---|---|
| 资源单位 | 金属碎片不同 quality 默认值被忽略；指定数量可正常拆分和取回 |
| 弹药容器 | `40/40 + 40/40` 合并为 2 个；`40/40 + 80/80` 隔离；取 1 不改变单盒弹量 |
| 品质 | 枪、近战、服装和有耐久枪管的质量差异不合并 |
| 完整状态 | 枪附件、枪内弹量、射击模式、皮肤和任意 `state[]` 字节差异不合并 |
| 容器状态 | 油、水、发电机、储物、图书馆、路障的不同状态不合并且完整取回 |
| 事务 | 背包满、写盘失败、原生移除失败、制作失败均能恢复双方快照 |
| 迁移 | 资源旧条目可迁移；`Count > 1` 的旧弹药容器必须隔离而非猜测 |
| UI | 资源不显示虚假品质；弹药箱显示实例数与单盒剩余弹药；“取全部”和输入框单位正确 |

### 10.3 真实游戏验收

在关闭 BattlEye 的单人环境中，至少对民用弹药盒、一个普通弹匣、金属碎片、两把状态不同的枪、两件品质不同的服装、油桶/水容器、滤芯/医疗/食物、储物类和至少一个 Workshop 未知资产分别执行存入、重启、取出、制作/自动拾取相关路径。每项都要检查原生背包中的 `amount`、`quality`、`state[]` 结果，而非只观察卡片文字。

## 11. 依据与范围

- 官方稳定版物品文档说明 `Amount` 对容器类可表示最大容量，并以弹药箱作为例子；蓝图状态转移明确涉及弹药、品质、射击模式和燃料等实例信息。
- 官方弹匣文档说明 `Stuck > 0` 才会产生可见品质；服装文档明确服装始终显示品质。
- 当前项目的 `InventoryStateReader.TryReadAmmoCount` 已确认：弹匣及通过填装蓝图映射的 `ItemSupplyAsset` 使用 `Item.amount` 作为当前弹药，而枪械当前弹量位于 `state[10]`。
- 当前无限储存的 `StoreAllCarried`、`VirtualStorageService` 和 `Withdraw` 源码共同证实了本文件第 1 节的错误数据流。

本文件完成仅表示分类、迁移边界和验收标准已经整理完成。任何运行时代码修复都必须以本方案为准重新实现、构建、Smoke 验证、部署并完成真实游戏验收后，才能声明问题已修复。
