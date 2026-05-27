namespace Hermes.Windows.Translation;

public enum TranslationStreamEventKind
{
    Delta,
    Completed,
    Failed
}

public sealed record TranslationStreamEvent(
    TranslationStreamEventKind Kind,
    string? DeltaText,
    string? CurrentText,
    TranslationResult? Result)
{
    public static TranslationStreamEvent Delta(string deltaText, string currentText) =>
        new(TranslationStreamEventKind.Delta, deltaText, currentText, null);

    public static TranslationStreamEvent Completed(string finalText) =>
        new(TranslationStreamEventKind.Completed, null, finalText, TranslationResult.Ok(finalText));

    public static TranslationStreamEvent Failed(TranslationResult result) =>
        new(TranslationStreamEventKind.Failed, null, null, result);
}
