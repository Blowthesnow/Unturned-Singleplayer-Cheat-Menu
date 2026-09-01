# Unturned 游戏本体框架汉化落地方案

> 日期：2026-08-31
> 状态：方案与 UI 入口已落地；本体运行时覆盖、翻译任务和字体后端适配尚未实现。
> 当前版本边界：Unturned Unity 2022.3.62f3、Unity Mono、BepInEx 5.4.23.5、插件 `netstandard2.1`。

## 目标

把现有“原版与 Workshop 资产翻译”扩展为两条互不混淆的产品线：

1. **本体**：游戏菜单、设置、服务器列表、HUD、提示、按钮、暂停/死亡界面、编辑器文字和三种 Glazier 后端字体；
2. **资产 / Workshop**：物品、载具、NPC、任务、地图和 Workshop 资产文字，继续复用现有扫描、缓存和机器翻译服务。

本方案不把玩家聊天、服务器名称、在线新闻、图片中烘焙文字或外部插件文字计入“本体汉化完成率”。

## 已验证现状

### 实机与程序集

- 游戏目录：`I:\Steam\steamapps\common\Unturned`；
- 当前 `Assembly-CSharp.dll` SHA-256：`D00E2889729CCA8CB11DB85C18956F4F7C25D043FB3E7F4E029ADDC1CB5A5D9A`；
- 项目 `D:\Unturned BepInEx\lib\Assembly-CSharp.dll` 与实机文件哈希一致；
- 默认 Glazier 后端为 uGUI；`-Glazier=IMGUI` 和 `-Glazier=UIToolkit` 可切换另外两套不兼容后端。

### 已安装汉化补丁

补丁来源：

```text
I:\Steam\steamapps\workshop\content\304930\3263565146
```

补丁是纯数据包：

| 区域 | 文件数 | 用途 |
|---|---:|---|
| `Localization\Schinese` | 144 `.dat` | 本体 Menu / Player / Server / Editor / Shared 文本 |
| `Bundles` | 2,701 `.dat` | 原版物品、载具、对象、NPC 等资产文本 |
| `Maps` | 10 `.dat` | 地图文本 |
| DLL / Module / AssetBundle / 字体 | 0 | 不含运行时代码和字体注入 |

补丁的 2,858 个文件均能在游戏根目录找到逐字节相同副本。客户端日志记录：

```text
Found Steam language 'schinese' in root Localization directory
```

因此当前本体汉化依赖复制到游戏根目录的 `Localization\Schinese`，不是 BepInEx Hook。

### 本体文本入口

当前程序集存在：

- 147 个 `Localization.read("literal")` 调用点；
- 142 个唯一核心本地化路径；
- 主链：`Provider.localizationRoot → Localization.read(path) → Local.TryReadString(key) → Local.format(key,args)`；
- 另有 38 条较明确的非空硬编码英文 UI 文本，需要单独归档处理。

当前 English 基线约 3,638 keys。中文补丁相对基线：

- 缺少 270 keys；
- 缺少整文件 `Menu\Play\MenuPlayServerCuration.dat`；
- `FOV_Slider_LabelV2_Name` 多出 `{0}`；
- `Tutorial_Vehicle` 丢失 `{2}`；
- `Tutorial_Fire` 丢失 `{0}`。

## 目标覆盖

### 计入本体覆盖

- `Localization\{Language}\Menu\**\*.dat`；
- `Localization\{Language}\Player\**\*.dat`；
- `Localization\{Language}\Editor\**\*.dat`；
- 客户端会显示的 Server / Shared 本地化键；
- 原生按钮、Label、Tooltip、Placeholder；
- HUD、交互提示、教程、暂停、死亡、制作、技能和服务器浏览器；
- 默认 uGUI、可选 IMGUI 和实验性 UIToolkit 的中文字体显示。

### 不计入本体覆盖

- 物品、载具、NPC、任务和地图资产名称；这些属于“资产 / Workshop”；
- 玩家聊天、角色名、服务器名和服务端自定义消息；
- 在线新闻、Steam 或网页远端内容；
- 纹理、截图和视频中的烘焙文字；
- 其他插件自行创建的 UI。

## 数据模型

未来本体目录使用稳定的 `(relative_path, key)` 作为唯一身份，不使用英文原文作为主键：

```text
CoreLocalizationRecord
  RelativePath          /Menu/Play/MenuPlaySingleplayer.dat
  Key                   Play_Button
  SourceText            Play Offline
  Translation           开始游戏
  SourceHash            SHA-256(source text + placeholder signature)
  PlaceholderSignature  {0},{1},...
  Origin                RootPatch | PluginCache | MachineTranslation
  Status                Ready | Missing | Stale | PlaceholderMismatch | Hardcoded
```

缓存建议：

```text
BepInEx/cache/UnturnedSingleplayerCheatMenu/CoreTranslations/zh-CN.json
```

不得直接复用现有资产缓存键空间，避免相同英文短句在不同界面语境互相污染。

## 运行时架构

```text
English/Schinese .dat
        │
        ▼
CoreLocalizationCatalog ── QA / missing / stale / placeholder
        │
        ▼
CoreLocalizationOverlay keyed by (path,key)
        │
        ├─ Localization.read Postfix
        │    └─ ConditionalWeakTable<Local,string> 保存 Local → path
        │
        ├─ Local.TryReadString Prefix
        │    └─ 在 string.Format 之前提供 overlay 模板
        │
        └─ Local.read Postfix
             └─ 覆盖少数保留 null/empty 语义的直接读取
```

### Hook 边界

| 目标 | 用途 | 约束 |
|---|---|---|
| `Localization.read(string path)` | 记录 `Local` 实例所属路径 | 不修改英文 fallback |
| `Local.TryReadString(string key, out string text)` | 按 `(path,key)` 注入译文模板 | 注入前必须通过占位符 QA |
| `Local.read(string key)` | 覆盖不用 `format` 的读取 | 保留 null/empty 行为 |
| Glazier Text / Tooltip setters | 记录硬编码英文漏网 | 默认不直接翻译玩家和网络动态文本 |

`FormatEnglishOrEmpty` 保持英文语义，不参与覆盖，以免破坏搜索、对照或内部逻辑。

## 硬编码漏网

最终 setter 观察器分别覆盖 Glazier 的 uGUI、IMGUI、UIToolkit 实现，但只写诊断记录：

```text
timestamp, backend, concrete_type, property, source_text, scene, stack_fingerprint
```

进入自动翻译队列前必须分类：

- 固定游戏 UI：可转为稳定补丁；
- 调试/编辑器文字：按需要翻译；
- 玩家、服务器或网页动态文字：默认排除；
- 已含富文本、格式参数或输入绑定：先保留 token 再翻译。

## 字体策略

### 默认 Glazier uGUI

当前 `UI/Glazier_uGUI/LiberationSans` 的 TMP fallback 已包含 `NotoSansCJK Fallback`。内置 `NotoSansCJK-Regular` 字体数据约 18.7 MB，覆盖 20,945 个 CJK Unified 字符。

运行时先执行探针：

```text
中文汉化按钮提示菜单物品地图界面游戏字体
```

仅在 `HasCharacters(..., searchFallbacks: true)` 失败或 fallback 表项丢失时：

1. 去重追加现有 CJK TMP fallback；
2. 保留 Glazier Latin 主字体及 `Font_Default / Outline / Shadow / Tooltip` 材质；
3. 请求动态 CJK 字符并让已有 TMP 控件重建 mesh；
4. 不把 CJK TMP asset 直接替换成主字体。

### IMGUI

Dark / Light GUISkin 当前主字体为 Unity builtin `LegacyRuntime`。支持该后端时，把 skin 主字体切换为：

```text
Resources.Load<Font>("Fonts/NotoSansCJK-Regular")
```

所有未单独指定字体的 GUIStyle 继承 GUISkin 主字体。

### UIToolkit

当前 PanelSettings 的 `textSettings` 为空，USS 未指定字体。支持该后端时，在插件可控根节点设置：

```csharp
root.style.unityFontDefinition =
    new StyleFontDefinition(FontDefinition.FromFont(notoSansCjk));
```

UIToolkit 仍是实验后端，字体通过不代表滚动、裁剪和阴影行为通过。

### 插件 Legacy uGUI

插件自己的 `UiText : UnityEngine.UI.Text` 保持现有 `SystemFontProvider`：默认 Microsoft YaHei UI，并通过 GDI 和 Unity `HasCharacter` 探针验证。它与原生 Glazier TMP 字体是两条独立链路。

## 翻译页面信息架构

翻译工具页分为两个子页：

```text
翻译
├─ 本体
│  ├─ 当前实机基线
│  ├─ 覆盖范围
│  ├─ 实施顺序
│  ├─ 验收边界
│  └─ 本文档路径
└─ 资产 / Workshop
   └─ 保留现有扫描、缓存、API 配置和翻译控制
```

“本体”页当前是如实状态页，不显示虚假的扫描或翻译按钮；运行时服务完成后，再把状态卡升级为真实进度与控制区。

## 分阶段实施

### 阶段 0：冻结与采集

- 记录游戏版本、Assembly-CSharp SHA-256、English 与 Schinese 清单；
- 备份现有根目录汉化文件；
- 建立只读扫描和回滚清单；
- 不覆盖当前资产翻译缓存。

### 阶段 1：静态目录与 QA

- 枚举 142 个核心路径；
- 补齐缺少的整文件和 270 keys；
- 检查 `{0}` 等占位符、富文本标签、按键 token、换行和空值语义；
- 输出 missing / stale / placeholder-mismatch 报告。

### 阶段 2：插件 overlay

- 新增 `CoreLocalizationCatalog`、`CoreLocalizationCache`、`CoreLocalizationOverlay`；
- 注册 `Localization.read`、`Local.TryReadString`、`Local.read` Harmony Patch；
- 缓存写入使用临时文件、原子替换和 `.bak`；
- 校验失败时回退现有 Schinese，再回退 English。

### 阶段 3：机器翻译接入

- 复用现有供应商、Endpoint、API Key、Model、批量和并发设置；
- 本体任务使用独立队列、缓存和进度；
- 仅提交 missing / stale 记录；
- token 或 placeholder 失败的结果不得进入生效缓存。

### 阶段 4：硬编码观察与定点修复

- 接入三后端 setter 观察器；
- 排除聊天、服务器名和远端内容；
- 把确认稳定的固定字符串转成上下文映射或具体调用点 Patch；
- 不使用全局英文字符串替换。

### 阶段 5：字体后端

- 默认 uGUI 先验证现有 CJK fallback；
- IMGUI 绑定内置 Noto Font；
- UIToolkit 设置根字体定义；
- 验证输入框、Tooltip、富文本、动态字号与场景切换。

### 阶段 6：导出与发布

- 可选导出完整 `Localization\Schinese` 数据包；
- 导出包必须包含源版本、文件数、key 数、哈希清单和回滚脚本；
- 插件 overlay 与导出数据包不能同时重复覆盖同一 key；
- 未完成实机矩阵前不标记正式发布。

## 验收矩阵

### 静态与 Smoke

- English 文件覆盖：100%；
- `(path,key)` 唯一且无重复冲突；
- placeholder / rich-text / token 签名一致；
- 缺失文件与缺失 key 为 0；
- overlay miss 时按 Schinese → English 正确回退；
- 本体与资产缓存完全隔离；
- 切换“本体 / 资产”不重建整个 Tools Shell，不丢 API Key 输入状态。

### Build

- `dotnet build UnturnedSingleplayerCheatMenu.slnx -c Release`；
- 当前所有 `tests/**/*Smoke.csproj` 重新构建后再运行；
- 不使用旧 Smoke 二进制证明新断言通过。

### 运行时日志

- 记录当前 `Provider.language` 与 `Provider.localizationRoot`；
- 记录三条 Hook 的签名验证结果；
- 记录 core cache 版本、命中、回退、拒绝和 placeholder 错误；
- 字体日志记录后端、主字体、fallback、探针缺字；
- 禁止逐帧刷同一文本。

### 实机画面

至少检查：

- 主菜单、设置、服务器列表、Workshop；
- 背包、制作、技能、HUD、教程和交互提示；
- 暂停、死亡、NPC 对话和商店；
- 默认 uGUI、`-Glazier=IMGUI`、`-Glazier=UIToolkit`；
- 16:9、16:10、21:9；
- 中文输入、Tooltip、滚动、裁剪、富文本、输入焦点和场景切换。

Build、Smoke、DLL 哈希和日志不能替代这些实机检查。

## 修改边界

本次已落地：

- 本文档；
- 翻译页的“本体 / 资产 / Workshop”子页路由；
- 本体页只读基线与实施状态；
- 对应英文界面文案和 UiArchitectureSmoke 契约。

本次未落地：

- 本体扫描服务；
- Core cache / overlay；
- Harmony Patch；
- 本体机器翻译队列；
- 原生字体运行时修改；
- 游戏目录部署。

## 风险

| 风险 | 处理 |
|---|---|
| 游戏更新新增或重命名 key | 每个版本从 English 重新生成差异，不盲沿用旧计数 |
| 占位符损坏导致 `string.Format` 异常 | 生效前强制 placeholder 签名门闸 |
| 全局 setter 翻译误伤聊天或服务器文本 | setter 默认只观察，翻译必须进入上下文白名单 |
| 替换 TMP 主字体导致材质与 atlas 不匹配 | 保留 Glazier 主字体，只维护 fallback |
| UI 页切换销毁 API Key 输入状态 | 只销毁子页 View，设置状态继续由 presenter/config 持有 |
| UI Toolkit 实验后端行为不完整 | 独立验收，不用 uGUI 通过替代 UIToolkit 通过 |

## 回滚

### UI 入口

回滚以下源文件中本次新增的本体子页路由即可恢复原单页 Workshop 翻译界面：

```text
src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.ToolsShell.cs
src/UnturnedSingleplayerCheatMenu/UI/CheatMenuOverlayUi.TranslationToolsView.cs
src/UnturnedSingleplayerCheatMenu/Services/PluginLocalization.cs
tests/UiArchitectureSmoke/Program.cs
```

### 未来运行时功能

- 禁用 core overlay 配置后立即回退现有 Schinese；
- 删除或改名插件 core cache 不得影响游戏根目录汉化；
- 字体 Patch 必须可单独关闭；
- 部署前备份 DLL，游戏停止后只保留一个活动插件 DLL，并校验 SHA-256。
