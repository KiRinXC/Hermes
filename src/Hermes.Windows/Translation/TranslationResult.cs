namespace Hermes.Windows.Translation;

public enum TranslationErrorKind
{
    None,
    MissingApiKey,
    Authentication,
    QuotaOrBilling,
    RateLimit,
    Timeout,
    Network,
    InvalidRequest,
    EmptyResponse,
    Cancelled,
    Unknown
}

public sealed record TranslationResult(
    bool Success,
    string? TranslatedText,
    TranslationErrorKind ErrorKind,
    string? UserMessage)
{
    public static TranslationResult Ok(string text) => new(true, text, TranslationErrorKind.None, null);

    public static TranslationResult Fail(TranslationErrorKind kind, string message) => new(false, null, kind, message);
}
