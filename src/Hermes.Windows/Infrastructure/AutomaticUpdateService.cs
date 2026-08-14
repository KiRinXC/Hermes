using System.Reflection;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace Hermes.Windows.Infrastructure;

public enum HermesUpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    ReadyToRestart,
    Unsupported,
    Failed
}

public sealed record HermesUpdateState(
    HermesUpdateStatus Status,
    string CurrentVersion,
    string? AvailableVersion = null,
    int DownloadProgress = 0);

public sealed class AutomaticUpdateService
{
    internal const string RepositoryUrl = "https://github.com/KiRinXC/Hermes";

    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private UpdateManager? _manager;
    private UpdateInfo? _availableUpdate;
    private HermesUpdateState _state = new(HermesUpdateStatus.Idle, GetAssemblyVersion());

    public AutomaticUpdateService(AppLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler<HermesUpdateState>? StateChanged;

    public HermesUpdateState State => _state;

    public async Task<HermesUpdateState> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            EnsureManager();
            if (_manager is null || !_manager.IsInstalled)
            {
                return SetState(HermesUpdateStatus.Unsupported);
            }

            if (_manager.UpdatePendingRestart is { } pending)
            {
                _availableUpdate = null;
                return SetState(
                    HermesUpdateStatus.ReadyToRestart,
                    pending.Version.ToString(),
                    100);
            }

            SetState(HermesUpdateStatus.Checking);
            var update = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
            if (update is null)
            {
                _availableUpdate = null;
                _logger.Info("Hermes update check completed; the current version is up to date.");
                return SetState(HermesUpdateStatus.UpToDate);
            }

            _availableUpdate = update;
            var version = update.TargetFullRelease.Version.ToString();
            _logger.Info($"Hermes update {version} is available and awaits user confirmation.");
            return SetState(HermesUpdateStatus.Available, version);
        }
        catch (NotInstalledException)
        {
            // Debug and manual-test publishes are intentionally outside Velopack management.
            return SetState(HermesUpdateStatus.Unsupported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Automatic update check failed. {ex.GetType().Name}: {ex.Message}");
            return SetState(HermesUpdateStatus.Failed);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task InstallAndRestartAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            EnsureManager();
            if (_manager is null || !_manager.IsInstalled)
            {
                SetState(HermesUpdateStatus.Unsupported);
                return;
            }

            var prepared = _manager.UpdatePendingRestart;
            if (prepared is null)
            {
                if (_availableUpdate is null)
                {
                    SetState(HermesUpdateStatus.Failed);
                    return;
                }

                var version = _availableUpdate.TargetFullRelease.Version.ToString();
                SetState(HermesUpdateStatus.Downloading, version);
                _logger.Info($"Hermes update {version} download started after user confirmation.");
                await _manager.DownloadUpdatesAsync(
                    _availableUpdate,
                    progress => SetState(HermesUpdateStatus.Downloading, version, progress),
                    cancellationToken);
                prepared = _manager.UpdatePendingRestart ?? _availableUpdate.TargetFullRelease;
            }

            var preparedVersion = prepared.Version.ToString();
            SetState(HermesUpdateStatus.ReadyToRestart, preparedVersion, 100);
            _logger.Info($"Applying user-confirmed Hermes update {preparedVersion} and restarting.");
            _manager.ApplyUpdatesAndRestart(prepared, []);
        }
        catch (NotInstalledException)
        {
            SetState(HermesUpdateStatus.Unsupported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Hermes update installation failed. {ex.GetType().Name}: {ex.Message}");
            SetState(HermesUpdateStatus.Failed, _state.AvailableVersion);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void EnsureManager()
    {
        _manager ??= new UpdateManager(new GithubSource(RepositoryUrl, null, prerelease: false));
    }

    private HermesUpdateState SetState(
        HermesUpdateStatus status,
        string? availableVersion = null,
        int downloadProgress = 0)
    {
        var currentVersion = _manager?.CurrentVersion?.ToString() ?? GetAssemblyVersion();
        var next = new HermesUpdateState(
            status,
            currentVersion,
            availableVersion,
            Math.Clamp(downloadProgress, 0, 100));
        if (next == _state)
        {
            return next;
        }

        _state = next;
        StateChanged?.Invoke(this, next);
        return next;
    }

    private static string GetAssemblyVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version
            ?? typeof(AutomaticUpdateService).Assembly.GetName().Version;
        return version is null
            ? "未知"
            : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
