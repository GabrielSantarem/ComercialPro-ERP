using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GetStartedApp.Data;
using GetStartedApp.Services;
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
            Log.Information("Iniciando Aplicação Comercial Pro ERP...");

            // 1. Configurar injeção de dependência (DI)!
            var collection = new ServiceCollection();
            
            // Plugar o Serilog na injeção de dependências do .NET
            collection.AddLogging(builder => 
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Registrar ele mesmo pra as ViewModels pegarem serviços
            collection.AddSingleton<IServiceProvider>(sp => sp);

            collection.AddDbContext<AppDbContext>();
            collection.AddSingleton<PdvService>();
            
            // Nossas ViewModels
            collection.AddTransient<MainViewModel>();           // O Navigation Shell (Janela)
            collection.AddTransient<HomeModulesViewModel>();    // A Tela Inicial (Hub de App)
            collection.AddTransient<PdvViewModel>();            // A Pagina de Venda
            collection.AddTransient<DashboardViewModel>();      // Dashboard
            collection.AddTransient<EstoqueViewModel>();        // Estoque
            collection.AddTransient<EntradaNfeViewModel>();      // Entrada de Notas (NF-e)
            collection.AddTransient<ConfiguracoesViewModel>();  // Configuracoes

            Services = collection.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Em vez de iniciar o Main abrindo rasgado, nós puxamos a nossa Tela de Início (Splash Screen)
                // Ela fará o trabalho interno de configurar o BD e carregar a janela mestre.
                desktop.MainWindow = new SplashWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Erro fatal durante a inicialização do programa.");
        }
    }
}
