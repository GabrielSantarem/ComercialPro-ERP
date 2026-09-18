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

        // 1. Fechamento de modais globais da MainWindow com ESC
        if (e.Key == Key.Escape)
        {
            if (vm.ExibirModalAjudaAtalhos)
            {
                vm.ToggleAjudaAtalhos();
                e.Handled = true;
                return;
            }
            if (vm.ExibirModalAcessoGerencial)
            {
                vm.FecharModalAcesso();
                e.Handled = true;
                return;
            }
        }

        // 2. Atalhos Globais com CTRL para não colidir com PDV/Balcão/Fiscal
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.F1:
                    vm.ToggleAjudaAtalhos();
                    e.Handled = true;
                    return;

                case Key.F12:
                    vm.AbrirConfiguracaoTerminal();
                    e.Handled = true;
                    return;
            }
        }

        // 3. Teclas F1 e F12 diretas SOMENTE quando estiver no Hub Inicial (HomeModulesViewModel)
        if (vm.CurrentPage is HomeModulesViewModel)
        {
            switch (e.Key)
            {
                case Key.F1:
                    vm.ToggleAjudaAtalhos();
                    e.Handled = true;
                    break;

                case Key.F2:
                    if (vm.ModoTerminal != "CAIXA")
                    {
                        vm.NavigateToBalcao();
                        e.Handled = true;
                    }
                    break;

                case Key.F3:
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
            }
        }
    }
}
