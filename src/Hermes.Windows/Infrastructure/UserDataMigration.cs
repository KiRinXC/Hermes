using System.IO;

namespace Hermes.Windows.Infrastructure;

internal static class UserDataMigration
{
    private const string MigrationMarkerName = ".legacy-data-migrated";

    private static readonly string[] DataFileNames =
    [
        "settings.json",
        "secrets.dat",
        "history.json",
        "app.log"
    ];

    private static readonly string[] DataDirectoryNames = ["apps"];

    public static void MigrateIfNeeded(string targetDirectory, params string[] legacyDirectories)
    {
        Directory.CreateDirectory(targetDirectory);
        var markerPath = Path.Combine(targetDirectory, MigrationMarkerName);
        if (File.Exists(markerPath))
        {
            return;
        }

        foreach (var legacyDirectory in legacyDirectories)
        {
            if (!Directory.Exists(legacyDirectory)
                || PathsEqual(legacyDirectory, targetDirectory))
            {
                continue;
            }

            MigrateDirectory(legacyDirectory, targetDirectory);
        }

        File.WriteAllText(markerPath, "Hermes user data is stored independently from the application installation.");
    }

    private static void MigrateDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (var fileName in DataFileNames)
        {
            var sourcePath = Path.Combine(sourceDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var targetPath = Path.Combine(targetDirectory, fileName);
            if (!File.Exists(targetPath))
            {
                File.Copy(sourcePath, targetPath);
            }
        }

        foreach (var directoryName in DataDirectoryNames)
        {
            var sourcePath = Path.Combine(sourceDirectory, directoryName);
            if (Directory.Exists(sourcePath))
            {
                CopyMissingDirectory(sourcePath, Path.Combine(targetDirectory, directoryName));
            }
        }

        foreach (var fileName in DataFileNames)
        {
            var sourcePath = Path.Combine(sourceDirectory, fileName);
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }

        foreach (var directoryName in DataDirectoryNames)
        {
            var sourcePath = Path.Combine(sourceDirectory, directoryName);
            if (Directory.Exists(sourcePath))
            {
                Directory.Delete(sourcePath, recursive: true);
            }
        }
    }

    private static void CopyMissingDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            if (!File.Exists(targetPath))
            {
                File.Copy(file, targetPath);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyMissingDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
