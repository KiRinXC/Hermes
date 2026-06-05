using System.Windows;
using Hermes.Windows.Infrastructure;
using DrawingPoint = System.Drawing.Point;
using Forms = System.Windows.Forms;

namespace Hermes.Windows.Overlay;

internal static class DpiAwareScreen
{
    private const uint MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;
    private const double DefaultDpi = 96;

    public static Rect GetWorkingAreaDip(DrawingPoint physicalMonitorPoint)
    {
        var area = Forms.Screen.FromPoint(physicalMonitorPoint).WorkingArea;
        var scale = GetScaleForPhysicalPoint(physicalMonitorPoint);
        var left = area.Left / scale.ScaleX;
        var top = area.Top / scale.ScaleY;
        var right = area.Right / scale.ScaleX;
        var bottom = area.Bottom / scale.ScaleY;
        return new Rect(left, top, right - left, bottom - top);
    }

    public static Rect GetWorkingAreaDip(Window window)
    {
        var rect = GetWindowRect(window);
        return GetWorkingAreaDip(new DrawingPoint(rect.Left, rect.Top));
    }

    public static (double Width, double Height) ToPhysicalSize(double widthDip, double heightDip, DrawingPoint physicalMonitorPoint)
    {
        var scale = GetScaleForPhysicalPoint(physicalMonitorPoint);
        return (widthDip * scale.ScaleX, heightDip * scale.ScaleY);
    }

    public static (double Left, double Top) ClampPhysical(
        double left,
        double top,
        double widthDip,
        double heightDip,
        DrawingPoint monitorPoint,
        double marginDip = 8)
    {
        var area = Forms.Screen.FromPoint(monitorPoint).WorkingArea;
        var scale = GetScaleForPhysicalPoint(monitorPoint);
        var width = widthDip * scale.ScaleX;
        var height = heightDip * scale.ScaleY;
        var marginX = marginDip * scale.ScaleX;
        var marginY = marginDip * scale.ScaleY;
        var clampedLeft = Math.Min(Math.Max(left, area.Left + marginX), area.Right - width - marginX);
        var clampedTop = Math.Min(Math.Max(top, area.Top + marginY), area.Bottom - height - marginY);
        return (clampedLeft, clampedTop);
    }

    public static void SetWindowPositionPhysical(Window window, double left, double top)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(left),
            (int)Math.Round(top),
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    public static void ClampWindowToScreen(Window window, double marginDip = 8)
    {
        var rect = GetWindowRect(window);
        var monitorPoint = new DrawingPoint(rect.Left, rect.Top);
        var area = Forms.Screen.FromPoint(monitorPoint).WorkingArea;
        var scale = GetScaleForPhysicalPoint(monitorPoint);
        var marginX = marginDip * scale.ScaleX;
        var marginY = marginDip * scale.ScaleY;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var left = Math.Min(Math.Max(rect.Left, area.Left + marginX), area.Right - width - marginX);
        var top = Math.Min(Math.Max(rect.Top, area.Top + marginY), area.Bottom - height - marginY);
        SetWindowPositionPhysical(window, left, top);
    }

    public static bool ContainsPhysicalPoint(Window window, int x, int y)
    {
        var rect = GetWindowRect(window);
        return x >= rect.Left
            && x <= rect.Right
            && y >= rect.Top
            && y <= rect.Bottom;
    }

    private static NativeMethods.RECT GetWindowRect(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var rect)
            ? rect
            : default;
    }

    public static (double ScaleX, double ScaleY) GetScaleForPhysicalPoint(DrawingPoint point)
    {
        var monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT { X = point.X, Y = point.Y },
            MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return (1, 1);
        }

        var result = NativeMethods.GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out var dpiY);
        if (result != 0 || dpiX == 0 || dpiY == 0)
        {
            return (1, 1);
        }

        return (dpiX / DefaultDpi, dpiY / DefaultDpi);
    }
}
