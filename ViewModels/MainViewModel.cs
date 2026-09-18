using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GetStartedApp.Models;
using GetStartedApp.Services;

namespace GetStartedApp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly EstacaoKioskService _kioskService;

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; } = null!;

    [ObservableProperty]
    private bool _isHome; // Propriedade para gerenciar o estado da navbar no topo

    // === ESTADO DO TERMINAL KIOSK ===
    [ObservableProperty] public partial string ModoTerminal { get; set; } = "GERENCIAL";
    [ObservableProperty] public partial string NomeTerminal { get; set; } = "TERMINAL-01";
    [ObservableProperty] public partial bool IsModoRestrito { get; set; } = false;
    [ObservableProperty] public partial string BadgeEstacaoTexto { get; set; } = string.Empty;

    // === MODAL DE AUTENTICAÇÃO / LIBERAÇÃO GERENCIAL ===
    [ObservableProperty] public partial bool ExibirModalAcessoGerencial { get; set; } = false;
    [ObservableProperty] public partial string SenhaGerencialDigitada { get; set; } = string.Empty;
    [ObservableProperty] public partial string MensagemErroAcesso { get; set; } = string.Empty;

    // Campos de reconfiguração de máquina dentro do modal
    [ObservableProperty] public partial string ModoParaConfigurar { get; set; } = "BALCAO";
    [ObservableProperty] public partial string NomeParaConfigurar { get; set; } = "BALCAO-01";
    [ObservableProperty] public partial string NovaSenhaMaster { get; set; } = string.Empty;
    [ObservableProperty] public partial bool PermissaoGerencialConcedida { get; set; } = false;

    // === MODAL DE AJUDA E ATALHOS UNIVERSAIS ===
    [ObservableProperty] public partial bool ExibirModalAjudaAtalhos { get; set; } = false;

    public ObservableCollection<string> ModosDisponiveis { get; } = ["BALCAO", "CAIXA", "GERENCIAL"];

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        _kioskService = _services.GetRequiredService<EstacaoKioskService>();
        
        AtualizarPropriedadesKiosk();
        InicializarPorModoEstacao();
    }

    private void AtualizarPropriedadesKiosk()
    {
        ModoTerminal = _kioskService.ModoAtual;
        NomeTerminal = _kioskService.NomeTerminal;
        IsModoRestrito = _kioskService.IsModoRestrito;

        ModoParaConfigurar = ModoTerminal;
        NomeParaConfigurar = NomeTerminal;

        BadgeEstacaoTexto = ModoTerminal switch
        {
            "BALCAO" => $"🛒 BALCÃO ({NomeTerminal}) • Restrito [F12]",
            "CAIXA" => $"💳 CAIXA ({NomeTerminal}) • Restrito [F12]",
            _ => $"👑 GERENCIAL ({NomeTerminal}) • Livre [F12]"
        };
    }

    private void InicializarPorModoEstacao()
    {
        switch (_kioskService.ModoAtual)
        {
            case "BALCAO":
                NavigateToBalcao();
                break;
            case "CAIXA":
                NavigateToPdv();
                break;
            default:
                NavigateToHome();
                break;
        }
    }

    // === COMANDOS DE NAVEGAÇÃO ENTRE MÓDULOS ===

    [RelayCommand]
    public void SolicitarRetornoAoHub()
    {
        if (ModoTerminal == "GERENCIAL" || PermissaoGerencialConcedida)
        {
            NavigateToHome();
            return;
        }

        // Se estiver em modo restrito (Balcão ou Caixa), exige senha master do gerente
        SenhaGerencialDigitada = string.Empty;
        MensagemErroAcesso = string.Empty;
        PermissaoGerencialConcedida = false;
        ExibirModalAcessoGerencial = true;
    }

    [RelayCommand]
    public void ConfirmarAcessoGerencial()
    {
        if (string.IsNullOrWhiteSpace(SenhaGerencialDigitada))
        {
            MensagemErroAcesso = "⚠️ Digite a senha master do gerente!";
            return;
        }

        if (!_kioskService.ValidarSenhaMaster(SenhaGerencialDigitada))
        {
            MensagemErroAcesso = "❌ Senha master incorreta!";
            return;
        }

        MensagemErroAcesso = string.Empty;
        PermissaoGerencialConcedida = true;
        ExibirModalAcessoGerencial = false;
        NavigateToHome();
    }

    [RelayCommand]
    public void AbrirConfiguracaoTerminal()
    {
        SenhaGerencialDigitada = string.Empty;
        MensagemErroAcesso = string.Empty;
        PermissaoGerencialConcedida = ModoTerminal == "GERENCIAL";
        ExibirModalAcessoGerencial = true;
    }

    [RelayCommand]
    public void SalvarConfiguracaoEstacao()
    {
        if (!PermissaoGerencialConcedida && !_kioskService.ValidarSenhaMaster(SenhaGerencialDigitada))
        {
            MensagemErroAcesso = "❌ Senha master incorreta para alterar a estação!";
            return;
        }

        var novaSenha = string.IsNullOrWhiteSpace(NovaSenhaMaster) 
            ? _kioskService.Configuracao.SenhaMaster 
            : NovaSenhaMaster.Trim();

        var config = new ConfiguracaoEstacao
        {
            ModoEstacao = ModoParaConfigurar,
            NomeTerminal = string.IsNullOrWhiteSpace(NomeParaConfigurar) ? "TERMINAL-01" : NomeParaConfigurar.Trim().ToUpper(),
            SenhaMaster = novaSenha
        };

        _kioskService.SalvarConfiguracao(config);
        AtualizarPropriedadesKiosk();
        ExibirModalAcessoGerencial = false;
        PermissaoGerencialConcedida = false;

        InicializarPorModoEstacao();
    }

    [RelayCommand]
    public void FecharModalAcesso()
    {
        ExibirModalAcessoGerencial = false;
        SenhaGerencialDigitada = string.Empty;
        MensagemErroAcesso = string.Empty;
    }

    [RelayCommand]
    public void ToggleAjudaAtalhos()
    {
        ExibirModalAjudaAtalhos = !ExibirModalAjudaAtalhos;
    }

    // === NAVEGAÇÃO INTERNA ===

    [RelayCommand]
    public void NavigateToHome()
    {
        var vm = _services.GetRequiredService<HomeModulesViewModel>();
        CurrentPage = vm;
        IsHome = true;
    }

    [RelayCommand]
    public void NavigateToBalcao()
    {
        var vm = _services.GetRequiredService<BalcaoViewModel>();
        _ = vm.InicializarAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToPdv() 
    {
        var vm = _services.GetRequiredService<PdvViewModel>();
        _ = vm.InicializarAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToDashboard() 
    {
        var vm = _services.GetRequiredService<DashboardViewModel>();
        _ = vm.CarregarMetricasAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToEstoque() 
    {
        var vm = _services.GetRequiredService<EstoqueViewModel>();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToEntradaNfe()
    {
        var vm = _services.GetRequiredService<EntradaNfeViewModel>();
        _ = vm.CarregarCatalogoAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToFinanceiro()
    {
        var vm = _services.GetRequiredService<FinanceiroViewModel>();
        _ = vm.InicializarAsync();
        CurrentPage = vm;
        IsHome = false;
    }

    [RelayCommand]
    public void NavigateToConfiguracoes() 
    {
        var vm = _services.GetRequiredService<ConfiguracoesViewModel>();
        CurrentPage = vm;
        IsHome = false;
    }
}
