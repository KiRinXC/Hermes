using Hermes.Windows.Settings;

namespace Hermes.Windows.Selection;

public sealed class SelectionCandidateService
{
    public static readonly TimeSpan CandidateLifetime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MinimumDragDuration = TimeSpan.FromMilliseconds(80);
    private const double MinimumDragDistance = 10;

    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly SettingsService _settingsService;
    private readonly TriggerDiagnosticsService _diagnosticsService;

    public SelectionCandidateService(
        UiAutomationSelectionProvider uiAutomationProvider,
        ForegroundWindowService foregroundWindowService,
        SettingsService settingsService,
        TriggerDiagnosticsService diagnosticsService)
    {
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
        _diagnosticsService = diagnosticsService;
    }

    public async Task<SelectionCandidateDecision> CreateFromMouseGestureAsync(
        int startX,
        int startY,
        int releaseX,
        int releaseY,
        DateTimeOffset startedAt,
        DateTimeOffset releasedAt,
        bool ctrlDownAtStart,
        bool ctrlHeldDuringDrag,
        bool ctrlDownAtRelease,
        CancellationToken cancellationToken = default)
    {
        if (ShouldIgnoreBeforeEvaluation(ctrlDownAtStart, ctrlHeldDuringDrag, ctrlDownAtRelease))
        {
            return SelectionCandidateDecision.Reject("ctrl-not-held");
        }

        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        var input = new SelectionCandidateInput(
            startX,
            startY,
            releaseX,
            releaseY,
            startedAt,
            releasedAt,
            foreground,
            ctrlDownAtStart,
            ctrlHeldDuringDrag,
            ctrlDownAtRelease);
        var decision = EvaluateGesture(
            input,
            _settingsService.Current,
            _foregroundWindowService.IsExcluded(foreground),
            _foregroundWindowService.IsFocusedElementSensitive());

        if (!decision.ShouldShow || decision.Candidate is null)
        {
            _diagnosticsService.Record("mouse-selection", "suppressed", foreground, decision.Reason);
            return decision;
        }

        await Task.CompletedTask;
        var accepted = SelectionCandidateDecision.Accept(decision.Candidate, "gesture-confidence");
        _diagnosticsService.Record("mouse-selection", "button-shown", foreground, accepted.Reason);
        return accepted;
    }

    internal static bool ShouldIgnoreBeforeEvaluation(bool ctrlDownAtStart, bool ctrlHeldDuringDrag, bool ctrlDownAtRelease)
    {
        return !ctrlDownAtStart || !ctrlHeldDuringDrag || !ctrlDownAtRelease;
    }

    public static bool IsExpired(SelectionCandidate candidate, DateTimeOffset? now = null)
    {
        return ((now ?? DateTimeOffset.Now) - candidate.CreatedAt) > CandidateLifetime;
    }

    public static bool TryCreatePreReadSelection(
        SelectionCandidate candidate,
        AppSettings settings,
        out SelectionResult selection,
        out SelectionValidationResult validation,
        DateTimeOffset? now = null)
    {
        selection = SelectionResult.Empty("候选选区已过期。", candidate.ForegroundWindow);
        validation = SelectionValidationResult.Invalid(selection.Message!);

        if (IsExpired(candidate, now) || string.IsNullOrWhiteSpace(candidate.PreReadText))
        {
            return false;
        }

        selection = SelectionResult.FromText(
            candidate.PreReadText,
            SelectionProviderKind.UiAutomation,
            candidate.Bounds,
            candidate.ForegroundWindow);
        validation = SelectionTextValidator.Validate(selection.Text, settings);
        return validation.IsValid;
    }

    public static SelectionCandidateDecision EvaluateGesture(
        SelectionCandidateInput input,
        AppSettings settings,
        bool isExcluded,
        bool isSensitive)
    {
        if (!settings.Triggers.AutoShowSelectionButton)
        {
            return SelectionCandidateDecision.Reject("automatic-selection-button-disabled");
        }

        if (isExcluded)
        {
            return SelectionCandidateDecision.Reject("foreground-app-excluded");
        }

        if (isSensitive)
        {
            return SelectionCandidateDecision.Reject("sensitive-control");
        }

        if (!input.CtrlDownAtStart || !input.CtrlHeldDuringDrag || !input.CtrlDownAtRelease)
        {
            return SelectionCandidateDecision.Reject("ctrl-not-held");
        }

        if (input.DragDuration < MinimumDragDuration)
        {
            return SelectionCandidateDecision.Reject("drag-too-short");
        }

        if (input.DragDistance < MinimumDragDistance)
        {
            return SelectionCandidateDecision.Reject("drag-distance-too-small");
        }

        var confidence = Math.Clamp((input.DragDistance / 160) + Math.Min(input.DragDuration.TotalMilliseconds / 900, 0.35), 0.45, 0.92);
        var candidate = new SelectionCandidate(
            Guid.NewGuid(),
            input.ReleasedAt,
            input.ReleaseX,
            input.ReleaseY,
            input.DragDistance,
            input.DragDuration,
            input.ForegroundWindow,
            PreReadText: null,
            Bounds: null,
            Confidence: confidence);

        return SelectionCandidateDecision.Accept(candidate, "gesture-confidence");
    }
}
