using System.Drawing;
using System.Windows.Forms;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Selection;

namespace Hermes.Windows.Overlay;

public sealed class OverlayPositionService
{
    public (double Left, double Top) PositionNearSelection(
        ScreenBounds? bounds,
        double width,
        double height,
        int fallbackX,
        int fallbackY)
    {
        if (bounds is { IsEmpty: false })
        {
            return Clamp(bounds.Value.Right - width, bounds.Value.Top - height - 8, width, height, PointFromBounds(bounds.Value));
        }

        return PositionNearPoint(fallbackX, fallbackY, width, height);
    }

    public (double Left, double Top) PositionNearPoint(int x, int y, double width, double height)
    {
        return Clamp(x + 16, y + 18, width, height, new Point(x, y));
    }

    public (double Left, double Top) PositionAtCursor(double width, double height)
    {
        return NativeMethods.GetCursorPos(out var point)
            ? PositionNearPoint(point.X, point.Y, width, height)
            : PositionNearPoint(40, 40, width, height);
    }

    public (double Left, double Top) Clamp(double left, double top, double width, double height, Point monitorPoint)
    {
        var area = Screen.FromPoint(monitorPoint).WorkingArea;
        var clampedLeft = Math.Min(Math.Max(left, area.Left + 8), area.Right - width - 8);
        var clampedTop = Math.Min(Math.Max(top, area.Top + 8), area.Bottom - height - 8);
        return (clampedLeft, clampedTop);
    }

    private static Point PointFromBounds(ScreenBounds bounds)
    {
        return new Point((int)Math.Round(bounds.Left), (int)Math.Round(bounds.Top));
    }
}
