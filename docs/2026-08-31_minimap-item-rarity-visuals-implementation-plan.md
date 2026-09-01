# 小地图物品稀有度黑色描边落地说明

> 状态：最终方案已按用户实机反馈收敛。
>
> 最终裁决：恢复游戏原生稀有度颜色，只保留黑色描边，并把星形从 32px 纹理改为 uGUI 程序化矢量网格。

## 最终效果

- 物品主体继续使用游戏原生 `ItemTool.getRarityColorUI(...)` 颜色。
- 主体继续使用 16×16 星形，但由 `MinimapStarGraphic` 直接生成三角形网格。
- 主体下方增加 20×20 黑色矢量星形，形成约 2 px 描边。
- 描边透明度为 `0.90 × marker.Color.a`。
- 其他楼层原有 Alpha 会同时作用于主体和黑色描边。
- 非物品标记不显示该描边。
- 改变尺寸时重新按 RectTransform 生成几何，不再缩放或双线性采样低分辨率星形纹理。

## 明确移除

- 自定义高对比色板；
- 神话五色幻彩、逐帧图集和动画；
- 白色内轮廓、光晕和呼吸缩放；
- “游戏原色 / 高对比 / 高对比动态”设置项；
- 新增的稀有度视觉配置、迁移和专用策略类；
- 12 px 新安装合并距离。

`ItemMergePixels` 恢复原默认值 8 px，`SchemaVersion` 保持 3。

## 运行链路

```text
Asset.rarity
  → MinimapItemRarity 筛选与聚合
  → ItemTool.getRarityColorUI
  → MinimapHudMarker.Color
  → 20×20 黑色 MinimapStarGraphic
  → 16×16 原色 MinimapStarGraphic
```

扫描、筛选、聚合、AutoPickup 显式联动、楼层判断和 20 Hz 投影频率全部保持不变。

## UI 层级

```text
MarkerSlot
├─ ItemOutline       20×20，程序化黑色星形
├─ ItemFill          16×16，程序化原色星形
└─ Image             非物品标记原路径
```

`ItemOutline` 和 `ItemFill` 与现有标记一样由 `MinimapMarkerPool` 创建和复用：

- 只在池扩容时创建；
- `raycastTarget = false`；
- 继承 `MaskableGraphic`，继续受圆形/方形小地图裁剪；
- 使用 `GetPixelAdjustedRect()` 和八点三角扇直接生成几何；
- 八个周界点按逆时针提交，保证 CanvasRenderer 将三角形视为正面；
- 不使用 uGUI `Outline` 组件；
- 不创建星形 Texture、Sprite、Material 或 Shader；
- 不增加每帧动画或纹理更新。

## 代码边界

| 文件 | 最终改动 |
|---|---|
| `src/UnturnedSingleplayerCheatMenu/UI/MinimapStarGraphic.cs` | 新增无纹理、可裁剪的八点星形网格 |
| `src/UnturnedSingleplayerCheatMenu/UI/MinimapMarkerPool.cs` | 池化矢量外星与内星，非物品继续走 Image |
| `src/UnturnedSingleplayerCheatMenu/UI/MinimapHud.cs` | 移除星形纹理和 Sprite 的初始化、所有权与传参 |
| `tests/UiArchitectureSmoke/Program.cs` | 验证矢量网格、尺寸、颜色、层级、池化及原生颜色链 |

其余动态稀有度实现文件和配置改动全部回退。

## 自动化验收

- Release 构建 0 警告、0 错误；
- 全部 Smoke 通过；
- `UiArchitectureSmoke` 确认：
  - 描边在主体之前创建；
  - 描边尺寸为 20×20，主体仍为 16×16；
  - 描边为黑色且 Alpha 跟随楼层 Alpha；
  - 非物品标记关闭描边；
  - 星形使用 `VertexHelper` 网格且不引用 Texture/Sprite；
  - 生产代码仍调用 `ItemTool.getRarityColorUI(...)`；
  - 不再存在幻彩图集或动画入口。

## 游戏内验收

用户需要在实际游戏中确认：

1. 普通至神话均恢复游戏原生颜色；
2. 黑色描边在草地、雪地、水面和室内背景上可见；
3. 没有白边、光晕、呼吸或颜色动画；
4. 其他楼层标记仍按原逻辑淡化；
5. 圆形与方形小地图表现一致。

构建和 Smoke 不能替代上述实机视觉验收。
