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
        suite.Add("uses candidate pre-read text", UsesCandidatePreReadText);
        suite.Add("rejects stale candidate pre-read text", RejectsStaleCandidatePreReadText);
        suite.Add("rejects empty candidate pre-read text", RejectsEmptyCandidatePreReadText);
        suite.Add("passive candidate requires pre-read validation", PassiveCandidateRequiresPreReadValidation);
        suite.Add("passive candidate falls back when pre-read is unavailable", PassiveCandidateFallsBackWhenPreReadUnavailable);
        suite.Add("passive candidate pre-read is time boxed", PassiveCandidatePreReadIsTimeBoxed);
        suite.Add("passive candidate sensitive check is time boxed", PassiveCandidateSensitiveCheckIsTimeBoxed);
    }

    private static void AcceptsHighConfidenceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, true, true),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.True(decision.ShouldShow);
    }

    private static void RejectsLowDistanceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 14, 12, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, true, true),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.False(decision.ShouldShow);
    }

    private static void AcceptsModifierReleasedBeforeMouseUpCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, true, false, false),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.True(decision.ShouldShow);
    }

    private static void RejectsNoCtrlSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, false, false, false),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("modifier-not-held", decision.Reason);
    }

    private static void RejectsModifierPressedAfterDragStartsCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, TranslationMode.Translate, false, true, true),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
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
            isExcluded: true,
            isSensitive: false);
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
            isExcluded: false,
            isSensitive: false);
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
            null,
            null,
            0.8);
        TestAssert.True(SelectionCandidateService.IsExpired(candidate));
    }

    private static void UsesCandidatePreReadText()
    {
        var candidate = CreateCandidate("Hello world");
        var created = SelectionCandidateService.TryCreatePreReadSelection(
            candidate,
            new AppSettings(),
            out var selection,
            out var validation);
        TestAssert.True(created);
        TestAssert.True(validation.IsValid);
        TestAssert.Equal("Hello world", selection.Text);
        TestAssert.Equal(SelectionProviderKind.UiAutomation, selection.Provider);
    }

    private static void RejectsStaleCandidatePreReadText()
    {
        var candidate = CreateCandidate("Hello world") with
        {
            CreatedAt = DateTimeOffset.Now.Subtract(TimeSpan.FromSeconds(10))
        };
        var created = SelectionCandidateService.TryCreatePreReadSelection(
            candidate,
            new AppSettings(),
            out _,
            out _);
        TestAssert.False(created);
    }

    private static void RejectsEmptyCandidatePreReadText()
    {
        var candidate = CreateCandidate(null);
        var created = SelectionCandidateService.TryCreatePreReadSelection(
            candidate,
            new AppSettings(),
            out _,
            out _);
        TestAssert.False(created);
    }

    private static SelectionCandidate CreateCandidate(string? preReadText)
    {
        return new SelectionCandidate(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            20,
            20,
            120,
            TimeSpan.FromMilliseconds(250),
            null,
            TranslationMode.Translate,
            preReadText,
            null,
            0.8);
    }

    private static void PassiveCandidateRequiresPreReadValidation()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidateService.cs"));
        TestAssert.Contains("_uiAutomationProvider.TryGetSelectionAsync", code);
        TestAssert.Contains("SelectionTextValidator.Validate(preRead.Text", code);
        TestAssert.Contains("pre-read-selection-invalid", code);
    }

    private static void PassiveCandidateFallsBackWhenPreReadUnavailable()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidateService.cs"));
        TestAssert.Contains("gesture-fallback-pending", code);
    }

    private static void PassiveCandidatePreReadIsTimeBoxed()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidateService.cs"));
        TestAssert.Contains("PassivePreReadTimeout = TimeSpan.FromMilliseconds(35)", code);
        TestAssert.Contains("Task.WhenAny", code);
    }

    private static void PassiveCandidateSensitiveCheckIsTimeBoxed()
    {
        var candidateCode = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/SelectionCandidateService.cs"));
        var foregroundCode = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Selection/ForegroundWindowService.cs"));

        TestAssert.Contains("PassiveSensitiveCheckTimeout", candidateCode);
        TestAssert.Contains("IsFocusedElementSensitiveWithinAsync", candidateCode);
        TestAssert.False(candidateCode.Contains("IsFocusedElementSensitive())", StringComparison.Ordinal));
        TestAssert.Contains("Task.WhenAny(sensitiveTask", foregroundCode);
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
