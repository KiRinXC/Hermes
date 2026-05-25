namespace Hermes.Windows.Settings;

public interface ISecretStorageService
{
    Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default);

    Task ClearApiKeyAsync(CancellationToken cancellationToken = default);

    bool HasApiKey();
}
