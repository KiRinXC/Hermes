using Hermes.Windows.Selection;

namespace Hermes.Tests.Selection;

public static class TriggerDiagnosticsServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("trigger diagnostics can be cleared", ClearsTriggerDiagnostics);
    }

    private static void ClearsTriggerDiagnostics()
    {
        var diagnostics = new TriggerDiagnosticsService();
        diagnostics.Record("mouse-selection", "suppressed", foreground: null, "ctrl-not-held");

        TestAssert.Equal(1, diagnostics.GetRecent().Count);

        diagnostics.Clear();

        TestAssert.Equal(0, diagnostics.GetRecent().Count);
    }
}
