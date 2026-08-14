using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hermes.Windows.Apps;
using Hermes.Windows.Apps.Contracts;
using Hermes.Windows.History;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;
using Hermes.Windows.UI.Themes;
using WpfInput = System.Windows.Input;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace Hermes.Windows.Shell;

public partial class SettingsWindow : Window, IHermesConfirmationHost
{
    private const string ApiKeyMask = "********";
    private const double ShellCornerRadius = 14;
    private static readonly double[] PopupFontSizeValues = [12, 14, 16, 18, 20];

    private readonly SettingsService _settingsService;
    private readonly ISecretStorageService _secretStorage;
    private readonly ITranslationService _translationService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly TranslationHistoryService _historyService;
    private readonly TriggerDiagnosticsService _triggerDiagnosticsService;
    private readonly AppRegistry _appRegistry;
    private readonly AppLogger _logger;
    private bool _isLoadingSettings;
    private bool _apiKeyVisible;
    private bool _isRecordingHotkey;
    private string? _hotkeyBeforeRecording;
    private TaskCompletionSource<bool>? _confirmationCompletion;
    private System.Windows.Threading.DispatcherTimer? _windowSizePersistTimer;

    public SettingsWindow(
        SettingsService settingsService,
        ISecretStorageService secretStorage,
        ITranslationService translationService,
        StartupRegistrationService startupRegistrationService,
        TranslationHistoryService historyService,
        TriggerDiagnosticsService triggerDiagnosticsService,
        AppRegistry appRegistry,
        AppLogger logger)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _secretStorage = secretStorage;
        _translationService = translationService;
        _startupRegistrationService = startupRegistrationService;
        _historyService = historyService;
        _triggerDiagnosticsService = triggerDiagnosticsService;
        _appRegistry = appRegistry;
        _logger = logger;
        AppsHost.Content = new AppsCenterView(appRegistry);
        _windowSizePersistTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _windowSizePersistTimer.Tick += (_, _) =>
        {
            _windowSizePersistTimer.Stop();
            PersistWindowSize();
        };
        ApplyStoredWindowSize();
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
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WindowMessageHook);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateRootShellClip();
        if (IsLoaded && (sizeInfo.WidthChanged || sizeInfo.HeightChanged))
        {
            _windowSizePersistTimer?.Stop();
            _windowSizePersistTimer?.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CompleteConfirmation(false);
        foreach (var participant in _appRegistry.Apps.OfType<ISettingsSaveParticipant>())
        {
            participant.DiscardPendingChanges();
        }

        base.OnClosed(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateRootShellClip();
        BeginEntranceAnimation();
        MoveThemeSegmentIndicator(animate: false);
    }

    private void InitializeOptionSources()
    {
        StyleCombo.ItemsSource = SettingsWindowOptions.TranslationStyles;
        FloatingButtonStyleCombo.ItemsSource = SettingsWindowOptions.FloatingButtonStyles;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = _settingsService.Current;
            TransmartBaseUrlText.Text = settings.Api.Transmart.BaseUrl;
            TransmartModelText.Text = settings.Api.Transmart.Model;
            OpenAiBaseUrlText.Text = settings.Api.OpenAi.BaseUrl;
            OpenAiModelText.Text = settings.Api.OpenAi.Model;
            OpenAiForTranslationCheck.IsChecked = settings.Api.UseOpenAiForTranslation;
            SetApiKeyHidden(_secretStorage.HasApiKey() ? ApiKeyMask : string.Empty);

            SelectComboValue(StyleCombo, settings.Translation.Style);
            MaxCharsText.Text = settings.Translation.MaxCharacters.ToString();
            PreserveFormatCheck.IsChecked = settings.Translation.PreserveFormatting;
            PromptText.Text = string.IsNullOrWhiteSpace(settings.Translation.SystemPrompt)
                ? TranslationPromptBuilder.DefaultSystemPrompt
                : settings.Translation.SystemPrompt;
            ExplanationPreferenceText.Text = string.IsNullOrWhiteSpace(settings.Translation.ExplanationPreference)
                ? TranslationPromptBuilder.DefaultExplanationPreference
                : settings.Translation.ExplanationPreference;

            AutoButtonCheck.IsChecked = settings.Triggers.AutoShowSelectionButton;
            SelectTheme(settings.Ui.Theme);
            SelectComboValue(FloatingButtonStyleCombo, settings.Ui.FloatingButtonStyle);
            SetFloatingButtonSizeSelection(settings.Ui.FloatingButtonSize);
            SetPopupFontSizeSelection(settings.Ui.FontSize);
            OpacitySlider.Value = Math.Clamp(settings.Ui.Opacity, 0.75, 1);
            UpdateAppearanceValueText();

            SaveHistoryCheck.IsChecked = settings.Privacy.SaveHistory;
            SaveOriginalCheck.IsChecked = settings.Privacy.SaveOriginalText;
            StartupCheck.IsChecked = settings.Startup.LaunchAtSignIn;
            UpdateApiHintText();
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

            foreach (var participant in _appRegistry.Apps.OfType<ISettingsSaveParticipant>())
            {
                if (!participant.HasPendingChanges)
                {
                    continue;
                }

                var appResult = await participant.SavePendingChangesAsync();
                if (!appResult.Success)
                {
                    StatusText.Text = appResult.UserMessage ?? "应用配置保存失败。";
                    return false;
                }
            }

            var settings = _settingsService.Current;
            settings.Api.Transmart.BaseUrl = TransmartBaseUrlText.Text.Trim();
            settings.Api.Transmart.Model = TransmartModelText.Text.Trim();
            settings.Api.OpenAi.BaseUrl = OpenAiBaseUrlText.Text.Trim();
            settings.Api.OpenAi.Model = OpenAiModelText.Text.Trim();
            settings.Api.UseOpenAiForTranslation = OpenAiForTranslationCheck.IsChecked == true;

            // Keep legacy flat fields in sync for backward compatibility.
            if (settings.Api.UseOpenAiForTranslation)
            {
                settings.Api.Provider = "OpenAI";
                settings.Api.BaseUrl = settings.Api.OpenAi.BaseUrl;
                settings.Api.Model = settings.Api.OpenAi.Model;
            }
            else
            {
                settings.Api.Provider = "Transmart";
                settings.Api.BaseUrl = settings.Api.Transmart.BaseUrl;
                settings.Api.Model = settings.Api.Transmart.Model;
            }

            settings.Translation.Style = GetComboValue(StyleCombo);
            settings.Translation.PreserveFormatting = PreserveFormatCheck.IsChecked == true;
            settings.Translation.SystemPrompt = string.IsNullOrWhiteSpace(PromptText.Text)
                ? TranslationPromptBuilder.DefaultSystemPrompt
                : PromptText.Text.Trim();
            settings.Translation.ExplanationPreference = string.IsNullOrWhiteSpace(ExplanationPreferenceText.Text)
                ? TranslationPromptBuilder.DefaultExplanationPreference
                : ExplanationPreferenceText.Text.Trim();
            if (int.TryParse(MaxCharsText.Text, out var maxChars))
            {
                settings.Translation.MaxCharacters = Math.Clamp(maxChars, 100, 50000);
            }

            settings.Triggers.AutoShowSelectionButton = AutoButtonCheck.IsChecked == true;
            settings.Triggers.Hotkey = GetHeaderHotkeyText();
            settings.Ui.Theme = GetSelectedThemeValue();
            var floatingButtonStyle = GetComboValue(FloatingButtonStyleCombo);
            settings.Ui.FloatingButtonStyle = string.IsNullOrWhiteSpace(floatingButtonStyle)
                ? "DarkBorderLightFill"
                : floatingButtonStyle;
            var floatingButtonSize = GetFloatingButtonSizeSelection();
            settings.Ui.FloatingButtonSize = string.IsNullOrWhiteSpace(floatingButtonSize)
                ? "Medium"
                : floatingButtonSize;
            settings.Ui.FontSize = GetPopupFontSizeSelection();
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
        SetBusy(true, "正在清空记录...");
        try
        {
            await _historyService.ClearAsync();
            _triggerDiagnosticsService.Clear();
            RefreshDiagnostics();
            StatusText.Text = "历史和触发诊断已清空。";
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

        if (!Uri.TryCreate(TransmartBaseUrlText.Text.Trim(), UriKind.Absolute, out var transmartBaseUri)
            || transmartBaseUri.Scheme is not ("http" or "https"))
        {
            message = "Transmart Base URL 需要是有效的 http 或 https 地址。";
            TransmartBaseUrlText.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(TransmartModelText.Text))
        {
            message = "Transmart Model 不能为空。";
            TransmartModelText.Focus();
            return false;
        }

        if (!Uri.TryCreate(OpenAiBaseUrlText.Text.Trim(), UriKind.Absolute, out var openAiBaseUri)
            || openAiBaseUri.Scheme is not ("http" or "https"))
        {
            message = "OpenAI Base URL 需要是有效的 http 或 https 地址。";
            OpenAiBaseUrlText.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(OpenAiModelText.Text))
        {
            message = "OpenAI Model 不能为空。";
            OpenAiModelText.Focus();
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

        if (OpenAiForTranslationCheck.IsChecked == true
            && !_secretStorage.HasApiKey()
            && string.IsNullOrWhiteSpace(GetApiKeyInput()))
        {
            message = "已启用 OpenAI 翻译通道，请先设置 API Key。";
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

            var backdrop = NativeMethods.DwmSystemBackdropTypeNone;
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

    private void RootShell_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRootShellClip();
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

    private void ApplyStoredWindowSize()
    {
        var settings = _settingsService.Current;
        Width = Math.Clamp(settings.Ui.SettingsWindowWidth, MinWidth, 1280);
        Height = Math.Clamp(settings.Ui.SettingsWindowHeight, MinHeight, 900);
    }

    private void PersistWindowSize()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var settings = _settingsService.Current;
        settings.Ui.SettingsWindowWidth = Math.Clamp(Math.Round(ActualWidth), MinWidth, 1280);
        settings.Ui.SettingsWindowHeight = Math.Clamp(Math.Round(ActualHeight), MinHeight, 900);
        _ = _settingsService.SaveAsync(settings);
    }

    private void UpdateRootShellClip()
    {
        if (RootShell.ActualWidth <= 0 || RootShell.ActualHeight <= 0)
        {
            return;
        }

        RootShell.Clip = new RectangleGeometry(
            new Rect(0, 0, RootShell.ActualWidth, RootShell.ActualHeight),
            ShellCornerRadius,
            ShellCornerRadius);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (e.ButtonState == WpfInput.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        if (_confirmationCompletion is not null)
        {
            return Task.FromResult(false);
        }

        ConfirmationTitleText.Text = title;
        ConfirmationMessageText.Text = message;
        ConfirmationConfirmButtonText.Text = confirmText;
        System.Windows.Automation.AutomationProperties.SetName(ConfirmationConfirmButton, confirmText);
        ConfirmationOverlay.Visibility = Visibility.Visible;
        _confirmationCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmationConfirmButton.Focus();
        return _confirmationCompletion.Task;
    }

    private void ConfirmationCancel_Click(object sender, RoutedEventArgs e) => CompleteConfirmation(false);

    private void ConfirmationConfirm_Click(object sender, RoutedEventArgs e) => CompleteConfirmation(true);

    private void CompleteConfirmation(bool confirmed)
    {
        var completion = _confirmationCompletion;
        if (completion is null)
        {
            return;
        }

        _confirmationCompletion = null;
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
        completion.TrySetResult(confirmed);
    }

    private void Window_PreviewKeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (_confirmationCompletion is not null && e.Key == WpfInput.Key.Escape)
        {
            CompleteConfirmation(false);
            e.Handled = true;
        }
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

    private void OpenAiForTranslationCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        UpdateApiHintText();
    }

    private void UpdateApiHintText()
    {
        if (ApiKeyStatusText is null)
        {
            return;
        }

        var hasStoredApiKey = _secretStorage.HasApiKey();
        if (OpenAiForTranslationCheck.IsChecked == true)
        {
            ApiKeyStatusText.Text = hasStoredApiKey
                ? "已启用 OpenAI 翻译通道：翻译和解释都会走 OpenAI。"
                : "已启用 OpenAI 翻译通道，但尚未保存 API Key。";
            return;
        }

        ApiKeyStatusText.Text = hasStoredApiKey
            ? "翻译走 Transmart，解释走 OpenAI。"
            : "翻译走 Transmart。若要使用解释模式，请先保存 OpenAI API Key。";
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

    private void FloatingButtonSizeDot_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }
    }

    private void PopupFontSizeDot_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }
    }

    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not System.Windows.Controls.TabControl || WpfInput.Mouse.LeftButton != WpfInput.MouseButtonState.Pressed)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            new Action(ClearSettingsTabMouseFocus),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ClearSettingsTabMouseFocus()
    {
        if (_isRecordingHotkey)
        {
            return;
        }

        WpfInput.FocusManager.SetFocusedElement(this, null);
        WpfInput.Keyboard.ClearFocus();
    }

    private void HeaderHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            CancelHotkeyRecording();
            return;
        }

        BeginHotkeyRecording();
    }

    private void HotkeyRecorder_LostKeyboardFocus(object sender, WpfInput.KeyboardFocusChangedEventArgs e)
    {
        CancelHotkeyRecording();
    }

    private void HotkeyRecorder_PreviewKeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (!_isRecordingHotkey)
        {
            return;
        }

        var key = e.Key == WpfInput.Key.System ? e.SystemKey : e.Key;
        if (key == WpfInput.Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (key == WpfInput.Key.Escape)
        {
            CancelHotkeyRecording();
            e.Handled = true;
            return;
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
            e.Handled = true;
            return;
        }

        parts.Add(FormatKey(key));
        var hotkey = string.Join("+", parts);
        UpdateHeaderHotkey(hotkey);
        FinishHotkeyRecording();
        e.Handled = true;
    }

    private void Window_PreviewMouseDown(object sender, WpfInput.MouseButtonEventArgs e)
    {
        if (!_isRecordingHotkey || IsWithinElement(e.OriginalSource as DependencyObject, HeaderHotkeyButton))
        {
            return;
        }

        CancelHotkeyRecording();
    }

    private void BeginHotkeyRecording()
    {
        _isRecordingHotkey = true;
        _hotkeyBeforeRecording = GetHeaderHotkeyText();
        HeaderHotkeyButton.Focus();
    }

    private void FinishHotkeyRecording()
    {
        _isRecordingHotkey = false;
        _hotkeyBeforeRecording = null;
        ClearHotkeyButtonFocus();
    }

    private void CancelHotkeyRecording()
    {
        if (!_isRecordingHotkey)
        {
            return;
        }

        _isRecordingHotkey = false;
        if (!string.IsNullOrWhiteSpace(_hotkeyBeforeRecording))
        {
            UpdateHeaderHotkey(_hotkeyBeforeRecording);
        }

        _hotkeyBeforeRecording = null;
        ClearHotkeyButtonFocus();
    }

    private void ClearHotkeyButtonFocus()
    {
        HeaderHotkeyButton.Effect = null;
        WpfInput.FocusManager.SetFocusedElement(this, null);
        WpfInput.Keyboard.ClearFocus();
    }

    private void UpdateAppearanceValueText()
    {
        if (OpacityValueText is null)
        {
            return;
        }

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
            var label = new TextBlock
            {
                Text = part,
                FontSize = 10,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Settings.TextMutedBrush");

            var keycap = new Border
            {
                MinWidth = 26,
                Height = 18,
                Margin = new Thickness(HeaderHotkeyKeys.Children.Count == 0 ? 0 : 4, 0, 0, 0),
                Padding = new Thickness(6, 0, 6, 1),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Child = label
            };
            keycap.SetResourceReference(Border.BackgroundProperty, "Settings.KeycapBrush");
            keycap.SetResourceReference(Border.BorderBrushProperty, "Settings.KeycapBorderBrush");
            HeaderHotkeyKeys.Children.Add(keycap);
        }
    }

    private string GetHeaderHotkeyText()
    {
        return HeaderHotkeyButton.Tag as string ?? _settingsService.Current.Triggers.Hotkey;
    }

    private void SetFloatingButtonSizeSelection(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Medium" : value;
        WpfRadioButton? fallback = null;
        foreach (var child in FloatingButtonSizeDots.Children)
        {
            if (child is not WpfRadioButton radio)
            {
                continue;
            }

            if (string.Equals(radio.Tag as string, "Medium", StringComparison.OrdinalIgnoreCase))
            {
                fallback = radio;
            }

            if (string.Equals(radio.Tag as string, normalized, StringComparison.OrdinalIgnoreCase))
            {
                radio.IsChecked = true;
                return;
            }
        }

        if (fallback is not null)
        {
            fallback.IsChecked = true;
        }
    }

    private string GetFloatingButtonSizeSelection()
    {
        foreach (var child in FloatingButtonSizeDots.Children)
        {
            if (child is WpfRadioButton { IsChecked: true } radio && radio.Tag is string value)
            {
                return value;
            }
        }

        return "Medium";
    }

    private void SetPopupFontSizeSelection(double value)
    {
        var target = PopupFontSizeValues[0];
        foreach (var option in PopupFontSizeValues)
        {
            if (Math.Abs(option - value) < Math.Abs(target - value))
            {
                target = option;
            }
        }

        WpfRadioButton? fallback = null;
        foreach (var child in PopupFontSizeDots.Children)
        {
            if (child is not WpfRadioButton radio)
            {
                continue;
            }

            if (string.Equals(radio.Tag as string, "14", StringComparison.Ordinal))
            {
                fallback = radio;
            }

            if (double.TryParse(radio.Tag as string, out var tagValue) && Math.Abs(tagValue - target) < 0.001)
            {
                radio.IsChecked = true;
                return;
            }
        }

        if (fallback is not null)
        {
            fallback.IsChecked = true;
        }
    }

    private double GetPopupFontSizeSelection()
    {
        foreach (var child in PopupFontSizeDots.Children)
        {
            if (child is WpfRadioButton { IsChecked: true } radio
                && double.TryParse(radio.Tag as string, out var value))
            {
                return value;
            }
        }

        return 14;
    }

    private void ApplyLocalTheme(string theme)
    {
        var effectiveTheme = string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase)
            ? (ThemeResourceService.ShouldUseDarkTheme(theme) ? "Dark" : "Light")
            : theme;
        var palette = SettingsWindowThemePalettes.For(effectiveTheme);
        var useDarkPreview = string.Equals(effectiveTheme, "Dark", StringComparison.OrdinalIgnoreCase);

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
        SetSolidBrush("Settings.SegmentTrackBrush", palette.SegmentTrack);
        SetSolidBrush("Settings.SegmentIndicatorBrush", palette.SegmentIndicator);
        SetSolidBrush("Settings.SegmentIndicatorBorderBrush", palette.SegmentIndicatorBorder);
        SetSolidBrush("Settings.KeycapShellBrush", palette.KeycapShell);
        SetSolidBrush("Settings.KeycapBrush", palette.Keycap);
        SetSolidBrush("Settings.KeycapBorderBrush", palette.KeycapBorder);
        SetSolidBrush("Settings.ToggleTrackBrush", palette.ToggleTrack);
        SetSolidBrush("Settings.SliderTrackBrush", palette.SliderTrack);
        SetSolidBrush("Settings.SliderThumbBrush", palette.SliderThumb);
        SetSolidBrush("Settings.FloatingButtonPreviewOuterBrush", useDarkPreview ? Colors.White : Colors.Black);
        SetSolidBrush("Settings.FloatingButtonPreviewInnerBrush", useDarkPreview ? Colors.Black : Colors.White);
        SetSolidBrush("Settings.ScrollBarThumbBrush", palette.ScrollBarThumb);
        SetSolidBrush("Settings.ScrollBarThumbHoverBrush", palette.ScrollBarThumbHover);
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

    private static bool IsWithinElement(DependencyObject? source, DependencyObject target)
    {
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            current = current switch
            {
                FrameworkElement element => element.Parent ?? VisualTreeHelper.GetParent(element),
                FrameworkContentElement contentElement => contentElement.Parent,
                _ => VisualTreeHelper.GetParent(current)
            };
        }

        return false;
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
