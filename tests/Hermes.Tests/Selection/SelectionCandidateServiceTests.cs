using Hermes.Windows.Selection;
using Hermes.Windows.Settings;

namespace Hermes.Tests.Selection;

public static class SelectionCandidateServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("accepts high confidence selection candidate", AcceptsHighConfidenceCandidate);
        suite.Add("rejects low distance selection candidate", RejectsLowDistanceCandidate);
        suite.Add("rejects no ctrl selection candidate", RejectsNoCtrlSelectionCandidate);
        suite.Add("rejects released ctrl selection candidate", RejectsReleasedCtrlSelectionCandidate);
        suite.Add("rejects excluded app selection candidate", RejectsExcludedAppSelectionCandidate);
        suite.Add("rejects disabled automatic selection", RejectsDisabledAutomaticSelection);
        suite.Add("rejects expired selection candidate", RejectsExpiredCandidate);
        suite.Add("uses candidate pre-read text", UsesCandidatePreReadText);
        suite.Add("rejects stale candidate pre-read text", RejectsStaleCandidatePreReadText);
        suite.Add("rejects empty candidate pre-read text", RejectsEmptyCandidatePreReadText);
    }

    private static void AcceptsHighConfidenceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, true, true),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.True(decision.ShouldShow);
    }

    private static void RejectsLowDistanceCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 14, 12, now, now.AddMilliseconds(320), null, true, true, true),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.False(decision.ShouldShow);
    }

    private static void RejectsNoCtrlSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, false, false, false),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("ctrl-not-held", decision.Reason);
    }

    private static void RejectsReleasedCtrlSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, false, false),
            new AppSettings(),
            isExcluded: false,
            isSensitive: false);
        TestAssert.False(decision.ShouldShow);
        TestAssert.Equal("ctrl-not-held", decision.Reason);
    }

    private static void RejectsExcludedAppSelectionCandidate()
    {
        var now = DateTimeOffset.Now;
        var foreground = new ForegroundWindowInfo(IntPtr.Zero, 123, "blocked", "Blocked app");
        var decision = SelectionCandidateService.EvaluateGesture(
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), foreground, true, true, true),
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
            new SelectionCandidateInput(10, 10, 180, 18, now, now.AddMilliseconds(320), null, true, true, true),
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
            preReadText,
            null,
            0.8);
    }
}
