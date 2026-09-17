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

    // === CONTROLE DE NAVEGAÇÃO POR ABAS ===
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAbaPagar))]
    [NotifyPropertyChangedFor(nameof(IsAbaReceber))]
    [NotifyPropertyChangedFor(nameof(IsAbaFluxoCaixa))]
    [NotifyPropertyChangedFor(nameof(CorAbaPagar))]
    [NotifyPropertyChangedFor(nameof(CorAbaReceber))]
    [NotifyPropertyChangedFor(nameof(CorAbaFluxoCaixa))]
    public partial int AbaSelecionadaIndice { get; set; } = 0; // 0 = Pagar, 1 = Receber / Crediário, 2 = Fluxo de Caixa

    public bool IsAbaPagar => AbaSelecionadaIndice == 0;
    public bool IsAbaReceber => AbaSelecionadaIndice == 1;
    public bool IsAbaFluxoCaixa => AbaSelecionadaIndice == 2;

    public string CorAbaPagar => IsAbaPagar ? "#EBF5FB" : "Transparent";
    public string CorAbaReceber => IsAbaReceber ? "#E8F8F5" : "Transparent";
    public string CorAbaFluxoCaixa => IsAbaFluxoCaixa ? "#FEF9E7" : "Transparent";

    [RelayCommand]
    public void SelecionarAba(string aba)
    {
        if (int.TryParse(aba, out int indice))
        {
            AbaSelecionadaIndice = indice;
        }
    }

    // === MENSAGEM DE FEEDBACK GLOBAL ===
    [ObservableProperty]
    public partial string MensagemFeedback { get; set; } = string.Empty;

    // =========================================================================
    // 1. CONTAS A PAGAR
    // =========================================================================
    public ObservableCollection<ContaPagar> Titulos { get; } = [];
    public ObservableCollection<string> FiltrosStatus { get; } = ["TODOS", "PENDENTE", "VENCIDOS", "PAGO"];

    [ObservableProperty]
    public partial string FiltroStatusSelecionado { get; set; } = "TODOS";

    [ObservableProperty]
    public partial ResumoFinanceiroDto Resumo { get; set; } = new();

    // Modal de Pagamento / Liquidação
    [ObservableProperty] public partial bool IsModalLiquidarAberto { get; set; }
    [ObservableProperty] public partial ContaPagar? TituloSelecionadoParaLiquidar { get; set; }
    [ObservableProperty] public partial decimal ValorPagoInformado { get; set; }
    [ObservableProperty] public partial string FormaPagamentoSelecionada { get; set; } = "Boleto";
    public ObservableCollection<string> FormasPagamento { get; } = ["Boleto", "PIX", "Transferência", "Dinheiro", "Débito"];
    [ObservableProperty] public partial string ObservacaoPagamento { get; set; } = string.Empty;

    // Modal de Novo Título a Pagar Manual
    [ObservableProperty] public partial bool IsModalNovoTituloAberto { get; set; }
    [ObservableProperty] public partial string NovoFornecedorNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoFornecedorCnpj { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoNumeroDocumento { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoNumeroParcela { get; set; } = "1/1";
    [ObservableProperty] public partial decimal NovoValor { get; set; } = 0m;
    [ObservableProperty] public partial DateTime? NovoDataVencimento { get; set; } = DateTime.Today.AddDays(30);
    [ObservableProperty] public partial string NovaObservacao { get; set; } = string.Empty;

    // =========================================================================
    // 2. CONTAS A RECEBER & CREDIÁRIO ("FIADO")
    // =========================================================================
    public ObservableCollection<ContaReceber> TitulosReceber { get; } = [];
    public ObservableCollection<string> FiltrosStatusReceber { get; } = ["TODOS", "PENDENTE", "VENCIDOS", "RECEBIDO"];

    [ObservableProperty] public partial string FiltroStatusReceberSelecionado { get; set; } = "TODOS";
    [ObservableProperty] public partial string BuscaClienteReceber { get; set; } = string.Empty;

    // Modal de Baixa de Crediário (Recebimento)
    [ObservableProperty] public partial bool IsModalReceberAberto { get; set; }
    [ObservableProperty] public partial ContaReceber? TituloSelecionadoParaReceber { get; set; }
    [ObservableProperty] public partial decimal ValorRecebimentoOriginal { get; set; }
    [ObservableProperty] public partial decimal JurosRecebimento { get; set; }
    [ObservableProperty] public partial decimal DescontoRecebimento { get; set; }
    [ObservableProperty] public partial decimal ValorFinalRecebimento { get; set; }
    [ObservableProperty] public partial string FormaRecebimentoSelecionada { get; set; } = "Dinheiro";
    public ObservableCollection<string> FormasRecebimento { get; } = ["Dinheiro", "PIX", "Débito", "Crédito", "Transferência"];
    [ObservableProperty] public partial string ObservacaoRecebimento { get; set; } = string.Empty;

    // Modal de Novo Crediário Manual
    [ObservableProperty] public partial bool IsModalNovoCrediarioAberto { get; set; }
    [ObservableProperty] public partial string NovoClienteCrediarioNome { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoClienteCrediarioCpf { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoClienteCrediarioTelefone { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovoDocumentoReceber { get; set; } = "CREDIARIO";
    [ObservableProperty] public partial string NovoNumeroParcelaReceber { get; set; } = "1/1";
    [ObservableProperty] public partial decimal NovoValorReceber { get; set; } = 0m;
    [ObservableProperty] public partial DateTime? NovoVencimentoReceber { get; set; } = DateTime.Today.AddDays(30);
    [ObservableProperty] public partial string NovaObservacaoReceber { get; set; } = string.Empty;

    // =========================================================================
    // 3. FLUXO DE CAIXA CONSOLIDADO
    // =========================================================================
    [ObservableProperty]
    public partial ResumoFluxoCaixaDto FluxoCaixa { get; set; } = new();

    public FinanceiroViewModel(PdvService service, ILogger<FinanceiroViewModel> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        await RecarregarTudoAsync();
    }

    [RelayCommand]
    public async Task RecarregarTudoAsync()
    {
        await CarregarTitulosAsync();
        await CarregarTitulosReceberAsync();
        await CarregarFluxoCaixaAsync();
    }

    partial void OnFiltroStatusSelecionadoChanged(string value) => _ = CarregarTitulosAsync();
    partial void OnFiltroStatusReceberSelecionadoChanged(string value) => _ = CarregarTitulosReceberAsync();
    partial void OnBuscaClienteReceberChanged(string value) => _ = CarregarTitulosReceberAsync();

    // =========================================================================
    // MÉTODOS DE CONTAS A PAGAR
    // =========================================================================
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
            _logger.LogError(ex, "Erro ao carregar títulos do contas a pagar.");
            MensagemFeedback = $"❌ Erro ao buscar títulos: {ex.Message}";
        }
    }

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

            MensagemFeedback = $"✅ Título #{TituloSelecionadoParaLiquidar.Id} liquidado com sucesso via {FormaPagamentoSelecionada}!";
            FecharModalLiquidar();
            await RecarregarTudoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liquidar título #{Id}", TituloSelecionadoParaLiquidar.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

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
            await RecarregarTudoAsync();
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
            await RecarregarTudoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar título #{Id}", conta.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    // =========================================================================
    // MÉTODOS DE CONTAS A RECEBER & CREDIÁRIO
    // =========================================================================
    [RelayCommand]
    public async Task CarregarTitulosReceberAsync()
    {
        try
        {
            TitulosReceber.Clear();
            var lista = await _service.ObterContasReceberAsync(
                statusFiltro: FiltroStatusReceberSelecionado,
                termoBusca: BuscaClienteReceber);

            foreach (var r in lista) TitulosReceber.Add(r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar títulos a receber.");
            MensagemFeedback = $"❌ Erro ao buscar títulos a receber: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AbrirModalReceber(ContaReceber conta)
    {
        if (conta.Status != "PENDENTE") return;

        TituloSelecionadoParaReceber = conta;
        ValorRecebimentoOriginal = conta.ValorOriginal;
        JurosRecebimento = 0m;
        DescontoRecebimento = 0m;
        AtualizarValorFinalRecebimento();
        FormaRecebimentoSelecionada = "Dinheiro";
        ObservacaoRecebimento = string.Empty;
        IsModalReceberAberto = true;
    }

    partial void OnJurosRecebimentoChanged(decimal value) => AtualizarValorFinalRecebimento();
    partial void OnDescontoRecebimentoChanged(decimal value) => AtualizarValorFinalRecebimento();

    private void AtualizarValorFinalRecebimento()
    {
        ValorFinalRecebimento = Math.Max(0, ValorRecebimentoOriginal + JurosRecebimento - DescontoRecebimento);
    }

    [RelayCommand]
    public void FecharModalReceber()
    {
        IsModalReceberAberto = false;
        TituloSelecionadoParaReceber = null;
    }

    [RelayCommand]
    public async Task ConfirmarRecebimentoAsync()
    {
        if (TituloSelecionadoParaReceber == null) return;

        if (ValorFinalRecebimento <= 0)
        {
            MensagemFeedback = "⚠️ O valor do recebimento deve ser maior que zero!";
            return;
        }

        try
        {
            await _service.LiquidarContaReceberAsync(
                TituloSelecionadoParaReceber.Id,
                ValorFinalRecebimento,
                JurosRecebimento,
                DescontoRecebimento,
                FormaRecebimentoSelecionada,
                ObservacaoRecebimento);

            MensagemFeedback = $"✅ Recebimento de R$ {ValorFinalRecebimento:N2} do cliente '{TituloSelecionadoParaReceber.ClienteNome}' registrado com sucesso!";
            FecharModalReceber();
            await RecarregarTudoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liquidar recebimento #{Id}", TituloSelecionadoParaReceber.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AbrirModalNovoCrediario()
    {
        NovoClienteCrediarioNome = string.Empty;
        NovoClienteCrediarioCpf = string.Empty;
        NovoClienteCrediarioTelefone = string.Empty;
        NovoDocumentoReceber = "CREDIARIO";
        NovoNumeroParcelaReceber = "1/1";
        NovoValorReceber = 0m;
        NovoVencimentoReceber = DateTime.Today.AddDays(30);
        NovaObservacaoReceber = string.Empty;
        IsModalNovoCrediarioAberto = true;
    }

    [RelayCommand]
    public void FecharModalNovoCrediario()
    {
        IsModalNovoCrediarioAberto = false;
    }

    [RelayCommand]
    public async Task SalvarNovoCrediarioAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoClienteCrediarioNome))
        {
            MensagemFeedback = "⚠️ Informe o nome do cliente para o crediário!";
            return;
        }

        if (NovoValorReceber <= 0)
        {
            MensagemFeedback = "⚠️ O valor a receber deve ser maior que zero!";
            return;
        }

        try
        {
            await _service.RegistrarContaReceberManualAsync(
                NovoClienteCrediarioNome,
                NovoClienteCrediarioCpf,
                NovoClienteCrediarioTelefone,
                NovoDocumentoReceber,
                NovoNumeroParcelaReceber,
                NovoValorReceber,
                NovoVencimentoReceber ?? DateTime.Today.AddDays(30),
                NovaObservacaoReceber);

            MensagemFeedback = $"✅ Título de crediário para '{NovoClienteCrediarioNome}' registrado com sucesso!";
            FecharModalNovoCrediario();
            await RecarregarTudoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar crediário manual.");
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CancelarTituloReceberAsync(ContaReceber conta)
    {
        if (conta == null || conta.Status != "PENDENTE") return;

        try
        {
            await _service.CancelarContaReceberAsync(conta.Id, "Cancelado manualmente pelo operador financeiro");
            MensagemFeedback = $"⚠️ Título a receber #{conta.Id} cancelado.";
            await RecarregarTudoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar título a receber #{Id}", conta.Id);
            MensagemFeedback = $"❌ Erro: {ex.Message}";
        }
    }

    // =========================================================================
    // FLUXO DE CAIXA
    // =========================================================================
    [RelayCommand]
    public async Task CarregarFluxoCaixaAsync()
    {
        try
        {
            FluxoCaixa = await _service.ObterResumoFluxoCaixaAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao calcular fluxo de caixa.");
        }
    }
}
