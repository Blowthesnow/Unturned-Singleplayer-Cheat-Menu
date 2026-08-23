using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnturnedSingleplayerCheatMenu.Models;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class MachineTranslationSettings
{
    internal const string TencentTmtProvider = "TencentTmt";
    internal const string BaiduTranslateProvider = "BaiduTranslate";
    internal const string GoogleCloudProvider = "GoogleCloud";
    internal const string MicrosoftTranslatorProvider = "MicrosoftTranslator";
    internal const string DeepLProvider = "DeepL";
    internal const string YoudaoProvider = "Youdao";
    internal const string OpenAICompatibleProtocol = OpenAICompatibleProvider;
    internal const string OpenAIProvider = "OpenAI";
    internal const string OpenAICompatibleProvider = "OpenAICompatible";
    internal const string AzureOpenAIProvider = "AzureOpenAI";
    internal const string AnthropicProvider = "Anthropic";
    internal const string GeminiProvider = "Gemini";
    internal const string OllamaProvider = "Ollama";
    internal const string OpenRouterProvider = "OpenRouter";
    internal const string DeepSeekProvider = "DeepSeek";
    internal const string SiliconFlowProvider = "SiliconFlow";
    internal const string LMStudioProvider = "LMStudio";
    internal const string TencentHunyuanProvider = "TencentHunyuan";
    internal const string BaiduQianfanProvider = "BaiduQianfan";
    internal const string AnthropicProtocol = AnthropicProvider;
    internal const string AzureOpenAIProtocol = AzureOpenAIProvider;
    internal const string GoogleGeminiProtocol = GeminiProvider;
    internal const string OllamaProtocol = OllamaProvider;

    internal static readonly string[] SupportedProtocols =
    {
        TencentTmtProvider, BaiduTranslateProvider, GoogleCloudProvider,
        MicrosoftTranslatorProvider, DeepLProvider, YoudaoProvider,
        OpenAIProvider, OpenAICompatibleProvider, AzureOpenAIProvider,
        AnthropicProvider, GeminiProvider, OllamaProvider, OpenRouterProvider,
        DeepSeekProvider, SiliconFlowProvider, LMStudioProvider,
        TencentHunyuanProvider, BaiduQianfanProvider
    };

    internal static readonly string[] CloudProtocols =
    {
        TencentTmtProvider, BaiduTranslateProvider, GoogleCloudProvider,
        MicrosoftTranslatorProvider, DeepLProvider, YoudaoProvider
    };

    internal string ApiProtocol { get; set; } = OpenAICompatibleProvider;
    internal string Endpoint { get; set; }
    internal string ApiKey { get; set; }
    internal string SecretKey { get; set; }
    internal string Region { get; set; }
    internal string ProjectId { get; set; }
    internal string Model { get; set; }
    internal string SourceLanguage { get; set; } = "English";
    internal string TargetLanguage { get; set; } = "zh-CN";
    internal int BatchSize { get; set; } = 50;
    internal int TimeoutSeconds { get; set; } = 30;
    internal int MaxRetries { get; set; } = 2;
    private readonly object _apiKeyGate = new();
    private int _apiKeyIndex;

    internal string GetNextApiKey()
    {
        string[] keys = (ApiKey ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (keys.Length == 0)
            return string.Empty;
        lock (_apiKeyGate)
        {
            string key = keys[_apiKeyIndex % keys.Length];
            _apiKeyIndex = (_apiKeyIndex + 1) % keys.Length;
            return key;
        }
    }

    internal int ApiKeyCount => (ApiKey ?? string.Empty)
        .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(value => value.Trim())
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Count();

    internal static int NormalizeBatchSize(int value) => Math.Max(1, Math.Min(50, value));

    internal static bool LooksLikeTargetLanguage(string text, string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLanguage))
            return false;
        if (!targetLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return false;

        int chinese = 0;
        int latinOrDigit = 0;
        foreach (char character in text)
        {
            if (character >= '\u4e00' && character <= '\u9fff')
                chinese++;
            else if (char.IsLetterOrDigit(character))
                latinOrDigit++;
        }

        if (chinese == 0)
            return false;
        if (latinOrDigit == 0)
            return chinese >= 2;
        return chinese >= 2 && (double)chinese / (chinese + latinOrDigit) >= 0.60;
    }

    internal bool IsProtocolSupported => SupportedProtocols.Any(value => string.Equals(value, ApiProtocol, StringComparison.OrdinalIgnoreCase));
    internal bool IsCloudProvider => CloudProtocols.Any(value => string.Equals(value, ApiProtocol, StringComparison.OrdinalIgnoreCase));
    internal bool IsLargeModelProvider => IsProtocolSupported && !IsCloudProvider;
    internal bool IsConfigured
    {
        get
        {
            if (!IsProtocolSupported || string.IsNullOrWhiteSpace(Endpoint)) return false;
            if (!string.Equals(ApiProtocol, OllamaProvider, StringComparison.OrdinalIgnoreCase) && ApiKeyCount == 0) return false;
            if (RequiresSecretKey && string.IsNullOrWhiteSpace(SecretKey)) return false;
            if (IsLargeModelProvider && string.IsNullOrWhiteSpace(Model)) return false;
            return true;
        }
    }
    internal bool RequiresSecretKey => string.Equals(ApiProtocol, TencentTmtProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(ApiProtocol, BaiduTranslateProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(ApiProtocol, YoudaoProvider, StringComparison.OrdinalIgnoreCase);
    internal bool RequiresRegion => string.Equals(ApiProtocol, TencentTmtProvider, StringComparison.OrdinalIgnoreCase)
        || string.Equals(ApiProtocol, MicrosoftTranslatorProvider, StringComparison.OrdinalIgnoreCase);

    internal static string GetDisplayName(string provider)
    {
        if (string.Equals(provider, TencentTmtProvider, StringComparison.OrdinalIgnoreCase)) return "腾讯云 TMT";
        if (string.Equals(provider, BaiduTranslateProvider, StringComparison.OrdinalIgnoreCase)) return "百度翻译";
        if (string.Equals(provider, GoogleCloudProvider, StringComparison.OrdinalIgnoreCase)) return "Google Cloud Translation";
        if (string.Equals(provider, MicrosoftTranslatorProvider, StringComparison.OrdinalIgnoreCase)) return "Microsoft Translator";
        if (string.Equals(provider, DeepLProvider, StringComparison.OrdinalIgnoreCase)) return "DeepL";
        if (string.Equals(provider, YoudaoProvider, StringComparison.OrdinalIgnoreCase)) return "有道智云";
        if (string.Equals(provider, OpenAIProvider, StringComparison.OrdinalIgnoreCase)) return "OpenAI";
        if (string.Equals(provider, OpenAICompatibleProvider, StringComparison.OrdinalIgnoreCase)) return "OpenAI 兼容";
        if (string.Equals(provider, AzureOpenAIProvider, StringComparison.OrdinalIgnoreCase)) return "Azure OpenAI";
        if (string.Equals(provider, AnthropicProvider, StringComparison.OrdinalIgnoreCase)) return "Anthropic";
        if (string.Equals(provider, GeminiProvider, StringComparison.OrdinalIgnoreCase)) return "Google Gemini";
        if (string.Equals(provider, OllamaProvider, StringComparison.OrdinalIgnoreCase)) return "Ollama";
        if (string.Equals(provider, OpenRouterProvider, StringComparison.OrdinalIgnoreCase)) return "OpenRouter";
        if (string.Equals(provider, DeepSeekProvider, StringComparison.OrdinalIgnoreCase)) return "DeepSeek";
        if (string.Equals(provider, SiliconFlowProvider, StringComparison.OrdinalIgnoreCase)) return "SiliconFlow";
        if (string.Equals(provider, LMStudioProvider, StringComparison.OrdinalIgnoreCase)) return "LM Studio";
        if (string.Equals(provider, TencentHunyuanProvider, StringComparison.OrdinalIgnoreCase)) return "腾讯混元";
        if (string.Equals(provider, BaiduQianfanProvider, StringComparison.OrdinalIgnoreCase)) return "百度千帆";
        return provider ?? string.Empty;
    }

    internal static string GetDefaultEndpoint(string provider)
    {
        if (string.Equals(provider, TencentTmtProvider, StringComparison.OrdinalIgnoreCase)) return "https://tmt.tencentcloudapi.com";
        if (string.Equals(provider, BaiduTranslateProvider, StringComparison.OrdinalIgnoreCase)) return "https://fanyi-api.baidu.com/api/trans/vip/translate";
        if (string.Equals(provider, GoogleCloudProvider, StringComparison.OrdinalIgnoreCase)) return "https://translation.googleapis.com/language/translate/v2";
        if (string.Equals(provider, MicrosoftTranslatorProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.cognitive.microsofttranslator.com";
        if (string.Equals(provider, DeepLProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.deepl.com/v2/translate";
        if (string.Equals(provider, YoudaoProvider, StringComparison.OrdinalIgnoreCase)) return "https://openapi.youdao.com/api";
        if (string.Equals(provider, OpenAIProvider, StringComparison.OrdinalIgnoreCase) || string.Equals(provider, OpenAICompatibleProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.openai.com/v1/chat/completions";
        if (string.Equals(provider, AzureOpenAIProvider, StringComparison.OrdinalIgnoreCase)) return "https://YOUR_RESOURCE.openai.azure.com";
        if (string.Equals(provider, AnthropicProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.anthropic.com/v1/messages";
        if (string.Equals(provider, GeminiProvider, StringComparison.OrdinalIgnoreCase)) return "https://generativelanguage.googleapis.com/v1beta";
        if (string.Equals(provider, OllamaProvider, StringComparison.OrdinalIgnoreCase)) return "http://127.0.0.1:11434/api/chat";
        if (string.Equals(provider, OpenRouterProvider, StringComparison.OrdinalIgnoreCase)) return "https://openrouter.ai/api/v1/chat/completions";
        if (string.Equals(provider, DeepSeekProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.deepseek.com/v1/chat/completions";
        if (string.Equals(provider, SiliconFlowProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.siliconflow.cn/v1/chat/completions";
        if (string.Equals(provider, LMStudioProvider, StringComparison.OrdinalIgnoreCase)) return "http://127.0.0.1:1234/v1/chat/completions";
        if (string.Equals(provider, TencentHunyuanProvider, StringComparison.OrdinalIgnoreCase)) return "https://api.hunyuan.cloud.tencent.com/v1/chat/completions";
        if (string.Equals(provider, BaiduQianfanProvider, StringComparison.OrdinalIgnoreCase)) return "https://qianfan.baidubce.com/v2/chat/completions";
        return string.Empty;
    }
}
internal sealed class WorkshopTranslationRequest
{
    internal WorkshopTranslationRequest(string id, WorkshopTextField field, string sourceText)
    {
        Id = id;
        Field = field;
        SourceText = sourceText;
    }

    internal string Id { get; }
    internal WorkshopTextField Field { get; }
    internal string SourceText { get; }
}

internal sealed class WorkshopTranslationWorkItem
{
    internal WorkshopTranslationWorkItem(string key, WorkshopTextField field, string sourceText, string assetName, string assetGuid, ushort legacyId, string actionKey)
    {
        Key = key;
        Field = field;
        SourceText = sourceText;
        AssetName = assetName;
        AssetGuid = assetGuid;
        LegacyId = legacyId;
        ActionKey = actionKey;
    }

    internal string Key { get; }
    internal WorkshopTextField Field { get; }
    internal string SourceText { get; }
    internal string AssetName { get; }
    internal string AssetGuid { get; }
    internal ushort LegacyId { get; }
    internal string ActionKey { get; }
}

internal enum WorkshopTranslationErrorKind
{
    None,
    Configuration,
    InvalidRequest,
    Authentication,
    RateLimit,
    Server,
    Timeout,
    Network,
    ResponseFormat,
    Cancelled,
    Unknown
}

internal sealed class WorkshopTranslationBatchResult
{
    internal bool Success { get; set; }
    internal IReadOnlyDictionary<string, string> Translations { get; set; }
    internal string Error { get; set; }
    internal WorkshopTranslationErrorKind ErrorKind { get; set; }
    internal int? StatusCode { get; set; }
    internal bool Retryable { get; set; }

    internal static WorkshopTranslationBatchResult Failed(
        string error,
        WorkshopTranslationErrorKind errorKind = WorkshopTranslationErrorKind.Unknown,
        bool retryable = false,
        int? statusCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorKind = errorKind,
        Retryable = retryable,
        StatusCode = statusCode,
        Translations = new Dictionary<string, string>(StringComparer.Ordinal)
    };
}

internal interface IWorkshopTranslationClient
{
    Task<WorkshopTranslationBatchResult> TranslateAsync(IReadOnlyList<WorkshopTranslationRequest> requests, MachineTranslationSettings settings, CancellationToken cancellationToken);
}

internal sealed class MachineTranslationClient : IWorkshopTranslationClient, IDisposable
{
    private readonly HttpClient _httpClient;

    internal MachineTranslationClient() : this(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = true }) { }

    internal MachineTranslationClient(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public void Dispose() => _httpClient.Dispose();

    public async Task<WorkshopTranslationBatchResult> TranslateAsync(IReadOnlyList<WorkshopTranslationRequest> requests, MachineTranslationSettings settings, CancellationToken cancellationToken)
    {
        if (requests == null || requests.Count == 0)
            return WorkshopTranslationBatchResult.Failed("translation batch is empty");
        if (settings == null || !settings.IsProtocolSupported)
            return WorkshopTranslationBatchResult.Failed("unsupported cloud translation provider");
        if (!settings.IsConfigured)
            return WorkshopTranslationBatchResult.Failed("cloud translation provider is not configured");
        if (requests.Any(request => request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.SourceText)))
            return WorkshopTranslationBatchResult.Failed("translation batch contains an invalid request");
        if (requests.Any(request => request.SourceText.Length > 4000))
            return WorkshopTranslationBatchResult.Failed("a translation item exceeds the character limit");
        if (requests.Select(request => request.Id).Distinct(StringComparer.Ordinal).Count() != requests.Count)
            return WorkshopTranslationBatchResult.Failed("translation batch contains duplicate ids");
        if (settings.IsLargeModelProvider)
            return await TranslateLargeModelAsync(requests, settings, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        int maxRetries = Math.Max(0, Math.Min(5, settings.MaxRetries));
        foreach (WorkshopTranslationRequest request in requests)
        {
            string source = request.SourceText;
            string translated = null;
            string error = null;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using HttpRequestMessage httpRequest = BuildRequest(request, settings);
                    using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, Math.Min(120, settings.TimeoutSeconds))));
                    using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, timeout.Token).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        translated = ParseSingleTranslation(body, settings.ApiProtocol);
                        if (!string.IsNullOrWhiteSpace(translated))
                            break;
                        error = "cloud translation response did not contain translated text";
                        break;
                    }
                    error = $"cloud translation API returned HTTP {(int)response.StatusCode}";
                    if (!IsRetryable(response.StatusCode) || attempt >= maxRetries)
                        break;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    error = "cloud translation request timed out";
                    if (attempt >= maxRetries) break;
                }
                catch (HttpRequestException ex)
                {
                    error = $"cloud translation request failed: {ex.Message}";
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            if (string.IsNullOrWhiteSpace(translated))
                return WorkshopTranslationBatchResult.Failed(error ?? "cloud translation failed");
            translations[request.Id] = translated;
        }
        return new WorkshopTranslationBatchResult { Success = true, Translations = translations };
    }

    private static bool IsRetryable(HttpStatusCode statusCode) => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private async Task<WorkshopTranslationBatchResult> TranslateLargeModelAsync(
        IReadOnlyList<WorkshopTranslationRequest> requests,
        MachineTranslationSettings settings,
        CancellationToken cancellationToken)
    {
        string instruction = "Translate each supplied game text from " + settings.SourceLanguage + " to " + settings.TargetLanguage + ". Return JSON only as {\"translations\":[{\"id\":\"...\",\"translation\":\"...\"}]}. Preserve protected markers exactly. For natural-language item and vehicle names, produce a natural Chinese name where possible; retain genuine brand, model, acronym, GUID, or code fragments only when appropriate. Do not skip an English item or vehicle name because another field of the same asset contains Chinese.";
        string userContent = JsonConvert.SerializeObject(requests.Select(request => new { id = request.Id, field = request.Field.ToString(), text = request.SourceText }));
        JObject body = BuildLargeModelBody(settings, instruction, userContent);
        string endpoint = NormalizeLargeModelEndpoint(settings);
        for (int attempt = 0; attempt <= Math.Max(0, Math.Min(5, settings.MaxRetries)); attempt++)
        {
            try
            {
                using HttpRequestMessage request = BuildLargeModelRequest(endpoint, body.ToString(Formatting.None), settings);
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, Math.Min(120, settings.TimeoutSeconds))));
                using HttpResponseMessage response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return ParseLargeModelResponse(responseBody, requests, settings.ApiProtocol);
                if (!IsRetryable(response.StatusCode) || attempt >= Math.Max(0, Math.Min(5, settings.MaxRetries)))
                    return WorkshopTranslationBatchResult.Failed($"large-model API returned HTTP {(int)response.StatusCode}: {TruncateError(responseBody, settings.ApiKey)}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= settings.MaxRetries)
                    return WorkshopTranslationBatchResult.Failed("large-model request timed out");
            }
            catch (HttpRequestException ex)
            {
                return WorkshopTranslationBatchResult.Failed($"large-model request failed: {ex.Message}");
            }
            catch (WebException ex)
            {
                if (attempt >= Math.Max(0, Math.Min(5, settings.MaxRetries)))
                    return WorkshopTranslationBatchResult.Failed($"large-model network timeout/cancel: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
        }
        return WorkshopTranslationBatchResult.Failed("large-model translation failed");
    }

    private static JObject BuildLargeModelBody(MachineTranslationSettings settings, string instruction, string userContent)
    {
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.AnthropicProvider, StringComparison.OrdinalIgnoreCase))
            return new JObject { ["model"] = settings.Model, ["max_tokens"] = 4096, ["system"] = instruction, ["messages"] = new JArray { new JObject { ["role"] = "user", ["content"] = userContent } } };
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase))
            return new JObject { ["contents"] = new JArray { new JObject { ["role"] = "user", ["parts"] = new JArray { new JObject { ["text"] = instruction + "\n" + userContent } } } }, ["generationConfig"] = new JObject { ["responseMimeType"] = "application/json" } };
        JObject body = new JObject
        {
            ["model"] = settings.Model,
            ["temperature"] = 0,
            ["stream"] = false,
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = instruction },
                new JObject { ["role"] = "user", ["content"] = userContent }
            }
        };

        // DeepSeek reasoning is unnecessary for deterministic translation and
        // can consume the whole request timeout. Require the documented JSON
        // envelope so the response parser receives machine-readable output.
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.DeepSeekProvider, StringComparison.OrdinalIgnoreCase))
        {
            body["thinking"] = new JObject { ["type"] = "disabled" };
            body["response_format"] = new JObject { ["type"] = "json_object" };
            body["max_tokens"] = 4096;
        }

        return body;
    }

    private static HttpRequestMessage BuildLargeModelRequest(string endpoint, string json, MachineTranslationSettings settings)
    {
        HttpRequestMessage request = JsonRequest(endpoint, json);
        string apiKey = settings.GetNextApiKey();
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.AnthropicProvider, StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
        else if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.AzureOpenAIProvider, StringComparison.OrdinalIgnoreCase))
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        else if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return request;
    }

    private static WorkshopTranslationBatchResult ParseLargeModelResponse(string body, IReadOnlyList<WorkshopTranslationRequest> requests, string provider)
    {
        try
        {
            JObject envelope = null;
            string content = null;
            try
            {
                envelope = JObject.Parse(StripCodeFence(body));
                content = ExtractProviderContent(envelope, provider);
            }
            catch (JsonException)
            {
                if (!TryExtractSseContent(body, out content))
                    return WorkshopTranslationBatchResult.Failed("large-model response JSON/SSE was invalid", WorkshopTranslationErrorKind.ResponseFormat);
            }

            content = RemoveThinkingBlock(content);
            JObject translatedObject = string.IsNullOrWhiteSpace(content)
                ? envelope
                : JObject.Parse(StripCodeFence(content));
            JArray translations = translatedObject?["translations"] as JArray;
            if (translations == null || translations.Count != requests.Count)
                return WorkshopTranslationBatchResult.Failed("large-model response count does not match request", WorkshopTranslationErrorKind.ResponseFormat);

            Dictionary<string, string> result = new(StringComparer.Ordinal);
            foreach (JToken token in translations)
            {
                string id = token["id"]?.Value<string>();
                string translation = token["translation"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(translation) || !result.TryAdd(id, translation))
                    return WorkshopTranslationBatchResult.Failed("large-model response contains an invalid id or translation", WorkshopTranslationErrorKind.ResponseFormat);
            }
            if (requests.Any(request => !result.ContainsKey(request.Id)))
                return WorkshopTranslationBatchResult.Failed("large-model response ids do not align with request", WorkshopTranslationErrorKind.ResponseFormat);
            return new WorkshopTranslationBatchResult { Success = true, Translations = result };
        }
        catch (Exception ex)
        {
            return WorkshopTranslationBatchResult.Failed($"large-model response JSON is invalid: {ex.Message}", WorkshopTranslationErrorKind.ResponseFormat);
        }
    }

    private static string ExtractProviderContent(JObject envelope, string provider)
    {
        if (envelope == null)
            return null;
        return string.Equals(provider, MachineTranslationSettings.AnthropicProvider, StringComparison.OrdinalIgnoreCase)
            ? envelope.SelectToken("content[0].text")?.Value<string>()
            : string.Equals(provider, MachineTranslationSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase)
                ? envelope.SelectToken("candidates[0].content.parts[0].text")?.Value<string>()
                : string.Equals(provider, MachineTranslationSettings.OllamaProvider, StringComparison.OrdinalIgnoreCase)
                    ? envelope.SelectToken("message.content")?.Value<string>()
                    : envelope.SelectToken("choices[0].message.content")?.Value<string>();
    }

    private static bool TryExtractSseContent(string body, out string content)
    {
        StringBuilder builder = new();
        foreach (string rawLine in (body ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;
            string payload = line.Substring(5).Trim();
            if (payload.Length == 0 || string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                JObject chunk = JObject.Parse(payload);
                string part = chunk.SelectToken("choices[0].delta.content")?.Value<string>()
                    ?? chunk.SelectToken("choices[0].message.content")?.Value<string>();
                if (!string.IsNullOrEmpty(part))
                    builder.Append(part);
            }
            catch (JsonException)
            {
                // Ignore non-JSON SSE comments/heartbeats; an empty result is rejected below.
            }
        }
        content = builder.ToString();
        return content.Length > 0;
    }

    private static string RemoveThinkingBlock(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;
        int end = content.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        return end >= 0 ? content.Substring(end + "</think>".Length).Trim() : content.Trim();
    }

    private static string NormalizeLargeModelEndpoint(MachineTranslationSettings settings)
    {
        string endpoint = settings.Endpoint.TrimEnd('/');
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.AnthropicProvider, StringComparison.OrdinalIgnoreCase)) return endpoint.EndsWith("/messages", StringComparison.OrdinalIgnoreCase) ? endpoint : endpoint + "/messages";
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.GeminiProvider, StringComparison.OrdinalIgnoreCase)) return endpoint.Contains(":generateContent", StringComparison.OrdinalIgnoreCase) ? endpoint : endpoint + "/models/" + Uri.EscapeDataString(settings.Model) + ":generateContent";
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.OllamaProvider, StringComparison.OrdinalIgnoreCase)) return endpoint.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase) ? endpoint : endpoint + "/api/chat";
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.AzureOpenAIProvider, StringComparison.OrdinalIgnoreCase) && !endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase)) return endpoint + "/openai/deployments/" + Uri.EscapeDataString(settings.Model) + "/chat/completions?api-version=2024-10-21";
        return endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? endpoint : endpoint + "/chat/completions";
    }

    private static HttpRequestMessage JsonRequest(string endpoint, string json)
    {
        return new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }


    private static string TruncateError(string value, string secret)
    {
        string text = value ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(secret))
            text = text.Replace(secret, "***", StringComparison.Ordinal);
        return text.Length <= 800 ? text : text.Substring(0, 800);
    }
    private static string StripCodeFence(string value)
    {
        string text = value?.Trim() ?? string.Empty;
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        int first = text.IndexOf('\n');
        int last = text.LastIndexOf("```", StringComparison.Ordinal);
        return first >= 0 && last > first ? text.Substring(first + 1, last - first - 1).Trim() : text;
    }
    private static HttpRequestMessage BuildRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.TencentTmtProvider, StringComparison.OrdinalIgnoreCase))
            return BuildTencentRequest(request, settings);
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.BaiduTranslateProvider, StringComparison.OrdinalIgnoreCase))
            return BuildBaiduRequest(request, settings);
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.GoogleCloudProvider, StringComparison.OrdinalIgnoreCase))
            return BuildGoogleRequest(request, settings);
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.MicrosoftTranslatorProvider, StringComparison.OrdinalIgnoreCase))
            return BuildMicrosoftRequest(request, settings);
        if (string.Equals(settings.ApiProtocol, MachineTranslationSettings.DeepLProvider, StringComparison.OrdinalIgnoreCase))
            return BuildDeepLRequest(request, settings);
        return BuildYoudaoRequest(request, settings);
    }

    private static HttpRequestMessage BuildGoogleRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        string endpoint = settings.Endpoint.TrimEnd('/') + (settings.Endpoint.Contains("?") ? "&" : "?") + "key=" + Uri.EscapeDataString(settings.ApiKey);
        return JsonRequest(endpoint, new { q = new[] { request.SourceText }, source = settings.SourceLanguage, target = settings.TargetLanguage, format = "text" });
    }

    private static HttpRequestMessage BuildMicrosoftRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        string endpoint = settings.Endpoint.TrimEnd('/') + "/translate?api-version=3.0&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage);
        HttpRequestMessage message = JsonRequest(endpoint, new[] { new { Text = request.SourceText } });
        message.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", settings.ApiKey);
        if (!string.IsNullOrWhiteSpace(settings.Region))
            message.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", settings.Region);
        return message;
    }

    private static HttpRequestMessage BuildDeepLRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        HttpRequestMessage message = JsonRequest(settings.Endpoint, new { text = new[] { request.SourceText }, source_lang = settings.SourceLanguage, target_lang = settings.TargetLanguage });
        message.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + settings.ApiKey);
        return message;
    }

    private static HttpRequestMessage BuildBaiduRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        string salt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string sign = Md5(settings.ApiKey + request.SourceText + salt + settings.SecretKey);
        string endpoint = settings.Endpoint + "?q=" + Uri.EscapeDataString(request.SourceText) + "&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage) + "&appid=" + Uri.EscapeDataString(settings.ApiKey) + "&salt=" + salt + "&sign=" + sign;
        return new HttpRequestMessage(HttpMethod.Get, endpoint);
    }

    private static HttpRequestMessage BuildYoudaoRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        string salt = Guid.NewGuid().ToString("N");
        string curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string sign = Sha256(settings.ApiKey + Truncate(request.SourceText) + salt + curtime + settings.SecretKey);
        string endpoint = settings.Endpoint + "?q=" + Uri.EscapeDataString(request.SourceText) + "&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage) + "&appKey=" + Uri.EscapeDataString(settings.ApiKey) + "&salt=" + salt + "&sign=" + sign + "&signType=v3&curtime=" + curtime;
        return new HttpRequestMessage(HttpMethod.Get, endpoint);
    }

    private static HttpRequestMessage BuildTencentRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
    {
        const string service = "tmt";
        string host = new Uri(settings.Endpoint).Host;
        const string action = "TextTranslate";
        const string version = "2018-03-21";
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd");
        string payload = JsonConvert.SerializeObject(new { SourceText = request.SourceText, Source = settings.SourceLanguage, Target = settings.TargetLanguage, ProjectId = ParseProjectId(settings.ProjectId) });
        string hashedPayload = Sha256(payload);
        string canonicalHeaders = "content-type:application/json\n" + "host:" + host + "\n";
        string signedHeaders = "content-type;host";
        string canonicalRequest = "POST\n/\n\n" + canonicalHeaders + "\n" + signedHeaders + "\n" + hashedPayload;
        string credentialScope = date + "/" + service + "/tc3_request";
        string stringToSign = "TC3-HMAC-SHA256\n" + timestamp + "\n" + credentialScope + "\n" + Sha256(canonicalRequest);
        byte[] secretDate = Hmac("TC3" + settings.SecretKey, date);
        byte[] secretService = Hmac(secretDate, service);
        byte[] secretSigning = Hmac(secretService, "tc3_request");
        string signature = HmacHex(secretSigning, stringToSign);
        HttpRequestMessage message = JsonRequest(settings.Endpoint, payload);
        message.Headers.Host = host;
        message.Headers.TryAddWithoutValidation("X-TC-Action", action);
        message.Headers.TryAddWithoutValidation("X-TC-Version", version);
        message.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString());
        message.Headers.TryAddWithoutValidation("X-TC-Region", settings.Region ?? string.Empty);
        message.Headers.TryAddWithoutValidation("Authorization", "TC3-HMAC-SHA256 Credential=" + settings.ApiKey + "/" + credentialScope + ", SignedHeaders=" + signedHeaders + ", Signature=" + signature);
        return message;
    }

    private static HttpRequestMessage JsonRequest(string endpoint, object body)
    {
        return new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body is string text ? text : JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };
    }

    internal static WorkshopTranslationBatchResult ParseResponse(string body, IReadOnlyList<WorkshopTranslationRequest> requests)
    {
        return ParseLargeModelResponse(body, requests, MachineTranslationSettings.OpenAICompatibleProvider);
    }
    private static string ParseSingleTranslation(string body, string provider)
    {
        JToken json = JToken.Parse(body);
        if (string.Equals(provider, MachineTranslationSettings.TencentTmtProvider, StringComparison.OrdinalIgnoreCase)) return json.SelectToken("Response.TargetText")?.Value<string>();
        if (string.Equals(provider, MachineTranslationSettings.BaiduTranslateProvider, StringComparison.OrdinalIgnoreCase)) return json.SelectToken("trans_result[0].dst")?.Value<string>();
        if (string.Equals(provider, MachineTranslationSettings.GoogleCloudProvider, StringComparison.OrdinalIgnoreCase)) return json.SelectToken("data.translations[0].translatedText")?.Value<string>();
        if (string.Equals(provider, MachineTranslationSettings.MicrosoftTranslatorProvider, StringComparison.OrdinalIgnoreCase)) return json.SelectToken("[0].translations[0].text")?.Value<string>();
        if (string.Equals(provider, MachineTranslationSettings.DeepLProvider, StringComparison.OrdinalIgnoreCase)) return json.SelectToken("translations[0].text")?.Value<string>();
        return json.SelectToken("translation")?.Value<string>() ?? json.SelectToken("translation[0]")?.Value<string>();
    }
    private static int ParseProjectId(string value) => int.TryParse(value, out int result) ? result : 0;
    private static string Truncate(string value) => value.Length <= 20 ? value : value.Substring(0, 10) + value.Length + value.Substring(value.Length - 10, 10);
    private static string Md5(string value) => Hex(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(value)));
    private static string Sha256(string value) => Hex(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(value)));
    private static byte[] Hmac(string key, string value) => Hmac(Encoding.UTF8.GetBytes(key), value);
    private static byte[] Hmac(byte[] key, string value)
    {
        using HMACSHA256 hmac = new(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }
    private static string HmacHex(byte[] key, string value) => Hex(Hmac(key, value));
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
}
