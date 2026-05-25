using System.Windows.Interop;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Input;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1001;

    private readonly AppLogger _logger;
    private HwndSource? _source;
    private bool _registered;

    public HotkeyService(AppLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler? HotkeyPressed;

    public void Register(HotkeyGesture gesture)
    {
        EnsureSource();
        Unregister();

        if (_source?.Handle is not { } handle || handle == IntPtr.Zero)
        {
            return;
        }

        _registered = NativeMethods.RegisterHotKey(handle, HotkeyId, gesture.Modifiers, gesture.VirtualKey);
        if (!_registered)
        {
            _logger.Warning($"Failed to register hotkey {gesture}.");
        }
    }

    public void Unregister()
    {
        if (_registered && _source?.Handle is { } handle && handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(handle, HotkeyId);
        }

        _registered = false;
    }

    public void Dispose()
    {
        Unregister();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void EnsureSource()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("HermesHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.HotkeyMessage && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
