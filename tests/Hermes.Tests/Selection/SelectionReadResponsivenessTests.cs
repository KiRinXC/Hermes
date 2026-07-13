using System.IO;
using Hermes.Windows.Selection;

namespace Hermes.Tests.Selection;

public static class SelectionReadResponsivenessTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("ui automation selection read runs off the ui thread", UiAutomationSelectionReadRunsOffUiThread);
        suite.Add("clipboard fallback uses dedicated sta thread", ClipboardFallbackUsesDedicatedStaThread);
        suite.Add("clipboard fallback requires a fresh clipboard update", ClipboardFallbackRequiresFreshUpdate);
        suite.Add("clipboard fallback preserves the selection target window", ClipboardFallbackPreservesTargetWindow);
    }

    private static void UiAutomationSelectionReadRunsOffUiThread()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/UiAutomationSelectionProvider.cs"));

        TestAssert.True(code.Contains("Task.Run(() => TryGetSelection", StringComparison.Ordinal));
        TestAssert.False(code.Contains("Task.FromResult", StringComparison.Ordinal));
    }

    private static void ClipboardFallbackUsesDedicatedStaThread()
    {
        var apartment = ClipboardSelectionProvider
            .RunOnStaThreadAsync(() => Thread.CurrentThread.GetApartmentState())
            .GetAwaiter()
            .GetResult();
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/ClipboardSelectionProvider.cs"));

        TestAssert.Equal(ApartmentState.STA, apartment);
        TestAssert.True(code.Contains("RunOnStaThreadAsync", StringComparison.Ordinal));
        TestAssert.False(code.Contains("Application.Current.Dispatcher.InvokeAsync", StringComparison.Ordinal));
    }

    private static void ClipboardFallbackRequiresFreshUpdate()
    {
        TestAssert.False(ClipboardSelectionProvider.IsFreshClipboardUpdate(42, 42));
        TestAssert.True(ClipboardSelectionProvider.IsFreshClipboardUpdate(42, 43));

        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/ClipboardSelectionProvider.cs"));
        TestAssert.Contains("Hermes.ControlledCopy.Probe", code);
        TestAssert.Contains("GetClipboardSequenceNumber", code);
        TestAssert.Contains("CopyTimeout = TimeSpan.FromMilliseconds(350)", code);
        TestAssert.False(code.Contains("WaitOne(90)", StringComparison.Ordinal));
    }

    private static void ClipboardFallbackPreservesTargetWindow()
    {
        var expected = new ForegroundWindowInfo(new IntPtr(10), 100, "source", null);
        var same = new ForegroundWindowInfo(new IntPtr(10), 100, "source", null);
        var different = new ForegroundWindowInfo(new IntPtr(11), 100, "source", null);

        TestAssert.True(ForegroundWindowService.MatchesExpectedWindow(expected, same));
        TestAssert.False(ForegroundWindowService.MatchesExpectedWindow(expected, different));
        TestAssert.True(ForegroundWindowService.MatchesExpectedWindow(null, different));
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
