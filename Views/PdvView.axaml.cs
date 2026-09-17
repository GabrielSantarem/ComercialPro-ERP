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
        Dispatcher.UIThread.Post(() =>
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            txt?.Focus();
        });
    }

    private void FocarModalPagamento()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var numValor = this.FindControl<NumericUpDown>("NumValorRecebido");
            numValor?.Focus();
        });
    }

    private void FocarFiltroFila()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var txt = this.FindControl<TextBox>("TxtFiltroFila");
            txt?.Focus();
        });
    }

    private async void PdvView_KeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (this.DataContext is not PdvViewModel vm) return;

        // ==========================================
        // 1. SE O MODAL DE OPERAÇÕES DE CAIXA ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalCaixaAberto)
        {
            if (e.Key == Key.Escape)
            {
                vm.FecharModalCaixaCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                await vm.ConfirmarAcaoCaixaCommand.ExecuteAsync(null);
                if (!vm.ModalCaixaAberto) FocarBusca();
                e.Handled = true;
                return;
            }

            return;
        }

        // ==========================================
        // 2. SE O MODAL DA FILA DO BALCÃO [F4] ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalFilaBalcaoAberto)
        {
            if (e.Key == Key.Escape)
            {
                vm.FecharModalFilaBalcaoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            // SETA PARA BAIXO / CIMA navega entre as comandas na fila
            if (e.Key == Key.Down && vm.FilaFiltrada.Count > 0)
            {
                var lista = vm.FilaFiltrada.ToList();
                var atualIdx = vm.PedidoFilaSelecionado != null ? lista.IndexOf(vm.PedidoFilaSelecionado) : -1;
                var proximoIdx = Math.Min(atualIdx + 1, lista.Count - 1);
                vm.PedidoFilaSelecionado = lista[proximoIdx];
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up && vm.FilaFiltrada.Count > 0)
            {
                var lista = vm.FilaFiltrada.ToList();
                var atualIdx = vm.PedidoFilaSelecionado != null ? lista.IndexOf(vm.PedidoFilaSelecionado) : 0;
                var anteriorIdx = Math.Max(atualIdx - 1, 0);
                vm.PedidoFilaSelecionado = lista[anteriorIdx];
                e.Handled = true;
                return;
            }

            // ENTER: Confirma a comanda selecionada e puxa para pagamento
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                vm.ConfirmarSelecaoFilaCommand.Execute(null);
                FocarModalPagamento();
                e.Handled = true;
                return;
            }

            return;
        }

        // ==========================================
        // 3. SE O MODAL DE PAGAMENTO ESTIVER ABERTO:
        // ==========================================
        if (vm.IsModalAberto)
        {
            // ESC: Fecha o modal e volta para a venda intacta
            if (e.Key == Key.Escape)
            {
                Log.Information("[PDV MODAL] ESC pressionado -> Fechando modal e restaurando foco na venda");
                vm.FecharModalPagamentoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            // ENTER: Confirma pagamento se valor foi atingido
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (vm.PodeConfirmarPagamento)
                {
                    Log.Information("[PDV MODAL] ENTER de confirmação -> Concluindo venda");
                    await vm.ConfirmarPagamentoCommand.ExecuteAsync(null);
                    FocarBusca();
                    e.Handled = true;
                }
                return;
            }

            return;
        }

        // ==========================================
        // 4. ATALHOS NA TELA PRINCIPAL DO PDV (BOCA DE CAIXA):
        // ==========================================

        // F4: Abre o modal de Fila do Balcão
        if (e.Key == Key.F4)
        {
            Log.Information("[PDV ATALHO] F4 -> Abrir Fila do Balcão");
            await vm.AbrirModalFilaBalcaoAsync();
            if (vm.ModalFilaBalcaoAberto) FocarFiltroFila();
            e.Handled = true;
            return;
        }

        // F12: Abre modal de pagamento / recebimento
        if (e.Key == Key.F12)
        {
            Log.Information("[PDV ATALHO] F12 -> Abrir Pagamento");
            vm.AbrirModalPagamentoCommand.Execute(null);
            FocarModalPagamento();
            e.Handled = true;
            return;
        }

        // F1: Abertura de Caixa (se fechado)
        if (e.Key == Key.F1 && !vm.IsCaixaAberto)
        {
            vm.AbrirModalAberturaCaixaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F6: Suprimento (se aberto)
        if (e.Key == Key.F6 && vm.IsCaixaAberto)
        {
            vm.AbrirModalSuprimentoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F7: Sangria (se aberto)
        if (e.Key == Key.F7 && vm.IsCaixaAberto)
        {
            vm.AbrirModalSangriaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F9: Fechamento cego de turno (se aberto)
        if (e.Key == Key.F9 && vm.IsCaixaAberto)
        {
            vm.AbrirModalFechamentoCaixaCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F2: Força o foco no campo de busca de venda direta
        if (e.Key == Key.F2)
        {
            FocarBusca();
            e.Handled = true;
            return;
        }

        // F8 ou Delete: Cancela/Remove o item selecionado do cupom
        if (e.Key == Key.F8 || e.Key == Key.Delete)
        {
            Log.Information("[PDV ATALHO] F8/DEL -> Remover Item");
            vm.CancelarItemSelecionadoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // ESC: Limpa a busca ou o cupom
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

        // SETA PARA BAIXO / CIMA: Navega na lista de sugestões de busca
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

        // ENTER: Lança o produto no cupom
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
            {
                vm.TextoPesquisa = txt.Text;
            }

            Log.Information("[PDV ATALHO] ENTER -> Lançando produto com texto '{Texto}'", vm.TextoPesquisa);
            vm.LancarProdutoCommand.Execute(null);

            if (txt != null && vm.IsCaixaAberto)
            {
                txt.Text = string.Empty;
                txt.Focus();
            }

            e.Handled = true;
        }
    }
}
