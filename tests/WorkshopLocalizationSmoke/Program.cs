using System.Net;using System.Net.Http;using System.Text;using Newtonsoft.Json.Linq;
using UnturnedSingleplayerCheatMenu.Models;
using UnturnedSingleplayerCheatMenu.Services;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

Check(MachineTranslationSettings.NormalizeBatchSize(0) == 1, "Batch size should clamp to the minimum of 1.");
Check(MachineTranslationSettings.NormalizeBatchSize(17) == 17, "Batch size should preserve values inside the supported range.");
Check(MachineTranslationSettings.NormalizeBatchSize(99) == 50, "Batch size should clamp to the maximum of 50.");
Check(MachineTranslationSettings.LooksLikeTargetLanguage("管理员工具", "zh-CN"), "Predominantly Chinese text should be skipped.");
Check(MachineTranslationSettings.LooksLikeTargetLanguage("瓶装蝌蚪可乐", "zh-CN"), "Chinese item names must be recognized as already target-language text.");
Check(MachineTranslationSettings.LooksLikeTargetLanguage("【BA】青翠钰滴", "zh-CN"), "Chinese item names with an English prefix must be recognized as already target-language text.");
Check(!MachineTranslationSettings.LooksLikeTargetLanguage("Admin Tool 中文标签", "zh-CN"), "Mixed English text should not be skipped merely because it contains Chinese.");
Check(!MachineTranslationSettings.LooksLikeTargetLanguage("English description with a 中文 token", "zh-CN"), "A single Chinese token in an English description should remain translatable.");

Check(WorkshopTextIdentity.NormalizeSource("  Repair\r\n  <b>123</b>  ") == "Repair\n  <b>123</b>", "Source normalization should preserve internal whitespace.");
Check(WorkshopTextIdentity.NormalizeSource("e\u0301") == "é", "Source normalization should use Unicode Form C.");

string keyA = WorkshopTextIdentity.CreateKey("English", "zh-CN", WorkshopTextField.ItemName, "Repair");
string keyB = WorkshopTextIdentity.CreateKey("English", "zh-CN", WorkshopTextField.ItemName, "Repair");
string descriptionKey = WorkshopTextIdentity.CreateKey("English", "zh-CN", WorkshopTextField.ItemDescription, "Repair");
string changedTextKey = WorkshopTextIdentity.CreateKey("English", "zh-CN", WorkshopTextField.ItemName, "Repair kit");
Check(keyA == keyB, "Equivalent content should produce a stable key independent of asset identity.");
Check(keyA != descriptionKey && keyA != changedTextKey, "Field type and source text must affect the cache identity.");MachineTranslationSettings unsupportedSettings = new MachineTranslationSettings{    ApiProtocol = "PrivateVendor",    Endpoint = "https://example.test/v1",    Model = "test"};Check(!unsupportedSettings.IsConfigured && !unsupportedSettings.IsProtocolSupported, "Unsupported API protocols must not be treated as configured.");Check(!typeof(WorkshopTranslationWorkItem).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Any(property => property.Name == "Instance"), "Background translation work items must not carry Unity object references.");

WorkshopProtectedText protectedText = WorkshopRichTextProtector.Protect("<b>Repair</b> {0}\nID 123 https://example.test/item");
Check(WorkshopRichTextProtector.TryRestore(protectedText, "__USCM_PROTECTED_0000__修理__USCM_PROTECTED_0001__ __USCM_PROTECTED_0002__ __USCM_PROTECTED_0003__ID __USCM_PROTECTED_0004__ __USCM_PROTECTED_0005__", out string restored, out _), "Protected text should restore after translation.");
Check(restored.Contains("<b>修理</b>") && restored.Contains("{0}") && restored.Contains("\nID 123"), "Protected markup, placeholder, newline, and number must survive.");
string reorderedTranslation = "__USCM_PROTECTED_0004__ __USCM_PROTECTED_0000__修理__USCM_PROTECTED_0001__ __USCM_PROTECTED_0002__ __USCM_PROTECTED_0003__ID __USCM_PROTECTED_0005__";
Check(WorkshopRichTextProtector.TryRestore(protectedText, reorderedTranslation, out string reordered, out _), "Reordered protection markers should be restored safely.");
Check(reordered.Contains("123 <b>修理</b> {0} \nID https://example.test/item"), "Reordered markers should map back to their original protected values.");
string duplicatedTranslation = "__USCM_PROTECTED_0000__修理__USCM_PROTECTED_0000__ __USCM_PROTECTED_0002__ __USCM_PROTECTED_0003__ID __USCM_PROTECTED_0004__ __USCM_PROTECTED_0005__";
Check(!WorkshopRichTextProtector.TryRestore(protectedText, duplicatedTranslation, out _, out _), "Duplicated protection markers must reject the response.");
Check(!WorkshopRichTextProtector.TryRestore(protectedText, "missing markers", out _, out _), "Missing protection markers must reject the response.");

WorkshopTranslationRequest[] requests =
{
    new WorkshopTranslationRequest("a", WorkshopTextField.ItemName, "Repair"),
    new WorkshopTranslationRequest("b", WorkshopTextField.ItemDescription, "A tool")
};
WorkshopTranslationBatchResult valid = MachineTranslationClient.ParseResponse(
    "{\"translations\":[{\"id\":\"a\",\"translation\":\"修理\"},{\"id\":\"b\",\"translation\":\"工具\"}]}", requests);
Check(valid.Success && valid.Translations.Count == 2, "Aligned translation response should be accepted.");
string sseBody = """
data: {"choices":[{"message":{"content":"{\"translations\":[{\"id\":\"a\",\"translation\":\"修理\"}]}"}}]}
data: [DONE]
""";
WorkshopTranslationBatchResult sse = MachineTranslationClient.ParseResponse(sseBody, new[] { requests[0] });
Check(sse.Success && sse.Translations["a"] == "修理", "OpenAI-compatible SSE response should be normalized and parsed.");
MachineTranslationSettings rotatedSettings = new MachineTranslationSettings { ApiKey = "key-a,key-b" };
Check(rotatedSettings.ApiKeyCount == 2, "Comma-separated API keys should be recognized.");
Check(rotatedSettings.GetNextApiKey() == "key-a" && rotatedSettings.GetNextApiKey() == "key-b" && rotatedSettings.GetNextApiKey() == "key-a", "API keys should rotate round-robin.");
WorkshopTranslationBatchResult duplicate = MachineTranslationClient.ParseResponse(
    "{\"translations\":[{\"id\":\"a\",\"translation\":\"修理\"},{\"id\":\"a\",\"translation\":\"工具\"}]}", requests);
Check(!duplicate.Success, "Duplicate response ids must reject the whole batch.");
WorkshopTranslationBatchResult missing = MachineTranslationClient.ParseResponse(
    "{\"translations\":[{\"id\":\"a\",\"translation\":\"修理\"}]}", requests);
Check(!missing.Success, "Missing response ids must reject the whole batch.");

MachineTranslationSettings retrySettings = new MachineTranslationSettings
{
    ApiProtocol = MachineTranslationSettings.OpenAICompatibleProtocol,
    Endpoint = "https://example.test/v1",
    ApiKey = "test-key",
    Model = "test",
    MaxRetries = 1,
    TimeoutSeconds = 5
};
WorkshopTranslationRequest[] clientRequests =
{
    new WorkshopTranslationRequest("a", WorkshopTextField.ItemName, "hello")
};
using (MachineTranslationClient retryClient = new(new ScriptedHandler(failuresBeforeSuccess: 1, throwNetworkError: false)))
{
    WorkshopTranslationBatchResult retried = await retryClient.TranslateAsync(clientRequests, retrySettings, CancellationToken.None);
    Check(retried.Success, "Retryable 5xx responses should retry within the configured limit.");
}
ScriptedHandler networkHandler = new(failuresBeforeSuccess: 0, throwNetworkError: true);
using (MachineTranslationClient networkClient = new(networkHandler))
{
    WorkshopTranslationBatchResult networkFailure = await networkClient.TranslateAsync(clientRequests, retrySettings, CancellationToken.None);
    Check(!networkFailure.Success && networkHandler.CallCount == 1, "HttpRequestException should fail without retrying outside the allowed retry classes.");
}
foreach (string protocol in MachineTranslationSettings.SupportedProtocols)
{
    MachineTranslationSettings protocolSettings = new MachineTranslationSettings
    {
        ApiProtocol = protocol,
        Endpoint = MachineTranslationSettings.GetDefaultEndpoint(protocol),
        ApiKey = "test-key",
        SecretKey = "test-secret",
        Region = "ap-guangzhou",
        ProjectId = "0",
        Model = "test-model",
        MaxRetries = 0,
        TimeoutSeconds = 5
    };
    using ScriptedHandler protocolHandler = new(0, false, protocol);
    using MachineTranslationClient protocolClient = new(protocolHandler);
    WorkshopTranslationBatchResult protocolResult = await protocolClient.TranslateAsync(clientRequests, protocolSettings, CancellationToken.None);
    Check(protocolResult.Success, $"Protocol {protocol} should accept its response shape.");
    Check(protocolSettings.IsCloudProvider || protocolHandler.RequestBody.Contains("test-model") || protocol == MachineTranslationSettings.GeminiProvider, $"Protocol {protocol} should send the configured model when applicable.");
    if (protocol == MachineTranslationSettings.DeepSeekProvider)
    {
        JObject deepSeekBody = JObject.Parse(protocolHandler.RequestBody);
        Check(deepSeekBody.SelectToken("thinking.type")?.Value<string>() == "disabled", "DeepSeek translation should explicitly disable thinking mode.");
        Check(deepSeekBody.SelectToken("response_format.type")?.Value<string>() == "json_object", "DeepSeek translation should request JSON output.");
    }
    if (protocol == MachineTranslationSettings.TencentTmtProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("Authorization"), "Tencent TMT should use TC3 Authorization.");
    else if (protocol == MachineTranslationSettings.BaiduTranslateProvider || protocol == MachineTranslationSettings.YoudaoProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("Query"), $"Protocol {protocol} should carry signed query authentication.");
    else if (protocol == MachineTranslationSettings.MicrosoftTranslatorProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("Ocp-Apim-Subscription-Key"), "Microsoft Translator should use its subscription key header.");
    else if (protocol == MachineTranslationSettings.DeepLProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("Authorization"), "DeepL should use its authorization header.");
    else if (protocol == MachineTranslationSettings.AnthropicProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("x-api-key"), "Anthropic should use x-api-key authentication.");
    else if (protocol == MachineTranslationSettings.AzureOpenAIProvider)
        Check(protocolHandler.RequestHeaders.ContainsKey("api-key"), "Azure OpenAI should use api-key authentication.");
    else
        Check(protocolHandler.RequestHeaders.ContainsKey("Authorization") || protocol == MachineTranslationSettings.GoogleCloudProvider, $"Protocol {protocol} should use its configured authentication.");
}string root = Path.Combine(Path.GetTempPath(), "WorkshopLocalizationSmoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string cachePath = Path.Combine(root, "zh-CN.json");
WorkshopTranslationCache cache = new(cachePath);
cache.Upsert(new WorkshopTranslationCacheEntry
{
    Key = keyA,
    SchemaVersion = WorkshopTextIdentity.SchemaVersion,
    SourceLanguage = "English",
    TargetLanguage = "zh-CN",
    Field = WorkshopTextField.ItemName,
    SourceText = "Repair",
    Translation = "修理",    ApiProtocol = "OpenAICompatible",
    Provider = "Smoke",
    Model = "none",
    CreatedUtc = DateTime.UtcNow,
    UpdatedUtc = DateTime.UtcNow,
    LastUsedUtc = DateTime.UtcNow
});
Check(cache.Save("English", "zh-CN"), "Initial cache save should succeed.");
WorkshopTranslationCache loaded = new(cachePath);
Check(loaded.TryGet(keyA, out WorkshopTranslationCacheEntry loadedEntry) && loadedEntry.Translation == "修理" && loadedEntry.ApiProtocol == "OpenAICompatible", "Cache JSON round-trip must preserve entries.");
loaded.Upsert(new WorkshopTranslationCacheEntry
{
    Key = descriptionKey,
    SchemaVersion = WorkshopTextIdentity.SchemaVersion,
    SourceLanguage = "English",
    TargetLanguage = "zh-CN",
    Field = WorkshopTextField.ItemDescription,
    SourceText = "Repair",
    Translation = "维修",
    Provider = "Smoke",
    Model = "none",
    CreatedUtc = DateTime.UtcNow,
    UpdatedUtc = DateTime.UtcNow,
    LastUsedUtc = DateTime.UtcNow
});
Check(loaded.Save("English", "zh-CN") && File.Exists(cachePath + ".bak"), "Replacing an existing cache should preserve a backup file.");
File.WriteAllText(cachePath, "{not valid json", Encoding.UTF8);
WorkshopTranslationCache recovered = new(cachePath);
Check(recovered.Count == 0 && Directory.GetFiles(root, "zh-CN.json.corrupt-*").Length == 1, "Corrupt cache should be isolated and replaced in memory.");
Directory.Delete(root, true);
Console.WriteLine("Workshop localization smoke checks passed.");sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly int _failuresBeforeSuccess;
    private readonly bool _throwNetworkError;
    private readonly string _protocol;

    internal ScriptedHandler(int failuresBeforeSuccess, bool throwNetworkError, string protocol = null)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
        _throwNetworkError = throwNetworkError;
        _protocol = protocol;
    }

    internal int CallCount { get; private set; }
    internal string RequestBody { get; private set; } = string.Empty;
    internal Dictionary<string, string> RequestHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        RequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            RequestHeaders[header.Key] = string.Join(",", header.Value);
        if (request.RequestUri != null && request.RequestUri.Query.Length > 0)
            RequestHeaders["Query"] = request.RequestUri.Query;
        if (_throwNetworkError)
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("simulated network failure"));
        if (CallCount <= _failuresBeforeSuccess)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        string body = _protocol switch
        {
            MachineTranslationSettings.TencentTmtProvider => "{\"Response\":{\"TargetText\":\"你好\"}}",
            MachineTranslationSettings.BaiduTranslateProvider => "{\"trans_result\":[{\"dst\":\"你好\"}]}",
            MachineTranslationSettings.GoogleCloudProvider => "{\"data\":{\"translations\":[{\"translatedText\":\"你好\"}]}}",
            MachineTranslationSettings.MicrosoftTranslatorProvider => "[{\"translations\":[{\"text\":\"你好\"}]}]",
            MachineTranslationSettings.DeepLProvider => "{\"translations\":[{\"text\":\"你好\"}]}",
            MachineTranslationSettings.YoudaoProvider => "{\"translation\":\"你好\"}",
            MachineTranslationSettings.AnthropicProvider => "{\"content\":[{\"text\":\"{\\\"translations\\\":[{\\\"id\\\":\\\"a\\\",\\\"translation\\\":\\\"你好\\\"}]}\"}]}",
            MachineTranslationSettings.GeminiProvider => "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"translations\\\":[{\\\"id\\\":\\\"a\\\",\\\"translation\\\":\\\"你好\\\"}]}\"}]}}]}",
            MachineTranslationSettings.OllamaProvider => "{\"message\":{\"content\":\"{\\\"translations\\\":[{\\\"id\\\":\\\"a\\\",\\\"translation\\\":\\\"你好\\\"}]}\"}}",
            _ => "{\"choices\":[{\"message\":{\"content\":\"{\\\"translations\\\":[{\\\"id\\\":\\\"a\\\",\\\"translation\\\":\\\"你好\\\"}]}\"}}]}"
        };        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        });
    }
}
