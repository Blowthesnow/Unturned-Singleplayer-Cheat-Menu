# AiNiee 协议与供应商实现对照研究

日期：2026-08-23  
研究范围：官方仓库 `NEKOparapa/AiNiee` 当前 `main` 在本次读取到的固定提交 `38b70aa0403303da383aae539712e6504ca25d40`（提交时间 2026-08-21），以及 DeepSeek、OpenAI、Anthropic、Google 官方 API 文档。  
研究性质：只读研究；本次只新增本文件，没有修改 Unturned 插件源代码。

## 结论先行

1. **DeepSeek 在 AiNiee 中被当作 OpenAI-compatible Chat Completions 平台处理，而不是独立 HTTP 协议。** AiNiee 使用 OpenAI Python SDK 的 `OpenAI(base_url=..., api_key=...)` 客户端；DeepSeek 的差异通过请求体中的 `thinking`、`reasoning_effort` 等平台参数补齐。DeepSeek 官方也同时提供 OpenAI 格式的 Chat Completions API，因此这一判断是正确的。
2. **AiNiee 最值得移植的不是某个供应商 SDK，而是分层结构：平台配置 → API 格式分发 → 客户端工厂/复用 → 专用 requester → 统一响应归一化。** 这能避免把 DeepSeek、Anthropic、Gemini 和本地 LLM 的请求体、认证头和响应路径混在一个 `if/else` 中。
3. **DeepSeek JSON 输出的关键不是只加 `response_format`。** 官方要求同时：设置 `response_format:{"type":"json_object"}`、在 prompt 中出现 `json`、给足 `max_tokens`；官方还提示 JSON 输出偶尔可能返回空内容。AiNiee 的 OpenAI requester 已有标准 ChatCompletion 与异常 SSE 两条解析路径，但其当前 OpenAI requester 源码中没有看到统一的“批量翻译 JSON envelope 校验”层；这部分在 Unturned 插件中仍应保留并加强。
4. **AiNiee 的 API key rotation 是平台配置层能力，不是 HTTP 协议能力。** `TaskConfig.py` 将逗号分隔的 key 字符串拆成列表，并在生成请求配置时调用 `get_next_apikey()`；同时把 RPM 按 key 数量放大。这种设计适合移植，但必须在 C# 中加入线程安全轮换、按 key 单独错误统计、401/403 时隔离失效 key，以及避免把多 key 简单等同于供应商真实配额增加。
5. **当前 Unturned 插件已有不少协议基础：** OpenAI-compatible/DeepSeek/Anthropic/Gemini/Ollama/Azure 等协议枚举、endpoint 归一化、Bearer/x-api-key/api-key 认证头、DeepSeek `thinking`/JSON、Anthropic/Gemini/Ollama/OpenAI 多响应路径、HTTP 429/5xx 重试和批次 JSON ID 对齐校验。最明显的差距是：当前插件是串行处理批内请求、超时默认较短、没有 AiNiee 那种按 RPM/TPM 计算并发度和 key rotation 的独立限流层。

## 证据基线与仓库状态

本报告以以下固定提交为源码基线：

- [AiNiee commit 38b70aa](https://github.com/NEKOparapa/AiNiee/commit/38b70aa0403303da383aae539712e6504ca25d40)
- 提交信息：`fix: 更新模型列表和适配接口规范`
- 提交时间：2026-08-21 15:30:12 UTC

动态 `main` 可能继续变化；因此下方 GitHub 链接优先使用该提交的 blob URL，而不是未固定的 `main` URL。

## 1. AiNiee 的平台配置模型

### `Resource/platforms/preset.json`

源码：[preset.json @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/Resource/platforms/preset.json)

AiNiee 的平台不是只有一个 provider 名称，而是一组可编辑配置。已核对的字段包括：

- `tag`：内部平台标识，例如 `deepseek`、`anthropic`、`google`、`custom`。
- `api_url`：基础 URL；例如 DeepSeek 预设为 `https://api.deepseek.com/v1`，Anthropic 为 `https://api.anthropic.com`，Gemini 为 `https://generativelanguage.googleapis.com`。
- `api_key`：密钥，可在 TaskConfig 层进一步拆分为多个 key。
- `api_format`：`OpenAI`、`Anthropic`、`Google` 等协议格式；自定义平台依此选择 requester。
- `model` / `model_datas`：默认模型和可选模型。
- `rpm_limit`：请求速率配置；不是单次请求 batch size。
- `temperature`、`think_switch`、`think_depth`、`thinking_budget`、`thinking_level`：模型/推理参数。
- `tls_switch`、`extra_body`：兼容特殊中转站或自定义请求体。
- `key_in_settings`：决定哪些字段在界面中暴露。

已核对的预设事实：

- DeepSeek 预设使用 `api_format: "OpenAI"`，`api_url: "https://api.deepseek.com/v1"`，模型为 `deepseek-v4-flash`。
- Anthropic 使用独立的 `api_format: "Anthropic"`。
- Gemini 使用独立的 `api_format: "Google"`。
- `custom` 平台允许在 `OpenAI`、`Anthropic`、`Google` 之间选择，并保留 `extra_body`。

这说明 AiNiee 的“供应商”与“协议格式”是两个概念：供应商用于默认 URL、模型和参数；协议格式决定请求器。

## 2. 请求器分发与客户端工厂

### `ModuleFolders/Infrastructure/LLMRequester/LLMRequester.py`

源码：[LLMRequester.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/LLMRequester.py)

`sent_request()` 先读 `target_platform` 和 `api_format`，然后按平台/格式选择 requester：

- `google` 或自定义平台 + `api_format == "Google"` → `GoogleRequester`。
- `anthropic` 或自定义平台 + `api_format == "Anthropic"` → `AnthropicRequester`。
- `sakura` → `SakuraRequester`。
- `LocalLLM` → `LocalLLMRequester`。
- `amazonbedrock` → `AmazonbedrockRequester`。
- 其余 → `OpenaiRequester`。

这不是单纯按 URL 猜测协议；平台标签和显式格式共同决定路由。C# 移植时建议把当前 `MachineTranslationSettings.ApiProtocol` 拆成：

```text
ProviderId      // DeepSeek、OpenAI、Azure、Anthropic、Gemini、Ollama...
ProtocolKind    // OpenAIChat、AnthropicMessages、GeminiGenerateContent、OllamaChat...
```

### `ModuleFolders/Infrastructure/LLMRequester/LLMClientFactory.py`

源码：[LLMClientFactory.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/LLMClientFactory.py)

工厂做了三件适合移植的事：

1. **客户端复用。** `LLMClientFactory` 使用单例和线程锁，按 `(类型, URL, key, TLS/extra_body 等)` 组成缓存 key，避免每一条文本都创建一个客户端。
2. **统一 transport 配置。** 普通 OpenAI 客户端使用 `httpx`，可选 `curl_cffi` transport 处理特殊 TLS 指纹场景；普通 HTTPX 客户端设置 HTTP/2、最大连接数、keep-alive 数量和过期时间。
3. **协议专用客户端。** OpenAI、Anthropic、Bedrock、Google 分别创建对应 SDK client；Google 的 `api_url` 与 `extra_body` 通过 `http_options` 传入。

对 BepInEx 的直接建议：使用一个长期存活的 `HttpClient`/`SocketsHttpHandler`，按 endpoint + provider + key 配置复用；不要照搬 `curl_cffi`，除非实际服务出现 TLS 指纹拦截且已确认这是必要条件。游戏插件中优先保持依赖少、可诊断性高。

## 3. DeepSeek：OpenAI-compatible 还是独立协议？

### 官方 API 证据

- [DeepSeek Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion)
- [DeepSeek Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode)
- [DeepSeek JSON Output](https://api-docs.deepseek.com/guides/json_mode)

DeepSeek 官方接口是 `POST /chat/completions`，请求体使用 `messages`、`model` 等 OpenAI Chat Completions 形状；官方示例也使用 OpenAI 客户端并把 `base_url` 指向 DeepSeek。因此 AiNiee 将它放在 `OpenaiRequester` 中是合理的。

但“兼容”不等于“完全相同”：

- DeepSeek 增加 `thinking: {"type":"enabled"|"disabled"}`。
- DeepSeek 支持 `reasoning_effort`，官方列出 `low`、`high`、`max`；兼容层可能把其他值映射到这些值。
- JSON 输出使用 `response_format: {"type":"json_object"}`。
- 官方要求 prompt 中包含 `json`，并要求合理设置 `max_tokens`，否则 JSON 可能被截断。
- 官方提示 JSON 模式偶尔可能返回空内容，因此客户端不能只做一次 `JObject.Parse` 后就假定必然有结果。

### AiNiee 的 DeepSeek/OpenAI 参数处理

源码：[OpenaiRequester.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/OpenaiRequester.py)

已核对的实现要点：

- `apply_platform_thinking_params()` 根据 `target_platform`、`api_url`、`model_name` 识别 DeepSeek、OpenAI、xAI、智谱、DashScope 等平台。
- 对 DeepSeek，关闭思考时将 `extra_body["thinking"]` 设置为 `{ "type": "disabled" }`；开启时设置为 enabled，并根据界面强度映射 `reasoning_effort`。
- 对 OpenAI 推理模型使用顶层 `reasoning_effort`，并在需要时移除 `temperature`。
- 对 DashScope/Qwen 使用 `extra_body.enable_thinking`，说明 AiNiee 不把所有 OpenAI-compatible 平台当作字面完全相同。
- 请求基础参数包含 `model`、`messages`、`timeout`、`stream: false`，可选 `temperature` 和 `extra_body`。
- 为了兼容忽略 `stream: false` 的中转站，调用 `with_raw_response`；先尝试标准 ChatCompletion 解析，失败后把原始文本按 SSE 解析。
- SSE 解析跳过空行、`event:`、`id:`、`retry:` 等非 `data:` 行，合并 `choices[0].delta.content` 和 `reasoning_content`，并读取末尾 usage。
- 标准响应解析兼容 `message.content` 中的 `<think>...</think>`，以及独立的 `message.reasoning_content`。

这套“标准 JSON + 原始响应 + SSE 降级”的容错值得移植到 C#；但要避免把响应解析异常静默吞掉，必须保留 HTTP 状态码、Content-Type、原始响应前若干字符（脱敏后）和解析分支。

## 4. Anthropic、Google、LocalLLM 的协议差异

### `AnthropicRequester.py`

源码：[AnthropicRequester.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/AnthropicRequester.py)

AiNiee 使用 Anthropic SDK 的 `client.messages.create()`，请求字段与 OpenAI 不同：

- `model`、`max_tokens`、`messages`。
- system prompt 单独传入 `system`，而不是作为 `messages[0].role=system`。
- 思考模型使用 Anthropic 的 `thinking` 配置；关闭时对允许关闭的模型发送 `{"type":"disabled"}`。
- 响应不是 `choices[0].message.content`，而是 content blocks；AiNiee 遍历 block，分别收集 `thinking` 和文本 block。

官方协议参考：[Anthropic Messages API](https://docs.anthropic.com/en/api/messages)。C# 不应把 Anthropic 仅作为 OpenAI endpoint 替换；至少要独立处理 `x-api-key`、`anthropic-version`、`system`、content blocks 和 max_tokens。

### `GoogleRequester.py`

源码：[GoogleRequester.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/GoogleRequester.py)

AiNiee 使用 Google GenAI SDK 的 `client.models.generate_content()`：

- 消息会从 OpenAI 风格转换为 Google `Content`/`Part`。
- `assistant` 角色转换为 Google 的 `model` 角色。
- AiNiee 特别处理最后一条 `model` 预填充，因为新版 Gemini 不支持以 model 角色结尾的预填充内容。
- Gemini 3.x 使用 `thinking_level`；较早 Gemini 版本使用 `thinking_budget`。
- 结果需要从 Gemini response candidates/content parts 提取，而不是读取 OpenAI choices。

官方协议参考：[Gemini GenerateContent API](https://ai.google.dev/api/generate-content)。官方请求形状是 `contents[]`，响应主要从 `candidates[]` 读取；因此 Gemini 应保持独立 requester。

### `LocalLLMRequester.py`

本次已确认该文件被 `LLMRequester.py` 作为本地协议分支调用，但由于本次 GitHub 连接在抓取剩余目录时中断，未完整复核该文件全文。路径：[LocalLLMRequester.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/LocalLLMRequester.py)。

已能确认的边界：AiNiee 将 `LocalLLM` 与普通 OpenAI-compatible 云端平台分开路由；不要根据 `api_url` 是否含 `/v1` 就自动把本地服务和云服务视为相同，因为本地服务可能有 slots、并发和无 key 规则。

## 5. API key rotation、RPM/TPM 与并发

### `TaskConfig.py`

源码：[TaskConfig.py @ 38b70aa](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/TaskConfig/TaskConfig.py)

已完整读取到的实现要点：

- TaskConfig 保存 `apikey_index`、`apikey_list`，并用锁保护配置和 key 相关操作。
- 平台的 `api_key` 会去除空白后按逗号拆分；空字符串被视为本地/无需 key 的特殊值。
- `prepare_for_active_platform()` 从平台配置读取 `rpm_limit`，然后按 `len(apikey_list)` 放大 RPM。
- `thread_counts_setting()`：用户显式指定线程数时直接使用；Sakura/LocalLLM 尝试读取 `/slots`；否则由 `calculate_thread_count()` 将 RPM 线性映射到 1–100 个线程。
- `get_active_platform_configuration()` 返回当前 endpoint、轮换后的 API key、格式、模型、timeout、temperature、extra_body、思考参数等完整请求配置。

### `RequestLimiter.py`、`TranslatorTask.py`、`TaskExecutor.py`

本次通过官方仓库结构和已读取的仓库索引确认了以下路径，但未能在网络中断前完整下载每个文件全文，因此以下内容标为**部分证据**，不冒充逐行源码复核：

- `ModuleFolders/Infrastructure/RequestLimiter/RequestLimiter.py`
- `ModuleFolders/Service/TaskExecutor/TranslatorTask.py`
- `ModuleFolders/Service/TaskExecutor/PolisherTask.py`
- `ModuleFolders/Service/TaskExecutor/TaskExecutor.py`
- `ModuleFolders/Service/SimpleExecutor/SimpleExecutor.py`

仓库索引摘要指出：

- RequestLimiter 同时考虑 RPM 与 TPM，并使用锁/token-bucket 风格的 limiter。
- TranslatorTask/PolisherTask 在 limiter 不允许时等待，再继续尝试；请求失败或空回复会进入下一轮处理。
- TaskExecutor/SimpleExecutor 以行数或 token 限制生成 chunks，并使用线程池执行任务。
- `rpm_limit` 还参与 `TaskConfig.calculate_thread_count()`，用于决定并发线程数量。

这些摘要与已完整读取的 `TaskConfig.py` 相互吻合，但在正式移植前仍应重新抓取上述文件全文，尤其要核对：是否对 429 使用 Retry-After、是否对 401/403 隔离 key、TPM 估算是否使用 prompt+completion、任务超时是单请求还是整批超时、以及失败任务的最大重试次数。

## 6. 错误分类与响应解析对照

### AiNiee 已确认的错误边界

在 `OpenaiRequester.py` 中，异常最终通过 requester 的 `try/except` 记录并返回 `skip=True` 一类失败结果；标准响应解析失败会尝试 SSE。请求器本身主要负责“请求和抽取”，任务层负责把 skip、空内容和任务失败汇总。

这种分层是合理的，但从已读源码看，OpenAI requester 没有把所有 HTTP 状态码细分为稳定的领域错误对象。C# 版应进一步规范为：

```text
Authentication        // 401/403，通常停止当前 key 或要求用户修正配置
RateLimited           // 429，遵守 Retry-After，进入限流等待
TransientServer       // 408/5xx/网络断开，可有限次指数退避
InvalidRequest        // 400/422，请求体或模型参数错误，不应盲重试
ResponseProtocol      // HTTP 成功但 JSON/SSE/字段不符合协议
ContentRejected       // 服务商返回安全/策略拒绝
Cancelled             // 用户主动停止
Timeout               // 连接/读取/整批超时
```

### 当前 Unturned 插件对照

已读取：

- [MachineTranslationClient.cs](../../src/UnturnedSingleplayerCheatMenu/Services/MachineTranslationClient.cs)
- [WorkshopLocalizationService.cs](../../src/UnturnedSingleplayerCheatMenu/Services/WorkshopLocalizationService.cs)

当前实现已经包含：

- `MachineTranslationSettings` 中的 provider/protocol 枚举、endpoint、API key、secret、region、project id、model、batch size、timeout、max retries。
- `NormalizeLargeModelEndpoint()` 对 Anthropic `/messages`、Gemini `:generateContent`、Ollama `/api/chat`、Azure deployment URL 和 OpenAI `/chat/completions` 做路径归一化。
- `BuildLargeModelRequest()` 对 Anthropic 使用 `x-api-key` + `anthropic-version`，Azure 使用 `api-key`，其他带 key 的协议使用 Bearer。
- DeepSeek 批量请求使用 `thinking: {type: disabled}`、`response_format: {type: json_object}`、`max_tokens: 4096`。
- OpenAI/DeepSeek/Ollama 等响应从 `choices[0].message.content` 读取，Anthropic 从 `content[0].text` 读取，Gemini 从 `candidates[0].content.parts[0].text` 读取。
- 批量翻译响应要求 `translations` 数量与请求数相等，ID 不重复且必须覆盖所有请求 ID。
- HTTP 429/5xx 可重试，网络异常和超时有有限次数重试。
- `WorkshopLocalizationService` 按 `Settings.BatchSize` 切批，当前默认值 50，批内逐批调用 client。

与 AiNiee 的主要差距：

1. 当前插件没有独立的 RPM/TPM limiter，也没有按 provider/key 计算并发数。
2. 当前插件没有 key rotation；API key 是单值配置。
3. 当前插件的重试分类仍较粗，401/403、400、429、5xx、解析失败需要分别进入不同状态和提示。
4. 当前 DeepSeek JSON 请求已符合官方核心要求，但仍应处理官方所说的空 content、截断 JSON、`finish_reason` 异常和供应商返回错误 envelope。
5. 当前大模型批次是“一个 HTTP 请求内包含 N 条翻译”，不是 AiNiee 那种按任务 chunks + 多 worker 并发的完整执行模型；不能直接把 AiNiee 的 `rpm_limit` 当作“每次发送多少条”。

## 7. 适合移植到 C# BepInEx 的设计

### 建议直接移植

1. **Protocol adapter 分层**：`IOpenAiChatAdapter`、`IAnthropicMessagesAdapter`、`IGeminiAdapter`、`IOllamaAdapter`，共享一个 `TranslationBatchExecutor`。
2. **Provider profile**：保存默认 endpoint、协议类型、模型、思考参数规则、认证类型、最大 batch 建议值和限流信息。
3. **长生命周期 HTTP client**：按 provider/endpoint 复用 `HttpClient`，避免每批建立连接。
4. **标准响应 + SSE fallback**：先按 Content-Type/标准 JSON 解析；若服务端无视 `stream=false` 或返回 `text/event-stream`，再按 SSE 合并 delta。
5. **结构化错误对象**：把 HTTP 状态、provider code、Retry-After、脱敏 body 摘要、attempt、batch id、key index 记录到日志。
6. **批量结果 ID 对齐**：保留当前插件已有的数量、重复 ID、缺失 ID 校验；失败时只重试未完成的子集，而不是无条件重发整个批次。
7. **配置验证与 endpoint 归一化**：在开始任务前显示最终请求 URL、协议和必需字段是否齐全，但绝不显示 key。

### 需要谨慎移植或改造

- **RPM 按 key 数量简单相乘**：只有在供应商明确按 key 独立计费/限流时才成立；更安全的 C# 实现是每个 key 独立 limiter，按实际响应状态动态降级。
- **RPM 线性映射线程数**：这是 AiNiee 的启发式，不是通用协议规则。Unturned 游戏内翻译更应优先串行/低并发，避免卡顿、429 和大量同时写入缓存。
- **curl_cffi/TLS 指纹模拟**：不应作为默认依赖；先验证真实网络错误是否确实是 TLS 指纹问题。
- **把 `max_tokens` 固定为 4096**：应根据 batch 条数、源文本长度和目标语言长度动态计算，并在达到上限时缩小 batch 或拆分重试。
- **把所有空响应都重试**：需要区分服务端明确拒答、被安全过滤、finish_reason=length、JSON 截断和真正网络失败。

## 8. 对当前 DeepSeek 翻译问题的具体判断

“卡住/全部失败”不能仅通过把 batch size 从 5 改到 50 解决。一次 50 条请求会同时放大：输入 token、输出 token、JSON 体积、服务端生成时间、超时概率和 JSON 截断概率。AiNiee 的 `rpm_limit`/线程数体系解决的是任务调度和限流，不会让单个 50 条请求更容易被 DeepSeek 接受。

当前更稳妥的执行策略是：

```text
1. 默认 batch 先用 5–10 条，而不是 50 条。
2. DeepSeek 明确发送 thinking.disabled + response_format=json_object。
3. prompt 明确包含 json，并给出固定 envelope 示例。
4. max_tokens 按批次内容动态留余量；解析失败时优先缩小批次重试。
5. 单次 timeout 至少覆盖实际生成时间；429 遵守 Retry-After。
6. 401/403 立即提示 key 无效，不要重复重试。
7. 400/422 记录脱敏请求摘要并停止该批，不要盲目重试。
8. 5xx、网络断开、读取超时才使用有限指数退避。
```

## 9. 最终移植建议

**建议移植 AiNiee 的架构思想和容错边界，不建议复制整个 Python SDK/transport。** 对本项目最有价值的下一阶段是：

1. 将 `MachineTranslationClient` 拆成 provider adapter + shared executor。
2. 增加用户可配置的 batch size、timeout、max retries、最大并发和可选 key 列表。
3. 添加一个 C# `RequestLimiter`，至少支持 RPM、并发数和 Retry-After；TPM 可先做估算，不要把估算值当成准确账单。
4. 增加 DeepSeek 专用回归测试：JSON、thinking disabled、空 content、截断 JSON、SSE、429、401、400、ID 缺失/重复。
5. 保持游戏 UI 只显示脱敏错误和当前批次进度；完整响应摘要写入日志但过滤 API key。

## 10. 本次研究的已知缺口

以下文件路径已确认，但由于本次 GitHub 连接在抓取剩余目录全文时中断，未将其逐行源码作为本报告的完整证据：

- `ModuleFolders/Infrastructure/RequestLimiter/RequestLimiter.py`
- `ModuleFolders/Service/TaskExecutor/TranslatorTask.py`
- `ModuleFolders/Service/TaskExecutor/PolisherTask.py`
- `ModuleFolders/Service/TaskExecutor/TaskExecutor.py`
- `ModuleFolders/Service/SimpleExecutor/SimpleExecutor.py`
- `ModuleFolders/Infrastructure/LLMRequester/LocalLLMRequester.py`

因此本报告对这些文件只保留“路径已确认、执行层摘要与已读取的 TaskConfig/LLMRequester 相互吻合”的结论，没有把未完整抓取的实现细节写成确定事实。正式改代码前应固定同一 commit 重新读取这些文件，并特别核对 Retry-After、key 轮换时机、线程池生命周期、任务取消和失败子集重试。

## 官方与源码链接索引

- [AiNiee 固定提交 38b70aa](https://github.com/NEKOparapa/AiNiee/commit/38b70aa0403303da383aae539712e6504ca25d40)
- [Resource/platforms/preset.json](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/Resource/platforms/preset.json)
- [LLMRequester.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/LLMRequester.py)
- [LLMClientFactory.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/LLMClientFactory.py)
- [OpenaiRequester.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/OpenaiRequester.py)
- [AnthropicRequester.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/AnthropicRequester.py)
- [GoogleRequester.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/LLMRequester/GoogleRequester.py)
- [TaskConfig.py](https://github.com/NEKOparapa/AiNiee/blob/38b70aa0403303da383aae539712e6504ca25d40/ModuleFolders/Infrastructure/TaskConfig/TaskConfig.py)
- [DeepSeek Chat Completions API](https://api-docs.deepseek.com/api/create-chat-completion)
- [DeepSeek Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode)
- [DeepSeek JSON Output](https://api-docs.deepseek.com/guides/json_mode)
- [OpenAI Chat Completions API](https://platform.openai.com/docs/api-reference/chat)
- [Anthropic Messages API](https://docs.anthropic.com/en/api/messages)
- [Gemini GenerateContent API](https://ai.google.dev/api/generate-content)