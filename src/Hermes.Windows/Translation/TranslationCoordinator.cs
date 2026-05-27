using Hermes.Windows.History;
using Hermes.Windows.Infrastructure;
using Hermes.Windows.Overlay;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using System.Text;
using System.Windows.Threading;

namespace Hermes.Windows.Translation;

public sealed class TranslationCoordinator
{
    private readonly SelectionOrchestrator _selectionOrchestrator;
    private readonly SelectionCandidateService _selectionCandidateService;
    private readonly ITranslationService _translationService;
    private readonly OverlayManager _overlayManager;
    private readonly TranslationHistoryService _historyService;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _currentRequestCts;
    private SelectionResult? _currentSelection;

    public TranslationCoordinator(
        SelectionOrchestrator selectionOrchestrator,
        SelectionCandidateService selectionCandidateService,
        ITranslationService translationService,
        OverlayManager overlayManager,
        TranslationHistoryService historyService,
        SettingsService settingsService)
    {
        _selectionOrchestrator = selectionOrchestrator;
        _selectionCandidateService = selectionCandidateService;
        _translationService = translationService;
        _overlayManager = overlayManager;
        _historyService = historyService;
        _settingsService = settingsService;

        _overlayManager.FloatingButtonTranslateRequested += async (_, candidate) => await TranslateCandidateAsync(candidate);
        _overlayManager.PopupRetryRequested += async (_, _) =>
        {
            if (_currentSelection is not null)
            {
                await TranslateSelectionAsync(_currentSelection);
            }
        };
        _overlayManager.PopupClosedByUser += (_, _) => _currentRequestCts?.Cancel();
    }

    public event EventHandler? SettingsRequested
    {
        add => _overlayManager.PopupSettingsRequested += value;
        remove => _overlayManager.PopupSettingsRequested -= value;
    }

    public async Task TranslateCurrentSelectionAsync(CancellationToken cancellationToken = default)
    {
        var (selection, validation) = await _selectionOrchestrator.ReadForExplicitTriggerAsync(cancellationToken);
        if (!selection.Success || !validation.IsValid)
        {
            _overlayManager.ShowMessage(validation.Message ?? selection.Message ?? "没有找到可翻译的文本。");
            return;
        }

        await TranslateSelectionAsync(selection, validation, cancellationToken);
    }

    public async Task TranslateClipboardAsync(CancellationToken cancellationToken = default)
    {
        var (selection, validation) = await _selectionOrchestrator.ReadClipboardAsync(cancellationToken);
        if (!selection.Success || !validation.IsValid)
        {
            _overlayManager.ShowMessage(validation.Message ?? selection.Message ?? "剪贴板中没有可翻译文本。");
            return;
        }

        await TranslateSelectionAsync(selection, validation, cancellationToken);
    }

    public async Task TryShowFloatingButtonAsync(
        int startX,
        int startY,
        int releaseX,
        int releaseY,
        DateTimeOffset startedAt,
        DateTimeOffset releasedAt,
        bool ctrlDownAtStart,
        bool ctrlHeldDuringDrag,
        bool ctrlDownAtRelease,
        CancellationToken cancellationToken = default)
    {
        if (ShouldIgnorePassiveMouseGesture(ctrlDownAtStart, ctrlHeldDuringDrag, ctrlDownAtRelease))
        {
            return;
        }

        await Task.Delay(90, cancellationToken);
        var decision = await _selectionCandidateService.CreateFromMouseGestureAsync(
            startX,
            startY,
            releaseX,
            releaseY,
            startedAt,
            releasedAt,
            ctrlDownAtStart,
            ctrlHeldDuringDrag,
            ctrlDownAtRelease,
            cancellationToken);

        if (decision.ShouldShow && decision.Candidate is not null)
        {
            _overlayManager.ShowFloatingButton(decision.Candidate);
        }
    }

    public void ClosePassiveUi()
    {
        _overlayManager.CloseFloatingButton();
    }

    public void ClosePassiveUiAfterPointerActivity()
    {
        _overlayManager.CloseFloatingButton();
        _overlayManager.CloseCompletedUnpinnedPopup();
    }

    public void CloseAll()
    {
        _currentRequestCts?.Cancel();
        _overlayManager.CloseAll();
    }

    internal static bool ShouldIgnorePassiveMouseGesture(bool ctrlDownAtStart, bool ctrlHeldDuringDrag, bool ctrlDownAtRelease)
    {
        return !ctrlDownAtStart || !ctrlHeldDuringDrag || !ctrlDownAtRelease;
    }

    private Task TranslateSelectionAsync(SelectionResult selection)
    {
        var validation = SelectionTextValidator.Validate(selection.Text, _settingsService.Current);
        return TranslateSelectionAsync(selection, validation, CancellationToken.None);
    }

    private async Task TranslateCandidateAsync(SelectionCandidate candidate, CancellationToken cancellationToken = default)
    {
        _currentRequestCts?.Cancel();
        _currentRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _overlayManager.CloseFloatingButton();
        var popup = _overlayManager.ShowCandidateLoadingPopup(candidate);
        await popup.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render, _currentRequestCts.Token);

        if (SelectionCandidateService.TryCreatePreReadSelection(
            candidate,
            _settingsService.Current,
            out var preReadSelection,
            out var preReadValidation))
        {
            await TranslateSelectionAsync(preReadSelection, preReadValidation, _currentRequestCts.Token, popup);
            return;
        }

        var (selection, validation) = await _selectionOrchestrator.ReadForExplicitTriggerAsync(_currentRequestCts.Token);
        if (!selection.Success || !validation.IsValid)
        {
            popup.SetError(validation.Message ?? selection.Message ?? "没有检测到可翻译文本。可以复制文本后再试。", showSettings: false);
            return;
        }

        await TranslateSelectionAsync(selection, validation, _currentRequestCts.Token, popup);
    }

    private async Task TranslateSelectionAsync(
        SelectionResult selection,
        SelectionValidationResult validation,
        CancellationToken cancellationToken = default,
        TranslationPopupWindow? existingPopup = null)
    {
        if (selection.Text is null)
        {
            return;
        }

        if (existingPopup is null)
        {
            _currentRequestCts?.Cancel();
            _currentRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var requestToken = _currentRequestCts?.Token ?? cancellationToken;
        _currentSelection = selection;

        var popup = existingPopup ?? _overlayManager.ShowPopup(selection);
        if (existingPopup is not null)
        {
            popup.SetSourcePreview(Redactor.SummarizeText(selection.Text, 320));
            popup.SetLoading();
        }

        if (validation.IsSoftLimitExceeded)
        {
            popup.SetError("文本较长，可能需要更久。正在继续翻译...", showSettings: false);
        }

        _ = Task.Delay(TimeSpan.FromSeconds(8), requestToken)
            .ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    popup.Dispatcher.Invoke(() => _overlayManager.SetLongRunning());
                }
            }, TaskScheduler.Default);

        var request = new TranslationRequest(
            selection.Text,
            _settingsService.Current.Translation.Style,
            _settingsService.Current.Translation.TargetLanguage,
            _settingsService.Current.Translation.PreserveFormatting,
            _settingsService.Current.Translation.SystemPrompt);

        var pendingStreamDelta = new StringBuilder();
        var lastStreamFlush = DateTimeOffset.MinValue;
        var streamFlushInterval = TimeSpan.FromMilliseconds(40);
        var result = await _translationService.TranslateStreamAsync(
            request,
            async (streamEvent, token) =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                switch (streamEvent.Kind)
                {
                    case TranslationStreamEventKind.Delta when streamEvent.DeltaText is not null:
                        pendingStreamDelta.Append(streamEvent.DeltaText);
                        var now = DateTimeOffset.UtcNow;
                        if (now - lastStreamFlush < streamFlushInterval)
                        {
                            return;
                        }

                        var deltaText = pendingStreamDelta.ToString();
                        pendingStreamDelta.Clear();
                        lastStreamFlush = now;
                        await InvokePopupAsync(popup, token, () => _overlayManager.AppendTranslationDelta(deltaText));
                        break;
                    case TranslationStreamEventKind.Completed when streamEvent.CurrentText is not null:
                        var finalDelta = pendingStreamDelta.ToString();
                        pendingStreamDelta.Clear();
                        await InvokePopupAsync(
                            popup,
                            token,
                            () =>
                            {
                                if (!string.IsNullOrEmpty(finalDelta))
                                {
                                    _overlayManager.AppendTranslationDelta(finalDelta);
                                }

                                _overlayManager.CompleteStreamingTranslation(streamEvent.CurrentText);
                            });
                        break;
                    case TranslationStreamEventKind.Failed when streamEvent.Result is not null:
                        pendingStreamDelta.Clear();
                        await InvokePopupAsync(
                            popup,
                            token,
                            () =>
                        {
                            _overlayManager.SetError(
                                streamEvent.Result.UserMessage ?? "翻译失败，请稍后重试。",
                                streamEvent.Result.ErrorKind is TranslationErrorKind.MissingApiKey or TranslationErrorKind.Authentication);
                        });
                        break;
                }
            },
            requestToken);

        if (result.Success && result.TranslatedText is not null)
        {
            _overlayManager.CompleteStreamingTranslation(result.TranslatedText);
            await _historyService.SaveAsync(selection.Text, result.TranslatedText, requestToken);
        }
        else if (result.ErrorKind != TranslationErrorKind.Cancelled)
        {
            _overlayManager.SetError(
                result.UserMessage ?? "翻译失败，请稍后重试。",
                result.ErrorKind is TranslationErrorKind.MissingApiKey or TranslationErrorKind.Authentication);
        }
        _currentRequestCts?.Cancel();
    }

    private static async Task InvokePopupAsync(TranslationPopupWindow popup, CancellationToken token, Action update)
    {
        await popup.Dispatcher.InvokeAsync(
            () =>
            {
                if (!token.IsCancellationRequested)
                {
                    update();
                }
            },
            DispatcherPriority.Background,
            token);
    }
}
