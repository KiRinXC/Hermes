using Hermes.Windows.Infrastructure;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.UI.Themes;

namespace Hermes.Windows.Overlay;

public sealed class OverlayManager
{
    private readonly OverlayPositionService _positionService;
    private readonly SettingsService _settingsService;
    private FloatingButtonWindow? _floatingButton;
    private TranslationPopupWindow? _popup;

    public OverlayManager(OverlayPositionService positionService, SettingsService settingsService)
    {
        _positionService = positionService;
        _settingsService = settingsService;
    }

    public event EventHandler<SelectionCandidate>? FloatingButtonTranslateRequested;

    public event EventHandler? PopupRetryRequested;

    public event EventHandler? PopupSettingsRequested;

    public event EventHandler? PopupClosedByUser;

    public void ShowFloatingButton(SelectionCandidate candidate)
    {
        CloseFloatingButton();
        const int buttonSize = 44;
        var position = _positionService.PositionNearSelection(candidate.Bounds, buttonSize, buttonSize, candidate.ReleaseX, candidate.ReleaseY);
        var pointToTextAbove = ShouldPointToTextAbove(candidate, position.Top, buttonSize);
        _floatingButton = new FloatingButtonWindow(candidate, IsDarkTheme(), pointToTextAbove);
        _floatingButton.TranslateRequested += (_, result) => FloatingButtonTranslateRequested?.Invoke(this, result);
        _floatingButton.Left = position.Left;
        _floatingButton.Top = position.Top;
        _floatingButton.Show();
    }

    public bool ContainsOverlayPoint(int x, int y)
    {
        return ContainsWindowPoint(_floatingButton, x, y)
            || ContainsWindowPoint(_popup, x, y);
    }

    public TranslationPopupWindow ShowPopup(SelectionResult selection)
    {
        CloseUnpinnedPopup();
        _popup = new TranslationPopupWindow
        {
            Width = GetPopupWidth()
        };

        _popup.ApplyTheme(IsDarkTheme(), _settingsService.Current.Ui.Opacity, _settingsService.Current.Ui.FontSize);
        _popup.SetSourcePreview(Redactor.SummarizeText(selection.Text, 320));
        _popup.SetLoading();
        _popup.RetryRequested += (_, _) => PopupRetryRequested?.Invoke(this, EventArgs.Empty);
        _popup.SettingsRequested += (_, _) => PopupSettingsRequested?.Invoke(this, EventArgs.Empty);
        _popup.ClosedByUser += (_, _) => PopupClosedByUser?.Invoke(this, EventArgs.Empty);

        var position = selection.Bounds is { } bounds
            ? _positionService.PositionNearSelection(bounds, _popup.Width, 220, (int)bounds.Left, (int)bounds.Top)
            : _positionService.PositionAtCursor(_popup.Width, 220);
        _popup.Left = position.Left;
        _popup.Top = position.Top;
        _popup.Show();
        return _popup;
    }

    public TranslationPopupWindow ShowCandidateLoadingPopup(SelectionCandidate candidate)
    {
        CloseUnpinnedPopup();
        _popup = new TranslationPopupWindow
        {
            Width = GetPopupWidth()
        };

        _popup.ApplyTheme(IsDarkTheme(), _settingsService.Current.Ui.Opacity, _settingsService.Current.Ui.FontSize);
        _popup.SetSourcePreview(string.Empty);
        _popup.SetLoading();
        _popup.RetryRequested += (_, _) => PopupRetryRequested?.Invoke(this, EventArgs.Empty);
        _popup.SettingsRequested += (_, _) => PopupSettingsRequested?.Invoke(this, EventArgs.Empty);
        _popup.ClosedByUser += (_, _) => PopupClosedByUser?.Invoke(this, EventArgs.Empty);

        var position = _positionService.PositionNearSelection(
            candidate.Bounds,
            _popup.Width,
            220,
            candidate.ReleaseX,
            candidate.ReleaseY);
        _popup.Left = position.Left;
        _popup.Top = position.Top;
        _popup.Show();
        return _popup;
    }

    public void ShowMessage(string message)
    {
        var selection = SelectionResult.FromText(message, SelectionProviderKind.None, null, null);
        var popup = ShowPopup(selection);
        popup.SetError(message, showSettings: false);
    }

    public void SetLongRunning()
    {
        _popup?.SetLongRunning();
    }

    public void SetTranslation(string translation)
    {
        _popup?.SetTranslation(translation);
    }

    public void AppendTranslationDelta(string deltaText)
    {
        _popup?.AppendTranslationDelta(deltaText);
    }

    public void CompleteStreamingTranslation(string translation)
    {
        _popup?.CompleteStreamingTranslation(translation);
    }

    public void SetError(string message, bool showSettings)
    {
        _popup?.SetError(message, showSettings);
    }

    public void CloseFloatingButton()
    {
        if (_floatingButton is not null)
        {
            _floatingButton.Close();
            _floatingButton = null;
        }
    }

    public void CloseUnpinnedPopup()
    {
        if (_popup is { IsPinned: false })
        {
            _popup.CloseWithFade();
            _popup = null;
        }
    }

    public void CloseCompletedUnpinnedPopup()
    {
        if (_popup is { IsPinned: false, HasCompletedTranslation: true })
        {
            _popup.CloseWithFade();
            _popup = null;
        }
    }

    public void CloseAll()
    {
        CloseFloatingButton();
        if (_popup is not null)
        {
            _popup.CloseWithFade();
            _popup = null;
        }
    }

    private bool IsDarkTheme() => ThemeResourceService.ShouldUseDarkTheme(_settingsService.Current.Ui.Theme);

    private double GetPopupWidth()
    {
        var width = _settingsService.Current.Ui.PopupWidth;
        if (Math.Abs(width - 420) < 0.1)
        {
            return 360;
        }

        return Math.Clamp(width, 320, 480);
    }

    private static bool ShouldPointToTextAbove(SelectionCandidate candidate, double buttonTop, double buttonHeight)
    {
        var buttonCenterY = buttonTop + buttonHeight / 2;
        if (candidate.Bounds is { IsEmpty: false } bounds)
        {
            return buttonCenterY > (bounds.Top + bounds.Bottom) / 2;
        }

        return buttonCenterY > candidate.ReleaseY;
    }

    private static bool ContainsWindowPoint(System.Windows.Window? window, int x, int y)
    {
        if (window is not { IsVisible: true })
        {
            return false;
        }

        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        return x >= window.Left
            && x <= window.Left + width
            && y >= window.Top
            && y <= window.Top + height;
    }
}
