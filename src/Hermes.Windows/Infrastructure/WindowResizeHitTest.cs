using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace Hermes.Windows.Infrastructure;

internal static class WindowResizeHitTest
{
    public const double ResizeBorderThickness = 8;

    public static IntPtr HitTest(Window window, IntPtr lParam, double borderThickness = ResizeBorderThickness)
    {
        if (window.ActualWidth <= 0 || window.ActualHeight <= 0)
        {
            return new IntPtr(NativeMethods.HtClient);
        }

        var screenPoint = GetScreenPoint(lParam);
        var dipPoint = ToDeviceIndependentPoint(window, screenPoint);
        var x = dipPoint.X - window.Left;
        var y = dipPoint.Y - window.Top;
        var onLeft = x >= 0 && x <= borderThickness;
        var onRight = x <= window.ActualWidth && x >= window.ActualWidth - borderThickness;
        var onTop = y >= 0 && y <= borderThickness;
        var onBottom = y <= window.ActualHeight && y >= window.ActualHeight - borderThickness;

        if (onTop && onLeft)
        {
            return new IntPtr(NativeMethods.HtTopLeft);
        }

        if (onTop && onRight)
        {
            return new IntPtr(NativeMethods.HtTopRight);
        }

        if (onBottom && onLeft)
        {
            return new IntPtr(NativeMethods.HtBottomLeft);
        }

        if (onBottom && onRight)
        {
            return new IntPtr(NativeMethods.HtBottomRight);
        }

        if (onLeft)
        {
            return new IntPtr(NativeMethods.HtLeft);
        }

        if (onRight)
        {
            return new IntPtr(NativeMethods.HtRight);
        }

        if (onTop)
        {
            return new IntPtr(NativeMethods.HtTop);
        }

        if (onBottom)
        {
            return new IntPtr(NativeMethods.HtBottom);
        }

        return new IntPtr(NativeMethods.HtClient);
    }

    private static WpfPoint GetScreenPoint(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        var x = unchecked((short)(value & 0xFFFF));
        var y = unchecked((short)((value >> 16) & 0xFFFF));
        return new WpfPoint(x, y);
    }

    private static WpfPoint ToDeviceIndependentPoint(Window window, WpfPoint screenPoint)
    {
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return transform.Transform(screenPoint);
    }
}
