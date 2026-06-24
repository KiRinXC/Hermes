using System.Diagnostics;
using System.Runtime.InteropServices;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Translation;

namespace Hermes.Windows.Input;

public sealed class MouseHookService : IDisposable
{
    internal static readonly TimeSpan ModifierStartTolerance = TimeSpan.FromMilliseconds(180);

    private readonly AppLogger _logger;
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hookHandle;
    private NativeMethods.POINT _downPoint;
    private DateTimeOffset _downAt;
    private long _downMessageTimeMs;
    private bool _isLeftDown;
    private bool _hasMoved;
    private bool _ctrlDownAtStart;
    private bool _ctrlHeldDuringDrag;
    private bool _hasActiveMode;
    private TranslationMode _gestureMode = TranslationMode.Translate;

    public MouseHookService(AppLogger logger)
    {
        _logger = logger;
        _hookProc = HookCallback;
    }

    public event EventHandler<MousePointEventArgs>? SelectionGestureCompleted;

    public event EventHandler<MouseActivityEventArgs>? UserActivity;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            _logger.Warning($"Failed to install mouse hook: {Marshal.GetLastWin32Error()}");
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
                var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                HandleMouseMessage(message, data.pt, data.time);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Mouse hook callback failed.", ex);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleMouseMessage(int message, NativeMethods.POINT point, uint messageTimeMs)
    {
        switch (message)
        {
            case NativeMethods.WmLButtonDown:
                _downAt = DateTimeOffset.Now;
                _downMessageTimeMs = messageTimeMs;
                _hasActiveMode = TryResolveGestureModeAtSelectionStart(_downMessageTimeMs, allowCurrentStateFallback: true, out _gestureMode);
                _ctrlDownAtStart = _hasActiveMode;
                _isLeftDown = ShouldTrackSelectionGesture(_ctrlDownAtStart);
                _hasMoved = false;
                _ctrlHeldDuringDrag = _ctrlDownAtStart;
                _downPoint = point;
                UserActivity?.Invoke(this, new MouseActivityEventArgs(point.X, point.Y, message));
                break;
            case NativeMethods.WmMouseMove:
                if (_isLeftDown && !_ctrlDownAtStart && TryResolveGestureModeAtSelectionStart(_downMessageTimeMs, allowCurrentStateFallback: false, out var recoveredMoveMode))
                {
                    _hasActiveMode = true;
                    _gestureMode = recoveredMoveMode;
                    _ctrlDownAtStart = true;
                    _ctrlHeldDuringDrag = true;
                }

                if (_isLeftDown && Distance(_downPoint, point) > 8)
                {
                    _hasMoved = true;
                }

                break;
            case NativeMethods.WmLButtonUp:
                var completedSelectionGesture = false;
                if (!_ctrlDownAtStart && TryResolveGestureModeAtSelectionStart(_downMessageTimeMs, allowCurrentStateFallback: false, out var releaseMode))
                {
                    _hasActiveMode = true;
                    _gestureMode = releaseMode;
                    _ctrlDownAtStart = true;
                    _ctrlHeldDuringDrag = true;
                }

                var modifierDownAtRelease = _hasActiveMode && IsGestureModifierHeld(_gestureMode);

                if (_isLeftDown && _hasMoved && ShouldEmitSelectionGesture(_ctrlDownAtStart, _ctrlHeldDuringDrag, modifierDownAtRelease))
                {
                    completedSelectionGesture = true;
                    SelectionGestureCompleted?.Invoke(
                        this,
                        new MousePointEventArgs(
                            _downPoint.X,
                            _downPoint.Y,
                            point.X,
                            point.Y,
                            _downAt,
                            DateTimeOffset.Now,
                            _gestureMode,
                            _ctrlDownAtStart,
                            _ctrlHeldDuringDrag,
                            modifierDownAtRelease));
                }

                _isLeftDown = false;
                _ctrlDownAtStart = false;
                _ctrlHeldDuringDrag = false;
                _hasActiveMode = false;
                if (!completedSelectionGesture)
                {
                    UserActivity?.Invoke(this, new MouseActivityEventArgs(point.X, point.Y, message));
                }

                break;
            case NativeMethods.WmMouseWheel:
            case NativeMethods.WmRButtonDown:
                UserActivity?.Invoke(this, new MouseActivityEventArgs(point.X, point.Y, message));
                break;
        }
    }

    private static bool TryResolveGestureMode(bool ctrlDown, bool altDown, out TranslationMode mode)
    {
        if (ctrlDown)
        {
            mode = TranslationMode.Translate;
            return true;
        }

        if (altDown)
        {
            mode = TranslationMode.Explain;
            return true;
        }

        mode = TranslationMode.Translate;
        return false;
    }

    private static bool IsGestureModifierHeld(TranslationMode mode)
    {
        return mode == TranslationMode.Explain
            ? KeyboardModifierState.IsAltDown()
            : KeyboardModifierState.IsCtrlDown();
    }

    private static double Distance(NativeMethods.POINT a, NativeMethods.POINT b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    internal static bool ShouldTrackSelectionGesture(bool ctrlDownAtStart)
    {
        return true;
    }

    internal static bool ShouldEmitSelectionGesture(bool ctrlDownAtStart, bool ctrlHeldDuringDrag, bool ctrlDownAtRelease)
    {
        return ctrlDownAtStart;
    }

    internal static bool IsModifierTimingCompatible(DateTimeOffset mouseDownAt, DateTimeOffset modifierDownAt)
    {
        return modifierDownAt <= mouseDownAt || modifierDownAt - mouseDownAt <= ModifierStartTolerance;
    }

    private static bool TryResolveGestureModeAtSelectionStart(long mouseDownMessageTimeMs, bool allowCurrentStateFallback, out TranslationMode mode)
    {
        if (KeyboardModifierState.WasCtrlDownAt(mouseDownMessageTimeMs, ModifierStartTolerance, allowCurrentStateFallback))
        {
            mode = TranslationMode.Translate;
            return true;
        }

        if (KeyboardModifierState.WasAltDownAt(mouseDownMessageTimeMs, ModifierStartTolerance, allowCurrentStateFallback))
        {
            mode = TranslationMode.Explain;
            return true;
        }

        mode = TranslationMode.Translate;
        return false;
    }
}

public sealed class MouseActivityEventArgs : EventArgs
{
    public MouseActivityEventArgs(int x, int y, int message)
    {
        X = x;
        Y = y;
        Message = message;
    }

    public int X { get; }

    public int Y { get; }

    public int Message { get; }
}

public sealed class MousePointEventArgs : EventArgs
{
    public MousePointEventArgs(
        int startX,
        int startY,
        int x,
        int y,
        DateTimeOffset startedAt,
        DateTimeOffset releasedAt,
        TranslationMode mode,
        bool ctrlDownAtStart,
        bool ctrlHeldDuringDrag,
        bool ctrlDownAtRelease)
    {
        StartX = startX;
        StartY = startY;
        X = x;
        Y = y;
        StartedAt = startedAt;
        ReleasedAt = releasedAt;
        Mode = mode;
        CtrlDownAtStart = ctrlDownAtStart;
        CtrlHeldDuringDrag = ctrlHeldDuringDrag;
        CtrlDownAtRelease = ctrlDownAtRelease;
    }

    public int StartX { get; }

    public int StartY { get; }

    public int X { get; }

    public int Y { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset ReleasedAt { get; }

    public TranslationMode Mode { get; }

    public bool CtrlDownAtStart { get; }

    public bool CtrlHeldDuringDrag { get; }

    public bool CtrlDownAtRelease { get; }
}
