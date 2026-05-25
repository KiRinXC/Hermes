namespace Hermes.Windows.Selection;

public sealed record TriggerDiagnosticEntry(
    DateTimeOffset Timestamp,
    string Trigger,
    string Decision,
    string? ProcessName,
    string Reason);

public sealed class TriggerDiagnosticsService
{
    private readonly object _gate = new();
    private readonly Queue<TriggerDiagnosticEntry> _entries = new();

    public void Record(string trigger, string decision, ForegroundWindowInfo? foreground, string reason)
    {
        lock (_gate)
        {
            _entries.Enqueue(new TriggerDiagnosticEntry(
                DateTimeOffset.Now,
                trigger,
                decision,
                foreground?.ProcessName,
                reason));

            while (_entries.Count > 30)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<TriggerDiagnosticEntry> GetRecent()
    {
        lock (_gate)
        {
            return _entries.Reverse().ToList();
        }
    }
}
