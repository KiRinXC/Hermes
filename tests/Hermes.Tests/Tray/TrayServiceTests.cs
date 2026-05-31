using System.IO;

namespace Hermes.Tests.Tray;

public static class TrayServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("tray left click opens settings and menu no longer exposes history", LeftClickOpensSettingsAndMenuOmitsHistory);
        suite.Add("tray notification click opens settings", NotificationClickOpensSettings);
    }

    private static void LeftClickOpensSettingsAndMenuOmitsHistory()
    {
        var source = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Tray/TrayService.cs"));

        TestAssert.True(source.Contains("e.Button == Forms.MouseButtons.Left", StringComparison.Ordinal));
        TestAssert.True(source.Contains("SettingsRequested?.Invoke(this, EventArgs.Empty);", StringComparison.Ordinal));
        TestAssert.False(source.Contains("HistoryRequested += ", StringComparison.Ordinal));
        TestAssert.False(source.Contains("HistoryRequested?.Invoke", StringComparison.Ordinal));
    }

    private static void NotificationClickOpensSettings()
    {
        var traySource = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Tray/TrayService.cs"));
        var notificationSource = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Tray/TrayNotificationWindow.xaml.cs"));

        TestAssert.Contains("_notificationWindow.SettingsRequested +=", traySource);
        TestAssert.Contains("public event EventHandler? SettingsRequested", notificationSource);
        TestAssert.Contains("OpenSettingsFromNotification", notificationSource);
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
