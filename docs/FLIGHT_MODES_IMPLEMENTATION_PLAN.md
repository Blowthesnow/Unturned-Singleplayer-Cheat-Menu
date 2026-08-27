# 飞行模式二：视角绑定飞行落地文档

日期：2026-08-27  
范围：`UnturnedSingleplayerCheatMenu`（BepInEx 5 / Unity Mono / 单人世界）  
状态：设计与可行性评估，未修改生产代码，未宣称游戏内验收完成

## 1. 结论

可以实现第二种飞行模式。推荐把它实现为 `ViewDirectionFlight`（视角绑定飞行，通俗名“指向性飞行”），与现有 `WorldAxisFlight`（世界轴/创世神式飞行）并列，而不是继续把两种语义塞进同一个 `FlightEnabled` 开关。

推荐的第一版行为：

- 鼠标决定飞行朝向：使用 `Player.look.aim.forward`，包含俯仰角（pitch）。
- `W/S` 沿视角前后，`A/D` 沿视角右/左。
- `Space/Ctrl` 仍是绝对 Y 轴上升/下降。
- 垂直键优先：当 `Space` 或 `Ctrl` 任一被按住时，Y 位移由键盘决定，并将鼠标俯仰对 Y 位移的影响屏蔽；释放垂直键后恢复视角控制。
- 默认仅在单人世界启用，沿用现有 `SingleplayerGuard`；不承诺多人服务器可用或绕过反作弊。

该方案可复用当前 Harmony 挂接的 `PlayerMovement.simulate` 周期，不需要逐帧 `transform.position` 瞬移。

## 2. 术语与产品命名

| 产品项 | 代码建议名 | 设计术语 | 方向来源 | 垂直控制 | 碰撞 |
| --- | --- | --- | --- | --- | --- |
| 现有飞行 | `WorldAxisFlight` | World-space / absolute movement | 世界轴（由原生移动模拟处理） | `Space/Ctrl` 绝对 Y | 保留 `CharacterController` |
| 新增飞行 | `ViewDirectionFlight` | View-dependent / local-space movement | `aim.forward`（含 pitch） | 键盘 Y 覆盖 | 保留 `CharacterController` |
| 现有穿墙 | `Noclip` | Collision-free view-relative movement | 当前实现为 `aim.forward` 去 Y 后的水平向量 | `Space/Ctrl` | 关闭控制器 |

注意：当前 `Noclip` 的水平输入已经是视角相关，但它会去掉 `aim.forward.y`，所以不是完整的“鼠标指向三维飞行”。当前 `Flight` 则只在原生移动之后补 Y 位移，不能直接视为第二模式。

## 3. 当前实现核对（源码证据）

### 3.1 插件入口与配置

- [CheatMenuPlugin.cs](../src/UnturnedSingleplayerCheatMenu/CheatMenuPlugin.cs) 在 `Movement` 节绑定 `FlightSpeed`（默认 `3x`）、`FlightVerticalSpeed`（默认 `2x`）和 `NoClipSafeExit`。
- `HandleFlightShortcut` 只切换 `Movement.SetFlight(!Movement.FlightEnabled)`，当前没有模式枚举或模式持久化。
- 移动面板只提供“飞行”“穿墙”、水平速度、垂直速度和安全退出开关，没有第二模式选择器。

### 3.2 `MovementCheatService` 的实际分支

源码：[MovementCheatService.cs](../src/UnturnedSingleplayerCheatMenu/Services/MovementCheatService.cs)

- `SetFlight` 保存并清零重力，设置原生速度倍率；关闭时恢复原始重力/速度/跳跃倍率。
- `SimulateMovement` 在 `NoclipEnabled` 分支读取 `GetHorizontalInput` 与 `GetVerticalInput`，直接移动 `transform.position`。
- `GetHorizontalInput` 使用 `player.look.aim.forward`，先把 `y` 设为 `0`，再计算平面前后左右，因此只实现“水平视角绑定”。
- 非 Noclip 的 `Flight` 分支只调用 `CharacterController.Move(Vector3.up * verticalInput * VerticalMetersPerSecond * deltaTime)`；水平移动仍由原生 `PlayerMovement.simulate` 完成。
- `IsMovementInputBlocked` 在菜单/光标显示时阻止移动，第二模式必须沿用此门禁。

### 3.3 Harmony 与原生模拟边界

源码：[MovementSimulationPatch.cs](../src/UnturnedSingleplayerCheatMenu/Patches/MovementSimulationPatch.cs)

- 通过反射确认 `PlayerMovement.simulate(uint,int,int,int,float,float,bool,bool,float)` 后安装 Prefix/Postfix。
- Prefix 调用 `PrepareNativeSimulation`，Postfix 调用 `SimulateMovement`。
- 另外挂接 `PlayerInput.ClientResimulate`，用 `_isResimulating` 避免客户端重模拟时重复注入移动。

官方 SDK 对照：

- `U3-SDK-main/.../PlayerMovement.cs:1051-1494` 的原生步行模拟将水平速度基于 `transform.rotation * move` 计算，默认仅使用玩家身体 yaw。
- `U3-SDK-main/.../PlayerLook.cs:593-625` 将 yaw 写入玩家本体旋转，将 pitch 保存在 `look.aim`，因此“鼠标俯仰直接改变水平飞行方向”需要插件显式使用 `aim.forward`。
- `U3-SDK-main/.../PlayerInput.cs:1268-1346` 的重模拟会重放输入历史，第二模式必须继续受 `_isResimulating` 保护。
- `U3-SDK-main/.../InputEx.cs:17-20` 表明原生输入还会检查游戏菜单输入门禁；插件读取键盘时应保持与 `IsMovementInputBlocked` 一致，避免菜单打开后继续移动。

## 4. 推荐控制算法

### 4.1 输入采样

```text
forward = normalize(aim.forward)
right   = normalize(cross(Vector3.up, forward))
horizontal = clampMagnitude(
    forward * (W - S) + right * (D - A), 1)
verticalKey = (Space ? 1 : 0) - (Ctrl ? 1 : 0)
```

为避免 `forward` 接近垂直时右向量退化，应增加保护：当 `forward` 与 `Vector3.up` 的叉积长度低于阈值时，使用玩家 yaw 的平面前向量计算 `right`。

### 4.2 键盘垂直优先级

```text
if verticalKey != 0:
    velocity = horizontal * HorizontalMetersPerSecond
             + Vector3.up * verticalKey * VerticalMetersPerSecond
else:
    velocity = forward * (W - S) * HorizontalMetersPerSecond
             + right * (D - A) * HorizontalMetersPerSecond
```

实现上建议把“水平视角分量”和“垂直键盘分量”分开计算，而不是简单把 `aim.forward` 与 `Space/Ctrl` 相加。这样能保证：

1. 按住 `Space` 时不会因鼠标向下而抵消上升；
2. 按住 `Ctrl` 时不会因鼠标向上而抵消下降；
3. 无垂直键时，W/S 才能通过 pitch 产生俯仰飞行。

### 4.3 碰撞与移动调用

第一版应保留 `CharacterController` 碰撞：

- `ViewDirectionFlight`：控制器保持启用，使用 `controller.Move(velocity * deltaTime)`；
- `Noclip`：保持现有控制器禁用和安全退出逻辑；
- 死亡、进入载具、菜单/光标捕获、非单人状态：不注入移动并调用现有恢复流程。

不要在第一版把新模式做成无碰撞瞬移；否则会同时引入穿墙、落点、网络校正和安全退出问题，验收面会扩大。

## 5. 建议的代码改造面（实现阶段）

### 阶段 A：状态模型与配置

新增一个小型枚举（建议放在 `Services/FlightMode.cs`）：

```csharp
internal enum FlightMode
{
    WorldAxis,
    ViewDirection
}
```

在 `MovementCheatService` 增加：

- `FlightMode Mode { get; set; }`；
- `SetFlightMode(FlightMode mode)`，切换时不改变 `FlightEnabled`；
- `GetWorldAxisVelocity(...)` 与 `GetViewDirectionVelocity(...)` 两个纯计算方法；
- `GetViewDirectionVelocity` 内部实现叉积退化保护、输入归一化和垂直键覆盖。

在 `CheatMenuPlugin` 增加稳定配置键，例如 `Movement/FlightMode = WorldAxis`，读取时对未知值回退 `WorldAxis` 并记录一次 warning。保留现有速度键，不改变已有配置含义。

### 阶段 B：模拟路径

建议调整 `SimulateMovement` 的结构：

1. 先执行公共门禁（本地玩家、单人、非死亡、非载具、未重模拟、菜单未捕获）；
2. `NoclipEnabled` 继续走现有无碰撞分支；
3. `FlightEnabled && Mode == WorldAxis`：维持当前原生水平 + 控制器垂直补偿；
4. `FlightEnabled && Mode == ViewDirection`：计算完整三维速度并通过控制器 `Move` 注入；
5. 关闭飞行或状态失效时，沿用 `Restore`、倍率恢复和安全退出。

不要在 `PlayerMovement.simulate` Prefix 中直接覆盖原生 `input_x/input_y`，除非后续实验确认需要；Prefix 修改输入会影响原生步行、重模拟和输入历史，风险高于在 Postfix 中追加模式位移。

### 阶段 C：UI 与快捷键

在 `BuildMovementPanel` 增加“飞行模式”下拉/分段选择：

- `世界轴（创世神）` -> `WorldAxis`
- `视角指向（鼠标俯仰）` -> `ViewDirection`

模式变化应立即写回 `Movement/FlightMode`，并在状态栏显示当前模式。快捷键仍只负责开关飞行，不新增第二个互斥开关。

## 6. 兼容性与风险边界

### 单人/多人

当前插件已经用 `SingleplayerGuard.IsReady` 限制移动作弊。第二模式应继续保持这一限制。Unturned 的 `PlayerMovement.simulate` 和 `PlayerInput.ClientResimulate` 属于客户端预测/服务器校正链路；客户端自定义三维速度可能导致位置校正、橡皮筋或被服务器拒绝。本文不把多人可用性作为目标，也不建议在 BattlEye 会话中测试。

### 旋转来源

`transform.forward` 代表玩家身体 yaw；`player.look.aim.forward` 才包含相机/准星俯仰。第二模式必须明确使用后者，并记录当前游戏版本下的实际方向。

### 速度与斜向输入

W+D、W+A 等组合必须归一化，避免斜向速度达到单轴的 `sqrt(2)` 倍。水平速度和垂直速度继续复用现有 1-10x 配置并在入口处 Clamp。

### 菜单与原生输入

菜单打开或 `PlayerUI.window.showCursor` 为真时不得移动；模式切换控件也不能让 `EventSystem`/原生输入状态残留。

## 7. 验证矩阵

### 7.1 源码与 Smoke

- 新增 `FlightModeSmoke`：枚举解析、未知值回退、速度 Clamp、输入归一化、垂直键优先级、pitch 方向符号。
- 保留并运行 `MovementSpeedSmoke`，确认现有速度换算不变。
- 检查 Harmony 目标签名仍匹配；挂接失败时必须保持飞行关闭并记录 warning。
- 构建命令：`dotnet build .\UnturnedSingleplayerCheatMenu.slnx -c Release --no-restore`。

### 7.2 启动与日志

需要看到：

- 插件启动与版本；
- `PlayerMovement.simulate` / `ClientResimulate` 挂接成功；
- 切换模式时打印 `WorldAxis` 或 `ViewDirection`；
- 目标签名缺失时只出现一次可诊断 warning，且功能保持关闭。

### 7.3 真实单人世界（必须单独标记）

使用 Steam 的无 BattlEye 启动项进入单人世界，角色加载完成后验证：

| 场景 | 预期 |
| --- | --- |
| ViewDirection + 鼠标向上 + W | 沿抬头方向上升前进 |
| ViewDirection + 鼠标向下 + W | 沿低头方向下降前进 |
| ViewDirection + Space + 鼠标向下 | 仍稳定上升，键盘优先 |
| ViewDirection + Ctrl + 鼠标向上 | 仍稳定下降，键盘优先 |
| ViewDirection + A/D | 随视角 yaw 改变左右方向 |
| 松开所有键 | 速度归零，不漂移 |
| 切回 WorldAxis | W/A/S/D 与视角 pitch 解耦，行为与现有模式一致 |
| 打开菜单/显示光标 | 角色不移动，关闭后恢复 |
| 进入载具或死亡 | 飞行状态恢复，倍率不残留 |
| 关闭飞行 | 重力、速度、跳跃倍率恢复 |

截图、构建成功或 DLL 哈希一致都不能代替上述手感和状态验收。

## 8. 当前证据与未决项

已确认：

- 本地插件项目为 `netstandard2.1`，引用 BepInEx、Harmony、`Assembly-CSharp` 和 Unity Mono 模块；
- 当前工作区存在 `lib/Assembly-CSharp.dll`（4,686,048 bytes，SHA-256 `E1146353E5C9BFF901EE94829640D88919C5E89F6A6B90B22C73ABF5C1608F94`；程序集版本元数据为 `0.0.0.0`）。该哈希是本次只读检查结果，不能替代对目标游戏安装目录的再次核对；
- 当前生产代码已有原生模拟补丁和重模拟保护；
- 当前 `Noclip` 已有“水平视角绑定 + 键盘升降”的部分能力；
- `U3-SDK-main` 提供了 `PlayerMovement.simulate`、`PlayerLook.simulate`、`PlayerInput.ClientResimulate` 的源码对照。

未确认、实现前必须补齐：

- 目标用户实际安装的 `lib/Assembly-CSharp.dll` 是否与 `U3-SDK-main` 版本一致；
- 当前版本 `player.look.aim.forward` 在真实游戏相机状态下的俯仰方向和坐标约定；
- 控制器 `Move` 注入完整三维速度后，原生 `checkGround`、斜坡和边界逻辑是否产生可接受结果；
- 客户端预测校正是否会在长距离/高速飞行时出现明显回弹。

在以上项目完成前，结论应保持为“技术上可实现、推荐单人实现”，而不是“第二飞行模式已经完成”。

## 9. 推荐实施顺序

1. 先对当前 `lib/Assembly-CSharp.dll` 做签名审计，确认 `simulate`、`look.aim` 和控制器 API。
2. 先实现并测试纯函数速度计算与 `FlightModeSmoke`。
3. 接入 `MovementCheatService` 的 ViewDirection 分支，保持 WorldAxis/Noclip 原逻辑不变。
4. 加入配置和 UI 选择器，验证菜单输入捕获与持久化。
5. Release 构建、Smoke、部署备份和哈希比对。
6. 运行真实单人交互矩阵，记录日志和手感结果。
7. 只有在真实验收通过后，才更新 README/ACCEPTANCE 的功能声明。
