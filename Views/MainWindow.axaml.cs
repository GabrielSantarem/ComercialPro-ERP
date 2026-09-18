using Avalonia.Controls;
using Avalonia.Input;
using GetStartedApp.ViewModels;

namespace GetStartedApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            case Key.F1:
                vm.ToggleAjudaAtalhos();
                e.Handled = true;
                break;

            case Key.F2:
                // Se estiver no modo gerencial ou balcão, permite atalho direto
                if (vm.ModoTerminal != "CAIXA")
                {
                    vm.NavigateToBalcao();
                    e.Handled = true;
                }
                break;

            case Key.F3:
                // Se estiver no modo gerencial ou caixa, permite atalho direto
                if (vm.ModoTerminal != "BALCAO")
                {
                    vm.NavigateToPdv();
                    e.Handled = true;
                }
                break;

            case Key.F12:
                vm.AbrirConfiguracaoTerminal();
                e.Handled = true;
                break;

            case Key.Escape:
                if (vm.ExibirModalAjudaAtalhos)
                {
                    vm.ToggleAjudaAtalhos();
                    e.Handled = true;
                }
                else if (vm.ExibirModalAcessoGerencial)
                {
                    vm.FecharModalAcesso();
                    e.Handled = true;
                }
                break;
        }
    }
}
