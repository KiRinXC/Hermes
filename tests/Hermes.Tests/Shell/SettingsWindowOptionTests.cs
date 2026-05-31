using System.IO;
using Hermes.Windows.Shell;

namespace Hermes.Tests.Shell;

public static class SettingsWindowOptionTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("settings option labels preserve storage values", LabelsPreserveStorageValues);
        suite.Add("settings providers include transmart default option", ProvidersIncludeTransmartDefaultOption);
        suite.Add("settings floating button style options include high contrast pairs", FloatingButtonStyleOptionsIncludeHighContrastPairs);
        suite.Add("settings floating button size options include five steps", FloatingButtonSizeOptionsIncludeFiveSteps);
        suite.Add("settings window keeps entrance transform off Window", WindowKeepsEntranceTransformOffWindow);
        suite.Add("settings window supports native edge resize and persistence", WindowSupportsNativeEdgeResizeAndPersistence);
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
        suite.Add("settings window includes explanation preference editor", WindowIncludesExplanationPreferenceEditor);
        suite.Add("settings window uses dual translation channels", WindowUsesDualTranslationChannels);
        suite.Add("settings window uses close only title action and aligned api test button", WindowUsesCloseOnlyTitleActionAndAlignedApiTestButton);
        suite.Add("settings window keeps action buttons visible in light theme", WindowKeepsActionButtonsVisibleInLightTheme);
        suite.Add("settings window includes floating button style selector", WindowIncludesFloatingButtonStyleSelector);
        suite.Add("settings window includes floating button dot size selector", WindowIncludesFloatingButtonDotSizeSelector);
        suite.Add("settings window icon size preview uses floating button glyph", WindowIconSizePreviewUsesFloatingButtonGlyph);
        suite.Add("settings appearance dot controls share aligned track geometry", WindowAppearanceDotControlsShareAlignedTrackGeometry);
        suite.Add("settings appearance endpoint previews are centered", WindowAppearanceEndpointPreviewsAreCentered);
        suite.Add("settings window icon size preview is theme-aware and unframed", WindowIconSizePreviewIsThemeAwareAndUnframed);
        suite.Add("settings window includes popup font dot size selector", WindowIncludesPopupFontDotSizeSelector);
        suite.Add("settings window uses local themed scrollbars", WindowUsesLocalThemedScrollbars);
        suite.Add("settings appearance does not expose popup size controls", AppearanceDoesNotExposePopupSizeControls);
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

    private static void ProvidersIncludeTransmartDefaultOption()
    {
        var first = SettingsWindowOptions.Providers.First();
        TestAssert.Equal("Transmart", first.Value);
    }

    private static void FloatingButtonStyleOptionsIncludeHighContrastPairs()
    {
        var options = SettingsWindowOptions.FloatingButtonStyles;
        TestAssert.Equal("DarkBorderLightFill", options[0].Value);
        TestAssert.Equal("黑框白底", options[0].Label);
        TestAssert.Equal("LightBorderDarkFill", options[1].Value);
        TestAssert.Equal("白框黑底", options[1].Label);
    }

    private static void FloatingButtonSizeOptionsIncludeFiveSteps()
    {
        var options = SettingsWindowOptions.FloatingButtonSizes;
        TestAssert.Equal(5, options.Count);
        TestAssert.Equal("ExtraSmall", options[0].Value);
        TestAssert.Equal("Small", options[1].Value);
        TestAssert.Equal("Medium", options[2].Value);
        TestAssert.Equal("Large", options[3].Value);
        TestAssert.Equal("ExtraLarge", options[4].Value);
    }

    private static void WindowKeepsEntranceTransformOffWindow()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.False(xaml.Contains("<Window.RenderTransform>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"RootShellScaleTransform\"", StringComparison.Ordinal));
    }

    private static void WindowSupportsNativeEdgeResizeAndPersistence()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("ResizeMode=\"CanResize\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("MaxWidth=\"800\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("MaxHeight=\"600\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("WindowResizeHitTest", StringComparison.Ordinal));
        TestAssert.True(code.Contains("ApplyStoredWindowSize", StringComparison.Ordinal));
        TestAssert.True(code.Contains("PersistWindowSize", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Ui.SettingsWindowWidth", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Ui.SettingsWindowHeight", StringComparison.Ordinal));
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

        TestAssert.False(xaml.Contains("x:Name=\"ProviderCombo\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"TransmartModelText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"OpenAiModelText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"OpenAiForTranslationCheck\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"ModelCombo\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"RefreshModelsButton\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Click=\"RefreshModels_Click\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PromptText\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("目标语言", StringComparison.Ordinal));
        TestAssert.False(code.Contains("ListModelsAsync", StringComparison.Ordinal));
        TestAssert.False(code.Contains("RefreshModelsButton", StringComparison.Ordinal));
    }

    private static void WindowIncludesExplanationPreferenceEditor()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("x:Name=\"ExplanationPreferenceText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("解释个性化偏好", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Translation.ExplanationPreference", StringComparison.Ordinal));
    }

    private static void WindowUsesDualTranslationChannels()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("AI 翻译", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("AI 解释", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"TransmartBaseUrlText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"OpenAiBaseUrlText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"OpenAiForTranslationCheck\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Api.UseOpenAiForTranslation", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Api.Provider = \"OpenAI\";", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Api.Provider = \"Transmart\";", StringComparison.Ordinal));
        TestAssert.True(code.Contains("UpdateApiHintText()", StringComparison.Ordinal));
    }

    private static void WindowUsesCloseOnlyTitleActionAndAlignedApiTestButton()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("AutomationProperties.Name=\"最小化\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Click=\"Minimize_Click\"", StringComparison.Ordinal));
        TestAssert.False(code.Contains("private void Minimize_Click", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Key=\"Settings.ApiTestButton\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Style=\"{StaticResource Settings.ApiTestButton}\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Width=\"112\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("HorizontalAlignment=\"Right\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Margin=\"0\"", StringComparison.Ordinal));
    }

    private static void WindowKeepsActionButtonsVisibleInLightTheme()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.True(xaml.Contains("<Style x:Key=\"Settings.FooterButton\" TargetType=\"{x:Type Button}\">", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("<Setter Property=\"Background\" Value=\"{DynamicResource Settings.KeycapBrush}\"/>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("<Setter Property=\"BorderBrush\" Value=\"{DynamicResource Settings.KeycapBorderBrush}\"/>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("<Setter Property=\"Foreground\" Value=\"#FFFFFFFF\"/>", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("<Style x:Key=\"Settings.GhostIconButton\" TargetType=\"{x:Type Button}\" BasedOn=\"{StaticResource Settings.FooterButton}\">", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("<Setter Property=\"Background\" Value=\"#0DFFFFFF\"/>", StringComparison.Ordinal));
    }

    private static void WindowIncludesFloatingButtonStyleSelector()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.True(xaml.Contains("x:Name=\"FloatingButtonStyleCombo\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("气球样式", StringComparison.Ordinal));
        TestAssert.True(code.Contains("FloatingButtonStyleCombo.ItemsSource = SettingsWindowOptions.FloatingButtonStyles;", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SelectComboValue(FloatingButtonStyleCombo, settings.Ui.FloatingButtonStyle);", StringComparison.Ordinal));
        TestAssert.True(code.Contains("var floatingButtonStyle = GetComboValue(FloatingButtonStyleCombo);", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Ui.FloatingButtonStyle = string.IsNullOrWhiteSpace(floatingButtonStyle)", StringComparison.Ordinal));
    }

    private static void WindowIncludesFloatingButtonDotSizeSelector()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("x:Name=\"FloatingButtonSizeCombo\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"FloatingButtonSizeTrack\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"FloatingButtonSizeDots\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"FloatingButtonSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"FloatingButtonSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"ExtraSmall\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"Small\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"Medium\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"Large\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"ExtraLarge\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetFloatingButtonSizeSelection(settings.Ui.FloatingButtonSize);", StringComparison.Ordinal));
        TestAssert.True(code.Contains("GetFloatingButtonSizeSelection()", StringComparison.Ordinal));
        TestAssert.True(code.Contains("FloatingButtonSizeDot_Checked", StringComparison.Ordinal));
    }

    private static void WindowIconSizePreviewUsesFloatingButtonGlyph()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.False(xaml.Contains("Data=\"M 9 2 C 11 2", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("M320 784c26.5 0 48 21.5 48 48", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Synced from src/Hermes.Windows/Resources/Icons/FloatingButtonLight.svg and FloatingButtonDark.svg", StringComparison.Ordinal));
    }

    private static void WindowAppearanceDotControlsShareAlignedTrackGeometry()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));

        TestAssert.Equal(4, CountOccurrences(xaml, "<ColumnDefinition Width=\"34\"/>"));
        TestAssert.Equal(2, CountOccurrences(xaml, "Margin=\"6,0\""));
        TestAssert.False(xaml.Contains("<ColumnDefinition Width=\"50\"/>", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("Margin=\"2,0,8,0\"", StringComparison.Ordinal));
    }

    private static void WindowAppearanceEndpointPreviewsAreCentered()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var iconRow = Slice(xaml, "x:Name=\"FloatingButtonSizeSmallPreview\"", "<TextBlock Grid.Row=\"2\"");
        var fontRow = Slice(xaml, "x:Name=\"PopupFontSizeSmallPreview\"", "<TextBlock Grid.Row=\"3\"");

        TestAssert.True(iconRow.Contains("x:Name=\"FloatingButtonSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.True(iconRow.Contains("x:Name=\"FloatingButtonSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.True(fontRow.Contains("x:Name=\"PopupFontSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.True(fontRow.Contains("x:Name=\"PopupFontSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.True(CountOccurrences(iconRow + fontRow, "HorizontalAlignment=\"Center\"") >= 4);
        TestAssert.False(fontRow.Contains("<TextBlock x:Name=\"PopupFontSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.False(fontRow.Contains("<TextBlock x:Name=\"PopupFontSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.True(fontRow.Contains("M4 20 L12 4 L20 20 M7.5 14 H16.5", StringComparison.Ordinal));
        TestAssert.True(fontRow.Contains("StrokeLineJoin=\"Round\"", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("HorizontalAlignment=\"Left\"", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("HorizontalAlignment=\"Right\"", StringComparison.Ordinal));
        TestAssert.False(fontRow.Contains("HorizontalAlignment=\"Left\"", StringComparison.Ordinal));
        TestAssert.False(fontRow.Contains("HorizontalAlignment=\"Right\"", StringComparison.Ordinal));
    }

    private static void WindowIconSizePreviewIsThemeAwareAndUnframed()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var iconRow = Slice(xaml, "x:Name=\"FloatingButtonSizeSmallPreview\"", "<TextBlock Grid.Row=\"2\"");

        TestAssert.True(iconRow.Contains("{DynamicResource Settings.FloatingButtonPreviewOuterBrush}", StringComparison.Ordinal));
        TestAssert.True(iconRow.Contains("{DynamicResource Settings.FloatingButtonPreviewInnerBrush}", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("Background=\"#FFFFFFFF\"", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("BorderBrush=\"#FF000000\"", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("CornerRadius=\"11\"", StringComparison.Ordinal));
        TestAssert.False(iconRow.Contains("CornerRadius=\"17\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("Settings.FloatingButtonPreviewOuterBrush", StringComparison.Ordinal));
        TestAssert.True(code.Contains("Settings.FloatingButtonPreviewInnerBrush", StringComparison.Ordinal));
        TestAssert.True(code.Contains("ThemeResourceService.ShouldUseDarkTheme", StringComparison.Ordinal));
    }

    private static void WindowIncludesPopupFontDotSizeSelector()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("x:Name=\"FontSizeSlider\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"FontSizeValueText\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PopupFontSizeTrack\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PopupFontSizeDots\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PopupFontSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("x:Name=\"PopupFontSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("<TextBlock x:Name=\"PopupFontSizeSmallPreview\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("<TextBlock x:Name=\"PopupFontSizeLargePreview\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Canvas Width=\"24\" Height=\"24\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("M4 20 L12 4 L20 20 M7.5 14 H16.5", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("StrokeThickness=\"2.25\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"12\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"14\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"16\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"18\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Tag=\"20\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("PopupFontSizeValues = [12, 14, 16, 18, 20]", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetPopupFontSizeSelection(settings.Ui.FontSize);", StringComparison.Ordinal));
        TestAssert.True(code.Contains("GetPopupFontSizeSelection()", StringComparison.Ordinal));
        TestAssert.True(code.Contains("PopupFontSizeDot_Checked", StringComparison.Ordinal));
    }

    private static void WindowUsesLocalThemedScrollbars()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var paletteSource = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindowThemePalette.cs"));

        TestAssert.True(xaml.Contains("x:Key=\"Settings.ScrollBarThumbBrush\"", StringComparison.Ordinal));
        TestAssert.True(xaml.Contains("Background=\"{DynamicResource Settings.ScrollBarThumbBrush}\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("SetSolidBrush(\"Settings.ScrollBarThumbBrush\", palette.ScrollBarThumb)", StringComparison.Ordinal));
        TestAssert.True(paletteSource.Contains("MediaColor ScrollBarThumb", StringComparison.Ordinal));
    }

    private static void AppearanceDoesNotExposePopupSizeControls()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("x:Name=\"PopupWidthSlider\"", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("x:Name=\"PopupWidthValueText\"", StringComparison.Ordinal));
        TestAssert.False(code.Contains("PopupWidthSlider", StringComparison.Ordinal));
        TestAssert.False(code.Contains("PopupWidthValueText", StringComparison.Ordinal));
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

    private static string Slice(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        TestAssert.True(start >= 0);
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        TestAssert.True(end > start);
        return text[start..end];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
