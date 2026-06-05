using Hermes.Windows.Translation;

namespace Hermes.Windows.Settings;

public static class SettingsCompatibility
{
    public static void Normalize(AppSettings settings, bool migrateLegacyProviderConfiguration = true)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Api ??= new ApiSettings();
        settings.Api.Transmart ??= new TransmartApiSettings();
        settings.Api.OpenAi ??= new OpenAiApiSettings();

        NormalizeTransmart(settings.Api.Transmart);
        NormalizeOpenAi(settings.Api.OpenAi);
        if (migrateLegacyProviderConfiguration)
        {
            MigrateLegacyProviderConfiguration(settings.Api);
        }
    }

    private static void NormalizeTransmart(TransmartApiSettings transmart)
    {
        if (string.IsNullOrWhiteSpace(transmart.BaseUrl))
        {
            transmart.BaseUrl = TransmartTranslationService.DefaultBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(transmart.Model))
        {
            transmart.Model = TransmartTranslationService.DefaultModelCategory;
        }
    }

    private static void NormalizeOpenAi(OpenAiApiSettings openAi)
    {
        if (string.IsNullOrWhiteSpace(openAi.BaseUrl))
        {
            openAi.BaseUrl = OpenAiTranslationService.DefaultBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(openAi.Model))
        {
            openAi.Model = OpenAiTranslationService.DefaultModel;
        }
    }

    private static void MigrateLegacyProviderConfiguration(ApiSettings api)
    {
        var legacyProvider = api.Provider?.Trim();
        var legacyWasOpenAi = string.Equals(legacyProvider, "OpenAI", StringComparison.OrdinalIgnoreCase)
            || string.Equals(legacyProvider, "OpenAI-compatible", StringComparison.OrdinalIgnoreCase);

        if (!legacyWasOpenAi)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(api.BaseUrl))
        {
            api.OpenAi.BaseUrl = api.BaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(api.Model))
        {
            api.OpenAi.Model = api.Model;
        }

        api.Provider = "Transmart";
        api.BaseUrl = TransmartTranslationService.DefaultBaseUrl;
        api.Model = TransmartTranslationService.DefaultModelCategory;
        api.UseOpenAiForTranslation = false;
    }
}
