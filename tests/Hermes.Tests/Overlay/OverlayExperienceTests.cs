using System.IO;

namespace Hermes.Tests.Overlay;

public static class OverlayExperienceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("translation popup uses app icon branding", PopupUsesAppIconBranding);
        suite.Add("translation popup supports surface drag guard", PopupSupportsSurfaceDragGuard);
        suite.Add("floating button keeps icon glyph crisp", FloatingButtonKeepsIconGlyphCrisp);
        suite.Add("floating button uses src svg theme icons", FloatingButtonUsesSrcSvgThemeIcons);
        suite.Add("floating button keeps svg icon files under src only", FloatingButtonKeepsSvgIconFilesUnderSrcOnly);
        suite.Add("floating button has full invisible hit frame", FloatingButtonHasFullInvisibleHitFrame);
        suite.Add("floating button supports manual high contrast styles", FloatingButtonSupportsManualHighContrastStyles);
        suite.Add("floating button supports user controlled size", FloatingButtonSupportsUserControlledSize);
        suite.Add("translation popup supports native edge resize and persistence", TranslationPopupSupportsNativeEdgeResizeAndPersistence);
        suite.Add("translation popup uses themed scrollbar style", TranslationPopupUsesThemedScrollbarStyle);
        suite.Add("translation popup applies configured font size to body and source preview", TranslationPopupAppliesConfiguredFontSizeToBodyAndSourcePreview);
        suite.Add("translation popup preserves loading channel while streaming", TranslationPopupPreservesLoadingChannelWhileStreaming);
        suite.Add("translation popup keeps readable chinese labels", TranslationPopupKeepsReadableChineseLabels);
        suite.Add("completed unpinned popup closes after outside pointer activity", CompletedUnpinnedPopupClosesAfterOutsidePointerActivity);
        suite.Add("overlay manager tracks multiple popups", OverlayManagerTracksMultiplePopups);
        suite.Add("overlay manager keeps configured popup width", OverlayManagerKeepsConfiguredPopupWidth);
        suite.Add("overlay manager closes orphan floating buttons before showing a new one", OverlayManagerClosesOrphanFloatingButtons);
    }

    private static void PopupUsesAppIconBranding()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml"));

        TestAssert.Contains("Source=\"../Resources/AppIcon.png\"", xaml);
        TestAssert.False(xaml.Contains("Text=\"Hermes 正在转译...\"", StringComparison.Ordinal));
    }

    private static void PopupSupportsSurfaceDragGuard()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));

        TestAssert.Contains("PreviewMouseLeftButtonDown=\"Card_PreviewMouseLeftButtonDown\"", xaml);
        TestAssert.Contains("IsInteractiveDragSource", code);
        TestAssert.Contains("ButtonBase", code);
        TestAssert.Contains("ScrollBar", code);
    }

    private static void FloatingButtonKeepsIconGlyphCrisp()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml.cs"));

        TestAssert.False(xaml.Contains("<Grid.Effect>", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("DropShadowEffect", StringComparison.Ordinal));
        TestAssert.False(code.Contains("IconGrid.Opacity = 0.55", StringComparison.Ordinal));
        TestAssert.Contains("SnapsToDevicePixels=\"True\"", xaml);
    }

    private static void FloatingButtonUsesSrcSvgThemeIcons()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));
        var lightSvg = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Resources/Icons/FloatingButtonLight.svg"));
        var darkSvg = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Resources/Icons/FloatingButtonDark.svg"));

        TestAssert.Contains("fill=\"#000000\"", lightSvg);
        TestAssert.Contains("fill=\"#FFFFFF\"", darkSvg);
        TestAssert.Contains("Synced from src/Hermes.Windows/Resources/Icons/FloatingButtonLight.svg and FloatingButtonDark.svg", xaml);
        TestAssert.Contains("Fill=\"#FF000000\"", xaml);
        TestAssert.Contains("Fill=\"#FFFFFFFF\"", xaml);
        TestAssert.False(xaml.Contains("think-light.svg", StringComparison.Ordinal));
        TestAssert.False(xaml.Contains("think-dark.svg", StringComparison.Ordinal));
    }

    private static void FloatingButtonKeepsSvgIconFilesUnderSrcOnly()
    {
        var repoRoot = FindRepoRoot();
        var project = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Hermes.Windows.csproj"));

        TestAssert.Contains("Resources\\Icons\\FloatingButtonLight.svg", project);
        TestAssert.Contains("Resources\\Icons\\FloatingButtonDark.svg", project);
        TestAssert.False(File.Exists(Path.Combine(repoRoot, "think-light.svg")));
        TestAssert.False(File.Exists(Path.Combine(repoRoot, "think-dark.svg")));
    }

    private static void FloatingButtonHasFullInvisibleHitFrame()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));

        TestAssert.Contains("Background=\"#01000000\"", xaml);
        TestAssert.Contains("x:Name=\"HitFrame\"", xaml);
        TestAssert.Contains("Width=\"{TemplateBinding Width}\"", xaml);
        TestAssert.Contains("Height=\"{TemplateBinding Height}\"", xaml);
        TestAssert.Contains("IsHitTestVisible=\"False\"", xaml);
    }

    private static void FloatingButtonSupportsManualHighContrastStyles()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml.cs"));
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));
        var visualStyle = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonVisualStyle.cs"));

        TestAssert.False(xaml.Contains("x:Name=\"BubbleSurface\"", StringComparison.Ordinal));
        TestAssert.Contains("Width=\"25\"", xaml);
        TestAssert.Contains("Height=\"25\"", xaml);
        TestAssert.Contains("FloatingButtonVisualStyle.LightBorderDarkFill", code);
        TestAssert.Contains("DarkBorderLightFill", visualStyle);
        TestAssert.Contains("LightBorderDarkFill", visualStyle);
        TestAssert.Contains("_settingsService.Current.Ui.FloatingButtonStyle", manager);
    }

    private static void FloatingButtonSupportsUserControlledSize()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml.cs"));
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));

        TestAssert.Contains("FloatingButtonDimensions", code);
        TestAssert.Contains("ExtraSmall", code);
        TestAssert.Contains("ExtraLarge", code);
        TestAssert.Contains("ApplyDimensions", code);
        TestAssert.Contains("IconGrid.Width = dimensions.IconSize", code);
        TestAssert.Contains("TranslateButton.Width = dimensions.HitSize", code);
        TestAssert.Contains("_settingsService.Current.Ui.FloatingButtonSize", manager);
        TestAssert.Contains("Width=\"{TemplateBinding Width}\"", xaml);
    }

    private static void TranslationPopupSupportsNativeEdgeResizeAndPersistence()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml"));
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));

        TestAssert.Contains("ResizeMode=\"CanResize\"", xaml);
        TestAssert.Contains("SizeToContent=\"Manual\"", xaml);
        TestAssert.Contains("WindowResizeHitTest", code);
        TestAssert.Contains("SizeChangedByUser", code);
        TestAssert.Contains("PersistPopupSize", manager);
        TestAssert.Contains("_settingsService.Current.Ui.PopupHeight", manager);
    }

    private static void TranslationPopupUsesThemedScrollbarStyle()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml"));
        var lightTheme = File.ReadAllText(FindRepoFile("src/Hermes.Windows/UI/Themes/LightTheme.xaml"));
        var darkTheme = File.ReadAllText(FindRepoFile("src/Hermes.Windows/UI/Themes/DarkTheme.xaml"));

        TestAssert.Contains("x:Key=\"Popup.ScrollViewer\"", xaml);
        TestAssert.Contains("x:Key=\"Popup.ScrollBarThumb\"", xaml);
        TestAssert.Contains("Background=\"{DynamicResource Brush.ScrollThumb}\"", xaml);
        TestAssert.Contains("Style=\"{StaticResource Popup.ScrollViewer}\"", xaml);
        TestAssert.Contains("Brush.ScrollThumb", lightTheme);
        TestAssert.Contains("Brush.ScrollThumb", darkTheme);
    }

    private static void TranslationPopupAppliesConfiguredFontSizeToBodyAndSourcePreview()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));

        TestAssert.Contains("Math.Clamp(fontSize, 12, 20)", code);
        TestAssert.Contains("BodyText.FontSize = appliedFontSize;", code);
        TestAssert.Contains("SourcePreviewText.FontSize = appliedFontSize;", code);
    }

    private static void TranslationPopupPreservesLoadingChannelWhileStreaming()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));

        TestAssert.Contains("private string? _loadingStateText;", code);
        TestAssert.Contains("_loadingStateText = string.IsNullOrWhiteSpace(stateText) ? \"正在翻译...\" : stateText;", code);
        TestAssert.Contains("StateText.Text = _loadingStateText;", code);
        TestAssert.Contains("StateText.Text = _loadingStateText ?? \"仍在处理...\";", code);
        TestAssert.False(code.Contains("StateText.Text = \"正在翻译...\";", StringComparison.Ordinal));
    }

    private static void TranslationPopupKeepsReadableChineseLabels()
    {
        var popupCode = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));
        var popupXaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml"));

        TestAssert.Contains("CopyLabel.Text = \"复制译文\";", popupCode);
        TestAssert.Contains("CopyLabel.Text = \"已复制 ✓\";", popupCode);
        TestAssert.Contains("Text=\"正在翻译...\"", popupXaml);

        TestAssert.False(popupCode.Contains("澶嶅埗璇戞枃", StringComparison.Ordinal));
        TestAssert.False(popupXaml.Contains("正在转译", StringComparison.Ordinal));
        TestAssert.False(popupXaml.Contains("穿透网络以太层", StringComparison.Ordinal));
    }

    private static void CompletedUnpinnedPopupClosesAfterOutsidePointerActivity()
    {
        var app = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));
        var coordinator = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TranslationCoordinator.cs"));
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));
        var popup = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs"));

        TestAssert.True(app.Contains("ClosePassiveUiAfterPointerActivity", StringComparison.Ordinal));
        TestAssert.True(coordinator.Contains("CloseCompletedUnpinnedPopup", StringComparison.Ordinal));
        TestAssert.True(manager.Contains("HasCompletedTranslation: true", StringComparison.Ordinal));
        TestAssert.True(popup.Contains("public bool HasCompletedTranslation", StringComparison.Ordinal));
    }

    private static void OverlayManagerTracksMultiplePopups()
    {
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));

        TestAssert.True(manager.Contains("private readonly List<TranslationPopupWindow> _popups", StringComparison.Ordinal));
        TestAssert.True(manager.Contains("foreach (var popup in _popups)", StringComparison.Ordinal));
        TestAssert.True(manager.Contains("PopupRetryRequested?.Invoke(this, new PopupEventArgs(popup))", StringComparison.Ordinal));
    }

    private static void OverlayManagerKeepsConfiguredPopupWidth()
    {
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));

        TestAssert.True(manager.Contains("Math.Clamp(width, 280, 640)", StringComparison.Ordinal));
        TestAssert.False(manager.Contains("Math.Abs(width - 420)", StringComparison.Ordinal));
    }

    private static void OverlayManagerClosesOrphanFloatingButtons()
    {
        var manager = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/OverlayManager.cs"));

        TestAssert.Contains("WpfApplication.Current?.Windows.OfType<FloatingButtonWindow>()", manager);
        TestAssert.Contains("CloseTrackedFloatingButton", manager);
        TestAssert.Contains("foreach (var orphan in orphanButtons)", manager);
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Design.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repo root.");
    }
}
