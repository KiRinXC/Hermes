using System.Net.Http;
using System.Windows;
using Hermes.Windows.History;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;
using Hermes.Windows.Overlay;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.Shell;
using Hermes.Windows.Translation;
using Hermes.Windows.Tray;
using Hermes.Windows.UI.Themes;

namespace Hermes.Windows;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstanceGuard;
    private AppLogger? _logger;
    private SettingsService? _settingsService;
    private ISecretStorageService? _secretStorage;
    private StartupRegistrationService? _startupRegistrationService;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private KeyboardHookService? _keyboardHookService;
    private MouseHookService? _mouseHookService;
    private OverlayManager? _overlayManager;
    private TranslationCoordinator? _translationCoordinator;
    private TranslationHistoryService? _historyService;
    private ITranslationService? _translationService;
    private TriggerDiagnosticsService? _triggerDiagnosticsService;
    private SettingsWindow? _settingsWindow;
    private bool _paused;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureCreated();
        _logger = new AppLogger();
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.IsFirstInstance)
        {
            _singleInstanceGuard.SignalExistingInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _logger.Error("Unhandled UI exception.", args.Exception);
            args.Handled = true;
        };

        await InitializeServicesAsync();
        _singleInstanceGuard.StartActivationListener(() => Dispatcher.Invoke(ShowSettingsWindow));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _translationCoordinator?.CloseAll();
        _hotkeyService?.Dispose();
        _keyboardHookService?.Dispose();
        _mouseHookService?.Dispose();
        _trayService?.Dispose();
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }

    private async Task InitializeServicesAsync()
    {
        if (_logger is null)
        {
            return;
        }

        _settingsService = new SettingsService(_logger);
        await _settingsService.LoadAsync();
        AppIdentityService.EnsureRegistered(_logger);
        ThemeResourceService.Apply(_settingsService.Current.Ui.Theme);
        _secretStorage = new DpapiSecretStorageService(_logger);
        _startupRegistrationService = new StartupRegistrationService();
        _startupRegistrationService.SetLaunchAtSignIn(_settingsService.Current.Startup.LaunchAtSignIn);

        var foregroundWindowService = new ForegroundWindowService(_settingsService);
        var uiAutomationProvider = new UiAutomationSelectionProvider(foregroundWindowService, _logger);
        var clipboardSelectionProvider = new ClipboardSelectionProvider(foregroundWindowService, _logger);
        _triggerDiagnosticsService = new TriggerDiagnosticsService();
        var selectionCandidateService = new SelectionCandidateService(
            uiAutomationProvider,
            foregroundWindowService,
            _settingsService,
            _triggerDiagnosticsService);
        var selectionOrchestrator = new SelectionOrchestrator(
            uiAutomationProvider,
            clipboardSelectionProvider,
            foregroundWindowService,
            _settingsService);

        _historyService = new TranslationHistoryService(_settingsService, _logger);
        _translationService = new OpenAiTranslationService(new HttpClient(), _settingsService, _secretStorage, _logger);
        _overlayManager = new OverlayManager(new OverlayPositionService(), _settingsService);
        _translationCoordinator = new TranslationCoordinator(
            selectionOrchestrator,
            selectionCandidateService,
            _translationService,
            _overlayManager,
            _historyService,
            _settingsService);
        _translationCoordinator.SettingsRequested += (_, _) => ShowSettingsWindow();

        _hotkeyService = new HotkeyService(_logger);
        _hotkeyService.HotkeyPressed += async (_, _) =>
        {
            if (!_paused && _translationCoordinator is not null)
            {
                await _translationCoordinator.TranslateCurrentSelectionAsync();
            }
        };

        _keyboardHookService = new KeyboardHookService(_logger);
        _keyboardHookService.UserActivity += (_, _) => _translationCoordinator?.ClosePassiveUi();
        _keyboardHookService.EscapePressed += (_, _) => _translationCoordinator?.ClosePassiveUi();

        _mouseHookService = new MouseHookService(_logger);
        _mouseHookService.UserActivity += (_, activity) =>
        {
            if (_overlayManager?.ContainsOverlayPoint(activity.X, activity.Y) == true)
            {
                return;
            }

            _translationCoordinator?.ClosePassiveUi();
        };
        _mouseHookService.SelectionGestureCompleted += (_, point) =>
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (!_paused && _translationCoordinator is not null)
                {
                    await _translationCoordinator.TryShowFloatingButtonAsync(
                        point.StartX,
                        point.StartY,
                        point.X,
                        point.Y,
                        point.StartedAt,
                        point.ReleasedAt,
                        point.CtrlDownAtStart,
                        point.CtrlHeldDuringDrag,
                        point.CtrlDownAtRelease);
                }
            });
        };

        _trayService = new TrayService();
        _trayService.PauseResumeRequested += (_, _) => TogglePaused();
        _trayService.TranslateClipboardRequested += async (_, _) =>
        {
            if (!_paused && _translationCoordinator is not null)
            {
                await _translationCoordinator.TranslateClipboardAsync();
            }
        };
        _trayService.SettingsRequested += (_, _) => ShowSettingsWindow();
        _trayService.HistoryRequested += (_, _) => _trayService.ShowBalloon("翻译历史", "历史功能已接入本地存储，详细列表将在后续 UI 中展示。");
        _trayService.ExitRequested += (_, _) => Shutdown();
        _trayService.Show();

        _settingsService.SettingsChanged += (_, settings) =>
        {
            ThemeResourceService.Apply(settings.Ui.Theme);
            _hotkeyService?.Register(HotkeyGesture.ParseOrDefault(settings.Triggers.Hotkey));
            _startupRegistrationService?.SetLaunchAtSignIn(settings.Startup.LaunchAtSignIn);
            if (!_paused)
            {
                if (settings.Triggers.AutoShowSelectionButton)
                {
                    _mouseHookService?.Start();
                }
                else
                {
                    _mouseHookService?.Stop();
                }
            }
        };

        ResumeTriggers();
        _trayService.ShowBalloon("Hermes", "已在后台运行。选中文本后按 Ctrl+Alt+E 可翻译。");
    }

    private void TogglePaused()
    {
        if (_paused)
        {
            ResumeTriggers();
        }
        else
        {
            PauseTriggers();
        }
    }

    private void PauseTriggers()
    {
        _paused = true;
        _hotkeyService?.Unregister();
        _keyboardHookService?.Stop();
        _mouseHookService?.Stop();
        _translationCoordinator?.ClosePassiveUi();
        _trayService?.SetPaused(true);
    }

    private void ResumeTriggers()
    {
        if (_settingsService is null)
        {
            return;
        }

        _paused = false;
        _hotkeyService?.Register(HotkeyGesture.ParseOrDefault(_settingsService.Current.Triggers.Hotkey));
        _keyboardHookService?.Start();
        if (_settingsService.Current.Triggers.AutoShowSelectionButton)
        {
            _mouseHookService?.Start();
        }

        _trayService?.SetPaused(false);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsService is null
            || _secretStorage is null
            || _translationService is null
            || _startupRegistrationService is null
            || _historyService is null
            || _triggerDiagnosticsService is null
            || _logger is null)
        {
            return;
        }

        if (_settingsWindow is null || !_settingsWindow.IsVisible)
        {
            _settingsWindow = new SettingsWindow(
                _settingsService,
                _secretStorage,
                _translationService,
                _startupRegistrationService,
                _historyService,
                _triggerDiagnosticsService,
                _logger);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }
}
