using System.IO;

namespace Hermes.Tests.Overlay;

public static class OverlayExperienceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("translation popup uses app icon branding", PopupUsesAppIconBranding);
        suite.Add("translation popup supports surface drag guard", PopupSupportsSurfaceDragGuard);
        suite.Add("floating button keeps icon glyph crisp", FloatingButtonKeepsIconGlyphCrisp);
        suite.Add("floating button uses packaged svg theme icons", FloatingButtonUsesPackagedSvgThemeIcons);
        suite.Add("floating button has full invisible hit frame", FloatingButtonHasFullInvisibleHitFrame);
        suite.Add("completed unpinned popup closes after outside pointer activity", CompletedUnpinnedPopupClosesAfterOutsidePointerActivity);
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

    private static void FloatingButtonUsesPackagedSvgThemeIcons()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));
        var project = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Hermes.Windows.csproj"));

        TestAssert.Contains("Resources\\Icons\\FloatingButtonLight.svg", project);
        TestAssert.Contains("Resources\\Icons\\FloatingButtonDark.svg", project);
        TestAssert.Contains("Fill=\"#FF2C2C2C\"", xaml);
        TestAssert.Contains("Fill=\"#FFFFFFFF\"", xaml);
        TestAssert.False(xaml.Contains("Fill=\"{DynamicResource Brush.TextPrimary}\"", StringComparison.Ordinal));
    }

    private static void FloatingButtonHasFullInvisibleHitFrame()
    {
        var xaml = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Overlay/FloatingButtonWindow.xaml"));

        TestAssert.Contains("Width=\"44\"", xaml);
        TestAssert.Contains("Height=\"44\"", xaml);
        TestAssert.Contains("Background=\"#01000000\"", xaml);
        TestAssert.Contains("x:Name=\"HitFrame\"", xaml);
        TestAssert.Contains("IsHitTestVisible=\"False\"", xaml);
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
