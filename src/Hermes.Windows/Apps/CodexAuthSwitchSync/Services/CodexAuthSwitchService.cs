using System.IO;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexAuthSwitchService : IDisposable
{
    private const int BackupsToKeep = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly CodexLocations _locations;
    private readonly CodexProfileStore _profiles;
    private readonly CodexSessionSyncService _sessions;
    private readonly CodexProcessGuard _processGuard;
    private readonly CodexBrowserLoginLauncher _browserLogin = new();
    private readonly AppLogger _logger;
    private Exception? _browserLoginRecoveryError;
    private int _activeOperations;

    public CodexAuthSwitchService(
        CodexLocations locations,
        CodexProfileStore profiles,
        CodexSessionSyncService sessions,
        CodexProcessGuard processGuard,
        AppLogger logger)
    {
        _locations = locations;
        _profiles = profiles;
        _sessions = sessions;
        _processGuard = processGuard;
        _logger = logger;
        _processGuard.OwnedLoginProcessIdProvider = () => _browserLogin.ActiveProcessId;
        TryRecoverInterruptedBrowserLogin();
    }

    public bool IsOperationInProgress => Volatile.Read(ref _activeOperations) > 0;

    public CodexStatus GetStatus()
    {
        var config = ReadConfig();
        var auth = ReadAuth();
        return new CodexStatus(
            CodexProfileStore.InferMode(auth),
            CodexToml.Provider(config),
            CodexToml.ReadRootString(config, "model") ?? "（默认）",
            CodexToml.ReadRootString(config, "model_reasoning_effort") ?? "（默认）",
            _profiles.GetSummary(CodexAuthMode.ChatGpt),
            _profiles.GetSummary(CodexAuthMode.Api),
            _sessions.GetAlignment(config),
            _processGuard.GetRunningClients());
    }

    public CodexProfileSummary CaptureCurrent(CodexAuthMode mode)
    {
        using var activity = BeginOperation();
        var result = _profiles.CaptureCurrent(mode);
        _logger.Info($"Codex {ModeLabel(mode)} profile captured.");
        return result;
    }

    public CodexApiProfileDraft GetApiDraft() => _profiles.GetApiDraft();

    public CodexProfileSummary ConfigureApiFiles(string configText, string? authText)
    {
        using var activity = BeginOperation();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = ReadConfig();
        var activeAuth = ReadAuth();
        var activeMode = CodexProfileStore.InferMode(activeAuth);
        if (activeMode != CodexAuthMode.Api)
        {
            var saved = _profiles.ConfigureApiFiles(configText, authText);
            _logger.Info($"Codex API profile configured for provider {saved.Provider}.");
            return saved;
        }

        _processGuard.EnsureClientsClosed();
        var backup = CreateBackupDirectory();
        var activeFiles = BackupActiveFiles(backup);
        var profileFile = BackupProfile(CodexAuthMode.Api, backup);

        try
        {
            var saved = _profiles.ConfigureApiFiles(configText, authText);
            var target = _profiles.Load(CodexAuthMode.Api);
            var mergedConfig = CodexToml.MergeForAuthSwitch(activeConfig, target.ConfigText);
            var targetProvider = CodexToml.Provider(target.ConfigText);
            AtomicFile.WriteAllText(_locations.ConfigPath, mergedConfig);
            AtomicFile.WriteAllText(_locations.AuthPath, target.AuthText);
            var sync = _sessions.Sync(mergedConfig, targetProvider, backup);
            TryPruneBackups();
            _logger.Info(
                $"Codex active API profile updated for provider {saved.Provider}; synchronized {sync.RolloutFilesUpdated} rollout files and {sync.SqliteRowsUpdated} thread rows.");
            return saved;
        }
        catch
        {
            RestoreActiveFiles(activeFiles);
            RestoreProfile(CodexAuthMode.Api, profileFile);
            throw;
        }
    }

    public CodexOperationResult Switch(CodexAuthMode targetMode)
    {
        using var activity = BeginOperation();
        if (targetMode == CodexAuthMode.Unknown)
        {
            throw new CodexSwitchException("目标认证方式无效。");
        }

        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = ReadConfig();
        var activeAuth = ReadAuth();
        var activeMode = CodexProfileStore.InferMode(activeAuth);
        if (activeMode != CodexAuthMode.Unknown)
        {
            // Refresh only authentication material. The live merged config can also contain
            // unrelated MCP/plugin settings that must not travel with an auth profile.
            _profiles.RefreshAuth(activeMode, activeConfig, activeAuth);
        }

        var target = _profiles.Load(targetMode);
        var targetProvider = CodexToml.Provider(target.ConfigText);
        var mergedConfig = CodexToml.MergeForAuthSwitch(activeConfig, target.ConfigText);
        var backup = CreateBackupDirectory();
        var activeFiles = BackupActiveFiles(backup);

        try
        {
            AtomicFile.WriteAllText(_locations.ConfigPath, mergedConfig);
            AtomicFile.WriteAllText(_locations.AuthPath, target.AuthText);
            var result = _sessions.Sync(mergedConfig, targetProvider, backup);
            TryPruneBackups();
            _logger.Info(
                $"Codex auth switched to {ModeLabel(targetMode)}; synchronized {result.RolloutFilesUpdated} rollout files and {result.SqliteRowsUpdated} thread rows.");
            return result;
        }
        catch
        {
            RestoreActiveFiles(activeFiles);
            throw;
        }
    }

    public async Task<CodexOperationResult> LoginChatGptWithBrowserAsync(CancellationToken cancellationToken)
    {
        EnsureBrowserLoginRecovered();
        using var activity = BeginOperation();
        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = ReadConfig();
        var activeAuth = ReadAuth();
        var activeMode = CodexProfileStore.InferMode(activeAuth);
        if (activeMode != CodexAuthMode.Unknown)
        {
            _profiles.RefreshAuth(activeMode, activeConfig, activeAuth);
        }

        var chatGptConfig = CodexToml.BuildChatGptLoginConfig(activeConfig);
        var backup = CreateBackupDirectory();
        var activeFiles = BackupActiveFiles(backup);
        var profileFile = BackupProfile(CodexAuthMode.ChatGpt, backup);
        WriteBrowserLoginJournal(new BrowserLoginTransaction(
            1,
            backup,
            profileFile.Present,
            BrowserLoginPhase.Pending));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFile.WriteAllText(_locations.ConfigPath, chatGptConfig);
            if (File.Exists(_locations.AuthPath))
            {
                File.Delete(_locations.AuthPath);
            }

            _logger.Info("Codex official ChatGPT browser login started.");
            await _browserLogin.LoginAsync(_locations.CodexHome, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(_locations.AuthPath))
            {
                throw new CodexSwitchException("浏览器认证已结束，但 Codex 没有生成 auth.json。当前配置已恢复，请重试。");
            }

            var chatGptAuth = File.ReadAllText(_locations.AuthPath, Encoding.UTF8);
            if (CodexProfileStore.InferMode(chatGptAuth) != CodexAuthMode.ChatGpt)
            {
                throw new CodexSwitchException("Codex 未返回可识别的 ChatGPT 认证。当前配置已恢复，请重试。");
            }

            _profiles.Save(CodexAuthMode.ChatGpt, chatGptConfig, chatGptAuth);
            var result = _sessions.Sync(chatGptConfig, "openai", backup);
            WriteBrowserLoginJournal(new BrowserLoginTransaction(
                1,
                backup,
                profileFile.Present,
                BrowserLoginPhase.Committed));
            DeleteBrowserLoginJournal();
            TryPruneBackups();
            _logger.Info(
                $"Codex ChatGPT browser login completed; synchronized {result.RolloutFilesUpdated} rollout files and {result.SqliteRowsUpdated} thread rows.");
            return result;
        }
        catch (Exception operationError)
        {
            var recoveryError = RestoreBrowserLoginState(backup, activeFiles, profileFile);
            if (recoveryError is not null)
            {
                _logger.Error("Codex browser login rollback was incomplete.", recoveryError);
                throw new CodexSwitchException(
                    "登录已停止，但原 Codex 状态未能完全恢复。请重启 Hermes 以再次自动恢复。",
                    new AggregateException(operationError, recoveryError));
            }

            DeleteBrowserLoginJournal();
            throw;
        }
    }

    public CodexOperationResult SyncCurrent()
    {
        using var activity = BeginOperation();
        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        var config = ReadConfig();
        var provider = CodexToml.Provider(config);
        var backup = CreateBackupDirectory();
        var result = _sessions.Sync(config, provider, backup);
        TryPruneBackups();
        _logger.Info(
            $"Codex sessions synchronized for provider {provider}; updated {result.RolloutFilesUpdated} rollout files and {result.SqliteRowsUpdated} thread rows.");
        return result;
    }

    public void Dispose() => _browserLogin.Dispose();

    private string ReadConfig() => File.Exists(_locations.ConfigPath)
        ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
        : string.Empty;

    private string ReadAuth() => File.Exists(_locations.AuthPath)
        ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8)
        : "{}";

    private ActiveFilesBackup BackupActiveFiles(string backupDirectory)
    {
        var backup = new ActiveFilesBackup(
            File.Exists(_locations.ConfigPath),
            File.Exists(_locations.AuthPath),
            File.Exists(_locations.ConfigPath) ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8) : null,
            File.Exists(_locations.AuthPath) ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8) : null);
        var clear = JsonSerializer.SerializeToUtf8Bytes(backup, JsonOptions);
        AtomicFile.WriteAllBytes(Path.Combine(backupDirectory, "active-files.dat"), CodexDpapi.Protect(clear));
        return backup;
    }

    private ProfileFileBackup BackupProfile(CodexAuthMode mode, string backupDirectory)
    {
        var path = _locations.GetProfilePath(mode);
        var backup = new ProfileFileBackup(
            File.Exists(path),
            File.Exists(path) ? File.ReadAllBytes(path) : null);
        if (backup.Bytes is not null)
        {
            AtomicFile.WriteAllBytes(Path.Combine(backupDirectory, ProfileBackupFileName(mode)), backup.Bytes);
        }

        return backup;
    }

    private void RestoreActiveFiles(ActiveFilesBackup backup)
    {
        RestoreFile(_locations.ConfigPath, backup.ConfigPresent, backup.ConfigText);
        RestoreFile(_locations.AuthPath, backup.AuthPresent, backup.AuthText);
    }

    private void RestoreProfile(CodexAuthMode mode, ProfileFileBackup backup)
    {
        var path = _locations.GetProfilePath(mode);
        if (!backup.Present)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        AtomicFile.WriteAllBytes(path, backup.Bytes ?? []);
    }

    private static void RestoreFile(string path, bool wasPresent, string? content)
    {
        if (!wasPresent)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        AtomicFile.WriteAllText(path, content ?? string.Empty);
    }

    private void TryRecoverInterruptedBrowserLogin()
    {
        if (!File.Exists(_locations.BrowserLoginJournalPath))
        {
            return;
        }

        try
        {
            using var operationLock = CodexOperationLock.Acquire(_locations);
            var transaction = JsonSerializer.Deserialize<BrowserLoginTransaction>(
                File.ReadAllText(_locations.BrowserLoginJournalPath, Encoding.UTF8),
                JsonOptions) ?? throw new CodexSwitchException("Codex 登录恢复记录无效。");
            var backupDirectory = ValidateBackupDirectory(transaction.BackupDirectory);
            if (transaction.Phase == BrowserLoginPhase.Pending)
            {
                var activeFiles = ReadActiveFilesBackup(backupDirectory);
                var profileFile = ReadProfileBackup(
                    CodexAuthMode.ChatGpt,
                    backupDirectory,
                    transaction.ChatGptProfilePresent);
                var recoveryError = RestoreBrowserLoginState(backupDirectory, activeFiles, profileFile);
                if (recoveryError is not null)
                {
                    throw new CodexSwitchException("Codex 登录事务恢复不完整。", recoveryError);
                }

                _logger.Warning("Recovered an interrupted Codex ChatGPT browser login transaction.");
            }

            DeleteBrowserLoginJournal();
        }
        catch (Exception ex)
        {
            _browserLoginRecoveryError = ex;
            _logger.Error("Interrupted Codex browser login could not be recovered.", ex);
        }
    }

    private void EnsureBrowserLoginRecovered()
    {
        if (_browserLoginRecoveryError is not null)
        {
            throw new CodexSwitchException(
                "上次 ChatGPT 登录中断后的配置无法自动恢复。请先检查 Hermes 日志与 Codex 文件权限。",
                _browserLoginRecoveryError);
        }
    }

    private ActiveFilesBackup ReadActiveFilesBackup(string backupDirectory)
    {
        var path = Path.Combine(backupDirectory, "active-files.dat");
        var clear = CodexDpapi.Unprotect(File.ReadAllBytes(path));
        return JsonSerializer.Deserialize<ActiveFilesBackup>(clear, JsonOptions)
            ?? throw new CodexSwitchException("Codex 活动文件备份无效。");
    }

    private ProfileFileBackup ReadProfileBackup(CodexAuthMode mode, string backupDirectory, bool wasPresent)
    {
        if (!wasPresent)
        {
            return new ProfileFileBackup(false, null);
        }

        var path = Path.Combine(backupDirectory, ProfileBackupFileName(mode));
        return new ProfileFileBackup(true, File.ReadAllBytes(path));
    }

    private Exception? RestoreBrowserLoginState(
        string backupDirectory,
        ActiveFilesBackup activeFiles,
        ProfileFileBackup profileFile)
    {
        var errors = new List<Exception>();
        try
        {
            _sessions.Restore(backupDirectory);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            RestoreActiveFiles(activeFiles);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            RestoreProfile(CodexAuthMode.ChatGpt, profileFile);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        return errors.Count switch
        {
            0 => null,
            1 => errors[0],
            _ => new AggregateException(errors)
        };
    }

    private string ValidateBackupDirectory(string backupDirectory)
    {
        var root = Path.GetFullPath(_locations.BackupsDirectory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(backupDirectory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new CodexSwitchException("Codex 登录恢复记录指向了无效的备份目录。");
        }

        return candidate.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void WriteBrowserLoginJournal(BrowserLoginTransaction transaction)
    {
        AtomicFile.WriteAllText(
            _locations.BrowserLoginJournalPath,
            JsonSerializer.Serialize(transaction, JsonOptions));
    }

    private void DeleteBrowserLoginJournal()
    {
        if (File.Exists(_locations.BrowserLoginJournalPath))
        {
            File.Delete(_locations.BrowserLoginJournalPath);
        }
    }

    private string CreateBackupDirectory()
    {
        Directory.CreateDirectory(_locations.BackupsDirectory);
        var name = $"{DateTime.Now:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
        var path = Path.Combine(_locations.BackupsDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private void PruneBackups()
    {
        var root = Path.GetFullPath(_locations.BackupsDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var appRoot = Path.GetFullPath(_locations.AppDataDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(root), appRoot, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(root), "backups", StringComparison.OrdinalIgnoreCase))
        {
            throw new CodexSwitchException("拒绝清理非托管备份目录。");
        }

        var directories = Directory.EnumerateDirectories(root)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        foreach (var old in directories.Take(Math.Max(0, directories.Length - BackupsToKeep)))
        {
            Directory.Delete(old, recursive: true);
        }
    }

    private void TryPruneBackups()
    {
        try
        {
            PruneBackups();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Old Codex auth backups could not be pruned. {ex.GetType().Name}");
        }
    }

    private IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationActivity(this);
    }

    private static string ProfileBackupFileName(CodexAuthMode mode) =>
        mode == CodexAuthMode.ChatGpt ? "chatgpt-profile.dat" : "api-profile.dat";

    private static string ModeLabel(CodexAuthMode mode) => mode switch
    {
        CodexAuthMode.ChatGpt => "ChatGPT",
        CodexAuthMode.Api => "API",
        _ => "Unknown"
    };

    private sealed record ActiveFilesBackup(
        bool ConfigPresent,
        bool AuthPresent,
        string? ConfigText,
        string? AuthText);

    private sealed record ProfileFileBackup(
        bool Present,
        byte[]? Bytes);

    private sealed record BrowserLoginTransaction(
        int Version,
        string BackupDirectory,
        bool ChatGptProfilePresent,
        string Phase);

    private static class BrowserLoginPhase
    {
        public const string Pending = "pending";
        public const string Committed = "committed";
    }

    private sealed class OperationActivity(CodexAuthSwitchService owner) : IDisposable
    {
        private CodexAuthSwitchService? _owner = owner;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            if (current is not null)
            {
                Interlocked.Decrement(ref current._activeOperations);
            }
        }
    }
}
