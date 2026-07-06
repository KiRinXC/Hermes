using Hermes.Windows.Infrastructure;
using Hermes.Windows.Input;

namespace Hermes.Tests.Input;

public static class KeyboardModifierStateTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("keyboard modifier state tracks low level hook events", TracksLowLevelHookEvents);
        suite.Add("keyboard modifier state resolves selection start release order", ResolvesSelectionStartReleaseOrder);
        suite.Add("keyboard hook ignores modifier only activity", IgnoresModifierOnlyActivity);
    }

    private static void TracksLowLevelHookEvents()
    {
        KeyboardModifierState.ResetTrackedState();

        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: true);
        TestAssert.True(KeyboardModifierState.IsCtrlDown());

        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: false);
        TestAssert.False(KeyboardModifierState.IsCtrlDown());

        KeyboardModifierState.NoteKeyState(NativeMethods.VkRMenu, isDown: true);
        TestAssert.True(KeyboardModifierState.IsAltDown());

        KeyboardModifierState.NoteKeyState(NativeMethods.VkRMenu, isDown: false);
        TestAssert.False(KeyboardModifierState.IsAltDown());
    }

    private static void ResolvesSelectionStartReleaseOrder()
    {
        var tolerance = TimeSpan.FromMilliseconds(180);
        KeyboardModifierState.ResetTrackedState();

        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: true, messageTimeMs: 1_000);
        TestAssert.True(KeyboardModifierState.WasCtrlDownAt(messageTimeMs: 1_100, tolerance));

        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: false, messageTimeMs: 1_200);
        TestAssert.True(KeyboardModifierState.WasCtrlDownAt(messageTimeMs: 1_100, tolerance));
        TestAssert.False(KeyboardModifierState.WasCtrlDownAt(messageTimeMs: 1_400, tolerance));

        KeyboardModifierState.ResetTrackedState();
        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: true, messageTimeMs: 2_060);
        TestAssert.False(KeyboardModifierState.WasCtrlDownAt(messageTimeMs: 2_000, tolerance));

        KeyboardModifierState.ResetTrackedState();
        KeyboardModifierState.NoteKeyState(NativeMethods.VkLControl, isDown: true, messageTimeMs: 2_260);
        TestAssert.False(KeyboardModifierState.WasCtrlDownAt(messageTimeMs: 2_000, tolerance));

        KeyboardModifierState.ResetTrackedState();
    }

    private static void IgnoresModifierOnlyActivity()
    {
        TestAssert.False(KeyboardHookService.ShouldRaiseUserActivity(NativeMethods.VkLControl, isDown: true));
        TestAssert.False(KeyboardHookService.ShouldRaiseUserActivity(NativeMethods.VkRMenu, isDown: true));
        TestAssert.False(KeyboardHookService.ShouldRaiseUserActivity((uint)'A', isDown: false));
        TestAssert.True(KeyboardHookService.ShouldRaiseUserActivity((uint)'A', isDown: true));
    }
}
