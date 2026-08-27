using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnturnedSingleplayerCheatMenu.Services;

internal sealed class MachineTranslationClient : IWorkshopTranslationClient, IDisposable
{
	private readonly HttpClient _httpClient;

	internal MachineTranslationClient()
		: this(new HttpClientHandler
		{
			AllowAutoRedirect = false,
			UseProxy = true
		})
	{
	}

	internal MachineTranslationClient(HttpMessageHandler handler)
	{
		_httpClient = new HttpClient(handler ?? throw new ArgumentNullException("handler"), disposeHandler: true)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
	}

	public void Dispose()
	{
		_httpClient.Dispose();
	}

	public async Task<WorkshopTranslationBatchResult> TranslateAsync(IReadOnlyList<WorkshopTranslationRequest> requests, MachineTranslationSettings settings, CancellationToken cancellationToken)
	{
		if (requests == null || requests.Count == 0)
		{
			return WorkshopTranslationBatchResult.Failed("translation batch is empty");
		}
		if (settings == null || !settings.IsProtocolSupported)
		{
			return WorkshopTranslationBatchResult.Failed("unsupported cloud translation provider");
		}
		if (!settings.IsConfigured)
		{
			return WorkshopTranslationBatchResult.Failed("cloud translation provider is not configured");
		}
		if (requests.Any((WorkshopTranslationRequest workshopTranslationRequest) => workshopTranslationRequest == null || string.IsNullOrWhiteSpace(workshopTranslationRequest.Id) || string.IsNullOrWhiteSpace(workshopTranslationRequest.SourceText)))
		{
			return WorkshopTranslationBatchResult.Failed("translation batch contains an invalid request");
		}
		if (requests.Any((WorkshopTranslationRequest workshopTranslationRequest) => workshopTranslationRequest.SourceText.Length > 4000))
		{
			return WorkshopTranslationBatchResult.Failed("a translation item exceeds the character limit");
		}
		if (requests.Select((WorkshopTranslationRequest workshopTranslationRequest) => workshopTranslationRequest.Id).Distinct<string>(StringComparer.Ordinal).Count() != requests.Count)
		{
			return WorkshopTranslationBatchResult.Failed("translation batch contains duplicate ids");
		}
		if (settings.IsLargeModelProvider)
		{
			return await TranslateLargeModelAsync(requests, settings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		Dictionary<string, string> translations = new Dictionary<string, string>(StringComparer.Ordinal);
		int maxRetries = Math.Max(0, Math.Min(5, settings.MaxRetries));
		foreach (WorkshopTranslationRequest request in requests)
		{
			_ = request.SourceText;
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
					using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
					string body = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
					if (response.IsSuccessStatusCode)
					{
						translated = ParseSingleTranslation(body, settings.ApiProtocol);
						if (string.IsNullOrWhiteSpace(translated))
						{
							error = "cloud translation response did not contain translated text";
						}
					}
					else
					{
						error = $"cloud translation API returned HTTP {(int)response.StatusCode}";
						if (IsRetryable(response.StatusCode) && attempt < maxRetries)
						{
							goto IL_0589;
						}
					}
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					error = "cloud translation request timed out";
					if (attempt >= maxRetries)
					{
						break;
					}
					goto IL_0589;
				}
				catch (HttpRequestException ex2)
				{
					error = "cloud translation request failed: " + ex2.Message;
				}
				break;
				IL_0589:
				await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (string.IsNullOrWhiteSpace(translated))
			{
				return WorkshopTranslationBatchResult.Failed(error ?? "cloud translation failed");
			}
			translations[request.Id] = translated;
		}
		return new WorkshopTranslationBatchResult
		{
			Success = true,
			Translations = translations
		};
	}

	private static bool IsRetryable(HttpStatusCode statusCode)
	{
		if (statusCode != HttpStatusCode.TooManyRequests)
		{
			return statusCode >= HttpStatusCode.InternalServerError;
		}
		return true;
	}

	private async Task<WorkshopTranslationBatchResult> TranslateLargeModelAsync(IReadOnlyList<WorkshopTranslationRequest> requests, MachineTranslationSettings settings, CancellationToken cancellationToken)
	{
		string instruction = "Translate each supplied game text from " + settings.SourceLanguage + " to " + settings.TargetLanguage + ". Return JSON only as {\"translations\":[{\"id\":\"...\",\"translation\":\"...\"}]}. Preserve protected markers exactly. For natural-language item and vehicle names, produce a natural Chinese name where possible; retain genuine brand, model, acronym, GUID, or code fragments only when appropriate. Do not skip an English item or vehicle name because another field of the same asset contains Chinese.";
		string userContent = JsonConvert.SerializeObject(requests.Select((WorkshopTranslationRequest workshopTranslationRequest) => new
		{
			id = workshopTranslationRequest.Id,
			field = workshopTranslationRequest.Field.ToString(),
			text = workshopTranslationRequest.SourceText
		}));
		JObject body = BuildLargeModelBody(settings, instruction, userContent);
		string endpoint = NormalizeLargeModelEndpoint(settings);
		for (int attempt = 0; attempt <= Math.Max(0, Math.Min(5, settings.MaxRetries)); attempt++)
		{
			try
			{
				using HttpRequestMessage request = BuildLargeModelRequest(endpoint, body.ToString(Formatting.None), settings);
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, Math.Min(120, settings.TimeoutSeconds))));
				using HttpResponseMessage response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
				string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (response.IsSuccessStatusCode)
				{
					return ParseLargeModelResponse(text, requests, settings.ApiProtocol);
				}
				if (!IsRetryable(response.StatusCode) || attempt >= Math.Max(0, Math.Min(5, settings.MaxRetries)))
				{
					return WorkshopTranslationBatchResult.Failed($"large-model API returned HTTP {(int)response.StatusCode}: {TruncateError(text, settings.ApiKey)}");
				}
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				if (attempt >= settings.MaxRetries)
				{
					return WorkshopTranslationBatchResult.Failed("large-model request timed out");
				}
			}
			catch (HttpRequestException ex2)
			{
				return WorkshopTranslationBatchResult.Failed("large-model request failed: " + ex2.Message);
			}
			catch (WebException ex3)
			{
				if (attempt >= Math.Max(0, Math.Min(5, settings.MaxRetries)))
				{
					return WorkshopTranslationBatchResult.Failed("large-model network timeout/cancel: " + ex3.Message);
				}
			}
			await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return WorkshopTranslationBatchResult.Failed("large-model translation failed");
	}

	private static JObject BuildLargeModelBody(MachineTranslationSettings settings, string instruction, string userContent)
	{
		if (string.Equals(settings.ApiProtocol, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			return new JObject
			{
				["model"] = settings.Model,
				["max_tokens"] = 4096,
				["system"] = instruction,
				["messages"] = new JArray
				{
					new JObject
					{
						["role"] = "user",
						["content"] = userContent
					}
				}
			};
		}
		if (string.Equals(settings.ApiProtocol, "Gemini", StringComparison.OrdinalIgnoreCase))
		{
			return new JObject
			{
				["contents"] = new JArray
				{
					new JObject
					{
						["role"] = "user",
						["parts"] = new JArray
						{
							new JObject { ["text"] = instruction + "\n" + userContent }
						}
					}
				},
				["generationConfig"] = new JObject { ["responseMimeType"] = "application/json" }
			};
		}
		JObject jObject = new JObject
		{
			["model"] = settings.Model,
			["temperature"] = 0,
			["stream"] = false,
			["messages"] = new JArray
			{
				new JObject
				{
					["role"] = "system",
					["content"] = instruction
				},
				new JObject
				{
					["role"] = "user",
					["content"] = userContent
				}
			}
		};
		if (string.Equals(settings.ApiProtocol, "DeepSeek", StringComparison.OrdinalIgnoreCase))
		{
			jObject["thinking"] = new JObject { ["type"] = "disabled" };
			jObject["response_format"] = new JObject { ["type"] = "json_object" };
			jObject["max_tokens"] = 4096;
		}
		return jObject;
	}

	private static HttpRequestMessage BuildLargeModelRequest(string endpoint, string json, MachineTranslationSettings settings)
	{
		HttpRequestMessage httpRequestMessage = JsonRequest(endpoint, json);
		string nextApiKey = settings.GetNextApiKey();
		if (string.Equals(settings.ApiProtocol, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation("x-api-key", nextApiKey);
			httpRequestMessage.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
		}
		else if (string.Equals(settings.ApiProtocol, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation("api-key", nextApiKey);
		}
		else if (!string.IsNullOrWhiteSpace(nextApiKey))
		{
			httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nextApiKey);
		}
		return httpRequestMessage;
	}

	private static WorkshopTranslationBatchResult ParseLargeModelResponse(string body, IReadOnlyList<WorkshopTranslationRequest> requests, string provider)
	{
		try
		{
			JObject jObject = null;
			string content = null;
			try
			{
				jObject = JObject.Parse(StripCodeFence(body));
				content = ExtractProviderContent(jObject, provider);
			}
			catch (JsonException)
			{
				if (!TryExtractSseContent(body, out content))
				{
					return WorkshopTranslationBatchResult.Failed("large-model response JSON/SSE was invalid", WorkshopTranslationErrorKind.ResponseFormat);
				}
			}
			content = RemoveThinkingBlock(content);
			if (!((string.IsNullOrWhiteSpace(content) ? jObject : JObject.Parse(StripCodeFence(content)))?["translations"] is JArray jArray) || jArray.Count != requests.Count)
			{
				return WorkshopTranslationBatchResult.Failed("large-model response count does not match request", WorkshopTranslationErrorKind.ResponseFormat);
			}
			Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (JToken item in jArray)
			{
				string text = item["id"]?.Value<string>();
				string value = item["translation"]?.Value<string>();
				if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(value) || !result.TryAdd(text, value))
				{
					return WorkshopTranslationBatchResult.Failed("large-model response contains an invalid id or translation", WorkshopTranslationErrorKind.ResponseFormat);
				}
			}
			if (requests.Any((WorkshopTranslationRequest request) => !result.ContainsKey(request.Id)))
			{
				return WorkshopTranslationBatchResult.Failed("large-model response ids do not align with request", WorkshopTranslationErrorKind.ResponseFormat);
			}
			return new WorkshopTranslationBatchResult
			{
				Success = true,
				Translations = result
			};
		}
		catch (Exception ex2)
		{
			return WorkshopTranslationBatchResult.Failed("large-model response JSON is invalid: " + ex2.Message, WorkshopTranslationErrorKind.ResponseFormat);
		}
	}

	private static string ExtractProviderContent(JObject envelope, string provider)
	{
		if (envelope == null)
		{
			return null;
		}
		if (!string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			if (!string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
			{
				if (!string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
				{
					return envelope.SelectToken("choices[0].message.content")?.Value<string>();
				}
				return envelope.SelectToken("message.content")?.Value<string>();
			}
			return envelope.SelectToken("candidates[0].content.parts[0].text")?.Value<string>();
		}
		return envelope.SelectToken("content[0].text")?.Value<string>();
	}

	private static bool TryExtractSseContent(string body, out string content)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = (body ?? string.Empty).Split(new string[3] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (!text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string text2 = text.Substring(5).Trim();
			if (text2.Length == 0 || string.Equals(text2, "[DONE]", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			try
			{
				JObject jObject = JObject.Parse(text2);
				string value = jObject.SelectToken("choices[0].delta.content")?.Value<string>() ?? jObject.SelectToken("choices[0].message.content")?.Value<string>();
				if (!string.IsNullOrEmpty(value))
				{
					stringBuilder.Append(value);
				}
			}
			catch (JsonException)
			{
			}
		}
		content = stringBuilder.ToString();
		return content.Length > 0;
	}

	private static string RemoveThinkingBlock(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return content;
		}
		int num = content.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return content.Trim();
		}
		return content.Substring(num + "</think>".Length).Trim();
	}

	private static string NormalizeLargeModelEndpoint(MachineTranslationSettings settings)
	{
		string text = settings.Endpoint.TrimEnd('/');
		if (string.Equals(settings.ApiProtocol, "Anthropic", StringComparison.OrdinalIgnoreCase))
		{
			if (!text.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
			{
				return text + "/messages";
			}
			return text;
		}
		if (string.Equals(settings.ApiProtocol, "Gemini", StringComparison.OrdinalIgnoreCase))
		{
			if (!text.Contains(":generateContent", StringComparison.OrdinalIgnoreCase))
			{
				return text + "/models/" + Uri.EscapeDataString(settings.Model) + ":generateContent";
			}
			return text;
		}
		if (string.Equals(settings.ApiProtocol, "Ollama", StringComparison.OrdinalIgnoreCase))
		{
			if (!text.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
			{
				return text + "/api/chat";
			}
			return text;
		}
		if (string.Equals(settings.ApiProtocol, "AzureOpenAI", StringComparison.OrdinalIgnoreCase) && !text.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			return text + "/openai/deployments/" + Uri.EscapeDataString(settings.Model) + "/chat/completions?api-version=2024-10-21";
		}
		if (!text.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
		{
			return text + "/chat/completions";
		}
		return text;
	}

	private static HttpRequestMessage JsonRequest(string endpoint, string json)
	{
		return new HttpRequestMessage(HttpMethod.Post, endpoint)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};
	}

	private static string TruncateError(string value, string secret)
	{
		string text = value ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(secret))
		{
			text = text.Replace(secret, "***", StringComparison.Ordinal);
		}
		if (text.Length > 800)
		{
			return text.Substring(0, 800);
		}
		return text;
	}

	private static string StripCodeFence(string value)
	{
		string text = value?.Trim() ?? string.Empty;
		if (!text.StartsWith("```", StringComparison.Ordinal))
		{
			return text;
		}
		int num = text.IndexOf('\n');
		int num2 = text.LastIndexOf("```", StringComparison.Ordinal);
		if (num < 0 || num2 <= num)
		{
			return text;
		}
		return text.Substring(num + 1, num2 - num - 1).Trim();
	}

	private static HttpRequestMessage BuildRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		if (string.Equals(settings.ApiProtocol, "TencentTmt", StringComparison.OrdinalIgnoreCase))
		{
			return BuildTencentRequest(request, settings);
		}
		if (string.Equals(settings.ApiProtocol, "BaiduTranslate", StringComparison.OrdinalIgnoreCase))
		{
			return BuildBaiduRequest(request, settings);
		}
		if (string.Equals(settings.ApiProtocol, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
		{
			return BuildGoogleRequest(request, settings);
		}
		if (string.Equals(settings.ApiProtocol, "MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
		{
			return BuildMicrosoftRequest(request, settings);
		}
		if (string.Equals(settings.ApiProtocol, "DeepL", StringComparison.OrdinalIgnoreCase))
		{
			return BuildDeepLRequest(request, settings);
		}
		return BuildYoudaoRequest(request, settings);
	}

	private static HttpRequestMessage BuildGoogleRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		return JsonRequest(settings.Endpoint.TrimEnd('/') + (settings.Endpoint.Contains("?") ? "&" : "?") + "key=" + Uri.EscapeDataString(settings.ApiKey), new
		{
			q = new string[1] { request.SourceText },
			source = settings.SourceLanguage,
			target = settings.TargetLanguage,
			format = "text"
		});
	}

	private static HttpRequestMessage BuildMicrosoftRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		HttpRequestMessage httpRequestMessage = JsonRequest(settings.Endpoint.TrimEnd('/') + "/translate?api-version=3.0&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage), new[]
		{
			new
			{
				Text = request.SourceText
			}
		});
		httpRequestMessage.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", settings.ApiKey);
		if (!string.IsNullOrWhiteSpace(settings.Region))
		{
			httpRequestMessage.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", settings.Region);
		}
		return httpRequestMessage;
	}

	private static HttpRequestMessage BuildDeepLRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		HttpRequestMessage httpRequestMessage = JsonRequest(settings.Endpoint, new
		{
			text = new string[1] { request.SourceText },
			source_lang = settings.SourceLanguage,
			target_lang = settings.TargetLanguage
		});
		httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + settings.ApiKey);
		return httpRequestMessage;
	}

	private static HttpRequestMessage BuildBaiduRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		string text = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
		string text2 = Md5(settings.ApiKey + request.SourceText + text + settings.SecretKey);
		string requestUri = settings.Endpoint + "?q=" + Uri.EscapeDataString(request.SourceText) + "&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage) + "&appid=" + Uri.EscapeDataString(settings.ApiKey) + "&salt=" + text + "&sign=" + text2;
		return new HttpRequestMessage(HttpMethod.Get, requestUri);
	}

	private static HttpRequestMessage BuildYoudaoRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		string text = Guid.NewGuid().ToString("N");
		string text2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
		string text3 = Sha256(settings.ApiKey + Truncate(request.SourceText) + text + text2 + settings.SecretKey);
		string requestUri = settings.Endpoint + "?q=" + Uri.EscapeDataString(request.SourceText) + "&from=" + Uri.EscapeDataString(settings.SourceLanguage) + "&to=" + Uri.EscapeDataString(settings.TargetLanguage) + "&appKey=" + Uri.EscapeDataString(settings.ApiKey) + "&salt=" + text + "&sign=" + text3 + "&signType=v3&curtime=" + text2;
		return new HttpRequestMessage(HttpMethod.Get, requestUri);
	}

	private static HttpRequestMessage BuildTencentRequest(WorkshopTranslationRequest request, MachineTranslationSettings settings)
	{
		string host = new Uri(settings.Endpoint).Host;
		long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string text = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.ToString("yyyy-MM-dd");
		string text2 = JsonConvert.SerializeObject(new
		{
			SourceText = request.SourceText,
			Source = settings.SourceLanguage,
			Target = settings.TargetLanguage,
			ProjectId = ParseProjectId(settings.ProjectId)
		});
		string text3 = Sha256(text2);
		string text4 = "content-type:application/json\nhost:" + host + "\n";
		string text5 = "content-type;host";
		string value = "POST\n/\n\n" + text4 + "\n" + text5 + "\n" + text3;
		string text6 = text + "/tmt/tc3_request";
		string value2 = "TC3-HMAC-SHA256\n" + seconds + "\n" + text6 + "\n" + Sha256(value);
		string text7 = HmacHex(Hmac(Hmac(Hmac("TC3" + settings.SecretKey, text), "tmt"), "tc3_request"), value2);
		HttpRequestMessage httpRequestMessage = JsonRequest(settings.Endpoint, text2);
		httpRequestMessage.Headers.Host = host;
		httpRequestMessage.Headers.TryAddWithoutValidation("X-TC-Action", "TextTranslate");
		httpRequestMessage.Headers.TryAddWithoutValidation("X-TC-Version", "2018-03-21");
		httpRequestMessage.Headers.TryAddWithoutValidation("X-TC-Timestamp", seconds.ToString());
		httpRequestMessage.Headers.TryAddWithoutValidation("X-TC-Region", settings.Region ?? string.Empty);
		httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", "TC3-HMAC-SHA256 Credential=" + settings.ApiKey + "/" + text6 + ", SignedHeaders=" + text5 + ", Signature=" + text7);
		return httpRequestMessage;
	}

	private static HttpRequestMessage JsonRequest(string endpoint, object body)
	{
		return new HttpRequestMessage(HttpMethod.Post, endpoint)
		{
			Content = new StringContent((body is string text) ? text : JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
		};
	}

	internal static WorkshopTranslationBatchResult ParseResponse(string body, IReadOnlyList<WorkshopTranslationRequest> requests)
	{
		return ParseLargeModelResponse(body, requests, "OpenAICompatible");
	}

	private static string ParseSingleTranslation(string body, string provider)
	{
		JToken jToken = JToken.Parse(body);
		if (string.Equals(provider, "TencentTmt", StringComparison.OrdinalIgnoreCase))
		{
			return jToken.SelectToken("Response.TargetText")?.Value<string>();
		}
		if (string.Equals(provider, "BaiduTranslate", StringComparison.OrdinalIgnoreCase))
		{
			return jToken.SelectToken("trans_result[0].dst")?.Value<string>();
		}
		if (string.Equals(provider, "GoogleCloud", StringComparison.OrdinalIgnoreCase))
		{
			return jToken.SelectToken("data.translations[0].translatedText")?.Value<string>();
		}
		if (string.Equals(provider, "MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
		{
			return jToken.SelectToken("[0].translations[0].text")?.Value<string>();
		}
		if (string.Equals(provider, "DeepL", StringComparison.OrdinalIgnoreCase))
		{
			return jToken.SelectToken("translations[0].text")?.Value<string>();
		}
		object obj = jToken.SelectToken("translation")?.Value<string>();
		if (obj == null)
		{
			JToken jToken2 = jToken.SelectToken("translation[0]");
			if (jToken2 == null)
			{
				return null;
			}
			obj = jToken2.Value<string>();
		}
		return (string)obj;
	}

	private static int ParseProjectId(string value)
	{
		if (!int.TryParse(value, out var result))
		{
			return 0;
		}
		return result;
	}

	private static string Truncate(string value)
	{
		if (value.Length > 20)
		{
			return value.Substring(0, 10) + value.Length + value.Substring(value.Length - 10, 10);
		}
		return value;
	}

	private static string Md5(string value)
	{
		return Hex(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(value)));
	}

	private static string Sha256(string value)
	{
		return Hex(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(value)));
	}

	private static byte[] Hmac(string key, string value)
	{
		return Hmac(Encoding.UTF8.GetBytes(key), value);
	}

	private static byte[] Hmac(byte[] key, string value)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256(key);
		return hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(value));
	}

	private static string HmacHex(byte[] key, string value)
	{
		return Hex(Hmac(key, value));
	}

	private static string Hex(byte[] bytes)
	{
		return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
	}
}
