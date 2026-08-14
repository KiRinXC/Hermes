using System.Diagnostics;
using System.ComponentModel;

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

    public virtual IReadOnlyList<string> GetRunningClients()
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id != Environment.ProcessId && BlockedProcessNames.Contains(process.ProcessName))
                {
                    found.Add(process.ProcessName + ".exe");
                }
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

        return found.ToArray();
    }

    public virtual void EnsureClientsClosed()
    {
        var running = GetRunningClients();
        if (running.Count > 0)
        {
            throw new CodexSwitchException(
                $"请先完全退出 Codex 桌面端、CLI 和 IDE 扩展后重试（检测到：{string.Join("、", running)}）。");
        }
    }
}
