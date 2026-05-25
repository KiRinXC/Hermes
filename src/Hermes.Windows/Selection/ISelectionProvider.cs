namespace Hermes.Windows.Selection;

public interface ISelectionProvider
{
    Task<SelectionResult> TryGetSelectionAsync(CancellationToken cancellationToken = default);
}
