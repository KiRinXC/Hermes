namespace Hermes.Windows.Translation;

public static class TranslationPromptBuilder
{
    public const string DefaultSystemPrompt = """
你是专业英文到简体中文翻译引擎。请将用户提供的英文内容翻译成自然、准确、流畅的简体中文。

要求：
1. 保留原文含义，不添加原文没有的信息。
2. 保留代码、命令、URL、变量名、品牌名、专有名词。
3. 技术术语要准确，必要时使用中文术语并保留英文原词。
4. 保留原文段落结构。
5. 不要解释，不要总结，不要输出多余前后缀，只输出译文。
""";

    public static string BuildInstructions(
        string style,
        string targetLanguage,
        bool preserveFormatting,
        string? customPrompt = null)
    {
        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            return customPrompt.Trim();
        }

        var styleInstruction = style.ToLowerInvariant() switch
        {
            "literal" => "偏直译，忠实反映原文结构。",
            "professional" => "使用专业、准确、适合技术和商务语境的表达。",
            "concise" => "表达简洁，但不遗漏关键含义。",
            _ => "使用自然、准确、流畅的表达。"
        };

        var formatInstruction = preserveFormatting ? "保留段落结构。" : "可整理格式。";
        return $"英译{targetLanguage}。{styleInstruction} 保留代码、命令、URL、变量名、品牌名和专有名词；技术术语准确。{formatInstruction} 只输出译文，不解释。";
    }

    public static string BuildInput(string sourceText)
    {
        return $"Translate the following text into Simplified Chinese:{Environment.NewLine}{Environment.NewLine}{sourceText}";
    }
}
