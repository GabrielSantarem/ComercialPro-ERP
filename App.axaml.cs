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
            collection.AddTransient<MainViewModel>();     // O Navigation Shell (Janela)
            collection.AddTransient<PdvViewModel>();      // A Pagina de Venda
            collection.AddTransient<DashboardViewModel>();// Dashboard
            collection.AddTransient<EstoqueViewModel>();
            collection.AddTransient<ConfiguracoesViewModel>();  // Estoque

            Services = collection.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainVm = Services.GetRequiredService<MainViewModel>();
                
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainVm
                };
                
                // Inicializamos o banco pela DI
                var pdvSvc = Services.GetRequiredService<PdvService>();
                _ = pdvSvc.InicializarBancoDadosAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Erro fatal durante a inicialização do programa.");
        }
    }
}