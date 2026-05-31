using Hermes.Windows.Overlay;

namespace Hermes.Tests.Overlay;

public static class PopupMarkdownRendererTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("popup markdown renderer covers common block and inline syntax", CoversCommonBlockAndInlineSyntax);
        suite.Add("popup markdown renderer preserves paragraph spacing", PreservesParagraphSpacing);
        suite.Add("popup markdown renderer keeps unclosed markers as plain text", KeepsUnclosedMarkersAsPlainText);
        suite.Add("popup markdown renderer supports fenced code block", SupportsFencedCodeBlock);
    }

    private static void CoversCommonBlockAndInlineSyntax()
    {
        const string markdown = """
## 标题
- 列表项
1. 有序项
> 引用内容
行内 `code` + **bold** + *italic* + [OpenAI](https://openai.com)
""";

        var segments = PopupMarkdownRenderer.BuildSegments(markdown);

        TestAssert.True(segments.Any(segment => segment.Text == "标题" && segment.Strong && segment.FontSizeOffset >= 1));
        TestAssert.True(segments.Any(segment => segment.Text == "• "));
        TestAssert.True(segments.Any(segment => segment.Text == "1. "));
        TestAssert.True(segments.Any(segment => segment.Text == "│ " && segment.Muted));
        TestAssert.True(segments.Any(segment => segment.Text == "code" && segment.Code));
        TestAssert.True(segments.Any(segment => segment.Text == "bold" && segment.Strong));
        TestAssert.True(segments.Any(segment => segment.Text == "italic" && segment.Italic));
        TestAssert.True(segments.Any(segment => segment.Text == "OpenAI" && segment.Link));
        TestAssert.True(segments.Any(segment => segment.Text == " (https://openai.com)" && segment.Muted));
    }

    private static void PreservesParagraphSpacing()
    {
        var segments = PopupMarkdownRenderer.BuildSegments("第一段\n\n第二段");
        var lineBreaks = segments.Count(segment => segment.IsLineBreak);
        TestAssert.True(lineBreaks >= 2);
    }

    private static void KeepsUnclosedMarkersAsPlainText()
    {
        var segments = PopupMarkdownRenderer.BuildSegments("name_with_underscore and `partial");
        var combined = CombineSegments(segments);
        TestAssert.Contains("name_with_underscore", combined);
        TestAssert.Contains("`partial", combined);
    }

    private static void SupportsFencedCodeBlock()
    {
        const string markdown = """
```csharp
var value = 42;
Console.WriteLine(value);
```
""";

        var segments = PopupMarkdownRenderer.BuildSegments(markdown);
        TestAssert.True(segments.Any(segment => segment.Text == "var value = 42;" && segment.Code));
        TestAssert.True(segments.Any(segment => segment.Text == "Console.WriteLine(value);" && segment.Code));
    }

    private static string CombineSegments(IEnumerable<PopupMarkdownSegment> segments)
    {
        return string.Concat(segments.Select(segment => segment.IsLineBreak ? "\n" : segment.Text));
    }
}
