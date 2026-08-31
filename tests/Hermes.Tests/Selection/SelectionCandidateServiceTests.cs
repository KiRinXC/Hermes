using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;
using System.IO;

namespace Hermes.Tests.Selection;

public static class SelectionCandidateServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("accepts high confidence selection candidate", AcceptsHighConfidenceCandidate);
        suite.Add("accepts selection candidate when modifier was released before mouse up", AcceptsModifierReleasedBeforeMouseUpCandidate);
        suite.Add("rejects low distance selection candidate", RejectsLowDistanceCandidate);
        suite.Add("rejects no ctrl selection candidate", RejectsNoCtrlSelectionCandidate);
        suite.Add("rejects modifier pressed after drag starts selection candidate", RejectsModifierPressedAfterDragStartsCandidate);
        suite.Add("ignores no ctrl candidate before evaluation", IgnoresNoCtrlCandidateBeforeEvaluation);
        suite.Add("rejects excluded app selection candidate", RejectsExcludedAppSelectionCandidate);
        suite.Add("rejects disabled automatic selection", RejectsDisabledAutomaticSelection);
        suite.Add("rejects expired selection candidate", RejectsExpiredCandidate);
        suite.Add("passive candidate does not read selection content", PassiveCandidateDoesNotReadSelectionContent);
        suite.Add("candidate stores no pre-read selection content", CandidateStoresNoPreReadSelectionContent);
    }

    private static void AcceptsHighConfidenceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, true, true),
            new AppSettings(),
            isExcluded: false);
        TestAssert.True(decision.ShouldShow);
    }

    private static void RejectsLowDistanceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 14, 12, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, true, true),
            new AppSettings(),
            isExcluded: false);
        TestAssert.False(decision.ShouldShow);
    }

    private static void AcceptsModifierReleasedBeforeMouseUpCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, false, false),
            new AppSettings(),
            isExcluded: false);
        TestAssert.True(decision.ShouldShow);
    }

    private static void RejectsNoCtrlSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, false, false, false),
            new AppSettings(),
            isExcluded: false);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("modifier-not-held", decision.Reason);
    }

    private static void RejectsModifierPressedAfterDragStartsCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, false, true, true),
            new AppSettings(),
            isExcluded: false);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("modifier-not-held", decision.Reason);
    }

    private static void IgnoresNoCtrlCandidateBeforeEvaluation()
    {
        TestAssert.True(SelectionCandidateService.ShouldIgnoreBeforeEvaluation(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.False(SelectionCandidateService.ShouldIgnoreBeforeEvaluation(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.True(SelectionCandidateService.ShouldIgnoreBeforeEvaluation(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: false));
    }

    private static void RejectsExcludedAppSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var foreground = new ForegroundWindowInfo(IntPtr.Zero, 123, "blocked", "Blocked app");
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), foreground, TranslationMode.Translate, true, true, true),
            new AppSettings(),
            isExcluded: true);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("foreground-app-excluded", decision.Reason);
    }

    private static void RejectsDisabledAutomaticSelection()
    {
        var settings = new AppSettings();
        settings.Triggers.AutoShowSelectionButton = false;
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, true, true),
            settings,
            isExcluded: false);
        TestAssert.False(decision.ShouldShow);
    }

    private static void RejectsExpiredCandidate()
    {
        var candidate = new SelectionCandidate(
            Guid.NewGuid(),
            DateTimeOffset.Now.Subtract(TimeSpan.FromSeconds(10)),
            20,
            20,
            120,
            TimeSpan.FromMilliseconds(250),
            null,
            TranslationMode.Translate,
            0.8);
        TestAssert.True(SelectionCandidateService.IsExpired(candidate));
    }

    private static void PassiveCandidateDoesNotReadSelectionContent()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidateService.cs"));
        TestAssert.False(code.Contains("UiAutomationSelectionProvider", StringComparison.Ordinal));
        TestAssert.False(code.Contains("TryGetSelectionAsync", StringComparison.Ordinal));
        TestAssert.False(code.Contains("ClipboardSelectionProvider", StringComparison.Ordinal));
        TestAssert.False(code.Contains("IsFocusedElementSensitive", StringComparison.Ordinal));
    }

    private static void CandidateStoresNoPreReadSelectionContent()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidate.cs"));
        TestAssert.False(code.Contains("PreReadText", StringComparison.Ordinal));
        TestAssert.False(code.Contains("ScreenBounds", StringComparison.Ordinal));
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
