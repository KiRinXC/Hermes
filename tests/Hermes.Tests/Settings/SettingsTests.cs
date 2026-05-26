using Hermes.Windows.Settings;

namespace Hermes.Tests.Settings;

public static class SettingsTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("settings defaults", SettingsDefaults);
    }

    private static void SettingsDefaults()
    {
        var settings = new AppSettings();
        TestAssert.Equal("https://api.openai.com/v1", settings.Api.BaseUrl);
        TestAssert.Equal("gpt-4.1-mini", settings.Api.Model);
        TestAssert.Contains("专业英文到简体中文翻译引擎", settings.Translation.SystemPrompt);
        TestAssert.Equal("Ctrl+Alt+E", settings.Triggers.Hotkey);
        TestAssert.False(settings.Privacy.SaveHistory);
    }
}
