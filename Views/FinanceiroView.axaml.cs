using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GetStartedApp.Views;

public partial class FinanceiroView : UserControl
{
    public FinanceiroView()
    {
       AvaloniaXamlLoader.Load(this);
    }
}
