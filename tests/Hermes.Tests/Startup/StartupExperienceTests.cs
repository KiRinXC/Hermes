using System.IO;

namespace Hermes.Tests.Startup;

public static class StartupExperienceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("startup defers trigger hooks until after notification", DefersTriggerHooksUntilAfterNotification);
        suite.Add("startup migrates user data only in the primary instance", MigratesUserDataOnlyInPrimaryInstance);
        suite.Add("settings window is raised to foreground when opened from notification flows", SettingsWindowIsRaisedToForegroundWhenOpenedFromNotificationFlows);
    }

    private static void DefersTriggerHooksUntilAfterNotification()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));
        var balloonIndex = code.IndexOf("_trayService.ShowBalloon", StringComparison.Ordinal);
        var scheduleIndex = code.IndexOf("ScheduleResumeTriggersAfterStartup();", StringComparison.Ordinal);

        TestAssert.True(balloonIndex >= 0);
        TestAssert.True(scheduleIndex > balloonIndex);
        TestAssert.Contains("StartupTriggerDelay", code);
        TestAssert.Contains("DispatcherTimer", code);
    }

    private static void SettingsWindowIsRaisedToForegroundWhenOpenedFromNotificationFlows()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));

        TestAssert.Contains("BringSettingsWindowToFront();", code);
        TestAssert.Contains("_settingsWindow.WindowState == WindowState.Minimized", code);
        TestAssert.Contains("_settingsWindow.Topmost = true;", code);
        TestAssert.Contains("_settingsWindow.Activate();", code);
        TestAssert.Contains("_settingsWindow.Focus();", code);
        TestAssert.Contains("_settingsWindow.Topmost = false;", code);
    }

    private static void MigratesUserDataOnlyInPrimaryInstance()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));
        var guardIndex = code.IndexOf("_singleInstanceGuard = new SingleInstanceGuard();", StringComparison.Ordinal);
        var firstInstanceIndex = code.IndexOf("if (!_singleInstanceGuard.IsFirstInstance)", StringComparison.Ordinal);
        var migrationIndex = code.IndexOf("AppPaths.EnsureCreated();", StringComparison.Ordinal);

        TestAssert.True(guardIndex >= 0);
        TestAssert.True(firstInstanceIndex > guardIndex);
        TestAssert.True(migrationIndex > firstInstanceIndex);
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}
