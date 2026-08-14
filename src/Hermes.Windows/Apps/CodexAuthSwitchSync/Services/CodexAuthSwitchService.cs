using System.IO;
using System.Text;
using System.Text.Json;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexAuthSwitchService
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
    }

    public CodexStatus GetStatus()
    {
        var config = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var auth = File.Exists(_locations.AuthPath)
            ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8)
            : "{}";
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
        var result = _profiles.CaptureCurrent(mode);
        _logger.Info($"Codex {ModeLabel(mode)} profile captured.");
        return result;
    }

    public CodexApiProfileDraft GetApiDraft() => _profiles.GetApiDraft();

    public CodexProfileSummary ConfigureApiFiles(string configText, string? authText)
    {
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var activeAuth = File.Exists(_locations.AuthPath)
            ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8)
            : "{}";
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
        var profileFile = BackupApiProfile(backup);

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
            RestoreApiProfile(profileFile);
            throw;
        }
    }

    public CodexOperationResult Switch(CodexAuthMode targetMode)
    {
        if (targetMode == CodexAuthMode.Unknown)
        {
            throw new CodexSwitchException("目标认证方式无效。");
        }

        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var activeAuth = File.Exists(_locations.AuthPath)
            ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8)
            : "{}";
        var activeMode = CodexProfileStore.InferMode(activeAuth);
        if (activeMode != CodexAuthMode.Unknown)
        {
            // Keep refreshed tokens without turning the fully merged live config into the
            // profile's next overlay. Otherwise stale MCP/plugin settings would travel back
            // with the profile on a later switch.
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

    public CodexOperationResult LoginChatGptWithBrowser()
    {
        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        _locations.EnsureCreated();

        var activeConfig = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var activeAuth = File.Exists(_locations.AuthPath)
            ? File.ReadAllText(_locations.AuthPath, Encoding.UTF8)
            : "{}";
        var activeMode = CodexProfileStore.InferMode(activeAuth);
        if (activeMode != CodexAuthMode.Unknown)
        {
            _profiles.RefreshAuth(activeMode, activeConfig, activeAuth);
        }

        var chatGptConfig = CodexToml.BuildChatGptLoginConfig(activeConfig);
        var backup = CreateBackupDirectory();
        var activeFiles = BackupActiveFiles(backup);

        try
        {
            AtomicFile.WriteAllText(_locations.ConfigPath, chatGptConfig);
            if (File.Exists(_locations.AuthPath))
            {
                File.Delete(_locations.AuthPath);
            }

            _logger.Info("Codex official ChatGPT browser login started.");
            _browserLogin.Login(_locations.CodexHome);
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
            TryPruneBackups();
            _logger.Info(
                $"Codex ChatGPT browser login completed; synchronized {result.RolloutFilesUpdated} rollout files and {result.SqliteRowsUpdated} thread rows.");
            return result;
        }
        catch
        {
            RestoreActiveFiles(activeFiles);
            throw;
        }
    }

    public CodexOperationResult SyncCurrent()
    {
        _processGuard.EnsureClientsClosed();
        using var operationLock = CodexOperationLock.Acquire(_locations);
        var config = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var provider = CodexToml.Provider(config);
        var backup = CreateBackupDirectory();
        var result = _sessions.Sync(config, provider, backup);
        TryPruneBackups();
        _logger.Info(
            $"Codex sessions synchronized for provider {provider}; updated {result.RolloutFilesUpdated} rollout files and {result.SqliteRowsUpdated} thread rows.");
        return result;
    }

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

    private ProfileFileBackup BackupApiProfile(string backupDirectory)
    {
        var path = _locations.GetProfilePath(CodexAuthMode.Api);
        var backup = new ProfileFileBackup(
            File.Exists(path),
            File.Exists(path) ? File.ReadAllBytes(path) : null);
        if (backup.Bytes is not null)
        {
            AtomicFile.WriteAllBytes(Path.Combine(backupDirectory, "api-profile.dat"), backup.Bytes);
        }

        return backup;
    }

    private void RestoreActiveFiles(ActiveFilesBackup backup)
    {
        RestoreFile(_locations.ConfigPath, backup.ConfigPresent, backup.ConfigText);
        RestoreFile(_locations.AuthPath, backup.AuthPresent, backup.AuthText);
    }

    private void RestoreApiProfile(ProfileFileBackup backup)
    {
        var path = _locations.GetProfilePath(CodexAuthMode.Api);
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
}
