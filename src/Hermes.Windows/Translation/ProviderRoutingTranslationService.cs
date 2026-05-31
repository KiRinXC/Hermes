using Hermes.Windows.Settings;

namespace Hermes.Windows.Translation;

public sealed class ProviderRoutingTranslationService : ITranslationService
{
    private readonly ITranslationService _openAiService;
    private readonly ITranslationService _transmartService;
    private readonly SettingsService _settingsService;

    public ProviderRoutingTranslationService(
        ITranslationService openAiService,
        ITranslationService transmartService,
        SettingsService settingsService)
    {
        _openAiService = openAiService;
        _transmartService = transmartService;
        _settingsService = settingsService;
    }

    public Task<TranslationResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return ResolveForMode(TranslationMode.Translate).TestConnectionAsync(cancellationToken);
    }

    public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveForRequest(request).TranslateAsync(request, cancellationToken);
    }

    public Task<TranslationResult> TranslateStreamAsync(
        TranslationRequest request,
        Func<TranslationStreamEvent, CancellationToken, Task> onEvent,
        CancellationToken cancellationToken = default)
    {
        return ResolveForRequest(request).TranslateStreamAsync(request, onEvent, cancellationToken);
    }

    private ITranslationService ResolveForRequest(TranslationRequest request)
    {
        return ResolveForMode(request.Mode);
    }

    private ITranslationService ResolveForMode(TranslationMode mode)
    {
        if (mode == TranslationMode.Explain)
        {
            return _openAiService;
        }

        return _settingsService.Current.Api.UseOpenAiForTranslation
            ? _openAiService
            : _transmartService;
    }
}
