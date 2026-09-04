# BattlEye 与多人运行边界调整 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `$executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 允许使用者保留插件安装并通过 BattlEye 或非 BattlEye 路径启动 Unturned，允许菜单在已生成本地玩家的多人客户端会话中打开，把“玩法功能受支持范围”限定为单人世界，同时保证 Workshop 翻译在多人中不被会话门自动停止，并把多人玩法、服务器和 BattlEye 环境定义为不保证兼容的自担风险使用。

**Architecture:** 把当前 `SingleplayerGuard` 同时承担的四件事拆开：进程加载、客户端菜单就绪、单人支持状态、玩法功能执行授权。新的纯策略模型负责识别会话；菜单只要求客户端世界和本地玩家就绪；多人玩法在每次连接会话确认风险后才进入 best-effort 执行；Workshop 翻译作为本地内容管线独立 Pump，绝不受单人/多人或玩法确认状态控制；Snapshot Infinite Storage 继续保持单人硬门。

**Tech Stack:** C# / .NET Standard 2.1、BepInEx 5.4.23.5、Harmony、Unity 2022.3.62f3 Legacy uGUI、Unturned `Assembly-CSharp.dll`、PowerShell 5.1、现有 `tests/**/*Smoke.csproj` 控制台 Smoke 工程。

**Spec:** `docs/superpowers/plans/2026-09-03-unturned-battleye-multiplayer-boundary.md` 的“目标边界合同”至“功能矩阵”章节。

## Global Constraints

- 本轮任务只交付本文档；不修改生产源码、测试、README、发布包，不构建、不部署、不启动游戏。
- 玩法功能唯一受支持、受测试、可提交兼容性缺陷的环境仍是 Unturned 单人世界。
- 多人客户端可打开菜单；玩法功能仅在本次连接会话明确确认风险后才允许尝试执行，成功、同步、回档、掉线和账号结果均不作保证。
- Workshop 翻译不是玩法功能：扫描、缓存命中、翻译请求、批次完成和原生 UI 文本应用在多人中不得因 session kind、风险确认、菜单关闭或场景切换自动停止。
- 资产刷新发生在翻译运行期间时只登记一次 deferred rescan；当前批次先正常完成。`AutoScanOnAssetsRefreshed=false` 时资产刷新不得调用 `CancelTranslation()`。
- BattlEye 允许由使用者正常启动；项目不得停止、禁用、修改、注入规避、伪装或宣称绕过 BattlEye。
- “允许 BattlEye 启动”只表示仓库启动器不再阻止该选择，不等于 BepInEx 或本插件获 BattlEye 允许，也不等于插件一定能加载或保持连接。
- 独立专用服务器进程只要求插件安全加载为 inert/no-op；当前客户端 uGUI 和玩法功能不承诺作为 U3DS 服务端插件运行。
- `PluginGuid`、程序集名、命名空间、插件目录、配置名、收藏/传送/自动拾取数据名及 Snapshot v1 路径保持不变，避免产生双 DLL、双配置或存档迁移。
- Snapshot Infinite Storage 的 `Singleplayer_<slot>` 身份合同保持不变；多人会话不得读取、写入或迁移单人仓库。
- 玩法功能的请求配置与有效运行状态分离：离开单人或尚未确认多人风险时，不把用户配置中的 `Enabled=true` 自动写回为 `false`；翻译配置和翻译任务不参与这个 gameplay effective-state 计算。
- 继续保持一份活动 DLL、精确 Snapshot 身份、自动拾取规则语义、输入所有权、UI 生命周期和原生回滚合同。
- 当前工作树已有大量用户改动；实施时只暂存任务触及的精确路径，不 reset、stash、clean、覆盖或格式化无关文件。
- 任务文件若在实施前已经 dirty，必须使用 `git add -p -- <path>` 只选本任务 hunk；若新旧改动落在同一不可分 hunk，保留工作树并暂停该次提交，不能把既有改动顺带暂存。
- 源码/API、Release 构建、Smoke、部署哈希、BattlEye 启动、多人连接和真实玩法验收必须分别报告。

---

## 1. 目标边界合同

### 1.1 本方案采用的语义

本方案把“功能只针对单人游戏”解释为**产品支持与验证范围**，而不是继续把整个插件锁死在单人会话：

1. 插件可以保持安装，启动器允许选择 BattlEye 或非 BattlEye。
2. 进入多人服务器后，只要 `Provider.isConnected` 且 `Player.LocalPlayer` 已生成，主菜单可以打开。
3. 多人菜单打开时显示常驻警示，但不弹出阻断整个菜单的确认层；第一次尝试玩法动作或启用玩法自动化时才显示风险确认。确认前仅玩法动作和玩法自动化暂停，翻译页与正在运行的翻译任务保持可用。
4. 确认后，瞬时玩法功能可以按现有单人实现 best-effort 调用，不新增多人协议、服务端 RPC 适配或反作弊适配。
5. Snapshot Infinite Storage 属于事务性持久化例外：多人中始终不可运行，因为当前身份、回滚和守恒证明只覆盖单人。
6. 断线、切换服务器、本地玩家实例变化或退出世界后，玩法风险确认立即失效；下一次多人会话重新确认。该状态变化不得取消、重建或清空翻译任务。
7. Dedicated Server 没有可打开的本地 uGUI。这里的“服务器可打开”默认指**玩家客户端已连接到多人服务器后可打开菜单**；U3DS 进程仅要求安全加载后不初始化 UI/玩法图。

如果产品最终要的是“多人中菜单可见但所有玩法按钮永久禁用”，只需把 `GameplayFeatureAccessPolicy.Evaluate` 的多人分支固定为 `DeniedUnsupportedSession`，其余加载、菜单、BattlEye、专服 no-op、文案和测试结构不变。

### 1.2 规范用语

以后文档和 UI 统一使用下列词义：

| 词语 | 精确定义 |
| --- | --- |
| 支持 | 已纳入构建、Smoke、真实游戏验收和缺陷处理承诺 |
| 允许加载 | 项目自身不主动阻止进程启动或插件发现 |
| 允许开菜单 | uGUI 可以创建并获得输入，不代表玩法动作被允许 |
| 本地翻译 | Workshop 文本扫描、缓存、外部翻译请求和本地应用；不属于玩法动作，不进入多人风险 gate |
| best-effort | 复用现有单人实现尝试执行，不保证服务器接受或状态同步 |
| 自担风险 | 使用者接受插件可能被阻止加载、踢出、回档、封禁或损坏非单人会话状态；这不是兼容性保证 |
| Dedicated Server | 独立 U3DS 无头服务端进程，不等同于玩家客户端加入服务器 |

严禁在 README、日志或发布说明中把“允许启动”写成“BattlEye 兼容”，也严禁把“多人可开菜单”写成“多人功能受支持”。

## 2. 当前证据快照

本文档依据 2026-09-03 当前磁盘状态编写：

| 证据 | 当前值 |
| --- | --- |
| 工作区 | `D:\Unturned BepInEx` |
| Git 分支 / HEAD | `main` / `48bbb32453b72da553527dd0b73b48732f14baa9` |
| 工作树 | dirty；README、CHANGELOG、源码、UI、Snapshot、Smoke 均有在途改动 |
| `lib/Assembly-CSharp.dll` SHA-256 | `126C27BA2811A78380319D8DD3623352C00169250F7189394D9CA6B0EF0AB529` |
| 本机 Unturned | `I:\Steam\steamapps\common\Unturned\Unturned.exe`，Unity file version `2022.3.62.9860879` |
| BattlEye launcher | `I:\Steam\steamapps\common\Unturned\Unturned_BE.exe` 存在 |
| BepInEx | `5.4.23.5` x64 |
| 当前部署插件 | `1.98.0.0`，SHA-256 `ADF7078D994FE5D986D337E457FDF748900E81BA0C4437184C8D1252CE62FC58` |
| 本机 U3DS | 未安装独立 U3DS；Dedicated no-op 只能列为待运行验收，不能由客户端安装推定 |
| 单人硬门引用 | `SingleplayerGuard` 共 50 处，分布于 15 个源码/测试文件 |
| 边界关键词文件 | 排除构建、发布和备份目录后共 27 个文件 |

权威证据：

- `D:\U3-SDK-main\Assets\Runtime\Assembly-CSharp\Unturned\Provider\Provider.cs:2047` 的 `Provider.singleplayer(...)` 会设置 `serverID = "Singleplayer_" + Characters.selected`，关闭 VAC/第三方反作弊状态，并令 `isServer`、`isClient` 同时为真。因此当前单人识别不能简单替换成 `Provider.isClient`。
- `D:\U3-SDK-main\Assets\Runtime\Assembly-CSharp\Unturned\Provider\Dedicator.cs:51` 提供 `isStandaloneDedicatedServer`；已用本地 `Assembly-CSharp.dll` 元数据确认其 getter 为 public static。
- Unturned 官方文档把独立服务器定义为 U3DS，并把 Rocket/OpenMod列为服务端插件框架：[Setting up a Server](https://docs.smartlydressedgames.com/en/stable/servers/server-hosting.html)、[Rocket](https://docs.smartlydressedgames.com/en/stable/servers/rocket.html)、[OpenMod](https://docs.smartlydressedgames.com/en/stable/servers/openmod.html)。
- BattlEye 官方说明其可能阻止游戏进程加载文件，Unturned 官方支持说明多人作弊检测可能导致 Steam game ban：[BattlEye FAQ](https://www.battleye.com/support/faq/)、[Unturned Multiplayer bans](https://support.smartlydressedgames.com/hc/en-us/articles/12082986788884-Multiplayer-bans)。
- Smartly Dressed Games 官方组织中的开发者讨论记录了 BattlEye 会阻止/踢出非官方模块；这只能作为风险证据，不能推导 BepInEx 已获允许：[U3-SDK discussion #4092](https://github.com/SmartlyDressedGames/U3-SDK/discussions/4092)。
- BepInEx 官方故障排查把“preloader 是否运行”和“plugin 是否发现”视为独立加载阶段：[BepInEx Troubleshooting](https://docs.bepinex.dev/articles/user_guide/troubleshooting.html)。因此验收必须分别记录 BE 启动、BepInEx 预加载、插件发现和进入服务器。

## 3. 目标会话矩阵

| 环境 | 插件发现 | 主菜单 | 本地翻译 | 玩法功能 | Snapshot Infinite Storage | 产品支持结论 |
| --- | --- | --- | --- | --- | --- | --- |
| 主菜单 / 未连接 | 允许 | 不打开 | 已开始的任务继续 Pump | 暂停 | 关闭 | 正常等待世界 |
| 世界加载中 / 无本地玩家 | 允许 | 不打开 | 继续，不因加载状态取消 | 暂停 | 关闭 | 正常等待玩家 |
| 无 BattlEye 单人 | 允许 | 允许 | 允许 | 立即允许 | 允许 | 唯一完整支持玩法环境 |
| BattlEye 单人 | 尝试加载，不保证 | 加载成功后允许 | 插件加载后允许 | 单人逻辑 | 单人逻辑 | 玩法仍属单人，但 BE 加载兼容不受支持 |
| 多人客户端，未确认风险 | 尝试加载 | 允许并显示常驻警示 | 允许且不得自动停止 | 阻断，玩法自动化暂停 | 关闭 | 玩法不受支持；翻译不受该 gate 影响 |
| 多人客户端，已确认风险 | 尝试加载 | 允许并显示常驻警示 | 允许且不得自动停止 | best-effort | 关闭 | 玩法自担风险；翻译保持本地功能 |
| 非单人 listen host | 尝试加载 | 与多人客户端相同 | 允许且不得自动停止 | best-effort | 关闭 | 玩法自担风险 |
| U3DS Dedicated Server | 允许尝试 | 无 UI | 不初始化客户端翻译服务 | inert/no-op | 关闭 | 只承诺不因客户端图初始化而崩溃；不是服务端功能支持 |
| 未识别连接状态 | 允许 | 不打开 | 已开始的任务继续 Pump | 阻断 | 关闭 | 玩法 fail closed，翻译独立 |

## 4. 会话与授权接口

### 4.1 新增纯会话模型

创建 `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionPolicy.cs`，保持 Unity-free 的核心判断，以便 Smoke 直接编译：

```csharp
using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum RuntimeSessionKind
{
    Frontend,
    Loading,
    Singleplayer,
    MultiplayerClient,
    ListenServer,
    DedicatedServer,
    UnknownConnected
}

internal readonly struct RuntimeSessionState
{
    internal RuntimeSessionKind Kind { get; }
    internal string ServerId { get; }
    internal bool HasLocalPlayer { get; }

    internal RuntimeSessionState(RuntimeSessionKind kind, string serverId, bool hasLocalPlayer)
    {
        Kind = kind;
        ServerId = serverId ?? string.Empty;
        HasLocalPlayer = hasLocalPlayer;
    }

    internal bool IsSupportedGameplay => Kind == RuntimeSessionKind.Singleplayer;
    internal bool IsUnsupportedClientSession =>
        Kind == RuntimeSessionKind.MultiplayerClient || Kind == RuntimeSessionKind.ListenServer;
    internal bool CanOpenClientMenu =>
        HasLocalPlayer && (Kind == RuntimeSessionKind.Singleplayer || IsUnsupportedClientSession);
    internal bool CanUseSnapshotStorage => Kind == RuntimeSessionKind.Singleplayer && HasLocalPlayer;
}

internal static class RuntimeSessionPolicy
{
    private const string SingleplayerServerIdPrefix = "Singleplayer_";

    internal static RuntimeSessionState Evaluate(
        bool isConnected,
        bool isServer,
        bool isClient,
        bool isStandaloneDedicatedServer,
        string serverId,
        bool hasLocalPlayer)
    {
        if (isStandaloneDedicatedServer)
            return new RuntimeSessionState(RuntimeSessionKind.DedicatedServer, serverId, false);
        if (!isConnected)
            return new RuntimeSessionState(RuntimeSessionKind.Frontend, serverId, false);
        if (!hasLocalPlayer)
            return new RuntimeSessionState(RuntimeSessionKind.Loading, serverId, false);
        if (isServer && isClient && !string.IsNullOrEmpty(serverId) &&
            serverId.StartsWith(SingleplayerServerIdPrefix, StringComparison.Ordinal))
            return new RuntimeSessionState(RuntimeSessionKind.Singleplayer, serverId, true);
        if (isServer && isClient)
            return new RuntimeSessionState(RuntimeSessionKind.ListenServer, serverId, true);
        if (isClient)
            return new RuntimeSessionState(RuntimeSessionKind.MultiplayerClient, serverId, true);
        return new RuntimeSessionState(RuntimeSessionKind.UnknownConnected, serverId, true);
    }
}
```

顺序不能交换：Dedicated Server 必须先判；`Provider.isServer && Provider.isClient` 只有同时满足 `Singleplayer_` 前缀才是单人。

### 4.2 新增当前运行时适配器

创建 `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionBoundary.cs`：

```csharp
using SDG.Unturned;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class RuntimeSessionBoundary
{
    internal static RuntimeSessionState CaptureCurrent()
    {
        return RuntimeSessionPolicy.Evaluate(
            Provider.isConnected,
            Provider.isServer,
            Provider.isClient,
            Dedicator.isStandaloneDedicatedServer,
            Provider.serverID,
            Player.LocalPlayer != null);
    }

    internal static string CaptureConnectionKey(RuntimeSessionState state)
    {
        int playerInstanceId = Player.LocalPlayer == null ? 0 : Player.LocalPlayer.GetInstanceID();
        return state.Kind + "|" + state.ServerId + "|" + (Provider.map ?? string.Empty) + "|" + playerInstanceId;
    }
}
```

不得用 `Provider.isThirdpartyAntiCheatActive` 作为菜单门禁。该成员在当前 SDK 中是 internal，且服务器是否宣告第三方反作弊并不能证明本地 BepInEx 是否已获允许。

### 4.3 新增每会话确认与功能授权

创建 `src/UnturnedSingleplayerCheatMenu/Services/GameplayFeatureAccessPolicy.cs`：

```csharp
using System;

namespace UnturnedSingleplayerCheatMenu.Services;

internal enum GameplayFeatureAccess
{
    DeniedNotReady,
    AllowedSupportedSingleplayer,
    RequiresMultiplayerAcknowledgement,
    AllowedUnsupportedMultiplayer,
    DeniedUnsupportedSession,
    DeniedDedicatedServer,
    DeniedUnknownSession
}

internal static class GameplayFeatureAccessPolicy
{
    internal static GameplayFeatureAccess Evaluate(RuntimeSessionState state, bool acknowledged)
    {
        if (state.Kind == RuntimeSessionKind.DedicatedServer)
            return GameplayFeatureAccess.DeniedDedicatedServer;
        if (state.Kind == RuntimeSessionKind.Frontend || state.Kind == RuntimeSessionKind.Loading)
            return GameplayFeatureAccess.DeniedNotReady;
        if (state.Kind == RuntimeSessionKind.Singleplayer)
            return GameplayFeatureAccess.AllowedSupportedSingleplayer;
        if (state.IsUnsupportedClientSession)
            return acknowledged
                ? GameplayFeatureAccess.AllowedUnsupportedMultiplayer
                : GameplayFeatureAccess.RequiresMultiplayerAcknowledgement;
        return GameplayFeatureAccess.DeniedUnknownSession;
    }

    internal static bool IsAllowed(GameplayFeatureAccess access)
    {
        return access == GameplayFeatureAccess.AllowedSupportedSingleplayer ||
               access == GameplayFeatureAccess.AllowedUnsupportedMultiplayer;
    }
}

internal sealed class MultiplayerRiskAcknowledgement
{
    private string _observedConnectionKey = string.Empty;
    private string _acceptedConnectionKey = string.Empty;

    internal bool Observe(string connectionKey)
    {
        string normalized = connectionKey ?? string.Empty;
        if (string.Equals(_observedConnectionKey, normalized, StringComparison.Ordinal))
            return false;
        _observedConnectionKey = normalized;
        _acceptedConnectionKey = string.Empty;
        return true;
    }

    internal void AcceptCurrent()
    {
        _acceptedConnectionKey = _observedConnectionKey;
    }

    internal bool IsAccepted =>
        _observedConnectionKey.Length > 0 &&
        string.Equals(_observedConnectionKey, _acceptedConnectionKey, StringComparison.Ordinal);

    internal void Reset()
    {
        _observedConnectionKey = string.Empty;
        _acceptedConnectionKey = string.Empty;
    }
}
```

确认只保存在内存中，不写配置。这样同一服务器重连、本地玩家重建或换图连接都会重新提醒。

### 4.4 Workshop 翻译独立生命周期

Workshop 翻译不能调用 `GameplayFeatureAccessPolicy`，也不能位于“玩法授权失效”的清理分支中。`PluginRuntimeContext.Update()` 应始终先执行 `_workshopLocalization?.Pump()`；单人转多人、多人转单人、断线、重连、菜单关闭、风险确认变化和原生 UI 切换都不是取消原因。

允许结束正在运行翻译任务的路径只有：

1. 用户点击翻译页的“取消”；
2. 用户主动发起一次新的完整扫描，明确选择重建候选集；
3. `WorkshopLocalizationService.Dispose()` 随插件真正 teardown 执行；
4. 翻译客户端自身返回完成、不可恢复错误或其既有请求超时。

资产刷新不能再通过 `OnAssetsRefreshed()` 取消当前任务。修改 `WorkshopLocalizationService` 为合并式 deferred rescan：

```csharp
private bool _deferredAssetRescan;

internal void OnAssetsRefreshed()
{
    if (_disposed || !AutoScanOnAssetsRefreshed)
        return;

    lock (_gate)
    {
        if (_translationTask != null && !_translationTask.IsCompleted)
        {
            _deferredAssetRescan = true;
            return;
        }
    }

    ScanCurrentAssets();
}

private bool TryTakeDeferredAssetRescan()
{
    lock (_gate)
    {
        if (!_deferredAssetRescan)
            return false;
        if (_translationTask != null && !_translationTask.IsCompleted)
            return false;

        _deferredAssetRescan = false;
        return AutoScanOnAssetsRefreshed && !_disposed;
    }
}
```

`Pump()` 不能在 `pendingApply == null` 时提前返回；它先按原逻辑消费 `PendingApply`，随后执行：

```csharp
if (TryTakeDeferredAssetRescan())
    ScanCurrentAssets();
```

多次资产刷新只折叠成一次 rescan。若当前任务仍在运行，不取消 token；若 `AutoScanOnAssetsRefreshed` 在任务完成前被关闭，则清 pending 但不扫描。缓存写入和翻译进度仍沿用现有 generation 检查。

## 5. 功能矩阵

| 功能域 | 单人 | 多人（未确认 / 已确认） | 专服 | 改动规则 |
| --- | --- | --- | --- | --- |
| 菜单、语言、字体、窗口、快捷键设置 | 支持 | 菜单允许；本地 UI 设置可用 | 不创建 | 使用 `CanOpenClientMenu`，不走玩法授权 |
| 资产扫描、图标、收藏 | 支持 | 允许本地处理；不保证远端资产完整 | 不运行 | 不发送玩法修改 |
| Workshop 翻译扫描、缓存、请求、应用 | 支持 | 未确认和已确认都持续运行 | 不初始化 | 完全绕过玩法 gate；仅用户取消/手动重扫、任务完成/错误或插件 Dispose 能结束当前任务 |
| 角色数值、无敌、需求、经验、声望、技能 | 支持 | best-effort | 不运行 | 每个 UI/快捷键入口与 `CheatActions` 深层均检查功能授权 |
| 给予物品、生成车辆 | 支持 | best-effort，服务器可拒绝或回滚 | 不运行 | 不新增服务端绕过或自定义 RPC |
| 传送、时间、天气、满月、空投 | 支持 | best-effort | 不运行 | 不宣称服务器权威状态已改变 |
| 飞行、穿墙 | 支持 | best-effort，可能产生纠正/断线 | 不运行 | 授权失效立即 `Restore()` |
| 准星工具、维修、删除、传送 | 支持 | best-effort | 不运行 | 授权失效关闭工具并清 HUD |
| 生物血条、小地图、其他玩家/实体显示 | 支持 | best-effort，仅客户端已加载数据 | 不运行 | 不增加远端实体获取或跨权限数据源 |
| 自动拾取 | 支持 | 确认后 best-effort；确认前不发请求 | 不运行 | 保留用户请求 `Enabled`，有效运行状态由授权派生 |
| 背包整理、快速转移、Undo | 支持 | best-effort，可能被服务器拒绝 | 不运行 | 每次执行前检查当前授权；Undo 不能跨会话 |
| Snapshot Infinite Storage、制作/补弹/分解适配器 | 支持 | 始终关闭 | 不运行 | 继续要求 `Singleplayer_<slot>`，不得创建多人身份或复用单人仓库 |
| Zombie region retention | 支持 | 关闭 | 不运行 | 这是世界生命周期修改，不进入多人 best-effort 白名单 |

## 6. 全量改动位置清单

行号基于上述 dirty 工作树快照，实施前应重新运行 `rg` 获取最新位置。

### 6.1 新增文件

| 文件 | 责任 |
| --- | --- |
| `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionPolicy.cs` | Unity-free 会话分类与菜单/支持/Snapshot 能力 |
| `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionBoundary.cs` | 从当前 `Provider`、`Dedicator`、`Player` 捕获会话 |
| `src/UnturnedSingleplayerCheatMenu/Services/GameplayFeatureAccessPolicy.cs` | 多人会话确认、玩法功能授权和重连失效；不包含 Workshop 翻译 |
| `tests/RuntimeBoundarySmoke/RuntimeBoundarySmoke.csproj` | 只链接两个纯策略源文件的 Smoke 项目；不链接含 Unity/Unturned 依赖的 runtime adapter |
| `tests/RuntimeBoundarySmoke/Program.cs` | 会话矩阵与确认生命周期测试 |
| `scripts/Launch-Unturned.ps1` | 中性启动入口，支持 Steam 对话框或明确 BattlEye launcher |
| `Launch-Unturned.cmd` | 中性 Windows 包装入口 |

### 6.2 生产源码：现有硬门与新可达入口

| 文件 / 当前位置 | 当前职责 | 必须改成 |
| --- | --- | --- |
| `Services/SingleplayerGuard.cs:6-52` | 同时判断单人、菜单和功能 | 所有调用迁移完成后删除；不保留两个相互竞争的状态源 |
| `CheatMenuPlugin.cs:26-34` | GUID、显示名、版本、进程限制 | GUID、程序集和 `BepInProcess("Unturned.exe")` 保持；显示名仍保留 Singleplayer 以标明支持范围 |
| `CheatMenuPlugin.cs:490-500` | `EnsureShortcutSingleplayer` | 改名为 `EnsureGameplayAccess`，区分未就绪、待确认、多人已确认、专服/未知状态 |
| `CheatMenuPlugin.cs:884,891-892,968-969,991,1160` | 配置描述和启动日志写死单人硬锁 | 改为“单人受支持；多人需本次会话确认；Snapshot 除外”的中英双语文本 |
| `CheatMenuPlugin.cs:1234-1243` | 菜单快捷键只认 `SingleplayerGuard.IsReady` | 使用 `CaptureCurrent().CanOpenClientMenu`；多人也打开并显示常驻警示，不能阻断翻译页 |
| `CheatMenuPlugin.cs:1252-1261` | 独立无限储存窗口只认单人 | 继续要求 `CanUseSnapshotStorage`，多人给明确状态而不是静默失败 |
| `CheatMenuPlugin.cs:1458-1499` | 开关值与单人门相与后直接写回配置 | 配置保存 requested 值；service 的 effective 值由功能授权和 Snapshot 例外计算 |
| `CheatMenuPlugin.cs:1899-1950` | 两个窗口都由单人硬门控制 | 主菜单按 `CanOpenClientMenu`；无限储存按 `CanUseSnapshotStorage` |
| `Services/PluginRuntimeComposition.cs` | 运行时所有权对象图 | 增加会话边界、确认对象或授权委托，保证所有 service 使用同一判断 |
| `Services/PluginRuntimeContext.cs:176-190,193-267` | 输入维护、菜单自动关闭、功能泵共用单人状态；`!SingleplayerGuard.IsReady` 会在第 246 行取消翻译 | `_workshopLocalization?.Pump()` 保持无条件运行并删除第 246 行；输入/菜单用 `CanOpenClientMenu`；玩法泵用 `GameplayFeatureAccessPolicy.IsAllowed`；Snapshot 和 Zombie retention 单独保持单人 |
| `Services/PluginRuntimeContext.cs:281-288` | 附近物品共享扫描只认单人 | 只有获玩法授权的客户端会话才扫描；确认前不产生后台工作 |
| `Services/CheatActions.cs:26-68,70-379` | 大部分 UI 入口无自己的模式门 | 构造时注入 `Func<bool> canExecuteGameplay`；每个修改方法入口 fail closed，维护循环同样检查 |
| `UI/CheatMenuPagePresenters.cs:38-80` | 角色页直接调用 `CheatActions` | 通过统一授权包装器调用并返回可显示的失败状态 |
| `Services/WorkshopLocalizationService.cs:171-217,329-380` | 扫描会取消旧任务，资产刷新关闭自动扫描时也会取消任务 | 保留用户手动扫描的显式重启；自动资产刷新运行中只登记 deferred rescan，关闭 AutoScan 时直接返回；session/ack 永不调用取消 |
| `CheatMenuPlugin.cs:1973-1987` | 资产刷新会根据菜单状态进入翻译刷新路径 | 保持资产事件传递，但不得借多人/菜单状态取消翻译；运行中的翻译由 service 合并 deferred rescan |
| `UI/CheatMenuUiFeaturePorts.cs:293-357,392-403,421-423,496-516` | 物品、车辆、传送、世界、仓库、翻译、整理、移动直接调用 service | 玩法动作走统一授权；翻译接口和本地 UI 设置明确绕过玩法授权；Snapshot 使用专用单人 gate |
| `AutoPickup/AutoPickupRuntime.cs:336-338,637-668` | native pickup 与 pump 的单人硬门 | 使用 effective 功能授权；确认丢失立即清 pending，不把 requested 配置写成 false |
| `InfiniteStorage/Runtime/SnapshotInfiniteStorageRuntime.cs:52-103` | 运行时和身份双层单人门 | 第一层改为 `CanUseSnapshotStorage`；第二层 `SnapshotRuntimeIdentity.TryCreate` 保持原合同 |
| `InfiniteStorage/SnapshotKernel/SnapshotRuntimePolicies.cs:7-54` | 只接受 `Singleplayer_<slot>` | 不放宽；仅把注释/错误说明写清为有意的持久化边界 |
| `InventoryArrange/InventoryArrangeFeature.cs:195-239,440-517` | 执行/预览/Undo 直接要求单人 | 执行与 Undo 使用功能授权并绑定当前 connection key；预览可读但不能跨会话执行 |
| `InventoryArrange/InventoryArrangePatch.cs:166-273` | 热键坐标修复只认单人 | 改为当前功能授权且引用本地玩家 inventory；授权失效不处理补丁 |
| `Minimap/MinimapFeature.cs:47-48,77,91-92,162-163,287` | 整个 HUD 只认单人 | 多人确认后可 best-effort；确认前隐藏并停止扫描 |
| `Minimap/ZombieRegionRetentionPolicy.cs:10-12` | 参数名和策略绑定 singleplayer | 参数改为 `retentionAllowed`；只有支持的单人会话为 true |
| `Patches/ZombieRegionRetentionPatch.cs:309` | 由单人状态启用区域保留 | 显式使用 `IsSupportedGameplay`，多人确认也不能开启 |
| `Patches/GodModePatch.cs:11-15` | 单人判断保护本地 life | 改用玩法授权并继续要求 `__instance == Player.LocalPlayer.life` |
| `Services/CreatureHealthBarService.cs:52-58,108,176` | 日志、Pump、渲染只认单人 | 使用玩法授权；诊断同时记录 `sessionKind`、`supported`、`acknowledged` |
| `Services/MovementCheatService.cs:70-177` | 开启、Pump、simulation patch 只认单人 | 使用玩法授权；授权丢失立即恢复 controller/layer/native movement |
| `Services/PointToolService.cs:144,775,1165` | Pump、动作、车辆路径只认单人 | 使用玩法授权；授权失效关功能并清理当前目标 |
| `UI/CheatMenuOverlayUi.cs:555-560,1253` | 组装 presenters、固定单人标题 | 注入授权对象；标题可保留，新增会话状态 badge |
| `UI/CheatMenuOverlayUi.AutoPickupToolsView.cs:120,501` | 自动拾取启用失败和空列表提示写死“进入单人” | 改为 requested/effective 状态与当前 session/ack 原因，不把多人未确认误报为无库存 |
| `UI/CheatMenuOverlayUi.ModalViews.cs` | 已有模态 UI 宿主 | 新增只由首次玩法动作触发的多人风险确认层；打开菜单和翻译操作不得触发该 modal |
| `UI/CheatMenuOverlayUi.FluentShell.cs` | 顶部 shell | 加常驻 `单人（受支持）` / `多人或服务器（不受支持）` badge |
| `Services/PluginLocalization.cs:205,614-616,971,1007,1010,1077` | 单人硬拒绝与标题翻译 | 保留产品名翻译；替换硬拒绝文本，新增风险、确认、状态 badge、Snapshot 单人例外文本 |

### 6.3 BattlEye 与启动器

| 文件 | 必须改动 |
| --- | --- |
| `scripts/Launch-Unturned-Singleplayer.ps1:20-37` | 删除 `Unturned_BE` 进程和 `BEService` 运行即抛错的两段；中性入口不得停止服务 |
| `scripts/Launch-Unturned.ps1` | 新增 `Dialog` / `BattlEye` 两种模式；默认仍打开 Steam 选择对话框 |
| `Launch-Unturned-Singleplayer.cmd` | 保留为兼容包装器，转调 `Launch-Unturned.ps1 -Mode Dialog` 并打印旧名称提示 |
| `Launch-Unturned.cmd` | 新增推荐入口，并把第二个参数传给 `-Mode` |
| `scripts/Build-PortablePackage.ps1:35-70` | 安装说明从 “Use only in singleplayer without BattlEye” 改为精确四层边界文本 |

中性启动器的完整目标行为：

```powershell
[CmdletBinding()]
param(
    [string]$GameRoot = $env:UNTURNED_GAME_DIR,
    [ValidateSet('Dialog', 'BattlEye')]
    [string]$Mode = 'Dialog'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    throw 'Specify -GameRoot or set the UNTURNED_GAME_DIR environment variable.'
}

$gameRootFull = [IO.Path]::GetFullPath($GameRoot)
$gameExecutable = Join-Path $gameRootFull 'Unturned.exe'
$battleyeExecutable = Join-Path $gameRootFull 'Unturned_BE.exe'
$plugin = Join-Path $gameRootFull 'BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll'

foreach ($required in @($gameExecutable, $plugin)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file not found: $required"
    }
}

if (Get-Process -Name 'Unturned' -ErrorAction SilentlyContinue) {
    Write-Host 'Unturned is already running.'
    exit 0
}

if ($Mode -eq 'BattlEye') {
    if (-not (Test-Path -LiteralPath $battleyeExecutable -PathType Leaf)) {
        throw "BattlEye launcher not found: $battleyeExecutable"
    }
    Start-Process -FilePath $battleyeExecutable -WorkingDirectory $gameRootFull
    Write-Host 'BattlEye launch requested. BepInEx/plugin loading is not guaranteed.'
    exit 0
}

Start-Process 'steam://launch/304930/dialog'
Write-Host 'Steam launch options opened. Choose BattlEye or no BattlEye yourself.'
```

### 6.4 测试与解决方案

| 文件 | 必须改动 |
| --- | --- |
| `UnturnedSingleplayerCheatMenu.slnx` | 加入 `tests/RuntimeBoundarySmoke/RuntimeBoundarySmoke.csproj` |
| `tests/RuntimeBoundarySmoke/*` | 覆盖 8 种会话、菜单能力、多人确认、重连清空和 Snapshot 例外 |
| `tests/AssemblyMetadataSmoke/Program.cs` | 断言本地 `SDG.Unturned.Dedicator.isStandaloneDedicatedServer` public getter 存在 |
| `tests/AutoPickupSmoke/Program.cs:301` | 删除“每个 pump 必须保留单人硬门”的字符串断言，改为确认前零请求、确认失效清 pending、requested/effective 分离 |
| `tests/MinimapSmoke/Program.cs:29-33` | 改名 `singleplayerReady` 参数/断言；继续证明 Zombie retention 只有受支持单人状态可开 |
| `tests/SnapshotKernelSmoke/Program.cs:193-215` | 保留并强化非单人身份 fail-closed；错误消息明确这是 Snapshot 例外而非全插件边界 |
| `tests/LocalizationSmoke/Program.cs:24-25,169,269` | 更新边界文本与中英风险警告、按钮、badge；同步静态模板计数 |
| `tests/UiArchitectureSmoke/Program.cs` | 断言菜单门和玩法门分离、多人 modal 存在、所有直接 action port 走授权、Dedicated 分支早于 `PluginUiRoot.Create` |
| `tests/ConfigInitializationSmoke/Program.cs` | 断言边界调整未改 GUID/config path，且 requested feature 状态不因无授权被写回 false |
| `tests/InventoryArrangePlannerSmoke/Program.cs` | 新增 connection key 变化后 Undo journal 不可继续的断言 |
| `tests/WorkshopLocalizationSmoke/Program.cs` | 断言 RuntimeContext 的非玩法分支没有 `CancelTranslation()`，资产刷新不取消活动任务，并存在 deferred rescan |

`RuntimeBoundarySmoke` 核心断言必须实际包含：

```csharp
using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

RuntimeSessionState singleplayer = RuntimeSessionPolicy.Evaluate(
    true, true, true, false, "Singleplayer_0", true);
RuntimeSessionState multiplayer = RuntimeSessionPolicy.Evaluate(
    true, false, true, false, "PublicServer", true);
RuntimeSessionState dedicated = RuntimeSessionPolicy.Evaluate(
    true, true, false, true, "DedicatedServer", false);

Check(singleplayer.Kind == RuntimeSessionKind.Singleplayer &&
      singleplayer.CanOpenClientMenu && singleplayer.CanUseSnapshotStorage,
    "Singleplayer must retain full supported access.");
Check(multiplayer.Kind == RuntimeSessionKind.MultiplayerClient &&
      multiplayer.CanOpenClientMenu && !multiplayer.CanUseSnapshotStorage,
    "Multiplayer must open the menu without opening Snapshot storage.");
Check(GameplayFeatureAccessPolicy.Evaluate(multiplayer, false) ==
      GameplayFeatureAccess.RequiresMultiplayerAcknowledgement,
    "Multiplayer gameplay must wait for per-session acknowledgement.");
Check(GameplayFeatureAccessPolicy.Evaluate(multiplayer, true) ==
      GameplayFeatureAccess.AllowedUnsupportedMultiplayer,
    "Acknowledged multiplayer gameplay must use the unsupported path.");
Check(dedicated.Kind == RuntimeSessionKind.DedicatedServer &&
      !dedicated.CanOpenClientMenu && !dedicated.CanUseSnapshotStorage,
    "Dedicated server must stay headless and inert.");

MultiplayerRiskAcknowledgement acknowledgement = new MultiplayerRiskAcknowledgement();
acknowledgement.Observe("MultiplayerClient|PublicServer|PEI|1001");
acknowledgement.AcceptCurrent();
Check(acknowledgement.IsAccepted, "Current multiplayer connection must be accepted.");
acknowledgement.Observe("MultiplayerClient|PublicServer|PEI|1002");
Check(!acknowledgement.IsAccepted, "A new player instance must reset acknowledgement.");
```

### 6.5 用户文档与发布文字

| 文件 / 当前位置 | 必须改动 |
| --- | --- |
| `README.md:5-9` | 首屏改为“单人玩法目标 + 多人可开菜单 + 翻译不受多人 gate 影响 + BE 不保证” |
| `README.md:108-119` | 用目标会话矩阵替换四条件全局硬门说明，并把 Workshop 翻译明确列为本地独立功能 |
| `README.md:135-154` | 安装步骤允许选择 BE；删除“不要进入多人”的绝对句，增加惰性玩法风险确认和加载证据区分 |
| `README.md:208-216` | 架构说明从 `SingleplayerGuard` 改为 Session/Feature 两层 gate；Snapshot 例外单列 |
| `README.md:228-237` | 免责声明和验收改为单人支持、多人与 BE 自担风险；不把 Smoke 当 BE/多人证明 |
| `README.en.md:5-9,57,84-87,140-147` | 与中文 README 同步，不能出现语义较软或较强的另一套边界 |
| `SECURITY.md:7-18` | 改写 Intended environment；说明 BE/多人加载不保证、无绕过、专服 no-op，并声明翻译不进入 gameplay gate |
| `SECURITY.md:24-31` | 报告模板增加 session kind、BE launch、preloader、chainloader、plugin discovered、server type 六项 |
| `CONTRIBUTING.md:3,24-38` | 允许边界层和多人警示改动；仍禁止反作弊绕过、隐藏网络流量和把多人标为受支持 |
| `CHANGELOG.md:5` | 只在 `[Unreleased]` 新增未来变更；历史版本中的单人证据保持原样 |
| `docs/IMPORTANT_UNTURNED_ITEM_API_LIVING_REFERENCE.md:193-201,355` | 明确 Snapshot v1 继续单人硬门；不要把全插件新边界误写成 Snapshot 已支持多人 |

建议 README/安装包使用以下中文规范文本：

> 本插件的玩法功能仅以 Unturned 单人世界为设计、测试和兼容性目标。插件可以保持安装并允许你选择 BattlEye 启动；若 BepInEx 成功加载，菜单也可以在已生成本地玩家的多人客户端会话中打开。多人/服务器会话中的玩法功能属于不受支持的自担风险使用。Workshop 翻译是本地内容功能，不受单人/多人玩法门或多人风险确认状态影响，也不会因为进入多人而自动停止。项目不会停止、禁用、修改或绕过 BattlEye，也不保证 BattlEye 会允许 BepInEx、本插件或当前连接继续运行。

对应英文必须固定为：

> Gameplay features are designed, tested, and supported only for Unturned singleplayer worlds. The plugin may remain installed while you choose a BattlEye launch, and the menu may open in a multiplayer client session if BepInEx successfully loads and the local player exists. Gameplay-feature use in multiplayer/server sessions is unsupported and at your own risk. Workshop translation is a local content feature: it is not controlled by the singleplayer/multiplayer gameplay gate or multiplayer-risk acknowledgement, and entering multiplayer does not cancel it. The project does not stop, disable, modify, or bypass BattlEye, and does not guarantee that BattlEye will allow BepInEx, this plugin, or the connection to continue.

### 6.6 历史文档与稳定身份：明确不改

下列内容看似含有 “singleplayer”，但本次不能机械重命名：

- `CHANGELOG.md` 已发布/已记录版本的历史事实不回写，只在 `[Unreleased]` 增加新边界。
- `docs/2026-08-30_unturned-ugui-control-library-redesign-plan.md`、`docs/2026-08-31_unturned-core-framework-localization-implementation-plan.md`、`docs/2026-09-01_other-page-world-maintenance-implementation-plan.md` 是日期化历史方案；不篡改旧验收语境。
- `com.codex.unturned.singleplayer-cheat-menu`、`UnturnedSingleplayerCheatMenu.dll`、`UnturnedSingleplayerCheatMenu` namespace、插件目录、配置文件名、收藏/传送文件名保留。
- `InfiniteStorageSnapshots/v1`、`snapshot-storage-identity-v1`、`identity-singleplayer-server-id-mismatch` 保留；它们是持久化 ABI 和诊断合同。
- `Singleplayer Cheat Menu` 产品显示名可以保留，因为它描述受支持功能目标。若以后重品牌，必须另写带旧配置/数据/部署去重迁移的独立计划。

## 7. 实施任务

### Task 1: 锁定会话矩阵并先写失败的纯策略测试

**Files:**

- Create: `tests/RuntimeBoundarySmoke/RuntimeBoundarySmoke.csproj`
- Create: `tests/RuntimeBoundarySmoke/Program.cs`
- Modify: `UnturnedSingleplayerCheatMenu.slnx`

**Interfaces:**

- Consumes: 本文第 3、4 节会话矩阵与签名。
- Produces: 可独立编译的 `RuntimeSessionKind`、`RuntimeSessionState`、`GameplayFeatureAccess` 行为合同。

- [ ] **Step 1: 创建只链接预期生产文件的 Smoke 项目**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\src\UnturnedSingleplayerCheatMenu\Services\RuntimeSessionPolicy.cs" Link="RuntimeSessionPolicy.cs" />
    <Compile Include="..\..\src\UnturnedSingleplayerCheatMenu\Services\GameplayFeatureAccessPolicy.cs" Link="GameplayFeatureAccessPolicy.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 写入第 6.4 节全部矩阵断言并加入 Frontend、Loading、ListenServer、UnknownConnected 四个额外案例**
- [ ] **Step 3: 运行测试并确认因为生产类型尚不存在而失败**

Run: `dotnet run --project .\tests\RuntimeBoundarySmoke\RuntimeBoundarySmoke.csproj -c Release`

Expected: 非零退出，错误明确指向缺失的 `RuntimeSessionPolicy` / `GameplayFeatureAccessPolicy` 源文件或类型。

- [ ] **Step 4: 把新 Smoke 项目加入 `.slnx`，只暂存本任务文件并提交**

```powershell
git add -- 'tests/RuntimeBoundarySmoke/RuntimeBoundarySmoke.csproj' 'tests/RuntimeBoundarySmoke/Program.cs'
git add -p -- 'UnturnedSingleplayerCheatMenu.slnx'
git diff --cached --name-only
git diff --cached --check
git commit -m "test: define runtime session boundary"
```

### Task 2: 实现会话分类、多人确认和本地 ABI 探针

**Files:**

- Create: `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionPolicy.cs`
- Create: `src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionBoundary.cs`
- Create: `src/UnturnedSingleplayerCheatMenu/Services/GameplayFeatureAccessPolicy.cs`
- Modify: `tests/AssemblyMetadataSmoke/Program.cs`
- Test: `tests/RuntimeBoundarySmoke/Program.cs`

**Interfaces:**

- Consumes: `Provider.isConnected/isServer/isClient/serverID`、`Dedicator.isStandaloneDedicatedServer`、`Player.LocalPlayer`。
- Produces: `CaptureCurrent()`、`CaptureConnectionKey(...)`、`Evaluate(...)`、`IsAllowed(...)`、`MultiplayerRiskAcknowledgement`。

- [ ] **Step 1: 按第 4.1 至 4.3 节实现两个纯策略源文件和一个 runtime adapter；第 4.4 节翻译生命周期留给 Task 4**
- [ ] **Step 2: 在 AssemblyMetadataSmoke 中读取 `SDG.Unturned.Dedicator` 的 properties，并断言 `isStandaloneDedicatedServer` getter 是 public static**

```csharp
TypeDefinition dedicator = types["SDG.Unturned.Dedicator"];
PropertyDefinition dedicatedProperty = dedicator.GetProperties()
    .Select(handle => reader.GetPropertyDefinition(handle))
    .Single(property => reader.GetString(property.Name) == "isStandaloneDedicatedServer");
MethodDefinition dedicatedGetter = reader.GetMethodDefinition(dedicatedProperty.GetAccessors().Getter);
if ((dedicatedGetter.Attributes & System.Reflection.MethodAttributes.Public) == 0 ||
    (dedicatedGetter.Attributes & System.Reflection.MethodAttributes.Static) == 0)
    throw new InvalidOperationException("Dedicator.isStandaloneDedicatedServer must remain public static.");
```

- [ ] **Step 3: 运行两个聚焦 Smoke**

```powershell
dotnet run --project .\tests\RuntimeBoundarySmoke\RuntimeBoundarySmoke.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'RuntimeBoundarySmoke failed.' }
dotnet run --project .\tests\AssemblyMetadataSmoke\AssemblyMetadataSmoke.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'AssemblyMetadataSmoke failed.' }
```

Expected: 两个项目均退出 0；本地 ABI hash 未漂移。

- [ ] **Step 4: 只暂存本任务文件并提交**

```powershell
git add -- 'src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionPolicy.cs' 'src/UnturnedSingleplayerCheatMenu/Services/RuntimeSessionBoundary.cs' 'src/UnturnedSingleplayerCheatMenu/Services/GameplayFeatureAccessPolicy.cs'
git add -p -- 'tests/AssemblyMetadataSmoke/Program.cs'
git diff --cached --name-only
git diff --cached --check
git commit -m "feat: classify client and server sessions"
```

### Task 3: 让 Dedicated Server 分支在 UI 初始化前 inert

**Files:**

- Modify: `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs:856-875`
- Modify: `tests/UiArchitectureSmoke/Program.cs`

**Interfaces:**

- Consumes: `Dedicator.isStandaloneDedicatedServer`。
- Produces: Dedicated Server 启动日志和不构造客户端 runtime graph 的早退路径。

- [ ] **Step 1: 先加静态架构断言，要求 Dedicated 判断位于 `Initialize()` 和 `PluginUiRoot.Create` 之前**
- [ ] **Step 2: 在 `Awake()` 中设置 `Instance` 后立即分支**

```csharp
private void Awake()
{
    Instance = this;
    if (Dedicator.isStandaloneDedicatedServer)
    {
        Logger.LogWarning("Client UI/gameplay features are inactive on a standalone dedicated server.");
        return;
    }
    _runtimeContext = new PluginRuntimeContext(this, Logger, _inputCapture);
    try
    {
        Initialize();
    }
    catch (Exception exception)
    {
        Logger.LogError($"插件初始化失败，正在执行统一回滚。\n{exception}");
        TransferRuntimeOwnership();
        Teardown("Awake initialization failed");
        throw;
    }
}
```

- [ ] **Step 3: 构建并运行 UiArchitectureSmoke**

Run: `dotnet run --project .\tests\UiArchitectureSmoke\UiArchitectureSmoke.csproj -c Release`

Expected: PASS，且早退分支不创建 Canvas、EventSystem、Harmony runtime patch 或客户端 service。

- [ ] **Step 4: 若 U3DS 在程序集装载阶段因缺少 UI 依赖而失败，停止当前实现，不改为吞异常；另拆 client DLL 与 dedicated bootstrap 的独立计划**
- [ ] **Step 5: 暂存并提交精确文件**

```powershell
git add -p -- 'src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs' 'tests/UiArchitectureSmoke/Program.cs'
git diff --cached --name-only
git diff --cached --check
git commit -m "fix: keep dedicated server bootstrap inert"
```

### Task 4: 分离菜单就绪与玩法授权，接入多人风险确认

**Files:**

- Modify: `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/Services/PluginRuntimeComposition.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/Services/PluginRuntimeContext.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationService.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.AutoPickupToolsView.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.FluentShell.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.ModalViews.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/UI/CheatMenuUiFeaturePorts.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/Services/PluginLocalization.cs`
- Test: `tests/UiArchitectureSmoke/Program.cs`
- Test: `tests/LocalizationSmoke/Program.cs`
- Test: `tests/WorkshopLocalizationSmoke/Program.cs`

**Interfaces:**

- Consumes: Task 2 的 session state、connection key、acknowledgement 和 access decision。
- Produces: `CanOpenClientMenu` 菜单门、`EnsureGameplayAccess(actionId)` 玩法门、多人风险 modal、常驻 session badge；Workshop 翻译不经过玩法门。

- [ ] **Step 1: 写失败断言，证明 multiplayer 可以打开菜单、未确认时 gameplay action 被阻断，但 Workshop 翻译仍继续 Pump**
- [ ] **Step 2: 在 WorkshopLocalizationSmoke 加入以下静态生命周期契约**

```csharp
string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string runtimeContext = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "PluginRuntimeContext.cs"));
string translationService = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "Services", "WorkshopLocalizationService.cs"));
string translationView = File.ReadAllText(Path.Combine(repoRoot, "src", "UnturnedSingleplayerCheatMenu", "UI", "CheatMenuOverlayUi.TranslationToolsView.cs"));
int assetRefreshStart = translationService.IndexOf("internal void OnAssetsRefreshed()", StringComparison.Ordinal);
int reloadCacheStart = assetRefreshStart < 0
    ? -1
    : translationService.IndexOf("internal void ReloadCache()", assetRefreshStart, StringComparison.Ordinal);
Check(assetRefreshStart >= 0 && reloadCacheStart > assetRefreshStart,
    "Workshop asset-refresh method boundaries were not found.");
string assetRefresh = translationService.Substring(assetRefreshStart, reloadCacheStart - assetRefreshStart);

Check(!runtimeContext.Contains("_workshopLocalization?.CancelTranslation()", StringComparison.Ordinal),
    "Runtime session changes must never cancel Workshop translation.");
Check(!assetRefresh.Contains("CancelTranslation()", StringComparison.Ordinal),
    "Automatic asset refresh must not cancel an active translation.");
Check(translationService.Contains("private bool _deferredAssetRescan;", StringComparison.Ordinal) &&
      translationService.Contains("TryTakeDeferredAssetRescan()", StringComparison.Ordinal) &&
      translationService.Contains("if (_disposed || !AutoScanOnAssetsRefreshed)", StringComparison.Ordinal),
    "Asset refresh must defer rescan without cancelling the active translation.");
Check(!translationService.Contains("GameplayFeatureAccessPolicy", StringComparison.Ordinal) &&
      !translationView.Contains("GameplayFeatureAccessPolicy", StringComparison.Ordinal),
    "Workshop translation must remain outside the multiplayer gameplay gate.");
```

- [ ] **Step 3: 把 `HandleToggleShortcut` 和 `SetMenuOpen` 改为只检查 `CanOpenClientMenu`；打开多人菜单只显示常驻警示，不弹阻断 modal**
- [ ] **Step 4: 每帧 Observe connection key；断线或 key 变化时只停止玩法自动化、恢复移动并清 gameplay UI state，不触碰翻译 task、token、progress、cache 或 deferred rescan**
- [ ] **Step 5: 删除 `PluginRuntimeContext.Update()` 中由 `!SingleplayerGuard.IsReady` 触发的 `_workshopLocalization?.CancelTranslation()`**
- [ ] **Step 6: 按第 4.4 节实现 asset refresh 合并式 deferred rescan；AutoScan 关闭时不取消活动翻译**
- [ ] **Step 7: 将 `EnsureShortcutSingleplayer` 改为 `EnsureGameplayAccess`；只有首次玩法动作把 `RequiresMultiplayerAcknowledgement` 映射为风险 modal，翻译命令不调用它**
- [ ] **Step 8: 风险 modal 仅允许以下两个动作**

```text
本次会话继续 / Continue This Session
关闭菜单 / Close Menu
```

- [ ] **Step 9: Header 始终显示会话 badge；多人确认后仍显示“不受支持”，不能变成绿色“安全”**
- [ ] **Step 10: 运行 RuntimeBoundarySmoke、LocalizationSmoke、WorkshopLocalizationSmoke、UiArchitectureSmoke**
- [ ] **Step 11: 暂存本任务精确路径并提交**

```powershell
git add -p -- 'src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs' 'src/UnturnedSingleplayerCheatMenu/Services/PluginRuntimeComposition.cs' 'src/UnturnedSingleplayerCheatMenu/Services/PluginRuntimeContext.cs' 'src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationService.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.AutoPickupToolsView.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.FluentShell.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.ModalViews.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuUiFeaturePorts.cs' 'src/UnturnedSingleplayerCheatMenu/Services/PluginLocalization.cs' 'tests/UiArchitectureSmoke/Program.cs' 'tests/LocalizationSmoke/Program.cs' 'tests/WorkshopLocalizationSmoke/Program.cs'
git diff --cached --name-only
git diff --cached --check
git commit -m "feat: separate multiplayer gameplay gate from translation"
```

### Task 5: 迁移所有玩法 gate，并补齐以前由“菜单打不开”间接保护的调用

**Files:**

- Modify: 第 6.2 节列出的所有 action、automation、movement、map、patch、inventory 文件
- Delete: `src/UnturnedSingleplayerCheatMenu/Services/SingleplayerGuard.cs`
- Test: `tests/AutoPickupSmoke/Program.cs`
- Test: `tests/MinimapSmoke/Program.cs`
- Test: `tests/InventoryArrangePlannerSmoke/Program.cs`
- Test: `tests/UiArchitectureSmoke/Program.cs`

**Interfaces:**

- Consumes: `GameplayFeatureAccessPolicy.IsAllowed(...)` 和当前 connection key。
- Produces: UI、快捷键、service、Harmony patch 四层一致的执行门。

- [ ] **Step 1: 先让 UiArchitectureSmoke 枚举 `CheatMenuUiFeaturePorts` 中所有直接玩法入口，并断言它们调用统一授权包装器**
- [ ] **Step 2: 给 `CheatActions` 每个 mutating method 加深层 gate；getter/capture 不误拦**
- [ ] **Step 3: 将 50 处 `SingleplayerGuard` 引用逐一迁移，不做盲目全局替换**
- [ ] **Step 4: 自动拾取、移动、Point Tool、health bar、minimap 在授权失效时清瞬时状态，但保留 requested 配置**
- [ ] **Step 5: Inventory Arrange journal 记录 connection key；key 不同则 Undo fail closed**
- [ ] **Step 6: GodMode Patch 保留 local-player identity；Zombie retention 永远只认 `IsSupportedGameplay`**
- [ ] **Step 7: 删除旧 guard 后执行零残留搜索**

```powershell
$hits = rg -n -S 'SingleplayerGuard|EnsureShortcutSingleplayer' .\src .\tests
if ($LASTEXITCODE -eq 0 -or $hits) { throw "Old guard remains:`n$hits" }
if ($LASTEXITCODE -ne 1) { throw "rg failed with exit code $LASTEXITCODE" }
```

- [ ] **Step 8: 运行受影响的聚焦 Smoke，再提交**

```powershell
foreach ($project in @(
    '.\tests\RuntimeBoundarySmoke\RuntimeBoundarySmoke.csproj',
    '.\tests\AutoPickupSmoke\AutoPickupSmoke.csproj',
    '.\tests\MinimapSmoke\MinimapSmoke.csproj',
    '.\tests\InventoryArrangePlannerSmoke\InventoryArrangePlannerSmoke.csproj',
    '.\tests\UiArchitectureSmoke\UiArchitectureSmoke.csproj')) {
    dotnet run --project $project -c Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke failed: $project" }
}
```

```powershell
git add -p -- 'src/UnturnedSingleplayerCheatMenu/AutoPickup/AutoPickupRuntime.cs' 'src/UnturnedSingleplayerCheatMenu/InfiniteStorage/Runtime/SnapshotInfiniteStorageRuntime.cs' 'src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangeFeature.cs' 'src/UnturnedSingleplayerCheatMenu/InventoryArrange/InventoryArrangePatch.cs' 'src/UnturnedSingleplayerCheatMenu/Minimap/MinimapFeature.cs' 'src/UnturnedSingleplayerCheatMenu/Minimap/ZombieRegionRetentionPolicy.cs' 'src/UnturnedSingleplayerCheatMenu/Patches/GodModePatch.cs' 'src/UnturnedSingleplayerCheatMenu/Patches/ZombieRegionRetentionPatch.cs' 'src/UnturnedSingleplayerCheatMenu/Services/CheatActions.cs' 'src/UnturnedSingleplayerCheatMenu/Services/CreatureHealthBarService.cs' 'src/UnturnedSingleplayerCheatMenu/Services/MovementCheatService.cs' 'src/UnturnedSingleplayerCheatMenu/Services/PointToolService.cs' 'src/UnturnedSingleplayerCheatMenu/Services/SingleplayerGuard.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuPagePresenters.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuUiFeaturePorts.cs' 'src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.AutoPickupToolsView.cs' 'tests/AutoPickupSmoke/Program.cs' 'tests/MinimapSmoke/Program.cs' 'tests/InventoryArrangePlannerSmoke/Program.cs' 'tests/UiArchitectureSmoke/Program.cs'
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: separate feature access from singleplayer support"
```

提交前必须用 `git diff --cached --name-only` 检查暂存区；若包含当前任务外的既有脏改动，取消那些路径的暂存而不是重置文件内容。

### Task 6: 固化 Snapshot 单人例外与 requested/effective 状态

**Files:**

- Modify: `src/UnturnedSingleplayerCheatMenu/InfiniteStorage/Runtime/SnapshotInfiniteStorageRuntime.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/InfiniteStorage/SnapshotKernel/SnapshotRuntimePolicies.cs`
- Modify: `src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs`
- Test: `tests/SnapshotKernelSmoke/Program.cs`
- Test: `tests/SnapshotRuntimeIntegrationSmoke/Program.cs`
- Test: `tests/ConfigInitializationSmoke/Program.cs`

**Interfaces:**

- Consumes: `RuntimeSessionState.CanUseSnapshotStorage`。
- Produces: 多人中永不激活 Snapshot session、永不触碰 Snapshot v1 文件、requested 设置不被覆盖。

- [ ] **Step 1: 增加多人已确认仍无法创建 Snapshot identity 的测试**
- [ ] **Step 2: Pump 在 `CanUseSnapshotStorage=false` 时 reset 内存 session，但不写磁盘、不删除 marker、不改配置**
- [ ] **Step 3: Store/Withdraw/Crafting/AmmoRefill/Decomposition 每个入口继续 fail closed**
- [ ] **Step 4: 验证 `Singleplayer_0` 到 `Singleplayer_4` 的现有路径与 hash 完全不变**
- [ ] **Step 5: 运行所有六个 Snapshot 相关 Smoke 与 ConfigInitializationSmoke**
- [ ] **Step 6: 只提交本任务路径**

```powershell
git add -p -- 'src/UnturnedSingleplayerCheatMenu/InfiniteStorage/Runtime/SnapshotInfiniteStorageRuntime.cs' 'src/UnturnedSingleplayerCheatMenu/InfiniteStorage/SnapshotKernel/SnapshotRuntimePolicies.cs' 'src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs' 'tests/SnapshotKernelSmoke/Program.cs' 'tests/SnapshotRuntimeIntegrationSmoke/Program.cs' 'tests/ConfigInitializationSmoke/Program.cs'
git diff --cached --name-only
git diff --cached --check
git commit -m "fix: keep snapshot persistence singleplayer-only"
```

### Task 7: 放开 BattlEye 启动选择但不接触反作弊

**Files:**

- Create: `scripts/Launch-Unturned.ps1`
- Create: `Launch-Unturned.cmd`
- Modify: `scripts/Launch-Unturned-Singleplayer.ps1`
- Modify: `Launch-Unturned-Singleplayer.cmd`
- Modify: `scripts/Build-PortablePackage.ps1`

**Interfaces:**

- Consumes: `GameRoot`、`UNTURNED_GAME_DIR`、`Mode=Dialog|BattlEye`。
- Produces: 不停止服务、不拒绝 BE 的中性 launcher；旧入口兼容转发。

- [ ] **Step 1: 写入第 6.3 节完整中性 PowerShell 入口**
- [ ] **Step 2: 旧 PowerShell 入口只转调新入口，不再查询 `BEService` 或抛 BE 错误**
- [ ] **Step 3: 新旧 `.cmd` 均只负责参数转发，不启动/停止 Windows 服务**
- [ ] **Step 4: 用静态搜索证明 launcher 没有 `Stop-Service`、`sc stop`、`taskkill`、BE 拒绝文案**

```powershell
$launcherText = Get-Content -LiteralPath '.\scripts\Launch-Unturned.ps1' -Raw -Encoding UTF8
foreach ($forbidden in @('Stop-Service', 'sc stop', 'taskkill', 'Exit it before', 'service is still running')) {
    if ($launcherText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Forbidden launcher behavior remains: $forbidden"
    }
}
```

- [ ] **Step 5: 在游戏未运行时分别验证 `Dialog` 打开 Steam 选择器、`BattlEye` 启动 `Unturned_BE.exe`；只记录启动事实，不宣称插件加载**
- [ ] **Step 6: 提交 launcher/package 文本**

```powershell
git add -- 'scripts/Launch-Unturned.ps1' 'Launch-Unturned.cmd'
git add -p -- 'scripts/Launch-Unturned-Singleplayer.ps1' 'Launch-Unturned-Singleplayer.cmd' 'scripts/Build-PortablePackage.ps1'
git diff --cached --name-only
git diff --cached --check
git commit -m "feat: allow explicit BattlEye launch selection"
```

### Task 8: 同步中英边界文档与历史记录

**Files:**

- Modify: `README.md`
- Modify: `README.en.md`
- Modify: `SECURITY.md`
- Modify: `CONTRIBUTING.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/IMPORTANT_UNTURNED_ITEM_API_LIVING_REFERENCE.md`

**Interfaces:**

- Consumes: 第 1、3、5 节产品合同。
- Produces: 中文、英文、贡献、安全、发布和 Snapshot 活文档一致的边界，并明确翻译生命周期独立于多人玩法 gate。

- [ ] **Step 1: 把第 6.5 节固定中英文声明放在两个 README 首屏**
- [ ] **Step 2: 加入会话矩阵和证据层；明确 Dedicated Server 只 no-op**
- [ ] **Step 3: 两个 README 都明确写出 Workshop 翻译在多人未确认、已确认和会话切换期间不会自动停止**
- [ ] **Step 4: SECURITY 区分受支持缺陷和不受支持运行报告，并保留敏感日志脱敏规则**
- [ ] **Step 5: CONTRIBUTING 允许边界实现，但拒绝把 gameplay gate 套到翻译或把 multiplayer/BE Smoke 写成完成证明**
- [ ] **Step 6: CHANGELOG 只追加 `[Unreleased]`，不改历史版本陈述**
- [ ] **Step 7: 活文档固定 Snapshot 单人身份；运行边界扫描检查矛盾绝对句**

```powershell
rg -n -i -S 'only.*without BattlEye|仅限无 BattlEye|does not support multiplayer|不支持多人服务器|refuses to continue|拒绝继续' README.md README.en.md SECURITY.md CONTRIBUTING.md scripts
```

Expected: 旧的全局禁止语句为零；Snapshot 单人限制、“不保证 BE/多人兼容”和“翻译不因多人自动停止”仍明确存在。

- [ ] **Step 8: 提交精确文档路径**

```powershell
git add -p -- 'README.md' 'README.en.md' 'SECURITY.md' 'CONTRIBUTING.md' 'CHANGELOG.md' 'docs/IMPORTANT_UNTURNED_ITEM_API_LIVING_REFERENCE.md'
git diff --cached --name-only
git diff --cached --check
git commit -m "docs: define unsupported multiplayer boundary"
```

### Task 9: 全量自动化验证

**Files:**

- Verify only: 当前解决方案和 `tests/**/*Smoke.csproj`

**Interfaces:**

- Consumes: Tasks 1-8 的完整源码和文档。
- Produces: Release、每个 Smoke、静态边界扫描、diff hygiene 四类证据。

- [ ] **Step 1: Release 构建主解决方案**

```powershell
dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
```

- [ ] **Step 2: 递归发现、逐个 rebuild、逐个 `--no-build` 运行所有 Smoke**

```powershell
$projects = Get-ChildItem -LiteralPath '.\tests' -Recurse -Filter '*.csproj' | Sort-Object FullName
foreach ($project in $projects) {
    dotnet build $project.FullName -c Release
    if ($LASTEXITCODE -ne 0) { throw "Smoke build failed: $($project.FullName)" }
    dotnet run --project $project.FullName -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Smoke run failed: $($project.FullName)" }
}
Write-Host "ALL_SMOKE_PASSED=$($projects.Count)/$($projects.Count)"
```

- [ ] **Step 3: 运行零残留和身份稳定扫描**

```powershell
rg -n -S 'SingleplayerGuard|EnsureShortcutSingleplayer' .\src .\tests
if ($LASTEXITCODE -eq 0) { throw 'Legacy global guard remains.' }
if ($LASTEXITCODE -ne 1) { throw "rg failed with exit code $LASTEXITCODE" }

rg -n -S 'com\.codex\.unturned\.singleplayer-cheat-menu|UnturnedSingleplayerCheatMenu\.dll|snapshot-storage-identity-v1' .\src README.md README.en.md scripts
if ($LASTEXITCODE -ne 0) { throw 'Stable identity strings are missing.' }
```

- [ ] **Step 4: 检查整个任务 diff，不要求工作树 clean**

```powershell
git diff --check
git status --short
```

Expected: 任务触及文件无 whitespace error；既有无关改动仍存在且内容未被改写。

### Task 10: 分层真实运行验收

**Files:**

- Evidence output only: `artifacts/runtime-boundary-acceptance/<timestamp>/`

**Interfaces:**

- Consumes: 已构建、已安全部署且 SHA-256 与构建产物一致的单一 DLL。
- Produces: 单人、多人无 BE、跨会话翻译连续性、BattlEye 启动、Dedicated no-op 五条彼此独立的证据。

- [ ] **Step 1: 停止游戏，备份活动 DLL，部署一份新 DLL，核对 source/target SHA-256**
- [ ] **Step 2: 无 BE 单人验收：菜单、所有页、移动恢复、AutoPickup、整理、Snapshot、场景切换**
- [ ] **Step 3: 无 BE 私有多人验收：菜单打开后只有常驻警示，翻译页立即可用；第一次玩法动作才出现风险 modal，确认后 badge 仍保持警告，断线重连后玩法确认失效**
- [ ] **Step 4: 在单人启动一个足够长的 Workshop 翻译批次后退出世界并加入私有多人；确认状态不变为 Cancelled、完成数继续增长、缓存继续落盘，资产刷新只在当前批次完成后合并执行一次 rescan**
- [ ] **Step 5: 多人确认前验证 AutoPickup/移动/Point Tool/health bar/minimap 都不产生后台动作，同时翻译 Pump、进度和结果应用继续**
- [ ] **Step 6: 确认后只在操作者控制的私有服务器做一个可回滚的瞬时玩法动作，记录服务器接受/拒绝/纠正，不把结果推广为兼容承诺**
- [ ] **Step 7: 验证多人中 Snapshot 页面明确不可用，且单人 Snapshot 目录所有文件 hash/mtime 均未改变**
- [ ] **Step 8: 使用 `Launch-Unturned.ps1 -Mode BattlEye`，分别记录 BE launcher 成功、Unturned 子进程、BepInEx preloader、chainloader、plugin discovered 五个状态**
- [ ] **Step 9: 若 BE 阻止 `winhttp.dll`、preloader、插件或连接，记录原始日志并停止该验收线；不得改成规避或宣称插件缺陷**
- [ ] **Step 10: 在可丢弃 U3DS 实例验证插件若被发现只打印 inert 日志，不创建 Canvas/Harmony 客户端 patch；若程序集依赖阶段已失败，启动独立拆包计划**
- [ ] **Step 11: 最终报告按下列证据层分别写结论**

```text
Source/API: PASS|FAIL
Release build: PASS|FAIL
Smoke: PASS|FAIL (N/N)
Deployment hash: PASS|FAIL
No-BE singleplayer: PASS|FAIL|NOT RUN
No-BE multiplayer menu: PASS|FAIL|NOT RUN
Workshop translation across multiplayer: PASS|FAIL|NOT RUN
BattlEye launcher: PASS|FAIL|NOT RUN
BepInEx under BattlEye: LOADED|BLOCKED|NOT RUN
Plugin under BattlEye: LOADED|BLOCKED|NOT RUN
Dedicated no-op: PASS|FAIL|NOT RUN
Gameplay acceptance: SUPPORTED SINGLEPLAYER ONLY
```

## 8. 停止条件与回滚

### 停止条件

- `lib/Assembly-CSharp.dll` SHA-256 漂移或 `Dedicator.isStandaloneDedicatedServer` 不再是 public static：先重做 ABI 审计。
- BattlEye 阻止 BepInEx、插件 DLL 或连接：记录为外部兼容边界，停止 BE 线，不进入绕过实现。
- Dedicated Server 在类型加载阶段缺少客户端 UI 依赖：停止 no-op 单 DLL路线，另做 client/server 拆包。
- Snapshot 非单人身份被接受、多人触碰 Snapshot v1 文件，或单人路径发生变化：立即判定失败并回滚该任务。
- 未确认多人风险时出现 native pickup、移动 patch、Point Tool、health bar、minimap scan 或持续 cheat maintenance：判定功能门失败。
- 单人转多人、多人重连、菜单关闭或自动资产刷新使 Workshop 翻译进入 Cancelled、取消 token、丢失进度或停止 Pump：判定翻译生命周期边界失败。
- 任何配置因为暂时无授权从 requested true 被写回 false：判定状态模型失败。

### 回滚合同

1. 退出 Unturned 和 U3DS。
2. 恢复部署前备份 DLL并核对 hash。
3. 源码按任务提交逐个 `git revert <commit>`；不得 reset 整个 dirty 工作树。
4. 删除新增的 `Launch-Unturned.ps1` / `Launch-Unturned.cmd` 只通过 revert 完成，旧 Singleplayer launcher 在整个迁移期始终保留。
5. 风险确认没有持久配置，无需配置迁移。
6. GUID、配置、Favorites、Teleports、AutoPickup rules、Snapshot v1 均保持原路径，因此回滚不能移动或重写这些数据。

## 9. 自审结果

- 需求覆盖：BattlEye 可选启动、多人客户端菜单、单人功能支持范围、服务器自担风险、Dedicated Server 定义均有对应代码、文案和验收任务。
- 可达入口覆盖：不仅迁移 50 处显式 `SingleplayerGuard`，还覆盖了以前依靠“菜单打不开”间接保护的 `CharacterPagePresenter` 与 `CheatMenuUiFeaturePorts` 直接调用。
- 持久化覆盖：Snapshot v1 明确保留单人硬门，requested/effective 状态分离，重连确认不落盘。
- 翻译覆盖：Workshop 翻译明确脱离 gameplay gate；RuntimeContext 删除会话触发的取消，资产刷新采用当前批次完成后的合并 rescan。
- 命名覆盖：稳定 GUID/程序集/路径不改；产品名仍准确表达唯一受支持功能目标。
- 证据覆盖：官方 Unturned/BattlEye/BepInEx 资料、本地 SDK、当前程序集 hash、构建、Smoke、部署、BE 与真实运行分层记录。
- 历史覆盖：日期化方案与旧版本 CHANGELOG 保持历史真实性，只有 canonical docs 和 `[Unreleased]` 更新。

## 10. 实施完成定义

只有同时满足以下条件，才能把边界调整称为完成：

1. 菜单门与玩法门是两个独立、可测试的决策。
2. 多人客户端本地玩家生成后能打开菜单；未确认前没有玩法后台动作。
3. Workshop 翻译在多人未确认、已确认、断线重连和自动资产刷新期间持续 Pump；当前批次不被自动取消。
4. 翻译页与翻译命令不触发 gameplay risk modal，也不调用 `GameplayFeatureAccessPolicy`。
5. 确认仅对当前 connection key 生效，重连后失效。
6. 单人功能和 Snapshot v1 回归全部通过。
7. 多人中 Snapshot 与 Zombie region retention 始终关闭。
8. 启动器不再因 `Unturned_BE` / `BEService` 拒绝，但也不停止、修改或规避 BattlEye。
9. Dedicated Server 不初始化客户端 UI/功能图；若单 DLL 无法加载，结果被如实记录并转入拆包方案。
10. 中英 README、SECURITY、CONTRIBUTING、安装包文字和 UI 警示完全一致。
11. Release 构建、递归全部 Smoke、diff check 通过。
12. 单人、多人菜单、跨会话翻译连续性、BattlEye 加载链和专服 no-op 证据分别给出，不以其中一项代替另一项。
