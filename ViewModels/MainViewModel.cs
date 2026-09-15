using CommunityToolkit.Mvvm.ComponentModel;

namespace GetStartedApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "vai tomar no seu kuu";
}
