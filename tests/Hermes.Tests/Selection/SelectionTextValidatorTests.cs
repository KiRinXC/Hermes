using Hermes.Windows.Selection;
using Hermes.Windows.Settings;

namespace Hermes.Tests.Selection;

public static class SelectionTextValidatorTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("validates English text", ValidatesEnglishText);
        suite.Add("rejects non-English text", RejectsNonEnglishText);
    }

    private static void ValidatesEnglishText()
    {
        var result = SelectionTextValidator.Validate("Hello world", new AppSettings());
        TestAssert.True(result.IsValid);
    }

    private static void RejectsNonEnglishText()
    {
        var result = SelectionTextValidator.Validate("你好世界", new AppSettings());
        TestAssert.False(result.IsValid);
    }
}
