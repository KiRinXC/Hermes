using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hermes.Windows.Infrastructure;
using Forms = System.Windows.Forms;

namespace Hermes.Windows.Tray;

public partial class TrayMenuWindow : Window
{
    private bool _isExecutingCommand;

    public TrayMenuWindow(bool paused)
    {
        InitializeComponent();
        StateText.Text = paused ? "已暂停" : "正在运行";
        PauseText.Text = paused ? "开启划词翻译" : "暂停划词翻译";
    }

    public event EventHandler? PauseResumeRequested;

    public event EventHandler? TranslateClipboardRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? HistoryRequested;

    public event EventHandler? ExitRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, -20, new IntPtr(style | NativeMethods.WsExToolWindow));
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        PositionNearCursor();
        AnimateIn();
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e) => InvokeAndClose(PauseResumeRequested);

    private void TranslateClipboard_Click(object sender, RoutedEventArgs e) => InvokeAndClose(TranslateClipboardRequested);

    private void Settings_Click(object sender, RoutedEventArgs e) => InvokeAndClose(SettingsRequested);

    private void History_Click(object sender, RoutedEventArgs e) => InvokeAndClose(HistoryRequested);

    private void Exit_Click(object sender, RoutedEventArgs e) => InvokeAndClose(ExitRequested);

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_isExecutingCommand)
        {
            Close();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void InvokeAndClose(EventHandler? handler)
    {
        if (_isExecutingCommand)
        {
            return;
        }

        _isExecutingCommand = true;
        Closed += OnClosed;
        Close();

        void OnClosed(object? sender, EventArgs e)
        {
            Closed -= OnClosed;
            Dispatcher.BeginInvoke(new Action(() => handler?.Invoke(this, EventArgs.Empty)));
        }
    }

    private void PositionNearCursor()
    {
        var cursor = Forms.Cursor.Position;
        var area = Forms.Screen.FromPoint(cursor).WorkingArea;
        var left = cursor.X - ActualWidth + 8;
        var top = cursor.Y - ActualHeight - 8;

        Left = Math.Min(Math.Max(left, area.Left + 8), area.Right - ActualWidth - 8);
        Top = Math.Min(Math.Max(top, area.Top + 8), area.Bottom - ActualHeight - 8);
    }

    private void AnimateIn()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        if (RootBorder.RenderTransform is not ScaleTransform transform)
        {
            return;
        }

        var animation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
