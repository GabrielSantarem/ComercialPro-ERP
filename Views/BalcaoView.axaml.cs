using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GetStartedApp.ViewModels;

namespace GetStartedApp.Views;

public partial class BalcaoView : UserControl
{
    public BalcaoView()
    {
        AvaloniaXamlLoader.Load(this);

        this.AddHandler(InputElement.KeyDownEvent, BalcaoView_KeyDownTunnel, RoutingStrategies.Tunnel);

        this.AttachedToVisualTree += (s, e) =>
        {
            FocarBusca();
        };
    }

    private void FocarBusca()
    {
        var txt = this.FindControl<TextBox>("TxtPesquisa");
        txt?.Focus();
    }

    private async void BalcaoView_KeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (this.DataContext is not BalcaoViewModel vm) return;

        // Se modal de confirmação estiver aberto, ENTER ou ESC fecha e foca na busca
        if (vm.IsModalConfirmacaoAberto)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Escape)
            {
                vm.FecharModalConfirmacaoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
            }
            return;
        }

        // F12: Salvar e Enviar ao Caixa Central
        if (e.Key == Key.F12)
        {
            await vm.EnviarAoCaixaCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        // F2: Focar campo de busca
        if (e.Key == Key.F2)
        {
            FocarBusca();
            e.Handled = true;
            return;
        }

        // F8: Remover item selecionado
        if (e.Key == Key.F8)
        {
            vm.CancelarItemSelecionadoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // ESC: Limpar busca ou carrinho
        if (e.Key == Key.Escape)
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            if (txt != null && !string.IsNullOrEmpty(txt.Text))
            {
                txt.Text = string.Empty;
                vm.TextoPesquisa = string.Empty;
            }
            else
            {
                vm.LimparCarrinhoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // SETA PARA BAIXO / CIMA nas sugestões
        if (e.Key == Key.Down && vm.ResultadosPesquisa.Count > 0)
        {
            var lista = vm.ResultadosPesquisa.ToList();
            var atualIdx = vm.ProdutoPesquisaSelecionado != null ? lista.IndexOf(vm.ProdutoPesquisaSelecionado) : -1;
            var proximoIdx = Math.Min(atualIdx + 1, lista.Count - 1);
            vm.ProdutoPesquisaSelecionado = lista[proximoIdx];
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && vm.ResultadosPesquisa.Count > 0)
        {
            var lista = vm.ResultadosPesquisa.ToList();
            var atualIdx = vm.ProdutoPesquisaSelecionado != null ? lista.IndexOf(vm.ProdutoPesquisaSelecionado) : 0;
            var anteriorIdx = Math.Max(atualIdx - 1, 0);
            vm.ProdutoPesquisaSelecionado = lista[anteriorIdx];
            e.Handled = true;
            return;
        }

        // ENTER: Lança produto
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
            {
                vm.TextoPesquisa = txt.Text;
            }

            vm.LancarProdutoCommand.Execute(null);

            if (txt != null)
            {
                txt.Text = string.Empty;
                txt.Focus();
            }

            e.Handled = true;
        }
    }
}
