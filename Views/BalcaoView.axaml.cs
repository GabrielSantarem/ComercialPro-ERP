using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GetStartedApp.ViewModels;
using Serilog;

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
        Dispatcher.UIThread.Post(() =>
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            txt?.Focus();
        });
    }

    private void FocarIdentificacaoCliente()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var txt = this.FindControl<TextBox>("TxtNomeCliente");
            if (txt != null)
            {
                txt.Focus();
                txt.SelectAll();
            }
        });
    }

    private async void BalcaoView_KeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (this.DataContext is not BalcaoViewModel vm) return;

        Log.Information("[BALCÃO KEY TUNNEL] Tecla: {Key}", e.Key);

        // 1. Se modal de confirmação final (comanda gerada) estiver aberto
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

        // 2. Se modal de identificação rápida estiver aberto
        if (vm.ModalIdentificacaoAberto)
        {
            if (e.Key == Key.Escape)
            {
                vm.CancelarIdentificacaoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.F12)
            {
                await vm.ConfirmarEnvioAoCaixaCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }

            return; // Deixa o usuário digitar o nome / CPF normalmente
        }

        // === ATALHOS NA TELA PRINCIPAL DO BALCÃO ===

        // F1 ou F3: Trocar de vendedor rapidamente
        if (e.Key == Key.F1 || e.Key == Key.F3)
        {
            Log.Information("[BALCÃO ATALHO] F1/F3 detectado -> Alternando Vendedor");
            vm.TrocarVendedorProximoCommand.Execute(null);
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

        // F8 ou Delete: Remover item selecionado no carrinho
        if (e.Key == Key.F8 || e.Key == Key.Delete)
        {
            vm.CancelarItemSelecionadoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F12: Solicitar envio ao Caixa (abre identificação rápida)
        if (e.Key == Key.F12)
        {
            if (vm.Carrinho.Count > 0)
            {
                vm.SolicitarEnvioAoCaixaCommand.Execute(null);
                FocarIdentificacaoCliente();
            }
            e.Handled = true;
            return;
        }

        // ESC: Limpar campo de busca ou carrinho
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

        // SETA PARA BAIXO / CIMA nas sugestões flutuantes
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
