using System.Windows;
using Forms = System.Windows.Forms;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Selection;

public sealed class ClipboardSelectionProvider
{
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly AppLogger _logger;

    public ClipboardSelectionProvider(ForegroundWindowService foregroundWindowService, AppLogger logger)
    {
        _foregroundWindowService = foregroundWindowService;
        _logger = logger;
    }

    public async Task<SelectionResult> TryCopySelectionAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        var operation = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            System.Windows.IDataObject? original = null;
            try
            {
                original = System.Windows.Clipboard.GetDataObject();
                Forms.SendKeys.SendWait("^c");
                await Task.Delay(90, cancellationToken);

                var text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
                RestoreClipboard(original);

                return string.IsNullOrWhiteSpace(text)
                    ? SelectionResult.Empty("剪贴板兜底未读取到文本。", foreground)
                    : SelectionResult.FromText(text.Trim(), SelectionProviderKind.ClipboardFallback, null, foreground);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Clipboard fallback failed. {ex.Message}");
                RestoreClipboard(original);
                return SelectionResult.Empty("无法从剪贴板读取选区。", foreground);
            }
        });

        return await await operation.Task;
    }

    public async Task<SelectionResult> ReadClipboardTextAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        var operation = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty;
                return string.IsNullOrWhiteSpace(text)
                    ? SelectionResult.Empty("剪贴板中没有可翻译文本。", foreground)
                    : SelectionResult.FromText(text.Trim(), SelectionProviderKind.Clipboard, null, foreground);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Clipboard read failed. {ex.Message}");
                return SelectionResult.Empty("无法读取剪贴板。", foreground);
            }
        });

        return await operation.Task;
    }

    private void RestoreClipboard(System.Windows.IDataObject? original)
    {
        if (original is null)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetDataObject(original, copy: true);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Clipboard restore failed. {ex.Message}");
        }
    }
}
