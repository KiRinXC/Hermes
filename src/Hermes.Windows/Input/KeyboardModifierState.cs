using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Input;

internal static class KeyboardModifierState
{
    public static bool IsCtrlDown()
    {
        return IsKeyDown(NativeMethods.VkControl)
            || IsKeyDown(NativeMethods.VkLControl)
            || IsKeyDown(NativeMethods.VkRControl);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }
}
