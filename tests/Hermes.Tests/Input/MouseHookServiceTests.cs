using Hermes.Windows.Input;

namespace Hermes.Tests.Input;

public static class MouseHookServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("mouse hook tracks selection gestures only while ctrl is held", TracksSelectionGesturesOnlyWithCtrl);
    }

    private static void TracksSelectionGesturesOnlyWithCtrl()
    {
        TestAssert.False(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: false));
        TestAssert.True(MouseHookService.ShouldTrackSelectionGesture(ctrlDownAtStart: true));

        TestAssert.False(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: false));
        TestAssert.False(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: true));
        TestAssert.True(MouseHookService.ShouldEmitSelectionGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: true));
    }
}
