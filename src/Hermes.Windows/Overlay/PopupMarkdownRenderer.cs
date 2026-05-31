using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;

namespace Hermes.Windows.Overlay;

public sealed record PopupMarkdownSegment(
    string Text,
    bool IsLineBreak = false,
    bool Strong = false,
    bool Italic = false,
    bool Code = false,
    bool Link = false,
    bool Muted = false,
    int FontSizeOffset = 0);

public static class PopupMarkdownRenderer
{
    private static readonly Regex MarkdownHeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex MarkdownOrderedListRegex = new(@"^(\s*)(\d+)[\.\)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex MarkdownUnorderedListRegex = new(@"^(\s*)[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex MarkdownBlockQuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex MarkdownHorizontalRuleRegex = new(@"^\s{0,3}([-*_]\s*){3,}$", RegexOptions.Compiled);

    public static void Render(
        TextBlock target,
        string markdown,
        Func<string, WpfBrush> brushResolver)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(brushResolver);

        target.Inlines.Clear();
        var segments = BuildSegments(markdown);
        foreach (var segment in segments)
        {
            if (segment.IsLineBreak)
            {
                target.Inlines.Add(new LineBreak());
                continue;
            }

            if (segment.Text.Length == 0)
            {
                continue;
            }

            var run = new Run(segment.Text);
            if (segment.Strong)
            {
                run.FontWeight = FontWeights.Medium;
            }

            if (segment.Italic)
            {
                run.FontStyle = FontStyles.Italic;
            }

            if (segment.FontSizeOffset > 0)
            {
                run.FontSize = target.FontSize + segment.FontSizeOffset;
            }

            if (segment.Code)
            {
                run.FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI");
                run.Background = brushResolver("Brush.SourcePanel");
            }

            if (segment.Link)
            {
                run.TextDecorations = TextDecorations.Underline;
                run.Foreground = brushResolver("Brush.Accent");
            }

            if (segment.Muted)
            {
                run.Foreground = brushResolver("Brush.TextMuted");
            }

            target.Inlines.Add(run);
        }
    }

    public static IReadOnlyList<PopupMarkdownSegment> BuildSegments(string markdown)
    {
        var segments = new List<PopupMarkdownSegment>();
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var lineIndex = 0;
        var wroteAnyBlock = false;
        var pendingBlankLine = false;

        while (lineIndex < lines.Length)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (wroteAnyBlock)
                {
                    pendingBlankLine = true;
                }

                lineIndex++;
                continue;
            }

            if (wroteAnyBlock)
            {
                AppendLineBreak(segments);
                if (pendingBlankLine)
                {
                    AppendLineBreak(segments);
                }
            }

            pendingBlankLine = false;
            if (IsMarkdownFenceLine(line))
            {
                var fencedCode = CollectFencedCodeBlock(lines, ref lineIndex);
                AppendMarkdownCodeBlock(segments, fencedCode);
                wroteAnyBlock = true;
                continue;
            }

            AppendMarkdownBlockLine(segments, line);
            wroteAnyBlock = true;
            lineIndex++;
        }

        return segments;
    }

    private static void AppendMarkdownBlockLine(List<PopupMarkdownSegment> segments, string line)
    {
        var headingMatch = MarkdownHeadingRegex.Match(line);
        if (headingMatch.Success)
        {
            var level = headingMatch.Groups[1].Value.Length;
            var headingText = headingMatch.Groups[2].Value;
            AppendMarkdownInline(segments, headingText, strong: true, italic: false, fontSizeOffset: Math.Max(0, 3 - level));
            return;
        }

        var quoteMatch = MarkdownBlockQuoteRegex.Match(line);
        if (quoteMatch.Success)
        {
            segments.Add(new PopupMarkdownSegment("│ ", Muted: true));
            AppendMarkdownInline(segments, quoteMatch.Groups[1].Value);
            return;
        }

        if (MarkdownHorizontalRuleRegex.IsMatch(line))
        {
            segments.Add(new PopupMarkdownSegment("──────────────", Muted: true));
            return;
        }

        var orderedMatch = MarkdownOrderedListRegex.Match(line);
        if (orderedMatch.Success)
        {
            AppendListIndentation(segments, orderedMatch.Groups[1].Value.Length);
            segments.Add(new PopupMarkdownSegment($"{orderedMatch.Groups[2].Value}. "));
            AppendMarkdownInline(segments, orderedMatch.Groups[3].Value);
            return;
        }

        var unorderedMatch = MarkdownUnorderedListRegex.Match(line);
        if (unorderedMatch.Success)
        {
            AppendListIndentation(segments, unorderedMatch.Groups[1].Value.Length);
            segments.Add(new PopupMarkdownSegment("• "));
            AppendMarkdownInline(segments, unorderedMatch.Groups[2].Value);
            return;
        }

        AppendMarkdownInline(segments, line);
    }

    private static void AppendListIndentation(List<PopupMarkdownSegment> segments, int leadingWhitespaceLength)
    {
        var level = Math.Max(0, leadingWhitespaceLength / 2);
        if (level == 0)
        {
            return;
        }

        segments.Add(new PopupMarkdownSegment(new string(' ', level * 2), Muted: true));
    }

    private static void AppendMarkdownInline(
        List<PopupMarkdownSegment> segments,
        string text,
        bool strong = false,
        bool italic = false,
        int fontSizeOffset = 0)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            if (TryAppendEscapedMarker(segments, text, ref cursor, strong, italic, fontSizeOffset))
            {
                continue;
            }

            if (TryAppendMarkdownCodeSpan(segments, text, ref cursor, strong, italic, fontSizeOffset))
            {
                continue;
            }

            if (TryAppendMarkdownLink(segments, text, ref cursor, strong, italic, fontSizeOffset))
            {
                continue;
            }

            if (TryAppendMarkdownStrong(segments, text, ref cursor, strong, italic, fontSizeOffset))
            {
                continue;
            }

            if (TryAppendMarkdownItalic(segments, text, ref cursor, strong, italic, fontSizeOffset))
            {
                continue;
            }

            var nextMarkerIndex = FindNextMarkdownMarker(text, cursor);
            if (nextMarkerIndex == cursor)
            {
                AppendStyledText(
                    segments,
                    text[cursor].ToString(),
                    strong,
                    italic,
                    codeSpan: false,
                    linkText: false,
                    muted: false,
                    fontSizeOffset);
                cursor++;
                continue;
            }

            AppendStyledText(segments, text[cursor..nextMarkerIndex], strong, italic, codeSpan: false, linkText: false, muted: false, fontSizeOffset);
            cursor = nextMarkerIndex;
        }
    }

    private static bool IsMarkdownFenceLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static string CollectFencedCodeBlock(string[] lines, ref int lineIndex)
    {
        lineIndex++;
        var codeBuilder = new StringBuilder();
        while (lineIndex < lines.Length && !IsMarkdownFenceLine(lines[lineIndex]))
        {
            if (codeBuilder.Length > 0)
            {
                codeBuilder.Append('\n');
            }

            codeBuilder.Append(lines[lineIndex]);
            lineIndex++;
        }

        if (lineIndex < lines.Length && IsMarkdownFenceLine(lines[lineIndex]))
        {
            lineIndex++;
        }

        return codeBuilder.ToString();
    }

    private static void AppendMarkdownCodeBlock(List<PopupMarkdownSegment> segments, string code)
    {
        if (code.Length == 0)
        {
            return;
        }

        var codeLines = code.Split('\n');
        for (var i = 0; i < codeLines.Length; i++)
        {
            AppendStyledText(
                segments,
                codeLines[i],
                strong: false,
                italic: false,
                codeSpan: true,
                linkText: false,
                muted: false,
                fontSizeOffset: 0);

            if (i < codeLines.Length - 1)
            {
                AppendLineBreak(segments);
            }
        }
    }

    private static bool TryAppendEscapedMarker(
        List<PopupMarkdownSegment> segments,
        string text,
        ref int cursor,
        bool strong,
        bool italic,
        int fontSizeOffset)
    {
        if (text[cursor] != '\\' || cursor + 1 >= text.Length || !IsEscapableMarker(text[cursor + 1]))
        {
            return false;
        }

        AppendStyledText(
            segments,
            text[cursor + 1].ToString(),
            strong,
            italic,
            codeSpan: false,
            linkText: false,
            muted: false,
            fontSizeOffset);
        cursor += 2;
        return true;
    }

    private static bool IsEscapableMarker(char value)
    {
        return value is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '-' or '!' or '.';
    }

    private static bool TryAppendMarkdownCodeSpan(
        List<PopupMarkdownSegment> segments,
        string text,
        ref int cursor,
        bool strong,
        bool italic,
        int fontSizeOffset)
    {
        if (text[cursor] != '`')
        {
            return false;
        }

        var closing = text.IndexOf('`', cursor + 1);
        if (closing <= cursor + 1)
        {
            return false;
        }

        AppendStyledText(
            segments,
            text[(cursor + 1)..closing],
            strong,
            italic,
            codeSpan: true,
            linkText: false,
            muted: false,
            fontSizeOffset);
        cursor = closing + 1;
        return true;
    }

    private static bool TryAppendMarkdownLink(
        List<PopupMarkdownSegment> segments,
        string text,
        ref int cursor,
        bool strong,
        bool italic,
        int fontSizeOffset)
    {
        if (text[cursor] != '[')
        {
            return false;
        }

        var closeText = text.IndexOf(']', cursor + 1);
        if (closeText <= cursor + 1 || closeText + 1 >= text.Length || text[closeText + 1] != '(')
        {
            return false;
        }

        var closeUrl = text.IndexOf(')', closeText + 2);
        if (closeUrl <= closeText + 2)
        {
            return false;
        }

        var label = text[(cursor + 1)..closeText];
        var url = text[(closeText + 2)..closeUrl];
        AppendStyledText(
            segments,
            label,
            strong,
            italic,
            codeSpan: false,
            linkText: true,
            muted: false,
            fontSizeOffset);
        AppendStyledText(
            segments,
            $" ({url})",
            strong: false,
            italic: false,
            codeSpan: false,
            linkText: false,
            muted: true,
            fontSizeOffset: 0);
        cursor = closeUrl + 1;
        return true;
    }

    private static bool TryAppendMarkdownStrong(
        List<PopupMarkdownSegment> segments,
        string text,
        ref int cursor,
        bool strong,
        bool italic,
        int fontSizeOffset)
    {
        if (!text.AsSpan(cursor).StartsWith("**", StringComparison.Ordinal)
            && !text.AsSpan(cursor).StartsWith("__", StringComparison.Ordinal))
        {
            return false;
        }

        var marker = text[cursor..(cursor + 2)];
        if (marker == "__" && !IsUnderscoreEmphasisBoundary(text, cursor, marker.Length))
        {
            return false;
        }

        var closing = FindClosingMarker(text, marker, cursor + 2);
        if (closing <= cursor + 2)
        {
            return false;
        }

        if (marker == "__" && !IsUnderscoreEmphasisBoundary(text, closing, marker.Length))
        {
            return false;
        }

        AppendMarkdownInline(segments, text[(cursor + 2)..closing], strong: true, italic, fontSizeOffset);
        cursor = closing + 2;
        return true;
    }

    private static bool TryAppendMarkdownItalic(
        List<PopupMarkdownSegment> segments,
        string text,
        ref int cursor,
        bool strong,
        bool italic,
        int fontSizeOffset)
    {
        var marker = text[cursor];
        if (marker is not ('*' or '_'))
        {
            return false;
        }

        if (cursor + 1 < text.Length && text[cursor + 1] == marker)
        {
            return false;
        }

        if (marker == '_' && !IsUnderscoreEmphasisBoundary(text, cursor, 1))
        {
            return false;
        }

        var closing = FindClosingMarker(text, marker.ToString(), cursor + 1);
        if (closing <= cursor + 1)
        {
            return false;
        }

        if (marker == '_' && !IsUnderscoreEmphasisBoundary(text, closing, 1))
        {
            return false;
        }

        AppendMarkdownInline(segments, text[(cursor + 1)..closing], strong, italic: true, fontSizeOffset);
        cursor = closing + 1;
        return true;
    }

    private static bool IsUnderscoreEmphasisBoundary(string text, int markerIndex, int markerLength)
    {
        var before = markerIndex > 0 ? text[markerIndex - 1] : '\0';
        var afterIndex = markerIndex + markerLength;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsWordLike(before) || !IsWordLike(after);
    }

    private static bool IsWordLike(char value)
    {
        return char.IsLetterOrDigit(value);
    }

    private static int FindClosingMarker(string text, string marker, int startIndex)
    {
        var closing = text.IndexOf(marker, startIndex, StringComparison.Ordinal);
        while (closing >= 0)
        {
            if (marker != "_" || IsUnderscoreEmphasisBoundary(text, closing, 1))
            {
                return closing;
            }

            closing = text.IndexOf(marker, closing + marker.Length, StringComparison.Ordinal);
        }

        return -1;
    }

    private static int FindNextMarkdownMarker(string text, int cursor)
    {
        var next = text.Length;
        next = Math.Min(next, IndexOrLength(text, '\\', cursor));
        next = Math.Min(next, IndexOrLength(text, '`', cursor));
        next = Math.Min(next, IndexOrLength(text, '[', cursor));
        next = Math.Min(next, IndexOrLength(text, '*', cursor));
        next = Math.Min(next, IndexOrLength(text, '_', cursor));
        return next;
    }

    private static int IndexOrLength(string text, char marker, int cursor)
    {
        var index = text.IndexOf(marker, cursor);
        return index < 0 ? text.Length : index;
    }

    private static void AppendLineBreak(List<PopupMarkdownSegment> segments)
    {
        segments.Add(new PopupMarkdownSegment(string.Empty, IsLineBreak: true));
    }

    private static void AppendStyledText(
        List<PopupMarkdownSegment> segments,
        string text,
        bool strong,
        bool italic,
        bool codeSpan,
        bool linkText,
        bool muted,
        int fontSizeOffset)
    {
        if (text.Length == 0)
        {
            return;
        }

        segments.Add(new PopupMarkdownSegment(
            text,
            Strong: strong,
            Italic: italic,
            Code: codeSpan,
            Link: linkText,
            Muted: muted,
            FontSizeOffset: fontSizeOffset));
    }
}
