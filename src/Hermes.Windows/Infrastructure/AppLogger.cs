using System.IO;

namespace Hermes.Windows.Infrastructure;

public sealed class AppLogger
{
    private readonly object _gate = new();

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var safeException = exception is null ? string.Empty : $" {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", message + safeException);
    }

    private void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureCreated();
            var line = $"{DateTimeOffset.Now:O} [{level}] {Redactor.RedactSecrets(message)}{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(AppPaths.LogPath, line);
            }
        }
        catch
        {
            // Logging must never bring down the desktop input path.
        }
    }
}
