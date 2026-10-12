# 物资补货修复记录

日期：2026-10-12。状态：源码、构建、聚焦回归和 DLL 部署完成；真实存档中的补货按钮验收待完成。

## 已确认的原因

1. 补货执行器使用 `Action<T>`，丢弃 `ExecuteItemRestock` 返回值，每处理一个区域固定记一次成功。最新日志两次“成功 3”只说明处理了三个区域，并不证明生成物资。
2. 自动无限储存的两条提交路径只调用 `ReceiveDestroyItem` 删除地面模型。当前原生实现只删除 `drops`，不会删除服务端 `items`；残留数据继续占用补货配额和 2m 出生点。
3. 上轮“单人没有本地 loopback”的判断与当前安装版源码不符。`SendItem.Invoke` 同步 loopback 到 `ReceiveItem`，由 `pendingInstantiations` 排队后在原生 Update 创建模型。额外调用私有 `spawnItem` 会生成重复实例。

证据来自当前安装的 `Assembly-CSharp.dll`，SHA-256：

`D122E0B615322E660ACEAF65E4F68850A2C9AEDD233B92D6028E35F0C80DB0DF`

定向反编译文件保存于 `artifacts/restock-fix-20261012/native`。

## 实现

- 无限储存持久化后核对服务端数据及地面实例的 ID、数量、品质和状态，先删除权威 `ItemData`，再调用原生模型删除。失败时恢复原记录并进入现有储存回滚流程；重复或过期身份拒绝提交。
- 补货使用 `Func<T,int>` 保留真实新增件数；无新增计 0，且不通知地面变化。丧尸生成相同的返回值丢弃问题一并修正。
- 移除手动 `spawnItem`，保留原生实例化队列、物资表、生成事件、安全区、占位规则和密度配额。
- 预览按配额缺口及安全空位显示预计件数，满配额区域不再进入候选。附近补货按范围覆盖的原生区域执行，页面说明同步明确此语义。
- 执行前重新核对区域、出生点列表、物资数、密度配额和安全空位；状态变化则计为失效。
- 原生生成发生异常时记录已提交件数，发出真实变化通知，并分别统计成功和失败；无提交异常恢复原刷新时间，所有路径恢复原生区域游标。

## 验证与部署

- 主项目 Release：0 警告、0 错误。
- 使用当前安装的游戏程序集另行编译：0 警告、0 错误；项目与构建日志保存于 `artifacts/restock-fix-20261012/installed-runtime-build`。
- `WorldMaintenanceNativeSmoke`：54 项检查通过，链接真实 Service、Bridge 和 Contracts；原生边界使用测试替身，覆盖返回值、队列唯一性、状态变化、部分提交和时钟恢复。
- `AutoPickupSmoke`、`WorldMaintenanceSmoke`、`UiArchitectureSmoke`、`LocalizationSmoke`：通过。
- 聚焦独立代码复核通过；保留原有无关脏树。
- 备份：`backups/restock-fix-predeploy-20261012-185728`，包含原 DLL 和原日志。
- 部署目标：`I:/Steam/steamapps/common/Unturned/BepInEx/plugins/UnturnedSingleplayerCheatMenu/UnturnedSingleplayerCheatMenu.dll`。
- 编译与部署 SHA-256 均为 `B601896E5694CBDD53F591FC36BFF3CC29257F5565CB14B747F529B33F92EEDE`；活动同名 DLL 为 1 份；部署时游戏已退出。

## 实机验收

完整重启并进入单人存档；原生 `onLevelLoaded` 会重建全部物资区域，清除旧会话残留记录。无需按“没有模型”删除数据，等待实例化或未加载区域的物资可以合法暂时没有模型。

拾取附近自然物资后，预览并确认“补充附近物资”。核对模型可拾取、无限储存不重复增加，以及日志真实计数：

```text
[WorldMaintenance] native item respawn region=(x,y) data=N->M desired=Q added=K nativeResult=True networked=True reason=committed.
```

`added=0` 应显示零成功；`reason=quota-filled` 表示原生配额已满，`reason=no-valid-spawn` 表示本次原生随机选择没有生成有效物资。原生随机选点和区域密度保留，单次调用不承诺填满每个空出生点。

构建、测试替身、DLL 哈希一致不等于真实游戏交互验收。未启动或操作游戏存档，本轮没有宣称实机补货已经通过。
