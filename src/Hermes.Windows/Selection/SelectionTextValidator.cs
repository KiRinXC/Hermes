using System.Text.RegularExpressions;
using Hermes.Windows.Settings;

namespace Hermes.Windows.Selection;

public static partial class SelectionTextValidator
{
    public static SelectionValidationResult Validate(string? text, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SelectionValidationResult.Invalid("没有找到可翻译的文本。");
        }

        var trimmed = text.Trim();
        if (trimmed.Length <= 1)
        {
            return SelectionValidationResult.Invalid("选中文本太短。");
        }

        if (!EnglishLetterPattern().IsMatch(trimmed))
        {
            return SelectionValidationResult.Invalid("选中文本不包含英文内容。");
        }

        var hardMax = Math.Max(settings.Translation.MaxCharacters * 4, 20000);
        if (trimmed.Length > hardMax)
        {
            return SelectionValidationResult.Invalid("文本过长，请缩短后再翻译。");
        }

        return SelectionValidationResult.Valid(trimmed.Length > settings.Translation.MaxCharacters);
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex EnglishLetterPattern();
}
