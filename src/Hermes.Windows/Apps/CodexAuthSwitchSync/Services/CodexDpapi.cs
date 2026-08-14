using System.Runtime.InteropServices;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

internal static class CodexDpapi
{
    private const uint CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(byte[] data)
    {
        var input = CreateBlob(data);
        try
        {
            if (!NativeMethods.CryptProtectData(
                    ref input,
                    "Hermes Codex auth profile",
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                throw new CodexSwitchException($"Windows 凭据加密失败（{Marshal.GetLastWin32Error()}）。");
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

    public static byte[] Unprotect(byte[] protectedData)
    {
        var input = CreateBlob(protectedData);
        try
        {
            if (!NativeMethods.CryptUnprotectData(
                    ref input,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                throw new CodexSwitchException("认证档案无法由当前 Windows 用户解密。");
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
        var memory = Marshal.AllocHGlobal(Math.Max(1, data.Length));
        if (data.Length > 0)
        {
            Marshal.Copy(data, 0, memory, data.Length);
        }

        return new NativeMethods.DATA_BLOB { cbData = data.Length, pbData = memory };
    }

    private static byte[] ReadBlob(NativeMethods.DATA_BLOB blob)
    {
        var data = new byte[blob.cbData];
        if (blob.cbData > 0)
        {
            Marshal.Copy(blob.pbData, data, 0, blob.cbData);
        }

        return data;
    }
}
