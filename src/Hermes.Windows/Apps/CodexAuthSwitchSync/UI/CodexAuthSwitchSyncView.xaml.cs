using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Services;
using Hermes.Windows.Apps.Contracts;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.UI;

public partial class CodexAuthSwitchSyncView : System.Windows.Controls.UserControl
{
    private readonly CodexAuthSwitchService _service;
    private CancellationTokenSource? _browserLoginCancellation;
    private bool _busy;
    private CodexStatus? _status;

    public CodexAuthSwitchSyncView(CodexAuthSwitchService service)
    {
        InitializeComponent();
        _service = service;
        Loaded += async (_, _) => await RefreshStatusAsync();
        IsVisibleChanged += (_, args) =>
        {
            if (args.NewValue is false)
            {
                CancelBrowserLogin();
                ResetApiEditor();
            }
        };
    }

    public bool HasPendingChanges => ApiEditor.Visibility == Visibility.Visible;

    public void DiscardPendingChanges() => ResetApiEditor();

    public async Task<SettingsSaveResult> SavePendingChangesAsync()
    {
        if (!HasPendingChanges)
        {
            return SettingsSaveResult.Completed();
        }

        ApiEditorErrorText.Visibility = Visibility.Collapsed;
        var configText = ApiConfigTomlText.Text;
        var authText = ApiAuthJsonText.Text;
        var appliesToActiveApi = _status?.Mode == CodexAuthMode.Api;
        SetBusy(true, appliesToActiveApi
            ? "正在加密保存并应用当前 API 配置…"
            : "正在校验并加密保存 API 配置…");
        try
        {
            await Task.Run(() => _service.ConfigureApiFiles(configText, authText));
            ResetApiEditor();
            var message = appliesToActiveApi
                ? "API 配置已加密保存，并已应用到当前 Codex 与本地会话。"
                : "API 配置已加密保存，将在下次切换到 API 时应用。";
            ShowMessage(message, success: true);
            return SettingsSaveResult.Completed(message);
        }
        catch (Exception ex)
        {
            var message = UserMessage(ex);
            ApiEditorErrorText.Text = message;
            ApiEditorErrorText.Visibility = Visibility.Visible;
            ShowMessage(message, success: false);
            return SettingsSaveResult.Failed(message);
        }
        finally
        {
            SetBusy(false, string.Empty);
            await RefreshStatusAsync();
        }
    }

    private async Task RefreshStatusAsync()
    {
        if (_busy)
        {
            return;
        }

        try
        {
            _status = await Task.Run(_service.GetStatus);
            RenderStatus(_status);
        }
        catch (Exception ex)
        {
            ShowMessage(UserMessage(ex), success: false);
        }
    }

    private void RenderStatus(CodexStatus status)
    {
        CurrentModeText.Text = ModeLabel(status.Mode);
        CurrentProviderText.Text = status.Provider;
        CurrentHabitText.Text = $"{status.Model} · {status.ReasoningEffort}";
        RenderProfile(status.ChatGptProfile, ChatGptBadge, ChatGptBadgeText, ChatGptProfileText);
        RenderProfile(status.ApiProfile, ApiBadge, ApiBadgeText, ApiProfileText);

        var running = status.RunningProcesses.Count > 0;
        ProcessWarning.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        ProcessWarningText.Text = running
            ? $"检测到 {string.Join("、", status.RunningProcesses)}。仍可保存档案；浏览器登录、切换和同步前请完全退出这些客户端。"
            : string.Empty;

        RenderChatGptActions(status, running);
        RenderApiActions(status, running);
        LoginChatGptButton.IsEnabled = !running;
        SyncButton.IsEnabled = !running;

        RolloutCountText.Text = CountLabel(status.History.RolloutTotal, status.History.RolloutMismatched);
        SqliteCountText.Text = CountLabel(status.History.SqliteTotal, status.History.SqliteMismatched);
        SqliteCountText.ToolTip = status.History.StateDatabasePath ?? "未发现 Codex state_5.sqlite";
        var composition = status.History.SessionComposition;
        SessionTotalText.Text = composition.Total.ToString();
        RegularSessionCountText.Text = composition.RegularActive.ToString();
        InternalSessionCountText.Text = composition.InternalActive.ToString();
        ArchivedSessionCountText.Text = composition.Archived.ToString();
        HistoryStatusText.Text = status.History.IsAligned
            ? $"已与当前 Provider “{status.Provider}” 对齐。"
            : $"发现 {status.History.RolloutMismatched + status.History.SqliteMismatched} 项未对齐；退出 Codex 后可同步。";
        HistoryStatusText.Foreground = FindBrush(
            status.History.IsAligned ? "Settings.SuccessBrush" : "Settings.WarningBrush");

    }

    private void RenderChatGptActions(CodexStatus status, bool running)
    {
        var isCurrent = status.Mode == CodexAuthMode.ChatGpt;
        var hasProfile = status.ChatGptProfile.Saved;
        var canCaptureCurrent = isCurrent && !hasProfile;

        SwitchChatGptButton.Visibility = !isCurrent && hasProfile ? Visibility.Visible : Visibility.Collapsed;
        SwitchChatGptButton.IsEnabled = !running;
        CaptureChatGptButton.Visibility = canCaptureCurrent ? Visibility.Visible : Visibility.Collapsed;
        CaptureChatGptButton.IsEnabled = canCaptureCurrent;
        CaptureChatGptButton.Margin = new Thickness(0);
        CaptureChatGptButtonText.Text = "保存当前登录";
        System.Windows.Automation.AutomationProperties.SetName(
            CaptureChatGptButton,
            "保存当前 ChatGPT 登录");

        LoginChatGptButton.Visibility = Visibility.Visible;
        LoginChatGptButton.Margin = new Thickness(
            SwitchChatGptButton.Visibility == Visibility.Visible || CaptureChatGptButton.Visibility == Visibility.Visible
                ? 8
                : 0,
            0,
            0,
            0);
        LoginChatGptButtonText.Text = isCurrent || hasProfile ? "重新登录" : "浏览器登录";
        System.Windows.Automation.AutomationProperties.SetName(
            LoginChatGptButton,
            isCurrent || hasProfile ? "重新在浏览器中登录 ChatGPT" : "在浏览器中登录 ChatGPT");

        SetButtonStyle(
            LoginChatGptButton,
            !isCurrent && !hasProfile ? "Settings.PrimaryButton" : "Settings.FooterButton");
        SetButtonStyle(
            CaptureChatGptButton,
            canCaptureCurrent ? "Settings.PrimaryButton" : "Settings.FooterButton");
    }

    private void RenderApiActions(CodexStatus status, bool running)
    {
        var isCurrent = status.Mode == CodexAuthMode.Api;
        var hasProfile = status.ApiProfile.Saved;

        SwitchApiButton.Visibility = !isCurrent && hasProfile ? Visibility.Visible : Visibility.Collapsed;
        SwitchApiButton.IsEnabled = !running;
        ConfigureApiButton.Margin = new Thickness(
            SwitchApiButton.Visibility == Visibility.Visible ? 8 : 0,
            0,
            0,
            0);
        ConfigureApiButtonText.Text = hasProfile ? "编辑配置" : "配置 API";
        SetButtonStyle(
            ConfigureApiButton,
            SwitchApiButton.Visibility == Visibility.Visible
                ? "Settings.FooterButton"
                : "Settings.PrimaryButton");
    }

    private static void SetButtonStyle(System.Windows.Controls.Button button, object resourceKey) =>
        button.SetResourceReference(FrameworkElement.StyleProperty, resourceKey);

    private void RenderProfile(
        CodexProfileSummary profile,
        Border badge,
        TextBlock badgeText,
        TextBlock detailText)
    {
        badgeText.Text = profile.Saved ? "已保存" : "未配置";
        detailText.Text = profile.Saved
            ? $"{profile.Provider ?? "—"} · {profile.Model ?? "—"}\n推理 {profile.ReasoningEffort ?? "—"}"
            : "尚未保存完整 config + auth 档案。";
        badge.BorderBrush = FindBrush(profile.Saved ? "Settings.SuccessBrush" : "Settings.BorderBrush");
        badgeText.Foreground = FindBrush(profile.Saved ? "Settings.SuccessBrush" : "Settings.TextMutedBrush");
    }

    private async Task RunOperationAsync(string busyText, Func<CodexOperationResult> operation)
    {
        SetBusy(true, busyText);
        try
        {
            var result = await Task.Run(operation);
            ShowMessage(
                $"操作完成：更新 {result.RolloutFilesUpdated} 个 rollout、{result.SqliteRowsUpdated} 条 SQLite 索引，校验已通过。",
                success: true);
        }
        catch (Exception ex)
        {
            ShowMessage(UserMessage(ex), success: false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            await RefreshStatusAsync();
        }
    }

    private async Task<bool> RunProfileOperationAsync(string busyText, Func<CodexProfileSummary> operation)
    {
        SetBusy(true, busyText);
        var succeeded = false;
        try
        {
            await Task.Run(operation);
            ShowMessage("认证档案已使用当前 Windows 用户凭据加密保存。", success: true);
            succeeded = true;
        }
        catch (Exception ex)
        {
            ShowMessage(UserMessage(ex), success: false);
        }
        finally
        {
            SetBusy(false, string.Empty);
            await RefreshStatusAsync();
        }

        return succeeded;
    }

    private void SetBusy(bool busy, string message, bool canCancel = false)
    {
        _busy = busy;
        BusyText.Text = message;
        ContentScrollViewer.IsHitTestVisible = !busy;
        BusyOverlay.IsHitTestVisible = busy;
        BusyCancelButton.Visibility = busy && canCancel ? Visibility.Visible : Visibility.Collapsed;
        BusyCancelButton.IsEnabled = busy && canCancel;
        if (busy)
        {
            BusyOverlay.Visibility = Visibility.Visible;
            StartBusyVisuals();
        }
        else
        {
            StopBusyVisuals();
            BusyOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void StartBusyVisuals()
    {
        StopBusyVisuals();
        BusyOverlay.Opacity = 1;
        BusyCardScaleTransform.ScaleX = 1;
        BusyCardScaleTransform.ScaleY = 1;
        BusySpinnerRotateTransform.Angle = 0;
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        BusyOverlay.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        var scaleAnimation = new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BusyCardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        BusyCardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
        BusySpinnerRotateTransform.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
    }

    private void StopBusyVisuals()
    {
        BusyOverlay.BeginAnimation(OpacityProperty, null);
        BusyCardScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        BusyCardScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        BusySpinnerRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void ShowMessage(string message, bool success)
    {
        OperationStatusBorder.Visibility = Visibility.Visible;
        OperationStatusText.Text = message;
        OperationStatusText.Foreground = FindBrush(success ? "Settings.SuccessBrush" : "Settings.WarningBrush");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshStatusAsync();

    private async void CaptureChatGpt_Click(object sender, RoutedEventArgs e) =>
        await RunProfileOperationAsync("正在保存 ChatGPT 档案…", () => _service.CaptureCurrent(CodexAuthMode.ChatGpt));

    private async void LoginChatGpt_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync(
            "在浏览器中登录 ChatGPT",
            "Hermes 将先加密备份当前 Codex 配置与认证，然后调用官方 codex login 并打开浏览器。认证成功后会保存 ChatGPT 档案、切换到 ChatGPT，并同步本地会话 Provider。继续前请确认 Codex、ChatGPT、CLI 与 IDE 扩展已完全退出。",
            "继续登录"))
        {
            await RunBrowserLoginAsync();
        }
    }

    private async Task RunBrowserLoginAsync()
    {
        _browserLoginCancellation?.Dispose();
        _browserLoginCancellation = new CancellationTokenSource();
        var cancellation = _browserLoginCancellation;
        SetBusy(true, "请在浏览器中完成 ChatGPT 登录。", canCancel: true);
        try
        {
            var result = await _service.LoginChatGptWithBrowserAsync(cancellation.Token);
            ShowMessage(
                $"登录完成：更新 {result.RolloutFilesUpdated} 个 rollout、{result.SqliteRowsUpdated} 条 SQLite 索引。",
                success: true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ShowMessage("登录已取消，原 Codex 配置与认证已恢复。", success: true);
        }
        catch (Exception ex)
        {
            ShowMessage(UserMessage(ex), success: false);
        }
        finally
        {
            if (ReferenceEquals(_browserLoginCancellation, cancellation))
            {
                _browserLoginCancellation = null;
            }

            cancellation.Dispose();
            SetBusy(false, string.Empty);
            await RefreshStatusAsync();
        }
    }

    private void BusyCancel_Click(object sender, RoutedEventArgs e) => CancelBrowserLogin();

    private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && _browserLoginCancellation is not null)
        {
            CancelBrowserLogin();
            e.Handled = true;
        }
    }

    private void CancelBrowserLogin()
    {
        var cancellation = _browserLoginCancellation;
        if (cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        BusyCancelButton.IsEnabled = false;
        BusyText.Text = "正在取消并恢复原配置…";
        cancellation.Cancel();
    }

    private void ConfigureApi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var draft = _service.GetApiDraft();
            ApiConfigTomlText.Text = draft.ConfigText;
            ApiAuthJsonText.Text = draft.AuthText;
            ApiEditorErrorText.Visibility = Visibility.Collapsed;
            ApiEditor.Visibility = Visibility.Visible;
            ApiConfigTomlText.Focus();
        }
        catch (Exception ex)
        {
            ShowMessage(UserMessage(ex), success: false);
        }
    }

    private void ClearApiAuthEditor()
    {
        ApiAuthJsonText.Clear();
    }

    private void ResetApiEditor()
    {
        ApiConfigTomlText.Clear();
        ClearApiAuthEditor();
        ApiEditorErrorText.Visibility = Visibility.Collapsed;
        ApiEditor.Visibility = Visibility.Collapsed;
    }

    private async void SwitchChatGpt_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSwitchAsync("ChatGPT"))
        {
            return;
        }

        await RunOperationAsync("正在切换到 ChatGPT 并同步本地会话…", () => _service.Switch(CodexAuthMode.ChatGpt));
    }

    private async void SwitchApi_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSwitchAsync("API"))
        {
            return;
        }

        await RunOperationAsync("正在切换到 API 并同步本地会话…", () => _service.Switch(CodexAuthMode.Api));
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmAsync(
            "同步 Codex 会话",
            "将所有本地 Codex 会话的 Provider 元数据对齐到当前配置。继续前请确认 Codex、ChatGPT、CLI 与 IDE 扩展已完全退出。",
            "确认同步"))
        {
            await RunOperationAsync("正在同步并校验本地会话…", _service.SyncCurrent);
        }
    }

    private Task<bool> ConfirmSwitchAsync(string target) => ConfirmAsync(
            "切换 Codex 认证",
            $"将切换到 {target} 认证，并把本地会话 Provider 元数据同步到目标配置。切换前会自动备份，是否继续？",
            "确认切换");

    private Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        if (Window.GetWindow(this) is IHermesConfirmationHost host)
        {
            return host.ConfirmAsync(title, message, confirmText);
        }

        ShowMessage("无法打开 Hermes 确认界面，请关闭设置窗口后重试。", success: false);
        return Task.FromResult(false);
    }

    private static string CountLabel(int total, int mismatch) => mismatch == 0
        ? $"{total} 项 · 已对齐"
        : $"{total} 项 · {mismatch} 项待同步";

    private static string ModeLabel(CodexAuthMode mode) => mode switch
    {
        CodexAuthMode.ChatGpt => "ChatGPT",
        CodexAuthMode.Api => "API",
        _ => "未识别"
    };

    private static string UserMessage(Exception exception) => exception switch
    {
        CodexSwitchException => exception.Message,
        IOException => "无法读写 Codex 本地文件。请确认客户端已退出，并检查 .codex 目录权限。",
        _ => "操作未完成。Hermes 已停止写入；请刷新状态后重试。"
    };

    private System.Windows.Media.Brush FindBrush(string key) =>
        (System.Windows.Media.Brush)(TryFindResource(key) ?? System.Windows.Media.Brushes.Gray);
}
