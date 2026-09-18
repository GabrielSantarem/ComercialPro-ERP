using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Models.Fiscal;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel
{
    // === CONTROLE DE MODAL DE PAGAMENTO ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool IsModalAberto { get; set; }

    [ObservableProperty]
    public partial string ClienteIdentificacao { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PedidoBalcao? PedidoBalcaoEmAtendimento { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Acrescimo))]
    [NotifyPropertyChangedFor(nameof(TotalComTaxa))]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    public partial string FormaPagamentoSelecionada { get; set; } = "Dinheiro";
    public ObservableCollection<string> FormasPagamento { get; } = ["Dinheiro", "PIX", "Débito", "Crédito (+2%)"];

    public decimal Acrescimo => FormaPagamentoSelecionada == "Crédito (+2%)" ? TotalVenda * 0.02m : 0m;
    public decimal TotalComTaxa => TotalVenda + Acrescimo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Troco))]
    [NotifyPropertyChangedFor(nameof(PodeConfirmarPagamento))]
    private decimal _valorRecebido;

    public decimal Troco => ValorRecebido > TotalComTaxa ? ValorRecebido - TotalComTaxa : 0m;
    public bool PodeConfirmarPagamento => ValorRecebido >= TotalComTaxa && TotalComTaxa > 0;

    // === CONTROLE FISCAL NFC-e (ZEUS) ===
    [ObservableProperty]
    public partial bool EmitirNfceAoFinalizar { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalNfceEmitidaAberto { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceDanfeTexto { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceChaveAcesso { get; set; }

    [ObservableProperty]
    public partial string? UltimaNfceMensagemStatus { get; set; }

    [RelayCommand]
    public void AbrirModalPagamento()
    {
        if (Carrinho.Count == 0)
        {
            _logger.LogWarning("Tentativa de fechar nota com carrinho vazio.");
            return;
        }

        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ Abra o caixa antes de finalizar qualquer venda!";
            return;
        }

        _logger.LogInformation("Abrindo modal de pagamento. Total: R$ {Total}", TotalVenda);
        IsModalAberto = true;
        ValorRecebido = TotalComTaxa;
    }

    [RelayCommand]
    private void FecharModalPagamento()
    {
        _logger.LogInformation("Fechando modal de pagamento (cancelado pelo usuário via ESC/Botão).");
        IsModalAberto = false;
        PedidoBalcaoEmAtendimento = null;
    }

    [RelayCommand]
    public void FecharModalNfceEmitida()
    {
        ModalNfceEmitidaAberto = false;
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;

        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}, Cliente: {Cliente}", Carrinho.Count, ClienteIdentificacao);

        // Copiar itens para emissão fiscal
        var itensParaNfce = Carrinho.Select((i, idx) => new ItemEmissaoNfceDto
        {
            ItemNumero = idx + 1,
            CodigoProduto = i.Produto.Id.ToString(),
            CodigoBarrasEan = string.IsNullOrWhiteSpace(i.Produto.CodigoBarras) ? "SEM GTIN" : i.Produto.CodigoBarras,
            DescricaoProduto = i.Produto.Nome,
            Ncm = "22021000",
            Cfop = 5102,
            UnidadeComercial = "UN",
            Quantidade = i.Quantidade,
            ValorUnitario = i.Produto.Preco,
            Csosn = "102",
            AliquotaTributosAproximadosPercentual = 15.00m
        }).ToList();

        var formaCod = FormaPagamentoSelecionada switch
        {
            "Dinheiro" => "01",
            "PIX" => "17",
            "Débito" => "04",
            "Crédito (+2%)" => "03",
            _ => "99"
        };

        var pagamentosNfce = new System.Collections.Generic.List<PagamentoEmissaoNfceDto>
        {
            new PagamentoEmissaoNfceDto { MeioPagamento = formaCod, Valor = ValorRecebido }
        };

        if (PedidoBalcaoEmAtendimento != null)
        {
            // Fatura o pedido que veio da fila do balcão
            await _pdvService.FaturarPedidoBalcaoNoCaixaAsync(PedidoBalcaoEmAtendimento.Id, FormaPagamentoSelecionada);
            PedidoBalcaoEmAtendimento = null;
        }
        else
        {
            // Venda direta lançada pelo caixa
            var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();
            var vendedorId = VendedorSelecionado?.Id ?? 1;
            await _pdvService.SalvarPedidoAsync(vendedorId, itens, FormaPagamentoSelecionada);
        }

        // Emissão Fiscal Automática NFC-e se estiver ativo
        if (EmitirNfceAoFinalizar && _nfceService != null && _fiscalConfig != null)
        {
            var dadosNfce = new DadosEmissaoNfce
            {
                CpfConsumidor = string.IsNullOrWhiteSpace(ClienteIdentificacao) ? null : ClienteIdentificacao,
                NomeConsumidor = string.IsNullOrWhiteSpace(ClienteIdentificacao) ? null : "CLIENTE BALCAO",
                Itens = itensParaNfce,
                Pagamentos = pagamentosNfce,
                ValorTroco = Troco,
                ModoContingenciaOffline = false
            };

            var retornoNfce = _nfceService.EmitirNfce(dadosNfce, _fiscalConfig);
            if (retornoNfce.Sucesso)
            {
                UltimaNfceDanfeTexto = retornoNfce.DanfeTextoTermica;
                UltimaNfceChaveAcesso = retornoNfce.ChaveAcesso;
                UltimaNfceMensagemStatus = $"✅ NFC-e Nº {retornoNfce.NumeroNota:D6} emitida e assinada com sucesso!";
                ModalNfceEmitidaAberto = true;
                _logger.LogInformation("NFC-e emitida com sucesso. Chave: {Chave}", retornoNfce.ChaveAcesso);
            }
            else
            {
                UltimaNfceMensagemStatus = $"⚠️ Erro na emissão fiscal: {retornoNfce.Mensagem}";
                _logger.LogWarning("Falha ao emitir NFC-e: {Msg}", retornoNfce.Mensagem);
            }
        }

        Carrinho.Clear();
        TextoPesquisa = string.Empty;
        ClienteIdentificacao = string.Empty;
        ResultadosPesquisa.Clear();
        AtualizarTotal();
        IsModalAberto = false;
        await AtualizarEstadoTurnoAsync();
        await AtualizarFilaPedidosAsync();
        _logger.LogInformation("Venda processada com sucesso. Modal fechado e fila atualizada.");
    }
}
