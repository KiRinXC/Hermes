using Hermes.Windows.Translation;

namespace Hermes.Tests.Translation;

public static class TranslationCoordinatorTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("translation coordinator ignores passive mouse drags without ctrl", IgnoresPassiveMouseDragsWithoutCtrl);
    }

    private static void IgnoresPassiveMouseDragsWithoutCtrl()
    {
        TestAssert.True(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: false,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: false));
        TestAssert.True(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: false,
            ctrlDownAtRelease: true));
        TestAssert.False(TranslationCoordinator.ShouldIgnorePassiveMouseGesture(
            ctrlDownAtStart: true,
            ctrlHeldDuringDrag: true,
            ctrlDownAtRelease: true));
    }
}
