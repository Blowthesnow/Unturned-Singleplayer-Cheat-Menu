# NPC 对话与交互界面汉化研究

> 文档状态：研究结论，未修改插件运行时代码。
>
> 研究日期：2026-08-26
>
> 证据等级：官方 Unturned stable 文档为一手依据；本仓库现有扫描器范围为本地实现基线。官方资料未提供 BepInEx/Harmony 的 NPC UI 挂钩 API。

## 结论

可以覆盖地图 NPC 对话正文和玩家点击的对话选项，但应优先使用
Unturned 官方资产本地化文件，而不是在 UI 上做全局文本替换。

官方明确将 `Message_#_Page_#` 定义为对话页文本、`Response_#`
定义为对应的响应选项文本。因此，`Response_#` 是地图作者定义的
可点击对话按钮，可以由资产本地化稳定覆盖。[1]

要完整覆盖 NPC 触发后的显示，还需要同时本地化任务预览、商店页面
及对象式对话入口。游戏内固定按钮（例如由原生 UI 创建的接受、返回、
放弃等文案）和地图 `Response_#` 不是同一类文本：官方资料只证明后者
有地图资产本地化字段，前者需要依当前游戏程序集另行确认。

## 官方可本地化范围

| 场景 | 应采集的本地化键 | 官方依据 | 说明 |
|---|---|---|---|
| NPC 名称 | `NPC.Character` | [2] | 交互时显示的角色名；NPC 资产通过 `Dialogue` 引用其对话资产。 |
| 对话正文 | `Dialogue.Message_#_Page_#` | [1] | 每个消息页的正文。 |
| 对话分支按钮 | `Dialogue.Response_#` | [1] | 玩家可见、可点击的响应选项；只替换显示文本。 |
| 任务预览和任务 UI | `Quest.Name`、`Quest.Description` | [3] | 对话响应可打开任务预览。 |
| NPC 商店 | `Vendor.Name`、`Vendor.Description`、`Buying_#_Description`、`Selling_#_Description` | [4] | 对话响应可打开商店；买卖说明可以覆盖物品原说明。 |
| 非角色 NPC/对话对象 | 对象的 `Name`、`Interact`、`Dialogue_Name` | [5] | `Interactability NPC` 可提供对话、任务和商店；`Interactability Dialogue` 可用自定义交互提示打开对话页。 |
| 条件、奖励显示名 | 各自本地化文件的 `Condition_#`、奖励列表对应前缀的 `Reward_#` | [6][7] | 仅在目标地图实际显示这类条件/奖励文案时纳入，不能修改其条件、奖励或状态。 |

NPC 对话允许 `<name_npc>`、`<name_char>`、`<br>` 和 `<pause>` 等特殊
富文本标签；翻译时必须逐个保留并验证其配对和顺序。尤其不能把
`<pause>` 当作普通可删除文本。[8]

## 推荐方案：资产本地化优先

官方定义中，每个资产会按当前语言在资产同目录查找本地化 `.dat` 文件，
例如 `English.dat`、`French.dat`。[9] 因此最稳定的交付形态是为地图或
Workshop 资产提供其当前语言的中文 `.dat`，由游戏原生加载与重开界面时
重新应用。

建议在现有自动翻译功能中新增独立的 `NpcLocalizationScanner`，而不是把
NPC 逻辑塞进物品/载具扫描器：

1. 枚举 NPC、对话、任务、商店和对象资产，建立以 `asset GUID + localization key`
   为主键的候选项。对象资产只在 `Interactability` 为 NPC 或 Dialogue，或确有
   相关本地化字段时进入范围。
2. 保留原文、资产 GUID、资产路径、键、文本类别、引用来源和翻译状态；相同原文
   不可跨资产无条件复用，避免上下文不同的按钮含义被误译。
3. 翻译前冻结并保护上述 NPC 富文本标签、TextMesh Pro 标签和换行；翻译返回后
   校验令牌多重集相同，失败则不写入候选项。
4. 按每个资产现有语言文件的格式生成或合并中文 `.dat`。不要改 `.asset` 中的
   `Message_#_Response_#` 关系、条件、奖励、任务 GUID 或商店 GUID。
5. 在生成前显示覆盖清单与冲突项；输出到可撤销的本地覆盖目录或用户明确选定的
   Workshop 副本，绝不静默覆盖原始地图文件。

这条路径能在原生 UI 每次重建时持续显示中文，且不会干预对话响应的网络请求、
条件判断或奖励发放。

## 当前插件的缺口

现有 `WorkshopLocalizationScanner.Scan` 只枚举 `ItemAsset` 和 `VehicleAsset`，
并读取物品名称/描述、物品 `Action` 文本/提示和载具名称；它没有枚举 NPC、
对话、任务、商店或对象资产。[10] 因此 NPC 范围应作为新扫描器与新候选类型
实现，保持既有物品/载具翻译行为不变。

## 兼容方案：不修改 Workshop 文件的客户端覆盖

若要求只通过 BepInEx 在本机显示中文，不产生或修改中文资产语言文件，可以做
第二层兼容覆盖，但它不是官方公开 API，必须按当前 `Assembly-CSharp.dll` 审计后
实施。可采用以下受限流程：

1. 使用目标 Unturned 安装目录的实际程序集，确认对话打开、换页、响应列表重建、
   任务预览、任务日志和商店重建的具体类型、方法签名及参数来源。
2. 仅对已确认的 UI 构建/刷新方法添加 Harmony Postfix；从该次构建所使用的
   资产和本地化键取得译文，再更新对应的原生文本控件。
3. 使用稳定身份而不是“英文原文”作为缓存键：至少包含资产 GUID、语言、
   本地化键，必要时包含消息/响应索引。英文重复时不能错配到别的 NPC。
4. 对话翻页、条件变化、选择响应、任务接受/完成/放弃、商店打开/关闭、语言切换
   和地图/单人世界退出时刷新或失效缓存。不得持久替换资产字段，以免污染其他
   UI 或下一局会话。
5. 原生固定按钮必须建立白名单并记录其所属 UI；禁止在 `SleekLabel`、
   `TMP_Text` 等通用 setter 上做全局翻译，否则背包、聊天、菜单和第三方 UI 都可能
   被误改。

本地程序集元数据已能看到 NPC 对话、任务、商店相关 UI 模块名，例如
`PlayerNPCDialogueUI`、`PlayerQuestUI` 和 `PlayerNPCVendorUI`；这只能作为
审计起点，不能据此假定其方法名或字段访问在下个游戏版本仍然成立。[11]

## 验证矩阵与完成边界

| 验证项 | 通过标准 |
|---|---|
| 对话正文 | 多页消息、变量名标签、暂停和富文本均正确显示；原文标签未丢失。 |
| 响应按钮 | 条件满足/不满足时可见项正确；点击后仍进入原来的 Dialogue、Quest 或 Vendor。 |
| 任务 | 预览、接受后任务名称/描述、完成与放弃路径均显示中文且状态不变。 |
| 商店 | 名称、描述、买卖覆盖说明均显示中文；价格、货币、购买/出售请求不变。 |
| 对象入口 | NPC 对象与 Dialogue 对象的交互提示、显示名和对话均覆盖。 |
| 回归 | 物品、载具、玩家菜单、聊天和非 NPC 原生 UI 未被错误替换。 |
| 版本门槛 | 记录游戏版本、`Assembly-CSharp.dll` SHA-256 和所有实际挂钩签名；版本变化后重新审计。 |

仅完成扫描、机器翻译或 Smoke 测试不能证明 NPC UI 已可用。完成声明至少需要在
包含 NPC 对话、任务和商店的真实单人地图中逐项验证上述矩阵。

## 证据来源

[1] Smartly Dressed Games, [Dialogue Assets - Localization](https://docs.smartlydressedgames.com/en/stable/npcs/dialogue-asset.html#localization), accessed 2026-08-26.

[2] Smartly Dressed Games, [NPC Character Assets - Appearance and Localization](https://docs.smartlydressedgames.com/en/stable/npcs/npc-asset.html#appearance), [Localization](https://docs.smartlydressedgames.com/en/stable/npcs/npc-asset.html#localization), accessed 2026-08-26.

[3] Smartly Dressed Games, [Quest Assets - Localization](https://docs.smartlydressedgames.com/en/stable/npcs/quest-asset.html#localization), accessed 2026-08-26.

[4] Smartly Dressed Games, [Vendor Assets - Localization](https://docs.smartlydressedgames.com/en/stable/npcs/vendor-asset.html#localization), accessed 2026-08-26.

[5] Smartly Dressed Games, [Object Assets - Interactables](https://docs.smartlydressedgames.com/en/stable/assets/object-asset.html#interactables), [Localization](https://docs.smartlydressedgames.com/en/stable/assets/object-asset.html#localization), accessed 2026-08-26.

[6] Smartly Dressed Games, [Conditions - Localization](https://docs.smartlydressedgames.com/en/stable/npcs/conditions.html#localization), accessed 2026-08-26.

[7] Smartly Dressed Games, [Rewards - Localization](https://docs.smartlydressedgames.com/en/stable/npcs/rewards.html#localization), accessed 2026-08-26.

[8] Smartly Dressed Games, [Rich Text - Extended Tags](https://docs.smartlydressedgames.com/en/stable/data/rich-text.html#extended-tags), accessed 2026-08-26.

[9] Smartly Dressed Games, [Asset Definitions - Localization](https://docs.smartlydressedgames.com/en/stable/assets/asset-definitions.html#localization), accessed 2026-08-26.

[10] Local project baseline, `src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationScanner.cs`, `Scan`, lines 32-93. The codebase knowledge graph was queried on 2026-08-26; verify the current source before implementation because the graph snapshot can lag the worktree.

[11] Local `lib/Assembly-CSharp.dll` metadata/string audit on 2026-08-26. This is discovery evidence, not a public API contract.
