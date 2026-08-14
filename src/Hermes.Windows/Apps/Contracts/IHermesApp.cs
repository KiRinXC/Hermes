using System.Windows;
using System.Windows.Media;

namespace Hermes.Windows.Apps.Contracts;

public interface IHermesApp
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    Geometry Icon { get; }

    FrameworkElement CreateView();
}
