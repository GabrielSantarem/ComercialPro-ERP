using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GetStartedApp.Models;
using GetStartedApp.Services;
using Microsoft.Extensions.Logging;

namespace GetStartedApp.ViewModels;

public partial class FinanceiroViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly ILogger<FinanceiroViewModel> _logger;

    public ObservableCollection<ContaPagar> Titulos { get; } = [];
    public ObservableCollection<string> FiltrosStatus { get; } = ["TODOS", "PENDENTE", "VENCIDOS", "PAGO"];

    [ObservableProperty]
    public partial string FiltroStatusSelecionado { get; set; } = "TODOS";

    [ObservableProperty]
    public partial ResumoFinanceiroDto Resumo { get; set; } = new();

    [ObservableProperty]
    public partial string MensagemFeedback { get; set; } = string.Empty;

    // === CONTROLE DO MODAL DE LIQUIDAÇÃO (PAGAMENTO) ===
    [ObservableProperty]
    public partial bool IsModalLiquidarAberto { get; set; }

    [ObservableProperty]
    public partial ContaPagar? TituloSelecionadoParaLiquidar { get; set; }

    [ObservableProperty]
    public partial decimal ValorPagoInformado { get; set; }

    [ObservableProperty]
    public partial string FormaPagamentoSelecionada { get; set; } = "Boleto";
    public ObservableCollection<string> FormasPagamento { get; } = ["Boleto", "PIX", "Transferência", "Dinheiro", "Débito"];

    [ObservableProperty]
    public partial string ObservacaoPagamento { get; set; } = string.Empty;

    // === CONTROLE DO MODAL DE NOVO TÍTULO AVULSO ===
    [ObservableProperty]
    public partial bool IsModalNovoTituloAberto { get; set; }

    [ObservableProperty] public partial string NovoFornecedorNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoFornecedorCnpj { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoNumeroDocumento { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoNumeroParcela { get; set; } = "1/1";
    [ObservableProperty] public partial decimal NovoValor { get; set; } = 0m;
    [ObservableProperty] public partial DateTime? NovoDataVencimento { get; set; } = DateTime.Today.AddDays(30);
    [ObservableProperty] public partial string NovaObservacao { get; set; } = string.Empty;

    public FinanceiroViewModel(PdvService service, ILogger<FinanceiroViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        await CarregarTitulosAsync();
    }

    partial void OnFiltroStatusSelecionadoChanged(string value)
    {
        _ = CarregarTitulosAsync();
    }

    [RelayCommand]
    public async Task CarregarTitulosAsync()
    {
        try
        {
            Titulos.Clear();
            var lista = await _service.ObterContasPagarAsync(statusFiltro: FiltroStatusSelecionado);
            foreach (var t in lista) Titulos.Add(t);

            Resumo = await _service.ObterResumoFinanceiroContasPagarAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar títulos do financeiro.");
            MensagemFeedback = $"❌ Erro ao buscar títulos: {ex.Message}";
        }
    }

    // === MODAL DE LIQUIDAÇÃO / PAGAMENTO ===
    [RelayCommand]
    public void AbrirModalLiquidar(ContaPagar conta)
    {
        if (conta.Status != "PENDENTE") return;

        TituloSelecionadoParaLiquidar = conta;
        ValorPagoInformado = conta.Valor;
        FormaPagamentoSelecionada = "Boleto";
        ObservacaoPagamento = string.Empty;
        IsModalLiquidarAberto = true;
    }

    [RelayCommand]
    public void FecharModalLiquidar()
    {
        IsModalLiquidarAberto = false;
        TituloSelecionadoParaLiquidar = null;
    }

    [RelayCommand]
    public async Task ConfirmarLiquidacaoAsync()
    {
        if (TituloSelecionadoParaLiquidar == null) return;

        if (ValorPagoInformado <= 0)
        {
            MensagemFeedback = "⚠️ O valor pago deve ser maior que zero!";
            return;
        }

        try
        {
            await _service.LiquidarContaPagarAsync(
                TituloSelecionadoParaLiquidar.Id,
                ValorPagoInformado,
                FormaPagamentoSelecionada,
                ObservacaoPagamento);

            MensagemFeedback = $"✅ Título #{TituloSelecionadoParaLiquidar.Id} pago com sucesso via {FormaPagamentoSelecionada}!";
            FecharModalLiquidar();
            await CarregarTitulosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liquidar título #{Id}", TituloSelecionadoParaLiquidar.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    // === MODAL DE NOVO TÍTULO AVULSO ===
    [RelayCommand]
    public void AbrirModalNovoTitulo()
    {
        NovoFornecedorNome = string.Empty;
        NovoFornecedorCnpj = string.Empty;
        NovoNumeroDocumento = string.Empty;
        NovoNumeroParcela = "1/1";
        NovoValor = 0m;
        NovoDataVencimento = DateTime.Today.AddDays(30);
        NovaObservacao = string.Empty;
        IsModalNovoTituloAberto = true;
    }

    [RelayCommand]
    public void FecharModalNovoTitulo()
    {
        IsModalNovoTituloAberto = false;
    }

    [RelayCommand]
    public async Task SalvarNovoTituloAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoFornecedorNome))
        {
            MensagemFeedback = "⚠️ Informe a razão social ou nome do fornecedor!";
            return;
        }

        if (NovoValor <= 0)
        {
            MensagemFeedback = "⚠️ O valor do título deve ser maior que zero!";
            return;
        }

        try
        {
            await _service.RegistrarContaPagarManualAsync(
                NovoFornecedorNome,
                NovoFornecedorCnpj,
                NovoNumeroDocumento,
                NovoNumeroParcela,
                NovoValor,
                NovoDataVencimento ?? DateTime.Today.AddDays(30),
                NovaObservacao);

            MensagemFeedback = "✅ Título a pagar cadastrado com sucesso!";
            FecharModalNovoTitulo();
            await CarregarTitulosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar título avulso.");
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CancelarTituloAsync(ContaPagar conta)
    {
        if (conta == null || conta.Status != "PENDENTE") return;

        try
        {
            await _service.CancelarContaPagarAsync(conta.Id, "Cancelado manualmente pelo operador financeiro");
            MensagemFeedback = $"⚠️ Título #{conta.Id} cancelado.";
            await CarregarTitulosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar título #{Id}", conta.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }
}
