using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Selection;

namespace Hermes.Windows.Overlay;

public partial class FloatingButtonWindow : Window
{
    private readonly DispatcherTimer _dismissTimer;

    public FloatingButtonWindow(SelectionCandidate candidate, bool useDarkIcon, bool pointToTextAbove)
    {
        InitializeComponent();
        Candidate = candidate;
        LightIcon.Visibility = useDarkIcon ? Visibility.Collapsed : Visibility.Visible;
        DarkIcon.Visibility = useDarkIcon ? Visibility.Visible : Visibility.Collapsed;
        IconOrientationTransform.ScaleY = pointToTextAbove ? -1 : 1;
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _dismissTimer.Tick += (_, _) => FadeOutAndClose();
    }

    public SelectionCandidate Candidate { get; }

    public event EventHandler<SelectionCandidate>? TranslateRequested;

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
        AnimateIn();
        _dismissTimer.Start();
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        _dismissTimer.Stop();
        TranslateButton.IsEnabled = false;
        var candidate = Candidate;
        Close();
        Dispatcher.BeginInvoke(
            new Action(() => TranslateRequested?.Invoke(this, candidate)),
            DispatcherPriority.Background);
    }

    private void TranslateButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _dismissTimer.Stop();
        AnimateScale(1.03);
    }

    private void TranslateButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateScale(1);
        _dismissTimer.Start();
    }

    private void TranslateButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        AnimateScale(0.97);
    }

    private void TranslateButton_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        AnimateScale(1.03);
    }

    private void AnimateIn()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        AnimateScale(1);
    }

    private void FadeOutAndClose()
    {
        _dismissTimer.Stop();
        AnimateScale(0.98);
        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120))
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

        var animation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }
}
