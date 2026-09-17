using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class PdvViewModel
{
    // === FILA DE PRÉ-VENDAS DO BALCÃO ===
    public ObservableCollection<PedidoBalcao> FilaPedidos { get; } = [];
    public ObservableCollection<PedidoBalcao> FilaFiltrada { get; } = [];

    [ObservableProperty]
    public partial PedidoBalcao? PedidoFilaSelecionado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalFilaBalcaoAberto { get; set; }

    [ObservableProperty]
    public partial string FiltroFilaBalcao { get; set; } = string.Empty;

    partial void OnFiltroFilaBalcaoChanged(string value)
    {
        AplicarFiltroFila();
    }

    private void AplicarFiltroFila()
    {
        FilaFiltrada.Clear();
        var termo = FiltroFilaBalcao?.Trim().ToLowerInvariant() ?? string.Empty;

        var filtrados = string.IsNullOrWhiteSpace(termo)
            ? FilaPedidos
            : FilaPedidos.Where(p => 
                p.NumeroComanda.ToLowerInvariant().Contains(termo) ||
                p.ClienteNome.ToLowerInvariant().Contains(termo) ||
                p.Vendedor.Nome.ToLowerInvariant().Contains(termo));

        foreach (var p in filtrados) FilaFiltrada.Add(p);

        PedidoFilaSelecionado = FilaFiltrada.FirstOrDefault();
    }

    [RelayCommand]
    public async Task AtualizarFilaPedidosAsync()
    {
        FilaPedidos.Clear();
        var lista = await _pdvService.ObterPedidosAguardandoPagamentoAsync();
        foreach (var p in lista) FilaPedidos.Add(p);
        AplicarFiltroFila();
    }

    [RelayCommand]
    public async Task AbrirModalFilaBalcaoAsync()
    {
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ Abra o caixa antes de atender a fila do balcão!";
            return;
        }

        await AtualizarFilaPedidosAsync();
        FiltroFilaBalcao = string.Empty;
        AplicarFiltroFila();
        ModalFilaBalcaoAberto = true;
    }

    [RelayCommand]
    public void FecharModalFilaBalcao()
    {
        ModalFilaBalcaoAberto = false;
        FiltroFilaBalcao = string.Empty;
    }

    [RelayCommand]
    public void ConfirmarSelecaoFila()
    {
        if (PedidoFilaSelecionado != null)
        {
            var p = PedidoFilaSelecionado;
            ModalFilaBalcaoAberto = false;
            PuxarPedidoFila(p);
        }
    }

    [RelayCommand]
    public void PuxarPedidoFila(PedidoBalcao pedido)
    {
        if (pedido == null) return;
        if (!IsCaixaAberto)
        {
            AbrirModalAberturaCaixa();
            MensagemCaixaErro = "⚠️ Abra o caixa antes de receber pedidos da fila!";
            return;
        }

        _logger.LogInformation("Puxando pedido {Comanda} da fila para recebimento no caixa...", pedido.NumeroComanda);
        PedidoBalcaoEmAtendimento = pedido;
        ClienteIdentificacao = $"{pedido.ClienteNome} ({pedido.NumeroComanda})";

        Carrinho.Clear();
        foreach (var item in pedido.Itens)
        {
            Carrinho.Add(new ProdutoItem
            {
                Produto = item.Produto,
                Quantidade = item.Quantidade
            });
        }

        AtualizarTotal();
        AbrirModalPagamento();
    }

    [RelayCommand]
    public async Task CancelarPedidoFilaAsync(PedidoBalcao pedido)
    {
        if (pedido == null) return;
        await _pdvService.CancelarPedidoBalcaoAsync(pedido.Id, "Cancelado no Caixa");
        await AtualizarFilaPedidosAsync();
    }

    // === CONTROLE DE TURNOS DE CAIXA (ABERTURA, SANGRIA, FECHAMENTO) ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial CaixaTurno? TurnoAtual { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusCaixaTexto))]
    [NotifyPropertyChangedFor(nameof(StatusCaixaCor))]
    [NotifyPropertyChangedFor(nameof(SaldoCaixaDinheiroTexto))]
    public partial bool IsCaixaAberto { get; set; }

    public string StatusCaixaTexto => IsCaixaAberto ? $"🟢 CAIXA ABERTO (TURNO #{TurnoAtual?.Id})" : "🔴 CAIXA FECHADO";
    public string StatusCaixaCor => IsCaixaAberto ? "#27AE60" : "#C0392B";
    public string SaldoCaixaDinheiroTexto => IsCaixaAberto ? $"Gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}" : "Abra o Caixa";

    // === MODAL DE OPERAÇÕES DE CAIXA ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBloqueadoPorModal))]
    public partial bool ModalCaixaAberto { get; set; }

    [ObservableProperty] public partial string TipoModalCaixa { get; set; } = "ABERTURA";
    [ObservableProperty] public partial string TituloModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string DescricaoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial decimal ValorModalCaixa { get; set; }
    [ObservableProperty] public partial string MotivoModalCaixa { get; set; } = string.Empty;
    [ObservableProperty] public partial string MensagemCaixaErro { get; set; } = string.Empty;

    public async Task AtualizarEstadoTurnoAsync()
    {
        TurnoAtual = await _pdvService.ObterTurnoAtualAsync();
        IsCaixaAberto = TurnoAtual != null;
        OnPropertyChanged(nameof(TurnoAtual));
        OnPropertyChanged(nameof(IsCaixaAberto));
        OnPropertyChanged(nameof(StatusCaixaTexto));
        OnPropertyChanged(nameof(StatusCaixaCor));
        OnPropertyChanged(nameof(SaldoCaixaDinheiroTexto));
    }

    [RelayCommand]
    public void AbrirModalAberturaCaixa()
    {
        TipoModalCaixa = "ABERTURA";
        TituloModalCaixa = "🟢 ABERTURA DE TURNO DE CAIXA";
        DescricaoModalCaixa = "Informe o fundo de troco inicial em dinheiro colocado na gaveta:";
        ValorModalCaixa = 100.00m;
        MotivoModalCaixa = "Fundo de troco inicial";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSuprimento()
    {
        TipoModalCaixa = "SUPRIMENTO";
        TituloModalCaixa = "➕ SUPRIMENTO DE CAIXA (ENTRADA DE TROCO)";
        DescricaoModalCaixa = "Informe o valor em dinheiro que está entrando na gaveta:";
        ValorModalCaixa = 50.00m;
        MotivoModalCaixa = "Troco extra em moedas/cédulas";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalSangria()
    {
        TipoModalCaixa = "SANGRIA";
        TituloModalCaixa = "➖ SANGRIA DE CAIXA (RETIRADA PARA COFRE)";
        DescricaoModalCaixa = $"Saldo disponível na gaveta: R$ {TurnoAtual?.SaldoEsperadoEmDinheiro:N2}. Digite o valor a retirar:";
        ValorModalCaixa = Math.Min(100.00m, TurnoAtual?.SaldoEsperadoEmDinheiro ?? 0m);
        MotivoModalCaixa = "Recolhimento para o cofre pelo gerente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void AbrirModalFechamentoCaixa()
    {
        TipoModalCaixa = "FECHAMENTO";
        TituloModalCaixa = "🔒 FECHAMENTO CEGO DE TURNO";
        DescricaoModalCaixa = "Conte o dinheiro físico presente na gaveta e informe o valor total apurado:";
        ValorModalCaixa = 0m;
        MotivoModalCaixa = "Fechamento de expediente";
        MensagemCaixaErro = string.Empty;
        ModalCaixaAberto = true;
    }

    [RelayCommand]
    public void FecharModalCaixa()
    {
        ModalCaixaAberto = false;
        MensagemCaixaErro = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmarAcaoCaixaAsync()
    {
        try
        {
            MensagemCaixaErro = string.Empty;
            var vendedorId = VendedorSelecionado?.Id ?? 1;

            switch (TipoModalCaixa)
            {
                case "ABERTURA":
                    await _pdvService.AbrirCaixaAsync(vendedorId, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SUPRIMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSuprimentoAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "SANGRIA":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    await _pdvService.RegistrarSangriaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    break;

                case "FECHAMENTO":
                    if (TurnoAtual == null) throw new InvalidOperationException("Nenhum turno aberto.");
                    var turnoFechado = await _pdvService.FecharCaixaAsync(TurnoAtual.Id, ValorModalCaixa, MotivoModalCaixa);
                    _logger.LogInformation("Fechamento concluído. Quebra: R$ {Quebra}", turnoFechado.DiferencaQuebra);
                    break;
            }

            await AtualizarEstadoTurnoAsync();
            ModalCaixaAberto = false;
        }
        catch (Exception ex)
        {
            MensagemCaixaErro = $"❌ {ex.Message}";
            _logger.LogWarning(ex, "Erro na ação de caixa ({Tipo})", TipoModalCaixa);
        }
    }
}
