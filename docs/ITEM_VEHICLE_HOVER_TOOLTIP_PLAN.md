# 物品、载具与收藏页面 Tooltip 及滚轮翻页方案

> 文档性质：功能落地方案
>
> 目标项目：`D:\Unturned BepInEx`
>
> 目标游戏：`I:\Steam\steamapps\common\Unturned\`
>
> 当前状态：已完成源码落地、Release 构建、9 个 Smoke、目标 DLL 部署、日志加载检查及用户实机视觉/交互验收。

## 1. 最终需求范围

为插件的物品页面、载具页面和收藏页面增加卡片悬停 Tooltip，并支持鼠标滚轮翻页。

### 1.1 Tooltip 行为

- 鼠标进入物品或载具卡片后，必须持续停留 **2 秒**才显示 Tooltip；
- 鼠标移出卡片立即隐藏 Tooltip；
- 移动到另一张卡片时，取消上一张卡片的计时并重新计时 2 秒；
- 切页、搜索、筛选、收藏类别切换、关闭菜单或重建列表时，立即隐藏 Tooltip 并取消计时；
- Tooltip 使用现有悬停显示层，不新增全局鼠标捕获系统；
- Tooltip 不显示卡片底部已经显示的 ID、GUID、来源；
- Tooltip 不读取当前世界中的物品实例或载具实例状态。

### 1.2 鼠标滚轮翻页

物品页面、载具页面和收藏页面的内容区域支持滚轮翻页：

| 操作 | 行为 |
|---|---|
| 鼠标上滚 | 上一页 |
| 鼠标下滚 | 下一页 |
| 首页继续上滚 | 保持首页，不越界 |
| 末页继续下滚 | 保持末页，不越界 |

滚轮翻页必须复用现有底部“上一页/下一页”按钮的页码回调，不维护第二套页码逻辑。

## 2. 当前代码基础

当前 UI 已经具备较好的接入点：

- `CheatMenuOverlayUi.BuildItemResults`：生成物品结果；
- `CheatMenuOverlayUi.BuildVehicleResults`：生成载具结果；
- `CheatMenuOverlayUi.BuildFavoriteResults`：生成收藏结果；
- `CheatMenuOverlayUi.CreateItemCard(Transform, ItemAsset)`：创建物品卡片；
- `CheatMenuOverlayUi.CreateVehicleCard(Transform, VehicleAsset)`：创建载具卡片；
- `CheatMenuOverlayUi.AddCardContents(...)`：统一绘制卡片图标、名称和底部元信息；
- `CheatMenuOverlayUi.BuildPagination(...)`：现有分页按钮和页码变更入口；
- `CheatMenuOverlayUi` 已有 `_hoverExplanationRoot`、`ShowHoverExplanation`、`HideHoverExplanation`；
- `OverlaySelectableVisual` 已有 `OnPointerEnter`、`OnPointerExit` 和悬停说明回调；
- 物品、载具和收藏页分别维护 `_itemPage`、`_vehiclePage`、`_favoritePage`。

因此本需求适合在当前 Overlay UI 上做局部扩展：卡片增加 Tooltip 内容，页面内容区域增加滚轮事件，原有点击和分页按钮继续保留。

## 3. Tooltip 总体原则

Tooltip 的主要目标是让用户在点击“获取物品”或“生成载具”之前，提前知道这件物品或车辆的实际用途。

Tooltip 不做成资产编辑器或调试面板，不展示普通用户用不到的内部字段。

### 3.1 不显示的内容

以下内容不放入 Tooltip：

- ID；
- GUID；
- 资产来源、原版/模组标签；
- 载具生成范围；
- 载具初始燃油、初始电池、电池充电倍率；
- 引擎 RPM、扭矩、齿轮比、内部摩擦等调参字段；
- 当前载具速度；
- 当前载具生命值；
- 当前燃油或电池电量；
- 当前背包数量、当前弹药数量或当前物品耐久。

ID、GUID 和来源继续由卡片底部现有元信息显示；它们只允许在内部作为资产索引键使用，不得重复输出到 Tooltip。

## 4. 物品 Tooltip

### 4.1 基线：复现背包右键看到的信息

物品 Tooltip 的基础内容应以游戏内背包中右键物品时显示的内容为准：

```text
物品名称
物品自带介绍
游戏内原生自动统计
```

优先复用或生成与 Unturned 原生 `ItemAsset.BuildDescription` 同语义的描述结果，不自行制作一份会与背包右键内容逐渐不一致的普通字段列表。

Unturned stable 文档中的 `ItemAsset.Description` 是物品在 UI 中使用的说明文本；`Use_Auto_Stat_Descriptions` 可以让伤害、储物、生命值等自动统计追加到说明。实现时应保留原始介绍，再补充结构化参数。

### 4.2 普通物品

普通物品首版只需要显示：

- 物品名称；
- 物品自带介绍；
- 原生自动统计；
- 对获取前有帮助、且原生描述没有覆盖的少量实用参数。

不默认显示尺寸、品质范围、内部枚举或其他编辑器字段。只有明确对玩家有用且该物品类型适用时，才增加一行参数。

### 4.3 武器

武器是物品 Tooltip 的重点。建议显示：

```text
武器名称
武器自带介绍
────────────
伤害：按游戏实际伤害对象显示
射程：最大射程；有伤害衰减时显示有效衰减信息
默认弹匣容量：N 发
射速：N 发/秒
射击模式：自动 / 半自动 / 点射 / 其他
弹丸数量：N（适用时）
默认配件：枪口、握把、瞄具、战术配件、弹匣
兼容弹药：弹药名称列表
```

规则：

- 伤害按游戏实际支持的对象拆分，例如玩家、僵尸、动物或其他适用目标；未配置的伤害类型隐藏；
- 射速根据目标程序集中的武器字段和 stable 文档语义换算，不能把 `Firerate` 原值直接当成发/秒；
- 默认弹匣通过武器的默认 `Magazine` 引用解析；找到对应弹匣后显示它的容量；
- 默认配件通过武器的 `Barrel`、`Grip`、`Sight`、`Tactical`、`Magazine` 等默认引用解析成名称；没有配置的槽位隐藏；
- `Pellets`、射程、伤害衰减、爆炸、每发消耗弹药数量等只在适用时显示；
- 武器资产的 `Infinite_Ammo` 为 `true` 时显示“弹药：无限子弹”，不再显示 Ammo 蓝图缺失提示；
- 只显示武器资产定义的默认配置，不读取玩家当前实际装上的配件或当前弹药数量；
- 解析失败时只隐藏无法解析的行，不让整张卡片创建失败。

### 4.4 兼容弹药

武器 Tooltip 必须显示“这把武器能用什么弹药”。兼容弹药的主要依据是 **Unturned 当前已加载资产中的 Ammo 蓝图关系**，并结合武器的弹匣口径关系进行过滤。

建议建立一次性的 `AmmoCompatibilityIndex`：

```text
Assets.find(List<ItemAsset>)
        │
        ├─ 建立当前已加载 ItemAsset 索引
        ├─ 找出已加载的 ItemGunAsset 和 ItemMagazineAsset
        ├─ 读取武器 Magazine_Caliber / Magazine_Calibers
        ├─ 读取弹匣上的 Blueprint_#_Type = Ammo
        ├─ 解析 Ammo 蓝图的 Supply / Product / Output 关系
        ├─ 过滤与武器口径不兼容的结果
        └─ 生成武器 → 兼容弹药名称列表
```

解析规则：

1. 只读取当前 `AssetCatalog` 已加载的 `ItemAsset`；
2. 不递归扫描磁盘 Workshop 目录；
3. 不把未加载的 `.dat` 文件或资源文件当成可用弹药；
4. 读取 Ammo 类型蓝图涉及的输入物品和输出物品；
5. 通过弹匣口径关系确认该弹药确实可以服务于目标武器；
6. 多个弹匣或多套蓝图产生重复结果时按资产对象去重，再按显示名称排序；
7. 蓝图关系不完整时显示已确认的部分结果，并记录诊断信息，不猜测缺失的弹药；
8. Ammo 蓝图与口径关系冲突时，以游戏实际可装配关系为准。
9. “未找到 Ammo 蓝图关系”不等于无限弹药；无限弹药只依据目标程序集公开的 `ItemGunAsset.infiniteAmmo`（资产键 `Infinite_Ammo`）判定。

官方 stable 文档确认蓝图存在 `Blueprint_#_Type`、`Blueprint_#_Supply_#_ID`、`Blueprint_#_Product`、`Blueprint_#_Output_#_ID` 等关系；阶段 A 仍需针对目标 `Assembly-CSharp.dll` 确认这些关系在运行时资产对象上的实际访问方式。

### 4.5 食物、饮料和消耗品

食物 Tooltip 必须显示吃下去以后会增加什么、减少什么：

```text
食物名称
物品自带介绍
────────────
食物：+X
饮水：+Y
生命：+Z
体力：+A
免疫：+B
病毒：-C
氧气：+D / -D
体温：+E
状态：止血 / 治疗骨折 / 其他
效果持续：N 秒（适用时）
```

显示规则：

- 增加使用 `+`，减少使用 `-`；
- 数值为 0 的行隐藏；
- 读取 `ConsumeableAsset` 继承字段，例如 `Food`、`Water`、`Health`、`Energy`、`Disinfectant`、`Virus`、`Oxygen`、`Warmth`；
- `Bleeding_Modifier`、`Bones_Modifier` 等状态修正只在实际生效时显示；
- `ItemFoodAsset` 没有独立字段时，继续读取其父类消耗品字段；
- 物品作者写的 Description 必须完整保留，结构化效果放在介绍之后或与原生自动统计合并。

## 5. 载具 Tooltip

载具页面生成的载具本身就是满状态。Tooltip 只显示普通用户选择载具时有用的信息，不显示生成配置。

建议显示：

```text
载具名称
────────────
类型：陆地车辆 / 固定翼飞机 / 直升机 / 船 / 火车 / 其他
最高速度：N km/h
生命值：N
储物空间：X × Y（适用时）
实用能力或特殊说明（资产明确提供时）
```

字段规则：

| 显示项 | 规则 |
|---|---|
| 类型 | 复用 `AssetCatalog.GetVehicleCategory(asset)` |
| 最高速度 | 使用 `VehicleAsset.Speed_Max` 对应运行时字段，按 `m/s × 3.6` 换算为 km/h |
| 生命值 | 显示车辆满状态生命值，不显示生成范围 |
| 储物空间 | 显示 `Trunk_Storage_X × Trunk_Storage_Y`，未配置时隐藏 |
| 实用能力 | 只显示用户选择车辆时有帮助的静态能力 |

明确不显示：

- `Health_Min` / `Health_Max` 生成范围；
- `Fuel_Min` / `Fuel_Max` 初始燃油范围；
- 电池初始电量、充电倍率和消耗参数；
- 引擎 RPM、扭矩、齿轮比、转向摩擦等内部调参；
- 当前速度、当前生命值、当前燃油或当前电量；
- 任何“生成时初始化了什么”的技术信息。

## 6. Tooltip UI 设计

### 6.1 数据流

```text
CreateItemCard / CreateVehicleCard
        │
        ├─ ItemHoverDetailsBuilder
        │       ├─ 原生物品 Description
        │       ├─ 武器参数 / AmmoCompatibilityIndex
        │       └─ 食物效果
        │
        ├─ VehicleHoverDetailsBuilder
        │       └─ 载具实用静态规格
        │
        └─ OverlaySelectableVisual.SetHoverExplanation
                │
                └─ 现有 ShowHoverExplanation / HideHoverExplanation
```

建议类型：

- `AssetHoverDetails`：标题、说明、参数分组和可选警告；
- `ItemHoverDetailsBuilder`：物品通用信息、原生 Description、武器/食物专属信息；
- `VehicleHoverDetailsBuilder`：载具实用静态规格；
- `AmmoCompatibilityIndex`：当前已加载资产的武器—弹匣—Ammo 蓝图—弹药关系；
- `AssetHoverDetailsFormatter`：负责中文/英文标签、数值格式、单位和空值隐藏。

Tooltip 文本和参数应在卡片创建时生成一次。鼠标悬停期间不进行反射扫描、网络请求、Workshop 文件扫描或资产重建。

### 6.2 2 秒悬停计时

建议在卡片级悬停组件或 Tooltip 控制器中加入延迟状态：

- `OnPointerEnter`：记录当前卡片和时间，启动 2 秒计时；
- 2 秒内未离开：显示当前卡片 Tooltip；
- `OnPointerExit`：取消计时并立即隐藏；
- 切换到另一张卡片：取消旧卡片计时，重新开始 2 秒；
- `OnDisable`、关闭菜单和列表重建：统一取消计时并隐藏。

隐藏不延迟，显示才延迟。

### 6.3 滚轮翻页

物品页、载具页和收藏页内容区域绑定页面级滚轮事件：

| 滚轮方向 | 行为 |
|---|---|
| `scrollDelta.y > 0` | 上一页 |
| `scrollDelta.y < 0` | 下一页 |

实现规则：

- 优先使用页面内容区域的 `PointerEventData.scrollDelta`，不使用全局 `Input.GetAxis("Mouse ScrollWheel")`；
- 搜索框、数量输入框等文本输入控件获得焦点或鼠标位于其上时，不翻页；
- 收藏页按照当前选择的物品收藏/车辆收藏集合翻页；
- 一次滚轮事件只移动一页；
- 首尾页不越界；
- 翻页后立即隐藏旧 Tooltip 并取消 2 秒计时；
- 翻页保持当前搜索词、筛选分类、收藏类别和数量输入状态；
- 底部上一页/下一页按钮继续保留，并与滚轮调用同一个页码回调。

## 7. 建议变更范围

本次文档不修改以下源码。进入实现阶段时建议控制在这些范围内：

| 文件/目录 | 作用 |
|---|---|
| `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs` | 卡片 Tooltip 绑定、页面滚轮翻页接入、分页状态复用 |
| `src/UnturnedSingleplayerCheatMenu/UI/` | 卡片 2 秒悬停计时或页面内容区域滚轮事件组件 |
| `src/UnturnedSingleplayerCheatMenu/Services/AssetCatalog.cs` | 提供当前已加载资产索引，不重复扫描磁盘 |
| `src/UnturnedSingleplayerCheatMenu/Services/AmmoCompatibilityIndex.cs` | 建立武器兼容弹药关系 |
| `src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationService.cs` | 复用物品名称和 Description 翻译，不新增网络请求 |
| `src/UnturnedSingleplayerCheatMenu/Models/` | Tooltip 数据模型 |
| `tests/` | 格式化、Ammo 解析、分页边界和输入冲突 Smoke |
| `docs/ITEM_VEHICLE_HOVER_TOOLTIP_PLAN.md` | 本方案和实施后反向验收记录 |

首版不要改动：

- `CheatActions.GiveItem`；
- `CheatActions.SpawnVehicles`；
- 收藏持久化；
- 图标缓存和载具缩略图渲染；
- PointTool 世界目标详情；
- Workshop 翻译扫描协议。

## 8. 实施阶段

### 阶段 A：目标程序集字段审计

- 使用 `I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll` 确认 `ItemAsset`、物品子类和 `VehicleAsset` 的实际字段名、访问级别和单位；
- 核对 `ItemAsset.BuildDescription` 与背包右键显示的调用路径；
- 确认 `ItemGunAsset` 的伤害、射速、默认弹匣、默认配件、口径和 Ammo 蓝图运行时对象；
- 确认 `ItemMagazineAsset` 的容量、口径以及 Ammo 蓝图输入/输出关系；
- 确认食物和饮料从 `ConsumeableAsset` 继承的实际字段；
- 确认车辆满状态生命值、最高速度和储物空间的运行时字段；
- 不能确认的字段不进入首版 Tooltip。

### 阶段 B：纯数据模型与格式化

- 先实现不依赖 UI 的 Tooltip 数据模型；
- 复用原生物品 Description；
- 实现武器、食物和载具参数格式化；
- 建立 AmmoCompatibilityIndex；
- 覆盖缺失字段、0 值、重复关系、部分解析和异常对象；
- 用 Smoke 验证上滚/下滚页码边界与 Ammo 解析结果。

### 阶段 C：卡片 Tooltip 接入

- 在 `CreateItemCard` 和 `CreateVehicleCard` 生成静态 Tooltip 文本；
- 使用现有悬停组件绑定 Tooltip；
- 加入 2 秒显示计时；
- 验证普通列表、搜索结果、分页结果和收藏列表一致；
- 确认 Tooltip 不遮挡卡片点击和收藏星标。

### 阶段 D：滚轮翻页接入

- 为物品页、载具页和收藏页内容区域接入 `PointerEventData.scrollDelta`；
- 上滚调用上一页，下滚调用下一页；
- 复用底部按钮页码回调；
- 首尾页不越界；
- 输入框、筛选控件和非目标页面不消费滚轮；
- 滚轮翻页时清理 Tooltip 和悬停计时。

### 阶段 E：本地化、视觉和实机验收

- 添加 Tooltip 标签的中文/英文文本；
- 调整多行宽度、最大高度、边缘定位和高 DPI 表现；
- 运行解决方案构建和相关 Smoke；
- 部署到目标 Unturned 后完成真实鼠标悬停、滚轮、点击和输入框验收；
- 单独记录源码/构建证据与游戏内视觉交互证据。

## 9. 验收标准

### 9.1 Tooltip 通用验收

- [x] 物品和载具卡片持续悬停 2 秒后显示 Tooltip；
- [x] 移出卡片立即隐藏；
- [x] 移动到另一张卡片时重新计时；
- [x] Tooltip 不重复显示卡片底部已有的 ID、GUID、来源；
- [x] Tooltip 不读取实时载具状态；
- [x] 切页、搜索、筛选、收藏类别切换和关闭菜单后没有残留 Tooltip；
- [x] Tooltip 不阻挡卡片点击、收藏星标或游戏原生输入。

### 9.2 物品页验收

- [x] 显示物品名称、物品自带介绍和与背包右键一致的原生自动统计；
- [x] 武器显示伤害、默认弹匣容量、默认配件、射速和兼容弹药；
- [x] `Infinite_Ammo` 武器明确标注“无限子弹”，且不会因 Ammo 蓝图结果为空误标普通武器；
- [x] 兼容弹药只来自当前已加载资产及 Ammo 蓝图关系；
- [x] 单口径、多口径、默认弹匣缺失和蓝图不完整的武器都有明确结果；
- [x] 食物和饮料显示使用后增加和减少的效果；
- [x] 0 值效果行隐藏；
- [x] 搜索、分类筛选、分页和收藏入口显示的 Tooltip 内容一致。

### 9.3 载具页验收

- [x] 显示车辆类型；
- [x] 显示最高速度，并明确单位；
- [x] 显示车辆满状态生命值；
- [x] 适用时显示储物空间和实用特殊能力；
- [x] 不显示载具生成范围、初始燃油、初始电池或内部引擎调参；
- [x] 收藏车辆和普通车辆页面显示一致。

### 9.4 滚轮翻页验收

- [x] 物品页上滚上一页、下滚下一页；
- [x] 载具页上滚上一页、下滚下一页；
- [x] 收藏页上滚上一页、下滚下一页；
- [x] 收藏页分别适用于物品收藏和车辆收藏；
- [x] 首页/末页不越界；
- [x] 一次滚轮事件只翻一页；
- [x] 搜索框和数量输入框内滚轮不会改变页面；
- [x] 滚轮翻页后旧 Tooltip 立即消失，新卡片重新等待 2 秒。

### 9.5 性能与兼容性验收

- [x] Tooltip 创建不触发网络请求、磁盘 Workshop 扫描或载具生成；
- [x] Ammo 关系索引在资产刷新后建立，不在每一帧扫描；
- [x] 快速扫过大量卡片不会产生对象洪泛或明显卡顿；
- [x] 默认 uGUI/Glazier 配置下通过真实 UI 验收；
- [ ] IMGUI 模式若存在能力限制，必须记录为明确的兼容性限制。
> 验收证据：源码/构建/Smoke/部署证据已在本次实施中完成；目标 Unturned 于 2026 年 8 月 24 日加载 UnturnedSingleplayerCheatMenu 1.7.0，并由用户通过 13 张实机截图完成物品、武器、弹药、配件、衣物、食品、饮料、医疗、收藏、载具和跨页 Tooltip 验收。IMGUI 模式未单独运行，记录为兼容性限制；本次验收覆盖默认 uGUI/Glazier 路径。
> 说明：部分模组作者自带 Description 文本本身包含 `Id/ID` 字样（截图中的原文内容），插件没有向 Tooltip 追加卡片底部的资产 ID、GUID 或来源元信息。
## 10. 风险与处理

| 风险 | 处理方式 |
|---|---|
| 原生 Description 与 Tooltip 文本重复或不一致 | 复用 `BuildDescription` 语义，结构化参数只做补充 |
| 不同物品类型字段不同 | 按实际资产子类型建立适配器，不读取不存在的字段 |
| Ammo 蓝图关系在不同版本中的运行时暴露方式不同 | 先审计目标程序集；只能显示已确认的关系，不能猜测 |
| Ammo 蓝图与口径关系冲突 | 以实际可装配关系为准，并记录部分解析状态 |
| 2 秒计时残留 | 卡片移出、切页、重建和禁用时统一取消计时 |
| 滚轮误触发输入框或游戏操作 | 只在目标页面内容区域处理 `PointerEventData.scrollDelta` |
| 滚轮重复跳页 | 对单次事件做一次性消费或节流，确保一次只移动一页 |
| Tooltip 遮挡星标或卡片点击 | Tooltip 视觉层不接收 Raycast，保留卡片和星标交互优先级 |

## 11. 完成定义

只有同时满足以下条件，才算功能完整落地：

1. 物品页、载具页和收藏页都有静态 Tooltip；
2. Tooltip 持续悬停 2 秒后显示，移出立即隐藏；
3. Tooltip 不显示 ID、GUID、来源；
4. 物品 Tooltip 与背包右键原生信息保持一致，并保留物品自带介绍；
5. 武器显示伤害、默认弹匣容量、默认配件、射速和兼容弹药；
6. 食物和饮料显示使用后增加和减少的效果；
7. 兼容弹药来自当前已加载资产的 Ammo 蓝图关系，并经过口径约束；
8. 载具只显示普通用户有用的满状态规格，不显示生成参数或实时状态；
9. 物品、载具和收藏页面支持上滚上一页、下滚下一页，且首尾不越界；
10. 构建、Smoke 和目标 Unturned 实机验收均完成；
11. 方案逐项反向审查，未验证项目继续明确记录。
