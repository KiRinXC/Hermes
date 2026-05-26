using Hermes.Windows.Input;

namespace Hermes.Tests.Input;

public static class HotkeyGestureTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("validates hotkey gesture", ValidatesHotkeyGesture);
        suite.Add("rejects bare hotkey gesture", RejectsBareHotkeyGesture);
    }

    private static void ValidatesHotkeyGesture()
    {
        TestAssert.True(HotkeyGesture.TryParse("Ctrl+Alt+E", out var gesture));
        TestAssert.Equal("Ctrl+Alt+E", gesture.ToString());
    }

    private static void RejectsBareHotkeyGesture()
    {
        TestAssert.False(HotkeyGesture.TryParse("E", out _));
    }
}
