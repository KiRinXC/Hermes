using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;

namespace Hermes.Windows.Translation;

public sealed class OpenAiTranslationService : ITranslationService
{
    public const string DefaultBaseUrl = "https://api.openai.com/v1";
    public const string DefaultModel = "gpt-4.1-mini";

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
        var settings = _settingsService.Current;
        return TranslateAsync(
            new TranslationRequest(
                "Connection test.",
                settings.Translation.Style,
                settings.Translation.TargetLanguage,
                settings.Translation.PreserveFormatting,
                settings.Translation.SystemPrompt),
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
            var baseUrl = string.IsNullOrWhiteSpace(settings.Api.OpenAi.BaseUrl) ? DefaultBaseUrl : settings.Api.OpenAi.BaseUrl;
            var model = string.IsNullOrWhiteSpace(settings.Api.OpenAi.Model) ? DefaultModel : settings.Api.OpenAi.Model;
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildResponsesUri(baseUrl));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(model, request), JsonOptions),
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

    public async Task<TranslationResult> TranslateStreamAsync(
        TranslationRequest request,
        Func<TranslationStreamEvent, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        var settings = _settingsService.Current;
        var apiKey = await _secretStorage.GetApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var result = TranslationResult.Fail(TranslationErrorKind.MissingApiKey, "请先在设置中填写 API Key。");
            await onEvent(TranslationStreamEvent.Failed(result), cancellationToken);
            return result;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var baseUrl = string.IsNullOrWhiteSpace(settings.Api.OpenAi.BaseUrl) ? DefaultBaseUrl : settings.Api.OpenAi.BaseUrl;
            var model = string.IsNullOrWhiteSpace(settings.Api.OpenAi.Model) ? DefaultModel : settings.Api.OpenAi.Model;
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildResponsesUri(baseUrl));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(CreatePayload(model, request, stream: true), JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                stopwatch.Stop();
                _logger.Warning($"Streaming translation API completed in {stopwatch.ElapsedMilliseconds} ms with {(int)response.StatusCode} {response.StatusCode}.");
                var result = MapFailure(response.StatusCode, body);
                await onEvent(TranslationStreamEvent.Failed(result), timeoutCts.Token);
                return result;
            }

            var accumulatedText = new StringBuilder();
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                var message = await ReadSseMessageAsync(reader, timeoutCts.Token);
                if (message is null)
                {
                    break;
                }

                var streamEvent = ParseStreamingEvent(message.EventName, message.Data, accumulatedText);
                if (streamEvent is null)
                {
                    continue;
                }

                if (streamEvent.Kind == TranslationStreamEventKind.Failed)
                {
                    var result = streamEvent.Result ?? TranslationResult.Fail(TranslationErrorKind.Unknown, "流式翻译失败，请稍后重试。");
                    await onEvent(TranslationStreamEvent.Failed(result), timeoutCts.Token);
                    return result;
                }

                if (streamEvent.Kind == TranslationStreamEventKind.Completed)
                {
                    break;
                }

                await onEvent(streamEvent, timeoutCts.Token);
            }

            var translatedText = accumulatedText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                var result = TranslationResult.Fail(TranslationErrorKind.EmptyResponse, "API 返回为空，请稍后重试。");
                await onEvent(TranslationStreamEvent.Failed(result), timeoutCts.Token);
                return result;
            }

            stopwatch.Stop();
            _logger.Info($"Streaming translation API completed in {stopwatch.ElapsedMilliseconds} ms.");
            var completed = TranslationStreamEvent.Completed(translatedText);
            await onEvent(completed, timeoutCts.Token);
            return completed.Result!;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TranslationResult.Fail(TranslationErrorKind.Cancelled, "翻译已取消。");
        }
        catch (OperationCanceledException)
        {
            var result = TranslationResult.Fail(TranslationErrorKind.Timeout, "网络请求超时，请稍后重试。");
            await onEvent(TranslationStreamEvent.Failed(result), cancellationToken);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.Warning($"Streaming translation network failure. {ex.Message}");
            var result = TranslationResult.Fail(TranslationErrorKind.Network, "网络不可用或无法连接到 API。");
            await onEvent(TranslationStreamEvent.Failed(result), cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected streaming translation failure.", ex);
            var result = TranslationResult.Fail(TranslationErrorKind.Unknown, "翻译失败，请稍后重试。");
            await onEvent(TranslationStreamEvent.Failed(result), cancellationToken);
            return result;
        }
    }

    internal static object CreatePayload(string model, TranslationRequest request)
    {
        return CreatePayload(model, request, stream: false);
    }

    internal static object CreatePayload(string model, TranslationRequest request, bool stream)
    {
        return new
        {
            model,
            instructions = TranslationPromptBuilder.BuildInstructions(request),
            input = TranslationPromptBuilder.BuildInput(request.SourceText, request.Mode),
            stream
        };
    }

    internal static TranslationStreamEvent? ParseStreamingEvent(
        string eventName,
        string data,
        StringBuilder accumulatedText)
    {
        if (string.Equals(data.Trim(), "[DONE]", StringComparison.Ordinal))
        {
            return TranslationStreamEvent.Completed(accumulatedText.ToString());
        }

        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            var type = !string.IsNullOrWhiteSpace(eventName)
                ? eventName
                : root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;

            if (string.Equals(type, "response.output_text.delta", StringComparison.Ordinal)
                && root.TryGetProperty("delta", out var deltaElement)
                && deltaElement.ValueKind == JsonValueKind.String)
            {
                var delta = deltaElement.GetString();
                if (string.IsNullOrEmpty(delta))
                {
                    return null;
                }

                accumulatedText.Append(delta);
                return TranslationStreamEvent.Delta(delta, accumulatedText.ToString());
            }

            if (string.Equals(type, "response.output_text.done", StringComparison.Ordinal)
                && root.TryGetProperty("text", out var textElement)
                && textElement.ValueKind == JsonValueKind.String)
            {
                accumulatedText.Clear();
                accumulatedText.Append(textElement.GetString());
                return null;
            }

            if (string.Equals(type, "response.completed", StringComparison.Ordinal))
            {
                return TranslationStreamEvent.Completed(accumulatedText.ToString());
            }

            if (string.Equals(type, "error", StringComparison.Ordinal)
                || string.Equals(type, "response.failed", StringComparison.Ordinal)
                || root.TryGetProperty("error", out _))
            {
                var message = ExtractProviderError(data) ?? "流式翻译失败，请稍后重试。";
                return TranslationStreamEvent.Failed(TranslationResult.Fail(TranslationErrorKind.Unknown, message));
            }
        }
        catch (JsonException)
        {
            return TranslationStreamEvent.Failed(
                TranslationResult.Fail(TranslationErrorKind.InvalidRequest, "API 返回的流式响应无效，请稍后重试。"));
        }

        return null;
    }

    private static async Task<SseMessage?> ReadSseMessageAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var eventName = string.Empty;
        var data = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return data.Length == 0 ? null : new SseMessage(eventName, data.ToString());
            }

            if (line.Length == 0)
            {
                if (data.Length == 0)
                {
                    continue;
                }

                return new SseMessage(eventName, data.ToString());
            }

            if (line.StartsWith(':'))
            {
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0)
                {
                    data.Append('\n');
                }

                data.Append(line["data:".Length..].TrimStart());
            }
        }
    }

    internal static Uri BuildResponsesUri(string baseUrl)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl)
            ? DefaultBaseUrl
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

            if (document.RootElement.TryGetProperty("response", out var response)
                && response.ValueKind == JsonValueKind.Object
                && response.TryGetProperty("error", out var responseError)
                && responseError.ValueKind == JsonValueKind.Object
                && responseError.TryGetProperty("message", out var responseMessage)
                && responseMessage.ValueKind == JsonValueKind.String)
            {
                return responseMessage.GetString();
            }
        }
        catch
        {
            return Redactor.SummarizeText(body);
        }

        return null;
    }

    private sealed record SseMessage(string EventName, string Data);
}
