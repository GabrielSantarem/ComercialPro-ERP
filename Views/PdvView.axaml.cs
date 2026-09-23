using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GetStartedApp.ViewModels;
using Serilog;

namespace GetStartedApp.Views;

public partial class PdvView : UserControl
{
    public PdvView()
    {
        InitializeComponent();
        this.Focusable = true;
        this.AddHandler(InputElement.KeyDownEvent, PdvView_KeyDownTunnel, RoutingStrategies.Tunnel);
        this.AttachedToVisualTree += (s, e) => FocarBusca();
    }

    private void FocarBusca()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var txt = this.FindControl<TextBox>("TxtPesquisa");
            if (txt != null && txt.IsEnabled)
            {
                txt.Focus();
            }
            else
            {
                this.Focus();
            }
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
        // -1. SE O MODAL DE TROCAS & VALE-CRÉDITO [F10] ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalTrocasAberto)
        {
            if (e.Key == Key.Escape)
            {
                Log.Information("[PDV TROCAS] ESC -> Fechando modal de trocas");
                vm.FecharModalTrocasCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (!vm.TrocaProcessando && !vm.ExibirValeEmitidoModal)
                {
                    Log.Information("[PDV TROCAS] ENTER -> Confirmando emissão de Vale-Crédito");
                    await vm.ConfirmarEmissaoValeCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                return;
            }

            return;
        }

        // ==========================================
        // 0. SE O MODAL DE CANCELAMENTO FISCAL [F7] ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalCancelamentoAberto)
        {
            if (e.Key == Key.Escape)
            {
                Log.Information("[PDV CANCELAMENTO] ESC -> Fechando modal de cancelamento");
                vm.FecharModalCancelamentoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (!vm.IsCancelandoProcessando)
                {
                    Log.Information("[PDV CANCELAMENTO] ENTER -> Confirmando cancelamento da NFC-e");
                    await vm.ConfirmarCancelamentoNfceCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                return;
            }

            return;
        }

        // ==========================================
        // 1. SE O MODAL DE NFC-E EMITIDA ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalNfceEmitidaAberto)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Return)
            {
                Log.Information("[PDV MODAL NFCE] ENTER/ESC -> Fechando modal de comprovante NFC-e");
                vm.FecharModalNfceEmitidaCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F5)
            {
                Log.Information("[PDV MODAL NFCE] F5 -> Visualizar DANFE A4 em PDF");
                vm.VisualizarDanfeA4PdfCommand.Execute(null);
                e.Handled = true;
                return;
            }

            return;
        }

        // ==========================================
        // 2. SE O MODAL DE OPERAÇÕES DE CAIXA ESTIVER ABERTO:
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
        // 3. SE O MODAL DA FILA DO BALCÃO [F4] ESTIVER ABERTO:
        // ==========================================
        if (vm.ModalFilaBalcaoAberto)
        {
            // ESC: Fecha o modal da fila
            if (e.Key == Key.Escape)
            {
                Log.Information("[PDV MODAL FILA] ESC pressionado -> Fechando modal da fila");
                vm.FecharModalFilaBalcaoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }

            // Teclas de navegação UP / DOWN na lista de pedidos
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
        // 4. SE O MODAL DE PAGAMENTO ESTIVER ABERTO:
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

            // F3: Adicionar Parcela no Split Payment
            if (e.Key == Key.F3)
            {
                Log.Information("[PDV MODAL] F3 -> Adicionando parcela no split payment");
                vm.AdicionarParcelaPagamentoCommand.Execute(null);
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
        // 5. ATALHOS NA TELA PRINCIPAL DO PDV (BOCA DE CAIXA):
        // ==========================================

        // F3: Captura peso da balança de checkout (REV-004)
        if (e.Key == Key.F3)
        {
            Log.Information("[PDV ATALHO] F3 -> Capturar peso da balança de checkout");
            await vm.CapturarPesoBalancaCommand.ExecuteAsync(null);
            e.Handled = true;
            return;
        }

        // F4: Abre o modal de Fila do Balcão
        if (e.Key == Key.F4)
        {
            Log.Information("[PDV ATALHO] F4 -> Abrir Fila do Balcão");
            await vm.AbrirModalFilaBalcaoAsync();
            if (vm.ModalFilaBalcaoAberto) FocarFiltroFila();
            e.Handled = true;
            return;
        }

        // F10: Trocas, Devoluções & Vale-Crédito (REV-003)
        if (e.Key == Key.F10)
        {
            Log.Information("[PDV ATALHO] F10 -> Abrir Trocas & Vales");
            vm.AbrirModalTrocasCommand.Execute(null);
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

        // F7: Cancelar Última Venda / NFC-e (REV-002)
        if (e.Key == Key.F7)
        {
            Log.Information("[PDV ATALHO] F7 -> Cancelar Última Venda / NFC-e");
            await vm.AbrirModalCancelamentoAsync();
            e.Handled = true;
            return;
        }

        // F8: Sangria (se aberto)
        if (e.Key == Key.F8 && vm.IsCaixaAberto)
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

        // F2 ou Tab: Foco na barra de busca de produtos
        if (e.Key == Key.F2)
        {
            FocarBusca();
            e.Handled = true;
            return;
        }

        // DELETE: Exclui o item atualmente selecionado no carrinho
        if (e.Key == Key.Delete)
        {
            if (vm.ItemSelecionado != null)
            {
                Log.Information("[PDV ATALHO] DELETE -> Removendo item: {Item}", vm.ItemSelecionado.Produto.Nome);
                vm.RemoverItemCommand.Execute(vm.ItemSelecionado);
                FocarBusca();
                e.Handled = true;
                return;
            }
        }

        // Se pressionar ENTER no campo de busca e houver produto selecionado, insere no carrinho
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (vm.ProdutoPesquisaSelecionado != null)
            {
                vm.AdicionarItemCommand.Execute(vm.ProdutoPesquisaSelecionado);
                FocarBusca();
                e.Handled = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(vm.TextoPesquisa))
            {
                vm.LancarProdutoCommand.Execute(null);
                FocarBusca();
                e.Handled = true;
                return;
            }
        }
    }
}
