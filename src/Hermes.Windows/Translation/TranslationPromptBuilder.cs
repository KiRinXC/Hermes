namespace Hermes.Windows.Translation;

public static class TranslationPromptBuilder
{
    public static string BuildInstructions(string style, string targetLanguage, bool preserveFormatting)
    {
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
