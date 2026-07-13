using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Selection;

public sealed class ClipboardSelectionProvider
{
    internal static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CopyPollInterval = TimeSpan.FromMilliseconds(15);
    private const string ProbeFormat = "Hermes.ControlledCopy.Probe";

    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly AppLogger _logger;

    public ClipboardSelectionProvider(ForegroundWindowService foregroundWindowService, AppLogger logger)
    {
        _foregroundWindowService = foregroundWindowService;
        _logger = logger;
    }

    public async Task<SelectionResult> TryCopySelectionAsync(
        ForegroundWindowInfo? expectedForeground,
        CancellationToken cancellationToken = default)
    {
        return await RunOnStaThreadAsync(() =>
        {
            var foreground = _foregroundWindowService.GetForegroundWindowInfo();
            if (!ForegroundWindowService.MatchesExpectedWindow(expectedForeground, foreground))
            {
                return SelectionResult.Empty("原选区所在窗口已变化，请重新划词。", foreground);
            }

            System.Windows.IDataObject? original = null;
            var clipboardMutated = false;
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return SelectionResult.Empty("剪贴板兜底已取消。", foreground);
                }

                original = System.Windows.Clipboard.GetDataObject();
                var probe = new System.Windows.DataObject();
                probe.SetData(ProbeFormat, Guid.NewGuid().ToString("N"));
                System.Windows.Clipboard.SetDataObject(probe, copy: true);
                clipboardMutated = true;
                var probeSequence = NativeMethods.GetClipboardSequenceNumber();

                var copyTarget = _foregroundWindowService.GetForegroundWindowInfo();
                if (!ForegroundWindowService.MatchesExpectedWindow(expectedForeground, copyTarget))
                {
                    return SelectionResult.Empty("原选区所在窗口已变化，请重新划词。", copyTarget);
                }

                Forms.SendKeys.SendWait("^c");

                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.Elapsed < CopyTimeout)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return SelectionResult.Empty("剪贴板兜底已取消。", foreground);
                    }

                    var currentSequence = NativeMethods.GetClipboardSequenceNumber();
                    if (IsFreshClipboardUpdate(probeSequence, currentSequence)
                        && !System.Windows.Clipboard.ContainsData(ProbeFormat))
                    {
                        var text = System.Windows.Clipboard.ContainsText()
                            ? System.Windows.Clipboard.GetText()
                            : string.Empty;
                        return string.IsNullOrWhiteSpace(text)
                            ? SelectionResult.Empty("本次复制没有产生可翻译文本。", foreground)
                            : SelectionResult.FromText(text.Trim(), SelectionProviderKind.ClipboardFallback, null, foreground);
                    }

                    if (cancellationToken.WaitHandle.WaitOne(CopyPollInterval))
                    {
                        return SelectionResult.Empty("剪贴板兜底已取消。", foreground);
                    }
                }

                return SelectionResult.Empty("没有检测到本次划词产生的新文本，请重新划词后再试。", foreground);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Clipboard fallback failed. {ex.Message}");
                return SelectionResult.Empty("无法从剪贴板读取选区。", foreground);
            }
            finally
            {
                if (clipboardMutated)
                {
                    RestoreClipboard(original);
                }
            }
        }, cancellationToken);
    }

    public async Task<SelectionResult> ReadClipboardTextAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        return await RunOnStaThreadAsync(() =>
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
        }, cancellationToken);
    }

    private void RestoreClipboard(System.Windows.IDataObject? original)
    {
        try
        {
            if (original is null)
            {
                System.Windows.Clipboard.Clear();
            }
            else
            {
                System.Windows.Clipboard.SetDataObject(original, copy: true);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Clipboard restore failed. {ex.Message}");
        }
    }

    internal static bool IsFreshClipboardUpdate(uint probeSequence, uint currentSequence)
    {
        return probeSequence != currentSequence;
    }

    internal static Task<T> RunOnStaThreadAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Hermes Clipboard STA"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
