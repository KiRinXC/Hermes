using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Overlay;
using Forms = System.Windows.Forms;

namespace Hermes.Windows.Tray;

public partial class TrayNotificationWindow : Window
{
    private readonly DispatcherTimer _dismissTimer;

    public TrayNotificationWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.6) };
        _dismissTimer.Tick += (_, _) => FadeOutAndClose();
        AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Notification_MouseLeftButtonUp), true);
    }

    public event EventHandler? SettingsRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, -20, new IntPtr(style | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow));
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        PositionNearTray();
        AnimateIn();
        _dismissTimer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        FadeOutAndClose();
    }

    private void Notification_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsCloseButtonClick(e.OriginalSource))
        {
            return;
        }

        OpenSettingsFromNotification();
        e.Handled = true;
    }

    private void OpenSettingsFromNotification()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        FadeOutAndClose();
    }

    private void PositionNearTray()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        if (!screen.Bounds.Contains(Forms.Cursor.Position))
        {
            screen = Forms.Screen.PrimaryScreen ?? screen;
        }

        var area = screen.WorkingArea;
        var anchor = new System.Drawing.Point(area.Right - 1, area.Bottom - 1);
        var scale = DpiAwareScreen.GetScaleForPhysicalPoint(anchor);
        var size = DpiAwareScreen.ToPhysicalSize(ActualWidth, ActualHeight, anchor);
        DpiAwareScreen.SetWindowPositionPhysical(
            this,
            area.Right - size.Width - (18 * scale.ScaleX),
            area.Bottom - size.Height - (18 * scale.ScaleY));
    }

    private void AnimateIn()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        AnimateScale(1);
    }

    private void FadeOutAndClose()
    {
        _dismissTimer.Stop();
        AnimateScale(0.98);
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void AnimateScale(double scale)
    {
        if (RootBorder.RenderTransform is not ScaleTransform transform)
        {
            return;
        }

        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static bool IsCloseButtonClick(object source)
    {
        if (source is not DependencyObject current)
        {
            return false;
        }

        while (current is not null)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
