using System.Windows;
using System.Windows.Controls;

namespace Hermes.Windows.Apps;

public partial class AppsCenterView : System.Windows.Controls.UserControl
{
    private readonly AppRegistry _registry;

    public AppsCenterView(AppRegistry registry)
    {
        InitializeComponent();
        _registry = registry;
        AppsItems.ItemsSource = registry.Apps;
    }

    private void AppCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string appId })
        {
            return;
        }

        var app = _registry.Apps.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, appId, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return;
        }

        DetailTitle.Text = app.Name;
        DetailContent.Content = app.CreateView();
        CatalogView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DetailView.Visibility = Visibility.Collapsed;
        CatalogView.Visibility = Visibility.Visible;
    }
}
