using Hermes.Windows.Settings;
using Hermes.Windows.Translation;

namespace Hermes.Tests.Settings;

public static class SettingsTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("settings defaults", SettingsDefaults);
        suite.Add("settings compatibility initializes nested API blocks", CompatibilityInitializesNestedApiBlocks);
        suite.Add("settings compatibility preserves legacy openai details without enabling translation", CompatibilityPreservesLegacyOpenAiDetailsWithoutEnablingTranslation);
        suite.Add("settings compatibility keeps transmart translation by default", CompatibilityKeepsTransmartTranslationDefault);
        suite.Add("settings compatibility does not force openai when migration is skipped", CompatibilitySkipsLegacyProviderMigrationWhenRequested);
    }

    private static void SettingsDefaults()
    {
        var settings = new AppSettings();
        TestAssert.Equal("Transmart", settings.Api.Provider);
        TestAssert.Equal(TransmartTranslationService.DefaultBaseUrl, settings.Api.BaseUrl);
        TestAssert.Equal(TransmartTranslationService.DefaultModelCategory, settings.Api.Model);
        TestAssert.Contains("专业英文到简体中文翻译引擎", settings.Translation.SystemPrompt);
        TestAssert.Contains("计算机体系结构", settings.Translation.ExplanationPreference);
        TestAssert.Equal("Ctrl+Alt+E", settings.Triggers.Hotkey);
        TestAssert.Equal("DarkBorderLightFill", settings.Ui.FloatingButtonStyle);
        TestAssert.Equal("Medium", settings.Ui.FloatingButtonSize);
        TestAssert.Equal(16d, settings.Ui.FontSize);
        TestAssert.Equal(260d, settings.Ui.PopupHeight);
        TestAssert.Equal(800d, settings.Ui.SettingsWindowWidth);
        TestAssert.Equal(600d, settings.Ui.SettingsWindowHeight);
        TestAssert.False(settings.Privacy.SaveHistory);
    }

    private static void CompatibilityInitializesNestedApiBlocks()
    {
        var settings = new AppSettings
        {
            Api = new ApiSettings
            {
                Transmart = null!,
                OpenAi = null!
            }
        };

        SettingsCompatibility.Normalize(settings);

        TestAssert.Equal(TransmartTranslationService.DefaultBaseUrl, settings.Api.Transmart.BaseUrl);
        TestAssert.Equal(TransmartTranslationService.DefaultModelCategory, settings.Api.Transmart.Model);
        TestAssert.Equal(OpenAiTranslationService.DefaultBaseUrl, settings.Api.OpenAi.BaseUrl);
        TestAssert.Equal(OpenAiTranslationService.DefaultModel, settings.Api.OpenAi.Model);
    }

    private static void CompatibilityPreservesLegacyOpenAiDetailsWithoutEnablingTranslation()
    {
        var settings = new AppSettings
        {
            Api = new ApiSettings
            {
                Provider = "OpenAI",
                BaseUrl = "https://api.example.com/v1",
                Model = "gpt-custom",
                UseOpenAiForTranslation = false,
                Transmart = new TransmartApiSettings(),
                OpenAi = new OpenAiApiSettings()
            }
        };

        SettingsCompatibility.Normalize(settings);

        TestAssert.False(settings.Api.UseOpenAiForTranslation);
        TestAssert.Equal("Transmart", settings.Api.Provider);
        TestAssert.Equal(TransmartTranslationService.DefaultBaseUrl, settings.Api.BaseUrl);
        TestAssert.Equal(TransmartTranslationService.DefaultModelCategory, settings.Api.Model);
        TestAssert.Equal("https://api.example.com/v1", settings.Api.OpenAi.BaseUrl);
        TestAssert.Equal("gpt-custom", settings.Api.OpenAi.Model);
    }

    private static void CompatibilityKeepsTransmartTranslationDefault()
    {
        var settings = new AppSettings
        {
            Api = new ApiSettings
            {
                Provider = "Transmart",
                BaseUrl = "https://transmart.qq.com/api",
                Model = "normal",
                UseOpenAiForTranslation = false,
                Transmart = new TransmartApiSettings(),
                OpenAi = new OpenAiApiSettings()
            }
        };

        SettingsCompatibility.Normalize(settings);

        TestAssert.False(settings.Api.UseOpenAiForTranslation);
        TestAssert.Equal(OpenAiTranslationService.DefaultBaseUrl, settings.Api.OpenAi.BaseUrl);
        TestAssert.Equal(OpenAiTranslationService.DefaultModel, settings.Api.OpenAi.Model);
    }

    private static void CompatibilitySkipsLegacyProviderMigrationWhenRequested()
    {
        var settings = new AppSettings
        {
            Api = new ApiSettings
            {
                Provider = "OpenAI",
                BaseUrl = "https://legacy.example.com/v1",
                Model = "legacy-model",
                UseOpenAiForTranslation = false,
                Transmart = new TransmartApiSettings(),
                OpenAi = new OpenAiApiSettings()
            }
        };

        SettingsCompatibility.Normalize(settings, migrateLegacyProviderConfiguration: false);

        TestAssert.False(settings.Api.UseOpenAiForTranslation);
        TestAssert.Equal(OpenAiTranslationService.DefaultBaseUrl, settings.Api.OpenAi.BaseUrl);
        TestAssert.Equal(OpenAiTranslationService.DefaultModel, settings.Api.OpenAi.Model);
    }
}
