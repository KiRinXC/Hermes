using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hermes.Windows.History;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;
using Hermes.Windows.UI.Themes;
using WpfInput = System.Windows.Input;

namespace Hermes.Windows.Shell;

public partial class SettingsWindow : Window
{
    private const string ApiKeyMask = "********";

    private readonly SettingsService _settingsService;
    private readonly ISecretStorageService _secretStorage;
    private readonly ITranslationService _translationService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly TranslationHistoryService _historyService;
    private readonly TriggerDiagnosticsService _triggerDiagnosticsService;
    private readonly AppLogger _logger;
    private bool _isLoadingSettings;
    private bool _apiKeyVisible;

    public SettingsWindow(
        SettingsService settingsService,
        ISecretStorageService secretStorage,
        ITranslationService translationService,
        StartupRegistrationService startupRegistrationService,
        TranslationHistoryService historyService,
        TriggerDiagnosticsService triggerDiagnosticsService,
        AppLogger logger)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _secretStorage = secretStorage;
        _translationService = translationService;
        _startupRegistrationService = startupRegistrationService;
        _historyService = historyService;
        _triggerDiagnosticsService = triggerDiagnosticsService;
        _logger = logger;
        InitializeOptionSources();
        LoadSettings();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshDiagnostics();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWindowChromeTheme();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        BeginEntranceAnimation();
        MoveThemeSegmentIndicator(animate: false);
    }

    private void InitializeOptionSources()
    {
        ProviderCombo.ItemsSource = SettingsWindowOptions.Providers;
        StyleCombo.ItemsSource = SettingsWindowOptions.TranslationStyles;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = _settingsService.Current;
            SelectComboValue(ProviderCombo, settings.Api.Provider);
            BaseUrlText.Text = settings.Api.BaseUrl;
            SetApiKeyHidden(_secretStorage.HasApiKey() ? ApiKeyMask : string.Empty);
            ModelText.Text = settings.Api.Model;

            SelectComboValue(StyleCombo, settings.Translation.Style);
            MaxCharsText.Text = settings.Translation.MaxCharacters.ToString();
            PreserveFormatCheck.IsChecked = settings.Translation.PreserveFormatting;
            PromptText.Text = string.IsNullOrWhiteSpace(settings.Translation.SystemPrompt)
                ? TranslationPromptBuilder.DefaultSystemPrompt
                : settings.Translation.SystemPrompt;

            AutoButtonCheck.IsChecked = settings.Triggers.AutoShowSelectionButton;
            SelectTheme(settings.Ui.Theme);
            PopupWidthSlider.Value = Math.Clamp(settings.Ui.PopupWidth, 320, 480);
            FontSizeSlider.Value = Math.Clamp(settings.Ui.FontSize, 12, 20);
            OpacitySlider.Value = Math.Clamp(settings.Ui.Opacity, 0.75, 1);
            UpdateAppearanceValueText();

            SaveHistoryCheck.IsChecked = settings.Privacy.SaveHistory;
            SaveOriginalCheck.IsChecked = settings.Privacy.SaveOriginalText;
            StartupCheck.IsChecked = settings.Startup.LaunchAtSignIn;
            ApiKeyStatusText.Text = _secretStorage.HasApiKey()
                ? "凭据已通过 Windows DPAPI 执行本机用户级加密存储。输入新值并保存即可替换。"
                : "尚未保存 API Key。凭据保存后会通过 Windows DPAPI 加密写入本机。";
            UpdateHeaderHotkey(settings.Triggers.Hotkey);
            RefreshDiagnostics();
        }
        finally
        {
            _isLoadingSettings = false;
            MoveThemeSegmentIndicator(animate: false);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "正在保存设置...");
        try
        {
            await SaveSettingsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<bool> SaveSettingsAsync()
    {
        try
        {
            if (!ValidateSettingsInputs(out var validationMessage))
            {
                StatusText.Text = validationMessage;
                return false;
            }

            var settings = _settingsService.Current;
            settings.Api.Provider = GetComboValue(ProviderCombo);
            settings.Api.BaseUrl = BaseUrlText.Text.Trim();
            settings.Api.Model = ModelText.Text.Trim();

            settings.Translation.Style = GetComboValue(StyleCombo);
            settings.Translation.PreserveFormatting = PreserveFormatCheck.IsChecked == true;
            settings.Translation.SystemPrompt = string.IsNullOrWhiteSpace(PromptText.Text)
                ? TranslationPromptBuilder.DefaultSystemPrompt
                : PromptText.Text.Trim();
            if (int.TryParse(MaxCharsText.Text, out var maxChars))
            {
                settings.Translation.MaxCharacters = Math.Clamp(maxChars, 100, 50000);
            }

            settings.Triggers.AutoShowSelectionButton = AutoButtonCheck.IsChecked == true;
            settings.Triggers.Hotkey = GetHeaderHotkeyText();
            settings.Ui.Theme = GetSelectedThemeValue();
            settings.Ui.PopupWidth = Math.Clamp(Math.Round(PopupWidthSlider.Value), 320, 480);
            settings.Ui.FontSize = Math.Clamp(Math.Round(FontSizeSlider.Value), 12, 20);
            settings.Ui.Opacity = Math.Clamp(OpacitySlider.Value, 0.75, 1);

            settings.Privacy.SaveHistory = SaveHistoryCheck.IsChecked == true;
            settings.Privacy.SaveOriginalText = SaveOriginalCheck.IsChecked == true;
            settings.Startup.LaunchAtSignIn = StartupCheck.IsChecked == true;

            var apiKeyInput = GetApiKeyInput();
            if (!string.IsNullOrWhiteSpace(apiKeyInput) && apiKeyInput != ApiKeyMask)
            {
                await _secretStorage.SaveApiKeyAsync(apiKeyInput);
                SetApiKeyHidden(ApiKeyMask);
            }

            await _settingsService.SaveAsync(settings);
            ThemeResourceService.Apply(settings.Ui.Theme);
            ApplyWindowChromeTheme();
            _startupRegistrationService.SetLaunchAtSignIn(settings.Startup.LaunchAtSignIn);
            StatusText.Text = "设置已保存。";
            LoadSettings();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Settings save failed.", ex);
            StatusText.Text = "保存失败，请查看日志。";
            return false;
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "正在测试连接...");
        SetTestButtonState(TestButtonState.Testing);
        try
        {
            if (!await SaveSettingsAsync())
            {
                return;
            }

            var result = await _translationService.TestConnectionAsync();
            StatusText.Text = result.Success ? "连接测试成功。" : result.UserMessage ?? "连接测试失败。";
            if (result.Success)
            {
                SetTestButtonState(TestButtonState.Success);
                await Task.Delay(1500);
            }
        }
        finally
        {
            SetBusy(false);
            SetTestButtonState(TestButtonState.Idle);
        }
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "正在清空历史...");
        try
        {
            await _historyService.ClearAsync();
            StatusText.Text = "历史已清空。";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnostics();
    }

    private void RefreshDiagnostics()
    {
        var recent = _triggerDiagnosticsService.GetRecent();
        DiagnosticsList.ItemsSource = recent;
        DiagnosticsEmptyText.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ValidateSettingsInputs(out string message)
    {
        message = string.Empty;

        if (!Uri.TryCreate(BaseUrlText.Text.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            message = "Base URL 需要是有效的 http 或 https 地址。";
            BaseUrlText.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(ModelText.Text))
        {
            message = "Model 不能为空。";
            ModelText.Focus();
            return false;
        }

        if (!HotkeyGesture.TryParse(GetHeaderHotkeyText(), out _))
        {
            message = "快捷键格式需要类似 Ctrl+Alt+E，并包含至少一个修饰键。";
            HeaderHotkeyButton.Focus();
            return false;
        }

        if (!int.TryParse(MaxCharsText.Text, out var maxChars) || maxChars is < 100 or > 50000)
        {
            message = "最大字符数需要在 100 到 50000 之间。";
            MaxCharsText.Focus();
            return false;
        }

        if (!_secretStorage.HasApiKey() && string.IsNullOrWhiteSpace(GetApiKeyInput()))
        {
            message = "请先填写 API Key，或保存已有的本机加密 Key。";
            ApiKeyBox.Focus();
            return false;
        }

        return true;
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        TestButton.IsEnabled = !isBusy;
        AdvancedClearHistoryButton.IsEnabled = !isBusy;
        SaveButton.IsEnabled = !isBusy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusText.Text = message;
        }
    }

    private void ApplyWindowChromeTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var useDark = 1;
            var result = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmwaUseImmersiveDarkMode,
                ref useDark,
                sizeof(int));
            if (result != 0)
            {
                _ = NativeMethods.DwmSetWindowAttribute(
                    hwnd,
                    NativeMethods.DwmwaUseImmersiveDarkModeBefore20H1,
                    ref useDark,
                    sizeof(int));
            }

            var backdrop = NativeMethods.DwmSystemBackdropTypeMica;
            _ = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DwmwaSystemBackdropType,
                ref backdrop,
                sizeof(int));
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not apply settings window chrome theme. {ex.Message}");
        }
    }

    private void BeginEntranceAnimation()
    {
        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));

        var ease = new BackEase
        {
            EasingMode = EasingMode.EaseOut,
            Amplitude = 0.35
        };
        var scaleX = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease
        };
        var scaleY = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = ease
        };
        RootShellScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        RootShellScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (e.ButtonState == WpfInput.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ApiKeyReveal_Click(object sender, RoutedEventArgs e)
    {
        if (!_apiKeyVisible)
        {
            var password = ApiKeyBox.Password == ApiKeyMask && _secretStorage.HasApiKey()
                ? await _secretStorage.GetApiKeyAsync() ?? string.Empty
                : ApiKeyBox.Password;
            ApiKeyRevealText.Text = password;
            ApiKeyRevealText.Visibility = Visibility.Visible;
            ApiKeyBox.Visibility = Visibility.Collapsed;
            _apiKeyVisible = true;
            return;
        }

        SetApiKeyHidden(ApiKeyRevealText.Text);
    }

    private void SetApiKeyHidden(string password)
    {
        _apiKeyVisible = false;
        ApiKeyBox.Password = password;
        ApiKeyRevealText.Text = password == ApiKeyMask ? string.Empty : password;
        ApiKeyRevealText.Visibility = Visibility.Collapsed;
        ApiKeyBox.Visibility = Visibility.Visible;
    }

    private string GetApiKeyInput()
    {
        return _apiKeyVisible ? ApiKeyRevealText.Text.Trim() : ApiKeyBox.Password.Trim();
    }

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            ApplyLocalTheme(GetSelectedThemeValue());
            MoveThemeSegmentIndicator(animate: true);
        }
    }

    private void AppearanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoadingSettings)
        {
            UpdateAppearanceValueText();
        }
    }

    private void HeaderHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        HeaderHotkeyButton.Focus();
        StatusText.Text = "请按下新的快捷键组合。";
    }

    private void HotkeyRecorder_LostKeyboardFocus(object sender, WpfInput.KeyboardFocusChangedEventArgs e)
    {
        HeaderHotkeyButton.Effect = null;
    }

    private void HotkeyRecorder_PreviewKeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        var key = e.Key == WpfInput.Key.System ? e.SystemKey : e.Key;
        if (key == WpfInput.Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = WpfInput.Keyboard.Modifiers;
        var parts = new List<string>();
        if (modifiers.HasFlag(WpfInput.ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(WpfInput.ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (parts.Count == 0)
        {
            StatusText.Text = "快捷键需要包含 Ctrl、Alt、Shift 或 Win 中至少一个修饰键。";
            e.Handled = true;
            return;
        }

        parts.Add(FormatKey(key));
        var hotkey = string.Join("+", parts);
        UpdateHeaderHotkey(hotkey);
        StatusText.Text = "快捷键已录制，保存后生效。";
        e.Handled = true;
    }

    private void UpdateAppearanceValueText()
    {
        if (PopupWidthValueText is null || FontSizeValueText is null || OpacityValueText is null)
        {
            return;
        }

        PopupWidthValueText.Text = $"{Math.Round(PopupWidthSlider.Value):0} px";
        FontSizeValueText.Text = $"{Math.Round(FontSizeSlider.Value):0} pt";
        OpacityValueText.Text = $"{OpacitySlider.Value:P0}";
    }

    private void SelectTheme(string value)
    {
        ThemeSystemRadio.IsChecked = string.Equals(value, "System", StringComparison.OrdinalIgnoreCase);
        ThemeDarkRadio.IsChecked = string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase);
        ThemeLightRadio.IsChecked = string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase);
        if (ThemeSystemRadio.IsChecked != true && ThemeDarkRadio.IsChecked != true && ThemeLightRadio.IsChecked != true)
        {
            ThemeSystemRadio.IsChecked = true;
        }

        ApplyLocalTheme(GetSelectedThemeValue());
    }

    private string GetSelectedThemeValue()
    {
        if (ThemeDarkRadio.IsChecked == true)
        {
            return "Dark";
        }

        if (ThemeLightRadio.IsChecked == true)
        {
            return "Light";
        }

        return "System";
    }

    private int GetSelectedThemeIndex()
    {
        return GetSelectedThemeValue() switch
        {
            "Dark" => 1,
            "Light" => 2,
            _ => 0
        };
    }

    private void MoveThemeSegmentIndicator(bool animate)
    {
        if (ThemeSegmentIndicator.Parent is not FrameworkElement parent || parent.ActualWidth <= 0)
        {
            return;
        }

        var target = parent.ActualWidth / 3 * GetSelectedThemeIndex();
        if (!animate)
        {
            ThemeSegmentTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            ThemeSegmentTransform.X = target;
            return;
        }

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ThemeSegmentTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
    }

    private void UpdateHeaderHotkey(string hotkey)
    {
        HeaderHotkeyButton.Tag = hotkey;
        HeaderHotkeyKeys.Children.Clear();
        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var keycap = new Border
            {
                MinWidth = 26,
                Height = 18,
                Margin = new Thickness(HeaderHotkeyKeys.Children.Count == 0 ? 0 : 4, 0, 0, 0),
                Padding = new Thickness(6, 0, 6, 1),
                CornerRadius = new CornerRadius(4),
                Background = FindResource("Settings.KeycapBrush") as System.Windows.Media.Brush,
                BorderBrush = FindResource("Settings.KeycapBorderBrush") as System.Windows.Media.Brush,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = part,
                    FontSize = 10,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
                    Foreground = FindResource("Settings.TextMutedBrush") as System.Windows.Media.Brush,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            HeaderHotkeyKeys.Children.Add(keycap);
        }
    }

    private string GetHeaderHotkeyText()
    {
        return HeaderHotkeyButton.Tag as string ?? _settingsService.Current.Triggers.Hotkey;
    }

    private void ApplyLocalTheme(string theme)
    {
        var palette = SettingsWindowThemePalettes.For(
            string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase)
                ? (ThemeResourceService.ShouldUseDarkTheme(theme) ? "Dark" : "Light")
                : theme);

        SetSolidBrush("Settings.ShellBrush", palette.Shell);
        SetSolidBrush("Settings.TextPrimaryBrush", palette.TextPrimary);
        SetSolidBrush("Settings.TextSecondaryBrush", palette.TextSecondary);
        SetSolidBrush("Settings.TextMutedBrush", palette.TextMuted);
        SetSolidBrush("Settings.CardBrush", palette.Card);
        SetSolidBrush("Settings.ControlBrush", palette.Control);
        SetSolidBrush("Settings.ControlStrongBrush", palette.ControlStrong);
        SetSolidBrush("Settings.BorderBrush", palette.Border);
        SetSolidBrush("Settings.DividerBrush", palette.Divider);
        SetSolidBrush("Settings.FaintDividerBrush", palette.FaintDivider);
        SetSolidBrush("Settings.KeycapBrush", palette.Keycap);
        SetSolidBrush("Settings.KeycapBorderBrush", palette.KeycapBorder);
        SetSolidBrush("Settings.ToggleTrackBrush", palette.ToggleTrack);
        SetSolidBrush("Settings.SliderTrackBrush", palette.SliderTrack);
        SetSolidBrush("Settings.SliderThumbBrush", palette.SliderThumb);
        SetSolidBrush("Settings.AccentBrush", palette.Accent);
        SetSolidBrush("Settings.SuccessBrush", palette.Success);
        SetSolidBrush("Settings.WarningBrush", palette.Warning);
        SetSolidBrush("Settings.BlueSoftBrush", palette.BlueSoft);

        if (FindResource("Settings.OuterStrokeBrush") is LinearGradientBrush outerStroke)
        {
            outerStroke.GradientStops[0].Color = palette.OuterStrokeTop;
            outerStroke.GradientStops[1].Color = palette.OuterStrokeBottom;
        }
    }

    private void SetSolidBrush(string resourceKey, System.Windows.Media.Color color)
    {
        Resources[resourceKey] = new SolidColorBrush(color);
    }

    private void SetTestButtonState(TestButtonState state)
    {
        TestButtonIdleContent.Visibility = state == TestButtonState.Idle ? Visibility.Visible : Visibility.Collapsed;
        TestButtonBusyContent.Visibility = state == TestButtonState.Testing ? Visibility.Visible : Visibility.Collapsed;
        TestButtonSuccessContent.Visibility = state == TestButtonState.Success ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string GetComboValue(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedValue is string selectedValue)
        {
            return selectedValue;
        }

        return (comboBox.SelectedItem as SettingsOption)?.Value ?? string.Empty;
    }

    private static void SelectComboValue(System.Windows.Controls.ComboBox comboBox, string value)
    {
        comboBox.SelectedValue = value;
        if (comboBox.SelectedItem is null && comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static bool IsModifierKey(WpfInput.Key key)
    {
        return key is WpfInput.Key.LeftCtrl
            or WpfInput.Key.RightCtrl
            or WpfInput.Key.LeftAlt
            or WpfInput.Key.RightAlt
            or WpfInput.Key.LeftShift
            or WpfInput.Key.RightShift
            or WpfInput.Key.LWin
            or WpfInput.Key.RWin;
    }

    private static string FormatKey(WpfInput.Key key)
    {
        return key.ToString();
    }

    private enum TestButtonState
    {
        Idle,
        Testing,
        Success
    }
}
