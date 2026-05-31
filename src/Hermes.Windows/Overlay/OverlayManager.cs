using Hermes.Windows.Infrastructure;
using Hermes.Windows.Selection;
using Hermes.Windows.Settings;
using Hermes.Windows.UI.Themes;
using WpfApplication = System.Windows.Application;

namespace Hermes.Windows.Overlay;

public sealed class OverlayManager
{
    private readonly OverlayPositionService _positionService;
    private readonly SettingsService _settingsService;
    private readonly List<TranslationPopupWindow> _popups = [];
    private FloatingButtonWindow? _floatingButton;
    private TranslationPopupWindow? _latestPopup;

    public OverlayManager(OverlayPositionService positionService, SettingsService settingsService)
    {
        _positionService = positionService;
        _settingsService = settingsService;
    }

    public event EventHandler<SelectionCandidate>? FloatingButtonTranslateRequested;

    public event EventHandler<PopupEventArgs>? PopupRetryRequested;

    public event EventHandler? PopupSettingsRequested;

    public event EventHandler<PopupEventArgs>? PopupClosedByUser;

    public event EventHandler<PopupEventArgs>? PopupClosed;

    public void ShowFloatingButton(SelectionCandidate candidate)
    {
        CloseFloatingButton();
        var dimensions = FloatingButtonWindow.ResolveDimensions(_settingsService.Current.Ui.FloatingButtonSize);
        var buttonSize = dimensions.HitSize;
        var position = _positionService.PositionNearSelection(candidate.Bounds, buttonSize, buttonSize, candidate.ReleaseX, candidate.ReleaseY);
        var pointToTextAbove = ShouldPointToTextAbove(candidate, position.Top, buttonSize);
        var style = ResolveFloatingButtonVisualStyle(_settingsService.Current.Ui.FloatingButtonStyle);
        _floatingButton = new FloatingButtonWindow(candidate, style, pointToTextAbove, dimensions);
        _floatingButton.TranslateRequested += (_, result) => FloatingButtonTranslateRequested?.Invoke(this, result);
        _floatingButton.Left = position.Left;
        _floatingButton.Top = position.Top;
        _floatingButton.Show();
    }

    public bool ContainsOverlayPoint(int x, int y)
    {
        if (ContainsWindowPoint(_floatingButton, x, y))
        {
            return true;
        }

        foreach (var popup in _popups)
        {
            if (ContainsWindowPoint(popup, x, y))
            {
                return true;
            }
        }

        return false;
    }

    public TranslationPopupWindow ShowPopup(
        SelectionResult selection,
        string? loadingStateText = null,
        string? loadingBodyText = null)
    {
        var popup = CreatePopup();
        popup.SetSourcePreview(Redactor.SummarizeText(selection.Text, 320));
        popup.SetLoading(loadingStateText, loadingBodyText);

        var position = selection.Bounds is { } bounds
            ? _positionService.PositionNearSelection(bounds, popup.Width, popup.Height, (int)bounds.Left, (int)bounds.Top)
            : _positionService.PositionAtCursor(popup.Width, popup.Height);
        popup.Left = position.Left;
        popup.Top = position.Top;
        popup.Show();
        return popup;
    }

    public TranslationPopupWindow ShowCandidateLoadingPopup(
        SelectionCandidate candidate,
        string? loadingStateText = null,
        string? loadingBodyText = null)
    {
        var popup = CreatePopup();
        popup.SetSourcePreview(string.Empty);
        popup.SetLoading(loadingStateText, loadingBodyText);

        var position = _positionService.PositionNearSelection(
            candidate.Bounds,
            popup.Width,
            popup.Height,
            candidate.ReleaseX,
            candidate.ReleaseY);
        popup.Left = position.Left;
        popup.Top = position.Top;
        popup.Show();
        return popup;
    }

    public void ShowMessage(string message)
    {
        var selection = SelectionResult.FromText(message, SelectionProviderKind.None, null, null);
        var popup = ShowPopup(selection);
        popup.SetError(message, showSettings: false);
    }

    public void CloseFloatingButton()
    {
        CloseTrackedFloatingButton();
        CloseOrphanFloatingButtons();
    }

    public void CloseUnpinnedPopup()
    {
        if (_latestPopup is { IsPinned: false } popup)
        {
            UntrackPopup(popup);
            popup.CloseWithFade();
        }
    }

    public void CloseCompletedUnpinnedPopup()
    {
        if (_latestPopup is { IsPinned: false, HasCompletedTranslation: true } popup)
        {
            UntrackPopup(popup);
            popup.CloseWithFade();
        }
    }

    public void CloseAll()
    {
        CloseFloatingButton();

        foreach (var popup in _popups.ToArray())
        {
            popup.CloseWithFade();
        }

        _popups.Clear();
        _latestPopup = null;
    }

    private bool IsDarkTheme() => ThemeResourceService.ShouldUseDarkTheme(_settingsService.Current.Ui.Theme);

    private static FloatingButtonVisualStyle ResolveFloatingButtonVisualStyle(string? value)
    {
        return string.Equals(value, "LightBorderDarkFill", StringComparison.OrdinalIgnoreCase)
            ? FloatingButtonVisualStyle.LightBorderDarkFill
            : FloatingButtonVisualStyle.DarkBorderLightFill;
    }

    private double GetPopupWidth()
    {
        var width = _settingsService.Current.Ui.PopupWidth;
        return Math.Clamp(width, 280, 640);
    }

    private double GetPopupHeight()
    {
        var height = _settingsService.Current.Ui.PopupHeight;
        return Math.Clamp(height, 180, 720);
    }

    private TranslationPopupWindow CreatePopup()
    {
        CloseUnpinnedPopup();
        var popup = new TranslationPopupWindow
        {
            Width = GetPopupWidth(),
            Height = GetPopupHeight()
        };

        popup.ApplyTheme(IsDarkTheme(), _settingsService.Current.Ui.Opacity, _settingsService.Current.Ui.FontSize);
        popup.RetryRequested += (_, _) => PopupRetryRequested?.Invoke(this, new PopupEventArgs(popup));
        popup.SettingsRequested += (_, _) => PopupSettingsRequested?.Invoke(this, EventArgs.Empty);
        popup.ClosedByUser += (_, _) => PopupClosedByUser?.Invoke(this, new PopupEventArgs(popup));
        popup.Closed += (_, _) =>
        {
            UntrackPopup(popup);
            PopupClosed?.Invoke(this, new PopupEventArgs(popup));
        };
        popup.SizeChangedByUser += (_, size) => PersistPopupSize(size.Width, size.Height);

        _popups.Add(popup);
        _latestPopup = popup;
        return popup;
    }

    private void CloseTrackedFloatingButton()
    {
        var tracked = _floatingButton;
        _floatingButton = null;
        CloseFloatingButtonWindow(tracked);
    }

    private void CloseOrphanFloatingButtons()
    {
        var orphanButtons = WpfApplication.Current?.Windows.OfType<FloatingButtonWindow>().ToArray() ?? [];
        foreach (var orphan in orphanButtons)
        {
            CloseFloatingButtonWindow(orphan);
        }
    }

    private static void CloseFloatingButtonWindow(FloatingButtonWindow? window)
    {
        if (window is null)
        {
            return;
        }

        try
        {
            window.Close();
        }
        catch (InvalidOperationException)
        {
            // The window may already be closing from its own fade-out timer.
        }
    }

    private void PersistPopupSize(double width, double height)
    {
        var settings = _settingsService.Current;
        settings.Ui.PopupWidth = Math.Clamp(Math.Round(width), 280, 640);
        settings.Ui.PopupHeight = Math.Clamp(Math.Round(height), 180, 720);
        _ = _settingsService.SaveAsync(settings);
    }

    private void UntrackPopup(TranslationPopupWindow popup)
    {
        _popups.Remove(popup);
        if (!ReferenceEquals(_latestPopup, popup))
        {
            return;
        }

        _latestPopup = _popups.Count > 0 ? _popups[^1] : null;
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

public sealed class PopupEventArgs : EventArgs
{
    public PopupEventArgs(TranslationPopupWindow popup)
    {
        Popup = popup;
    }

    public TranslationPopupWindow Popup { get; }
}
