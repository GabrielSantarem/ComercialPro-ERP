using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GetStartedApp.Data;
using GetStartedApp.Models.Fiscal;
using GetStartedApp.Services;
using GetStartedApp.Services.Clientes;
using GetStartedApp.Services.Comercial;
using GetStartedApp.Services.Etiquetas;
using GetStartedApp.Services.Fiscal;
using GetStartedApp.Services.Hardware;
using GetStartedApp.Services.Impressao;
using GetStartedApp.Services.Inteligencia;
using GetStartedApp.ViewModels;
using GetStartedApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GetStartedApp;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // === 0. Configurar Serilog (Sistema de Logs) ===
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("logs/pdv_log_.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information(">>> INICIANDO APLICAÇÃO ERP / PDV DESKTOP AVALONIA (Fase 4: Hardware & Automação) <<<");

            var collection = new ServiceCollection();

            // Configurar ILogger para usar Serilog
            collection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Registrar ele mesmo pra as ViewModels pegarem serviços
            collection.AddSingleton<IServiceProvider>(sp => sp);

            collection.AddDbContext<AppDbContext>();
            collection.AddSingleton<IBackupDatabaseService, BackupDatabaseService>();
            collection.AddSingleton<PdvService>();
            collection.AddSingleton<NfeXmlParserService>();
            collection.AddSingleton<CupomTermicoService>();
            collection.AddSingleton<EstacaoKioskService>();

            // Módulo Fiscal NFC-e (Zeus Automação, DANFE A4 QuestPDF, Cancelamento e Fechamento Contábil)
            collection.AddSingleton<ConfiguracaoFiscalEmpresa>();
            collection.AddSingleton<DanfeNfceTermicaService>();
            collection.AddSingleton<DanfeA4PdfService>();
            collection.AddSingleton<NfceEmissaoService>();
            collection.AddSingleton<INfceCancelamentoService, NfceCancelamentoService>();
            collection.AddSingleton<IFechamentoFiscalService, FechamentoFiscalService>();

            // Módulos Comerciais, Clientes e Inteligência (REV-003)
            collection.AddSingleton<ITrocaDevolucaoService, TrocaDevolucaoService>();
            collection.AddSingleton<IClienteService, ClienteService>();
            collection.AddSingleton<IInteligenciaComercialService, InteligenciaComercialService>();

            // Automação Comercial, Hardware & Etiquetas (REV-004)
            collection.AddSingleton<IBalancaEtiquetaParserService, BalancaEtiquetaParserService>();
            collection.AddSingleton<IBalancaCheckoutService, BalancaMockService>();
            collection.AddSingleton<IGavetaDinheiroService, GavetaDinheiroService>();
            collection.AddSingleton<IEtiquetaGondolaService, EtiquetaGondolaService>();
            
            // Nossas ViewModels
            collection.AddTransient<MainViewModel>();           // O Navigation Shell (Janela)
            collection.AddTransient<HomeModulesViewModel>();    // A Tela Inicial (Hub de App)
            collection.AddTransient<BalcaoViewModel>();         // Terminal Balcao (Pre-Venda)
            collection.AddTransient<PdvViewModel>();            // Boca de Caixa (Recebimento)
            collection.AddTransient<DashboardViewModel>();      // Dashboard
            collection.AddTransient<EstoqueViewModel>();        // Estoque
            collection.AddTransient<EntradaNfeViewModel>();      // Entrada de Notas (NF-e)
            collection.AddTransient<FinanceiroViewModel>();      // Gestão Financeira / Contas a Pagar
            collection.AddTransient<ConfiguracoesViewModel>();  // Configuracoes

            Services = collection.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Auto-migrar e inicializar o banco de dados SQLite local no startup desktop
                try
                {
                    var pdvService = Services.GetRequiredService<PdvService>();
                    pdvService.InicializarBancoDadosAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "Falha crítica ao auto-migrar e inicializar o banco de dados SQLite no startup.");
                    throw;
                }

                var mainVm = Services.GetRequiredService<MainViewModel>();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainVm
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Erro fatal ao inicializar o contêiner de injeção de dependência do App.");
            throw;
        }
    }
}
