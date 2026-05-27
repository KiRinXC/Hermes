using System.Windows.Automation;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Selection;

public sealed class UiAutomationSelectionProvider : ISelectionProvider
{
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly AppLogger _logger;

    public UiAutomationSelectionProvider(ForegroundWindowService foregroundWindowService, AppLogger logger)
    {
        _foregroundWindowService = foregroundWindowService;
        _logger = logger;
    }

    public Task<SelectionResult> TryGetSelectionAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        return Task.Run(() => TryGetSelection(foreground, cancellationToken));
    }

    private SelectionResult TryGetSelection(ForegroundWindowInfo? foreground, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SelectionResult.Empty("读取选区已取消。", foreground);
            }

            var focused = AutomationElement.FocusedElement;
            if (focused is null || focused.Current.IsPassword)
            {
                return SelectionResult.Empty("当前控件不支持读取选区。", foreground);
            }

            if (!focused.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject)
                || patternObject is not TextPattern textPattern)
            {
                return SelectionResult.Empty("当前控件未暴露文本选区。", foreground);
            }

            var ranges = textPattern.GetSelection();
            foreach (var range in ranges)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return SelectionResult.Empty("读取选区已取消。", foreground);
                }

                var text = range.GetText(-1)?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                return SelectionResult.FromText(
                    text,
                    SelectionProviderKind.UiAutomation,
                    TryGetBounds(range),
                    foreground);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"UI Automation selection read failed. {ex.Message}");
        }

        return SelectionResult.Empty("无法通过 UI Automation 读取选区。", foreground);
    }

    private static ScreenBounds? TryGetBounds(dynamic range)
    {
        try
        {
            var rectangles = range.GetBoundingRectangles();
            if (rectangles.Length < 4)
            {
                return null;
            }

            var left = rectangles[0];
            var top = rectangles[1];
            var right = rectangles[0] + rectangles[2];
            var bottom = rectangles[1] + rectangles[3];

            for (var i = 4; i + 3 < rectangles.Length; i += 4)
            {
                left = Math.Min(left, rectangles[i]);
                top = Math.Min(top, rectangles[i + 1]);
                right = Math.Max(right, rectangles[i] + rectangles[i + 2]);
                bottom = Math.Max(bottom, rectangles[i + 1] + rectangles[i + 3]);
            }

            var bounds = new ScreenBounds(left, top, right - left, bottom - top);
            return bounds.IsEmpty ? null : bounds;
        }
        catch
        {
            return null;
        }
    }
}
