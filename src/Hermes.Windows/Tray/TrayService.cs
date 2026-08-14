using System.Drawing;
using Forms = System.Windows.Forms;

namespace Hermes.Windows.Tray;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private TrayMenuWindow? _menuWindow;
    private TrayNotificationWindow? _notificationWindow;
    private bool _paused;

    public TrayService()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Hermes",
            Icon = LoadAppIcon()
        };
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
    }

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? TranslateClipboardRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _notifyIcon.Text = _paused ? "Hermes（已暂停）" : "Hermes";
    }

    public void ShowBalloon(string title, string message, Action? activatedAction = null)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationWindow?.Close();
            _notificationWindow = new TrayNotificationWindow(title, message);
            _notificationWindow.SettingsRequested += (_, _) =>
            {
                if (activatedAction is not null)
                {
                    activatedAction();
                    return;
                }

                SettingsRequested?.Invoke(this, EventArgs.Empty);
            };
            _notificationWindow.Closed += (_, _) => _notificationWindow = null;
            _notificationWindow.Show();
        });
    }

    public void Dispose()
    {
        _menuWindow?.Close();
        _notificationWindow?.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private void NotifyIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Button != Forms.MouseButtons.Right)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _menuWindow?.Close();
            _menuWindow = new TrayMenuWindow(_paused);
            _menuWindow.PauseResumeRequested += (_, _) => PauseResumeRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.TranslateClipboardRequested += (_, _) => TranslateClipboardRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.Closed += (_, _) => _menuWindow = null;
            _menuWindow.Show();
            _menuWindow.Activate();
        });
    }

    private static Icon LoadAppIcon()
    {
        var streamInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/AppIcon.ico"));
        return streamInfo is null ? SystemIcons.Application : new Icon(streamInfo.Stream);
    }
}
