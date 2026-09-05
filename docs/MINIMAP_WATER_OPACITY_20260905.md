# Water4 低档俯视透明度候选 v5

## 已取得的实机证据

读取诊断 `I:\Steam\steamapps\common\Unturned\BepInEx\diagnostics\minimap-water-v4-20260904-214717-775c74a8\`：

- terrain 与 objects 来自同一帧 6382。水面缺失外观已存在于 terrain PNG，不是道路叠层导致。
- 当前主水面 Y=25.088，Water 层 4、active/enabled=true、forceOff=false、inCameraMask/inFrustum=true。
- 实际材质 FX/Water4，maximumLOD=201，WATER_SIMPLE。BaseColor 与 ReflectionColor 都是 (0.117647, 0.196078, 0.274510, 0.5)。
- edgeBlend 原本已关闭。当前低档不存在 GrabPass，所以高档折射通道不是该次运行的路径。

从当前安装 resources.assets 的同一水 shader 提取低档 GL 编译程序（不是任意网络示例），保存在 `artifacts/reverse/water-low-48.txt` / `water-low-49.txt`：

```text
fresnel = pow(max(1 - cosTheta, 0), power) * (1 - bias) + bias
alpha = min(2 * fresnel + 0.5, 1)
bias = _DistortParams.w
```

资源材料 bias=0，法线入射时 alpha 趋近 0.5。半透明暗色水与河床混合可解释“干河”外观；这是基于实测路径和已编译程序的候选解释，不是已完成 GPU A/B 验收的最终根因断言。

## 窄范围修正

- 仅精确 `FX/Water4`、maximumLOD 200..299、有限且有效的 Fresnel bias 生效。
- 小地图 terrain 捕获时令 bias 至少 0.25，从上述公式保证最低 alpha=1。
- 不修改材质 RGB、纹理、shader、LOD、反射开关、道路相机或山体分层。较高档与自定义 shader 不处理。
- 保存完整 `_DistortParams` Vector4，按材质去重，绘制后完整恢复。失败 receipt 保留重试，不能被下一次捕获覆盖。
- 仍是原生水网格和水 shader 绘制，不以蓝色 UI 填充替代真实水体。

## 验证与边界

- 最低透明度数学断言先在旧策略上失败，再通过新策略。
- MinimapSceneryScopeSmoke 引用实际生产策略与 scope，通过去重、XYZ 保留、全 Vector 恢复、连续恢复失败及拒绝重捕获测试；这些状态替身不模拟 GPU。
- MinimapSmoke 与 UiArchitectureSmoke 通过；独立 Release 编译通过。
- 不宣称水已实机恢复。须确认日志 `water-opacity-v5` 中 adjusted>0，first material 的实际 bias 变化，并检查新 terrain PNG。
- 取证保留，输出到 BepInEx/diagnostics/minimap-water-v5-*。JSON 的 `postRenderDistortParams` 是回调后的观察值，可能已经恢复；以捕获 scope 日志确认绘制期间的偏置。

候选：`D:\Unturned BepInEx\artifacts\minimap-water-opacity-v5\UnturnedSingleplayerCheatMenu.dll`。本轮未部署、不改配置和存档。
