using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Impressao;

namespace GetStartedApp.ViewModels;

public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly CupomTermicoService _cupomService = new();

    public ObservableCollection<Vendedor> VendedoresLista { get; } = new();

    [ObservableProperty]
    public partial string NovoVendedorNome { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MensagemAviso { get; set; } = string.Empty;

    // === CONFIGURAÇÕES DE HARDWARE & IMPRESSÃO ===
    [ObservableProperty]
    public partial string NomeEstacao { get; set; } = "Terminal Caixa 01";

    [ObservableProperty]
    public partial string ModeloImpressora { get; set; } = "EPSON TM-T20X";

    public ObservableCollection<string> ModelosImpressoraDisponiveis { get; } = 
    [
        "EPSON TM-T20X",
        "ELGIN i9",
        "BEMATECH MP-4200",
        "DARUMA DR800",
        "GENÉRICA ESC/POS",
        "NENHUMA / SALVAR PDF"
    ];

    [ObservableProperty]
    public partial string LarguraBobina { get; set; } = "80mm";

    public ObservableCollection<string> LargurasBobinaDisponiveis { get; } = 
    [
        "80mm",
        "58mm"
    ];

    [ObservableProperty]
    public partial string PortaComunicacao { get; set; } = "USB / Spooler";

    public ObservableCollection<string> PortasComunicacaoDisponiveis { get; } = 
    [
        "USB / Spooler",
        "COM1",
        "COM2",
        "COM3",
        "REDE TCP/IP (9100)"
    ];

    [ObservableProperty]
    public partial bool CortarPapelAutomatico { get; set; } = true;

    [ObservableProperty]
    public partial string ModoImpressaoPadrao { get; set; } = "Cupom Térmico NFC-e";

    public ObservableCollection<string> ModosImpressaoDisponiveis { get; } = 
    [
        "Cupom Térmico NFC-e",
        "DANFE Simplificado A4",
        "Apenas Visualizar"
    ];

    [ObservableProperty]
    public partial bool ImprimirComandaBalcaoAutomatico { get; set; } = true;

    [ObservableProperty]
    public partial bool IntegracaoBalancaHabilitada { get; set; } = false;

    [ObservableProperty]
    public partial string ModeloBalanca { get; set; } = "Toledo Prix 3";

    public ObservableCollection<string> ModelosBalancaDisponiveis { get; } = 
    [
        "Toledo Prix 3",
        "Filizola Platina",
        "Urano Pop",
        "Elgin DP-30"
    ];

    [ObservableProperty]
    public partial string MensagemAvisoHardware { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UltimoCupomTeste { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ModalCupomTesteVisivel { get; set; } = false;

    public ConfiguracoesViewModel(PdvService service)
    {
        _service = service;
        _ = CarregarDadosIniciaisAsync();
    }

    public async Task CarregarDadosIniciaisAsync()
    {
        await CarregarVendedoresAsync();
        await CarregarConfiguracoesHardwareAsync();
    }

    [RelayCommand]
    public async Task CarregarConfiguracoesHardwareAsync()
    {
        var config = await _service.ObterConfiguracaoTerminalAsync();
        NomeEstacao = config.NomeEstacao;
        ModeloImpressora = config.ModeloImpressora;
        LarguraBobina = config.LarguraBobina;
        PortaComunicacao = config.PortaComunicacao;
        CortarPapelAutomatico = config.CortarPapelAutomatico;
        ModoImpressaoPadrao = config.ModoImpressaoPadrao;
        ImprimirComandaBalcaoAutomatico = config.ImprimirComandaBalcaoAutomatico;
        IntegracaoBalancaHabilitada = config.IntegracaoBalancaHabilitada;
        ModeloBalanca = config.ModeloBalanca;
    }

    [RelayCommand]
    public async Task SalvarConfiguracoesHardwareAsync()
    {
        var config = new ConfiguracaoTerminal
        {
            Id = 1,
            NomeEstacao = NomeEstacao,
            ModeloImpressora = ModeloImpressora,
            LarguraBobina = LarguraBobina,
            PortaComunicacao = PortaComunicacao,
            CortarPapelAutomatico = CortarPapelAutomatico,
            ModoImpressaoPadrao = ModoImpressaoPadrao,
            ImprimirComandaBalcaoAutomatico = ImprimirComandaBalcaoAutomatico,
            IntegracaoBalancaHabilitada = IntegracaoBalancaHabilitada,
            ModeloBalanca = ModeloBalanca
        };

        await _service.SalvarConfiguracaoTerminalAsync(config);
        MensagemAvisoHardware = "✅ Configurações de hardware e impressora salvas com sucesso no banco de dados!";
        _ = LimparAvisoHardwareAsync();
    }

    [RelayCommand]
    public void TestarImpressaoCupom()
    {
        var pedidoExemplo = new PedidoBalcao
        {
            NumeroComanda = "TEST-01",
            DataHora = DateTime.Now,
            ClienteNome = "TESTE DE IMPRESSÃO ESC/POS",
            ClienteCpf = "000.000.000-00",
            ValorTotal = 45.00m,
            Itens = 
            [
                new ItemPedidoBalcao { Quantidade = 1, PrecoUnitario = 30.00m, Produto = new Produto { Nome = "Item de Teste 80mm A" } },
                new ItemPedidoBalcao { Quantidade = 1, PrecoUnitario = 15.00m, Produto = new Produto { Nome = "Item de Teste 80mm B" } }
            ]
        };

        UltimoCupomTeste = _cupomService.GerarCupomComandaTexto(pedidoExemplo, nomeFantasia: $"TESTE ({ModeloImpressora})", largura: LarguraBobina);
        ModalCupomTesteVisivel = true;
        MensagemAvisoHardware = $"🖨️ Cupom de teste gerado com sucesso para {ModeloImpressora} ({LarguraBobina}) na porta {PortaComunicacao}!";
    }

    [RelayCommand]
    public void FecharModalCupomTeste()
    {
        ModalCupomTesteVisivel = false;
    }

    [RelayCommand]
    private async Task CarregarVendedoresAsync()
    {
        VendedoresLista.Clear();
        var lista = await _service.ObterVendedoresAsync();
        foreach (var v in lista) VendedoresLista.Add(v);
    }

    [RelayCommand]
    private async Task AdicionarVendedorAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoVendedorNome)) return;
        await _service.AdicionarVendedorAsync(NovoVendedorNome);

        NovoVendedorNome = string.Empty;
        MensagemAviso = "Funcionário habilitado com sucesso!";

        await CarregarVendedoresAsync();
        _ = LimparAvisoAsync();
    }

    private async Task LimparAvisoAsync()
    {
        await Task.Delay(3000);
        MensagemAviso = string.Empty;
    }

    private async Task LimparAvisoHardwareAsync()
    {
        await Task.Delay(4000);
        MensagemAvisoHardware = string.Empty;
    }
}
