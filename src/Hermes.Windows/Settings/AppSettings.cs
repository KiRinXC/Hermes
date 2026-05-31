namespace Hermes.Windows.Settings;

public sealed class AppSettings
{
    public ApiSettings Api { get; set; } = new();

    public TranslationSettings Translation { get; set; } = new();

    public TriggerSettings Triggers { get; set; } = new();

    public UiSettings Ui { get; set; } = new();

    public PrivacySettings Privacy { get; set; } = new();

    public StartupSettings Startup { get; set; } = new();
}

public sealed class ApiSettings
{
    // Legacy fields retained for backward compatibility with existing settings.json.
    public string Provider { get; set; } = "Transmart";

    public string BaseUrl { get; set; } = Hermes.Windows.Translation.TransmartTranslationService.DefaultBaseUrl;

    public string Model { get; set; } = Hermes.Windows.Translation.TransmartTranslationService.DefaultModelCategory;

    public bool UseOpenAiForTranslation { get; set; }

    public TransmartApiSettings Transmart { get; set; } = new();

    public OpenAiApiSettings OpenAi { get; set; } = new();
}

public sealed class TransmartApiSettings
{
    public string BaseUrl { get; set; } = Hermes.Windows.Translation.TransmartTranslationService.DefaultBaseUrl;

    public string Model { get; set; } = Hermes.Windows.Translation.TransmartTranslationService.DefaultModelCategory;
}

public sealed class OpenAiApiSettings
{
    public string BaseUrl { get; set; } = Hermes.Windows.Translation.OpenAiTranslationService.DefaultBaseUrl;

    public string Model { get; set; } = Hermes.Windows.Translation.OpenAiTranslationService.DefaultModel;
}

public sealed class TranslationSettings
{
    public string SourceLanguage { get; set; } = "auto";

    public string TargetLanguage { get; set; } = "Simplified Chinese";

    public string SystemPrompt { get; set; } = Hermes.Windows.Translation.TranslationPromptBuilder.DefaultSystemPrompt;

    public string Style { get; set; } = "natural";

    public int MaxCharacters { get; set; } = 5000;

    public bool PreserveFormatting { get; set; } = true;

    public string ExplanationPreference { get; set; } = Hermes.Windows.Translation.TranslationPromptBuilder.DefaultExplanationPreference;
}

public sealed class TriggerSettings
{
    public bool AutoShowSelectionButton { get; set; } = true;

    public string Hotkey { get; set; } = "Ctrl+Alt+E";

    public bool PromptAfterCopy { get; set; }

    public List<string> ExcludedApplications { get; set; } = new();

    public List<string> SensitiveApplications { get; set; } = new()
    {
        "1password",
        "bitwarden",
        "keepass",
        "lastpass"
    };
}

public sealed class UiSettings
{
    public string Theme { get; set; } = "System";

    public string FloatingButtonStyle { get; set; } = "DarkBorderLightFill";

    public string FloatingButtonSize { get; set; } = "Medium";

    public double PopupWidth { get; set; } = 360;

    public double PopupHeight { get; set; } = 260;

    public double FontSize { get; set; } = 16;

    public double Opacity { get; set; } = 0.98;

    public double SettingsWindowWidth { get; set; } = 800;

    public double SettingsWindowHeight { get; set; } = 600;

    public bool PinPopupByDefault { get; set; }
}

public sealed class PrivacySettings
{
    public bool SaveHistory { get; set; }

    public bool SaveOriginalText { get; set; }

    public bool LogFullText { get; set; }
}

public sealed class StartupSettings
{
    public bool LaunchAtSignIn { get; set; }
}
