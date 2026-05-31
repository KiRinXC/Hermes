using System.Diagnostics;
using System.Windows.Automation;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Settings;

namespace Hermes.Windows.Selection;

public sealed class ForegroundWindowService
{
    private readonly SettingsService _settingsService;

    public ForegroundWindowService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public ForegroundWindowInfo? GetForegroundWindowInfo()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        try
        {
            var process = Process.GetProcessById((int)processId);
            return new ForegroundWindowInfo(hwnd, process.Id, process.ProcessName, process.MainWindowTitle);
        }
        catch
        {
            return new ForegroundWindowInfo(hwnd, (int)processId, string.Empty, null);
        }
    }

    public bool IsExcluded(ForegroundWindowInfo? info)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.ProcessName))
        {
            return false;
        }

        var excluded = _settingsService.Current.Triggers.ExcludedApplications
            .Concat(_settingsService.Current.Triggers.SensitiveApplications);

        return excluded.Any(name => string.Equals(
            Normalize(name),
            Normalize(info.ProcessName),
            StringComparison.OrdinalIgnoreCase));
    }

    public bool IsFocusedElementSensitive()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            return element?.Current.IsPassword == true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> IsFocusedElementSensitiveAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        return Task.Run(IsFocusedElementSensitive);
    }

    public async Task<bool> IsFocusedElementSensitiveWithinAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var sensitiveTask = IsFocusedElementSensitiveAsync(cancellationToken);
        var completedTask = await Task.WhenAny(sensitiveTask, Task.Delay(timeout, cancellationToken));
        if (ReferenceEquals(completedTask, sensitiveTask))
        {
            return await sensitiveTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static string Normalize(string value)
    {
        return value.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value.Trim()[..^4]
            : value.Trim();
    }
}
