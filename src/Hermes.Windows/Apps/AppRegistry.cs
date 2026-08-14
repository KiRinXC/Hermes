using Hermes.Windows.Apps.Contracts;

namespace Hermes.Windows.Apps;

public sealed class AppRegistry
{
    private readonly List<IHermesApp> _apps = [];

    public IReadOnlyList<IHermesApp> Apps => _apps;

    public void Register(IHermesApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (_apps.Any(existing => string.Equals(existing.Id, app.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Hermes app id is already registered: {app.Id}");
        }

        _apps.Add(app);
    }
}
