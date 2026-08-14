using System.ComponentModel;
using System.Diagnostics;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public class CodexProcessGuard
{
    private static readonly HashSet<string> BlockedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chatgpt",
        "codex",
        "codex-code-mode-host",
        "codex-app-server"
    };

    internal Func<int?>? OwnedLoginProcessIdProvider { private get; set; }

    public virtual IReadOnlyList<string> GetRunningClients()
    {
        var found = new List<string>();
        var ownedLoginId = GetOwnedLoginProcessId();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId || !BlockedProcessNames.Contains(process.ProcessName))
                {
                    continue;
                }

                string? executablePath = null;
                try
                {
                    executablePath = process.MainModule?.FileName;
                }
                catch (Win32Exception)
                {
                    // Protected processes still receive a useful name and PID.
                }

                found.Add(DescribeProcess(
                    process.ProcessName,
                    process.Id,
                    executablePath,
                    process.Id == ownedLoginId));
            }
            catch (InvalidOperationException)
            {
                // The process exited while the list was being inspected.
            }
            catch (Win32Exception)
            {
                // Some protected processes cannot expose metadata to this user.
            }
            finally
            {
                process.Dispose();
            }
        }

        return found.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public virtual void EnsureClientsClosed()
    {
        var running = GetRunningClients();
        if (running.Count > 0)
        {
            throw new CodexSwitchException(
                $"请先完全退出 Codex、ChatGPT、CLI 与 IDE 扩展后重试（检测到：{string.Join("、", running)}）。");
        }
    }

    internal static string DescribeProcess(string processName, int processId, string? executablePath, bool ownedLogin)
    {
        var label = ownedLogin
            ? "Hermes 浏览器登录"
            : ClassifyProcess(processName, executablePath);
        return $"{label}（PID {processId}）";
    }

    private int? GetOwnedLoginProcessId()
    {
        try
        {
            return OwnedLoginProcessIdProvider?.Invoke();
        }
        catch
        {
            return null;
        }
    }

    private static string ClassifyProcess(string processName, string? executablePath)
    {
        if (processName.Equals("codex-code-mode-host", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex Code Mode Host";
        }

        if (processName.Equals("codex-app-server", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex App Server";
        }

        if (processName.Equals("chatgpt", StringComparison.OrdinalIgnoreCase))
        {
            return "ChatGPT 桌面端";
        }

        var path = executablePath?.Replace('/', '\\') ?? string.Empty;
        if (path.Contains("\\.vscode-insiders\\extensions\\openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
        {
            return "VS Code Insiders Codex 扩展";
        }

        if (path.Contains("\\.vscode\\extensions\\openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
        {
            return "VS Code Codex 扩展";
        }

        if (path.Contains("\\.cursor\\extensions\\openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
        {
            return "Cursor Codex 扩展";
        }

        if (path.Contains("\\.windsurf\\extensions\\openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
        {
            return "Windsurf Codex 扩展";
        }

        return "Codex CLI";
    }
}
