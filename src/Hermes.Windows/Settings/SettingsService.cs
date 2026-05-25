using System.IO;
using System.Text.Json;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppLogger _logger;

    public SettingsService(AppLogger logger)
    {
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();

        if (!File.Exists(AppPaths.SettingsPath))
        {
            Current = new AppSettings();
            await SaveAsync(Current, cancellationToken);
            return Current;
        }

        try
        {
            await using var stream = File.OpenRead(AppPaths.SettingsPath);
            Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Settings file could not be loaded; defaults will be used. {ex.Message}");
            Current = new AppSettings();
        }

        return Current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        await using var stream = File.Create(AppPaths.SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        Current = settings;
        SettingsChanged?.Invoke(this, Current);
    }
}
