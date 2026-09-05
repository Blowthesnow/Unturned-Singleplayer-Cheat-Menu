# 缺水诊断 v4（不是水面修复版）

> 正式 2.0 发布调整：诊断 GPU 回读和 PNG/JSON 保存默认关闭。仅在启动游戏前显式设置进程环境变量 `UNTURNED_MINIMAP_CAPTURE=1` 时启用下述采集流程；普通启动不采集。

## 新实机证据

- 用户确认 v3 山体恢复，道路高度约束保留。
- 当前日志 `water-edge-scope-v3: originalOn=False, originalOff=True`：水边缘深度淡化原本已经关闭，不能把它称为缺水根因。
- 用户临时关闭已加载建筑后，道路消失但水仍缺失，排除上层道路/物体纹理遮盖水面的方向。
- 安装资源中的 Water_Tile、Water_Plane、Water_Fallback 均在 Water 层 4。实际 FX/Water4 含 LOD 500 命名 GrabPass 折射通道；它仍只是候选调查方向，未据此修改 shader LOD 或水颜色。

## 本轮仅增加直接取证

`MinimapRenderCaptureProbe` 挂在两台现有小地图相机上。每种实时模式第 3 次真实 OnPostRender 保存一次 PNG 与 JSON，此后不再采集该模式。MSAA 纹理先 resolve 到临时纹理，finally 恢复 RenderTexture.active 并释放临时资源。

输出：`I:\Steam\steamapps\common\Unturned\BepInEx\diagnostics\minimap-water-v4-<UTC时间>-<随机标识>\`

- `RealtimeHeightSlice.terrain.png/.json`
- `RealtimeHeightSlice.objects.png/.json`
- 若切到实时地表模式，也会分别保存一次。

JSON 包含帧号、相机位置/near/far/掩码、附近水面的高度和 bounds、激活状态、材质名/shader/LOD/关键字/颜色；即便水面不在视锥，也保留附近 XZ 候选并标记 `inFrustum=false`。水面样本最多 64 条，截断时明确标记。

读取范围是小地图自身纹理与附近游戏水体，不包含桌面、聊天或账户数据。未修改道路相机、山体分类、shader LOD、反射开关、水面颜色、UI 布局、配置或存档。

## 验证

- 源码断言先失败后通过：双通道实际 OnPostRender 取证、延迟三次渲染、限次写盘、保留被裁剪的水面、恢复 readback 状态。
- MinimapSmoke、MinimapSceneryScopeSmoke、UiArchitectureSmoke 通过。
- 独立 Release 编译通过，0 警告、0 错误。GPU 回读与水面参数尚未实机验证。

候选：`D:\Unturned BepInEx\artifacts\minimap-water-diagnostics-v4\UnturnedSingleplayerCheatMenu.dll`。本轮未部署。

复测时恢复已加载建筑开关，在缺水位置保持实时/高度切片至少两秒。日志出现 `water-layer-evidence-v4 saved` 后读取生成的两层 PNG 与 JSON，再决定具体修复；不能再次凭 HUD 合成截图猜测。
