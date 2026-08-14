namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;

public enum CodexAuthMode
{
    Unknown,
    ChatGpt,
    Api
}

public sealed record CodexProfileSummary(
    bool Saved,
    CodexAuthMode Mode,
    string? Provider = null,
    string? Model = null,
    string? ReasoningEffort = null,
    string? BaseUrl = null,
    DateTimeOffset? SavedAt = null);

public sealed record CodexApiProfileDraft(
    string ConfigText,
    string AuthText);

public sealed record CodexSessionComposition(
    int RegularActive,
    int InternalActive,
    int Archived,
    int Unclassified)
{
    public int Total => RegularActive + InternalActive + Archived + Unclassified;

    public int Active => RegularActive + InternalActive + Unclassified;
}

public sealed record CodexHistoryAlignment(
    IReadOnlyDictionary<string, int> RolloutProviderCounts,
    IReadOnlyDictionary<string, int> SqliteProviderCounts,
    int RolloutMismatched,
    int SqliteMismatched,
    string? StateDatabasePath,
    CodexSessionComposition SessionComposition)
{
    public int RolloutTotal => RolloutProviderCounts.Values.Sum();

    public int SqliteTotal => SqliteProviderCounts.Values.Sum();

    public bool IsAligned => RolloutMismatched == 0 && SqliteMismatched == 0;
}

public sealed record CodexStatus(
    CodexAuthMode Mode,
    string Provider,
    string Model,
    string ReasoningEffort,
    CodexProfileSummary ChatGptProfile,
    CodexProfileSummary ApiProfile,
    CodexHistoryAlignment History,
    IReadOnlyList<string> RunningProcesses);

public sealed record CodexOperationResult(
    string TargetProvider,
    int RolloutFilesUpdated,
    int SqliteRowsUpdated,
    CodexHistoryAlignment Verification,
    string BackupDirectory);

internal sealed record CodexProfileBundle(
    int Version,
    CodexAuthMode Mode,
    DateTimeOffset SavedAt,
    string ConfigText,
    string AuthText);
