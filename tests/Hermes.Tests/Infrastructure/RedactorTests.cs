using Hermes.Windows.Infrastructure;

namespace Hermes.Tests.Infrastructure;

public static class RedactorTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("redacts API key", RedactsApiKey);
    }

    private static void RedactsApiKey()
    {
        var redacted = Redactor.RedactSecrets("api_key=sk-test-secret");
        TestAssert.False(redacted.Contains("sk-test-secret", StringComparison.Ordinal));
    }
}
