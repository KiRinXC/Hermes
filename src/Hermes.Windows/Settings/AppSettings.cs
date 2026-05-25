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
    public string Provider { get; set; } = "OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4.1-mini";
}

public sealed class TranslationSettings
{
    public string SourceLanguage { get; set; } = "auto";

    public string TargetLanguage { get; set; } = "Simplified Chinese";

    public string Style { get; set; } = "natural";

    public int MaxCharacters { get; set; } = 5000;

    public bool PreserveFormatting { get; set; } = true;
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

    public double PopupWidth { get; set; } = 360;

    public double FontSize { get; set; } = 14;

    public double Opacity { get; set; } = 0.98;

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
