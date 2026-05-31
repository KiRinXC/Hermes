using Hermes.Windows.Input;
using System.IO;

namespace Hermes.Tests.Input;

public static class MouseHookServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("mouse hook tracks selection gestures only while ctrl is held", TracksSelectionGesturesOnlyWithCtrl);
        suite.Add("mouse hook supports alt gesture explain mode", SupportsAltGestureExplainMode);
    }

    private static void TracksSelectionGesturesOnlyWithCtrl()
    {
        TestAssert.False(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: false));
        TestAssert.True(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: true));

        TestAssert.False(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: false));
        TestAssert.False(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: true));
        TestAssert.True(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: true));
    }

    private static void SupportsAltGestureExplainMode()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Input/MouseHookService.cs"));
        TestAssert.Contains("IsAltDown", code);
        TestAssert.Contains("TranslationMode.Explain", code);
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
