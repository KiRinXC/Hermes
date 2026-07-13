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
    internal static readonly TimeSpan PassiveSelectionSettleDelay = TimeSpan.FromMilliseconds(25);

    private readonly SelectionOrchestrator _selectionOrchestrator;
    private readonly SelectionCandidateService _selectionCandidateService;
    private readonly ITranslationService _translationService;
    private readonly OverlayManager _overlayManager;
    private readonly TranslationHistoryService _historyService;
    private readonly SettingsService _settingsService;
    private readonly Dictionary<TranslationPopupWindow, PopupRequestContext> _popupRequestContexts = [];
    private CancellationTokenSource? _currentRequestCts;
    private CancellationTokenSource? _passiveButtonCts;
    private TranslationPopupWindow? _currentPopup;

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
        _overlayManager.PopupRetryRequested += async (_, args) =>
        {
            if (_popupRequestContexts.TryGetValue(args.Popup, out var context))
            {
                await TranslateSelectionAsync(context.Selection, context.Mode, args.Popup);
            }
        };
        _overlayManager.PopupClosedByUser += (_, args) =>
        {
            _popupRequestContexts.Remove(args.Popup);
            if (ReferenceEquals(_currentPopup, args.Popup))
            {
                _currentRequestCts?.Cancel();
            }
        };
        _overlayManager.PopupClosed += (_, args) => _popupRequestContexts.Remove(args.Popup);
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

        await TranslateSelectionAsync(selection, validation, TranslationMode.Translate, cancellationToken);
    }

    public async Task TranslateClipboardAsync(CancellationToken cancellationToken = default)
    {
        var (selection, validation) = await _selectionOrchestrator.ReadClipboardAsync(cancellationToken);
        if (!selection.Success || !validation.IsValid)
        {
            _overlayManager.ShowMessage(validation.Message ?? selection.Message ?? "剪贴板中没有可翻译文本。");
            return;
        }

        await TranslateSelectionAsync(selection, validation, TranslationMode.Translate, cancellationToken);
    }

    public async Task TryShowFloatingButtonAsync(
        int startX,
        int startY,
        int releaseX,
        int releaseY,
        DateTimeOffset startedAt,
        DateTimeOffset releasedAt,
        TranslationMode mode,
        bool ctrlDownAtStart,
        bool ctrlHeldDuringDrag,
        bool ctrlDownAtRelease,
        CancellationToken cancellationToken = default)
    {
        if (ShouldIgnorePassiveMouseGesture(ctrlDownAtStart, ctrlHeldDuringDrag, ctrlDownAtRelease))
        {
            return;
        }

        CancelPendingPassiveButton();
        var passiveButtonCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _passiveButtonCts = passiveButtonCts;
        var passiveButtonToken = passiveButtonCts.Token;
        try
        {
            await Task.Delay(PassiveSelectionSettleDelay, passiveButtonToken);
            var decision = await _selectionCandidateService.CreateFromMouseGestureAsync(
                startX,
                startY,
                releaseX,
                releaseY,
                startedAt,
                releasedAt,
                mode,
                ctrlDownAtStart,
                ctrlHeldDuringDrag,
                ctrlDownAtRelease,
                passiveButtonToken);

            if (!ReferenceEquals(_passiveButtonCts, passiveButtonCts) || passiveButtonToken.IsCancellationRequested)
            {
                return;
            }

            if (decision.ShouldShow && decision.Candidate is not null)
            {
                _overlayManager.ShowFloatingButton(decision.Candidate);
            }
        }
        catch (OperationCanceledException) when (passiveButtonToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_passiveButtonCts, passiveButtonCts))
            {
                _passiveButtonCts = null;
            }

            passiveButtonCts.Dispose();
        }
    }

    public void ClosePassiveUi()
    {
        CancelPendingPassiveButton();
        _overlayManager.CloseFloatingButton();
    }

    public void ClosePassiveUiAfterPointerActivity()
    {
        CancelPendingPassiveButton();
        _overlayManager.CloseFloatingButton();
        _overlayManager.CloseCompletedUnpinnedPopup();
    }

    public void CloseAll()
    {
        CancelPendingPassiveButton();
        _currentRequestCts?.Cancel();
        _overlayManager.CloseAll();
    }

    internal static bool ShouldIgnorePassiveMouseGesture(bool ctrlDownAtStart, bool ctrlHeldDuringDrag, bool ctrlDownAtRelease)
    {
        return !ctrlDownAtStart;
    }

    private void CancelPendingPassiveButton()
    {
        _passiveButtonCts?.Cancel();
        _passiveButtonCts = null;
    }

    private Task TranslateSelectionAsync(
        SelectionResult selection,
        TranslationMode mode,
        TranslationPopupWindow? popup = null,
        CancellationToken cancellationToken = default)
    {
        var validation = SelectionTextValidator.Validate(selection.Text, _settingsService.Current);
        return TranslateSelectionAsync(selection, validation, mode, cancellationToken, popup);
    }

    private async Task TranslateCandidateAsync(SelectionCandidate candidate, CancellationToken cancellationToken = default)
    {
        _currentRequestCts?.Cancel();
        _overlayManager.CloseFloatingButton();
        var loadingStateText = BuildLoadingStateTextForMode(_settingsService.Current, candidate.Mode);
        var loadingBodyText = candidate.Mode == TranslationMode.Explain ? "\u6B63\u5728\u89E3\u91CA" : "\u6B63\u5728\u7FFB\u8BD1";
        var popup = _overlayManager.ShowCandidateLoadingPopup(candidate, loadingStateText, loadingBodyText);
        await popup.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render, cancellationToken);

        if (SelectionCandidateService.TryCreatePreReadSelection(
            candidate,
            _settingsService.Current,
            out var preReadSelection,
            out var preReadValidation))
        {
            await TranslateSelectionAsync(preReadSelection, preReadValidation, candidate.Mode, cancellationToken, popup);
            return;
        }

        var (selection, validation) = await _selectionOrchestrator.ReadForCandidateTriggerAsync(
            candidate.ForegroundWindow,
            cancellationToken);
        if (!selection.Success || !validation.IsValid)
        {
            popup.SetError(validation.Message ?? selection.Message ?? "没有检测到可翻译文本。可以复制文本后再试。", showSettings: false);
            return;
        }

        await TranslateSelectionAsync(selection, validation, candidate.Mode, cancellationToken, popup);
    }

    private async Task TranslateSelectionAsync(
        SelectionResult selection,
        SelectionValidationResult validation,
        TranslationMode mode = TranslationMode.Translate,
        CancellationToken cancellationToken = default,
        TranslationPopupWindow? existingPopup = null)
    {
        if (selection.Text is null)
        {
            return;
        }

        _currentRequestCts?.Cancel();
        _currentRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestToken = _currentRequestCts.Token;

        var loadingStateText = BuildLoadingStateTextForMode(_settingsService.Current, mode);
        var loadingBodyText = mode == TranslationMode.Explain ? "\u6B63\u5728\u89E3\u91CA" : "\u6B63\u5728\u7FFB\u8BD1";
        var popup = existingPopup ?? _overlayManager.ShowPopup(selection, loadingStateText, loadingBodyText);
        _currentPopup = popup;
        _popupRequestContexts[popup] = new PopupRequestContext(selection, mode);
        if (existingPopup is not null)
        {
            popup.SetSourcePreview(Redactor.SummarizeText(selection.Text, 320));
            popup.SetLoading(loadingStateText, loadingBodyText);
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
                    popup.Dispatcher.Invoke(popup.SetLongRunning);
                }
            }, TaskScheduler.Default);

        var request = new TranslationRequest(
            selection.Text,
            _settingsService.Current.Translation.Style,
            _settingsService.Current.Translation.TargetLanguage,
            _settingsService.Current.Translation.PreserveFormatting,
            _settingsService.Current.Translation.SystemPrompt,
            mode,
            _settingsService.Current.Translation.ExplanationPreference);

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
                        await InvokePopupAsync(popup, token, () => popup.AppendTranslationDelta(deltaText));
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
                                    popup.AppendTranslationDelta(finalDelta);
                                }

                                popup.CompleteStreamingTranslation(streamEvent.CurrentText);
                            });
                        break;
                    case TranslationStreamEventKind.Failed when streamEvent.Result is not null:
                        pendingStreamDelta.Clear();
                        await InvokePopupAsync(
                            popup,
                            token,
                            () =>
                            {
                                popup.SetError(
                                    streamEvent.Result.UserMessage ?? "翻译失败，请稍后重试。",
                                    streamEvent.Result.ErrorKind is TranslationErrorKind.MissingApiKey or TranslationErrorKind.Authentication);
                            });
                        break;
                }
            },
            requestToken);

        if (result.Success && result.TranslatedText is not null)
        {
            popup.CompleteStreamingTranslation(result.TranslatedText);
            await _historyService.SaveAsync(selection.Text, result.TranslatedText, mode, requestToken);
        }
        else if (result.ErrorKind != TranslationErrorKind.Cancelled)
        {
            popup.SetError(
                result.UserMessage ?? "翻译失败，请稍后重试。",
                result.ErrorKind is TranslationErrorKind.MissingApiKey or TranslationErrorKind.Authentication);
        }

        if (ReferenceEquals(_currentPopup, popup))
        {
            _currentPopup = null;
        }

        _currentRequestCts?.Cancel();
        _currentRequestCts = null;
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

    internal static string BuildLoadingStateTextForMode(AppSettings settings, TranslationMode mode)
    {
        var actionText = mode == TranslationMode.Explain ? "\u6B63\u5728\u89E3\u91CA" : "\u6B63\u5728\u7FFB\u8BD1";
        var channel = ResolveLoadingChannelForMode(settings, mode);
        return $"{actionText} ({channel})...";
    }

    internal static string ResolveLoadingChannelForMode(AppSettings settings, TranslationMode mode)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (mode == TranslationMode.Explain || settings.Api.UseOpenAiForTranslation)
        {
            return ResolveOpenAiModel(settings);
        }

        return "Tencent";
    }

    private static string ResolveOpenAiModel(AppSettings settings)
    {
        var configuredModel = settings.Api.OpenAi.Model?.Trim();
        return string.IsNullOrWhiteSpace(configuredModel)
            ? OpenAiTranslationService.DefaultModel
            : configuredModel;
    }

    private sealed record PopupRequestContext(SelectionResult Selection, TranslationMode Mode);
}
