using Hermes.Windows.Translation;

namespace Hermes.Tests.Translation;

public static class OpenAiTranslationServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("builds responses URI", BuildsResponsesUri);
        suite.Add("extracts output_text", ExtractsOutputText);
        suite.Add("extracts output array text", ExtractsOutputArrayText);
    }

    private static void BuildsResponsesUri()
    {
        var uri = OpenAiTranslationService.BuildResponsesUri("https://api.openai.com/v1");
        TestAssert.Equal("https://api.openai.com/v1/responses", uri.ToString());
    }

    private static void ExtractsOutputText()
    {
        var text = OpenAiTranslationService.ExtractOutputText("""{"output_text":"你好"}""");
        TestAssert.Equal("你好", text);
    }

    private static void ExtractsOutputArrayText()
    {
        var text = OpenAiTranslationService.ExtractOutputText(
            """{"output":[{"content":[{"type":"output_text","text":"你好"}]}]}""");
        TestAssert.Equal("你好", text);
    }
}
