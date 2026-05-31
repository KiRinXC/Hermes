using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;

namespace Hermes.Windows.Translation;

public sealed class TransmartTranslationService : ITranslationService
{
    public const string DefaultBaseUrl = "https://transmart.qq.com/api";
    public const string DefaultModelCategory = "normal";
    private const string DefaultOrigin = "https://transmart.qq.com";
    private const string DefaultReferer = "https://transmart.qq.com/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly AppLogger _logger;

    public TransmartTranslationService(
        HttpClient httpClient,
        SettingsService settingsService,
        AppLogger logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public Task<TranslationResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Current;
        return TranslateAsync(
            new TranslationRequest(
                "Connection test.",
                settings.Translation.Style,
                settings.Translation.TargetLanguage,
                settings.Translation.PreserveFormatting,
                settings.Translation.SystemPrompt,
                TranslationMode.Translate,
                settings.Translation.ExplanationPreference),
            cancellationToken);
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Mode == TranslationMode.Explain)
        {
            return TranslationResult.Fail(
                TranslationErrorKind.InvalidRequest,
                "当前提供方不支持术语解释，请切换到 OpenAI。");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var settings = _settingsService.Current;
            var baseUrl = string.IsNullOrWhiteSpace(settings.Api.Transmart.BaseUrl) ? DefaultBaseUrl : settings.Api.Transmart.BaseUrl;
            var model = string.IsNullOrWhiteSpace(settings.Api.Transmart.Model) ? DefaultModelCategory : settings.Api.Transmart.Model;
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildImtUri(baseUrl));
            ApplyDefaultHeaders(httpRequest);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(request, model, settings.Translation.SourceLanguage), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                _logger.Warning($"Transmart API completed in {stopwatch.ElapsedMilliseconds} ms with {(int)response.StatusCode} {response.StatusCode}.");
                return MapFailure(response.StatusCode, body);
            }

            var translatedText = ExtractOutputText(body);
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return TranslationResult.Fail(TranslationErrorKind.EmptyResponse, "翻译服务返回为空，请稍后重试。");
            }

            stopwatch.Stop();
            _logger.Info($"Transmart API completed in {stopwatch.ElapsedMilliseconds} ms.");
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
            _logger.Warning($"Transmart network failure. {ex.Message}");
            return TranslationResult.Fail(TranslationErrorKind.Network, "网络不可用或无法连接到翻译服务。");
        }
        catch (JsonException ex)
        {
            _logger.Warning($"Transmart response parse failure. {ex.Message}");
            return TranslationResult.Fail(TranslationErrorKind.InvalidRequest, "翻译服务响应格式异常，请稍后重试。");
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected transmart translation failure.", ex);
            return TranslationResult.Fail(TranslationErrorKind.Unknown, "翻译失败，请稍后重试。");
        }
    }

    public async Task<TranslationResult> TranslateStreamAsync(
        TranslationRequest request,
        Func<TranslationStreamEvent, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        var result = await TranslateAsync(request, cancellationToken);
        if (result.Success && result.TranslatedText is not null)
        {
            await onEvent(TranslationStreamEvent.Completed(result.TranslatedText), cancellationToken);
            return result;
        }

        if (result.ErrorKind != TranslationErrorKind.Cancelled)
        {
            await onEvent(TranslationStreamEvent.Failed(result), cancellationToken);
        }

        return result;
    }

    internal static Uri BuildImtUri(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? DefaultBaseUrl
            : baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/imt", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed, UriKind.Absolute);
        }

        return new Uri($"{trimmed}/imt", UriKind.Absolute);
    }

    internal static string? ExtractOutputText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("header", out var header)
            && header.ValueKind == JsonValueKind.Object
            && header.TryGetProperty("ret_code", out var retCode)
            && retCode.ValueKind == JsonValueKind.String
            && !string.Equals(retCode.GetString(), "succ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!root.TryGetProperty("auto_translation", out var autoTranslation)
            || autoTranslation.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var item in autoTranslation.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                builder.Append(item.GetString());
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static object CreatePayload(TranslationRequest request, string modelCategory, string sourceLanguage)
    {
        var normalizedModelCategory = string.IsNullOrWhiteSpace(modelCategory)
            ? DefaultModelCategory
            : modelCategory.Trim();

        return new
        {
            header = new
            {
                fn = "auto_translation",
                session = string.Empty,
                client_key = GenerateClientKey(),
                user = string.Empty
            },
            type = "plain",
            model_category = normalizedModelCategory,
            text_domain = string.Empty,
            source = new
            {
                lang = NormalizeSourceLanguage(sourceLanguage),
                text_list = new[] { request.SourceText }
            },
            target = new
            {
                lang = NormalizeTargetLanguage(request.TargetLanguage)
            }
        };
    }

    private static string GenerateClientKey()
    {
        return $"browser-chrome-116.0.0-Windows-{Guid.NewGuid()}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static void ApplyDefaultHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Origin", DefaultOrigin);
        request.Headers.TryAddWithoutValidation("Referer", DefaultReferer);
        request.Headers.TryAddWithoutValidation("Host", "transmart.qq.com");
    }

    private static string NormalizeSourceLanguage(string sourceLanguage)
    {
        if (string.IsNullOrWhiteSpace(sourceLanguage)
            || string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return "auto";
        }

        var normalized = sourceLanguage.Trim().ToLowerInvariant();
        if (normalized.StartsWith("en", StringComparison.Ordinal))
        {
            return "en";
        }

        if (normalized.StartsWith("zh", StringComparison.Ordinal)
            || normalized.Contains("chinese", StringComparison.Ordinal))
        {
            return "zh";
        }

        return "auto";
    }

    private static string NormalizeTargetLanguage(string targetLanguage)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            return "zh";
        }

        var normalized = targetLanguage.Trim().ToLowerInvariant();
        if (normalized.StartsWith("en", StringComparison.Ordinal)
            || normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        if (normalized.StartsWith("zh", StringComparison.Ordinal)
            || normalized.Contains("chinese", StringComparison.Ordinal))
        {
            return "zh";
        }

        return "zh";
    }

    private TranslationResult MapFailure(HttpStatusCode statusCode, string body)
    {
        var message = ExtractError(body) ?? "翻译服务请求失败，请稍后重试。";
        _logger.Warning($"Transmart API failure: {(int)statusCode} {statusCode}. {message}");
        return statusCode switch
        {
            HttpStatusCode.BadRequest => TranslationResult.Fail(TranslationErrorKind.InvalidRequest, message),
            HttpStatusCode.RequestTimeout => TranslationResult.Fail(TranslationErrorKind.Timeout, "网络请求超时，请稍后重试。"),
            (HttpStatusCode)429 => TranslationResult.Fail(TranslationErrorKind.RateLimit, "请求过快，请稍后重试。"),
            _ => TranslationResult.Fail(TranslationErrorKind.Unknown, message)
        };
    }

    private static string? ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("header", out var header)
                && header.ValueKind == JsonValueKind.Object
                && header.TryGetProperty("ret_code", out var retCode)
                && retCode.ValueKind == JsonValueKind.String)
            {
                var code = retCode.GetString();
                if (!string.IsNullOrWhiteSpace(code) && !string.Equals(code, "succ", StringComparison.OrdinalIgnoreCase))
                {
                    return $"翻译服务返回错误：{code}";
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
