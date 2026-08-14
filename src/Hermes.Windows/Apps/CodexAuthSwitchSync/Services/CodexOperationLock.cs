using System.IO;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

internal sealed class CodexOperationLock : IDisposable
{
    private readonly string _path;
    private readonly FileStream _stream;

    private CodexOperationLock(string path, FileStream stream)
    {
        _path = path;
        _stream = stream;
    }

    public static CodexOperationLock Acquire(CodexLocations locations)
    {
        locations.EnsureCreated();
        var path = Path.Combine(locations.AppDataDirectory, "operation.lock");
        try
        {
            return Create(path);
        }
        catch (IOException) when (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromHours(1))
        {
            File.Delete(path);
            return Create(path);
        }
        catch (IOException ex)
        {
            throw new CodexSwitchException("另一个 Codex 认证操作正在运行。", ex);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
            // A stale lock is reclaimed after one hour.
        }
    }

    private static CodexOperationLock Create(string path)
    {
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, leaveOpen: true);
        writer.Write(Environment.ProcessId);
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return new CodexOperationLock(path, stream);
    }
}
