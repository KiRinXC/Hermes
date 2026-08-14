using System.Net.Http;
using System.Windows;
using Hermes.Windows.Apps;
using Hermes.Windows.Apps.CodexAuthSwitchSync;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Services;
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
using System.Windows.Threading;

namespace Hermes.Windows;

public partial class App : System.Windows.Application
{
    internal static readonly TimeSpan StartupTriggerDelay = TimeSpan.FromMilliseconds(250);

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
    private AppRegistry? _appRegistry;
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
        _startupRegistrationService = new StartupRegistrationService(_logger);
        _startupRegistrationService.SetLaunchAtSignIn(_settingsService.Current.Startup.LaunchAtSignIn);

        var codexLocations = new CodexLocations();
        var codexProfiles = new CodexProfileStore(codexLocations);
        var codexSessions = new CodexSessionSyncService(codexLocations);
        var codexProcessGuard = new CodexProcessGuard();
        var codexService = new CodexAuthSwitchService(
            codexLocations,
            codexProfiles,
            codexSessions,
            codexProcessGuard,
            _logger);
        _appRegistry = new AppRegistry();
        _appRegistry.Register(new CodexAuthSwitchSyncApp(codexService));

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
        var openAiService = new OpenAiTranslationService(new HttpClient(), _settingsService, _secretStorage, _logger);
        var transmartService = new TransmartTranslationService(new HttpClient(), _settingsService, _logger);
        _translationService = new ProviderRoutingTranslationService(openAiService, transmartService, _settingsService);
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

            _translationCoordinator?.ClosePassiveUiAfterPointerActivity();
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
                        point.Mode,
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

        _trayService.ShowBalloon("Hermes", "已在后台运行。选中文本后按 Ctrl+Alt+E 可翻译。");
        ScheduleResumeTriggersAfterStartup();
    }

    private void ScheduleResumeTriggersAfterStartup()
    {
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Dispatcher)
        {
            Interval = StartupTriggerDelay
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_paused)
            {
                ResumeTriggers();
            }
        };
        timer.Start();
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
            || _appRegistry is null
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
                _appRegistry,
                _logger);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        BringSettingsWindowToFront();
    }

    private void BringSettingsWindowToFront()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        _settingsWindow.Topmost = true;
        _settingsWindow.Activate();
        _settingsWindow.Focus();
        _settingsWindow.Topmost = false;
    }
}
