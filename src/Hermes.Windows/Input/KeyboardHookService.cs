using System.Diagnostics;
using System.Runtime.InteropServices;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Input;

public sealed class KeyboardHookService : IDisposable
{
    private const uint VirtualKeyEscape = 0x1B;

    private readonly AppLogger _logger;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hookHandle;

    public KeyboardHookService(AppLogger logger)
    {
        _logger = logger;
        _hookProc = HookCallback;
    }

    public bool IsEnabled => _hookHandle != IntPtr.Zero;

    public event EventHandler? UserActivity;

    public event EventHandler? EscapePressed;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            _logger.Warning($"Failed to install keyboard hook: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
                {
                    var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                    UserActivity?.Invoke(this, EventArgs.Empty);
                    if (data.vkCode == VirtualKeyEscape)
                    {
                        EscapePressed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Keyboard hook callback failed.", ex);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
