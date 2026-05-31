namespace Hermes.Windows.Shell;

public sealed record SettingsOption(string Value, string Label)
{
    public override string ToString() => Label;
}

public static class SettingsWindowOptions
{
    public static IReadOnlyList<SettingsOption> Providers { get; } =
    [
        new("Transmart", "Tencent Transmart (Recommended)"),
        new("OpenAI", "OpenAI"),
        new("OpenAI-compatible", "OpenAI-compatible")
    ];

    public static IReadOnlyList<SettingsOption> TranslationStyles { get; } =
    [
        new("natural", "\u81EA\u7136\u6D41\u5229 (Natural)"),
        new("literal", "\u4E25\u683C\u76F4\u8BD1 (Literal)"),
        new("professional", "\u5B66\u672F\u4E13\u4E1A (Professional)"),
        new("concise", "\u6781\u7B80\u7CBE\u70BC (Concise)")
    ];

    public static IReadOnlyList<SettingsOption> Themes { get; } =
    [
        new("System", "\u8DDF\u968F\u7CFB\u7EDF"),
        new("Dark", "\u6DF1\u8272\u6A21\u5F0F"),
        new("Light", "\u6D45\u8272\u6A21\u5F0F")
    ];

    public static IReadOnlyList<SettingsOption> FloatingButtonStyles { get; } =
    [
        new("DarkBorderLightFill", "\u9ED1\u6846\u767D\u5E95"),
        new("LightBorderDarkFill", "\u767D\u6846\u9ED1\u5E95")
    ];

    public static IReadOnlyList<SettingsOption> FloatingButtonSizes { get; } =
    [
        new("ExtraSmall", "\u8D85\u5C0F"),
        new("Small", "\u5C0F"),
        new("Medium", "\u4E2D"),
        new("Large", "\u5927"),
        new("ExtraLarge", "\u8D85\u5927")
    ];
}
