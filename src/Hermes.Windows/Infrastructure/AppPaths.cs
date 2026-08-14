using System.IO;

namespace Hermes.Windows.Infrastructure;

public static class AppPaths
{
    private const string AppDataFolderName = "Hermes";
    private const string PublisherFolderName = "KiRinXC";
    private const string LegacyAppDataFolderName = "AITranslator";

    private static string LocalAppDataDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static bool _suppressCreationForUserDataDeletion;

    public static string AppDataDirectory { get; } = Path.Combine(
        LocalAppDataDirectory,
        PublisherFolderName,
        AppDataFolderName);

    private static string InstallCoupledAppDataDirectory { get; } = Path.Combine(
        LocalAppDataDirectory,
        AppDataFolderName);

    private static string LegacyAppDataDirectory { get; } = Path.Combine(
        LocalAppDataDirectory,
        LegacyAppDataFolderName);

    public static string ProgramDirectory
    {
        get
        {
            var contentDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            var parent = Directory.GetParent(contentDirectory);
            return parent is not null
                && string.Equals(parent.Name, AppDataFolderName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(contentDirectory), "current", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(parent.FullName, "Update.exe"))
                    ? parent.FullName
                    : contentDirectory;
        }
    }

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string SecretsPath => Path.Combine(AppDataDirectory, "secrets.dat");

    public static string LogPath => Path.Combine(AppDataDirectory, "app.log");

    public static string HistoryPath => Path.Combine(AppDataDirectory, "history.json");

    public static string AppsDirectory => Path.Combine(AppDataDirectory, "apps");

    public static string GetAppDirectory(string appId) => Path.Combine(AppsDirectory, appId);

    public static void EnsureCreated()
    {
        if (_suppressCreationForUserDataDeletion)
        {
            return;
        }

        Directory.CreateDirectory(AppDataDirectory);
        UserDataMigration.MigrateIfNeeded(
            AppDataDirectory,
            InstallCoupledAppDataDirectory,
            LegacyAppDataDirectory);
    }

    public static void DeleteForShutdown()
    {
        var expectedParent = Path.GetFullPath(Path.Combine(LocalAppDataDirectory, PublisherFolderName));
        var actualDirectory = Path.GetFullPath(AppDataDirectory);
        if (!string.Equals(
                Directory.GetParent(actualDirectory)?.FullName,
                expectedParent,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(actualDirectory), AppDataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hermes user data path validation failed.");
        }

        _suppressCreationForUserDataDeletion = true;
        try
        {
            if (Directory.Exists(actualDirectory))
            {
                Directory.Delete(actualDirectory, recursive: true);
            }
        }
        catch
        {
            _suppressCreationForUserDataDeletion = false;
            throw;
        }
    }
}
