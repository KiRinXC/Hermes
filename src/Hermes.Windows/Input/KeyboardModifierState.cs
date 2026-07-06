using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Input;

internal static class KeyboardModifierState
{
    private static int _leftCtrlDown;
    private static int _rightCtrlDown;
    private static int _leftAltDown;
    private static int _rightAltDown;
    private static long _lastCtrlDownTimeMs;
    private static long _lastCtrlUpTimeMs;
    private static long _lastAltDownTimeMs;
    private static long _lastAltUpTimeMs;

    public static bool IsCtrlDown()
    {
        return Volatile.Read(ref _leftCtrlDown) != 0
            || Volatile.Read(ref _rightCtrlDown) != 0
            || IsKeyDown(NativeMethods.VkControl)
            || IsKeyDown(NativeMethods.VkLControl)
            || IsKeyDown(NativeMethods.VkRControl);
    }

    public static bool IsAltDown()
    {
        return Volatile.Read(ref _leftAltDown) != 0
            || Volatile.Read(ref _rightAltDown) != 0
            || IsKeyDown(NativeMethods.VkMenu)
            || IsKeyDown(NativeMethods.VkLMenu)
            || IsKeyDown(NativeMethods.VkRMenu);
    }

    public static void NoteKeyState(uint virtualKey, bool isDown, uint messageTimeMs = 0)
    {
        var value = isDown ? 1 : 0;
        var eventTimeMs = messageTimeMs == 0 ? Environment.TickCount64 : messageTimeMs;
        switch (virtualKey)
        {
            case NativeMethods.VkControl:
            case NativeMethods.VkLControl:
                Interlocked.Exchange(ref _leftCtrlDown, value);
                RecordTransition(ref _lastCtrlDownTimeMs, ref _lastCtrlUpTimeMs, isDown, eventTimeMs);
                break;
            case NativeMethods.VkRControl:
                Interlocked.Exchange(ref _rightCtrlDown, value);
                RecordTransition(ref _lastCtrlDownTimeMs, ref _lastCtrlUpTimeMs, isDown, eventTimeMs);
                break;
            case NativeMethods.VkMenu:
            case NativeMethods.VkLMenu:
                Interlocked.Exchange(ref _leftAltDown, value);
                RecordTransition(ref _lastAltDownTimeMs, ref _lastAltUpTimeMs, isDown, eventTimeMs);
                break;
            case NativeMethods.VkRMenu:
                Interlocked.Exchange(ref _rightAltDown, value);
                RecordTransition(ref _lastAltDownTimeMs, ref _lastAltUpTimeMs, isDown, eventTimeMs);
                break;
        }
    }

    public static bool WasCtrlDownAt(long messageTimeMs, TimeSpan tolerance, bool allowCurrentStateFallback = true)
    {
        return WasModifierDownAt(
            messageTimeMs,
            tolerance,
            IsCtrlDown(),
            allowCurrentStateFallback,
            Volatile.Read(ref _lastCtrlDownTimeMs),
            Volatile.Read(ref _lastCtrlUpTimeMs));
    }

    public static bool WasAltDownAt(long messageTimeMs, TimeSpan tolerance, bool allowCurrentStateFallback = true)
    {
        return WasModifierDownAt(
            messageTimeMs,
            tolerance,
            IsAltDown(),
            allowCurrentStateFallback,
            Volatile.Read(ref _lastAltDownTimeMs),
            Volatile.Read(ref _lastAltUpTimeMs));
    }

    public static void ResetTrackedState()
    {
        Interlocked.Exchange(ref _leftCtrlDown, 0);
        Interlocked.Exchange(ref _rightCtrlDown, 0);
        Interlocked.Exchange(ref _leftAltDown, 0);
        Interlocked.Exchange(ref _rightAltDown, 0);
        Interlocked.Exchange(ref _lastCtrlDownTimeMs, 0);
        Interlocked.Exchange(ref _lastCtrlUpTimeMs, 0);
        Interlocked.Exchange(ref _lastAltDownTimeMs, 0);
        Interlocked.Exchange(ref _lastAltUpTimeMs, 0);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    private static bool WasModifierDownAt(
        long messageTimeMs,
        TimeSpan tolerance,
        bool isCurrentlyDown,
        bool allowCurrentStateFallback,
        long lastDownTimeMs,
        long lastUpTimeMs)
    {
        if (lastDownTimeMs <= 0 && lastUpTimeMs <= 0)
        {
            return allowCurrentStateFallback && isCurrentlyDown;
        }

        if (lastDownTimeMs <= 0)
        {
            return allowCurrentStateFallback && isCurrentlyDown;
        }

        if (lastDownTimeMs > messageTimeMs)
        {
            return false;
        }

        if (lastUpTimeMs > lastDownTimeMs && lastUpTimeMs <= messageTimeMs)
        {
            return allowCurrentStateFallback && isCurrentlyDown;
        }

        return true;
    }

    private static void RecordTransition(ref long downTargetMs, ref long upTargetMs, bool isDown, long eventTimeMs)
    {
        if (isDown)
        {
            Interlocked.Exchange(ref downTargetMs, eventTimeMs);
        }
        else
        {
            Interlocked.Exchange(ref upTargetMs, eventTimeMs);
        }
    }
}
