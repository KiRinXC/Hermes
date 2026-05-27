namespace Hermes.Windows.Translation;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);

    Task<TranslationResult> TranslateStreamAsync(
        TranslationRequest request,
        Func<TranslationStreamEvent, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default);

    Task<TranslationResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
