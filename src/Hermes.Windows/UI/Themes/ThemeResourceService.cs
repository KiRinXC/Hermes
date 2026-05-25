using Microsoft.Win32;

namespace Hermes.Windows.UI.Themes;

public static class ThemeResourceService
{
    private static readonly Uri LightThemeUri = new("UI/Themes/LightTheme.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("UI/Themes/DarkTheme.xaml", UriKind.Relative);

    public static bool Apply(string? requestedTheme)
    {
        var useDark = ShouldUseDarkTheme(requestedTheme);
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is "UI/Themes/LightTheme.xaml" or "UI/Themes/DarkTheme.xaml")
            {
                dictionaries.RemoveAt(i);
            }
        }

        dictionaries.Add(new System.Windows.ResourceDictionary { Source = useDark ? DarkThemeUri : LightThemeUri });
        return useDark;
    }

    public static bool ShouldUseDarkTheme(string? requestedTheme)
    {
        if (string.Equals(requestedTheme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(requestedTheme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
