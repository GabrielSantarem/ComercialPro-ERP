using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GetStartedApp.ViewModels;
using Serilog;

namespace GetStartedApp.Views;

public partial class PdvView : UserControl
{
    public PdvView()
    {
        AvaloniaXamlLoader.Load(this);

        // Intercepta teclas no nível mais alto do UserControl via Tunnel
        this.AddHandler(InputElement.KeyDownEvent, PdvView_KeyDownTunnel, RoutingStrategies.Tunnel);

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

    private async void PdvView_KeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (this.DataContext is not PdvViewModel vm) return;

        // SE O MODAL DE PAGAMENTO ESTIVER ABERTO:
        if (vm.IsModalAberto)
        {
            if (e.Key == Key.Escape)
            {
                vm.FecharModalPagamentoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (vm.PodeConfirmarPagamento)
                {
                    await vm.ConfirmarPagamentoCommand.ExecuteAsync(null);
                    FocarBusca();
                    e.Handled = true;
                }
                return;
            }
            return;
        }

        // SE ESTIVER NA TELA PRINCIPAL DO PDV:

        // F12: Abre modal de pagamento / fechamento
        if (e.Key == Key.F12)
        {
            Log.Information("[PDV ATALHO] F12 pressionado -> Abrir Pagamento");
            vm.AbrirModalPagamentoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F2: Força o foco no campo de busca
        if (e.Key == Key.F2)
        {
            FocarBusca();
            e.Handled = true;
            return;
        }

        // F8: Cancela/Remove o item selecionado do carrinho
        if (e.Key == Key.F8)
        {
            Log.Information("[PDV ATALHO] F8 pressionado -> Remover Item");
            vm.RemoverItemSelecionadoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // ESC: Limpa o carrinho todo (se tiver itens) ou limpa a busca
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

        // SETA PARA BAIXO (Down): Navega para o próximo produto na lista de sugestões
        if (e.Key == Key.Down && vm.ResultadosPesquisa.Count > 0)
        {
            var lista = vm.ResultadosPesquisa.ToList();
            var atualIdx = vm.ProdutoPesquisaSelecionado != null ? lista.IndexOf(vm.ProdutoPesquisaSelecionado) : -1;
            var proximoIdx = Math.Min(atualIdx + 1, lista.Count - 1);
            vm.ProdutoPesquisaSelecionado = lista[proximoIdx];
            e.Handled = true;
            return;
        }

        // SETA PARA CIMA (Up): Navega para o produto anterior na lista de sugestões
        if (e.Key == Key.Up && vm.ResultadosPesquisa.Count > 0)
        {
            var lista = vm.ResultadosPesquisa.ToList();
            var atualIdx = vm.ProdutoPesquisaSelecionado != null ? lista.IndexOf(vm.ProdutoPesquisaSelecionado) : 0;
            var anteriorIdx = Math.Max(atualIdx - 1, 0);
            vm.ProdutoPesquisaSelecionado = lista[anteriorIdx];
            e.Handled = true;
            return;
        }

        // ENTER: Lança o produto no carrinho com a quantidade especificada
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
            {
                vm.TextoPesquisa = txt.Text;
            }

            Log.Information("[PDV ATALHO] ENTER pressionado -> Lançando produto com texto '{Texto}'", vm.TextoPesquisa);
            await vm.LancarProdutoCommand.ExecuteAsync(null);

            if (txt != null)
            {
                txt.Text = string.Empty;
                txt.Focus();
            }

            e.Handled = true;
        }
    }
}
