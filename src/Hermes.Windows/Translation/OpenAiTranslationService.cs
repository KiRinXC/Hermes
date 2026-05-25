using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;

namespace Hermes.Windows.Translation;

public sealed class OpenAiTranslationService : ITranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly ISecretStorageService _secretStorage;
    private readonly AppLogger _logger;

    public OpenAiTranslationService(
        HttpClient httpClient,
        SettingsService settingsService,
        ISecretStorageService secretStorage,
        AppLogger logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _secretStorage = secretStorage;
        _logger = logger;
    }

    public Task<TranslationResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return TranslateAsync(
            new TranslationRequest("Connection test.", "natural", "Simplified Chinese", PreserveFormatting: true),
            cancellationToken);
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Current;
        var apiKey = await _secretStorage.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TranslationResult.Fail(TranslationErrorKind.MissingApiKey, "请先在设置中填写 API Key。");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildResponsesUri(settings.Api.BaseUrl));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(settings.Api.Model, request), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                _logger.Warning($"Translation API completed in {stopwatch.ElapsedMilliseconds} ms with {(int)response.StatusCode} {response.StatusCode}.");
                return MapFailure(response.StatusCode, body);
            }

            var translatedText = ExtractOutputText(body);
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return TranslationResult.Fail(TranslationErrorKind.EmptyResponse, "API 返回为空，请稍后重试。");
            }

            stopwatch.Stop();
            _logger.Info($"Translation API completed in {stopwatch.ElapsedMilliseconds} ms.");
            return TranslationResult.Ok(translatedText.Trim());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TranslationResult.Fail(TranslationErrorKind.Cancelled, "翻译已取消。");
        }
        catch (OperationCanceledException)
        {
            return TranslationResult.Fail(TranslationErrorKind.Timeout, "网络请求超时，请稍后重试。");
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning($"Translation network failure. {ex.Message}");
            return TranslationResult.Fail(TranslationErrorKind.Network, "网络不可用或无法连接到 API。");
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected translation failure.", ex);
            return TranslationResult.Fail(TranslationErrorKind.Unknown, "翻译失败，请稍后重试。");
        }
    }

    internal static object CreatePayload(string model, TranslationRequest request)
    {
        return new
        {
            model,
            instructions = TranslationPromptBuilder.BuildInstructions(request.Style, request.TargetLanguage, request.PreserveFormatting),
            input = TranslationPromptBuilder.BuildInput(request.SourceText)
        };
    }

    internal static Uri BuildResponsesUri(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com/v1"
            : baseUrl.Trim().TrimEnd('/');

        if (trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed, UriKind.Absolute);
        }

        return new Uri($"{trimmed}/responses", UriKind.Absolute);
    }

    internal static string? ExtractOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && contentItem.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private TranslationResult MapFailure(HttpStatusCode statusCode, string body)
    {
        var providerMessage = ExtractProviderError(body);
        _logger.Warning($"Translation API failure: {(int)statusCode} {statusCode}. {providerMessage}");

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                TranslationResult.Fail(TranslationErrorKind.Authentication, "API Key 无效或无权限，请检查设置。"),
            HttpStatusCode.PaymentRequired =>
                TranslationResult.Fail(TranslationErrorKind.QuotaOrBilling, "API 额度或余额不足，请检查账户状态。"),
            (HttpStatusCode)429 =>
                TranslationResult.Fail(TranslationErrorKind.RateLimit, "API 请求过快或达到限额，请稍后重试。"),
            HttpStatusCode.BadRequest =>
                TranslationResult.Fail(TranslationErrorKind.InvalidRequest, providerMessage ?? "API 请求无效，请检查模型和 Base URL。"),
            _ =>
                TranslationResult.Fail(TranslationErrorKind.Unknown, providerMessage ?? "API 请求失败，请稍后重试。")
        };
    }

    private static string? ExtractProviderError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }

                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }
            }
        }
        catch
        {
            return Redactor.SummarizeText(body);
        }

        return null;
    }
}
