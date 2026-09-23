using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;
using GetStartedApp.Services.Fiscal;
using GetStartedApp.Services.Impressao;

namespace GetStartedApp.ViewModels;

public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly PdvService _service;
    private readonly CupomTermicoService _cupomService = new();
    private readonly IFechamentoFiscalService? _fechamentoFiscalService;
    private readonly IBackupDatabaseService? _backupDatabaseService;

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

    // Automação Comercial, Balança de Gôndola e Pulso de Gaveta (REV-004)
    [ObservableProperty]
    public partial string ModoBalancaEtiqueta { get; set; } = "ValorTotal";

    public ObservableCollection<string> ModosBalancaEtiquetaDisponiveis { get; } = 
    [
        "ValorTotal",
        "PesoLiquido"
    ];

    [ObservableProperty]
    public partial bool AcionarGavetaAutomaticamente { get; set; } = true;

    [ObservableProperty]
    public partial bool UsarEmuladorBalanca { get; set; } = true;

    [ObservableProperty]
    public partial int TamanhoCodigoBalanca { get; set; } = 4;

    [ObservableProperty]
    public partial string MensagemAvisoHardware { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UltimoCupomTeste { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ModalCupomTesteVisivel { get; set; } = false;

    // === MÓDULO 2: FECHAMENTO FISCAL MENSAL (.ZIP CONTÁBIL) ===
    [ObservableProperty]
    public partial int MesFechamentoFiscal { get; set; } = DateTime.Now.Month;

    public ObservableCollection<int> MesesDisponiveis { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    [ObservableProperty]
    public partial int AnoFechamentoFiscal { get; set; } = DateTime.Now.Year;

    public ObservableCollection<int> AnosDisponiveis { get; } = [2024, 2025, 2026, 2027];

    [ObservableProperty]
    public partial string MensagemFechamentoFiscal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UltimoArquivoFechamento { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGerandoFechamento { get; set; } = false;

    // === MÓDULO 3: BACKUP E RESILIÊNCIA SQLITE ===
    [ObservableProperty]
    public partial string DataUltimoBackupTexto { get; set; } = "Nenhum backup recente";

    [ObservableProperty]
    public partial string TamanhoUltimoBackupTexto { get; set; } = "0 KB";

    [ObservableProperty]
    public partial int TotalBackupsArmazenados { get; set; } = 0;

    [ObservableProperty]
    public partial string MensagemBackupStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExecutandoBackup { get; set; } = false;

    public ObservableCollection<InformacaoBackupDto> ListaBackups { get; } = new();

    public ConfiguracoesViewModel(
        PdvService service,
        IFechamentoFiscalService? fechamentoFiscalService = null,
        IBackupDatabaseService? backupDatabaseService = null)
    {
        _service = service;
        _fechamentoFiscalService = fechamentoFiscalService;
        _backupDatabaseService = backupDatabaseService;
        _ = CarregarDadosIniciaisAsync();
    }

    public async Task CarregarDadosIniciaisAsync()
    {
        await CarregarVendedoresAsync();
        await CarregarConfiguracoesHardwareAsync();
        await CarregarStatusBackupAsync();
    }

    [RelayCommand]
    public async Task CarregarVendedoresAsync()
    {
        VendedoresLista.Clear();
        var lista = await _service.ObterVendedoresAsync();
        foreach (var v in lista)
        {
            VendedoresLista.Add(v);
        }
    }

    [RelayCommand]
    public async Task AdicionarVendedorAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoVendedorNome))
        {
            MensagemAviso = "Por favor, digite o nome do operador.";
            return;
        }

        await _service.AdicionarVendedorAsync(new Vendedor { Nome = NovoVendedorNome.Trim() });
        NovoVendedorNome = string.Empty;
        MensagemAviso = "Operador cadastrado com sucesso!";
        await CarregarVendedoresAsync();
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
        ModoBalancaEtiqueta = config.ModoBalancaEtiqueta;
        AcionarGavetaAutomaticamente = config.AcionarGavetaAutomaticamente;
        UsarEmuladorBalanca = config.UsarEmuladorBalanca;
        TamanhoCodigoBalanca = config.TamanhoCodigoBalanca;
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
            ModeloBalanca = ModeloBalanca,
            ModoBalancaEtiqueta = ModoBalancaEtiqueta,
            AcionarGavetaAutomaticamente = AcionarGavetaAutomaticamente,
            UsarEmuladorBalanca = UsarEmuladorBalanca,
            TamanhoCodigoBalanca = TamanhoCodigoBalanca
        };

        await _service.SalvarConfiguracaoTerminalAsync(config);
        MensagemAvisoHardware = "✅ Configurações de hardware, balança e gaveta salvas com sucesso no banco de dados!";
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
    public async Task GerarFechamentoFiscalAsync()
    {
        if (_fechamentoFiscalService == null)
        {
            MensagemFechamentoFiscal = "❌ Serviço de Fechamento Fiscal não configurado no container de injeção.";
            return;
        }

        IsGerandoFechamento = true;
        MensagemFechamentoFiscal = "Compilando XMLs fiscais e gerando resumo CSV...";

        try
        {
            var pastaDestino = Path.Combine(AppContext.BaseDirectory, "fechamentos_fiscais");
            var resumo = await _fechamentoFiscalService.GerarPacoteMensalAsync(AnoFechamentoFiscal, MesFechamentoFiscal, pastaDestino);

            UltimoArquivoFechamento = resumo.CaminhoArquivoZip;
            MensagemFechamentoFiscal = $"✅ Pacote gerado com sucesso!\n• Autorizadas: {resumo.TotalAutorizadas} | Canceladas: {resumo.TotalCanceladas}\n• Faturamento: R$ {resumo.FaturamentoTotal:N2} | Tributos Aprox: R$ {resumo.TotalImpostosAproximados:N2}\n• Arquivo: {resumo.CaminhoArquivoZip}";
        }
        catch (Exception ex)
        {
            MensagemFechamentoFiscal = $"⚠️ {ex.Message}";
        }
        finally
        {
            IsGerandoFechamento = false;
        }
    }

    [RelayCommand]
    public async Task ExecutarBackupManualAsync()
    {
        if (_backupDatabaseService == null)
        {
            MensagemBackupStatus = "❌ Serviço de Backup não configurado.";
            return;
        }

        IsExecutandoBackup = true;
        MensagemBackupStatus = "Executando snapshot atômico SQLite via VACUUM INTO...";

        try
        {
            var backup = await _backupDatabaseService.ExecutarBackupAsync("Painel_Manual");
            MensagemBackupStatus = $"✅ Backup criado com sucesso! Arquivo: {backup.NomeArquivo} ({backup.TamanhoBytes / 1024:N0} KB)";
            await CarregarStatusBackupAsync();
        }
        catch (Exception ex)
        {
            MensagemBackupStatus = $"❌ Falha ao criar backup: {ex.Message}";
        }
        finally
        {
            IsExecutandoBackup = false;
        }
    }

    [RelayCommand]
    public async Task LimparBackupsAntigosAsync()
    {
        if (_backupDatabaseService == null) return;

        try
        {
            var removidos = await _backupDatabaseService.LimparBackupsAntigosAsync(30);
            MensagemBackupStatus = $"🧹 Limpeza concluída: {removidos} arquivo(s) com mais de 30 dias foram removidos.";
            await CarregarStatusBackupAsync();
        }
        catch (Exception ex)
        {
            MensagemBackupStatus = $"❌ Falha na limpeza: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CarregarStatusBackupAsync()
    {
        if (_backupDatabaseService == null) return;

        try
        {
            var backups = await _backupDatabaseService.ListarBackupsExistentesAsync();
            ListaBackups.Clear();
            foreach (var b in backups)
            {
                ListaBackups.Add(b);
            }

            TotalBackupsArmazenados = backups.Count;
            var ultimo = backups.FirstOrDefault();
            if (ultimo != null)
            {
                DataUltimoBackupTexto = ultimo.DataHoraCriacao.ToString("dd/MM/yyyy HH:mm:ss");
                TamanhoUltimoBackupTexto = $"{ultimo.TamanhoBytes / 1024:N0} KB";
            }
            else
            {
                DataUltimoBackupTexto = "Nenhum backup recente";
                TamanhoUltimoBackupTexto = "0 KB";
            }
        }
        catch (Exception ex)
        {
            MensagemBackupStatus = $"Erro ao verificar backups: {ex.Message}";
        }
    }

    private async Task LimparAvisoHardwareAsync()
    {
        await Task.Delay(4000);
        MensagemAvisoHardware = string.Empty;
    }
}
