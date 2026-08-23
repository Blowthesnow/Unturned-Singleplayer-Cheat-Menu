# 物品与载具页面悬停参数 Tooltip 落地方案

> 文档性质：功能方案与实施清单
>
> 当前范围：只规划“鼠标悬停显示静态资产信息”，不读取世界中的实时实例状态。
>
> 信息原则：物品 Tooltip 以游戏内背包右键信息为基线，并在获取物品前提前展示；不为了“参数完整”而堆砌普通用户不需要的内部字段。
>
> 目标页面：物品页面、载具页面，以及复用同一套卡片的收藏页。
>
> 文档状态：方案已整理，尚未修改源码、尚未构建、尚未进行游戏内验收。

## 1. 需求定义

在物品或载具卡片上移动鼠标并持续停留 2 秒后，显示一个跟随鼠标、不会被屏幕边缘裁切的参数面板。

物品页、载具页和收藏页支持鼠标滚轮翻页：鼠标在页面内容区域内上滚进入上一页，下滚进入下一页。

Tooltip 的数据来自卡片对应的 `ItemAsset` 或 `VehicleAsset` 静态资产定义，不读取当前世界中已经生成的物品/载具实例。

因此，本需求明确不包含以下内容：

- 不显示某一辆已经生成载具的当前速度；
- 不显示某一辆已经生成载具的当前生命值、当前燃料或当前电池电量；
- 不监听 `InteractableVehicle` 的运行状态；
- 不因为玩家驾驶、受伤、加油或维修而刷新卡片 Tooltip；
- 不把“资产定义的最高速度”误标为“当前速度”。

## 2. 已确认的现状

### 2.1 页面和卡片入口

当前代码已经把物品和载具结果分别收集为 `ItemAsset`、`VehicleAsset`，并由统一的卡片入口绘制：

- `CheatMenuOverlayUi.BuildItemResults` / `BuildVehicleResults`：生成当前页面结果；
- `CheatMenuOverlayUi.CreateItemCard(Transform, ItemAsset)`：物品卡片；
- `CheatMenuOverlayUi.CreateVehicleCard(Transform, VehicleAsset)`：载具卡片；
- `CheatMenuOverlayUi.AddCardContents(...)`：统一绘制图标、名称和元信息；
- 收藏页也调用 `CreateItemCard` / `CreateVehicleCard`，因此可以自动获得相同的悬停行为。

当前卡片点击行为必须保持不变：物品仍然执行给予物品，载具仍然执行生成载具，收藏按钮仍然独立处理收藏状态。

### 2.2 已有的悬停基础设施

`CheatMenuOverlayUi` 已存在 `_hoverExplanationRoot`、`ShowHoverExplanation`、`HideHoverExplanation`；`OverlaySelectableVisual` 已实现：

- `SetHoverExplanation(...)`；
- `OnPointerEnter` 显示悬停内容；
- `OnPointerExit` 隐藏悬停内容；
- `OnDisable` 自动隐藏，避免切页或重建 UI 后残留 Tooltip。

因此本功能应扩展现有 Tooltip 层和卡片生命周期，不新增第二套鼠标捕获或窗口定位系统。

分页也应复用现有 `BuildPagination` 的页码和回调，不另外维护一套页码状态。

### 2.3 本地游戏与文档依据

- 目标游戏目录：`I:\Steam\steamapps\common\Unturned\`；
- 目标程序集存在：`I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll`；
- Unturned stable 官方手册已同步到 `$unturned-modding` skill 的 `references/official-docs`；
- `VehicleAsset` 文档明确提供 `Engine`、`Health`、`Health_Min`、`Health_Max`、`Fuel`、`Fuel_Min`、`Fuel_Max`、`Speed_Max`、`Trunk_Storage_X`、`Trunk_Storage_Y` 等静态字段；
- `ItemAsset` 通用文档明确提供 `GUID`、`ID`、`Type`、`Rarity`、`Quality_Min`、`Quality_Max`、`Size_X`、`Size_Y` 等字段；
- 现有 `WorkshopLocalization` 已覆盖物品名称、物品说明和载具名称，可继续复用。

## 3. 用户看到的界面

### 3.1 物品卡片 Tooltip：对齐背包右键信息

建议显示顺序如下：

Tooltip 不需要复制一套复杂的“资产编辑器面板”。首要目标是让用户在点击获取前，看到游戏内背包右键物品时会看到的名称、介绍和自动统计信息。

建议首版显示顺序：

```text
物品名称
物品自带介绍
游戏内原生自动统计信息
（例如伤害、容量、生命值等，按物品类型出现）
```

`ItemAsset` 的 `Description` 是物品在用户界面中使用的说明文本；官方文档还提供 `Use_Auto_Stat_Descriptions`，开启时游戏会把伤害、储物、生命值等自动统计追加到说明中。实现时应优先复用/生成与原生 `BuildDescription` 同语义的结果，而不是自行维护一份与背包右键不同的字段清单。

因此物品信息分两层：

| 层级 | 内容 | 规则 |
|---|---|---|
| 原生信息 | 名称、物品自带介绍、原生自动统计 | 与背包右键显示保持一致，优先级最高 |
| 使用前有用的信息 | 武器伤害/弹容量/默认配件/射速/兼容弹药；食物和饮料的增减效果 | 只补充用户在获取前确实需要知道的内容 |

不建议首版默认显示尺寸、品质范围、内部枚举、生成范围等编辑器/调试字段。它们可以作为后续“详细模式”，但不能挤占原生介绍和用户关心的使用效果。

专属字段不能通过“所有资产都有同名属性”的假设读取。实现阶段应按官方字段、实际程序集类型和原生描述构建路径建立类型适配器；无法确认的字段不显示，不用反射兜底猜值。

### 3.2 武器 Tooltip

武器是物品 Tooltip 的重点，首版建议显示：

```text
武器名称
武器自带介绍
────────────
伤害：玩家 / 僵尸 / 动物（按实际支持的伤害类型）
射程：最大射程；有衰减时显示衰减区间或最低倍率
弹容量：默认弹匣容量；无默认弹匣时显示“未配置默认弹匣”
射速：发/秒（同时保留自动/半自动/点射模式）
弹丸：每次射击弹丸/散射数量（适用时）
默认配件：枪口、握把、瞄具、战术配件、弹匣
兼容弹药：5.56mm、9mm、……
```

具体规则：

- 伤害不能只显示一个模糊的“Damage”，应按游戏实际伤害对象拆分；没有配置的对象隐藏；
- 射速使用官方 `Firerate` 语义换算。stable 文档给出的计算是 `50 ÷ (Firerate + 1)` 发/秒，不能把 `Firerate` 原值直接当成发/秒；
- 默认弹匣来自武器的默认 `Magazine` 引用。通过资产 ID/GUID 找到对应 `ItemMagazineAsset` 后，显示该弹匣的容量字段；找不到时保留“默认弹匣未解析”，不能猜一个容量；
- 默认配件来自武器的 `Barrel`、`Grip`、`Sight`、`Tactical`、`Magazine` 引用，解析成名称后显示。没有配置的槽位隐藏；
- 兼容弹药以当前 Unturned 已加载资产中的 `Ammo` 类型蓝图关系为主要来源，解析蓝图涉及的弹药/弹匣资产并显示名称；不扫描 Workshop 文件夹，也不把未加载的文件当成可用弹药；
- 武器兼容关系需要同时结合武器允许的 `Magazine_Caliber_#` / `Magazine_Calibers` 与已加载弹匣资产，避免把“同样出现在 Ammo 蓝图里但实际口径不兼容”的资产误列出来；
- 如果某个武器存在多种口径或多套 Ammo 蓝图，合并去重后按名称排序，并在无法完整解析时显示“部分兼容弹药已解析”，不能静默显示一个看似完整的错误列表；
- `Pellets`、射程、伤害衰减、爆炸等只在字段适用时显示；
- 只显示武器本体和默认配置，不把玩家当前实际装上的配件、当前弹药数量或当前耐久带入静态 Tooltip。

这里的“默认弹夹容量”和“武器自带什么配件”指资产定义的默认配置，目标是用户在获取前知道拿到手的大致性能，不是读取某个已生成实例的状态。

#### 兼容弹药解析规则

兼容弹药是武器 Tooltip 的独立数据项，建议建立一次性的 `AmmoCompatibilityIndex`：

```text
Assets.find(List<ItemAsset>)
        │
        ├─ 建立已加载资产 ID/GUID 索引
        ├─ 找出已加载的 ItemMagazineAsset
        ├─ 读取 Magazine_Caliber / Magazine_Calibers 关系
        ├─ 读取 Ammo 类型蓝图的 Supply / Product / Output 关系
        └─ 为每把 ItemGunAsset 生成去重后的兼容弹药列表
```

首选关系：

1. 当前已加载的 `ItemAsset` 中，找到与武器弹匣口径兼容的 `ItemMagazineAsset`；
2. 读取这些弹匣资产上已加载的 `Blueprint_#_Type Ammo` 蓝图；
3. 解析 Ammo 蓝图的输入物品和输出物品，定位实际弹药资产；
4. 只保留仍然满足武器弹匣口径关系的弹药；
5. 以弹药显示名称为主，必要时在次要行显示“通过某某弹匣兼容”。

`Ammo` 蓝图的具体输入/输出字段应按目标 `Assembly-CSharp.dll` 的实际对象结构核对，不能只按 `.dat` 文本字段硬编码。官方 stable 文档确认蓝图有 `Blueprint_#_Type`、`Blueprint_#_Supply_#_ID`、`Blueprint_#_Product` 或 `Blueprint_#_Output_#_ID` 等关系，但实现前仍需确认游戏运行时资产对象如何暴露这些关系。

兼容性边界：

- 只读取 `AssetCatalog` 已加载的 `ItemAsset`，不递归扫描磁盘 Workshop 目录；
- 不把普通 Craft 蓝图、拆解蓝图或与弹药无关的蓝图列为兼容弹药；
- 不把当前背包拥有的弹药数量带入 Tooltip；
- 不要求玩家先生成武器或弹匣，索引在目录刷新后即可供 Tooltip 使用；
- Ammo 蓝图关系和口径关系冲突时，以游戏实际可装配关系为准，并记录一次诊断日志。

### 3.3 食物、饮料和消耗品 Tooltip

食物和饮料不能只显示名称与一句泛化介绍，应把使用后会增加或减少的效果直接写出来：

```text
食物名称
物品自带介绍
────────────
食物：+X
饮水：+Y
生命：+Z
体力：+A
免疫：+B / 病毒：-C
氧气：+D / -D
体温：+E
状态：止血 / 治疗骨折 / 其他（适用时）
效果持续：N 秒（适用时）
```

显示约定：

- 增加用 `+`，减少用 `-`；
- 数值为 0 的行隐藏；
- `Food`、`Water`、`Health`、`Energy`、`Disinfectant`、`Virus`、`Oxygen`、`Warmth` 等按照官方 `ConsumeableAsset` 语义映射；
- `Bleeding_Modifier`、`Bones_Modifier` 等状态修正只有实际生效时显示；
- 食物没有专属字段时，读取其继承的 `ConsumeableAsset` 字段，不因 `ItemFoodAsset` 没有独立字段而放弃显示效果；
- 原生 Description 仍然完整保留，效果统计作为原生描述后的结构化补充或与原生自动统计合并，不能覆盖掉物品作者写的介绍。

### 3.4 载具卡片 Tooltip：只显示使用价值

载具页的首版信息固定为普通用户在选择载具时有用的静态规格，不显示载具生成配置：

```text
载具名称
────────────
类型：陆地车辆 / 飞机 / 直升机 / 船 / 其他
最高速度：56.25 km/h（资产 Speed_Max 换算）
生命值：100
储物空间：6 × 4（适用时）
载具自带能力/特殊说明（资产明确提供时）
```

字段定义：

| 显示项 | 数据来源 | 说明 |
|---|---|---|
| 类型 | `AssetCatalog.GetVehicleCategory(asset)` | 使用当前项目已经存在的分类逻辑 |
| 最高速度 | `VehicleAsset.speedMax` | 官方文档单位是 m/s；默认显示 km/h，可保留 m/s 辅助值 |
| 生命值 | `VehicleAsset.health` 或目标程序集对应的车辆最大生命字段 | 显示车辆本身的满状态生命值；不显示生成范围 |
| 储物空间 | `trunkStorageX` / `trunkStorageY` | 显示为列 × 行；为 0 时隐藏 |
| 特殊能力 | 目标程序集确认的静态能力字段 | 只显示对选择载具有帮助的能力，不展示内部调参 |

明确删除以下载具 Tooltip 内容：

- `Health_Min` / `Health_Max` 生成范围；
- `Fuel_Min` / `Fuel_Max` 初始燃油生成范围；
- 电池初始充电倍率、初始电量或电池消耗参数；
- 引擎 RPM、扭矩、齿轮比、转向摩擦等生成/调参信息；
- 任何“生成时会得到什么”的技术字段。

载具页面本身生成的就是满状态载具，所以 Tooltip 只回答“这是什么车、性能大致如何、能否装东西”，不回答“生成时初始化了哪些参数”。

速度换算必须统一：

- `km/h = m/s × 3.6`；
- 英里制不是首版必须项；如后续加入单位设置，再统一由格式化服务处理；
- 不直接把 `Speed_Max` 数值显示成 km/h，因为官方文档定义其原始单位为 m/s，且普通车辆还有游戏内部速度调整行为。

### 3.5 缺失字段和默认值

Tooltip 不应堆积大量 `0` 或空行：

- 车辆满状态生命值字段为 0 或无法确认时，隐藏生命值行；
- `Trunk_Storage_X/Y == 0`：隐藏储物空间；
- 不适用的专属字段不显示；
- 读取字段异常时，保留名称和物品自带介绍，并记录一次受控日志，不让整个卡片创建失败。

## 4. 技术设计

### 4.1 推荐的数据流

```text
CreateItemCard / CreateVehicleCard
        │
        ├─ 构造静态 HoverDetailsModel
        │       ├─ ItemHoverDetailsBuilder
        │       └─ VehicleHoverDetailsBuilder
        │
        ├─ AddCardContents 绘制卡片
        └─ OverlaySelectableVisual.SetHoverExplanation
                │
                └─ 现有 ShowHoverExplanation / HideHoverExplanation
```

推荐把“取字段”和“拼显示文本”从 UI 方法中抽离，避免继续扩大 `CreateItemCard` / `CreateVehicleCard` 的职责。

建议的最小类型：

- `AssetHoverDetails`：标题、说明、分组行、可选警告；
- `ItemHoverDetailsBuilder`：接收 `ItemAsset` 和本地化服务，返回静态模型；
- `VehicleHoverDetailsBuilder`：接收 `VehicleAsset` 和本地化服务，返回静态模型；
- `AssetHoverDetailsFormatter`：负责数字格式、单位、空值和本地化标签；
- `Tooltip` 渲染部分继续使用现有 `_hoverExplanationRoot`，首版可以先把模型格式化成多行文本，不必立即引入复杂的动态 prefab。

### 4.2 卡片绑定方式

`AddCardContents` 目前只接收名称、元信息、图标加载器和 `isVehicle`。建议增加一个可选的 Tooltip 文本或工厂参数，例如：

```text
AddCardContents(..., hoverText: BuildItemHoverText(asset))
AddCardContents(..., hoverText: BuildVehicleHoverText(asset))
```

卡片创建时生成一次静态文本即可。鼠标移动期间不重新读取资产字段，也不进行反射、扫描或网络请求。

### 4.3 悬停行为

- 鼠标进入卡片时显示 Tooltip；
- 鼠标从卡片移出时立即隐藏；
- 鼠标进入收藏星标时，Tooltip 不应因为星标按钮而闪烁或覆盖卡片 Tooltip；
- 切页、搜索重建、筛选重建、关闭菜单、对象销毁时统一隐藏；
- Tooltip 不拦截卡片点击，文本和背景 `raycastTarget` 应关闭，或将 Tooltip 放在非交互层；
- 继续使用现有屏幕边界夹紧逻辑；
- 长说明和多行参数使用换行，限制最大宽度和最大高度；
- 同一帧快速从一个卡片移到另一个卡片时，允许直接替换内容，不创建多个 Tooltip 根对象。

悬停 Tooltip 必须持续停留 2 秒才显示；隐藏不延迟。鼠标移出、移动到另一张卡片、切页、筛选、搜索重建或关闭页面时，立即取消计时并隐藏当前 Tooltip。

### 4.4 鼠标滚轮翻页

为物品页、载具页和收藏页的内容区域绑定页面级滚轮处理器：

| 滚轮方向 | 行为 |
|---|---|
| 上滚（`scrollDelta.y > 0`） | 上一页 |
| 下滚（`scrollDelta.y < 0`） | 下一页 |
| 首页继续上滚 | 不越界，保持第 1 页 |
| 末页继续下滚 | 不越界，保持最后一页 |

实现规则：

- 只在物品、载具或收藏页面的内容区域/卡片区域响应，不使用全局 `Input.GetAxis("Mouse ScrollWheel")` 抢占其他 UI 或游戏输入；
- 搜索框、数量输入框等文本输入控件获得焦点或鼠标位于其上时，不执行页面翻页；
- 收藏页按照当前选中的“物品收藏/车辆收藏”集合翻页，共用 `_favoritePage` 和现有分页回调；
- 滚轮触发翻页后立即隐藏 Tooltip 并取消 2 秒悬停计时，避免 Tooltip 留在已销毁的旧卡片上；
- 一次滚轮事件只移动一页，不因单次高分辨率滚轮事件重复跳多页；
- 翻页后保持当前筛选、搜索词、收藏类别和数量输入状态；
- 不改变底部上一页/下一页按钮，按钮和滚轮都调用同一个页码变更入口。

## 5. 文件影响范围（实现阶段）

以下是建议的最小变更范围，本次方案整理不修改这些文件：

| 文件 | 计划用途 |
|---|---|
| `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs` | 在物品/载具卡片绑定 Tooltip，复用现有悬停生命周期 |
| `src/UnturnedSingleplayerCheatMenu/UI/OverlaySelectableVisual.cs` 或其实际所在文件 | 仅在现有悬停组件不足时补充卡片级 Tooltip 绑定能力；优先不改通用行为 |
| `src/UnturnedSingleplayerCheatMenu/Services/AssetCatalog.cs` | 复用分类和资产身份逻辑；提供已加载 `ItemAsset` 索引入口 |
| `src/UnturnedSingleplayerCheatMenu/Services/AmmoCompatibilityIndex.cs` | 从已加载资产建立武器—弹匣—Ammo 蓝图—弹药关系 |
| `src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationService.cs` | 复用现有名称/说明翻译，不新增网络请求 |
| `src/UnturnedSingleplayerCheatMenu/Models/` | 放置 Tooltip 模型（如确有必要） |
| `src/UnturnedSingleplayerCheatMenu/UI/` | 增加页面内容区域滚轮导航组件或等价的事件绑定 |
| `tests/` 下新增或扩展 Smoke | 覆盖格式化、单位换算、空值隐藏和 Tooltip 不显示身份字段 |
| `docs/ITEM_VEHICLE_HOVER_TOOLTIP_PLAN.md` | 本方案与后续反向验收记录 |

不要在首版改动：

- 载具生成逻辑 `CheatActions.SpawnVehicles`；
- 物品给予逻辑 `CheatActions.GiveItem`；
- 收藏持久化；
- 图标缓存和载具缩略图渲染；
- PointTool 的世界目标详情逻辑；
- Workshop 翻译扫描协议。

## 6. 实施阶段拆分

### 阶段 A：字段审计

- 使用 `I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll` 确认 `ItemAsset`、各物品子类、`VehicleAsset` 的实际字段名、访问级别和单位；
- 将官方 stable 文档字段与实际程序集字段逐项对照；
- 对每个字段记录“公开属性 / 私有字段读取 / 不支持”结论；
- 重点确认 `ItemGunAsset` 的默认 Magazine、Magazine Caliber、默认附件和 Ammo 蓝图运行时集合；
- 重点确认 `ItemMagazineAsset` 的容量、口径以及 `Blueprint_#_Type == Ammo` 的输入/输出对象关系；
- 不能确认的字段暂不进入首版 Tooltip。

### 阶段 B：静态模型与格式化

- 实现物品和载具 Tooltip 的纯格式化逻辑；
- 完成名称、物品自带介绍和原生自动统计等基础信息；
- 完成载具速度换算、满状态生命值、储物空间和必要的实用能力字段；
- 建立已加载资产的 Ammo 兼容索引，并为武器生成兼容弹药名称列表；
- 用至少一把单口径武器、一把多口径武器、一种默认弹匣缺失武器和一个 Ammo 蓝图不完整样本验证解析结果；
- 处理默认值、空值、超长文本和异常字段；
- 不接 UI、不碰游戏运行状态，先用 Smoke 验证输出。

### 阶段 C：UI 绑定

- 在 `CreateItemCard`、`CreateVehicleCard` 生成 Tooltip 文本；
- 将 Tooltip 绑定到现有卡片悬停组件；
- 验证普通列表、收藏列表、搜索结果、分页重建都一致；
- 验证星标按钮仍然可点击，卡片点击仍然只执行原动作。

### 阶段 D：滚轮分页

- 为物品页、载具页和收藏页内容区域接入 `PointerEventData.scrollDelta`；
- 上滚调用上一页，下滚调用下一页；
- 复用底部分页按钮的同一页码回调，首尾页不越界；
- 验证输入框、筛选控件和收藏类别切换不会被滚轮误触发；
- 验证滚轮翻页会清理 Tooltip 和悬停计时。

### 阶段 E：本地化和视觉调整

- 新增 Tooltip 标签的中文/英文资源；
- 数值、单位、百分号和乘号使用稳定格式；
- 调整最大宽度、内边距、层级、屏幕边缘定位；
- 处理高 DPI、窗口缩放、窄窗口和长名称。

### 阶段 F：验证与反向审查

- 运行解决方案构建和相关 Smoke；
- 对照本方案逐项检查字段、页面、交互和限制；
- 部署 DLL 后，在目标 Unturned 实例中完成真实鼠标悬停验收；
- 单独记录“源码/构建证据”和“游戏内视觉交互证据”，不以构建通过代替游戏内验收。

## 7. 验收标准

### 7.1 物品页

- [ ] 鼠标进入物品卡片后持续停留 2 秒才显示 Tooltip，移出立即隐藏；
- [ ] Tooltip 包含名称、物品自带介绍和与背包右键一致的原生自动统计；
- [ ] Tooltip 不重复显示卡片底部已有的 ID、GUID、来源；
- [ ] 尺寸、品质、容量等非原生字段只在明确属于用户有用信息且适用时显示；
- [ ] 枪械、医疗、背包、建筑物品等不同类型不会因读取不适用字段而报错；
- [ ] 武器 Tooltip 显示伤害、默认弹匣容量、默认配件、射速和兼容弹药；
- [ ] 兼容弹药只来自当前已加载资产及其 Ammo 蓝图关系，不来自磁盘文件夹扫描；
- [ ] 单口径、多口径、默认弹匣缺失和蓝图关系不完整的武器都有明确结果；
- [ ] 食物/饮料显示使用后增加和减少的效果，0 值行隐藏；
- [ ] 鼠标移出后 Tooltip 消失；
- [ ] 卡片点击给予物品的原行为不变；
- [ ] 搜索、分页、筛选和收藏页均能显示正确对应卡片信息。
- [ ] 物品页内容区域上滚上一页、下滚下一页；首尾页不越界；

### 7.2 载具页

- [ ] 鼠标进入载具卡片后持续停留 2 秒才显示 Tooltip，移出立即隐藏；
- [ ] 显示载具类型和引擎类型；
- [ ] 显示最高速度，并明确单位为 km/h 或同时显示 m/s；
- [ ] 显示车辆满状态生命值和储物空间（适用时）；
- [ ] 不显示载具生成范围、初始燃油、初始电池或内部引擎调参；
- [ ] Tooltip 不重复显示卡片底部已有的 ID、GUID、来源；
- [ ] 不显示当前速度、当前生命值、当前燃料、当前电量；
- [ ] 卡片点击生成载具的原行为不变；
- [ ] 收藏载具和普通载具页面显示一致。
- [ ] 收藏页上滚上一页、下滚下一页，并分别适用于物品收藏和车辆收藏；

### 7.3 UI 与性能

- [ ] Tooltip 不挡住卡片点击，也不抢占游戏原生输入；
- [ ] 从一张卡片移动到另一张卡片时重新计时，不能沿用上一张卡片的 2 秒计时；
- [ ] 载具页内容区域上滚上一页、下滚下一页；首尾页不越界；
- [ ] 鼠标位于搜索框、数量输入框时滚轮不会改变页面；
- [ ] 滚轮翻页后旧 Tooltip 立即隐藏，新的卡片重新计算 2 秒悬停时间；
- [ ] 屏幕四边和不同缩放比例下不被裁切；
- [ ] 快速扫过大量卡片不会创建 Tooltip 对象洪泛或明显卡顿；
- [ ] Tooltip 显示不触发网络请求、Workshop 扫描或载具生成；
- [ ] 切页、关闭菜单、重建列表后没有残留 Tooltip；
- [ ] IMGUI 模式的限制被明确记录；项目当前为 uGUI 叠加 UI 时，至少完成默认 Glazier/uGUI 验收。

## 8. 风险与决策

| 风险 | 处理决策 |
|---|---|
| 不同 Unturned 版本字段名或访问级别变化 | 以目标 `Assembly-CSharp.dll` 和 stable 文档双重核对；字段不可确认就隐藏 |
| `Health`/`Fuel` 的语义容易被误解为当前值 | 载具 Tooltip 只显示满状态生命值和有实际用途的静态规格，不显示生成范围与初始燃油/电池 |
| Ammo 蓝图关系不完整或不同版本暴露方式不同 | 以目标程序集审计结果为准；只显示已确认关系，显示部分解析状态，不猜测 |
| `Speed_Max` 原始单位为 m/s | 在格式化层统一换算，测试固定样例，禁止 UI 层重复换算 |
| 某些资产字段为 0 或不适用 | 隐藏行或显示“未配置”，不输出无意义的 0 |
| Workshop 载具没有通用说明字段 | 首版只显示载具名称和静态参数，不虚构说明文本 |
| 现有 HoverExplanation 是单行设计 | 先扩展为多行文本；若视觉上不足，再单独拆 `AssetTooltipView`，不重做整套 UI |
| 悬停延迟造成快速扫卡片时误显示 | 使用每张卡片独立的 2 秒计时；离开、禁用、切页和切换目标时取消计时 |
| 页面滚轮与输入框或游戏输入冲突 | 仅在页面内容区域处理 `PointerEventData.scrollDelta`，输入框和非目标页面不消费滚轮 |
| 卡片重建时 Tooltip 引用旧对象 | 在 `ShowTab`、关闭和 `OnDisable` 路径统一调用隐藏逻辑 |
| Tooltip 覆盖收藏星标 | 星标保持更高交互优先级，Tooltip 视觉层不接收 Raycast |

## 9. 完成定义

本功能只有同时满足以下条件，才可称为“已完成”：

1. 物品页和载具页都已接入静态 Tooltip；
2. 普通列表和收藏列表都覆盖；
3. 载具速度、满状态生命值、储物空间的单位和缺省值处理正确，且没有生成参数噪声；
4. 武器兼容弹药来自已加载资产的 Ammo 蓝图关系，并经过弹匣口径约束；
5. 物品页、载具页和收藏页支持上滚上一页、下滚下一页，且首尾不越界；
6. 明确没有实时状态层，不读取 `InteractableVehicle` 当前运行数据；
7. 构建与 Smoke 通过；
8. 目标 Unturned 实机完成鼠标悬停、移出、点击、滚轮翻页、搜索、收藏和屏幕边缘验收；
9. 方案逐项反向审查，未完成项和环境限制仍保留在验收台账中。

## 10. 本轮结论

该需求适合在现有 `CheatMenuOverlayUi` 上做局部扩展，核心工作是“静态资产字段审计 + Tooltip 文本模型 + 现有 Hover 层绑定”。不需要接入实时载具状态，也不需要修改物品给予、载具生成、收藏和图标渲染逻辑。

下一步若进入实现，应先完成阶段 A 的程序集字段审计，再实现最小的物品/载具通用字段 Tooltip，最后逐步加入各物品类型专属参数。
