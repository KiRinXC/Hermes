using System.IO;
using Hermes.Windows.Infrastructure;

namespace Hermes.Tests.Infrastructure;

public static class UserDataMigrationTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("user data migration separates data from install files", SeparatesDataFromInstallFiles);
        suite.Add("user data migration keeps newer target values", KeepsExistingTargetValues);
        suite.Add("Hermes user data path is independent from install root", AppDataPathUsesPublisherDirectory);
    }

    private static void SeparatesDataFromInstallFiles()
    {
        var root = CreateTestRoot();
        try
        {
            var legacy = Path.Combine(root, "Hermes");
            var target = Path.Combine(root, "KiRinXC", "Hermes");
            Directory.CreateDirectory(Path.Combine(legacy, "current"));
            Directory.CreateDirectory(Path.Combine(legacy, "apps", "codex-auth-switch-sync"));
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "settings");
            File.WriteAllText(Path.Combine(legacy, "secrets.dat"), "secret");
            File.WriteAllText(Path.Combine(legacy, "apps", "codex-auth-switch-sync", "api.profile"), "profile");
            File.WriteAllText(Path.Combine(legacy, "current", "Hermes.Windows.exe"), "program");
            File.WriteAllText(Path.Combine(legacy, "Update.exe"), "updater");

            UserDataMigration.MigrateIfNeeded(target, legacy);

            TestAssert.Equal("settings", File.ReadAllText(Path.Combine(target, "settings.json")));
            TestAssert.Equal("secret", File.ReadAllText(Path.Combine(target, "secrets.dat")));
            TestAssert.Equal(
                "profile",
                File.ReadAllText(Path.Combine(target, "apps", "codex-auth-switch-sync", "api.profile")));
            TestAssert.True(File.Exists(Path.Combine(target, ".legacy-data-migrated")));
            TestAssert.False(File.Exists(Path.Combine(legacy, "settings.json")));
            TestAssert.False(Directory.Exists(Path.Combine(legacy, "apps")));
            TestAssert.True(File.Exists(Path.Combine(legacy, "current", "Hermes.Windows.exe")));
            TestAssert.True(File.Exists(Path.Combine(legacy, "Update.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void KeepsExistingTargetValues()
    {
        var root = CreateTestRoot();
        try
        {
            var legacy = Path.Combine(root, "legacy");
            var target = Path.Combine(root, "target");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy");
            File.WriteAllText(Path.Combine(target, "settings.json"), "current");

            UserDataMigration.MigrateIfNeeded(target, legacy);

            TestAssert.Equal("current", File.ReadAllText(Path.Combine(target, "settings.json")));
            TestAssert.False(File.Exists(Path.Combine(legacy, "settings.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AppDataPathUsesPublisherDirectory()
    {
        var expectedSuffix = Path.Combine("KiRinXC", "Hermes");
        TestAssert.True(AppPaths.AppDataDirectory.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase));
        TestAssert.False(string.Equals(
            AppPaths.AppDataDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hermes"),
            StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "Hermes.UserDataMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
