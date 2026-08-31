using Hermes.Windows.Settings;
using Hermes.Windows.Translation;

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
        ForegroundWindowService foregroundWindowService,
        SettingsService settingsService,
        TriggerDiagnosticsService diagnosticsService)
    {
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
        _diagnosticsService = diagnosticsService;
    }

    public SelectionCandidateDecision CreateFromMouseGesture(
        int startX,
        int startY,
        int releaseX,
        int releaseY,
        DateTimeOffset startedAt,
        DateTimeOffset releasedAt,
        TranslationMode mode,
        bool ctrlDownAtStart,
        bool ctrlHeldDuringDrag,
        bool ctrlDownAtRelease)
    {
        if (ShouldIgnoreBeforeEvaluation(ctrlDownAtStart, ctrlHeldDuringDrag, ctrlDownAtRelease))
        {
            return SelectionCandidateDecision.Reject("modifier-not-held");
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
            mode,
            ctrlDownAtStart,
            ctrlHeldDuringDrag,
            ctrlDownAtRelease);
        var decision = EvaluateGesture(
            input,
            _settingsService.Current,
            _foregroundWindowService.IsExcluded(foreground));

        if (!decision.ShouldShow || decision.Candidate is null)
        {
            _diagnosticsService.Record("mouse-selection", "suppressed", foreground, decision.Reason);
            return decision;
        }

        _diagnosticsService.Record("mouse-selection", "button-shown", foreground, decision.Reason);
        return decision;
    }

    internal static bool ShouldIgnoreBeforeEvaluation(bool ctrlDownAtStart, bool ctrlHeldDuringDrag, bool ctrlDownAtRelease)
    {
        return !ctrlDownAtStart;
    }

    public static bool IsExpired(SelectionCandidate candidate, DateTimeOffset? now = null)
    {
        return ((now ?? DateTimeOffset.Now) - candidate.CreatedAt) > CandidateLifetime;
    }

    public static SelectionCandidateDecision EvaluateGesture(
        SelectionCandidateInput input,
        AppSettings settings,
        bool isExcluded)
    {
        if (!settings.Triggers.AutoShowSelectionButton)
        {
            return SelectionCandidateDecision.Reject("automatic-selection-button-disabled");
        }

        if (isExcluded)
        {
            return SelectionCandidateDecision.Reject("foreground-app-excluded");
        }

        if (!input.CtrlDownAtStart)
        {
            return SelectionCandidateDecision.Reject("modifier-not-held");
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
            input.Mode,
            Confidence: confidence);

        return SelectionCandidateDecision.Accept(candidate, "gesture-confidence");
    }
}
