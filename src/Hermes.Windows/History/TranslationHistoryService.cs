using System.IO;
using System.Text.Json;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;

namespace Hermes.Windows.History;

public sealed class TranslationHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SettingsService _settingsService;
    private readonly AppLogger _logger;

    public TranslationHistoryService(SettingsService settingsService, AppLogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task SaveAsync(
        string sourceText,
        string translatedText,
        TranslationMode mode = TranslationMode.Translate,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Current;
        if (!settings.Privacy.SaveHistory)
        {
            return;
        }

        var (provider, model) = ResolveProviderAndModel(settings, mode);
        var records = await LoadAsync(cancellationToken);
        records.Insert(0, new TranslationHistoryRecord(
            DateTimeOffset.Now,
            settings.Privacy.SaveOriginalText ? sourceText : null,
            translatedText,
            provider,
            model));

        AppPaths.EnsureCreated();
        await using var stream = File.Create(AppPaths.HistoryPath);
        await JsonSerializer.SerializeAsync(stream, records.Take(200).ToList(), JsonOptions, cancellationToken);
    }

    public async Task<List<TranslationHistoryRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.HistoryPath))
        {
            return new List<TranslationHistoryRecord>();
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.HistoryPath);
            return await JsonSerializer.DeserializeAsync<List<TranslationHistoryRecord>>(stream, JsonOptions, cancellationToken)
                ?? new List<TranslationHistoryRecord>();
        }
        catch (Exception ex)
        {
            _logger.Warning($"History could not be loaded. {ex.Message}");
            return new List<TranslationHistoryRecord>();
        }
    }

    public Task ClearAsync()
    {
        if (File.Exists(AppPaths.HistoryPath))
        {
            File.Delete(AppPaths.HistoryPath);
        }

        return Task.CompletedTask;
    }

    private static (string Provider, string Model) ResolveProviderAndModel(AppSettings settings, TranslationMode mode)
    {
        if (mode == TranslationMode.Explain)
        {
            return ("OpenAI", settings.Api.OpenAi.Model);
        }

        if (settings.Api.UseOpenAiForTranslation)
        {
            return ("OpenAI", settings.Api.OpenAi.Model);
        }

        return ("Transmart", settings.Api.Transmart.Model);
    }
}
