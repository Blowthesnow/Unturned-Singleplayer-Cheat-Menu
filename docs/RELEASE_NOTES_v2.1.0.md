# v2.1.0 — 世界维护、补货修复与操作体验更新

本版在 v2.0.0 基础上新增世界维护与环境预设，修复物资补货主流程，并改进小地图室内显示、穿墙移动和跨箱集中操作。

> 本 Release 只提供 **Unturned-Singleplayer-Cheat-Menu-v2.1.0-Plugin-Only.zip**。压缩包不包含 BepInEx；请另行安装 BepInEx `5.4.23.5` x64。

## 新增：世界维护

- 自然资源刷新：恢复附近或全图可安全恢复的枯竭树木与岩石，并显示枯竭、可恢复和被阻挡数量。
- 丧尸维护：清除附近或已加载区域的丧尸，并在可用槽位中生成丧尸；默认保护 Boss、Mega 和信标事件丧尸。
- 地面掉落物清理：支持附近与全图范围，不触碰背包、衣物容器、储物箱和无限仓库。
- 物资点补货：按原生区域、物资表和密度配额补充物资，保留安全区及出生点占位规则。
- 附近范围提供 `100m / 250m / 500m`；批量操作提供预览、确认、分帧执行、进度、停止和结果统计。

## 世界页面与布局

- 新增当前天气、时间冻结和满月状态显示。
- 新增“晴朗正午”“满月之夜”“暴雪夜晚”“恢复自然”情景预设；恢复自然会解除时间冻结、关闭强制满月并恢复自然天气调度。
- 资产概览分别显示全部/Workshop 物品与载具数量，并保留重新扫描入口。
- 世界维护改为两列独立堆叠：左列自然资源刷新与物资补货，右列丧尸维护与掉落物清理，减少卡片之间的空白。

## 重点修复

### 物资补货与无限储存拾取

- 修复无限储存拾取只删除地面模型、服务端物资数据仍占用配额和出生点的问题。
- 补货与丧尸生成按真实新增数量计数；处理区域但未生成物资时不再报告成功。
- 使用原生物资实例化队列，移除重复的手动实例生成。
- 执行前重新核对区域、出生点、配额和安全空位；状态变化与部分提交异常分别统计。
- 预览显示预计可补充件数；附近补货按范围覆盖的原生区域执行，单次随机生成不承诺填满每个空出生点。
- 修复关闭天气后“恢复自然”没有重新启用自然天气调度的问题。

### 小地图与穿墙

- 加强地下和无原生地图室内区域的识别，结合地形覆盖、地下区域信息和头顶表面探针选择断面背景。
- 检测到房间顶盖后，断面固定在顶盖下方，避免沿坡道移动时切片随玩家高度漂移。
- 实时小地图捕获统一处理阴影、雾和环境光，并在每个捕获通道结束后恢复游戏原状态。
- 穿墙移动遵循当前飞行模式和原生方向键绑定；视角指向模式跟随镜头方向，世界轴模式保留水平移动。

## 跨箱快捷键调整

同物品跨箱集中现在严格限定为 **Ctrl＋鼠标左键**。

- 左、右 Ctrl 均可使用。
- 同时按下 Shift 或 Alt 时不触发集中。
- 右键、中键保留原生操作。
- 页面提示、启动日志和中英文说明同步更新。

## 运行边界

- 正式玩法支持目标仍是 Unturned 单人世界。
- 世界维护、Snapshot 无限储存和僵尸区域保留仅在真实单人会话执行。
- 多人客户端菜单、每连接风险确认及独立资产翻译沿用 v2.0 的运行边界；多人玩法仍是不受支持的 best-effort 行为。
- 本项目不会关闭、修改或绕过 BattlEye，也不保证 BattlEye 会允许 BepInEx、插件或连接继续运行。

## 安装

1. 另行安装 BepInEx `5.4.23.5` x64。
2. 完整退出游戏，将 Plugin-Only ZIP 解压到包含 `Unturned.exe` 的游戏根目录。
3. 确认仅保留一份活动 DLL：

   `BepInEx\plugins\UnturnedSingleplayerCheatMenu\UnturnedSingleplayerCheatMenu.dll`

4. 进入世界并生成本地玩家后按 `End` 打开菜单。

## 构建与发布包验证

- 当前安装环境：Unturned `3.26.3.13` / Unity `2022.3.62f3` / Windows x64 / Unity Mono。
- Commit: `v2.1.0 (tag target)`
- Release build: 0 warnings / 0 errors
- Smoke: 33/33 PASS (each project built and run)
- External API inventory: 1,731 rows, SHA-256 `035C80F1D997EFF5BFDE0D37763EF13CF6C4EACE1C6A521F1BC46379740E6BE6`
- 已重新审计当前安装版的 Snapshot 原生接口、制作输出和附件搜索，保留程序集哈希变化检查。
- 原生边界回归链接真实生产代码，并在游戏、Unity、输入和 Harmony 边界使用测试替身。
- DLL: 1,269,248 bytes, FileVersion `2.1.0.0`, SHA-256 `1D04866C99E9AF152DEC6184AEAAD6CC70266F456E07F15D948B298CF9329AE1`
- Plugin-Only ZIP: 478,363 bytes, SHA-256 `4658EF096B2C5D70EA234E23242B66C98D4FACCB551837C41900E6BBA1DC6435`
- Deployment: 最终 2.1 发布包本轮未部署；此前 2.0 构建的部署另有记录
- Restarted-game visual / interaction acceptance: 本轮未运行，游戏内视觉与交互仍待验收
- 构建、Smoke 和包哈希不等于真实游戏验收。

---

## English summary

Version 2.1.0 adds previewed, confirmed, frame-budgeted world maintenance for natural resources, zombies, ground items, and native item restocking, alongside environment status and scenario presets. Restore Natural resumes scheduled natural weather after it was disabled.

It fixes authoritative world-item cleanup during Snapshot pickup, real spawn accounting, and duplicate item instantiation; improves underground and unmapped-interior minimap cutaways and capture lighting; and makes noclip follow the selected flight mode and native movement bindings.

Cross-storage consolidation now requires Ctrl + left-click. The maintenance cards use two independent columns to reduce unused space.

The attached archive is Plugin-Only. Install BepInEx 5.4.23.5 x64 separately. Supported gameplay remains singleplayer-only; build, test-harness, package, deployment, and real-game acceptance evidence are recorded separately.

完整的历史变更见 [CHANGELOG](https://github.com/Blowthesnow/Unturned-Singleplayer-Cheat-Menu/blob/main/CHANGELOG.md)。[自愿赞助开发](https://afdian.com/a/Blowthesnow)，不影响插件免费使用。
