using System.IO;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

internal static class AtomicFile
{
    public static void WriteAllText(string path, string value)
    {
        WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(value));
    }

    public static void WriteAllBytes(string path, ReadOnlySpan<byte> value)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"文件没有父目录：{path}");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            {
                stream.Write(value);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
