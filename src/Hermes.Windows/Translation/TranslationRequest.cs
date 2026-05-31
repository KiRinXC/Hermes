namespace Hermes.Windows.Translation;

public sealed record TranslationRequest(
    string SourceText,
    string Style,
    string TargetLanguage,
    bool PreserveFormatting,
    string? SystemPrompt = null,
    TranslationMode Mode = TranslationMode.Translate,
    string? ExplanationPreference = null);
