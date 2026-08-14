namespace Hermes.Windows.Apps.Contracts;

public interface ISettingsSaveParticipant
{
    bool HasPendingChanges { get; }

    Task<SettingsSaveResult> SavePendingChangesAsync();

    void DiscardPendingChanges();
}

public sealed record SettingsSaveResult(bool Success, string? UserMessage = null)
{
    public static SettingsSaveResult Completed(string? userMessage = null) => new(true, userMessage);

    public static SettingsSaveResult Failed(string userMessage) => new(false, userMessage);
}
