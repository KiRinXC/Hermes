using System.Drawing;
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
        if (bounds is { IsEmpty: false } && IsNearFallbackPoint(bounds.Value, fallbackX, fallbackY))
        {
            var monitorPoint = PointFromBounds(bounds.Value);
            var size = DpiAwareScreen.ToPhysicalSize(width, height, monitorPoint);
            var scale = DpiAwareScreen.GetScaleForPhysicalPoint(monitorPoint);
            return Clamp(bounds.Value.Right - size.Width, bounds.Value.Top - size.Height - (8 * scale.ScaleY), width, height, monitorPoint);
        }

        return PositionNearPoint(fallbackX, fallbackY, width, height);
    }

    public (double Left, double Top) PositionNearPoint(int x, int y, double width, double height)
    {
        var monitorPoint = new Point(x, y);
        var scale = DpiAwareScreen.GetScaleForPhysicalPoint(monitorPoint);
        return Clamp(x + (16 * scale.ScaleX), y + (18 * scale.ScaleY), width, height, monitorPoint);
    }

    public (double Left, double Top) PositionAtCursor(double width, double height)
    {
        return NativeMethods.GetCursorPos(out var point)
            ? PositionNearPoint(point.X, point.Y, width, height)
            : PositionNearPoint(40, 40, width, height);
    }

    public (double Left, double Top) Clamp(double left, double top, double width, double height, Point monitorPoint)
    {
        return DpiAwareScreen.ClampPhysical(left, top, width, height, monitorPoint);
    }

    private static Point PointFromBounds(ScreenBounds bounds)
    {
        return new Point((int)Math.Round(bounds.Left), (int)Math.Round(bounds.Top));
    }

    private static bool IsNearFallbackPoint(ScreenBounds bounds, int fallbackX, int fallbackY)
    {
        var padding = Math.Max(240, Math.Max(bounds.Width, bounds.Height) * 0.75);
        return fallbackX >= bounds.Left - padding
            && fallbackX <= bounds.Right + padding
            && fallbackY >= bounds.Top - padding
            && fallbackY <= bounds.Bottom + padding;
    }
}
