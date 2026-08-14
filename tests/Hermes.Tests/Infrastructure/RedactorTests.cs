using Hermes.Windows.Infrastructure;

namespace Hermes.Tests.Infrastructure;

public static class RedactorTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("redacts API key", RedactsApiKey);
        suite.Add("redacts Codex auth tokens", RedactsCodexAuthTokens);
    }

    private static void RedactsApiKey()
    {
        var redacted = Redactor.RedactSecrets("api_key=sk-test-secret");
        TestAssert.False(redacted.Contains("sk-test-secret", StringComparison.Ordinal));
    }

    private static void RedactsCodexAuthTokens()
    {
        var result = Redactor.RedactSecrets(
            "OPENAI_API_KEY=sk-abcdefghijklmnop access_token=oauth-secret-token");
        TestAssert.False(result.Contains("sk-abcdefghijklmnop", StringComparison.Ordinal));
        TestAssert.False(result.Contains("oauth-secret-token", StringComparison.Ordinal));
    }
}
