using System.IO;
using Hermes.Windows.Translation;

namespace Hermes.Tests.Translation;

public static class TransmartTranslationServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("builds transmart imt URI", BuildsTransmartImtUri);
        suite.Add("builds transmart imt URI from default when empty", BuildsTransmartImtUriFromDefaultWhenEmpty);
        suite.Add("extracts transmart translation output", ExtractsTransmartTranslationOutput);
        suite.Add("transmart service uses configured endpoint and model with defaults", ServiceUsesConfiguredEndpointAndModelWithDefaults);
        suite.Add("transmart explain mode is rejected", ExplainModeIsRejected);
    }

    private static void BuildsTransmartImtUri()
    {
        var uri = TransmartTranslationService.BuildImtUri("https://transmart.qq.com/api");
        TestAssert.Equal("https://transmart.qq.com/api/imt", uri.ToString());
    }

    private static void BuildsTransmartImtUriFromDefaultWhenEmpty()
    {
        var uri = TransmartTranslationService.BuildImtUri(string.Empty);
        TestAssert.Equal("https://transmart.qq.com/api/imt", uri.ToString());
    }

    private static void ExtractsTransmartTranslationOutput()
    {
        var text = TransmartTranslationService.ExtractOutputText(
            """{"header":{"ret_code":"succ"},"auto_translation":["hello ","world"]}""");
        TestAssert.Equal("hello world", text);
    }

    private static void ServiceUsesConfiguredEndpointAndModelWithDefaults()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TransmartTranslationService.cs"));
        TestAssert.True(code.Contains("DefaultBaseUrl = \"https://transmart.qq.com/api\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("DefaultModelCategory = \"normal\"", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Api.Transmart.BaseUrl", StringComparison.Ordinal));
        TestAssert.True(code.Contains("settings.Api.Transmart.Model", StringComparison.Ordinal));
        TestAssert.True(code.Contains("BuildImtUri(baseUrl)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("CreatePayload(request, model, settings.Translation.SourceLanguage)", StringComparison.Ordinal));
    }

    private static void ExplainModeIsRejected()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/TransmartTranslationService.cs"));
        TestAssert.True(code.Contains("if (request.Mode == TranslationMode.Explain)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("TranslationErrorKind.InvalidRequest", StringComparison.Ordinal));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}
