using System.IO;

namespace Hermes.Windows.Infrastructure;

public static class AppPaths
{
    private const string AppDataFolderName = "Hermes";
    private const string LegacyAppDataFolderName = "AITranslator";

    private static string LocalAppDataDirectory { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string AppDataDirectory { get; } = Path.Combine(
        LocalAppDataDirectory,
        AppDataFolderName);

    private static string LegacyAppDataDirectory { get; } = Path.Combine(
        LocalAppDataDirectory,
        LegacyAppDataFolderName);

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string SecretsPath => Path.Combine(AppDataDirectory, "secrets.dat");

    public static string LogPath => Path.Combine(AppDataDirectory, "app.log");

    public static string HistoryPath => Path.Combine(AppDataDirectory, "history.json");

    public static void EnsureCreated()
    {
        if (!Directory.Exists(AppDataDirectory) && Directory.Exists(LegacyAppDataDirectory))
        {
            CopyDirectory(LegacyAppDataDirectory, AppDataDirectory);
            return;
        }

        Directory.CreateDirectory(AppDataDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }
}
