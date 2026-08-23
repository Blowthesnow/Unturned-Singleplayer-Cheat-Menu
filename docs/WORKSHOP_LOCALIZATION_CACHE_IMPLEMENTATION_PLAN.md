# Workshop 模组物品汉化与持久缓存落地文档

> 基线日期：2026-08-21  
> 目标项目：`D:\Unturned BepInEx`  
> 当前插件版本：`1.7.0`  
> 文档性质：实现依据与验收台账，不代表功能已经落地

## 1. 目标

在当前精简后的插件中增加 Workshop 模组资产汉化能力：

1. 扫描 Unturned 当前已经成功加载的 Workshop 物品和载具资产。
2. 翻译所有 `ItemAsset` 的名称与说明。
3. 翻译 `VehicleAsset` 的名称；载具不存在通用说明字段，因此不设计载具说明翻译。
4. 使用用户配置的机翻 API 翻译未命中的原文。
5. 将翻译结果持久保存到本地缓存。
6. 模组更新、Workshop ID 改变或资产 GUID 改变时，只要字段类型和原文不变，继续复用已有译文。
7. 译文同时用于当前插件资产列表和 Unturned 原生界面读取到的物品名称、物品说明、载具名称，以及 Workshop 物品的自定义右键按钮和 Tooltip。

功能不得修改 Steam Workshop 原始文件，不生成替代模组，不覆盖原作者的 `.dat` 文件。

## 2. 当前项目真实基线

### 2.1 当前保留的相关能力

| 当前文件 | 可复用职责 |
|---|---|
| `Services/AssetCatalog.cs` | `Assets.find(List<ItemAsset>)`、`Assets.find(List<VehicleAsset>)`、Workshop 来源识别 |
| `Services/ItemFilterService.cs` | 武器、附件、衣物、食物饮料、医疗、建筑、工具、其他分类过滤 |
| `UI/CheatMenuOverlayUi.cs` | 当前实际使用的 Unity Overlay UI；物品和载具卡片读取 `FriendlyName` |
| `Services/FavoriteStore.cs` | 以 GUID 或旧式复合键持久化收藏，不依赖显示名称 |
| `Services/IconCache.cs` | 物品图标和载具缩略图缓存协调 |
| `Services/VehicleIconDiskCache.cs` | `Paths.CachePath` 下的磁盘缓存模式 |
| `Services/PluginLocalization.cs` | 插件固定界面文本，不用于动态 Workshop 机翻 |
| `CheatMenuPlugin.cs` | 配置绑定、服务组合、Harmony 安装、资产刷新事件、Unity 主线程更新入口 |

项目已引用 `Newtonsoft.Json`，并采用显式 `<Compile Include=... />` 清单。新增源码必须同步加入 `.csproj`。

### 2.2 已移除能力，不得重新依赖

当前项目中已经不存在：

- `NativeInventoryUiInjector`
- 配装与 `LoadoutService`
- 武器编辑器与 `WeaponEditorService`
- 载具工具箱与 `VehicleToolboxService`
- 背包锁定、原生右键附加按钮等旧实现

新功能不得恢复上述废弃模块，也不得把“原生右键按钮翻译”重新纳入范围。`UI/CheatMenuUi.cs` 虽仍在显式清单中，但当前 `Awake()` 实际创建的是 `CheatMenuOverlayUi`；新功能只接入当前运行路径。

### 2.3 当前扫描限制

现有 `AssetCatalog.Refresh()` 会移除 `null`、`id == 0` 和 `isPro` 物品。这适合当前给予物品列表，但不能作为“全部已加载 Workshop 物品汉化”的唯一数据源。新扫描器必须直接读取当前资产映射，或让 `AssetCatalog` 暴露未经过给予功能过滤的 Workshop 资产视图；不能因旧式 ID 缺失而漏掉 GUID-only 物品。

## 3. 功能范围

### 3.1 必须覆盖

#### Workshop 物品

覆盖所有已加载、来源为 `EAssetOrigin.WORKSHOP`、继承 `ItemAsset` 的资产：

- `ItemAsset.itemName`
- `ItemAsset.itemDescription`

不维护类型白名单。食物、饮料、医疗品、枪械、近战武器、弹药、附件、衣物、背包、工具、材料类用途物品、路障、结构件、陷阱、储物、发电机、农作物、载具维修工具等，只要属于 `ItemAsset`，都自动进入扫描范围。

“建筑物”分两类：尚未放置、能进入物品栏的 `ItemStructureAsset`、`ItemBarricadeAsset` 等属于范围；已经放置的 `StructureDrop`、`BarricadeDrop` 和其他世界实体不属于物品栏扫描对象。

#### Workshop 载具

只覆盖 `VehicleAsset.vehicleName`。当前 `VehicleAsset` 没有与 `ItemAsset.itemDescription` 等价的通用字段，因此不设计载具说明翻译。

### 3.2 右键自定义动作

右键动作需要拆开处理：

- Workshop 自定义动作的 `Action.text`：覆盖。
- Workshop 自定义动作的 `Action.tooltip`：覆盖。
- `Action.key`：不送机翻；它是 Unturned 的固定本地化键，应沿用游戏本地化或插件已有静态映射。

本机 `3253740707` 已验证有 36 个带 `Actions` 的 `.dat` 文件、52 组 `Action_#_Text` / `Action_#_Tooltip`，没有 `Action_#_Key`。例如：`Repair`、`View Billboard`、`Range Expansion`、`Take Medical Syringe`、`Hide Compass` 和 `Recharge`。这类按钮属于 Unturned 原生物品动作系统，可以通过运行时 Getter Patch 汉化，不需要注入新的背包按钮。

### 3.3 其他明确不覆盖

- 原版、官方和非 Workshop 地图随附资产
- 已放置建筑、路障、资源点、动物、NPC 的世界实体提示
- Workshop 网页标题、网页描述、评论、更新日志
- 模组程序集硬编码文本、贴图中的文字、其他任务/NPC/配方本地化
- 修改 Workshop 原始目录、`.dat` 或 AssetBundle

## 4. 当前程序集验证结论

当前游戏路径为 `I:\Steam\steamapps\common\Unturned`。`Assembly-CSharp.dll` 已确认：

| 类型 | 属性 | Setter | 实际字段 |
|---|---|---:|---|
| `ItemAsset` | `FriendlyName` | 无 | `_itemName` |
| `ItemAsset` | `itemName` | 无 | `_itemName` |
| `ItemAsset` | `itemDescription` | 无 | `_itemDescription` |
| `VehicleAsset` | `FriendlyName` | 无 | `_vehicleName` |
| `VehicleAsset` | `vehicleName` | 无 | `_vehicleName` |
| `Action` | `text` | 无 | `_text` |
| `Action` | `tooltip` | 无 | `_tooltip` |
| `Action` | `key` | 无 | `_key` |

`FriendlyName` Getter 会直接读取私有字段，而不是调用 `itemName` 或 `vehicleName` Getter。因此只 Patch `itemName` 不足以影响当前插件卡片，必须分别覆盖 `FriendlyName` 和具体名称属性。

`Action.text`、`Action.tooltip` 和 `Action.key` 也都是无 Setter 的只读 Getter。当前 `Action` 对象不暴露所属资产，因此扫描阶段必须保存 Workshop 资产的 `Action` 对象引用；运行时只对已登记的 Workshop 动作对象返回译文，不能按全局相同字符串替换，以免影响原版动作。

## 5. 总体架构

```text
资产刷新 / 手动扫描
    -> WorkshopLocalizationScanner
    -> 原文内容键查询 WorkshopTranslationCache
       -> 命中：发布只读运行时快照
       -> 未命中：MachineTranslationClient 批量请求
                    -> 校验响应和保护标记
                    -> 原子写入缓存
                    -> 发布只读运行时快照
    -> WorkshopLocalizationPatch
       -> 当前 Overlay 卡片、搜索、Unturned 原生名称/说明/动作 Getter
```

资产发现不发网络请求；HTTP 客户端不访问 Unity 对象；Harmony Patch 不读磁盘、不发请求。

## 6. 建议新增文件

```text
Models/WorkshopTextField.cs
Models/WorkshopTextRecord.cs
Models/WorkshopTranslationCacheDocument.cs
Models/WorkshopTranslationCacheEntry.cs
Models/WorkshopLocalizationSnapshot.cs
Models/WorkshopLocalizationProgress.cs
Services/WorkshopLocalizationScanner.cs
Services/WorkshopTextIdentity.cs
Services/WorkshopTranslationCache.cs
Services/WorkshopRichTextProtector.cs
Services/MachineTranslationClient.cs
Services/WorkshopLocalizationService.cs
Patches/WorkshopLocalizationPatch.cs
tests/WorkshopLocalizationSmoke/WorkshopLocalizationSmoke.csproj
tests/WorkshopLocalizationSmoke/Program.cs
```

建议字段类型为：`ItemName`、`ItemDescription`、`VehicleName`、`ActionText`、`ActionTooltip`。所有新增生产文件必须加入 `UnturnedSingleplayerCheatMenu.csproj` 的显式 Compile 清单。

## 7. 文本扫描设计

### 7.1 扫描来源

扫描器必须以当前资产映射为准：

```csharp
Assets.find(List<ItemAsset>)
Assets.find(List<VehicleAsset>)
```

不得递归扫描 `I:\Steam\steamapps\workshop\content\304930` 并把文件直接认定为当前可用资产；文件存在不代表当前地图和加载顺序中实际生效。

### 7.2 原文读取

Getter Patch 启用后，公开属性可能已返回译文。扫描器必须通过预先缓存的 `FieldInfo` 读取原始字段：`ItemAsset._itemName`、`ItemAsset._itemDescription`、`VehicleAsset._vehicleName`。字段解析失败时只禁用 Workshop 汉化并记录一次警告，不影响插件其他功能。

### 7.3 自定义动作扫描

对每个 Workshop `ItemAsset` 读取其 `actions` 列表：

```text
asset.actions[*].text
asset.actions[*].tooltip
asset.actions[*].key
```

只把 `text` 和 `tooltip` 作为动态机翻文本。`key` 保存到记录中用于诊断和判断，但不发送给 API。扫描记录必须包含所属 Workshop 资产元数据、`Action` 对象引用或本次扫描代次内的稳定索引、字段类型和原始文本。

如果动作数组在资产刷新后重建，旧对象映射必须随资产代次一起丢弃；不能把旧 `Action` 引用用于新一代资产。

### 7.4 过滤规则

保留记录需要满足：资产非空、来源为 Workshop、原文非空白、规范化后仍有可翻译字符。不以 `id != 0` 或物品分类作为条件。明显已经是目标语言的文本默认跳过自动机翻，但缓存中的人工/已有译文仍可应用。

## 8. 缓存身份与模组更新复用

### 8.1 内容键

```text
SHA-256(
    schemaVersion + \n +
    sourceLanguage + \n +
    targetLanguage + \n +
    fieldKind + \n +
    normalizedSourceText
)
```

Workshop ID、Asset GUID、Legacy ID、模组版本、目录时间、供应商、模型名和请求时间都不进入主键。因此模组更新但文本不变时命中旧缓存；不同模组复用相同字段类型和原文时可共享译文；文本改变时只创建新条目。

API 配置记录在条目元数据中但不进入主键。需要换模型重译时提供显式“强制重译”，不得因改配置暗中丢失整个缓存。

### 8.2 规范化

只做：CRLF/CR 统一为 LF、移除首尾空白、Unicode Form C。保留正文内部空格、标点、大小写、富文本标签、数字和占位符；不得激进分词或把不同数字替换成通配符。

### 8.3 文件与原子保存

```text
BepInEx/cache/UnturnedSingleplayerCheatMenu/WorkshopTranslations/zh-CN.json
```

缓存文件保存 `schemaVersion`、语言、字段类型、原文、译文、生成配置、时间和来源诊断信息。来源信息不参与查找。

- UTF-8 编码。
- 单一写入锁。
- 先写同目录临时文件，关闭并刷新后替换正式文件。
- 正式文件保留 `.bak`。
- JSON 损坏时改名为 `.corrupt-{UTC}` 后从空缓存继续。
- 单批失败不得删除旧译文。
- 默认不清理长期未使用条目。

## 9. 运行时显示覆盖

### 9.1 选择 Harmony Getter Postfix

不直接修改 `_itemName`、`_itemDescription`、`_vehicleName`，原因是功能关闭时无需恢复字段、切换语言时无需重新加载资产、原文始终可用于缓存键和英文搜索，也能降低模组切换和退出流程中的状态污染。

安装以下 Postfix：

```text
ItemAsset.get_FriendlyName
ItemAsset.get_itemName
ItemAsset.get_itemDescription
VehicleAsset.get_FriendlyName
VehicleAsset.get_vehicleName
Action.get_text
Action.get_tooltip
```

Postfix 规则：

1. 功能未启用、对象不是已登记的 Workshop 资产/动作或快照未命中时保留 `__result`。
2. 命中有效译文时替换 `__result`。
3. Patch 内不发网络请求、不写文件、不等待长锁。
4. Harmony 卸载后自动恢复原始 Getter 行为。

### 9.2 只读快照

`WorkshopLocalizationService` 将缓存构造成不可变查找快照。扫描或翻译批次完成后，在 Unity 主线程原子替换快照引用。Getter 只做 O(1) 查找，不计算哈希、不读 JSON、不访问 Unity UI。

### 9.3 动作对象安全边界

不能对所有 `Action` 使用“按原文字符串全局替换”，因为原版动作可能也叫 `Repair`、`Take` 或 `Reload`。必须采用动作对象引用映射；若当前游戏版本在 UI 构建过程中复制了 `Action` 对象，则需要在动作列表构建点建立“所属 Workshop 资产 + 原文 + 动作索引”的临时映射，并在动作窗口关闭或资产刷新时清理。

Patch 只改变显示文本，不改变 `Action.type`、`blueprints`、`source`、`key` 或动作执行路径。

### 9.4 搜索和排序

当前 `ItemFilterService.Matches` 需要同时加入原始名称和译名，并保留内部名、ID、GUID、来源名称。载具搜索也要同时支持原始名称与译名。译文快照变化后重建物品、载具和收藏结果，并按当前显示名称重新排序。

不得复用当前 `RefreshCatalog()` 作为译文更新回调，因为它会调用 `Icons.Clear()`，导致无意义的图标重载。应新增轻量 `RefreshLocalizedAssetViews()`；文本变化不影响图像缓存。

## 10. 机翻 API

### 10.1 首期协议

首期实现一个明确协议，建议为 OpenAI-compatible Chat Completions JSON，而不是同时支持多个未经验证的供应商私有格式。配置允许填写 Endpoint、API Key、Model。以后其他供应商通过独立适配器扩展，不把供应商判断散落在协调器中。

### 10.2 请求约束

- 只发送缓存未命中的去重文本。
- 每条文本带本地请求 ID 和字段类型。
- 默认每批 20 条，可配置 1–50。
- 默认超时 30 秒，最多重试 2 次。
- 只对超时、429 和可重试的 5xx 退避重试。
- 不自动跟随跨主机重定向。
- 限制单条文本和单批总字符数。
- 响应必须按请求 ID 对齐，不能依赖返回数组顺序。
- 数量不一致、ID 重复、空译文或 JSON 无法解析时整批不写缓存。

### 10.3 富文本和占位符

发送前保护 Unity Rich Text 标签、`{0}` 等占位符、换行、URL、GUID、数字 ID 等不可翻译片段。返回后验证所有保护标记的数量和顺序，验证失败的条目不得写入缓存。

## 11. 配置设计

在 `CheatMenuPlugin.Awake()` 增加 `WorkshopLocalization` 配置组：

| 键 | 默认值 | 说明 |
|---|---:|---|
| `Enabled` | `false` | 是否启用缓存译文显示 |
| `AutoScanOnAssetsRefreshed` | `true` | 资产刷新后重建文本映射 |
| `AutoTranslateMissing` | `false` | 是否自动请求未缓存文本，默认关闭以避免意外费用 |
| `ApplyToNativeUi` | `true` | 是否应用 Getter Patch 到 Unturned 原生显示 |
| `SourceLanguage` | `English` | 源语言 |
| `TargetLanguage` | `zh-CN` | 目标语言 |
| `ApiProtocol` | `OpenAICompatible` | 首期协议标识 |
| `Endpoint` | 空 | API 地址 |
| `ApiKey` | 空 | API 密钥 |
| `Model` | 空 | 模型名 |
| `BatchSize` | `50` | 1–50 |
| `TimeoutSeconds` | `30` | 5–120 |
| `MaxRetries` | `2` | 0–5 |
| `SkipLikelyTargetLanguageText` | `true` | 跳过明显已是中文的文本 |

未配置 API 时可以只应用缓存。配置规范化应沿用现有设置模式：运行时采用安全值，保存失败只影响持久化，不使插件崩溃。API Key 只能显示“已配置/未配置”，日志不得回显密钥、Authorization Header 或带密钥 URL。

## 12. 当前 Overlay UI 接入

不新增主标签页。把控制区加入当前 `CheatMenuOverlayUi.BuildOtherTab()` 的“扫描信息”附近，保持现有精简结构。

显示：

```text
Workshop 汉化
已扫描物品 N / 载具 N
文本总数 N · 缓存命中 N · 待翻译 N · 失败 N
状态：空闲 / 扫描中 / 翻译中 / 已取消 / 错误
```

按钮：

- `扫描并应用缓存`
- `翻译未缓存文本`
- `取消`
- `重新加载缓存`
- `强制重译未缓存文本`

未配置 API 时翻译按钮禁用或给出明确提示。UI 关闭不取消已经启动的翻译任务；退出世界或插件销毁时必须取消。后台线程不得直接操作 Unity 控件。不恢复已删除的原生背包按钮或右键动作入口。

## 13. 生命周期

### 插件启动

1. 绑定配置。
2. 构造缓存、扫描器、HTTP 客户端和协调器。
3. 读取本地缓存。
4. 安装 Getter Patch。
5. 订阅现有 `Assets.onAssetsRefreshed`。

### 进入世界或打开菜单

1. `AssetCatalog.Refresh()` 保持当前行为。
2. 汉化服务独立扫描全部已加载 Workshop ItemAsset/VehicleAsset。
3. 立即发布缓存命中的译文快照。
4. 只有 `AutoTranslateMissing=true` 且 API 配置完整时才自动请求。

### 资产刷新

1. 取消上一代尚未发送的扫描工作。
2. 生成新的资产代次 ID。
3. 扫描新映射并复用内容缓存。
4. 丢弃旧代次晚到的结果对当前快照的应用；内容有效的译文仍可安全写入全局缓存。

### 退出世界或插件销毁

- 取消网络请求并完成或中止受控缓存写入。
- 清空资产实例映射。
- 卸载 Harmony Patch。
- 不需要恢复资产字段，因为本方案不修改字段。

## 14. 线程与性能要求

- Unity 资产枚举和反射读取在主线程执行。
- HTTP 与 JSON 响应解析可在后台执行。
- Unity 对象不得跨后台线程访问。
- 使用不可变请求 DTO 将原文复制给后台任务。
- Getter 查询保持 O(1)，不在 Getter 中分配大对象、计算哈希、读文件或等待网络。
- 扫描阶段按“字段类型 + 原文”去重。
- `lastUsedUtc` 只在扫描或保存批次更新，不在每次 Getter 调用时写缓存。

## 15. 失败降级

| 失败 | 降级行为 |
|---|---|
| API 未配置 | 只应用已有缓存，未命中显示原文 |
| 网络断开/超时 | 已缓存译文继续显示，未缓存显示原文 |
| 429/5xx | 有上限退避重试，失败后停止该批 |
| 响应格式错误 | 整批拒绝写入，记录脱敏错误 |
| 富文本验证失败 | 仅拒绝对应条目 |
| 缓存损坏 | 隔离损坏文件，使用空缓存继续 |
| 私有字段或 Getter 改名 | 仅禁用 Workshop 汉化，插件其他功能继续 |
| 资产刷新发生在翻译途中 | 代次检查阻止旧结果污染当前映射 |
| UI 重建 | 从服务快照恢复状态，不依赖旧控件引用 |

## 16. 需要修改的当前文件

### `CheatMenuPlugin.cs`

- 增加配置字段。
- 在 `Awake()` 组合汉化服务。
- 在 `InstallRuntimePatches()` 安装 Getter Patch。
- 在当前更新循环泵送服务完成事件。
- 在 `OnAssetsRefreshed()` 通知汉化服务。
- 新增不清理图标的 `RefreshLocalizedAssetViews()`。
- 在销毁阶段取消任务并释放 HTTP 客户端。

### `Services/AssetCatalog.cs`

- 保持当前给予物品目录过滤规则。
- 增加重排当前目录的能力，或让 UI 在结果构建时按译名排序。
- 不放入网络、缓存和机翻逻辑。

### `Services/ItemFilterService.cs`

- 搜索快照同时保存原名和译名。
- 保留 ID、GUID、内部名和来源搜索。

### `UI/CheatMenuOverlayUi.cs`

- 在“其他 → 扫描信息”附近增加控制和进度。
- 译文快照变化后只重建物品、载具、收藏结果和当前相关页面。
- 卡片继续读取 `FriendlyName`，由 Getter Patch 提供译名。
- 不改变当前卡片点击给予/生成行为。

### `Services/PluginLocalization.cs`

- 只新增控制面板固定文案。
- 动态 Workshop 译文不得塞入静态词典。

### `UnturnedSingleplayerCheatMenu.csproj`

- 显式加入全部新增模型、服务和 Patch 文件。

## 17. 测试计划

### 17.1 WorkshopLocalizationSmoke

新增 Smoke 项目，至少覆盖：

- 相同原文、字段类型和语言生成相同键。
- GUID、Workshop ID、模组版本变化不改变键。
- `ItemName`、`ItemDescription`、`VehicleName` 不错误共享键。
- 原文变化生成新键。
- 换行和 Unicode Form C 规范化符合规则。
- 富文本和 `{0}` 等占位符可保护并恢复。
- 缺失、重复或错位响应被拒绝。
- 缓存 JSON 往返不丢字段。
- 损坏缓存不会覆盖原文件。
- 原子保存失败时旧缓存仍可读取。
- 已有缓存优先于网络。
- 原名和译名都能搜索。
- Workshop `Action.text` 和 `Action.tooltip` 能按动作对象正确汉化，原版同名动作不被误替换。
- 原版、地图随附资产不应用 Workshop 译文。

### 17.2 构建验证

```powershell
dotnet build .\src\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.csproj -c Release
```

递归运行当前所有 Smoke 项目和新增 Smoke。必须分别记录构建与每个 Smoke 的结果。

### 17.3 实机验收矩阵

| 场景 | 预期 |
|---|---|
| Workshop 食物 | 原生背包和插件卡片显示译名，原生说明显示译文 |
| Workshop 工具 | 名称、说明正确，工具功能不受影响 |
| Workshop 医疗品 | 名称、说明正确 |
| Workshop 枪械 | 名称、说明正确，给予行为不变 |
| Workshop 衣物 | 名称、说明正确 |
| Workshop 建筑类物品 | 放置前物品名称/说明正确；放置后的实体不承诺翻译 |
| GUID-only、`id == 0` 物品 | 即使不出现在当前给予列表，也能被汉化扫描和原生 UI 命中 |
| Workshop 载具 | 插件列表和原生读取处显示译名，不显示虚构说明 |
| `3253740707` 自定义动作 | `Repair`、`View Billboard`、`Range Expansion`、`Take Medical Syringe`、`Hide Compass`、`Recharge` 等按钮和 Tooltip 显示译文，点击后动作仍按原逻辑执行 |
| 原版资产 | 不被机翻覆盖 |
| 已有目标语言本地化 | 默认不重复调用 API |
| 断网重启 | 已缓存译文继续显示，未缓存回退原文 |
| 关闭功能 | 立即恢复原始 Getter 结果 |
| 英文搜索 | 输入原名仍能找到中文显示的物品 |
| 中文搜索 | 输入译名能找到物品 |
| 模组更新、文本不变 | API 请求数为 0，复用旧缓存 |
| 模组更新、一条文本变化 | 只有变化文本进入未命中队列 |
| API 返回异常 | 插件和游戏 UI 不崩溃，旧缓存不丢失 |

## 18. 分阶段实施台账

### 阶段 A：纯数据与缓存

- [x] 建立字段类型、文本记录和缓存模型。
- [x] 实现保守规范化和 SHA-256 内容键。
- [x] 实现 JSON 读取、损坏隔离和原子保存。
- [x] 实现富文本/占位符保护。
- [x] 新增纯逻辑 Smoke 测试。

验收：不接游戏 UI、不发网络，也能证明跨模组更新缓存复用规则正确。

### 阶段 B：当前资产扫描与只读显示覆盖

- [x] 从当前资产映射扫描全部 Workshop ItemAsset 和 VehicleAsset。
- [x] 绕过 Getter 读取原始私有字段。
- [x] 建立不可变运行时快照。
- [x] 安装五个 Getter Postfix。
- [x] 功能关闭、非 Workshop、缓存未命中均回退原文。

验收：手工准备缓存后，插件卡片与原生物品名称/说明能够显示译文，无网络依赖。

### 阶段 C：机翻 API

- [x] 增加配置和 OpenAI-compatible 客户端。
- [x] 实现去重、批量、ID 对齐、超时、取消和有限重试。
- [x] 验证保护标记后再写缓存。
- [x] 日志脱敏。

验收：只请求缓存未命中项，异常响应不会污染缓存。

### 阶段 D：当前 Overlay UI

- [x] 在“工具 → 翻译”页增加状态与操作按钮。
- [x] 显示扫描、命中、未命中、失败、进度和取消状态。
- [x] 支持重新加载缓存和手动翻译。
- [x] 搜索同时覆盖原名和译名。
- [x] 译文变化只刷新文本相关列表，不清空图标缓存。

验收：当前实际使用的 `CheatMenuOverlayUi` 完成闭环，不恢复废弃 UI。

### 阶段 E：实机闭环

- [x] 构建成功。
- [x] 所有 Smoke 成功。
- [x] 部署 DLL 后比较源/目标 SHA-256 与程序集版本。
- [ ] 在真实单人世界完成物品类型矩阵。
- [ ] 完成断网缓存、模组更新文本不变、单条文本变化测试。
- [ ] 检查 BepInEx 日志无 API Key 泄漏。

构建和 Smoke 只是前置证据，不能替代真实游戏名称、说明和缓存复用验收。

## 19. 完成定义

只有同时满足以下条件，才能称为“Workshop 模组物品汉化并缓存”完整落地：

1. 所有当前已加载的 Workshop `ItemAsset` 类型均进入扫描范围，不受分类白名单和旧式 ID 限制。
2. 物品名称、物品说明和载具名称能够使用缓存译文。
3. Workshop 自定义动作按钮与 Tooltip 能按对象显示正确译文，且不改变动作执行逻辑。
4. 当前插件 Overlay 和 Unturned 原生 Getter 读取路径均显示正确译文。
5. 原名与译名都可搜索。
6. 模组更新但文本不变时不重新请求 API。
7. 文本变化时仅翻译变化项。
8. 断网、限流、错误响应和损坏缓存均能安全降级到缓存或原文。
9. 不修改 Workshop 文件，不恢复已经删除的旧功能。
10. 通过构建、全部 Smoke、部署哈希验证和真实单人游戏验收。


## 20. 当前落地审查（2026-08-22）

本节是本轮实现后的反向审查记录。代码、构建和 Smoke 通过不等于真实单人世界验收已经完成。

### 20.1 已落地并有自动证据的部分

- 阶段 A：新增字段模型、内容键、保守规范化、富文本/占位符保护、JSON 缓存、损坏隔离、原子保存与 .bak。WorkshopLocalizationSmoke 已覆盖键稳定性、字段隔离、文本变化、保护标记、响应 ID 校验、缓存往返、备份和损坏文件隔离。
- 阶段 B：扫描器直接使用当前 Assets.find(List<ItemAsset>) / Assets.find(List<VehicleAsset>)，绕过 Getter 读取 _itemName、_itemDescription、_vehicleName，并登记 Workshop Action 对象；Getter Patch 覆盖 Item/Vehicle 名称、说明和 Action 文本/Tooltip。目标程序集元数据已用 I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll 核对。
- 阶段 C：OpenAI-compatible Chat Completions 客户端支持去重批次、单条/批量长度限制、超时、有限重试、取消、响应 ID 对齐、保护标记验证和不记录 API Key。
- 阶段 D：当前实际运行的 CheatMenuOverlayUi 的 工具 → 翻译 页面已接入扫描、重载缓存、翻译未缓存、强制重译、取消、启用/原生显示/自动扫描/自动翻译开关和进度状态；物品/载具搜索同时保留原名和显示译名。
- 阶段 E 的前置证据：Release 构建 0 警告/0 错误；递归发现的 10 个 Smoke 项目全部通过；最终构建 DLL 与 canonical 目标 DLL SHA-256 均为 53F675188A7B3005606780002DA2C5BD2147AB0C068B077AC48CA71D6B2F9BE6；程序集版本均为 1.7.0.0；活跃同名 DLL 数为 1。

### 20.2 尚未声称完成的实机项目

- 尚未启动真实 Unturned 单人世界并完成 Workshop 食物、工具、医疗、枪械、衣物、建筑物品、GUID-only、载具和 3253740707 自定义 Action 的逐项验收。
- 尚未执行断网重启、模组文本不变/单条变化的 API 请求数观测、关闭功能后的原生 Getter 视觉回退和 BepInEx 运行日志检查。
- 因此“代码已完整落地 + 自动验证通过 + DLL 已部署”已经成立；“真实单人游戏名称/说明/动作/断网缓存闭环已验收”仍需在游戏内完成。

### 20.3 部署回滚说明

最终部署前已备份上一版已部署 DLL，manifest 位于仓库 backups 下本轮生成的 deploy-workshop-localization-final-* 目录。第一次部署尝试因 PowerShell 参数错误未生成旧版本备份，旧目标哈希 D679DCAFD2AB7CD1235D59924A91AB0E4ECFED518B42342DDBEED0F8AEAAE96 未能从现有历史文件恢复；后续最终部署已正确备份当时已部署版本并完成哈希复核。

## 21. 机翻接口复审补全（2026-08-22）

本轮复审针对第 10、11、12、13、14、15 节的机翻接口覆盖进行了补全：

- 增加 `WorkshopLocalization.ApiProtocol` 配置，首期只接受 `OpenAICompatible`；不支持协议不会被视为已配置，也不会发出请求。
- 后台翻译流程改为只传递不可变工作 DTO，扫描候选中的 Unity Asset/Action 引用不会跨到后台线程。
- 进入非单人世界时主动取消翻译任务；新增进程退出清理路径，取消网络、保存缓存、释放 HTTP 客户端并卸载 Harmony。
- HTTP 异常策略收紧为仅对超时、429 和 5xx 重试；普通 `HttpRequestException` 直接失败。
- 缓存条目记录实际 `ApiProtocol`，翻译页面显示协议、目标语言和模型；English 动态状态映射同步补齐。
- WorkshopLocalizationSmoke 新增不支持协议、DTO 不携带 Unity 对象、5xx 重试上限、HttpRequestException 不重试和协议元数据往返断言。

复审后的最终证据：Release 构建 0 警告/0 错误；全部 10 个 Smoke 项目通过；最终构建与 canonical 目标 DLL SHA-256 均为 `4FCC7FD35EFD2D9FD742102F84208C83B5F9AA8DA8B64E2E9AE3D85793B65D47`；程序集版本均为 `1.7.0.0`；活跃同名 DLL 数为 1。真实游戏内 API 请求、断网和动作矩阵仍属于需要人工执行的实机验收。
## 22. 完整复审结论（2026-08-22）

本轮对方案第 1～21 节、阶段 A～E 和完成定义逐项复核后的结论如下：

1. 当前资产映射扫描、Workshop 来源过滤、GUID-only/Legacy ID 不依赖、原始私有字段读取均已落地；真实游戏当前加载资产数量和类型矩阵尚未在游戏内执行。
2. ItemName、ItemDescription、VehicleName、ActionText、ActionTooltip 均有字段类型、内容键、缓存条目和运行时快照路径。
3. Action 使用对象引用映射，不按全局字符串替换；Action key 不发送给 API，并保留在缓存诊断元数据。
4. 缓存身份不包含 Workshop ID、GUID、模组版本或模型名；文本不变复用，文本变化生成新键；强制重译为显式操作。
5. API 层已明确限制为 OpenAICompatible，配置、批处理、长度限制、响应 ID 对齐、保护标记、超时、取消、429/5xx 重试和普通 HTTP 异常降级均已覆盖。
6. 后台只接收不可变 DTO；Getter 只读 O(1) 快照；世界退出和进程退出有取消/释放路径。
7. 当前运行路径的 Overlay 已在“工具 → 翻译”页提供控制和状态，物品/载具搜索保留原名与译名。
8. 当前自动证据：Release 构建 0 警告/0 错误，全部 10 个 Smoke 通过，包含新增协议/DTO/重试/缓存元数据断言。
9. 最终部署证据：canonical 目标 DLL 与构建 DLL SHA-256 均为 9B105E839BC9474AB13EB661FE5D663B8CD7AA3DF4A14AB144FE90097231383F，程序集版本均为 1.7.0.0，活跃同名 DLL 数为 1；manifest 位于仓库 backups/deploy-full-audit-final-20260822-034000/deploy-manifest.txt。
10. 仍不能从静态、构建、Smoke 或部署证据替代的项目：真实单人世界矩阵、断网重启、模组文本不变/单条变化的真实 API 请求数、关闭功能后的原生显示回退、BepInEx 实际日志无密钥泄漏。这些保持未完成，不作“完整实机闭环”声明。
## 23. 云机器翻译与大模型双通道配置（2026-08-22）

翻译页现在同时支持两类接口，统一使用紧凑协议抽屉选择，避免主页面堆叠大按钮：

- 云机器翻译：腾讯云 TMT、百度翻译、Google Cloud Translation、Microsoft Translator、DeepL、有道智云。
- 大模型翻译：OpenAI、OpenAI 兼容、Azure OpenAI、Anthropic、Google Gemini、Ollama、OpenRouter、DeepSeek、SiliconFlow、LM Studio、腾讯混元、百度千帆。

翻译页直接提供 Endpoint、API Key / App ID、Secret Key、Region、Project ID 和 Model 输入框；不需要的字段可以留空。API Key 和 Secret Key 使用密码输入，不写入日志。云翻译使用各供应商专用 REST/签名/响应解析，大模型使用 Chat/Message/GenerateContent 响应解析，不再把所有供应商强行当作同一种协议。

协议抽屉使用 3 列紧凑按钮布局，当前构建与部署证据：构建/目标 DLL SHA-256 均为 `CBC7F8168AAB5A213D031EB4D5D49F11ADD9983483088BD6ECFD5E40BAADFAEC`，程序集版本均为 `1.7.0.0`，活跃同名 DLL 数为 1；manifest 位于 `backups/deploy-cloud-and-llm-final-20260822-035000/deploy-manifest.txt`。