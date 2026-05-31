using Hermes.Windows.Infrastructure;
using Microsoft.Win32;

namespace Hermes.Windows.Settings;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hermes";
    private const string LegacyValueName = "AITranslator";
    private readonly AppLogger? _logger;

    public StartupRegistrationService(AppLogger? logger = null)
    {
        _logger = logger;
    }

    public void SetLaunchAtSignIn(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(ValueName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            _logger?.Warning($"Startup registration update failed. {ex.Message}");
        }
    }
}
