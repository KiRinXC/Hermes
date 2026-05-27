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
        suite.Add("settings theme segment keeps selected capsule visible in light mode", WindowThemeSegmentKeepsLightSelectionVisible);
        suite.Add("settings mouse tab navigation clears automatic toggle focus", WindowMouseTabNavigationClearsAutomaticToggleFocus);
        suite.Add("settings window separates header and tabs", WindowSeparatesHeaderAndTabs);
        suite.Add("settings window removes clipped outer frame", WindowRemovesClippedOuterFrame);
        suite.Add("settings window clips shell to rounded corners", WindowClipsShellToRoundedCorners);
        suite.Add("settings window keeps icon label gaps readable", WindowKeepsIconLabelGapsReadable);
        suite.Add("settings window uses light themed hotkey keycaps", WindowUsesLightThemedHotkeyKeycaps);
        suite.Add("settings hotkey recording can be cancelled", HotkeyRecordingCanBeCancelled);
        suite.Add("settings window uses local light readable toggles and sliders", WindowUsesLocalLightReadableTogglesAndSliders);
        suite.Add("settings window uses manual model input and editable prompt", WindowUsesManualModelInputAndEditablePrompt);
        suite.Add("settings clear history also clears diagnostics", ClearHistoryAlsoClearsDiagnostics);
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
        TestAssert.Equal("#FFE5E5EA", light.KeycapShell.ToString());
        TestAssert.Equal("#33000000", dark.KeycapShell.ToString());
        TestAssert.Equal("#FFE5E5EA", light.SegmentTrack.ToString());
        TestAssert.Equal("#FFFFFFFF", light.SegmentIndicator.ToString());
        TestAssert.Equal("#FFD1D1D6", light.SegmentIndicatorBorder.ToString());
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

    private static void WindowThemeSegmentKeepsLightSelectionVisible()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var light = SettingsWindowThemePalettes.For("Light");

        TestAssert.True(xaml.Contains("x:Key=\"Settings.SegmentTrackBrush\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.SegmentIndicatorBrush\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.SegmentIndicatorBorderBrush\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Background=\"{DynamicResource Settings.SegmentTrackBrush}\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Background=\"{DynamicResource Settings.SegmentIndicatorBrush}\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("BorderBrush=\"{DynamicResource Settings.SegmentIndicatorBorderBrush}\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetSolidBrush(\"Settings.SegmentTrackBrush\", palette.SegmentTrack)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetSolidBrush(\"Settings.SegmentIndicatorBrush\", palette.SegmentIndicator)", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Background=\"#26FFFFFF\"", StringComparison.Ordinal));
        TestAssert.Equal("#FFE5E5EA", light.SegmentTrack.ToString());
        TestAssert.Equal("#FFFFFFFF", light.SegmentIndicator.ToString());
        TestAssert.Equal("#FFD1D1D6", light.SegmentIndicatorBorder.ToString());
    }

    private static void WindowMouseTabNavigationClearsAutomaticToggleFocus()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("SelectionChanged=\"SettingsTabs_SelectionChanged\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("private void SettingsTabs_SelectionChanged", StringComparison.Ordinal));
        TestAssert.True(code.Contains("WpfInput.Mouse.LeftButton != WpfInput.MouseButtonState.Pressed", StringComparison.Ordinal));
        TestAssert.True(code.Contains("ClearSettingsTabMouseFocus", StringComparison.Ordinal));
        TestAssert.True(code.Contains("DispatcherPriority.ApplicationIdle", StringComparison.Ordinal));
        TestAssert.True(code.Contains("WpfInput.Keyboard.ClearFocus()", StringComparison.Ordinal));
    }

    private static void WindowSeparatesHeaderAndTabs()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(xaml.Contains("<RowDefinition Height=\"48\"/>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Margin=\"24,12,24,0\"", StringComparison.Ordinal));
    }

    private static void WindowRemovesClippedOuterFrame()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var rootShellStart = xaml.IndexOf("<Border x:Name=\"RootShell\"", StringComparison.Ordinal);
        TestAssert.True(rootShellStart >= 0);
        var rootShell = xaml[rootShellStart..xaml.IndexOf("<Border.RenderTransform>", rootShellStart, StringComparison.Ordinal)];

        TestAssert.True(rootShell.Contains("Margin=\"0\"", StringComparison.Ordinal));
        TestAssert.False(rootShell.Contains("Effect=\"{StaticResource Settings.ShellShadow}\"", StringComparison.Ordinal));
        TestAssert.False(rootShell.Contains("Settings.OuterStrokeBrush", StringComparison.Ordinal));
        TestAssert.True(code.Contains("DwmSystemBackdropTypeNone", StringComparison.Ordinal));
        TestAssert.False(code.Contains("DwmSystemBackdropTypeMica", StringComparison.Ordinal));
    }

    private static void WindowClipsShellToRoundedCorners()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("SizeChanged=\"RootShell_SizeChanged\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("RootShell.Clip = new RectangleGeometry", StringComparison.Ordinal));
        TestAssert.True(code.Contains("ShellCornerRadius", StringComparison.Ordinal));
    }

    private static void WindowKeepsIconLabelGapsReadable()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(xaml.Contains("<Grid Width=\"42\" Height=\"36\" Margin=\"0,0,18,0\">", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Margin=\"0,0,10,0\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Margin=\"0,0,6,0\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Margin=\"0,1,9,0\"", StringComparison.Ordinal));
    }

    private static void WindowUsesLightThemedHotkeyKeycaps()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var light = SettingsWindowThemePalettes.For("Light");

        TestAssert.True(xaml.Contains("x:Key=\"Settings.KeycapShellBrush\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Property=\"Background\" Value=\"{DynamicResource Settings.KeycapShellBrush}\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Property=\"Background\" Value=\"{DynamicResource Settings.KeycapBrush}\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("Settings.KeycapShellBrush", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetResourceReference(Border.BackgroundProperty, \"Settings.KeycapBrush\")", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetResourceReference(Border.BorderBrushProperty, \"Settings.KeycapBorderBrush\")", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetResourceReference(TextBlock.ForegroundProperty, \"Settings.TextMutedBrush\")", StringComparison.Ordinal));
        TestAssert.False(code.Contains("FindResource(\"Settings.KeycapBrush\")", StringComparison.Ordinal));
        TestAssert.Equal("#FFE5E5EA", light.KeycapShell.ToString());
        TestAssert.Equal("#FFF2F2F7", light.Keycap.ToString());
    }

    private static void HotkeyRecordingCanBeCancelled()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("PreviewMouseDown=\"Window_PreviewMouseDown\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("_isRecordingHotkey", StringComparison.Ordinal));
        TestAssert.True(code.Contains("BeginHotkeyRecording", StringComparison.Ordinal));
        TestAssert.True(code.Contains("CancelHotkeyRecording", StringComparison.Ordinal));
        TestAssert.True(code.Contains("ClearHotkeyButtonFocus", StringComparison.Ordinal));
        TestAssert.True(code.Contains("WpfInput.Keyboard.ClearFocus()", StringComparison.Ordinal));
        TestAssert.True(code.Contains("IsWithinElement", StringComparison.Ordinal));
        TestAssert.True(code.Contains("key == WpfInput.Key.Escape", StringComparison.Ordinal));

        var hotkeyRecordingStart = code.IndexOf("private void HeaderHotkeyButton_Click", StringComparison.Ordinal);
        var hotkeyRecordingEnd = code.IndexOf("private void UpdateAppearanceValueText", hotkeyRecordingStart, StringComparison.Ordinal);
        TestAssert.True(hotkeyRecordingStart >= 0);
        TestAssert.True(hotkeyRecordingEnd > hotkeyRecordingStart);
        var hotkeyRecordingCode = code[hotkeyRecordingStart..hotkeyRecordingEnd];
        TestAssert.False(hotkeyRecordingCode.Contains("StatusText.Text", StringComparison.Ordinal));
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

    private static void ClearHistoryAlsoClearsDiagnostics()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(code.Contains("await _historyService.ClearAsync();", StringComparison.Ordinal));
        TestAssert.True(code.Contains("_triggerDiagnosticsService.Clear();", StringComparison.Ordinal));
        TestAssert.True(code.Contains("RefreshDiagnostics();", StringComparison.Ordinal));
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
