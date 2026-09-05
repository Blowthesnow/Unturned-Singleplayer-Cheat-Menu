# 实时小地图光照与树冠候选 v6

> 后续变更：用户要求撤掉光影。已移除 v6 新增的固定方向光和梯度环境光，恢复此前的均匀环境光捕获；树冠/远景树补全、山水修复和道路相机策略保留。`MinimapLightingScope` 及其接线、专用测试已删除。当前主 DLL 仍在 artifacts 根目录。

## 已确认与冻结

- 用户实机确认 v5 山体和水面恢复；输出子目录曾导致误用旧 DLL。
- 本轮唯一主交付位置恢复为 `D:\Unturned BepInEx\artifacts\UnturnedSingleplayerCheatMenu.dll`，证据目录不是替代交付目录。
- 保留山体分层、水面不透明度修正、全图道路切片。道路相机位置/near/far 所在 MinimapRenderPolicy 与本轮前备份比较一致。

## 树冠

- 原 RESOURCE 层由低高度道路相机绘制，超过上沿的树冠会被截断。本轮移到完整高度地表相机，并从道路相机移除该层。
- 使用本机新 API `Regions.GetCoordinateBoundsVector2Int` 与 `LevelGround.GetTreesOrNullInRegion`，先查询附近区域再枚举 model/stump/skybox renderer。查询余量 256 米；不每两秒遍历全地图资源层级。
- 复用已验证的 MinimapSceneryScope 做实体/远景分层。仍尊重 active、enabled、forceRenderingOff，不复活已砍树木、不强制加载不可见资源。原版静态 Map.png 中仍保留的历史树木不一定在当前世界存在。

## 光照

- 原捕获将天空/侧面/地面环境光设成相同 Palette.AMBIENT，会削弱朝向层次。
- 新 MinimapLightingScope 在捕获期间使用分层 Trilight，并暂时将原生太阳光设为固定方向 (50,-35,0)、中性光色、强度 0.9，提供基于真实材质/法线的明暗，不做图像滤镜。
- 完整保存并恢复原光源方向、颜色、强度、enabled 与环境光。两个相机共用同一 scope，避免异常恢复时相互污染；pending receipt 未恢复前禁止重新取快照。
- 不额外改变全局阴影质量或距离，不保证完全复制离线 Map.png 的烘焙阴影。原生同步反射子渲染仍继承父捕获的临时光照。

## 验证

- 源架构约束先红后绿；MinimapSceneryScopeSmoke 使用实际生产 LightingScope 和状态替身，测试分层环境光、方向/颜色/强度/enabled 恢复、跨 pass pending 阻止、连续失败重试、无太阳光回退。
- Release 编译 0 警告、0 错误。只读审查发现并闭环共享 scope 与树扫描范围两项问题。
- 最终递归重建并运行全量 Smoke：30/30 通过。根目录 DLL 的路径和 SHA-256 记录于 `artifacts/minimap-lighting-trees-v6-evidence/result.json`。
- 状态替身不模拟 Unity GPU、真实树木 LOD 或帧耗；当前候选画面尚未实机验收。
- 没有替换游戏安装目录 DLL，没有改配置、存档或其他并行 UI 工作。

复测：用 artifacts 根目录的 DLL 替换游戏插件后，在原桥面位置对照明暗和树冠；观察 `capture-lighting-v6` 日志，并在关闭小地图/离开世界后确认主视角光照恢复。新取证目录前缀为 `minimap-lighting-v6-*`。
