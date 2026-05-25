using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hermes.Windows.Infrastructure;
using Forms = System.Windows.Forms;
using WpfBrush = System.Windows.Media.Brush;
using WpfClipboard = System.Windows.Clipboard;

namespace Hermes.Windows.Overlay;

public partial class TranslationPopupWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _copyResetTimer;
    private string? _translatedText;
    private string? _sourcePreview;
    private bool _isPinned;
    private bool _clamping;
    private bool _isClosing;
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
    }

    public bool IsPinned => _isPinned;

    public event EventHandler? RetryRequested;

    public event EventHandler? ClosedByUser;

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
        AnimateIn();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        ClampToScreen();
    }

    public void ApplyTheme(bool dark, double opacity, double fontSize)
    {
        _targetOpacity = Math.Clamp(opacity, 0.75, 1);
        Opacity = _targetOpacity;
        var appliedFontSize = Math.Clamp(fontSize, 13, 16);
        BodyText.FontSize = appliedFontSize;
        BodyText.LineHeight = appliedFontSize * 1.58;
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        MaxHeight = screen.Height * 0.6;
        BodyScroll.MaxHeight = Math.Max(140, MaxHeight - 150);
    }

    public void SetSourcePreview(string text)
    {
        _sourcePreview = text;
        SourceContainer.Visibility = Visibility.Collapsed;
        RenderSourcePreview();
    }

    public void SetLoading()
    {
        _translatedText = null;
        CopyButton.IsEnabled = false;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        BodyScroll.Visibility = Visibility.Visible;
        ProgressRail.Visibility = Visibility.Visible;
        ProgressRail.BeginAnimation(OpacityProperty, null);
        ProgressRail.Opacity = 1;
        StateText.Text = "Hermes 正在转译...";
        StateText.Foreground = (WpfBrush)FindResource("Brush.TextMuted");
        BodyText.Inlines.Clear();
        BodyText.Inlines.Add(new Run("正在翻译"));
        StartProgressAnimation();
    }

    public void SetLongRunning()
    {
        if (_translatedText is not null)
        {
            return;
        }

        StateText.Text = "仍在处理...";
        BodyScroll.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Visible;
        StartSpinnerAnimation();
    }

    public void SetTranslation(string translatedText)
    {
        _translatedText = translatedText;
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

    public void SetError(string message, bool showSettings)
    {
        _translatedText = null;
        CopyButton.IsEnabled = false;
        SettingsButton.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        LongRunningPanel.Visibility = Visibility.Collapsed;
        SpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        BodyScroll.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        FadeOutProgress();
        StateText.Text = "无法翻译";
        StateText.Foreground = (WpfBrush)FindResource("Brush.Danger");
        ErrorText.Text = "Hermes 提示：" + message;
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

    private void DragSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void RenderSourcePreview()
    {
        SourcePreviewText.Text = _sourcePreview ?? string.Empty;
    }

    private void RenderTranslatedText(string text)
    {
        BodyText.Inlines.Clear();
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            AppendMarkdownLine(lines[i]);
            if (i < lines.Length - 1)
            {
                BodyText.Inlines.Add(new LineBreak());
            }
        }
    }

    private void AppendMarkdownLine(string line)
    {
        var cursor = 0;
        while (cursor < line.Length)
        {
            var start = line.IndexOf("**", cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                BodyText.Inlines.Add(new Run(line[cursor..]));
                return;
            }

            if (start > cursor)
            {
                BodyText.Inlines.Add(new Run(line[cursor..start]));
            }

            var end = line.IndexOf("**", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                BodyText.Inlines.Add(new Run(line[start..]));
                return;
            }

            BodyText.Inlines.Add(new Run(line[(start + 2)..end]) { FontWeight = FontWeights.SemiBold });
            cursor = end + 2;
        }
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
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top)).WorkingArea;
            Left = Math.Min(Math.Max(Left, screen.Left + 8), screen.Right - ActualWidth - 8);
            Top = Math.Min(Math.Max(Top, screen.Top + 8), screen.Bottom - ActualHeight - 8);
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
