namespace Hermes.Windows.Apps.Contracts;

public interface IHermesConfirmationHost
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
}
