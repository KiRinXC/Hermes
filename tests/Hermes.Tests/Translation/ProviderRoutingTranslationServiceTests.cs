using System.IO;

namespace Hermes.Tests.Translation;

public static class ProviderRoutingTranslationServiceTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("provider routing sends explanation to openai", SendsExplanationToOpenAi);
        suite.Add("provider routing sends translation by toggle", SendsTranslationByToggle);
    }

    private static void SendsExplanationToOpenAi()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/ProviderRoutingTranslationService.cs"));

        TestAssert.True(code.Contains("if (mode == TranslationMode.Explain)", StringComparison.Ordinal));
        TestAssert.True(code.Contains("return _openAiService;", StringComparison.Ordinal));
    }

    private static void SendsTranslationByToggle()
    {
        var code = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Translation/ProviderRoutingTranslationService.cs"));

        TestAssert.True(code.Contains("_settingsService.Current.Api.UseOpenAiForTranslation", StringComparison.Ordinal));
        TestAssert.True(code.Contains("? _openAiService", StringComparison.Ordinal));
        TestAssert.True(code.Contains(": _transmartService;", StringComparison.Ordinal));
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
