using CommunityToolkit.Mvvm.ComponentModel;
namespace GetStartedApp.ViewModels;
public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Mensagem { get; set; } = "Bem-vindo ao Painel Gerencial do PDV!";
}
