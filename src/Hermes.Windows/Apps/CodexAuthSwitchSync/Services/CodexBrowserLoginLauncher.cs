using System.Diagnostics;
using System.IO;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

internal sealed class CodexBrowserLoginLauncher
{
    private const int LoginTimeoutMilliseconds = 15 * 60 * 1000;

    public void Login(string codexHome)
    {
        var executable = FindExecutable()
            ?? throw new CodexSwitchException(
                "未找到可用的 Codex 登录程序。请安装 Codex CLI，或安装并启用官方 Codex IDE 扩展后重试。");
        var startInfo = CreateStartInfo(executable, codexHome);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new CodexSwitchException("无法启动 Codex 官方浏览器登录流程。");
            if (!process.WaitForExit(LoginTimeoutMilliseconds))
            {
                TryStop(process);
                throw new CodexSwitchException("等待 ChatGPT 浏览器认证超时。请重新点击“浏览器登录”后完成认证。");
            }

            if (process.ExitCode != 0)
            {
                throw new CodexSwitchException("ChatGPT 浏览器认证未完成。当前 Codex 配置和认证已恢复，请重试。");
            }
        }
        catch (CodexSwitchException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CodexSwitchException("无法启动 Codex 官方浏览器登录流程。", ex);
        }
    }

    internal static string? FindExecutable(string? pathVariable = null, string? userProfile = null)
    {
        var pathDirectories = EnumeratePathDirectories(pathVariable).ToArray();

        foreach (var directory in pathDirectories)
        {
            var executable = Path.Combine(directory, "codex.exe");
            if (File.Exists(executable))
            {
                return executable;
            }
        }

        foreach (var directory in pathDirectories)
        {
            foreach (var relativePath in NativeNpmExecutablePaths)
            {
                var executable = Path.Combine(directory, relativePath);
                if (File.Exists(executable))
                {
                    return executable;
                }
            }
        }

        var profile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var extensionExecutable = FindEditorExtensionExecutable(profile);
        if (extensionExecutable is not null)
        {
            return extensionExecutable;
        }

        foreach (var directory in pathDirectories)
        {
            var command = Path.Combine(directory, "codex.cmd");
            if (File.Exists(command))
            {
                return command;
            }
        }

        return null;
    }

    private static ProcessStartInfo CreateStartInfo(string executable, string codexHome)
    {
        var isCommandFile = string.Equals(Path.GetExtension(executable), ".cmd", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = isCommandFile
                ? Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe")
                : executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = codexHome
        };

        if (isCommandFile)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add($"\"{executable}\" login");
        }
        else
        {
            startInfo.ArgumentList.Add("login");
        }

        startInfo.Environment["CODEX_HOME"] = codexHome;
        startInfo.Environment["NO_COLOR"] = "1";
        return startInfo;
    }

    private static IEnumerable<string> EnumeratePathDirectories(string? pathVariable)
    {
        var value = pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var item in value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string directory;
            try
            {
                directory = Environment.ExpandEnvironmentVariables(item.Trim('"'));
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (Directory.Exists(directory))
            {
                yield return directory;
            }
        }
    }

    private static string? FindEditorExtensionExecutable(string userProfile)
    {
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        var extensionRoots = new[]
        {
            Path.Combine(userProfile, ".vscode", "extensions"),
            Path.Combine(userProfile, ".vscode-insiders", "extensions"),
            Path.Combine(userProfile, ".cursor", "extensions"),
            Path.Combine(userProfile, ".windsurf", "extensions")
        };

        foreach (var root in extensionRoots)
        {
            try
            {
                var executable = Directory.Exists(root)
                    ? Directory.EnumerateDirectories(root, "openai.chatgpt-*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(Directory.GetLastWriteTimeUtc)
                        .Select(directory => Path.Combine(directory, "bin", "windows-x86_64", "codex.exe"))
                        .FirstOrDefault(File.Exists)
                    : null;
                if (executable is not null)
                {
                    return executable;
                }
            }
            catch (IOException)
            {
                // Try the next known editor extension root.
            }
            catch (UnauthorizedAccessException)
            {
                // Try the next known editor extension root.
            }
        }

        return null;
    }

    private static void TryStop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // The process exited between the timeout and the stop request.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The timeout error is still the useful message for the user.
        }
    }

    private static readonly string[] NativeNpmExecutablePaths =
    {
        Path.Combine(
            "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64",
            "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe"),
        Path.Combine(
            "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64",
            "vendor", "x86_64-pc-windows-msvc", "codex", "codex.exe")
    };
}
