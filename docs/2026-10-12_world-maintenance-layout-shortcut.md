# 世界维护布局与跨箱快捷键

日期：2026-10-12。状态：源码、构建、聚焦回归及部署完成；游戏内视觉与鼠标交互待验收。

## 修改

- 世界维护改为两列独立纵向排列：左列为自然资源刷新、物资点补货；右列为丧尸维护、地面掉落物清理。每列下一张卡片紧接上一张卡片，间距 12，不再等待另一列较高卡片的高度。
- 继续使用现有 uGUI 布局和内容高度计算。两列最少需要 732 宽度，最小窄窗口可用 822，宽布局切换点可用 782；未添加填充内容。
- 同物品跨箱集中只接管原生左键 `onGrabbedItem`，移除右键 `onSelectedItem` 补丁。左、右 Ctrl 均支持，Shift 或 Alt 同时按下时不触发集中。
- 页面说明、英文翻译、启动日志及中英文 README 同步为 Ctrl＋鼠标左键。
- 保留本聊天上一轮的补货修复，记录见 `2026-10-12_restock-fix.md`。

## 验证

- 使用当前安装的游戏程序集构建：0 警告、0 错误。项目与日志位于 `artifacts/layout-shortcut-20261012/installed-runtime-build`。
- `InventoryQuickTransferNativeSmoke`：411 项检查通过，链接真实生产 Patch；原生、Harmony 和输入边界使用测试替身。覆盖 64 种修饰组合与三种鼠标键、注册入口、储存页、开箱及输入门控，已加入 solution。
- `InventoryArrangePlannerSmoke`、`UiLayoutPolishSmoke`、`UiArchitectureSmoke`、`LocalizationSmoke`：通过。
- 两列布局经独立源码复核；`git diff --check` 通过。

## 部署

- 部署时间：2026-10-12 19:16:47 +08:00，部署时游戏已退出。
- 目标：`I:/Steam/steamapps/common/Unturned/BepInEx/plugins/UnturnedSingleplayerCheatMenu/UnturnedSingleplayerCheatMenu.dll`。
- 备份：`backups/layout-shortcut-predeploy-20261012-191647`。
- 候选、游戏目标及工作区 `artifacts` DLL 的 SHA-256 均为 `6CE38F6B16758C956443CA53402264F35D8D2EC74BA28A008CB77F71F61459B5`；活动同名 DLL 为 1 份。
- 完整部署证据：`artifacts/layout-shortcut-20261012/deployment.json`。

完整重启游戏后检查世界维护卡片排列，并在打开储物箱时验证 Ctrl＋左键集中。构建、测试替身及部署哈希一致不代表游戏内视觉和交互已经通过。
