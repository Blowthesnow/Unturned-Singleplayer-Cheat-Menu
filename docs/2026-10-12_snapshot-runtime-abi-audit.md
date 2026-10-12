# 2026-10-12 Snapshot Runtime 原生 ABI 复核

本次 2.1.0 发行更新了当前 Unturned 3.26.3.13 引用。`SnapshotRuntimeIntegrationSmoke` 的旧固定 SHA-256 门因此按设计中止，需要先核对它覆盖的原生契约，再更新受审查的基线。

## 程序集来源与固定门

- 安装来源：`I:\Steam\steamapps\common\Unturned\Unturned_Data\Managed\Assembly-CSharp.dll`。
- 发行引用：`D:\Unturned BepInEx\lib\Assembly-CSharp.dll`。
- 两处文件的 SHA-256 均为 `D122E0B615322E660ACEAF65E4F68850A2C9AEDD233B92D6028E35F0C80DB0DF`。
- 原固定基线为 `A110F5FE471422254C594E29F90FC4058D04D357EB3AE9CB4D3EDA0FFEC33F10`。

使用安装目录自带 `BepInEx\core\Mono.Cecil.dll` 读取当前引用的元数据和 IL；不执行 Unity 原生游戏逻辑。完成以下复核后，仅替换测试中的固定 hash 并添加本记录的来源注释。哈希漂移门、所有原生/产品程序集断言均保留。

## 原生契约与 IL 证据

| 契约 | 当前程序集证据 |
| --- | --- |
| Snapshot 恢复构造 | 公共 `Item(UInt16, Byte, Byte, Byte[])` 仍存在。IL 分别写入 `_id`、`amount`、`quality`、`state`；非 null 状态直接保留传入数组，null 状态转为空数组。 |
| 默认模板构造 | 公共 `Item(ItemAsset, EItemOrigin)` 仍存在，`IL_000e` 委托给 `Item(UInt16, EItemOrigin)`。 |
| 物品容量 | `ItemAsset.MaxAmount` 的公共实例 getter 返回 `Int32`。 |
| 原生角色身份 | `Characters.selected` 的公共静态 getter 返回 `Byte`；`Customization` 静态构造 `IL_0001` 写入 `FREE_CHARACTERS=1`，`IL_0007` 写入 `PRO_CHARACTERS=4`，与产品固定五个角色槽一致。 |
| Farm 状态规范化依据 | `EBuild.FARM=7`。`ItemBarricadeAsset.getState(EItemOrigin)` 的 Farm 分支分配四字节数组，`IL_0053` 直接调用 `Provider.get_time()`，`IL_0058` 将该 `UInt32` 写入状态；原有时间戳规范化依据仍成立。 |
| Crafting 物品搜索入口 | `Boolean PlayerCrafting.UpdateBlueprintInputItemStatus(ref UpdateBlueprintStatusParameters, BlueprintSupply, BlueprintInputItemStatus, ItemJar)` 仍存在；第一参数的 `status` 字段类型为 `BlueprintStatus`，其 `targetStatus` 字段类型与第三参数同为 `BlueprintInputItemStatus`，第四参数保持 `ItemJar`。 |
| ItemToIgnore 传递 | 上述搜索方法在 `IL_00e7` 调用 `PlayerInventorySearchParameters.set_ItemToIgnore(ItemJar)`，`IL_00fb` 调用 `PlayerInventory.SearchContents(ref PlayerInventorySearchParameters)`；`Items.SearchContents` 在 `IL_002e` 读取 `ItemToIgnore`。 |
| Crafting 输出入口 | `Boolean PlayerCrafting.HandleCraftRequestInternal(ref ServerInvocationContext, Blueprint, Boolean, Boolean, Boolean)` 仍存在。其 `IL_053c`、`IL_0562` 恰好两处调用公共 `PlayerInventory.forceAddItem(Item, Boolean)`；没有新增第三处输出发放。产品 transpiler 继续只替换这两个调用。 |
| 配件界面搜索 | `UseableGun.updateAttach` 的 `IL_0041`、`IL_01c4`、`IL_0349`、`IL_04d2`、`IL_0728` 恰好五处调用 `FindAttachmentsByCaliber`。`UInt16[]` 重载在 `IL_0053`、`UInt16` 重载在 `IL_0058` 各调用一次 `SearchContents`，既有五槽路径仍成立。 |
| 全局背包搜索 seam | 公共实例 `Void PlayerInventory.SearchContents(ref PlayerInventorySearchParameters)` 保持。它在 `IL_0039` 将同一个 ref 参数交给 `Items.SearchContents`，并保留设备槽、当前储存容器、最大结果数量的原生过滤。 |
| 虚拟搜索结果构造与删除 | `ItemJar(Item)`、`PlayerInventorySearchResultV2(Items, ItemJar)` 构造保持；公共实例 `Delete(Player)` 与 `UInt32 DeleteAmount(Player, UInt32, Boolean)` 保持。产品 `SearchContentsPostfix`、`DeletePrefix` 的 ref Harmony seam 由聚焦测试再次确认。 |
| 拾取复合证据 | 公共实例 `Byte PlayerInventory.getItemCount(Byte)` 与 `ItemJar PlayerInventory.getItem(Byte, Byte)` 保持；产品编译 IL 仍调用两者枚举实际 jar。 |
| Steam 身份 null 边界 | `SteamPlayerID.op_Equality` 仍在 `IL_0001`、`IL_0007` 对两个参数直接调用 `get_steamID`，没有 null 判定；产品 Runtime 的既有两处守卫继续避免调用此运算符。 |

## 聚焦验证

使用 `--no-dependencies` 构建现有测试工程，再使用 `--no-build --no-restore` 运行，保持已冻结的生产 DLL。两条命令退出码均为 0，测试构建为零警告、零错误。

- 构建日志：`artifacts/release-v2.1.0-20261012/SnapshotRuntimeIntegrationSmoke-build.log`。
- 运行日志：`artifacts/release-v2.1.0-20261012/SnapshotRuntimeIntegrationSmoke-run.log`。
- 运行结果：`Snapshot runtime integration smoke checks passed; one product DLL and one BepInPlugin entry verified.`

该完整测试还重新执行了产品程序集反射、Crafting transpiler 替换、搜索角色分类、后台存储跨会话完成、回滚与 reconciliation 路径及单一插件入口等已有契约。没有删除、跳过或放宽任一断言。

本记录证明当前程序集的静态 ABI/IL 与该聚焦回归相符；游戏内合成、存档重启和实际拾取仍属于独立运行验收。
