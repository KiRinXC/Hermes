using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Hermes.Windows.History;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Translation;
using Hermes.Windows.UI.Themes;

namespace Hermes.Windows;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ISecretStorageService _secretStorage;
    private readonly ITranslationService _translationService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private readonly TranslationHistoryService _historyService;
    private readonly TriggerDiagnosticsService _triggerDiagnosticsService;
    private readonly AppLogger _logger;

    public MainWindow(
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

    private void LoadSettings()
    {
        var settings = _settingsService.Current;
        SelectComboValue(ProviderCombo, settings.Api.Provider);
        BaseUrlText.Text = settings.Api.BaseUrl;
        ApiKeyBox.Password = _secretStorage.HasApiKey() ? "********" : string.Empty;
        ModelText.Text = settings.Api.Model;

        SelectComboValue(StyleCombo, settings.Translation.Style);
        MaxCharsText.Text = settings.Translation.MaxCharacters.ToString();
        PreserveFormatCheck.IsChecked = settings.Translation.PreserveFormatting;

        AutoButtonCheck.IsChecked = settings.Triggers.AutoShowSelectionButton;
        HotkeyText.Text = settings.Triggers.Hotkey;
        SelectComboValue(ThemeCombo, settings.Ui.Theme);
        PopupWidthText.Text = settings.Ui.PopupWidth.ToString("0");
        FontSizeText.Text = settings.Ui.FontSize.ToString("0");
        OpacityText.Text = settings.Ui.Opacity.ToString("0.00");

        SaveHistoryCheck.IsChecked = settings.Privacy.SaveHistory;
        SaveOriginalCheck.IsChecked = settings.Privacy.SaveOriginalText;
        StartupCheck.IsChecked = settings.Startup.LaunchAtSignIn;
        ApiKeyStatusText.Text = _secretStorage.HasApiKey()
            ? "已保存 API Key。输入新值并保存即可替换。"
            : "尚未保存 API Key。";
        RefreshDiagnostics();
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
            if (int.TryParse(MaxCharsText.Text, out var maxChars))
            {
                settings.Translation.MaxCharacters = Math.Clamp(maxChars, 100, 50000);
            }

            settings.Triggers.AutoShowSelectionButton = AutoButtonCheck.IsChecked == true;
            settings.Triggers.Hotkey = HotkeyText.Text.Trim();
            settings.Ui.Theme = GetComboValue(ThemeCombo);
            if (double.TryParse(PopupWidthText.Text, out var width))
            {
                settings.Ui.PopupWidth = Math.Clamp(width, 320, 480);
            }

            if (double.TryParse(FontSizeText.Text, out var fontSize))
            {
                settings.Ui.FontSize = Math.Clamp(fontSize, 12, 20);
            }

            if (double.TryParse(OpacityText.Text, out var opacity))
            {
                settings.Ui.Opacity = Math.Clamp(opacity, 0.75, 1);
            }

            settings.Privacy.SaveHistory = SaveHistoryCheck.IsChecked == true;
            settings.Privacy.SaveOriginalText = SaveOriginalCheck.IsChecked == true;
            settings.Startup.LaunchAtSignIn = StartupCheck.IsChecked == true;

            if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password) && ApiKeyBox.Password != "********")
            {
                await _secretStorage.SaveApiKeyAsync(ApiKeyBox.Password);
                ApiKeyBox.Password = "********";
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
        try
        {
            if (!await SaveSettingsAsync())
            {
                return;
            }

            var result = await _translationService.TestConnectionAsync();
            StatusText.Text = result.Success ? "连接测试成功。" : result.UserMessage ?? "连接测试失败。";
        }
        finally
        {
            SetBusy(false);
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

        if (!HotkeyGesture.TryParse(HotkeyText.Text, out _))
        {
            message = "快捷键格式需要类似 Ctrl+Alt+E，并包含至少一个修饰键。";
            HotkeyText.Focus();
            return false;
        }

        if (!int.TryParse(MaxCharsText.Text, out var maxChars) || maxChars is < 100 or > 50000)
        {
            message = "最大字符数需要在 100 到 50000 之间。";
            MaxCharsText.Focus();
            return false;
        }

        if (!double.TryParse(PopupWidthText.Text, out var popupWidth) || popupWidth is < 320 or > 480)
        {
            message = "卡片宽度需要在 320 到 480 之间。";
            PopupWidthText.Focus();
            return false;
        }

        if (!double.TryParse(FontSizeText.Text, out var fontSize) || fontSize is < 12 or > 20)
        {
            message = "译文字号需要在 12 到 20 之间。";
            FontSizeText.Focus();
            return false;
        }

        if (!double.TryParse(OpacityText.Text, out var opacity) || opacity is < 0.75 or > 1)
        {
            message = "卡片透明度需要在 0.75 到 1 之间。";
            OpacityText.Focus();
            return false;
        }

        if (!_secretStorage.HasApiKey() && string.IsNullOrWhiteSpace(ApiKeyBox.Password))
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
        ClearHistoryButton.IsEnabled = !isBusy;
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

            var useDark = ThemeResourceService.ShouldUseDarkTheme(_settingsService.Current.Ui.Theme) ? 1 : 0;
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
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not apply settings window chrome theme. {ex.Message}");
        }
    }

    private static string GetComboValue(System.Windows.Controls.ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    private static void SelectComboValue(System.Windows.Controls.ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }
}
