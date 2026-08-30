using System;
using System.Collections.Generic;
using System.Linq;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class MachineTranslationSettings
{
	internal const int MaxApiKeyCount = 2;

	internal const string TencentTmtProvider = "TencentTmt";

	internal const string BaiduTranslateProvider = "BaiduTranslate";

	internal const string GoogleCloudProvider = "GoogleCloud";

	internal const string MicrosoftTranslatorProvider = "MicrosoftTranslator";

	internal const string DeepLProvider = "DeepL";

	internal const string YoudaoProvider = "Youdao";

	internal const string OpenAICompatibleProtocol = "OpenAICompatible";

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

	internal const string AnthropicProtocol = "Anthropic";

	internal const string AzureOpenAIProtocol = "AzureOpenAI";

	internal const string GoogleGeminiProtocol = "Gemini";

	internal const string OllamaProtocol = "Ollama";

	internal static readonly string[] SupportedProtocols = new string[18]
	{
		"TencentTmt", "BaiduTranslate", "GoogleCloud", "MicrosoftTranslator", "DeepL", "Youdao", "OpenAI", "OpenAICompatible", "AzureOpenAI", "Anthropic",
		"Gemini", "Ollama", "OpenRouter", "DeepSeek", "SiliconFlow", "LMStudio", "TencentHunyuan", "BaiduQianfan"
	};

	internal static readonly string[] CloudProtocols = new string[6] { "TencentTmt", "BaiduTranslate", "GoogleCloud", "MicrosoftTranslator", "DeepL", "Youdao" };

	private readonly object _apiKeyGate = new object();

	private int _apiKeyIndex;

	private string _apiKey = string.Empty;

	internal string ApiProtocol { get; set; } = "OpenAICompatible";

	internal string Endpoint { get; set; }

	internal string ApiKey
	{
		get => _apiKey;
		set => _apiKey = SerializeApiKeys(ParseApiKeys(value));
	}

	internal string SecretKey { get; set; }

	internal string Region { get; set; }

	internal string ProjectId { get; set; }

	internal string Model { get; set; }

	internal string SourceLanguage { get; set; } = "English";

	internal string TargetLanguage { get; set; } = "zh-CN";

	internal int BatchSize { get; set; } = 50;

	internal int TimeoutSeconds { get; set; } = 30;

	internal int MaxRetries { get; set; } = 2;

	internal IReadOnlyList<string> ApiKeys => ParseApiKeys(ApiKey);

	internal int ApiKeyCount => ApiKeys.Count;

	internal int ParallelWorkerCount => Math.Max(1, ApiKeyCount);

	internal bool IsProtocolSupported => SupportedProtocols.Any((string value) => string.Equals(value, ApiProtocol, StringComparison.OrdinalIgnoreCase));

	internal bool IsCloudProvider => CloudProtocols.Any((string value) => string.Equals(value, ApiProtocol, StringComparison.OrdinalIgnoreCase));

	internal bool IsLargeModelProvider
	{
		get
		{
			if (IsProtocolSupported)
			{
				return !IsCloudProvider;
			}
			return false;
		}
	}

	internal bool IsConfigured
	{
		get
		{
			if (!IsProtocolSupported || string.IsNullOrWhiteSpace(Endpoint))
			{
				return false;
			}
			if (!string.Equals(ApiProtocol, "Ollama", StringComparison.OrdinalIgnoreCase) && ApiKeyCount == 0)
			{
				return false;
			}
			if (RequiresSecretKey && string.IsNullOrWhiteSpace(SecretKey))
			{
				return false;
			}
			if (IsLargeModelProvider && string.IsNullOrWhiteSpace(Model))
			{
				return false;
			}
			return true;
		}
	}

	internal bool RequiresSecretKey
	{
		get
		{
			if (!string.Equals(ApiProtocol, "TencentTmt", StringComparison.OrdinalIgnoreCase) && !string.Equals(ApiProtocol, "BaiduTranslate", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(ApiProtocol, "Youdao", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	internal bool RequiresRegion
	{
		get
		{
			if (!string.Equals(ApiProtocol, "TencentTmt", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(ApiProtocol, "MicrosoftTranslator", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}

	internal string GetNextApiKey()
	{
		string[] array = ApiKeys.ToArray();
		if (array.Length == 0)
		{
			return string.Empty;
		}
		lock (_apiKeyGate)
		{
			string result = array[_apiKeyIndex % array.Length];
			_apiKeyIndex = (_apiKeyIndex + 1) % array.Length;
			return result;
		}
	}

	internal IReadOnlyList<MachineTranslationSettings> CreateApiKeyWorkerSettings()
	{
		IReadOnlyList<string> apiKeys = ApiKeys;
		if (apiKeys.Count == 0)
		{
			return new[] { CloneForApiKey(string.Empty) };
		}
		return apiKeys.Select(CloneForApiKey).ToArray();
	}

	internal static string SerializeApiKeys(IEnumerable<string> values)
	{
		return string.Join("\n", NormalizeApiKeys(values));
	}

	private static IReadOnlyList<string> ParseApiKeys(string value)
	{
		return NormalizeApiKeys((value ?? string.Empty).Split(new char[4] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
	}

	private static string[] NormalizeApiKeys(IEnumerable<string> values)
	{
		return (values ?? Array.Empty<string>())
			.Select(value => (value ?? string.Empty).Trim())
			.Where(value => value.Length > 0)
			.Distinct(StringComparer.Ordinal)
			.Take(MaxApiKeyCount)
			.ToArray();
	}

	private MachineTranslationSettings CloneForApiKey(string apiKey)
	{
		return new MachineTranslationSettings
		{
			ApiProtocol = ApiProtocol,
			Endpoint = Endpoint,
			ApiKey = apiKey,
			SecretKey = SecretKey,
			Region = Region,
			ProjectId = ProjectId,
			Model = Model,
			SourceLanguage = SourceLanguage,
			TargetLanguage = TargetLanguage,
			BatchSize = BatchSize,
			TimeoutSeconds = TimeoutSeconds,
			MaxRetries = MaxRetries
		};
	}

	internal static int NormalizeBatchSize(int value)
	{
		return Math.Max(1, Math.Min(100, value));
	}

	internal static int ResolveParallelBatchSize(int totalItems, int configuredBatchSize, int workerCount)
	{
		int normalizedBatchSize = NormalizeBatchSize(configuredBatchSize);
		if (totalItems <= 0)
		{
			return normalizedBatchSize;
		}
		int availableWorkers = Math.Max(1, Math.Min(MaxApiKeyCount, workerCount));
		int balancedBatchSize = (totalItems + availableWorkers - 1) / availableWorkers;
		return Math.Max(1, Math.Min(normalizedBatchSize, balancedBatchSize));
	}

	internal static bool LooksLikeTargetLanguage(string text, string targetLanguage)
	{
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLanguage))
		{
			return false;
		}
		if (!targetLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		int num = 0;
		int num2 = 0;
		foreach (char c in text)
		{
			if (c >= '一' && c <= '鿿')
			{
				num++;
			}
			else if (char.IsLetterOrDigit(c))
			{
				num2++;
			}
		}
		if (num == 0)
		{
			return false;
		}
		if (num2 == 0)
		{
			return num >= 2;
		}
		if (num >= 2)
		{
			return (double)num / (double)(num + num2) >= 0.6;
		}
		return false;
	}

	internal static string GetDisplayName(string provider)
	{
		if (string.Equals(provider, "TencentTmt", StringComparison.OrdinalIgnoreCase))
		{
			return "腾讯云 TMT";
		}
		if (string.Equals(provider, "BaiduTranslate", StringComparison.OrdinalIgnoreCase))
		{
			return "百度翻译";
		}
		if (string.Equals(provider, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
		{
			return "Google Cloud Translation";
		}
		if (string.Equals(provider, "MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
		{
			return "Microsoft Translator";
		}
		if (string.Equals(provider, "DeepL", StringComparison.OrdinalIgnoreCase))
		{
			return "DeepL";
		}
		if (string.Equals(provider, "Youdao", StringComparison.OrdinalIgnoreCase))
		{
			return "有道智云";
		}
		if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
		{
			return "OpenAI";
		}
		if (string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
		{
			return "OpenAI 兼容";
		}
		if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
		{
			return "Azure OpenAI";
		}
		if (string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			return "Anthropic";
		}
		if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
		{
			return "Google Gemini";
		}
		if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
		{
			return "Ollama";
		}
		if (string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase))
		{
			return "OpenRouter";
		}
		if (string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
		{
			return "DeepSeek";
		}
		if (string.Equals(provider, "SiliconFlow", StringComparison.OrdinalIgnoreCase))
		{
			return "SiliconFlow";
		}
		if (string.Equals(provider, "LMStudio", StringComparison.OrdinalIgnoreCase))
		{
			return "LM Studio";
		}
		if (string.Equals(provider, "TencentHunyuan", StringComparison.OrdinalIgnoreCase))
		{
			return "腾讯混元";
		}
		if (string.Equals(provider, "BaiduQianfan", StringComparison.OrdinalIgnoreCase))
		{
			return "百度千帆";
		}
		return provider ?? string.Empty;
	}

	internal static string GetDefaultEndpoint(string provider)
	{
		if (string.Equals(provider, "TencentTmt", StringComparison.OrdinalIgnoreCase))
		{
			return "https://tmt.tencentcloudapi.com";
		}
		if (string.Equals(provider, "BaiduTranslate", StringComparison.OrdinalIgnoreCase))
		{
			return "https://fanyi-api.baidu.com/api/trans/vip/translate";
		}
		if (string.Equals(provider, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
		{
			return "https://translation.googleapis.com/language/translate/v2";
		}
		if (string.Equals(provider, "MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.cognitive.microsofttranslator.com";
		}
		if (string.Equals(provider, "DeepL", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.deepl.com/v2/translate";
		}
		if (string.Equals(provider, "Youdao", StringComparison.OrdinalIgnoreCase))
		{
			return "https://openapi.youdao.com/api";
		}
		if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase) || string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.openai.com/v1/chat/completions";
		}
		if (string.Equals(provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
		{
			return "https://YOUR_RESOURCE.openai.azure.com";
		}
		if (string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.anthropic.com/v1/messages";
		}
		if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
		{
			return "https://generativelanguage.googleapis.com/v1beta";
		}
		if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
		{
			return "http://127.0.0.1:11434/api/chat";
		}
		if (string.Equals(provider, "OpenRouter", StringComparison.OrdinalIgnoreCase))
		{
			return "https://openrouter.ai/api/v1/chat/completions";
		}
		if (string.Equals(provider, "DeepSeek", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.deepseek.com/v1/chat/completions";
		}
		if (string.Equals(provider, "SiliconFlow", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.siliconflow.cn/v1/chat/completions";
		}
		if (string.Equals(provider, "LMStudio", StringComparison.OrdinalIgnoreCase))
		{
			return "http://127.0.0.1:1234/v1/chat/completions";
		}
		if (string.Equals(provider, "TencentHunyuan", StringComparison.OrdinalIgnoreCase))
		{
			return "https://api.hunyuan.cloud.tencent.com/v1/chat/completions";
		}
		if (string.Equals(provider, "BaiduQianfan", StringComparison.OrdinalIgnoreCase))
		{
			return "https://qianfan.baidubce.com/v2/chat/completions";
		}
		return string.Empty;
	}
}
