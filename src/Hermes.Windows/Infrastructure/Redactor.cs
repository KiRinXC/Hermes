using System.Text.RegularExpressions;

namespace Hermes.Windows.Infrastructure;

public static partial class Redactor
{
    public static string RedactSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
        {
            return "********";
        }

        return $"{trimmed[..4]}...{trimmed[^4..]}";
    }

    public static string RedactSecrets(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return ApiKeyPattern().Replace(value, "$1[redacted]");
    }

    public static string SummarizeText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var compact = WhitespacePattern().Replace(text.Trim(), " ");
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "...";
    }

    [GeneratedRegex(@"(?i)(api[_-]?key\s*[:=]\s*)([^\s,;]+)")]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
