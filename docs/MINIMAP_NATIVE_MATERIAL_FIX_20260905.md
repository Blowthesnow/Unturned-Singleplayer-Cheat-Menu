# 小地图实时地表黑色：原生材质通道修复候选

## 问题与证据

- 用户截图：桥面和建筑可见，地形大片黑色；本轮沿用上一任务的地表渲染问题，不改图标布局。
- 修复前游戏日志 `artifacts/minimap-native-materials-20260905/before-game.log` 明确包含 `Unlit/Texture unavailable; scene cameras use native materials.`。因此通用 shader 并未参与该次绘制，不能把删除它当作本次黑地根因修复。
- 当前 `lib/Assembly-CSharp.dll` 与安装目录同名 DLL SHA-256 一致：`A110F5FE471422254C594E29F90FC4058D04D357EB3AE9CB4D3EDA0FFEC33F10`。
- 精确程序集反编译证据：`artifacts/reverse/LandscapeTile.minimap-20260905.cs` 的 `applyGraphicsSettings()` 按全局渲染模式选择独立的 `Landscape_Forward` / `Landscape_Deferred` 材质；关闭混合时用 `Landscape_Classic`。
- 小地图相机是正交 Forward 相机，却没有适配仍绑定在 Terrain 上的 Deferred 材质。原生卫星截图会切换并应用全局画质；插件此前只复制了相机和环境参数，缺失材质切换。
- [Unity 2022.3 Camera.orthographic](https://docs.unity3d.com/kr/2022.3/ScriptReference/Camera-orthographic.html)：正交相机不支持 Deferred，实际使用 Forward。因此仅把相机设置成 Deferred 不是解决方法。

这确认了一个代码层面的材质通道不匹配。当前截图是否由此产生，仍须候选版本的运行日志和同位置截图确认，不宣称已经实机修好。

## 本轮实现

1. 移除 `Unlit/Texture` 通用替换；复制相机参数后显式 `ResetReplacementShader()`。
2. 在既有 terrain capture 作用域内记录每块 Terrain 的原始材质引用。
3. 只有原始材质精确等于游戏原生 Deferred 资源，且原生 Forward shader 可用时，才临时切换 `materialTemplate`。Classic、自定义材质不替换；不改共享材质内部属性，不新建材质。
4. 在 `OnPostRender`、`OnDisable`、`OnDestroy` 和下一帧取消激活路径恢复原引用，材质恢复与 LOD 恢复分开处理。
5. 保留朝向、地形相机深度、建筑高度切片、地图边缘裁剪、原生地图垫底、水体反射事件和已有 LOD/环境状态恢复；不修改全局渲染模式，不调用全局 `GraphicsSettings.apply()`。
6. 新增单次 `native-materials-v1` 和 `terrain-material-scope-v1` 日志，包含相机深度、图层、游戏模式、材质资源是否存在和实际切换数量。失败单次告警，避免逐帧刷屏。

## 验证与复测

- Release 编译通过：0 警告、0 错误；递归枚举并重新构建/运行 Smoke，27/27 通过，日志位于 `artifacts/minimap-native-materials-20260905/`。
- 独立只读审查未发现本轮可证实的新回归；本轮仅修改 renderer、两个 Smoke 文件和本文档。
- 候选 DLL SHA-256：`D132EEAB32CBDA5D7F1B9C99C3B1C3FEEADF071AC8A47C7C2CCADB9F772A4DD7`。未替换游戏 DLL。
- 新的原生材质约束与材质作用域约束均先在旧实现上触发失败，再在候选实现上通过。
- `UiArchitectureSmoke` 中材质作用域检查是源代码契约检查，不是 Unity GPU 或回调运行测试。
- 实机复测应在原截图桥面位置、相同缩放、相同背景模式下进行，并验证主视角地面没有闪烁/变色。
- 查找 `terrain-material-scope-v1`：若 `gameMode=DEFERRED` 且 `swapped>0`，证明修正路径被执行；`swapped=0` 不能视为修复确认，应进一步核对材质和资源。
- 必须检查退出世界、重新进图和禁用小地图后主相机恢复；日志成功和 DLL 哈希一致均不能替代画面验收。
- 本轮不修改存档、配置或其他未提交功能，不自动部署或发布。

## 本轮前备份

`backups/minimap-native-materials-20260905-043738/` 保存修改前的 renderer 和 UI 架构测试。工作区既有弹药修复及上一轮小地图改动均保留。
