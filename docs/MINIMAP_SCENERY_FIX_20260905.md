# 山水缺失修复候选 v3

## 冻结约束

- 道路相机位置、near plane、far plane 策略均不变；`MinimapRenderPolicy.cs` 与本轮前备份逐文本比较一致。
- 保留整个小地图的道路高度切片，不使用局部圆形遮罩，不改布局/缩放/旋转/自动切换设置。
- 保留前版已通过游戏日志验证的原生 Terrain Deferred→Forward 材质修正。

## 当前地图证据

`中国南方/Level/Objects.dat` 的 v12 格式按本机 River/LevelObjects 反编译核对：旋转以 3 个 Euler float 保存，GUID 使用长度前缀。49,909 个对象解析至文件末尾，38,612 个匹配原生对象 GUID；截图位置 `(198.9, 36.2, -802)` 周围 350 米内有 8 个 `Boulder_03`，资产标注 `Chart Cliff`。例如 `(188.6, 52.7, -768.5)` 和 `(172.6, 49.0, -779.6)`。

这些山体是场景模型，不是 Terrain；原实现只将它们交给道路高度切片相机，导致上部被截断。证据：`artifacts/minimap-scenery-v3-20260905/map-cliff-evidence.json`。

安装资源 `resources.assets` 中的实际水 shader 是 `FX/Water4`，含 `WATER_EDGEBLEND_ON/OFF` 编译变体、`_InvFadeParemeter` 和透明水面 pass。原生 WaterBase 负责全局边缘混合关键字。导出元数据保存在 `artifacts/reverse/Water4.installed.shader`，该文件**不是可编译 shader**。水面正交深度淡化是本轮诊断候选，尚未由 GPU 捕获证明为唯一原因。

## 变更

1. 仅将明确 `EObjectChart.CLIFF` 的实体与 skybox renderer 纳入山体集合。不泛化 WATER/GROUND 或按名称猜类型。
2. 高层地表相机绘制前，将已启用山体 renderer 所在对象临时置于原生 GROUND2 层；绘制完成恢复。道路相机绘制期间暂时隐藏这些山体，避免被截断的残片覆盖完整山形。
3. 仅小地图地表捕获期间使用水面的原生无边缘深度淡化变体，准确保存和恢复 ON/OFF 两个原始关键字位。不改颜色、不强开反射、不改地图水体配置。
4. 不激活原本隐藏的模型。恢复路径涵盖 PostRender、Disable、Destroy、下一帧；失败 receipt 保留重试。Renderer 被销毁但 GameObject 存活时仍恢复图层。
5. 水关键字恢复失败时拒绝再次捕获，不能覆盖原始快照。

## 验证边界

- Release 编译通过：0 警告、0 错误。
- 新增 MinimapSceneryScopeSmoke 编译并执行实际生产作用域代码，使用状态替身覆盖重复 renderer、原隐藏状态、四种清理入口、设置失败、恢复失败重试、Renderer 组件销毁，以及水关键字四种初始组合与连续恢复失败。
- 状态替身不模拟 Unity GPU、材质、静态批次剔除或透明排序。
- 全量回归首次曾达到 30/30；最终代码复跑时，并行任务改写共享输出，导致旧 UiControlLibrarySmoke 复制件哈希不一致，重建该测试后通过。随后并行 UI 任务扩展 UiLayoutPolishSmoke，新增翻译折叠区和天气布局 9 项断言尚未通过。本轮不修改其实现，不能宣称最终全量全绿。
- 静态批次中的临时层变更、山体和道路视觉关系、水面恢复、主视角无闪烁，均需同位置实机确认。
- 既有两张独立纹理的透明/深度合成限制没有被声称消除；如果水仍缺失，需要检查 Terrain/Objects 两张捕获纹理区分“未绘制”与“后层覆盖”。
- 原生同步反射子渲染会继承当前捕获范围的临时状态，不能宣称反射子相机已完全隔离。

## 候选与复测

独立候选：`artifacts/minimap-scenery-v3-20260905/UnturnedSingleplayerCheatMenu.dll`。

候选 SHA-256：`4C9FFA003FCFD94B61EDB1329A8CBFAC9B4CE7BBDE188B74CDE6D287C828ABD9`。

本轮未部署。运行中检测到游戏 DLL 被并行工作更新，不能盲目覆盖。部署前应重新确认游戏已退出，并核对并行版本及备份。

日志检查点：

- `scenery-scope-v3: pass=terrain/roads, selected=..., staticBatch=...`
- `water-edge-scope-v3: originalOn=..., originalOff=..., capture=off`

在原桥面位置检查完整道路、两侧岩山、水面，以及关闭小地图和退出世界后的主视角恢复。不以 DLL 哈希、日志或测试通过替代画面验收。
