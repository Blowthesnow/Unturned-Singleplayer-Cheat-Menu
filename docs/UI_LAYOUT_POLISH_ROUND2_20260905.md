# UI 布局优化：第二轮

## 已实施

| Before | After | Why |
| --- | --- | --- |
| 翻译状态和任务按钮随配置一起滚走 | 状态、进度摘要、开始和停止按钮放在滚动区外的顶部 | 查看配置时仍能查看任务状态和停止任务 |
| 配置、译文显示、缓存维护混排 | 分为接口与密钥、译文显示、高级与缓存维护 | 高频状态与低频配置分离 |
| 接口配置始终展开 | 首次未配置时展开，已配置时折叠；之后记住本会话的选择 | 新用户能找到配置，日常使用减少占用 |
| 高级操作与主要开始按钮竞争 | 高级区默认折叠，扫描并应用缓存使用次要按钮样式 | 保留所有入口但降低干扰 |
| 开始与停止缺少运行状态区分 | 翻译中禁用重复开始；无翻译任务时禁用停止；扫描中禁用开始 | 状态可直接通过操作区读取 |
| 天气区拉高，扫描信息另占整行 | 时钟左列，天气与扫描信息组成右列；两列等宽、顶部对齐 | 利用原有右侧留白 |
| 扫描与接口说明使用固定单行高度 | 14 参考字号，按文本首选高度换行；仅保留 24 单位最小高度 | 长中文、英文和模型名称不再被固定单行高度约束 |
| 改变接口说明或窗口尺寸后，滚动高度可能过时 | 说明变化及分辨率／缩放切换时重算滚动高度 | 适应新增的多行文字布局 |

折叠通过 SetActive 切换同一组控件，不清空草稿、不销毁输入框、不运行翻译或缓存命令。切换翻译服务协议仍沿用现有的设置区刷新流程。

## 范围与保全

- 修改：TranslationToolsView、ToolsShell、WorldView、主窗口的布局刷新、PluginLocalization、UiLayoutPolishSmoke。
- 新增：`D:\Unturned BepInEx\src\UnturnedSingleplayerCheatMenu\UI\CheatMenuOverlayUi.LayoutPolish.cs`，仅包含多行标签构造辅助方法。
- 对本轮开始时已有的 src / tests 文件进行 SHA-256 快照核对；只有上述预定文件发生变化，其他已有源文件保持哈希一致。新增文件单独列出，不包含在初始哈希集合中。
- 按调用文本核对：翻译 Presenter 的 28 处、世界 Presenter 的 11 处原有调用均保留。此检查不代替运行时行为验证。
- 未修改配置文件、服务实现、全局 Canvas 缩放或字体系统；未调用翻译 API，未启动游戏，未部署、提交或发布。

## 验证证据

- UiLayoutPolishSmoke：上一轮 11 项 + 本轮 12 项，共 **23/23** 通过。
- 本轮新增检查均经历 RED → GREEN。追加发现并处理了列宽、接口说明高度、窗口尺寸变化后的重排缺口。
- 最终 Release 构建：**0 警告 / 0 错误**。
- 最终 7 个 Smoke 项目通过：UiArchitectureSmoke、UiControlLibrarySmoke、UiMotionSmoke、UiTransientMotionSmoke、LocalizationSmoke、WorkshopLocalizationSmoke、UiLayoutPolishSmoke。
- git diff --check 通过。
- 日志与本轮前快照：`D:\Unturned BepInEx\artifacts\ui-polish-round2-20260905`。
- 候选 DLL：`D:\Unturned BepInEx\artifacts\UnturnedSingleplayerCheatMenu.dll`。
- 候选 SHA-256：`B0D5A2C3C770F9C48AC457D162CCFD770B22986E0399C33834D06FC1087574F2`。
- 候选包含整个工作区原有修改，不是仅包含本轮 UI 变更的独立部署包。

## 实机尚未验证

上述布局检查主要是源码契约；本轮没有运行 Unity 场景或获得新游戏截图，不能据此宣称无裁切、视觉效果已验收。

部署后的验收清单：

1. 翻译页：未配置、已配置、空闲、翻译中、错误、服务未就绪。
2. 滚动至页面底部，任务状态和开始／停止仍可见；键盘焦点不进入折叠内容。
3. 编辑 Endpoint、模型名、两个 API Key；折叠再展开，输入保留；仅折叠不发起网络请求。
4. 长模型名、中文／英文下的状态摘要、缓存路径完整换行；切换窗口缩放或分辨率后仍能滚到最后一项。
5. 世界页的时钟、天气按钮、扫描按钮不重叠；长说明不挤窄时钟列；时钟拖动行为不变。
6. 1280×720、1920×1080、16:10、21:9，以及现有缩放 0.75 / 1 / 1.5。

全局小屏字体可读性、外观设置合并尚未改造；本轮仅改善上述两页的排版及长文本适配。

## 布局依据

多行标签让父布局读取 Text 的首选高度，不给文字额外加自我控制尺寸的组件，遵循 [Unity uGUI 自动布局说明](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/UIAutoLayout.html)。
