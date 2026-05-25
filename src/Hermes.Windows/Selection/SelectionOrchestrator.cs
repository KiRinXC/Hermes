using Hermes.Windows.Settings;

namespace Hermes.Windows.Selection;

public sealed class SelectionOrchestrator
{
    private readonly UiAutomationSelectionProvider _uiAutomationProvider;
    private readonly ClipboardSelectionProvider _clipboardProvider;
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly SettingsService _settingsService;

    public SelectionOrchestrator(
        UiAutomationSelectionProvider uiAutomationProvider,
        ClipboardSelectionProvider clipboardProvider,
        ForegroundWindowService foregroundWindowService,
        SettingsService settingsService)
    {
        _uiAutomationProvider = uiAutomationProvider;
        _clipboardProvider = clipboardProvider;
        _foregroundWindowService = foregroundWindowService;
        _settingsService = settingsService;
    }

    public async Task<(SelectionResult Result, SelectionValidationResult Validation)> ReadForExplicitTriggerAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        if (_foregroundWindowService.IsExcluded(foreground) || _foregroundWindowService.IsFocusedElementSensitive())
        {
            var excludedResult = SelectionResult.Empty("当前应用或控件已被排除。", foreground);
            return (excludedResult, SelectionValidationResult.Invalid(excludedResult.Message!));
        }

        var result = await _uiAutomationProvider.TryGetSelectionAsync(cancellationToken);
        var validation = SelectionTextValidator.Validate(result.Text, _settingsService.Current);
        if (result.Success && validation.IsValid)
        {
            return (result, validation);
        }

        result = await _clipboardProvider.TryCopySelectionAsync(cancellationToken);
        validation = SelectionTextValidator.Validate(result.Text, _settingsService.Current);
        return (result, validation);
    }

    public async Task<(SelectionResult Result, SelectionValidationResult Validation)> ReadForPassiveMouseAsync(CancellationToken cancellationToken = default)
    {
        var foreground = _foregroundWindowService.GetForegroundWindowInfo();
        if (_foregroundWindowService.IsExcluded(foreground) || _foregroundWindowService.IsFocusedElementSensitive())
        {
            var excludedResult = SelectionResult.Empty("当前应用或控件已被排除。", foreground);
            return (excludedResult, SelectionValidationResult.Invalid(excludedResult.Message!));
        }

        var result = await _uiAutomationProvider.TryGetSelectionAsync(cancellationToken);
        var validation = SelectionTextValidator.Validate(result.Text, _settingsService.Current);
        return (result, validation);
    }

    public async Task<(SelectionResult Result, SelectionValidationResult Validation)> ReadClipboardAsync(CancellationToken cancellationToken = default)
    {
        var result = await _clipboardProvider.ReadClipboardTextAsync(cancellationToken);
        var validation = SelectionTextValidator.Validate(result.Text, _settingsService.Current);
        return (result, validation);
    }
}
