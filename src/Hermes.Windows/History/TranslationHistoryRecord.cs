namespace Hermes.Windows.History;

public sealed record TranslationHistoryRecord(
    DateTimeOffset CreatedAt,
    string? SourceText,
    string TranslatedText,
    string Provider,
    string Model);
