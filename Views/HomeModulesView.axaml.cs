using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GetStartedApp.Views;

public partial class HomeModulesView : UserControl
{
    public HomeModulesView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}