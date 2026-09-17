using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    [RelayCommand]
    private void AbrirModalPagamento()
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
    }

    [RelayCommand]
    private async Task ConfirmarPagamentoAsync()
    {
        if (!PodeConfirmarPagamento) return;

        _logger.LogInformation("Confirmando Checkout do Carrinho. Itens: {Qtd}, Cliente: {Cliente}", Carrinho.Count, ClienteIdentificacao);
        var itens = Carrinho.Select(i => (i.Produto, i.Quantidade)).ToList();

        await _pdvService.SalvarPedidoAsync(VendedorSelecionado!.Id, itens, FormaPagamentoSelecionada);

        Carrinho.Clear();
        TextoPesquisa = string.Empty;
        ClienteIdentificacao = string.Empty;
        ResultadosPesquisa.Clear();
        AtualizarTotal();
        IsModalAberto = false;
        await AtualizarEstadoTurnoAsync();
        _logger.LogInformation("Venda processada com sucesso. Modal fechado.");
    }
}
