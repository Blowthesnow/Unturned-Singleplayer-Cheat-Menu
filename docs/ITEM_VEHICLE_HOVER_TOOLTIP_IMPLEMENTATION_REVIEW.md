# 物品与载具页面悬停 Tooltip 实施审查

> 审查时间：2026-08-24
> 文档依据：ITEM_VEHICLE_HOVER_TOOLTIP_PLAN.md
> 审查方式：源码 + 编译 + 烟雾测试 + 待游戏内验收

## 1. 阶段 A：程序集字段审计

### 1.1 ItemAsset 通用字段
- [x] itemName (已在现有代码中使用)
- [x] id (已在现有代码中使用)
- [x] type (已在现有代码中使用)
- [x] rarity (已在现有代码中使用)
- [x] Size_X, Size_Y (文档确认但未在 tooltip 中显示 - 按计划首版不显示)
- [x] Quality_Min, Quality_Max (文档确认但未在 tooltip 中显示 - 按计划首版不显示)

### 1.2 ItemGunAsset 专用字段
- [x] range (已在 AssetTooltipFormatter 中使用)
- [⚠️] magazineCapacity / magazineReplacements (API 不确定，已移除)
- [⚠️] 弹药兼容性 (文档规划但未实现 - 需要 Ammo 蓝图关系审计)

### 1.3 VehicleAsset 字段
- [x] vehicleName (已在现有代码中使用)
- [x] id (已在现有代码中使用)
- [x] engine (已在现有代码中使用)
- [x] TargetForwardVelocity (已在 AssetTooltipFormatter 中使用，替代过时的 speedMax)
- [⚠️] passengers (API 不确定，已移除)
- [⚠️] Health/Health_Min/Health_Max (文档规划但未实现)
- [⚠️] Trunk_Storage_X/Trunk_Storage_Y (文档规划但未实现)

### 1.4 食物/医疗/消耗品字段
- [⚠️] 食物/饮料效果 (文档规划但未实现)
- [⚠️] 医疗物品恢复量 (文档规划但未实现)

**状态：部分完成**
- 通用字段已验证
- 部分专用字段因 API 不确定性被简化
- 武器弹药关系、食物效果等高级特性未实现

---

## 2. 阶段 B：Tooltip 文本格式化

### 2.1 AssetTooltipFormatter.cs
- [x] 文件已创建：`src/UnturnedSingleplayerCheatMenu/UI/AssetTooltipFormatter.cs`
- [x] FormatItemTooltip 方法实现
- [x] FormatVehicleTooltip 方法实现
- [x] 物品类型本地化 (武器、近战、食物、医疗)
- [x] 稀有度本地化 (普通、罕见、稀有、史诗、传说、神话)
- [x] 使用 StringBuilder 构建多行文本
- [x] 使用 Unity Rich Text 标签 (<b>, <color>)
- [⚠️] 简化版本：仅显示基础信息，未包含武器详细参数、食物效果等

**状态：基础完成，高级特性待补充**

---

## 3. 阶段 C：卡片悬停触发器

### 3.1 AssetCardTooltipTrigger.cs
- [x] 文件已创建：`src/UnturnedSingleplayerCheatMenu/UI/AssetCardTooltipTrigger.cs`
- [x] 实现 IPointerEnterHandler
- [x] 实现 IPointerExitHandler
- [x] Initialize 方法接受正确的回调签名 `Action<string, RectTransform, Vector2>`
- [x] OnPointerEnter 传递 tooltipText, RectTransform, eventData.position
- [x] OnPointerExit 隐藏 tooltip
- [x] OnDisable 自动隐藏，避免切页残留
- [⚠️] **未实现 2 秒延迟** - 当前是立即显示

**状态：基础完成，缺少 2 秒悬停延迟**

---

## 4. 阶段 D：集成到现有卡片

### 4.1 CheatMenuOverlayUi.cs - CreateItemCard
- [x] 在 AddFavoriteButton 之后插入 tooltip 触发器
- [x] 调用 AssetTooltipFormatter.FormatItemTooltip
- [x] 添加 AssetCardTooltipTrigger 组件
- [x] 传递正确的回调 (ShowHoverExplanation, HideHoverExplanation)
- [x] 检查 tooltipText 非空

### 4.2 CheatMenuOverlayUi.cs - CreateVehicleCard
- [x] 在 AddFavoriteButton 之后插入 tooltip 触发器
- [x] 调用 AssetTooltipFormatter.FormatVehicleTooltip
- [x] 添加 AssetCardTooltipTrigger 组件
- [x] 传递正确的回调
- [x] 检查 tooltipText 非空

### 4.3 收藏页自动继承
- [x] 收藏页调用相同的 CreateItemCard/CreateVehicleCard
- [x] 自动获得相同的 tooltip 行为

**状态：完成**

---

## 5. 编译与烟雾测试

### 5.1 编译结果
- [x] Release 构建成功
- [x] DLL 已复制到 `I:\Steam\steamapps\common\Unturned\BepInEx\plugins\`
- [x] 源文件与目标文件哈希一致

### 5.2 烟雾测试结果 (10/10 通过)
- [x] FavoritesSerializationSmoke
- [x] InventoryArrangePlannerSmoke
- [x] ItemFilteringSmoke
- [x] LocalizationSmoke
- [x] MovementSpeedSmoke
- [x] PointToolActionSmoke
- [x] ShortcutToggleSmoke
- [x] TeleportSerializationSmoke
- [x] VehicleThumbnailSettingsSmoke
- [x] WorkshopLocalizationSmoke

**状态：全部通过**

---

## 6. 文档第 7 节验收台账对照

### 7.1 物品页
- [ ] **鼠标进入卡片后持续停留 2 秒才显示** - ❌ 未实现延迟
- [x] 显示物品名称
- [x] 显示 ID 或 GUID
- [x] 显示物品类型 (武器、近战、食物、医疗)
- [x] 显示稀有度
- [ ] 武器显示伤害、弹容量、射程、默认配件、兼容弹药 - ⚠️ 仅显示射程
- [ ] 食物/饮料显示使用后效果 - ❌ 未实现
- [x] 鼠标移出后 Tooltip 消失
- [x] 卡片点击给予物品的原行为不变
- [ ] 搜索、分页、筛选和收藏页均能显示正确对应卡片信息 - ⚠️ 需游戏内验收
- [ ] 物品页内容区域上滚上一页、下滚下一页 - ❌ 未实现滚轮翻页

### 7.2 载具页
- [ ] **鼠标进入载具卡片后持续停留 2 秒才显示** - ❌ 未实现延迟
- [x] 显示载具类型和引擎类型
- [x] 显示最高速度，并明确单位为 km/h
- [ ] 显示车辆满状态生命值 - ❌ 未实现
- [ ] 显示储物空间 - ❌ 未实现
- [x] 不显示当前速度、当前生命值、当前燃料、当前电量
- [x] 卡片点击生成载具的原行为不变
- [ ] 收藏载具和普通载具页面显示一致 - ⚠️ 需游戏内验收
- [ ] 载具页上滚上一页、下滚下一页 - ❌ 未实现滚轮翻页
- [ ] 收藏页上滚上一页、下滚下一页 - ❌ 未实现滚轮翻页

### 7.3 UI 与性能
- [x] Tooltip 不挡住卡片点击 (使用现有 HoverExplanation 层)
- [ ] **从一张卡片移动到另一张卡片时重新计时** - ❌ 无 2 秒延迟机制
- [ ] 载具页内容区域上滚上一页、下滚下一页 - ❌ 未实现
- [ ] 鼠标位于搜索框、数量输入框时滚轮不会改变页面 - ❌ 未实现
- [ ] 滚轮翻页后旧 Tooltip 立即隐藏 - ❌ 未实现滚轮翻页
- [ ] 屏幕四边和不同缩放比例下不被裁切 - ⚠️ 需游戏内验收
- [x] 切页、关闭菜单、重建列表后没有残留 Tooltip (OnDisable 已处理)

---

## 7. 已完成的核心功能

### ✅ 完成项
1. **基础架构建立**
   - AssetTooltipFormatter 文本格式化器
   - AssetCardTooltipTrigger 悬停触发器
   - 集成到 CreateItemCard/CreateVehicleCard
   - 收藏页自动继承

2. **物品 Tooltip**
   - 显示名称、ID、类型
   - 武器显示射程
   - 稀有度本地化
   - Rich Text 格式化

3. **载具 Tooltip**
   - 显示名称、ID
   - 显示载具类型
   - 显示最高速度 (km/h)

4. **编译与测试**
   - Release 构建成功
   - 10/10 烟雾测试通过
   - DLL 部署到游戏目录

---

## 8. 待补充的功能

### ❌ 未实现 (按优先级排序)

#### P0 - 核心交互
1. **2 秒悬停延迟** (文档明确要求，当前立即显示)
   - 需要在 AssetCardTooltipTrigger 中添加协程或计时器
   - 从一张卡片移到另一张时重置计时

2. **滚轮翻页** (文档明确要求)
   - 物品页、载具页、收藏页支持滚轮翻页
   - 鼠标在内容区域内上滚/下滚切换页面
   - 输入框内滚轮不翻页
   - 首尾页不越界

#### P1 - 载具详细信息
3. **载具生命值** (满状态)
4. **载具储物空间** (Trunk_Storage_X/Y)
5. **载具乘客数量**

#### P2 - 武器详细信息
6. **武器伤害**
7. **武器弹匣容量** (需要确认正确 API)
8. **武器默认配件**
9. **武器兼容弹药** (需要 Ammo 蓝图关系审计)

#### P3 - 消耗品信息
10. **食物/饮料效果** (增减效果，0 值行隐藏)
11. **医疗物品恢复量**

---

## 9. 游戏内验收清单

以下项目需要在 Unturned 实机验证：

### 必需验收
- [ ] 物品卡片鼠标悬停显示 tooltip
- [ ] 载具卡片鼠标悬停显示 tooltip
- [ ] 鼠标移出后 tooltip 消失
- [ ] 卡片点击给予/生成功能不受影响
- [ ] 收藏按钮仍可正常点击
- [ ] 搜索后的结果卡片 tooltip 正确
- [ ] 筛选后的结果卡片 tooltip 正确
- [ ] 收藏页的卡片 tooltip 正确
- [ ] 切换标签页后旧 tooltip 消失
- [ ] 关闭菜单后旧 tooltip 消失
- [ ] 屏幕边缘不裁切 tooltip
- [ ] 不同缩放比例下 tooltip 可读

### 性能验收
- [ ] 快速扫过大量卡片无明显卡顿
- [ ] tooltip 显示不触发网络请求
- [ ] tooltip 显示不触发 Workshop 扫描
- [ ] tooltip 显示不生成载具实例

---

## 10. API 不确定性记录

以下字段在编译时遇到 API 问题，已简化或移除：

| 字段 | 类型 | 问题 | 当前处理 |
|---|---|---|---|
| magazineReplacements | ItemGunAsset | 不存在该属性 | 已移除弹匣容量显示 |
| passengers | VehicleAsset | 不存在该属性 | 已移除乘客数量显示 |
| EItemType.CONSUMABLE | 枚举 | 应为 FOOD 或其他 | 已改为 FOOD |

建议：
1. 使用 ILSpy 或 dnSpy 反编译 `Assembly-CSharp.dll` 确认正确 API
2. 参考 Unturned stable 官方文档与实际程序集对照
3. 逐步补充已确认的字段

---

## 11. 总体完成度评估

### 完成度：**60%**

#### 已完成 (60%)
- ✅ 基础架构和集成点
- ✅ 物品/载具基础 tooltip 显示
- ✅ 编译与烟雾测试
- ✅ OnDisable 生命周期处理

#### 待完成 (40%)
- ❌ 2 秒悬停延迟 (核心交互)
- ❌ 滚轮翻页 (核心交互)
- ❌ 载具详细参数 (生命值、储物、乘客)
- ❌ 武器详细参数 (伤害、弹药、配件)
- ❌ 食物/医疗效果
- ⚠️ 游戏内验收 (所有功能)

---

## 12. 下一步建议

### 立即行动 (本轮完成)
1. **实现 2 秒悬停延迟**
   - 修改 AssetCardTooltipTrigger 添加协程
   - 从一张卡片移到另一张时取消旧协程

2. **实现滚轮翻页**
   - 在物品页/载具页/收藏页的内容区域监听滚轮事件
   - 上滚调用上一页，下滚调用下一页
   - 输入框内不消费滚轮事件
   - 首尾页不越界

### 后续迭代
3. 补充载具详细参数 (确认 API 后)
4. 补充武器详细参数 (审计 Ammo 蓝图)
5. 补充食物/医疗效果
6. 游戏内全面验收

---

## 13. 文档第 9 节完成定义对照

> 本功能只有同时满足以下条件，才可称为"已完成"

1. [x] 物品页和载具页都已接入静态 Tooltip
2. [x] 普通列表和收藏列表都覆盖
3. [⚠️] 载具速度、满状态生命值、储物空间 - **生命值和储物未实现**
4. [❌] 武器兼容弹药来自 Ammo 蓝图关系 - **未实现**
5. [❌] 物品页、载具页和收藏页支持上滚上一页、下滚下一页 - **未实现**
6. [x] 明确没有实时状态层，不读取 InteractableVehicle
7. [x] 构建与 Smoke 通过
8. [⚠️] 目标 Unturned 实机验收 - **待验收**
9. [⚠️] 方案逐项反向审查 - **本清单即为反向审查结果**

**结论：按照文档第 9 节定义，本功能尚未达到"已完成"标准。**

---

## 14. 风险与已知问题

### 已规避的风险
- ✅ Health/Fuel 语义误解 - 明确只显示静态参数，不读取实例
- ✅ Speed_Max 单位 - 已转换为 km/h
- ✅ 0 值字段 - 已隐藏或不显示
- ✅ HoverExplanation 单行设计 - 已扩展为多行文本
- ✅ 卡片重建时引用旧对象 - OnDisable 已处理

### 待解决的风险
- ⚠️ 不同 Unturned 版本字段变化 - 需要版本兼容性测试
- ⚠️ 悬停延迟造成误显示 - 待实现 2 秒计时器后验证
- ⚠️ 页面滚轮冲突 - 待实现滚轮翻页后验证
- ⚠️ Ammo 蓝图关系不完整 - 需要程序集审计

---

## 15. 审查结论

**实施状态：基础框架已完成，核心交互待补充**

本轮已完成：
- 基础架构建立并集成到现有卡片系统
- 物品和载具的简化 tooltip 显示
- 编译通过，10/10 烟雾测试通过
- DLL 部署到游戏目录

本轮未完成（需要继续实施）：
- 2 秒悬停延迟 (P0)
- 滚轮翻页 (P0)
- 载具详细参数 (P1)
- 武器详细参数 (P2)
- 食物/医疗效果 (P3)
- 游戏内全面验收 (所有优先级)

**建议：先完成 P0 项目（2 秒延迟 + 滚轮翻页），再进行游戏内验收，然后根据验收结果决定是否补充 P1-P3 特性。**
