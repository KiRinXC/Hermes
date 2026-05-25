using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Settings;

public sealed class DpapiSecretStorageService : ISecretStorageService
{
    private readonly AppLogger _logger;

    public DpapiSecretStorageService(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureCreated();
        var protectedBytes = Protect(Encoding.UTF8.GetBytes(apiKey));
        await File.WriteAllBytesAsync(AppPaths.SecretsPath, protectedBytes, cancellationToken);
    }

    public async Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(AppPaths.SecretsPath))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(AppPaths.SecretsPath, cancellationToken);
            var bytes = Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Saved API key could not be decrypted. {ex.Message}");
            return null;
        }
    }

    public Task ClearApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(AppPaths.SecretsPath))
        {
            File.Delete(AppPaths.SecretsPath);
        }

        return Task.CompletedTask;
    }

    public bool HasApiKey() => File.Exists(AppPaths.SecretsPath);

    private static byte[] Protect(byte[] data)
    {
        var input = CreateBlob(data);
        try
        {
            if (!NativeMethods.CryptProtectData(ref input, "Hermes API key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw new InvalidOperationException($"CryptProtectData failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                return ReadBlob(output);
            }
            finally
            {
                NativeMethods.LocalFree(output.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
        }
    }

    private static byte[] Unprotect(byte[] protectedData)
    {
        var input = CreateBlob(protectedData);
        try
        {
            if (!NativeMethods.CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out var output))
            {
                throw new InvalidOperationException($"CryptUnprotectData failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                return ReadBlob(output);
            }
            finally
            {
                NativeMethods.LocalFree(output.pbData);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input.pbData);
        }
    }

    private static NativeMethods.DATA_BLOB CreateBlob(byte[] data)
    {
        var blob = new NativeMethods.DATA_BLOB
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };
        Marshal.Copy(data, 0, blob.pbData, data.Length);
        return blob;
    }

    private static byte[] ReadBlob(NativeMethods.DATA_BLOB blob)
    {
        var data = new byte[blob.cbData];
        Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        return data;
    }
}
