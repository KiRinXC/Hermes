using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Text;
using Hermes.Windows.Infrastructure;
using Forms = System.Windows.Forms;
using WpfBrush = System.Windows.Media.Brush;
using WpfClipboard = System.Windows.Clipboard;

namespace Hermes.Windows.Overlay;

public partial class TranslationPopupWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _copyResetTimer;
    private readonly System.Windows.Threading.DispatcherTimer _sizePersistTimer;
    private readonly StringBuilder _streamingText = new();
    private string? _translatedText;
    private string? _sourcePreview;
    private string? _loadingStateText;
    private bool _isPinned;
    private bool _clamping;
    private bool _isClosing;
    private bool _hasStreamingText;
    private double _targetOpacity = 1;

    public TranslationPopupWindow()
    {
        InitializeComponent();
        _copyResetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _copyResetTimer.Tick += (_, _) =>
        {
            _copyResetTimer.Stop();
            CopyLabel.Text = "复制译文";
            CopyButton.Foreground = (WpfBrush)FindResource("Brush.TextSecondary");
        };
        _sizePersistTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _sizePersistTimer.Tick += (_, _) =>
        {
            _sizePersistTimer.Stop();
            if (IsLoaded && ActualWidth > 0 && ActualHeight > 0)
            {
                SizeChangedByUser?.Invoke(this, new PopupSizeChangedEventArgs(ActualWidth, ActualHeight));
            }
        };
    }

    public bool IsPinned => _isPinned;

    public bool HasCompletedTranslation => !string.IsNullOrEmpty(_translatedText);

    public event EventHandler? RetryRequested;

    public event EventHandler? ClosedByUser;

    public event EventHandler? SettingsRequested;

    public event EventHandler<PopupSizeChangedEventArgs>? SizeChangedByUser;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, -20).ToInt64();
        NativeMethods.SetWindowLongPtr(hwnd, -20, new IntPtr(style | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow));
        HwndSource.FromHwnd(hwnd)?.AddHook(WindowMessageHook);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        AnimateIn();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        ClampToScreen();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateBodyScrollHeight();
        if (IsLoaded && sizeInfo.WidthChanged || IsLoaded && sizeInfo.HeightChanged)
        {
            _sizePersistTimer.Stop();
            _sizePersistTimer.Start();
        }
    }

    public void ApplyTheme(bool dark, double opacity, double fontSize)
    {
        _targetOpacity = Math.Clamp(opacity, 0.75, 1);
        Opacity = _targetOpacity;
        var appliedFontSize = Math.Clamp(fontSize, 12, 20);
        BodyText.FontSize = appliedFontSize;
        BodyText.LineHeight = appliedFontSize * 1.58;
        SourcePreviewText.FontSize = appliedFontSize;
        SourcePreviewText.LineHeight = appliedFontSize * 1.45;
        var screen = DpiAwareScreen.GetWorkingAreaDip(Forms.Cursor.Position);
        MaxHeight = Math.Min(720, screen.Height * 0.86);
        UpdateBodyScrollHeight();
    }

    public void SetSourcePreview(string text)
    {
        _sourcePreview = text;
        SourceContainer.Visibility = Visibility.Collapsed;
        RenderSourcePreview();
    }

    public void SetLoading(string? stateText = null, string? bodyText = null)
    {
        _translatedText = null;
        _loadingStateText = string.IsNullOrWhiteSpace(stateText) ? "正在翻译..." : stateText;
        _streamingText.Clear();
        _hasStreamingText = false;
        CopyButton.IsEnabled = false;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        BodyScroll.Visibility = Visibility.Visible;
        ProgressRail.Visibility = Visibility.Visible;
        ProgressRail.BeginAnimation(OpacityProperty, null);
        ProgressRail.Opacity = 1;
        StateText.Text = _loadingStateText;
        StateText.Foreground = (WpfBrush)FindResource("Brush.TextMuted");
        RenderTranslatedText(string.IsNullOrWhiteSpace(bodyText) ? "正在翻译" : bodyText);
        StartProgressAnimation();
    }

    public void SetLongRunning()
    {
        if (_translatedText is not null || _hasStreamingText)
        {
            return;
        }

        StateText.Text = _loadingStateText ?? "仍在处理...";
        BodyScroll.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Visible;
        StartSpinnerAnimation();
    }

    public void SetTranslation(string translatedText)
    {
        _translatedText = translatedText;
        _loadingStateText = null;
        _streamingText.Clear();
        _hasStreamingText = false;
        CopyButton.IsEnabled = true;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyScroll.Visibility = Visibility.Visible;
        FadeOutProgress();
        StateText.Text = "译文";
        StateText.Foreground = (WpfBrush)FindResource("Brush.TextMuted");
        RenderTranslatedText(translatedText);
        AnimateBodyReveal();
    }

    public void AppendTranslationDelta(string deltaText)
    {
        if (string.IsNullOrEmpty(deltaText))
        {
            return;
        }

        _streamingText.Append(deltaText);
        _hasStreamingText = true;
        CopyButton.IsEnabled = false;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyScroll.Visibility = Visibility.Visible;
        _loadingStateText ??= "正在翻译...";
        StateText.Text = _loadingStateText;
        StateText.Foreground = (WpfBrush)FindResource("Brush.TextMuted");
        RenderTranslatedText(_streamingText.ToString());
    }

    public void CompleteStreamingTranslation(string translatedText)
    {
        SetTranslation(translatedText);
    }

    public void SetError(string message, bool showSettings)
    {
        _translatedText = null;
        _loadingStateText = null;
        _streamingText.Clear();
        _hasStreamingText = false;
        CopyButton.IsEnabled = false;
        SettingsButton.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyScroll.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        FadeOutProgress();
        StateText.Text = "无法翻译";
        StateText.Foreground = (WpfBrush)FindResource("Brush.Danger");
        ErrorText.Text = $"Hermes 提示：{message}";
    }

    public void CloseWithFade()
    {
        FadeOutAndClose();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_translatedText))
        {
            return;
        }

        WpfClipboard.SetText(_translatedText);
        MarkCopied();
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = PinButton.IsChecked == true;
        Topmost = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ClosedByUser?.Invoke(this, EventArgs.Empty);
        CloseWithFade();
    }

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInteractiveDragSource(e.OriginalSource))
        {
            return;
        }

        DragMove();
    }

    internal static bool IsInteractiveDragSource(object? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.Primitives.TextBoxBase
                or PasswordBox
                or System.Windows.Controls.ComboBox
                or Slider
                or System.Windows.Controls.Primitives.ScrollBar
                or Thumb
                or Hyperlink)
            {
                return true;
            }

            current = current switch
            {
                FrameworkElement element => element.Parent ?? VisualTreeHelper.GetParent(element),
                FrameworkContentElement contentElement => contentElement.Parent,
                DependencyObject dependencyObject => VisualTreeHelper.GetParent(dependencyObject),
                _ => null
            };
        }

        return false;
    }

    private void RenderSourcePreview()
    {
        SourcePreviewText.Text = _sourcePreview ?? string.Empty;
    }

    private void RenderTranslatedText(string text)
    {
        PopupMarkdownRenderer.Render(BodyText, text, ResolveBrushResource);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var result = WindowResizeHitTest.HitTest(this, lParam);
        if (result == new IntPtr(NativeMethods.HtClient))
        {
            return IntPtr.Zero;
        }

        handled = true;
        return result;
    }

    private void UpdateBodyScrollHeight()
    {
        var availableHeight = (ActualHeight > 0 ? ActualHeight : Height) - 150;
        BodyScroll.MaxHeight = Math.Max(80, availableHeight);
    }

    private WpfBrush ResolveBrushResource(string key)
    {
        return (WpfBrush)FindResource(key);
    }

    private void MarkCopied()
    {
        CopyLabel.Text = "已复制 ✓";
        CopyButton.Foreground = (WpfBrush)FindResource("Brush.Success");
        _copyResetTimer.Stop();
        _copyResetTimer.Start();
    }

    private void ClampToScreen()
    {
        if (_clamping || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        try
        {
            _clamping = true;
            DpiAwareScreen.ClampWindowToScreen(this);
        }
        finally
        {
            _clamping = false;
        }
    }

    private void AnimateIn()
    {
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(_targetOpacity, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        if (Card.RenderTransform is TranslateTransform translate)
        {
            translate.Y = 10;
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }

    private void FadeOutAndClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        if (Card.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
        }

        var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, animation);
    }

    private void StartProgressAnimation()
    {
        ProgressTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-124, 360, TimeSpan.FromMilliseconds(950))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private void FadeOutProgress()
    {
        ProgressTransform.BeginAnimation(TranslateTransform.XProperty, null);
        ProgressRail.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void StartSpinnerAnimation()
    {
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
        {
            RepeatBehavior = RepeatBehavior.Forever
        });
    }

    private void AnimateBodyReveal()
    {
        BodyText.Opacity = 0;
        BodyTextTransform.Y = 2;
        BodyText.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(80))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        BodyTextTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(80))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}

public sealed class PopupSizeChangedEventArgs : EventArgs
{
    public PopupSizeChangedEventArgs(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}
