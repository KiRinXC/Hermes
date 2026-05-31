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
            SettingsCompatibility.Normalize(Current, migrateLegacyProviderConfiguration: false);
            await SaveAsync(Current, cancellationToken);
            return Current;
        }

        try
        {
            var json = await File.ReadAllTextAsync(AppPaths.SettingsPath, cancellationToken);
            var shouldApplyLegacyProviderMigration = ShouldApplyLegacyProviderMigration(json);
            Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? new AppSettings();
            SettingsCompatibility.Normalize(Current, shouldApplyLegacyProviderMigration);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Settings file could not be loaded; defaults will be used. {ex.Message}");
            Current = new AppSettings();
            SettingsCompatibility.Normalize(Current, migrateLegacyProviderConfiguration: false);
        }

        return Current;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        SettingsCompatibility.Normalize(settings, migrateLegacyProviderConfiguration: false);
        await using var stream = File.Create(AppPaths.SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        Current = settings;
        SettingsChanged?.Invoke(this, Current);
    }

    private static bool ShouldApplyLegacyProviderMigration(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            if (!TryGetPropertyIgnoreCase(document.RootElement, "Api", out var apiNode)
                || apiNode.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            return !TryGetPropertyIgnoreCase(apiNode, "UseOpenAiForTranslation", out _);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
