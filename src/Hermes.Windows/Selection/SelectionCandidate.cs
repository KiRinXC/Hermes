using Hermes.Windows.Translation;

namespace Hermes.Windows.Selection;

public sealed record SelectionCandidate(
    Guid Id,
    DateTimeOffset CreatedAt,
    int ReleaseX,
    int ReleaseY,
    double DragDistance,
    TimeSpan DragDuration,
    ForegroundWindowInfo? ForegroundWindow,
    TranslationMode Mode,
    string? PreReadText,
    ScreenBounds? Bounds,
    double Confidence)
{
    public bool HasPreReadText => !string.IsNullOrWhiteSpace(PreReadText);

    public SelectionCandidate WithPreRead(SelectionResult selection, double confidence)
    {
        return this with
        {
            PreReadText = selection.Text,
            Bounds = selection.Bounds,
            ForegroundWindow = selection.ForegroundWindow ?? ForegroundWindow,
            Confidence = Math.Max(Confidence, confidence)
        };
    }
}

public sealed record SelectionCandidateInput(
    int StartX,
    int StartY,
    int ReleaseX,
    int ReleaseY,
    DateTimeOffset StartedAt,
    DateTimeOffset ReleasedAt,
    ForegroundWindowInfo? ForegroundWindow,
    TranslationMode Mode,
    bool CtrlDownAtStart,
    bool CtrlHeldDuringDrag,
    bool CtrlDownAtRelease)
{
    public double DragDistance
    {
        get
        {
            var dx = ReleaseX - StartX;
            var dy = ReleaseY - StartY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public TimeSpan DragDuration => ReleasedAt - StartedAt;
}

public sealed record SelectionCandidateDecision(
    bool ShouldShow,
    SelectionCandidate? Candidate,
    string Reason)
{
    public static SelectionCandidateDecision Reject(string reason)
    {
        return new SelectionCandidateDecision(false, null, reason);
    }

    public static SelectionCandidateDecision Accept(SelectionCandidate candidate, string reason)
    {
        return new SelectionCandidateDecision(true, candidate, reason);
    }
}
