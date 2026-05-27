using System.IO;
using Hermes.Windows.Selection;

namespace Hermes.Tests.Selection;

public static class SelectionReadResponsivenessTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("ui automation selection read runs off the ui thread", UiAutomationSelectionReadRunsOffUiThread);
        suite.Add("clipboard fallback uses dedicated sta thread", ClipboardFallbackUsesDedicatedStaThread);
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
