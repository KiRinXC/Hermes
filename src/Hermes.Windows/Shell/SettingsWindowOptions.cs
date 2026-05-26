namespace Hermes.Windows.Shell;

public sealed record SettingsOption(string Value, string Label)
{
    public override string ToString() => Label;
}

public static class SettingsWindowOptions
{
    public static IReadOnlyList<SettingsOption> Providers { get; } =
    [
        new("OpenAI", "OpenAI"),
        new("OpenAI-compatible", "OpenAI-compatible")
    ];

    public static IReadOnlyList<SettingsOption> TranslationStyles { get; } =
    [
        new("natural", "自然流利 (Natural)"),
        new("literal", "严格直译 (Literal)"),
        new("professional", "学术专业 (Professional)"),
        new("concise", "极简精炼 (Concise)")
    ];

    public static IReadOnlyList<SettingsOption> Themes { get; } =
    [
        new("System", "跟随系统"),
        new("Dark", "深色模式"),
        new("Light", "浅色模式")
    ];
}
