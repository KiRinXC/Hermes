using System.IO;
using Hermes.Windows.Shell;

namespace Hermes.Tests.Shell;

public static class SettingsWindowOptionTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("settings option labels preserve storage values", LabelsPreserveStorageValues);
        suite.Add("settings window keeps entrance transform off Window", WindowKeepsEntranceTransformOffWindow);
        suite.Add("settings window palette switches light and dark surfaces", PaletteSwitchesLightAndDarkSurfaces);
        suite.Add("settings window uses local control center input styles", WindowUsesLocalControlCenterInputStyles);
        suite.Add("settings window replaces brush resources instead of mutating frozen brushes", WindowReplacesBrushResources);
        suite.Add("settings window keeps actions in contextual panels", WindowKeepsActionsInContextualPanels);
        suite.Add("settings window styles use dynamic brushes for local theme switching", WindowStylesUseDynamicBrushes);
        suite.Add("settings window separates header and tabs", WindowSeparatesHeaderAndTabs);
        suite.Add("settings window uses local light readable toggles and sliders", WindowUsesLocalLightReadableTogglesAndSliders);
        suite.Add("settings window uses manual model input and editable prompt", WindowUsesManualModelInputAndEditablePrompt);
    }

    private static void LabelsPreserveStorageValues()
    {
        var natural = SettingsWindowOptions.TranslationStyles.Single(option => option.Value == "natural");
        var dark = SettingsWindowOptions.Themes.Single(option => option.Value == "Dark");

        TestAssert.Equal("自然流利 (Natural)", natural.Label);
        TestAssert.Equal("深色模式", dark.Label);
        TestAssert.Equal("natural", natural.Value);
        TestAssert.Equal("Dark", dark.Value);
        TestAssert.Equal(natural.Label, natural.ToString());
    }

    private static void WindowKeepsEntranceTransformOffWindow()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.False(xaml.Contains("<Window.RenderTransform>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"RootShellScaleTransform\"", StringComparison.Ordinal));
    }

    private static void PaletteSwitchesLightAndDarkSurfaces()
    {
        var light = SettingsWindowThemePalettes.For("Light");
        var dark = SettingsWindowThemePalettes.For("Dark");

        TestAssert.Equal("#F4F5F5F7", light.Shell.ToString());
        TestAssert.Equal("#F20A0A0C", dark.Shell.ToString());
        TestAssert.Equal("#FF1D1D1F", light.TextPrimary.ToString());
        TestAssert.Equal("#FFFFFFFF", dark.TextPrimary.ToString());
    }

    private static void WindowUsesLocalControlCenterInputStyles()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.False(xaml.Contains("众神的语言信使", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.TextBox\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.PasswordBox\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.ComboBox\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Value=\"{DynamicResource Settings.ControlBrush}\"", StringComparison.Ordinal));
    }

    private static void WindowReplacesBrushResources()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(code.Contains("Resources[resourceKey] = new SolidColorBrush(color);", StringComparison.Ordinal));
        TestAssert.False(code.Contains("brush.Color = color;", StringComparison.Ordinal));
    }

    private static void WindowKeepsActionsInContextualPanels()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.False(xaml.Contains("全局控制中心", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Header=\"快捷键\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"HeaderHotkeyButton\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("PreviewKeyDown=\"HotkeyRecorder_PreviewKeyDown\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"TestButton\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"AdvancedClearHistoryButton\"", StringComparison.Ordinal));

        var footerStart = xaml.IndexOf("<Border Grid.Row=\"4\"", StringComparison.Ordinal);
        TestAssert.True(footerStart >= 0);
        var footer = xaml[footerStart..];
        TestAssert.False(footer.Contains("x:Name=\"TestButton\"", StringComparison.Ordinal));
        TestAssert.False(footer.Contains("x:Name=\"ClearHistoryButton\"", StringComparison.Ordinal));
    }

    private static void WindowStylesUseDynamicBrushes()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(xaml.Contains("{DynamicResource Settings.ShellBrush}", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("{DynamicResource Settings.TextPrimaryBrush}", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("{DynamicResource Settings.ControlBrush}", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("{StaticResource Settings.TextPrimaryBrush}", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("{StaticResource Settings.ControlBrush}", StringComparison.Ordinal));
    }

    private static void WindowSeparatesHeaderAndTabs()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(xaml.Contains("<RowDefinition Height=\"42\"/>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Margin=\"24,8,24,0\"", StringComparison.Ordinal));
    }

    private static void WindowUsesLocalLightReadableTogglesAndSliders()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var light = SettingsWindowThemePalettes.For("Light");

        TestAssert.True(xaml.Contains("x:Key=\"Settings.ToggleSwitch\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Style=\"{StaticResource Toggle.Switch}\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("{DynamicResource Settings.ToggleTrackBrush}", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("{DynamicResource Settings.SliderTrackBrush}", StringComparison.Ordinal));
        TestAssert.Equal("#FFE5E5EA", light.ToggleTrack.ToString());
        TestAssert.Equal("#FFC7C7CC", light.SliderTrack.ToString());
    }

    private static void WindowUsesManualModelInputAndEditablePrompt()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("x:Name=\"ModelText\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"ModelCombo\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"RefreshModelsButton\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Click=\"RefreshModels_Click\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PromptText\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("目标语言", StringComparison.Ordinal));
        TestAssert.False(code.Contains("ListModelsAsync", StringComparison.Ordinal));
        TestAssert.False(code.Contains("RefreshModelsButton", StringComparison.Ordinal));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}
