using System.IO;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexLocations
{
    public const string AppId = "codex-auth-switch-sync";

    public CodexLocations(string? codexHome = null, string? appDataDirectory = null)
    {
        CodexHome = Path.GetFullPath(codexHome ?? ResolveCodexHome());
        AppDataDirectory = Path.GetFullPath(appDataDirectory ?? AppPaths.GetAppDirectory(AppId));
    }

    public string CodexHome { get; }

    public string AppDataDirectory { get; }

    public string ConfigPath => Path.Combine(CodexHome, "config.toml");

    public string AuthPath => Path.Combine(CodexHome, "auth.json");

    public string ProfilesDirectory => Path.Combine(AppDataDirectory, "profiles");

    public string BackupsDirectory => Path.Combine(AppDataDirectory, "backups");

    public string GetProfilePath(Domain.CodexAuthMode mode) => Path.Combine(
        ProfilesDirectory,
        mode == Domain.CodexAuthMode.ChatGpt ? "chatgpt.profile" : "api.profile");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(CodexHome);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }

    private static string ResolveCodexHome()
    {
        var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Environment.ExpandEnvironmentVariables(configured);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }
}
