namespace Hermes.Windows.Translation;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);

    Task<TranslationResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
