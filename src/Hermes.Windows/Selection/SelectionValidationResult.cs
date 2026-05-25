namespace Hermes.Windows.Selection;

public sealed record SelectionValidationResult(bool IsValid, bool IsSoftLimitExceeded, string? Message)
{
    public static SelectionValidationResult Valid(bool softLimitExceeded = false)
    {
        return new SelectionValidationResult(true, softLimitExceeded, null);
    }

    public static SelectionValidationResult Invalid(string message)
    {
        return new SelectionValidationResult(false, false, message);
    }
}
