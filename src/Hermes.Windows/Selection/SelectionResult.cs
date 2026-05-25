namespace Hermes.Windows.Selection;

public enum SelectionProviderKind
{
    None,
    UiAutomation,
    ClipboardFallback,
    Clipboard
}

public sealed record ForegroundWindowInfo(IntPtr Hwnd, int ProcessId, string ProcessName, string? WindowTitle);

public sealed record SelectionResult(
    bool Success,
    string? Text,
    SelectionProviderKind Provider,
    ScreenBounds? Bounds,
    ForegroundWindowInfo? ForegroundWindow,
    string? Message)
{
    public static SelectionResult Empty(string message, ForegroundWindowInfo? foreground = null)
    {
        return new SelectionResult(false, null, SelectionProviderKind.None, null, foreground, message);
    }

    public static SelectionResult FromText(
        string text,
        SelectionProviderKind provider,
        ScreenBounds? bounds,
        ForegroundWindowInfo? foreground)
    {
        return new SelectionResult(true, text, provider, bounds, foreground, null);
    }
}
