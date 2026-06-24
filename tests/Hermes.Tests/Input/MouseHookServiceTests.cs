using Hermes.Windows.Input;
using System.IO;

namespace Hermes.Tests.Input;

public static class MouseHookServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("mouse hook requires modifier before dragging but allows release in any order", RequiresModifierBeforeDraggingButAllowsReleaseOrder);
        suite.Add("mouse hook accepts only near-start modifier timing", AcceptsOnlyNearStartModifierTiming);
        suite.Add("mouse hook supports alt gesture explain mode", SupportsAltGestureExplainMode);
    }

    private static void RequiresModifierBeforeDraggingButAllowsReleaseOrder()
    {
        TestAssert.True(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: false));
        TestAssert.True(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: true));

        TestAssert.False(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.True(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
    }

    private static void AcceptsOnlyNearStartModifierTiming()
    {
        var mouseDownAt = DateTimeOffset.Now;
        TestAssert.True(MouseHookService.IsModifierTimingCompatible(mouseDownAt, mouseDownAt.AddMilliseconds(-120)));
        TestAssert.True(MouseHookService.IsModifierTimingCompatible(mouseDownAt, mouseDownAt.AddMilliseconds(60)));
        TestAssert.False(MouseHookService.IsModifierTimingCompatible(mouseDownAt, mouseDownAt.AddMilliseconds(260)));
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
